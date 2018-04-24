using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Core.Objects;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using ClosedXML.Excel;
using Ionic.Zip;
using Neodynamic.SDK.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QDLogistics.Commons;
using QDLogistics.Filters;
using QDLogistics.Models;
using QDLogistics.Models.Object;
using QDLogistics.Models.Repositiry;
using QDLogistics.OrderService;
using SellerCloud_WebService;

namespace QDLogistics.Controllers
{
    public class DirectLineController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<Orders> Orders;
        private IRepository<Addresses> Addresses;
        private IRepository<Payments> Payments;
        private IRepository<Packages> Packages;
        private IRepository<Items> Items;
        private IRepository<Box> Box;
        private IRepository<DirectLineLabel> Label;
        private IRepository<Warehouses> Warehouses;
        private IRepository<ShippingMethod> Method;
        private IRepository<PickProduct> PickProduct;
        private IRepository<SerialNumbers> SerialNumbers;

        public DirectLineController()
        {
            db = new QDLogisticsEntities();
        }

        [CheckSession]
        public ActionResult Waiting()
        {
            return View();
        }

        [CheckSession]
        public ActionResult Package()
        {
            int warehouseID;
            List<ShippingMethod> MethodList = new List<ShippingMethod>();

            if (int.TryParse(Session["warehouseId"].ToString(), out warehouseID))
            {
                Warehouses = new GenericRepository<Warehouses>(db);

                Warehouses warehouse = Warehouses.Get(warehouseID);
                if (warehouse != null && !string.IsNullOrEmpty(warehouse.CarrierData))
                {
                    Method = new GenericRepository<ShippingMethod>(db);

                    Dictionary<int, bool> methodData = JsonConvert.DeserializeObject<Dictionary<int, bool>>(warehouse.CarrierData);
                    MethodList = Method.GetAll(true).Where(m => m.IsEnable && methodData.Keys.Contains(m.ID) && methodData[m.ID]).ToList();
                }
            }

            ViewBag.directLineList = db.DirectLine.AsNoTracking().Where(d => d.IsEnable).ToList();
            ViewBag.methodList = MethodList;
            ViewBag.WCPScript = WebClientPrint.CreateScript(Url.Action("ProcessRequest", "WebClientPrintAPI", null, HttpContext.Request.Url.Scheme), Url.Action("PrintFile", "File", null, HttpContext.Request.Url.Scheme), HttpContext.Session.SessionID);
            ViewData["warehouseId"] = (int)Session["WarehouseID"];
            ViewData["adminId"] = (int)Session["AdminId"];
            ViewData["adminName"] = Session["AdminName"].ToString();
            return View();
        }

        [CheckSession]
        public ActionResult Delivery()
        {
            return View();
        }

        public ActionResult BoxEdit(string id)
        {
            if (!MyHelp.CheckAuth("directLine", "delivery", EnumData.AuthType.Edit)) return RedirectToAction("index", "main");

            Box = new GenericRepository<Box>(db);

            Box box = Box.Get(id);

            if (box == null) return RedirectToAction("delivery", "directLine");

            ViewData["Carrier"] = db.ShippingMethod.AsNoTracking().First(m => m.IsEnable && m.ID.Equals(box.FirstMileMethod)).Carriers.Name;

            ViewBag.logList = db.ActionLog.AsNoTracking().Where(log => log.TableName.Equals("Box") && log.TargetID.Equals(box.BoxID)).OrderBy(log => log.CreateDate).ToList();

            return View(box);
        }

        public ActionResult GetWaitingData(DataFilter filter, int page = 1, int rows = 100)
        {
            int total = 0;
            List<object> dataList = new List<object>();

            /** Order Filter **/
            var OrderFilter = db.Orders.AsNoTracking().Where(o => !o.StatusCode.Value.Equals((int)OrderStatusCode.Completed) && o.PaymentStatus.Value.Equals((int)OrderPaymentStatus2.Charged));
            if (!filter.StatusCode.Equals(null)) OrderFilter = OrderFilter.Where(o => o.StatusCode.Value.Equals(filter.StatusCode.Value));
            if (!string.IsNullOrWhiteSpace(filter.OrderID)) OrderFilter = OrderFilter.Where(o => o.OrderID.ToString().Equals(filter.OrderID) || (o.OrderSource.Value.Equals(1) && o.eBaySalesRecordNumber.Equals(filter.OrderID)) || (o.OrderSource.Value.Equals(4) && o.OrderSourceOrderId.Equals(filter.OrderID)));

            /** Package Filter **/
            var PackageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨));
            if (!filter.MethodID.Equals(null)) PackageFilter = PackageFilter.Where(p => p.ShippingMethod.Value.Equals(filter.MethodID.Value));
            if (!filter.Export.Equals(null)) PackageFilter = PackageFilter.Where(p => p.Export.Value.Equals(filter.Export.Value));
            if (!filter.ExportMethod.Equals(null)) PackageFilter = PackageFilter.Where(p => p.ExportMethod.Value.Equals(filter.ExportMethod.Value));
            if (!string.IsNullOrWhiteSpace(filter.Comment)) PackageFilter = PackageFilter.Where(p => p.Comment.ToLower().Contains(filter.Comment.ToLower()));
            if (!string.IsNullOrWhiteSpace(filter.TagNo)) PackageFilter = PackageFilter.Where(p => p.TagNo.ToLower().Contains(filter.TagNo.ToLower()));
            if (!string.IsNullOrWhiteSpace(filter.Tracking)) PackageFilter = PackageFilter.Where(p => p.TrackingNumber.ToLower().Contains(filter.Tracking.ToLower()));
            if (!filter.DispatchDate.Equals(new DateTime()))
            {
                DateTime dateFrom = new DateTime(filter.DispatchDate.Year, filter.DispatchDate.Month, filter.DispatchDate.Day, 0, 0, 0);
                dateFrom = new TimeZoneConvert(dateFrom, MyHelp.GetTimeZone((int)Session["TimeZone"])).ConvertDateTime(EnumData.TimeZone.EST);
                DateTime dateTo = new DateTime(filter.DispatchDate.Year, filter.DispatchDate.Month, filter.DispatchDate.Day + 1, 0, 0, 0);
                dateTo = new TimeZoneConvert(dateTo, MyHelp.GetTimeZone((int)Session["TimeZone"])).ConvertDateTime(EnumData.TimeZone.EST);
                PackageFilter = PackageFilter.Where(p => DateTime.Compare(p.ShipDate.Value, dateFrom) >= 0 && DateTime.Compare(p.ShipDate.Value, dateTo) < 0);
            }

            /** Item Filter **/
            int warehouseID = 0;
            int.TryParse(Session["warehouseId"].ToString(), out warehouseID);
            var ItemFilter = db.Items.AsNoTracking().Where(i => i.IsEnable.Value && i.ShipFromWarehouseID.Value.Equals(warehouseID));
            if (!string.IsNullOrWhiteSpace(filter.Sku)) ItemFilter = ItemFilter.Where(i => i.ProductID.ToLower().Contains(filter.Sku.ToLower()));
            if (!string.IsNullOrWhiteSpace(filter.ItemName)) ItemFilter = ItemFilter.Where(i => i.DisplayName.ToLower().Contains(filter.ItemName.ToLower()));
            if (!filter.WarehouseID.Equals(null)) ItemFilter = ItemFilter.Where(i => i.ShipFromWarehouseID.Value.Equals(filter.WarehouseID.Value));

            /** Address Filter **/
            var AddressFilter = db.Addresses.AsNoTracking().Where(a => a.IsEnable.Value);
            if (!string.IsNullOrWhiteSpace(filter.CountryCode)) AddressFilter = AddressFilter.Where(a => a.CountryCode.Equals(filter.CountryCode));

            /** Shipping Method Filter **/
            var MethodFilter = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && m.IsDirectLine);

            var results = OrderFilter.ToList()
                .Join(PackageFilter, o => o.OrderID, p => p.OrderID, (o, p) => new OrderJoinData() { order = o, package = p })
                .Join(ItemFilter.GroupBy(i => i.PackageID.Value), oData => oData.package.ID, i => i.Key, (oData, i) => new OrderJoinData(oData) { item = i.First(), items = i.ToList(), itemCount = i.Sum(ii => 1 + ii.KitItemCount).Value })
                .Join(AddressFilter, oData => oData.order.ShippingAddress, a => a.Id, (oData, a) => new OrderJoinData(oData) { address = a })
                .Join(MethodFilter, oData => oData.package.ShippingMethod, m => m.ID, (oData, m) => new OrderJoinData(oData) { method = m }).ToList();

            /** Payment Filter **/
            var PaymentFilter = db.Payments.AsNoTracking().Where(p => p.IsEnable.Value && p.PaymentType.Value.Equals((int)PaymentRecordType.Payment));
            if (!filter.PaymentDate.Equals(new DateTime()))
            {
                DateTime dateFrom = new DateTime(filter.PaymentDate.Year, filter.PaymentDate.Month, filter.PaymentDate.Day, 0, 0, 0);
                dateFrom = new TimeZoneConvert(dateFrom, MyHelp.GetTimeZone((int)Session["TimeZone"])).ConvertDateTime(EnumData.TimeZone.EST);
                DateTime dateTo = new DateTime(filter.PaymentDate.Year, filter.PaymentDate.Month, filter.PaymentDate.Day + 1, 0, 0, 0);
                dateTo = new TimeZoneConvert(dateTo, MyHelp.GetTimeZone((int)Session["TimeZone"])).ConvertDateTime(EnumData.TimeZone.EST);
                PaymentFilter = PaymentFilter.Where(p => DateTime.Compare(p.AuditDate.Value, dateFrom) >= 0 && DateTime.Compare(p.AuditDate.Value, dateTo) < 0);

                results = results.Join(PaymentFilter, oData => oData.order.OrderID, p => p.OrderID, (oData, p) => new OrderJoinData(oData) { payment = p }).ToList();
            }
            else
            {
                results = results.GroupJoin(PaymentFilter, oData => oData.order.OrderID, p => p.OrderID.Value, (oData, p) => new { orderJoinData = oData, payment = p.Take(1) })
                    .SelectMany(o => o.payment.DefaultIfEmpty(), (o, p) => new OrderJoinData(o.orderJoinData) { payment = p }).ToList();
            }

            if (results.Any())
            {
                int length = rows;
                int start = (page - 1) * length;
                total = results.Count();
                results = results.OrderByDescending(data => data.order.TimeOfOrder).Skip(start).Take(length).ToList();

                Warehouses = new GenericRepository<Warehouses>(db);

                TimeZoneConvert timeZoneConvert = new TimeZoneConvert();
                EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);

                Dictionary<int, string> warehouses = db.Warehouses.AsNoTracking().Where(w => w.IsSellable.Value).ToDictionary(w => w.ID, w => w.Name);

                dataList.AddRange(results.Select(data => new
                {
                    PackageID = data.package.ID,
                    OrderID = data.package.OrderID,
                    ParentOrderID = data.order.ParentOrderID,
                    OrderSourceOrderId = data.order.OrderSourceOrderId,
                    PaymentDate = data.payment != null ? timeZoneConvert.InitDateTime(data.payment.AuditDate.Value, EnumData.TimeZone.EST).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy<br />hh:mm tt") : "",
                    Sku = data.itemCount == 1 ? data.item.ProductID : "Multi",
                    DisplayName = data.itemCount == 1 ? data.item.DisplayName : "Multi",
                    ItemCount = data.itemCount,
                    Qty = data.items.Sum(i => i.Qty),
                    ShippingCountry = data.address.CountryName,
                    Warehouse = warehouses[data.item.ShipFromWarehouseID.Value],
                    ShippingMethod = data.method.Name,
                    Export = Enum.GetName(typeof(EnumData.Export), data.package.Export != null ? data.package.Export : 0),
                    ExportMethod = Enum.GetName(typeof(EnumData.ExportMethod), data.package.ExportMethod != null ? data.package.ExportMethod : 0),
                    StatusCode = Enum.GetName(typeof(OrderStatusCode), data.order.StatusCode),
                    OrderSatusCode = data.order.StatusCode,
                    Comment = data.package.Comment,
                    Confirmed = data.order.IsConfirmed,
                    DispatchDate = timeZoneConvert.InitDateTime(data.package.ShipDate.Value, EnumData.TimeZone.UTC).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy<br />hh:mm tt"),
                    TagNo = data.package.TagNo,
                    TrackingNumber = data.package.TrackingNumber
                }));
            }

            return Json(new { total, rows = dataList }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult PackagePickUpList()
        {
            int warehouseId = int.Parse(Session["warehouseId"].ToString());

            var PackageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨) && string.IsNullOrEmpty(p.BoxID));

            string packageSelect = string.Format("SELECT * FROM Packages WHERE IsEnable = 1 AND ProcessStatus = {0}", (byte)EnumData.ProcessStatus.待出貨);
            string orderSelect = string.Format("SELECT * FROM Orders WHERE StatusCode = {0}", (int)OrderStatusCode.InProcess);
            string itemSelect = string.Format("SELECT * FROM Items WHERE IsEnable = 1 AND ShipFromWarehouseID = {0}", warehouseId);

            var methodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && m.IsDirectLine).ToList();

            ObjectContext context = new ObjectContext("name=QDLogisticsEntities");
            var ItemList = PackageFilter.ToList().Join(methodList, p => p.ShippingMethod, m => m.ID, (p, m) => p)
                .Join(context.ExecuteStoreQuery<Orders>(orderSelect).ToList(), p => p.OrderID, o => o.OrderID, (package, order) => new { order, package })
                .OrderBy(data => data.order.TimeOfOrder).OrderByDescending(data => data.order.RushOrder)
                .Join(db.Items.AsNoTracking().Where(i => i.IsEnable.Value && i.ShipFromWarehouseID.Value.Equals(warehouseId)), op => op.package.ID, i => i.PackageID, (op, item) => item).Distinct()
                .GroupBy(i => i.PackageID).ToList();

            ViewBag.itemList = ItemList;
            return PartialView("~/Views/Ajax/_PickUpList.cshtml");
        }

        [HttpPost]
        public ActionResult PrintPickUpList(int warehouseId, int adminId)
        {
            IRepository<AdminUsers> AdminUsers = new GenericRepository<AdminUsers>(db);

            AdminUsers admin = AdminUsers.Get(adminId);

            var PackageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨) && string.IsNullOrEmpty(p.BoxID));

            string packageSelect = string.Format("SELECT * FROM Packages WHERE IsEnable = 1 AND ProcessStatus = {0}", (byte)EnumData.ProcessStatus.待出貨);
            string orderSelect = string.Format("SELECT * FROM Orders WHERE StatusCode = {0}", (int)OrderStatusCode.InProcess);
            string itemSelect = string.Format("SELECT * FROM Items WHERE IsEnable = 1 AND ShipFromWarehouseID = {0}", warehouseId);

            var methodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && m.IsDirectLine).ToList();

            ObjectContext context = new ObjectContext("name=QDLogisticsEntities");
            List<Items> itemList = PackageFilter.ToList().Join(methodList, p => p.ShippingMethod, m => m.ID, (p, m) => p)
                .Join(context.ExecuteStoreQuery<Orders>(orderSelect).ToList(), p => p.OrderID, o => o.OrderID, (package, order) => new { order, package })
                .OrderBy(data => data.order.TimeOfOrder).OrderByDescending(data => data.order.RushOrder)
                .Join(db.Items.AsNoTracking().Where(i => i.IsEnable.Value && i.ShipFromWarehouseID.Value.Equals(warehouseId)), op => op.package.ID, i => i.PackageID, (op, item) => item).Distinct().ToList();

            string basePath = HostingEnvironment.MapPath("~/FileUploads");
            string DirPath = Path.Combine(basePath, "pickup");
            if (!Directory.Exists(DirPath)) Directory.CreateDirectory(DirPath);
            string[] fileName = new string[2];
            string[] filePath = new string[2];

            List<IGrouping<int?, Items>> itemGroupList = itemList.GroupBy(i => i.PackageID).ToList();

            XLWorkbook workbook1 = new XLWorkbook();
            if (SetWorkSheet(workbook1, "單項產品", itemGroupList.Where(i => i.Sum(ii => ii.Qty) == 1).ToList(), admin.Name))
            {
                fileName[0] = "Box-Single.xlsx";
                filePath[0] = Path.Combine(DirPath, fileName[0]);
                workbook1.SaveAs(filePath[0]);
            }

            XLWorkbook workbook2 = new XLWorkbook();
            if (SetWorkSheet(workbook2, "多項產品", itemGroupList.Where(i => i.Sum(ii => ii.Qty) > 1).ToList(), admin.Name))
            {
                fileName[1] = "Box-Multiple.xlsx";
                filePath[1] = Path.Combine(DirPath, fileName[1]);
                workbook2.SaveAs(filePath[1]);
            }

            return Content(JsonConvert.SerializeObject(new { status = true, filePath = filePath, fileName = fileName, amount = new int[] { 1, 1 } }), "appllication /json");
        }
        private bool SetWorkSheet(XLWorkbook workbook, string sheetName, List<IGrouping<int?, Items>> itemGroupList, string adminName)
        {
            if (itemGroupList.Any())
            {
                JArray jObjects = new JArray();

                switch (sheetName)
                {
                    case "單項產品":
                        var singleList = itemGroupList.SelectMany(i => i).OrderBy(i => i.ProductID).ToList();

                        foreach (Items item in singleList)
                        {
                            JObject jo = new JObject();
                            jo.Add("ProductID", item.ProductID);
                            jo.Add("ProductName", item.Skus.ProductName);
                            jo.Add("Qty", item.Qty);
                            jo.Add("Warehouse", item.ShipWarehouses.Name);
                            jo.Add("PickUpDate", DateTime.Today);
                            jo.Add("PickUpBy", adminName);
                            jo.Add("Check", "□");
                            jObjects.Add(jo);
                        }
                        break;
                    case "多項產品":
                        int I = 0;
                        var multipleList = itemGroupList
                            .ToDictionary(i => Convert.ToChar(65 + (I / 9 % 26)) + (I++ % 9 + 1).ToString(), i => i)
                            .SelectMany(i => i.Value.Select(ii => new { block = i.Key, data = ii })).OrderBy(i => i.data.ProductID).ToList();

                        foreach (var item in multipleList)
                        {
                            JObject jo = new JObject();
                            jo.Add("ProductID", item.data.ProductID);
                            jo.Add("ProductName", item.data.Skus.ProductName);
                            jo.Add("Qty", item.data.Qty);
                            jo.Add("Warehouse", item.data.ShipWarehouses.Name);
                            jo.Add("PickUpDate", DateTime.Today);
                            jo.Add("PickUpBy", adminName);
                            jo.Add("Block", item.block);
                            jo.Add("Check", "□");
                            jObjects.Add(jo);
                        }
                        break;
                }

                var sheet = workbook.Worksheets.Add(JsonConvert.DeserializeObject<DataTable>(jObjects.ToString()), sheetName);
                sheet.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                sheet.Style.Font.FontName = "新細明體";
                sheet.Style.Font.FontSize = 14;
                sheet.Column(1).Width = sheet.Column(4).Width = sheet.Column(5).Width = sheet.Column(6).Width = 12;
                sheet.Column(2).Width = 80;
                sheet.Column(3).Width = 5;
                sheet.Column(7).Width = 6;
                if (sheetName == "多項產品") sheet.Column(8).Width = 6;
                sheet.Column(2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                sheet.Cell(1, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
                sheet.PageSetup.PagesWide = 1;
                sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
                sheet.PageSetup.Margins.Top = 0;
                sheet.PageSetup.Margins.Bottom = 0;
                sheet.PageSetup.Margins.Left = 0;
                sheet.PageSetup.Margins.Right = 0;
                sheet.PageSetup.Margins.Footer = 0;
                sheet.PageSetup.Margins.Header = 0;
                sheet.PageSetup.CenterHorizontally = true;

                return true;
            }

            return false;
        }

        public ActionResult GetBoxData(BoxFilter filter, int page = 1, int rows = 100)
        {
            int total = 0;
            List<object> dataList = new List<object>();

            var BoxFilter = db.Box.AsNoTracking().Where(b => b.IsEnable);
            if (!string.IsNullOrEmpty(filter.BoxID)) BoxFilter = BoxFilter.Where(b => b.BoxID.Contains(filter.BoxID));
            if (!string.IsNullOrEmpty(filter.SupplierBoxID)) BoxFilter = BoxFilter.Where(b => b.SupplierBoxID.Contains(filter.SupplierBoxID));
            if (!filter.CreateDate.Equals(new DateTime()))
            {
                DateTime dateFrom = new DateTime(filter.CreateDate.Year, filter.CreateDate.Month, filter.CreateDate.Day, 0, 0, 0);
                dateFrom = new TimeZoneConvert(dateFrom, MyHelp.GetTimeZone((int)Session["TimeZone"])).ConvertDateTime(EnumData.TimeZone.EST);
                DateTime dateTo = new DateTime(filter.CreateDate.Year, filter.CreateDate.Month, filter.CreateDate.Day + 1, 0, 0, 0);
                dateTo = new TimeZoneConvert(dateTo, MyHelp.GetTimeZone((int)Session["TimeZone"])).ConvertDateTime(EnumData.TimeZone.EST);
                BoxFilter = BoxFilter.Where(b => DateTime.Compare(b.Create_at, dateFrom) >= 0 && DateTime.Compare(b.Create_at, dateTo) < 0);
            }

            int warehouseID = 0;
            if (int.TryParse(Session["warehouseId"].ToString(), out warehouseID)) BoxFilter = BoxFilter.Where(b => b.WarehouseFrom.Equals(warehouseID));
            if (!filter.Warehouse.Equals(null)) BoxFilter = BoxFilter.Where(b => b.WarehouseTo.Equals(filter.Warehouse.Value));
            if (!string.IsNullOrEmpty(filter.WITID)) BoxFilter = BoxFilter.Where(b => b.WITID.Contains(filter.WITID));
            if (!string.IsNullOrEmpty(filter.Tracking)) BoxFilter = BoxFilter.Where(b => b.TrackingNumber.Contains(filter.Tracking));
            if (!filter.Status.Equals(null)) BoxFilter = BoxFilter.Where(b => b.ShippingStatus.Equals(filter.Status.Value));
            if (!filter.Type.Equals(null)) BoxFilter = BoxFilter.Where(b => b.BoxType.Equals(filter.Type.Value));
            if (!string.IsNullOrEmpty(filter.Notes)) BoxFilter = BoxFilter.Where(b => b.Note.Contains(filter.Notes));

            var results = BoxFilter.ToList();
            if (results.Any())
            {
                int length = rows;
                int start = (page - 1) * length;
                total = results.Count();
                results = results.OrderByDescending(b => b.Create_at).Skip(start).Take(length).ToList();

                TimeZoneConvert timeZoneConvert = new TimeZoneConvert();
                EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);

                string[] boxIDs = results.Select(b => b.BoxID).ToArray();
                Dictionary<string, List<Items>> itemGroup = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && boxIDs.Contains(p.BoxID)).ToList()
                    .GroupJoin(db.Items.AsNoTracking().Where(i => i.IsEnable.Value), p => p.ID, i => i.PackageID, (p, i) => new { package = p, items = i.ToList() })
                    .GroupBy(data => data.package.BoxID).ToDictionary(group => group.Key, group => group.SelectMany(data => data.items).ToList());

                dataList.AddRange(results.Select(data => new
                {
                    data.BoxID,
                    data.SupplierBoxID,
                    CreateDate = timeZoneConvert.InitDateTime(data.Create_at, EnumData.TimeZone.UTC).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy<br />hh:mm tt"),
                    WharehouseTO = "",
                    BoxQty = data.BoxNo,
                    TotalWeight = itemGroup.Any(i => i.Key.Equals(data.BoxID)) ? itemGroup[data.BoxID].Sum(i => i.Qty * ((float)i.Skus.Weight / 1000)) : 0,
                    data.WITID,
                    Tracking = data.TrackingNumber,
                    Status = Enum.GetName(typeof(EnumData.DirectLineStatus), data.ShippingStatus),
                    Type = EnumData.BoxTypeList()[(EnumData.DirectLineBoxType)data.BoxType],
                    data.Note
                }));
            }

            return Json(new { total, rows = dataList }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetBoxOrderData(string boxID, int page = 1, int rows = 100)
        {
            int total = 0;
            List<object> dataList = new List<object>();

            var OrderFilter = db.Orders.AsNoTracking();
            var PackageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.BoxID.Equals(boxID));
            var ItemFilter = db.Items.AsNoTracking().Where(i => i.IsEnable.Value);

            var results = PackageFilter.ToList()
                .Join(OrderFilter, p => p.OrderID.Value, o => o.OrderID, (p, o) => new OrderJoinData() { order = o, package = p })
                .Join(ItemFilter.GroupBy(i => i.PackageID.Value), oData => oData.package.ID, i => i.Key, (oData, i) => new OrderJoinData(oData) { item = i.First(), items = i.ToList(), itemCount = i.Sum(ii => 1 + ii.KitItemCount).Value }).ToList();

            if (results.Any())
            {
                int length = rows;
                int start = (page - 1) * length;
                total = results.Count();
                results = results.OrderByDescending(data => data.order.OrderID).Skip(start).Take(length).ToList();

                dataList.AddRange(results.Select(data => new
                {
                    PackageID = data.package.ID,
                    ProductID = data.itemCount == 1 ? data.item.ProductID : "Multi",
                    ProductName = data.itemCount == 1 ? data.item.DisplayName : "Multi",
                    SentQty = data.itemCount,
                    ReceivedQty = 0,
                    Weight = data.items.Sum(i => i.Skus.Weight * i.Qty.Value / 1000),
                    data.order.OrderID,
                    Serial = data.itemCount == 1 && data.item.SerialNumbers.Any() ? data.item.SerialNumbers.First().SerialNumber : "Multi",
                    LabelID = data.package.TagNo,
                    data.order.StatusCode
                }));
            }

            return Json(new { total, rows = dataList }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetSelectOption(List<string> optionType)
        {
            AjaxResult result = new AjaxResult();

            try
            {
                if (!optionType.Any()) throw new Exception("沒有給項目!");

                Dictionary<string, object> optionList = new Dictionary<string, object>();

                foreach (string type in optionType)
                {
                    switch (type)
                    {
                        case "CountryCode":
                            optionList.Add(type, MyHelp.GetCountries().Select(c => new { text = c.Name, value = c.ID }).ToArray());
                            break;
                        case "Warehouse":
                            Warehouses = new GenericRepository<Warehouses>(db);
                            optionList.Add(type, Warehouses.GetAll(true).Where(w => w.IsEnable.Value && w.IsSellable.Value).Select(w => new { text = w.Name, value = w.ID }).ToArray());
                            break;
                        case "Method":
                            Method = new GenericRepository<ShippingMethod>(db);
                            optionList.Add(type, Method.GetAll(true).Where(m => m.IsEnable && m.IsDirectLine).Select(m => new { text = m.Name, value = m.ID }).ToArray());
                            break;
                        case "StatusCode":
                            optionList.Add(type, Enum.GetValues(typeof(OrderStatusCode)).Cast<OrderStatusCode>().Select(s => new { text = s.ToString(), value = (int)s }).ToArray());
                            break;
                        case "Export":
                            optionList.Add(type, Enum.GetValues(typeof(EnumData.Export)).Cast<EnumData.Export>().Select(e => new { text = e.ToString(), value = (byte)e }).ToArray());
                            break;
                        case "ExportMethod":
                            optionList.Add(type, Enum.GetValues(typeof(EnumData.ExportMethod)).Cast<EnumData.ExportMethod>().Select(e => new { text = e.ToString(), value = (byte)e }).ToArray());
                            break;
                        case "DirectLineStatus":
                            optionList.Add(type, Enum.GetValues(typeof(EnumData.DirectLineStatus)).Cast<EnumData.DirectLineStatus>().Select(s => new { text = s.ToString(), value = (byte)s }).ToArray());
                            break;
                        case "DirectLineBoxType":
                            optionList.Add(type, Enum.GetValues(typeof(EnumData.DirectLineBoxType)).Cast<EnumData.DirectLineBoxType>().Select(t => new { text = EnumData.BoxTypeList()[t], value = (byte)t }).ToArray());
                            break;
                    }
                }

                result.data = optionList;
            }
            catch (Exception e)
            {
                result.status = false;
                result.message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult ChangeOrderStatus(int packageID, int status)
        {
            AjaxResult result = new AjaxResult();

            Packages = new GenericRepository<Packages>(db);
            Box = new GenericRepository<Box>(db);

            Packages package = Packages.Get(packageID);

            try
            {
                MyHelp.Log("Orders", package.OrderID.Value, string.Format("Order【{0}】訂單狀態更改", package.OrderID.Value), Session);

                if (!package.Method.IsDirectLine) throw new Exception("非Direct Line訂單");

                switch (package.ProcessStatus)
                {
                    case (byte)EnumData.ProcessStatus.待出貨:
                        if (package.Orders.StatusCode.Equals((int)OrderStatusCode.OnHold) && status.Equals((int)OrderStatusCode.InProcess))
                        {
                            if (package.Label.Status.Equals((byte)EnumData.LabelStatus.鎖定中))
                            {
                                Box prevBox = Box.Get(package.Label.PrevBoxID);
                                Box box = Box.GetAll(true).FirstOrDefault(b => b.IsEnable && b.DirectLine.Equals(prevBox.DirectLine) && b.WarehouseFrom.Equals(prevBox.WarehouseFrom) && b.ShippingStatus.Equals((byte)EnumData.DirectLineStatus.未發貨));
                                if (box == null)
                                {
                                    Warehouses warehouse = db.Warehouses.AsNoTracking().First(w => w.ID.Equals(prevBox.WarehouseFrom));
                                    Dictionary<int, bool> methodData = JsonConvert.DeserializeObject<Dictionary<int, bool>>(warehouse.CarrierData);

                                    TimeZoneConvert timeZoneConvert = new TimeZoneConvert();
                                    EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);
                                    string boxID = string.Format("{0}-{1}", db.DirectLine.AsNoTracking().First(d => d.ID.Equals(prevBox.DirectLine)).Abbreviation, timeZoneConvert.ConvertDateTime(TimeZone).ToString("yyyyMMdd"));
                                    int count = Box.GetAll(true).Count(b => b.IsEnable && b.DirectLine.Equals(prevBox.DirectLine) && b.WarehouseFrom.Equals(prevBox.WarehouseFrom) && b.BoxID.Contains(boxID)) + 1;
                                    byte[] Byte = BitConverter.GetBytes(count);
                                    Byte[0] += 64;
                                    box = new Box()
                                    {
                                        IsEnable = true,
                                        BoxID = string.Format("{0}-{1}", boxID, System.Text.Encoding.ASCII.GetString(Byte.Take(1).ToArray())),
                                        DirectLine = prevBox.DirectLine,
                                        FirstMileMethod = methodData.First(m => m.Value).Key,
                                        WarehouseFrom = prevBox.WarehouseFrom,
                                        BoxType = (byte)EnumData.DirectLineBoxType.DirectLine,
                                        Create_at = timeZoneConvert.Utc
                                    };
                                    Box.Create(box);
                                    Box.SaveChanges();
                                }

                                package.BoxID = package.Label.BoxID = box.BoxID;
                            }
                        }
                        else
                        {

                        }

                        package.Orders.StatusCode = status;
                        Packages.Update(package, package.ID);
                        Packages.SaveChanges();

                        SyncOrderStatus(package.Orders, status);
                        break;
                    case (byte)EnumData.ProcessStatus.已出貨:
                        throw new Exception("已出貨訂單未開放更改!");
                        break;
                }
            }
            catch (Exception e)
            {
                result.status = false;
                result.message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        private int SyncOrderStatus(Orders order, int StatusCode)
        {
            TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
            ThreadTask threadTask = new ThreadTask(string.Format("Direct Line訂單 - 更新訂單【{0}】訂單狀態至SC", order.OrderID));

            lock (factory)
            {
                threadTask.AddWork(factory.StartNew(Session =>
                {
                    threadTask.Start();

                    string message = "";

                    try
                    {
                        HttpSessionStateBase session = (HttpSessionStateBase)Session;
                        SC_WebService SCWS = new SC_WebService(session["ApiUserName"].ToString(), session["ApiPassword"].ToString());

                        if (!SCWS.Is_login) throw new Exception("SC is not login");

                        if (SCWS.Update_OrderStatus(order.OrderID, StatusCode))
                        {
                            MyHelp.Log("Orders", order.OrderID, "更新SC訂單狀態");
                        }
                    }
                    catch (Exception e)
                    {
                        message = e.Message;
                    }

                    return message;
                }, HttpContext.Session));
            }

            return threadTask.ID;
        }

        public ActionResult GetDirectLineMethod()
        {
            AjaxResult result = new AjaxResult();

            int warehouseID;
            if (int.TryParse(Session["warehouseId"].ToString(), out warehouseID))
            {
                Warehouses = new GenericRepository<Warehouses>(db);

                Warehouses warehouse = Warehouses.Get(warehouseID);
                if (warehouse != null && !string.IsNullOrEmpty(warehouse.CarrierData))
                {
                    Method = new GenericRepository<ShippingMethod>(db);

                    Dictionary<int, bool> methodData = JsonConvert.DeserializeObject<Dictionary<int, bool>>(warehouse.CarrierData);
                    result.data = Method.GetAll(true).Where(m => m.IsEnable && m.IsDirectLine && methodData.Keys.Contains(m.ID) && methodData[m.ID])
                        .GroupBy(m => m.DirectLine).ToDictionary(g => g.Key.ToString(), g => g.Select(m => new { text = m.Name, value = m.ID }).ToList());
                }
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetOrderSerialData(int type, int methodID)
        {
            AjaxResult result = new AjaxResult();

            int warehouseID = int.Parse(Session["warehouseId"].ToString());

            string orderSelect = string.Format("SELECT * FROM Orders WHERE StatusCode <> {0}", (int)OrderStatusCode.Completed);
            string itemSelect = string.Format("SELECT * FROM Items WHERE IsEnable = 1 AND ShipFromWarehouseID = {0}", warehouseID);
            string pickSelect = string.Format("SELECT * FROM PickProduct WHERE IsEnable = 1 AND IsPicked = 0 AND WarehouseID = {0}", warehouseID);

            var packageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨));
            if (!methodID.Equals(0)) packageFilter = packageFilter.Where(p => p.ShippingMethod.Value.Equals(methodID));

            var methodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && m.IsDirectLine && m.DirectLine.Equals(type)).ToList();
            var labelList = db.DirectLineLabel.AsNoTracking().Where(l => l.IsEnable && l.Status.Equals((byte)EnumData.LabelStatus.正常)).ToList();

            ObjectContext context = new ObjectContext("name=QDLogisticsEntities");
            var ProductList = packageFilter.ToList().Join(methodList, p => p.ShippingMethod, m => m.ID, (p, m) => p).Join(labelList, p => p.TagNo, l => l.LabelID, (p, l) => p)
                .Join(context.ExecuteStoreQuery<Items>(itemSelect).ToList(), p => p.ID, i => i.PackageID, (p, i) => p)
                .Join(context.ExecuteStoreQuery<Orders>(orderSelect).ToList(), p => p.OrderID, o => o.OrderID, (package, order) => new { order, package })
                .Join(context.ExecuteStoreQuery<PickProduct>(pickSelect).ToList(), op => op.package.ID, pk => pk.PackageID, (op, pick) => new { op.order, op.package, pick }).Distinct()
                .OrderBy(data => data.package.Qty).OrderBy(data => data.order.TimeOfOrder).OrderByDescending(data => data.order.RushOrder).ToList();

            string[] productIDs = ProductList.Select(p => p.pick.ProductID).Distinct().ToArray();
            var productList = ProductList.Select(p => p.pick.SetInBox(methodList.First(m => m.ID.Equals(p.package.ShippingMethod)).InBox).SetTagNo(p.package.TagNo).SetNote(p.package.Comment))
                .GroupBy(p => p.ProductID).ToDictionary(group => group.Key.ToString(), group => group.ToDictionary(p => p.ItemID.ToString()));

            List<SerialNumbers> itemSerials = db.SerialNumbers.AsNoTracking().Where(s => productIDs.Contains(s.ProductID)).ToList();
            var serialList = db.PurchaseItemReceive.AsNoTracking().Where(s => productIDs.Contains(s.ProductID)).GroupBy(s => s.ProductID)
                .ToDictionary(s => s.Key.ToString(), s => new
                {
                    isRequire = s.Max(sn => sn.IsRequireSerialScan),
                    serials = s.Select(sn => sn.SerialNumber.Trim()).ToArray(),
                    used = itemSerials.Where(i => i.ProductID == s.Key).Select(i => i.SerialNumber.Trim()).ToArray()
                });

            var groupList = ProductList.Select(p => p.pick).GroupBy(p => p.PackageID).GroupBy(p => p.First().OrderID)
                .ToDictionary(o => o.Key.ToString(), o => o.ToDictionary(p => p.Key.ToString(), p => p.ToDictionary(pp => pp.ItemID.ToString(),
                pp => new { data = pp, serial = itemSerials.Where(sn => sn.OrderItemID == pp.ItemID).Select(sn => sn.SerialNumber.Trim()).ToArray() })))
                .GroupBy(o => o.Value.Sum(p => p.Value.Sum(pp => pp.Value.data.Qty)) > 1).ToDictionary(g => g.Key ? "Multiple" : "Single", g => g.ToDictionary(o => o.Key.ToString(), o => o.Value));

            result.data = new { productList, groupList, serialList };

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetCurrentBox(int type)
        {
            AjaxResult result = new AjaxResult();

            int warehouseID;
            if (int.TryParse(Session["warehouseId"].ToString(), out warehouseID))
            {
                Box = new GenericRepository<Box>(db);

                var BoxFilter = Box.GetAll(true).Where(b => b.IsEnable && b.DirectLine.Equals(type) && b.WarehouseFrom.Equals(warehouseID));

                Box box = BoxFilter.FirstOrDefault(b => b.ShippingStatus.Equals((byte)EnumData.DirectLineStatus.未發貨));
                if (box == null)
                {
                    Warehouses warehouse = db.Warehouses.AsNoTracking().First(w => w.ID.Equals(warehouseID));
                    Dictionary<int, bool> methodData = JsonConvert.DeserializeObject<Dictionary<int, bool>>(warehouse.CarrierData);

                    TimeZoneConvert timeZoneConvert = new TimeZoneConvert();
                    EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);
                    string boxID = string.Format("{0}-{1}", db.DirectLine.AsNoTracking().First(d => d.ID.Equals(type)).Abbreviation, timeZoneConvert.ConvertDateTime(TimeZone).ToString("yyyyMMdd"));
                    int count = Box.GetAll(true).Count(b => b.IsEnable && b.DirectLine.Equals(type) && b.WarehouseFrom.Equals(warehouseID) && b.BoxID.Contains(boxID)) + 1;
                    byte[] Byte = BitConverter.GetBytes(count);
                    Byte[0] += 64;
                    box = new Box()
                    {
                        IsEnable = true,
                        BoxID = string.Format("{0}-{1}", boxID, System.Text.Encoding.ASCII.GetString(Byte.Take(1).ToArray())),
                        DirectLine = type,
                        FirstMileMethod = methodData.First(m => m.Value).Key,
                        WarehouseFrom = warehouseID,
                        BoxType = (byte)EnumData.DirectLineBoxType.DirectLine,
                        Create_at = timeZoneConvert.Utc
                    };
                    Box.Create(box);
                    Box.SaveChanges();

                    MyHelp.Log("Box", box.BoxID, string.Format("Box【{0}】建立完成", box.BoxID), Session);
                }

                List<PickProduct> pickList = box.Packages.Where(p => p.IsEnable.Value)
                    .Join(db.PickProduct.AsNoTracking().Where(pick => pick.IsEnable), p => p.ID, pick => pick.PackageID.Value, (p, pick) => pick.SetTagNo(p.TagNo).SetNote(p.Comment).SetInBox(p.Method.InBox)).ToList();
                result.data = new
                {
                    info = new { box.BoxID, box.FirstMileMethod, box.BoxNo },
                    list = pickList
                };
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult OrderPicked(string boxID, List<PickProduct> picked, Dictionary<string, string[]> serials, int reTry = 0)
        {
            AjaxResult result = new AjaxResult();

            Packages = new GenericRepository<Packages>(db);
            PickProduct = new GenericRepository<PickProduct>(db);
            SerialNumbers = new GenericRepository<SerialNumbers>(db);

            Packages package = Packages.Get(picked.First().PackageID.Value);

            try
            {
                MyHelp.Log("Orders", package.OrderID, string.Format("訂單【{0}】出貨", package.OrderID));

                if (package.Orders.StatusCode != (int)OrderStatusCode.InProcess)
                {
                    reTry = 3;
                    throw new Exception(string.Format("訂單【{0}】無法出貨因為並非InProcess的狀態", package.OrderID));
                }

                if (package.ProcessStatus != (byte)EnumData.ProcessStatus.待出貨)
                {
                    reTry = 3;
                    throw new Exception(string.Format("訂單【{0}】無法出貨因為並非待出貨的狀態", package.OrderID));
                }

                int AdminId = 0;
                int.TryParse(Session["AdminId"].ToString(), out AdminId);
                DateTime PickUpDate = new TimeZoneConvert().Utc;

                foreach (PickProduct pick in picked)
                {
                    pick.IsMail = false;
                    pick.PickUpDate = PickUpDate;
                    pick.PickUpBy = AdminId;
                    PickProduct.Update(pick, pick.ID);
                }

                package.BoxID = package.Label.BoxID = boxID;
                Packages.Update(package, package.ID);

                int itemID;
                List<SerialNumbers> serialList = package.Items.Where(i => i.IsEnable.Value).SelectMany(i => i.SerialNumbers).ToList();
                foreach (var sn in serials)
                {
                    if (int.TryParse(sn.Key, out itemID))
                    {
                        foreach (var serialNumber in sn.Value)
                        {
                            if (!serialList.Any(s => s.OrderItemID.Equals(itemID) && s.SerialNumber.Equals(serialNumber)))
                            {
                                SerialNumbers.Create(new SerialNumbers
                                {
                                    OrderID = package.OrderID,
                                    ProductID = package.Items.First(i => i.ID == itemID).ProductID,
                                    SerialNumber = serialNumber,
                                    OrderItemID = itemID,
                                    KitItemID = 0
                                });
                            }
                        }
                    }
                }

                Packages.SaveChanges();

                string basePath = HostingEnvironment.MapPath("~/FileUploads");
                result.data = new { fileName = new string[] { "Label.pdf" }, filePath = new string[] { Path.Combine(basePath, package.FilePath, "Label.pdf") }, amount = new int[] { 1 }, printName = package.Method.PrinterName };

                using (Hubs.ServerHub server = new Hubs.ServerHub())
                    server.BroadcastOrderChange(package.OrderID.Value, EnumData.OrderChangeStatus.已完成出貨);

                MyHelp.Log("Orders", package.OrderID, string.Format("訂單【{0}】出貨完成", package.OrderID));
            }
            catch (Exception e)
            {
                ResetShippedData(package, picked, serials);

                if (reTry < 2)
                {
                    MyHelp.Log("", null, string.Format("訂單【{0}】第{1}次重新出貨", package.OrderID, reTry + 1));
                    return OrderPicked(boxID, picked, serials, reTry + 1);
                }

                MyHelp.ErrorLog(e, string.Format("訂單【{0}】出貨失敗", package.OrderID), package.OrderID.ToString());
                result.message = string.Format("訂單【{0}】出貨失敗，錯誤：", package.OrderID) + (e.InnerException != null ? e.InnerException.Message.Trim() : e.Message.Trim());
                result.status = false;
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        private void ResetShippedData(Packages package, List<PickProduct> picked, Dictionary<string, string[]> serials)
        {
            MyHelp.Log("", null, string.Format("訂單【{0}】出貨狀態重置", package.OrderID));

            foreach (PickProduct data in picked)
            {
                data.IsPicked = false;
                data.QtyPicked = 0;
                PickProduct.Update(data, data.ID);
            }

            var serialArray = serials.SelectMany(s => s.Value).ToArray();
            foreach (var ss in SerialNumbers.GetAll().Where(s => s.OrderID.Equals(package.OrderID) && serialArray.Contains(s.SerialNumber)))
            {
                SerialNumbers.Delete(ss);
            };

            package.ProcessStatus = (int)EnumData.ProcessStatus.待出貨;
            Packages.Update(package, package.ID);
            Packages.SaveChanges();
        }

        public ActionResult SaveBox(string boxID, int methodID)
        {
            AjaxResult result = new AjaxResult();

            Box = new GenericRepository<Box>(db);
            Label = new GenericRepository<DirectLineLabel>(db);

            Box box = Box.Get(boxID);

            try
            {
                if (box == null) throw new Exception("Not found box!");

                if (!box.Packages.Any()) new Exception("尚未有任何訂單!");

                SC_WebService SCWS = new SC_WebService(Session["ApiUserName"].ToString(), Session["ApiPassword"].ToString());

                if (!SCWS.Is_login) throw new Exception("SC is not login");

                MyHelp.Log("box", box.BoxID, string.Format("開始檢查 Box【{0}】內的訂單狀態", box.BoxID), Session);

                List<object> fileList = new List<object>();
                List<object> errorList = new List<object>();
                foreach (Packages package in box.Packages.Where(p => p.IsEnable.Value).ToList())
                {
                    DirectLineLabel label = Label.Get(package.TagNo);
                    OrderData order = SCWS.Get_OrderData(package.OrderID.Value);
                    if (CheckOrderStatus(package, order.Order))
                    {
                        package.ProcessStatus = (int)EnumData.ProcessStatus.已出貨;
                        label.Status = (byte)EnumData.LabelStatus.正常;
                    }
                    else
                    {
                        MyHelp.Log("box", box.BoxID, string.Format("訂單【{0}】資料狀態異常", package.OrderID.Value), Session);

                        package.Orders.StatusCode = (int)order.Order.StatusCode;
                        package.Orders.PaymentStatus = (int)order.Order.PaymentStatus;
                        label.PrevBoxID = label.BoxID;
                        package.BoxID = label.BoxID = null;
                        label.Status = (byte)EnumData.LabelStatus.鎖定中;

                        if (order.Order.StatusCode.Equals((int)OrderStatusCode.Canceled))
                        {
                            label.Status = (byte)EnumData.LabelStatus.作廢;

                            PickProduct = new GenericRepository<PickProduct>(db);
                            foreach (PickProduct pick in PickProduct.GetAll(true).Where(pick => pick.OrderID.Equals(package.OrderID)))
                            {
                                pick.IsPicked = false;
                                pick.QtyPicked = 0;
                                PickProduct.Update(pick, pick.ID);
                            }

                            SerialNumbers = new GenericRepository<SerialNumbers>(db);
                            foreach (var ss in SerialNumbers.GetAll().Where(s => s.OrderID.Equals(package.OrderID)))
                            {
                                SerialNumbers.Delete(ss);
                            };
                        }

                        errorList.Add(new { package.OrderID, label.LabelID, ErrorMsg = string.Format("訂單【{0}】資料狀態異常，請重新取出!", package.OrderID.Value) });
                    }
                }

                MyHelp.Log("box", box.BoxID, string.Format("Box【{0}】儲存資料", box.BoxID), Session);

                box.FirstMileMethod = methodID;
                box.ShippingStatus = (byte)EnumData.DirectLineStatus.運輸中;
                Box.Update(box, box.BoxID);
                Box.SaveChanges();

                MyHelp.Log("box", box.BoxID, string.Format("開始產出 Box【{0}】報關資料", box.BoxID), Session);

                ShipProcess shipProcess = new ShipProcess(SCWS);
                ShipResult boxResult = shipProcess.Dispatch(box);
                if (boxResult.Status)
                {
                    MyHelp.Log("box", box.BoxID, string.Format("開始產出 Box【{0}】報關資料成功", box.BoxID), Session);

                    string[] fileName = new string[2];
                    string[] filePath = new string[2];
                    int[] amount = new int[] { 0, 0 };
                    string basePath = HostingEnvironment.MapPath("~/FileUploads");

                    /***** 提貨單 *****/
                    fileName[0] = "AirWaybill.pdf";
                    filePath[0] = Path.Combine(basePath, "export", "box", box.Create_at.ToString("yyyy/MM/dd"), box.BoxID, fileName[0]);
                    /***** 提貨單 *****/

                    /***** 商業發票 *****/
                    fileName[1] = "Invoice.xls";
                    filePath[1] = Path.Combine(basePath, "export", "box", box.Create_at.ToString("yyyy/MM/dd"), box.BoxID, fileName[1]);
                    /***** 商業發票 *****/

                    ShippingMethod method = db.ShippingMethod.AsNoTracking().First(m => m.ID.Equals(methodID));
                    switch (method.Carriers.CarrierAPI.Type)
                    {
                        case (byte)EnumData.CarrierType.DHL:
                            amount = new int[] { 2, 2 };
                            break;
                        case (byte)EnumData.CarrierType.FedEx:
                            amount = new int[] { 1, 4 };
                            break;
                        case (byte)EnumData.CarrierType.UPS:
                            break;
                        case (byte)EnumData.CarrierType.USPS:
                            break;
                    }

                    fileList.Add(new { fileName, filePath, amount, printerName = method.PrinterName });

                    MyHelp.Log("box", box.BoxID, string.Format("寄送 Box【{0}】報關資料", box.BoxID), Session);
                    SendMailToCarrier(box, method, db.DirectLine.AsNoTracking().First(d => d.ID.Equals(box.DirectLine)));
                    
                    MyHelp.Log("box", box.BoxID, string.Format("Box【{0}】完成出貨", box.BoxID), Session);
                }
                else
                {
                    string error = string.Format("產出 Box【{0}】報關資料失敗", box.BoxID);
                    MyHelp.Log("box", box.BoxID, error, Session);
                    throw new Exception(error);
                }

                result.data = new { fileList, errorList };
            }
            catch (Exception e)
            {
                result.message = string.Format("Box【{0}】出貨失敗，錯誤：", boxID) + (e.InnerException != null ? e.InnerException.Message.Trim() : e.Message.Trim());
                result.status = false;

                MyHelp.Log("box", box.BoxID, result.message, Session);
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        private bool CheckOrderStatus(Packages package, Order order)
        {
            bool OrderCompare = package.Orders.StatusCode.Value.Equals((int)order.StatusCode);
            bool PaymentCompare = package.Orders.PaymentStatus.Value.Equals((int)order.PaymentStatus);

            return OrderCompare && PaymentCompare;
        }

        public ActionResult ProductList(int PackageID, string Type)
        {
            List<string[]> productList = new List<string[]>();

            Packages = new GenericRepository<Packages>(db);

            Packages package = Packages.Get(PackageID);
            if (package != null)
            {
                switch (Type)
                {
                    case "BoxOrder":
                        foreach (Items item in package.Items.Where(i => i.IsEnable.Value))
                        {
                            for (int i = 0; i < item.Qty; i++)
                            {
                                SerialNumbers serial = item.SerialNumbers.Skip(i).Take(1).FirstOrDefault();

                                productList.Add(new string[] { item.ProductID, item.Skus.ProductName, (serial != null ? serial.SerialNumber : "") });

                                if (item.BundleItems.Any())
                                {
                                    foreach (BundleItems bundleItem in item.BundleItems)
                                    {
                                        productList.Add(new string[] { bundleItem.ProductID, bundleItem.Skus.ProductName, "" });
                                    }
                                }
                            }
                        }
                        break;
                }
            }

            ViewBag.productList = productList;
            return PartialView(string.Format("List_{0}", Type));
        }

        public void SendMailToCarrier(Box box, ShippingMethod method, DirectLine directLine)
        {
            PickProduct = new GenericRepository<PickProduct>(db);

            List<Items> itemsList = box.Packages.Where(p => p.IsEnable.Value).SelectMany(p => p.Items.Where(i => i.IsEnable.Value)).ToList();
            List<PickProduct> pickList = itemsList.Join(db.PickProduct.AsNoTracking().Where(p => p.IsEnable && p.IsPicked && !p.IsMail).ToList(), i => i.ID, pick => pick.ItemID, (i, pick) => pick).ToList();
            if (pickList.Any())
            {
                string basePath = HostingEnvironment.MapPath("~/FileUploads");
                string filePath;

                string sendMail = "dispatch-qd@hotmail.com";
                string mailTitle;
                string mailBody;
                string[] receiveMails;
                string[] ccMails = new string[] { "peter0626@hotmail.com", "kellyyang82@hotmail.com", "demi@qd.com.tw" };

                switch (method.Carriers.CarrierAPI.Type)
                {
                    case (byte)EnumData.CarrierType.DHL:
                        MyHelp.Log("PickProduct", null, "寄送DHL出口報單");

                        XLWorkbook DHL_workbook = new XLWorkbook();
                        JArray jObjects = new JArray();
                        List<string> DHLFile = new List<string>();

                        string OrderCurrencyCode = Enum.GetName(typeof(CurrencyCodeType), box.Packages.First().Orders.OrderCurrencyCode);
                        foreach (var group in itemsList.GroupBy(i => i.ProductID).ToList())
                        {
                            Skus sku = group.First().Skus;

                            JObject jo = new JObject();
                            jo.Add("1", !sku.ProductName.ToLower().Contains("htc") ? "G3" : "G5");
                            jo.Add("2", !sku.ProductName.ToLower().Contains("htc") ? "81" : "02");
                            jo.Add("3", sku.PurchaseInvoice);
                            jo.Add("4", directLine.ContactName);
                            jo.Add("5", string.Join(" - ", new string[] { sku.ProductType.ProductTypeName, sku.ProductName }));
                            jo.Add("6", box.TrackingNumber);
                            jo.Add("7", OrderCurrencyCode);
                            jo.Add("8", group.Sum(i => i.DeclaredValue * i.Qty.Value).ToString("N"));
                            jo.Add("9", directLine.StreetLine1);
                            jo.Add("10", directLine.City);
                            jo.Add("11", directLine.StateName);
                            jo.Add("12", directLine.PostalCode);
                            jo.Add("13", directLine.CountryName);
                            jo.Add("14", directLine.PhoneNumber);
                            jo.Add("15", group.Sum(i => i.Qty));
                            jObjects.Add(jo);
                        }

                        var DHL_sheet = DHL_workbook.Worksheets.Add(JsonConvert.DeserializeObject<DataTable>(jObjects.ToString()), "檢核表");
                        DHL_sheet.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                        DHL_sheet.Style.Font.FontName = "Times New Roman";
                        DHL_sheet.Style.Font.FontSize = 11;
                        DHL_sheet.Row(1).Hide();
                        DHL_sheet.Column(1).Width = 10;
                        DHL_sheet.Column(2).Width = 10;
                        DHL_sheet.Column(3).Width = 17;
                        DHL_sheet.Column(4).Width = 24;
                        DHL_sheet.Column(5).Width = 70;
                        DHL_sheet.Column(6).Width = 15;
                        DHL_sheet.Column(7).Width = 13;
                        DHL_sheet.Column(8).Width = 13;
                        DHL_sheet.Column(9).Width = 50;
                        DHL_sheet.Column(10).Width = 20;
                        DHL_sheet.Column(11).Width = 20;
                        DHL_sheet.Column(12).Width = 20;
                        DHL_sheet.Column(13).Width = 20;
                        DHL_sheet.Column(14).Width = 20;

                        filePath = Path.Combine(basePath, "mail", box.Create_at.ToString("yyyy/MM/dd"));
                        if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);

                        string fileName = string.Format("{0} 出口報關表格.xlsx", box.BoxID);
                        DHL_workbook.SaveAs(Path.Combine(filePath, fileName));

                        receiveMails = new string[] { "twtxwisa@dhl.com" };
                        mailTitle = string.Format("至優網 正式出口報關資料");

                        mailBody = box.TrackingNumber;

                        DHLFile.Add(Path.Combine(filePath, fileName));
                        bool DHL_Status = MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, DHLFile.ToArray(), null, false);

                        if (DHL_Status)
                        {
                            MyHelp.Log("", null, mailTitle);
                            foreach (PickProduct pick in pickList)
                            {
                                pick.IsMail = true;
                                PickProduct.Update(pick, pick.ID);
                            }
                        }
                        else
                        {
                            MyHelp.Log("PickProduct", null, string.Format("{0} 寄送失敗", mailTitle));
                        }
                        break;
                    case (byte)EnumData.CarrierType.FedEx:
                        MyHelp.Log("PickProduct", null, "寄送FedEx出口報單");

                        List<Tuple<Stream, string>> FedExFile = new List<Tuple<Stream, string>>();

                        filePath = Path.Combine(basePath, "export", "box", box.Create_at.ToString("yyyy/MM/dd"), box.BoxID);

                        var memoryStream = new MemoryStream();
                        using (var file = new ZipFile())
                        {
                            file.AddFile(Path.Combine(basePath, filePath, "Invoice.xls"), "");
                            file.AddFile(Path.Combine(basePath, "sample", "Fedex_Recognizances.pdf"), "");

                            foreach (var group in itemsList.GroupBy(i => i.ProductID).ToList())
                            {
                                file.AddFile(Path.Combine(basePath, filePath, string.Format("CheckList-{0}.xlsx", group.Key)), "");
                            }
                            file.Save(memoryStream);
                        }

                        memoryStream.Seek(0, SeekOrigin.Begin);
                        FedExFile.Add(new Tuple<Stream, string>(memoryStream, box.TrackingNumber + ".zip"));

                        receiveMails = new string[] { "edd@fedex.com" };
                        mailTitle = string.Format("至優網 正式出口報關資料");

                        bool FedEx_Status = MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, "", true, null, FedExFile, false);

                        if (FedEx_Status)
                        {
                            MyHelp.Log("", null, mailTitle);
                            foreach (PickProduct pick in pickList)
                            {
                                pick.IsMail = true;
                                PickProduct.Update(pick, pick.ID);
                            }
                        }
                        else
                        {
                            MyHelp.Log("PickProduct", null, string.Format("{0} 寄送失敗", mailTitle));
                        }
                        break;
                    default:
                        break;
                }
                PickProduct.SaveChanges();
            }
        }

        public class BoxFilter
        {
            public string BoxID { get; set; }
            public string SupplierBoxID { get; set; }
            public DateTime CreateDate { get; set; }
            public Nullable<int> Warehouse { get; set; }
            public string WITID { get; set; }
            public string Tracking { get; set; }
            public Nullable<byte> Status { get; set; }
            public Nullable<byte> Type { get; set; }
            public string Notes { get; set; }
        }

        public class AjaxResult
        {
            public bool status { get; set; }
            public string message { get; set; }
            public object data { get; set; }

            public AjaxResult()
            {
                this.status = true;
                this.message = null;
                this.data = null;
            }
        }
    }
}