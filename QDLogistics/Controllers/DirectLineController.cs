using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Core.Objects;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using ClosedXML.Excel;
using Ionic.Zip;
using Neodynamic.SDK.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NPOI.XSSF.UserModel;
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
        private IRepository<Packages> Packages;
        private IRepository<Items> Items;
        private IRepository<Box> Box;
        private IRepository<Warehouses> Warehouses;
        private IRepository<ShippingMethod> Method;
        private IRepository<PickProduct> PickProduct;
        private IRepository<SerialNumbers> SerialNumbers;

        private SC_WebService SCWS;

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
            List<SelectListItem> MethodList = new List<SelectListItem>();

            if (int.TryParse(Session["warehouseId"].ToString(), out warehouseID))
            {
                Warehouses warehouse = db.Warehouses.FirstOrDefault(w => w.IsEnable.Value && w.ID.Equals(warehouseID));
                if (warehouse != null && !string.IsNullOrEmpty(warehouse.CarrierData))
                {
                    Dictionary<int, bool> methodData = JsonConvert.DeserializeObject<Dictionary<int, bool>>(warehouse.CarrierData);
                    int[] methodIDs = methodData.Where(m => m.Value).Select(m => m.Key).ToArray();
                    MethodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && methodIDs.Contains(m.ID))
                        .Select(m => new SelectListItem() { Text = m.Name, Value = m.ID.ToString(), Selected = m.ID.Equals(35) }).ToList();
                }
            }

            ViewBag.directLineList = db.DirectLine.AsNoTracking().Where(d => d.IsEnable).Select(d => new SelectListItem() { Text = d.Name, Value = d.ID.ToString() }).ToList();
            ViewBag.FirstMileList = MethodList;
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

            ViewData["Carrier"] = !box.FirstMileMethod.Equals(0) ? db.ShippingMethod.AsNoTracking().First(m => m.IsEnable && m.ID.Equals(box.FirstMileMethod)).Carriers.Name : "";

            ViewBag.logList = db.ActionLog.AsNoTracking().Where(log => log.TableName.Equals("Box") && log.TargetID.Equals(box.BoxID)).OrderBy(log => log.CreateDate).ToList();

            return View(box);
        }

        public ActionResult Cancel()
        {
            return View();
        }

        public ActionResult Upload()
        {
            ViewBag.LogList = db.ActionLog.AsNoTracking().Where(l => l.TableName.Equals("DL_Upload")).OrderByDescending(l => l.CreateDate).Take(1000).ToList();

            return View();
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
                .Join(ItemFilter.ToList().GroupBy(i => i.PackageID.Value), oData => oData.package.ID, i => i.Key, (oData, i) => new OrderJoinData(oData) { item = i.First(), items = i.ToList(), itemCount = i.Sum(ii => 1 + ii.KitItemCount).Value })
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

        public ActionResult PackagePickUpList(int directLine, int firstMile, int methodID)
        {
            int warehouseID = int.Parse(Session["WarehouseID"].ToString());

            var PackageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨) && p.FirstMile.Value.Equals(firstMile));
            if (!methodID.Equals(0)) PackageFilter = PackageFilter.Where(p => p.ShippingMethod.Value.Equals(methodID));

            var LabelFilter = db.DirectLineLabel.AsNoTracking().Where(l => l.IsEnable && l.Status.Equals((byte)EnumData.LabelStatus.正常));
            var PickFilter = db.PickProduct.AsNoTracking().Where(pick => pick.IsEnable && !pick.IsPicked && pick.WarehouseID.Value.Equals(warehouseID));
            var MethodFilter = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && m.IsDirectLine && m.DirectLine.Equals(directLine));
            var OrderFilter = db.Orders.AsNoTracking().Where(o => !o.StatusCode.Value.Equals((int)OrderStatusCode.Completed));
            var ItemFilter = db.Items.AsNoTracking().Where(i => i.IsEnable.Value && i.ShipFromWarehouseID.Value.Equals(warehouseID));

            var ItemList = PackageFilter.Join(LabelFilter, p => p.TagNo, l => l.LabelID, (p, l) => p)
                .Join(PickFilter, package => package.ID, pick => pick.PackageID.Value, (package, pick) => new { package, pick })
                .Join(MethodFilter, data => data.package.ShippingMethod.Value, m => m.ID, (data, method) => new { data.package, data.pick, method })
                .Join(OrderFilter, data => data.package.OrderID.Value, o => o.OrderID, (data, order) => new { order, data.package, data.pick, data.method }).Distinct()
                .OrderBy(data => data.package.Qty).OrderBy(data => data.order.TimeOfOrder).OrderByDescending(data => data.order.RushOrder).ToList()
                .Join(ItemFilter, data => data.package.ID, i => i.PackageID, (data, item) => item).Distinct().GroupBy(i => i.PackageID).ToList();

            ViewBag.itemList = ItemList;
            return PartialView("~/Views/Ajax/_PickUpList.cshtml");
        }

        [HttpPost]
        public ActionResult PrintPickUpList(int directLine, int firstMile, int methodID)
        {
            int warehouseID = int.Parse(Session["WarehouseID"].ToString());
            int adminID = int.Parse(Session["AdminId"].ToString());
            AdminUsers admin = db.AdminUsers.Find(adminID);

            var PackageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨) && p.FirstMile.Value.Equals(firstMile));
            if (!methodID.Equals(0)) PackageFilter = PackageFilter.Where(p => p.ShippingMethod.Value.Equals(methodID));

            var LabelFilter = db.DirectLineLabel.AsNoTracking().Where(l => l.IsEnable && l.Status.Equals((byte)EnumData.LabelStatus.正常));
            var PickFilter = db.PickProduct.AsNoTracking().Where(pick => pick.IsEnable && !pick.IsPicked && pick.WarehouseID.Value.Equals(warehouseID));
            var MethodFilter = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && m.IsDirectLine && m.DirectLine.Equals(directLine));
            var OrderFilter = db.Orders.AsNoTracking().Where(o => !o.StatusCode.Value.Equals((int)OrderStatusCode.Completed));
            var ItemFilter = db.Items.AsNoTracking().Where(i => i.IsEnable.Value && i.ShipFromWarehouseID.Value.Equals(warehouseID));

            var ItemList = PackageFilter.Join(LabelFilter, p => p.TagNo, l => l.LabelID, (p, l) => p)
                .Join(PickFilter, package => package.ID, pick => pick.PackageID.Value, (package, pick) => new { package, pick })
                .Join(MethodFilter, data => data.package.ShippingMethod.Value, m => m.ID, (data, method) => new { data.package, data.pick, method })
                .Join(OrderFilter, data => data.package.OrderID.Value, o => o.OrderID, (data, order) => new { order, data.package, data.pick, data.method })
                .OrderBy(data => data.package.Qty).OrderBy(data => data.order.TimeOfOrder).OrderByDescending(data => data.order.RushOrder).ToList()
                .Join(ItemFilter, data => data.package.ID, i => i.PackageID, (data, item) => item).Distinct().ToList();
            
            string basePath = HostingEnvironment.MapPath("~/FileUploads");
            string DirPath = Path.Combine(basePath, "pickup");
            if (!Directory.Exists(DirPath)) Directory.CreateDirectory(DirPath);
            string[] fileName = new string[2];
            string[] filePath = new string[2];

            List<IGrouping<int?, Items>> itemGroupList = ItemList.GroupBy(i => i.PackageID).ToList();

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

            return Content(JsonConvert.SerializeObject(new { status = true, filePath, fileName, amount = new int[] { 1, 1 } }), "appllication /json");
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
            if (!string.IsNullOrEmpty(filter.LabelID)) BoxFilter = BoxFilter.Where(b => b.DirectLineLabel.Any(l => l.LabelID.Contains(filter.LabelID)));

            //int warehouseID = 0;
            //if (int.TryParse(Session["warehouseId"].ToString(), out warehouseID)) BoxFilter = BoxFilter.Where(b => b.WarehouseFrom.Equals(warehouseID));
            if (filter.WarehouseFrom.HasValue) BoxFilter = BoxFilter.Where(b => b.WarehouseFrom.Equals(filter.WarehouseFrom.Value));
            if (filter.WarehouseTo.HasValue) BoxFilter = BoxFilter.Where(b => b.WarehouseTo.Equals(filter.WarehouseTo.Value));
            if (!string.IsNullOrEmpty(filter.WITID)) BoxFilter = BoxFilter.Where(b => b.WITID.Contains(filter.WITID));
            if (!string.IsNullOrEmpty(filter.Tracking)) BoxFilter = BoxFilter.Where(b => b.TrackingNumber.Contains(filter.Tracking));
            if (filter.Status.HasValue) BoxFilter = BoxFilter.Where(b => b.ShippingStatus.Equals(filter.Status.Value));
            if (filter.Type.HasValue) BoxFilter = BoxFilter.Where(b => b.BoxType.Equals(filter.Type.Value));
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

                List<StockKeepingUnit.SkuData> SkuData = new List<StockKeepingUnit.SkuData>();
                using (StockKeepingUnit stock = new StockKeepingUnit())
                {
                    var IDs = itemGroup.SelectMany(g => g.Value).Select(i => i.ProductID).Distinct().ToArray();
                    SkuData = stock.GetSkuData(IDs);
                }

                Dictionary<int, string> warehouseName = db.Warehouses.AsNoTracking().Where(w => w.IsEnable.Value && w.IsSellable.Value).ToDictionary(w => w.ID, w => w.Name);

                int[] methodIDs = results.Select(m => m.FirstMileMethod).ToArray();
                Dictionary<int, string> carrierName = db.ShippingMethod.AsNoTracking().Where(m => methodIDs.Contains(m.ID)).ToDictionary(m => m.ID, m => m.Carriers.Name);

                var boxTypeList = EnumData.BoxTypeList();
                List<DirectLineLabel> lockLabel = db.DirectLineLabel.AsNoTracking().Where(l => l.IsEnable && l.Status.Equals((byte)EnumData.LabelStatus.鎖定中) && !string.IsNullOrEmpty(l.BoxID)).ToList();

                string baseUrl = Request.Url.Scheme + "://" + Request.Url.Authority + Request.ApplicationPath.TrimEnd('/') + "/fileUploads";
                var dd = results.FirstOrDefault(d => string.IsNullOrEmpty(d.BoxID));
                dataList.AddRange(results.Select(data => new
                {
                    data.IsReserved,
                    data.BoxID,
                    data.SupplierBoxID,
                    CreateDate = timeZoneConvert.InitDateTime(data.Create_at, EnumData.TimeZone.UTC).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy<br />hh:mm tt"),
                    WarehouseFrom = warehouseName.ContainsKey(data.WarehouseFrom) ? warehouseName[data.WarehouseFrom] : "",
                    WarehouseTO = warehouseName.ContainsKey(data.WarehouseTo) ? warehouseName[data.WarehouseTo] : "",
                    BoxQty = data.BoxNo,
                    TotalWeight = itemGroup.Any(i => i.Key.Equals(data.BoxID)) ? itemGroup[data.BoxID].Sum(i => i.Qty * ((float)(SkuData.Any(s => s.Sku.Equals(i.ProductID)) ? SkuData.First(s => s.Sku.Equals(i.ProductID)).Weight : i.Skus.ShippingWeight) / 1000)) : 0,
                    data.WITID,
                    data.DirectLine,
                    Method = data.FirstMileMethod,
                    Carrier = carrierName.ContainsKey(data.FirstMileMethod) ? carrierName[data.FirstMileMethod] : "",
                    Tracking = data.TrackingNumber ?? "",
                    Status = Enum.GetName(typeof(EnumData.DirectLineStatus), data.ShippingStatus),
                    data.ShippingStatus,
                    data.DeliveryNote,
                    Type = boxTypeList[(EnumData.DirectLineBoxType)data.BoxType],
                    data.Note,
                    Download = !data.ShippingStatus.Equals((byte)EnumData.DirectLineStatus.未發貨) ? string.Format("{0}/export/box/{1}/{2}", baseUrl, data.Create_at.ToString("yyyy/MM/dd"), data.BoxID) : "",
                    OrderLock = lockLabel.Count(l => l.BoxID.Equals(data.BoxID))
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
            var MethodFilter = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && m.IsDirectLine);

            var results = PackageFilter.ToList()
                .Join(OrderFilter, p => p.OrderID.Value, o => o.OrderID, (p, o) => new OrderJoinData() { order = o, package = p })
                .Join(MethodFilter, oData => oData.package.ShippingMethod.Value, m => m.ID, (oData, m) => new OrderJoinData(oData) { method = m })
                .Join(ItemFilter.GroupBy(i => i.PackageID.Value), oData => oData.package.ID, i => i.Key, (oData, i) => new OrderJoinData(oData) { item = i.First(), items = i.ToList(), itemCount = i.Sum(ii => ii.Qty + ii.KitItemCount).Value }).ToList();

            if (results.Any())
            {
                int length = rows;
                int start = (page - 1) * length;
                total = results.Count();
                results = results.OrderByDescending(data => data.order.OrderID).Skip(start).Take(length).ToList();

                DirectLine directLine = db.DirectLine.Find(results.First().package.Method.DirectLine);

                string[] skuList = results.Select(data => data.item.ProductID).ToArray();
                List<StockKeepingUnit.SkuData> SkuData = new List<StockKeepingUnit.SkuData>();
                using (StockKeepingUnit stock = new StockKeepingUnit())
                {
                    SkuData = stock.GetSkuData(skuList);
                }
                dataList.AddRange(results.Select(data => new
                {
                    PackageID = data.package.ID,
                    ProductID = data.itemCount == 1 ? data.item.ProductID : "Multi",
                    ProductName = data.itemCount == 1 ? (SkuData.Any(s => s.Sku.Equals(data.item.ProductID)) ? SkuData.First(s => s.Sku.Equals(data.item.ProductID)).Name : data.item.Skus.ProductName) : "Multi",
                    SentQty = data.itemCount,
                    ReceivedQty = 0,
                    Weight = data.items.Sum(i => (SkuData.Any(s => s.Sku.Equals(i.ProductID)) ? SkuData.First(s => s.Sku.Equals(i.ProductID)).Weight : i.Skus.ShippingWeight) * (float)i.Qty.Value / 1000),
                    data.order.OrderID,
                    Serial = data.item.SerialNumbers.Any() ? (data.itemCount == 1 ? data.item.SerialNumbers.First().SerialNumber : "Multi") : "找不到",
                    Tracking = data.package.TrackingNumber,
                    LabelID = GetLabelLink(directLine.Abbreviation, data),
                    data.order.StatusCode
                }));
            }

            return Json(new { total, rows = dataList }, JsonRequestBehavior.AllowGet);
        }

        private string GetLabelLink(string directLine, OrderJoinData data)
        {
            string link = "<a href='http://internal.qd.com.tw/fileUploads/{0}/Label.pdf' target='_blank'>{1}</a>";
            switch (directLine)
            {
                case "ECOF":
                    switch (data.method.Carriers.CarrierAPI.Type.Value)
                    {
                        case (byte)EnumData.CarrierType.Sendle:
                            string Sendle_Label = string.Format("{0}-{1}-{2}", data.item.ProductID, data.order.OrderID, data.package.TrackingNumber);
                            link = string.Format(link, data.package.FilePath, Sendle_Label);
                            break;
                        default:
                            link = string.Format(link, data.package.FilePath, data.package.TagNo);
                            break;
                    }
                    break;
                default:
                    link = string.Format(link, data.package.FilePath, data.package.TagNo);
                    break;
            }
            return link;
        }

        public ActionResult GetCancelData(CancelFilter filter, int page = 1, int rows = 100)
        {
            int total = 0;
            List<object> dataList = new List<object>();

            TimeZoneConvert timeZoneConvert = new TimeZoneConvert();
            EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);

            var LabelFilter = db.SerialNumberForRefundLabel.AsNoTracking().AsQueryable();
            if (filter.IsUsed.HasValue) LabelFilter = LabelFilter.Where(l => l.IsUsed.Equals(filter.IsUsed.Value));
            if (!string.IsNullOrEmpty(filter.OrderID)) LabelFilter = LabelFilter.Where(l => l.oldOrderID.ToString().Equals(filter.OrderID));
            if (!string.IsNullOrEmpty(filter.LabelID)) LabelFilter = LabelFilter.Where(l => l.oldLabelID.Equals(filter.LabelID));
            if (!string.IsNullOrEmpty(filter.NewOrderID)) LabelFilter = LabelFilter.Where(l => l.newOrderID.ToString().Equals(filter.NewOrderID));
            if (!string.IsNullOrEmpty(filter.NewLabelID)) LabelFilter = LabelFilter.Where(l => l.newLabelID.Equals(filter.NewLabelID));
            if (filter.RMAID.HasValue) LabelFilter = LabelFilter.Where(l => l.RMAID.Equals(filter.RMAID.Value));
            if (!string.IsNullOrEmpty(filter.Sku)) LabelFilter = LabelFilter.Where(l => l.Sku.Contains(filter.Sku));
            if (!string.IsNullOrEmpty(filter.SerialNumber)) LabelFilter = LabelFilter.Where(l => l.SerialNumber.Contains(filter.SerialNumber));
            if (filter.WarehouseID.HasValue) LabelFilter = LabelFilter.Where(l => l.WarehouseID.Equals(filter.WarehouseID.Value));
            if (filter.CreateDate.HasValue)
            {
                DateTime dateFrom = timeZoneConvert.InitDateTime(filter.CreateDate.Value, TimeZone).Utc;
                DateTime dateTO = timeZoneConvert.Utc.AddDays(1);
                LabelFilter = LabelFilter.Where(l => l.Create_at.CompareTo(dateFrom) >= 0 && l.Create_at.CompareTo(dateTO) < 0);
            }
            if (filter.Dispatch.HasValue) LabelFilter = LabelFilter.Where(l => l.IsUsed.Equals(filter.Dispatch.Value));

            string[] Skus = LabelFilter.Select(l => l.Sku).Distinct().ToArray();
            var SkuFilter = db.Skus.AsNoTracking().Where(s => s.IsEnable.Value && Skus.Contains(s.Sku));
            if (!string.IsNullOrEmpty(filter.ProductName)) SkuFilter = SkuFilter.Where(s => s.ProductName.Contains(filter.ProductName));

            var results = LabelFilter.ToList().Join(SkuFilter, l => l.Sku, s => s.Sku, (l, s) => l).GroupBy(l => l.oldLabelID).Select(g => new { label = g.First(), qty = g.Count() }).ToList();
            if (results.Any())
            {
                int length = rows;
                int start = (page - 1) * length;
                total = results.Count();
                results = results.OrderByDescending(data => data.label.Create_at).Skip(start).Take(length).ToList();

                Skus = results.Where(data => data.qty == 1).Select(data => data.label.Sku).ToArray();
                Dictionary<string, string> ProductName = SkuFilter.Where(s => Skus.Contains(s.Sku)).ToDictionary(s => s.Sku, s => s.ProductName);

                Dictionary<int, string> DirectLine = db.DirectLine.ToDictionary(d => d.ID, d => d.Abbreviation);

                int[] WarehouseIDs = results.Select(data => data.label.WarehouseID).Distinct().ToArray();
                Dictionary<int, string> WarehouseName = db.Warehouses.AsNoTracking().Where(w => w.IsEnable.Value && WarehouseIDs.Contains(w.ID)).ToDictionary(w => w.ID, w => w.Name);

                dataList.AddRange(results.Select(data => new
                {
                    data.label.IsUsed,
                    LabelID = data.label.oldLabelID,
                    OrderID = data.label.oldOrderID,
                    OldLabelID = GetCorrectLabelID(data.label.oldLabelID),
                    NewLabelID = GetCorrectLabelID(data.label.newLabelID),
                    NewOrderID = data.label.newOrderID,
                    data.label.RMAID,
                    Qty = data.qty,
                    Sku = data.qty == 1 ? data.label.Sku : "Multi",
                    ProductName = data.qty == 1 ? ProductName[data.label.Sku] : "Multi",
                    SerialNumber = data.qty == 1 ? data.label.SerialNumber : "Multi",
                    WarehouseID = data.label.WarehouseID,
                    WarehouseName = WarehouseName[data.label.WarehouseID],
                    CreateDate = timeZoneConvert.InitDateTime(data.label.Create_at, EnumData.TimeZone.UTC).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy tt hh:mm")
                }));
            }

            return Json(new { total, rows = dataList }, JsonRequestBehavior.AllowGet);
        }

        private string GetCorrectLabelID(string labelID)
        {
            DirectLineLabel label = db.DirectLineLabel.Find(labelID);

            if (label == null) return "";

            Packages package = db.Packages.Find(label.PackageID);
            if (package.Method.IsDirectLine)
            {
                if (db.DirectLine.Any(d => d.ID.Equals(package.Method.DirectLine) && d.Abbreviation.Equals("ECOF")))
                {
                    switch (package.Method.Carriers.CarrierAPI.Type.Value)
                    {
                        case (byte)EnumData.CarrierType.Sendle:
                            return string.Format("{0}-{1}-{2}", package.Items.First(i => i.IsEnable.Value).ProductID, package.OrderID, package.TrackingNumber);
                    }
                }
            }

            return label.LabelID;
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
                            optionList.Add(type, db.Warehouses.AsNoTracking().Where(w => w.IsEnable.Value && w.IsSellable.Value).Select(w => new { text = w.Name, value = w.ID }).ToArray());
                            break;
                        case "Method":
                            optionList.Add(type, db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && m.IsDirectLine).Select(m => new { text = m.Name, value = m.ID }).ToArray());
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
                        case "Dispatch":
                            optionList.Add(type, Enum.GetValues(typeof(EnumData.YesNo)).Cast<EnumData.YesNo>().Select(s => new { text = s.ToString().Equals("Yes") ? "Dispatch" : "Undispatch", value = (byte)s }).ToArray());
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

                switch (status)
                {
                    case (int)OrderStatusCode.InProcess:
                        if (package.Orders.StatusCode.Equals((int)OrderStatusCode.OnHold) && package.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨) && package.Label.Status.Equals((byte)EnumData.LabelStatus.鎖定中))
                        {
                            Box prevBox = Box.Get(!string.IsNullOrEmpty(package.Label.PrevBoxID) ? package.Label.PrevBoxID : package.Label.BoxID);
                            Box box = Box.GetAll(true).FirstOrDefault(b => b.IsEnable && b.DirectLine.Equals(prevBox.DirectLine) && b.WarehouseFrom.Equals(prevBox.WarehouseFrom) && b.ShippingStatus.Equals((byte)EnumData.DirectLineStatus.未發貨));
                            if (box == null)
                            {
                                Warehouses warehouse = db.Warehouses.AsNoTracking().First(w => w.ID.Equals(prevBox.WarehouseFrom));
                                Dictionary<int, bool> methodData = JsonConvert.DeserializeObject<Dictionary<int, bool>>(warehouse.CarrierData);

                                TimeZoneConvert timeZoneConvert = new TimeZoneConvert();
                                EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);
                                string boxID = string.Format("{0}-{1}", db.DirectLine.AsNoTracking().First(d => d.ID.Equals(prevBox.DirectLine)).Abbreviation, timeZoneConvert.ConvertDateTime(TimeZone).ToString("yyyyMMdd"));
                                int count = Box.GetAll(true).Count(b => b.IsEnable && b.DirectLine.Equals(prevBox.DirectLine) && b.BoxID.Contains(boxID)) + 1;
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
                        break;
                    case (int)OrderStatusCode.OnHold:
                        if (!package.Orders.StatusCode.Equals((int)OrderStatusCode.Completed))
                        {
                            package.Label.Status = (byte)EnumData.LabelStatus.鎖定中;
                        }
                        break;
                    case (int)OrderStatusCode.Completed:
                        if (package.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨))
                        {
                            package.ProcessStatus = (byte)EnumData.ProcessStatus.已出貨;
                        }

                        package.Label.Status = (byte)EnumData.LabelStatus.完成;
                        break;
                    case (int)OrderStatusCode.Canceled:
                        if (package.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨))
                        {
                            package.ProcessStatus = (byte)EnumData.ProcessStatus.訂單管理;
                        }

                        package.Label.Status = (byte)EnumData.LabelStatus.作廢;
                        break;
                }

                package.Orders.StatusCode = status;
                Packages.Update(package, package.ID);
                Packages.SaveChanges();

                SyncOrderStatus(package.Orders, status);
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

            try
            {
                using (StockKeepingUnit Stock = new StockKeepingUnit())
                {
                    Stock.RecordOrderSkuStatement(order.OrderID, Enum.GetName(typeof(OrderStatusCode), StatusCode));
                }
            }
            catch (Exception e)
            {
                string errorMsg = string.Format("傳送訂單狀態至測試系統失敗，請通知處理人員：{0}", e.InnerException != null ? e.InnerException.Message.Trim() : e.Message.Trim());
                MyHelp.Log("SkuStatement", order.OrderID, string.Format("訂單【{0}】{1}", order.OrderID, errorMsg), Session);
            }

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

        public ActionResult GetOrderSerialData(int type, int firstMile, int methodID)
        {
            AjaxResult result = new AjaxResult();

            int warehouseID = int.Parse(Session["warehouseId"].ToString());

            var PackageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨) && p.FirstMile.Value.Equals(firstMile));
            if (!methodID.Equals(0)) PackageFilter = PackageFilter.Where(p => p.ShippingMethod.Value.Equals(methodID));

            var OrderFilter = db.Orders.AsNoTracking().Where(o => !o.StatusCode.Value.Equals((int)OrderStatusCode.Completed));
            var ItemFilter = db.Items.AsNoTracking().Where(i => i.IsEnable.Value && i.ShipFromWarehouseID.Value.Equals(warehouseID));
            var PickFilter = db.PickProduct.AsNoTracking().Where(pick => pick.IsEnable && !pick.IsPicked && pick.WarehouseID.Value.Equals(warehouseID));
            var SkuFilter = db.Skus.AsNoTracking().Where(sku => sku.IsEnable.Value);
            var MethodFilter = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && m.IsDirectLine && m.DirectLine.Equals(type));
            var LabelFilter = db.DirectLineLabel.AsNoTracking().Where(l => l.IsEnable && l.Status.Equals((byte)EnumData.LabelStatus.正常));

            var productList = PackageFilter.Join(LabelFilter, p => p.TagNo, l => l.LabelID, (p, l) => p)
                .Join(PickFilter, package => package.ID, pick => pick.PackageID.Value, (package, pick) => new { package, pick })
                .Join(MethodFilter, data => data.package.ShippingMethod.Value, m => m.ID, (data, method) => new { data.package, data.pick, method })
                .Join(OrderFilter, data => data.package.OrderID.Value, o => o.OrderID, (data, order) => new { order, data.package, data.pick, data.method }).Distinct()
                .OrderBy(data => data.package.Qty).OrderBy(data => data.order.TimeOfOrder).OrderByDescending(data => data.order.RushOrder).ToList()
                .Select(data => data.pick.SetTagNo(data.package.TrackingNumber ?? data.package.TagNo).SetNote(data.package.Comment).SetInBox(data.method.InBox))
                .Join(ItemFilter, pick => pick.ItemID.Value, i => i.ID, (pick, i) => new { item = i, pick })
                .Join(SkuFilter, data => data.pick.ProductID, sku => sku.Sku, (data, sku) => new { data.pick, data.item, sku })
                .Select(p => p.pick.SetDeclaredValue(p.item.DeclaredValue).SetBattery(p.sku.Battery ?? false).SetWeight(p.sku.ShippingWeight))
                .GroupBy(pick => pick.ProductID).ToDictionary(group => group.Key.ToString(), group => group.ToDictionary(p => p.ItemID.Value.ToString()));

            string[] productIDs = productList.Select(p => p.Key).Distinct().ToArray();
            List<StockKeepingUnit.SkuData> SkuData = new List<StockKeepingUnit.SkuData>();
            using (StockKeepingUnit stock = new StockKeepingUnit())
            {
                SkuData = stock.GetSkuData(productIDs);
                foreach (var group in productList)
                    if (SkuData.Any(s => s.Sku.Equals(group.Key)))
                        foreach (var pick in group.Value.Select(p => p.Value))
                            pick.SetWeight(SkuData.First(s => s.Sku.Equals(pick.ProductID)).Weight);
            }
            List<SerialNumbers> itemSerials = db.SerialNumbers.AsNoTracking().Where(s => productIDs.Contains(s.ProductID)).ToList();
            List<PurchaseItemReceive> purchaseItemSerial = db.PurchaseItemReceive.AsNoTracking().Where(s => productIDs.Contains(s.ProductID)).ToList();
            var serialList = productIDs.ToDictionary(p => p, p => new
            {
                isRequire = purchaseItemSerial.Any(s => s.ProductID.Equals(p)) ? purchaseItemSerial.Where(s => s.ProductID.Equals(p)).Max(sn => sn.IsRequireSerialScan) : false,
                serials = purchaseItemSerial.Where(sn => sn.ProductID.Equals(p)).Select(sn => sn.SerialNumber.Trim()).ToArray(),
                used = itemSerials.Where(i => i.ProductID.Equals(p)).Select(i => i.SerialNumber.Trim()).ToArray()
            });

            var groupList = productList.Values.SelectMany(p => p.Values).GroupBy(p => p.PackageID).GroupBy(p => p.First().OrderID)
                .ToDictionary(o => o.Key.ToString(), o => o.ToDictionary(p => p.Key.ToString(), p => p.ToDictionary(pp => pp.ItemID.ToString(),
                pp => new { data = pp, serial = itemSerials.Where(sn => sn.OrderItemID == pp.ItemID).Select(sn => sn.SerialNumber.Trim()).ToArray() })))
                .GroupBy(o => o.Value.Sum(p => p.Value.Sum(pp => pp.Value.data.Qty)) > 1).ToDictionary(g => g.Key ? "Multiple" : "Single", g => g.ToDictionary(o => o.Key.ToString(), o => o.Value));

            result.data = new { productList, groupList, serialList, SkuData };

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetCurrentBox(int type, int firstMile, string boxID)
        {
            AjaxResult result = new AjaxResult();

            int warehouseID;
            if (int.TryParse(Session["warehouseId"].ToString(), out warehouseID))
            {
                DirectLine directLine = db.DirectLine.Find(type);
                ShippingMethod method = db.ShippingMethod.Find(firstMile);

                Box box = db.Box.Find(boxID);
                List<object> pickList = new List<object>();

                if (box == null)
                {
                    BoxManage BoxManage = new BoxManage(Session);
                    box = BoxManage.GetCurrentBox(directLine, warehouseID, firstMile);
                }

                decimal totalValue = 0, totalWeight = 0;
                List<StockKeepingUnit.SkuData> SkuData = new List<StockKeepingUnit.SkuData>();
                List<Packages> packageList = db.Box.Where(b => b.IsEnable && b.MainBox.Equals(box.MainBox)).SelectMany(b => b.Packages.Where(p => p.IsEnable.Value)).ToList();
                if (packageList.Any())
                {
                    List<OrderJoinData> dataList = packageList
                        .Join(db.Items.AsNoTracking().Where(i => i.IsEnable.Value), p => p.ID, i => i.PackageID, (p, i) => new OrderJoinData() { package = p, item = i })
                        .Join(db.PickProduct.AsNoTracking().Where(pick => pick.IsEnable), data => data.item.ID, pick => pick.ItemID, (data, pick) => new OrderJoinData(data) { pick = pick }).ToList();

                    using (StockKeepingUnit stock = new StockKeepingUnit())
                    {
                        SkuData = stock.GetSkuData(dataList.Select(data => data.item.ProductID).Distinct().ToArray());
                    }

                    foreach (OrderJoinData data in dataList.Where(data => data.package.BoxID.Equals(box.BoxID)))
                    {
                        for (int i = 0; i < data.item.Qty; i++)
                        {
                            pickList.Add(new
                            {
                                data.pick.OrderID,
                                data.pick.PackageID,
                                data.pick.ProductID,
                                ProductName = SkuData.Any(s => s.Sku.Equals(data.pick.ProductID)) ? SkuData.First(s => s.Sku.Equals(data.pick.ProductID)).Name : data.pick.ProductName,
                                data.item.SerialNumbers.Skip(i).FirstOrDefault()?.SerialNumber,
                                data.package.TagNo,
                                data.package.Label.Note,
                                data.package.Method.InBox,
                                data.item.DeclaredValue,
                                Weight = SkuData.Any(s => s.Sku.Equals(data.pick.ProductID)) ? SkuData.First(s => s.Sku.Equals(data.pick.ProductID)).Weight : data.item.Skus.ShippingWeight,
                                IsBattery = data.item.Skus.Battery ?? false,
                            });
                        }
                    }

                    totalValue = dataList.Sum(data => data.package.DeclaredTotal);
                    totalWeight = dataList.Sum(data => (SkuData.Any(s => s.Sku.Equals(data.pick.ProductID)) ? SkuData.First(s => s.Sku.Equals(data.pick.ProductID)).Weight : data.item.Skus.ShippingWeight) * data.pick.Qty.Value);
                }

                result.data = new
                {
                    info = MyHelp.RenderViewToString(ControllerContext, "Info_Box", box, new ViewDataDictionary() {
                        { "directLine", directLine },
                        { "method", method },
                        { "totalValue", totalValue },
                        { "totalWeight", totalWeight / 1000 }
                    }),
                    list = pickList
                };
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult OrderPicked(string boxID, List<PickProduct> picked, Dictionary<string, string[]> serials)
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
                    throw new Exception(string.Format("訂單【{0}】無法出貨因為並非InProcess的狀態", package.OrderID));
                }

                if (package.ProcessStatus != (byte)EnumData.ProcessStatus.待出貨)
                {
                    throw new Exception(string.Format("訂單【{0}】無法出貨因為並非待出貨的狀態", package.OrderID));
                }

                if (!package.Method.DirectLine.Equals(db.Box.Find(boxID).DirectLine))
                {
                    throw new Exception(string.Format("訂單【{0}】無法出貨因為並非相同的DL", package.OrderID));
                }

                int AdminId = 0;
                int.TryParse(Session["AdminId"].ToString(), out AdminId);
                DateTime PickUpDate = new TimeZoneConvert().Utc;

                foreach (PickProduct pick in picked)
                {
                    pick.IsPicked = true;
                    pick.IsMail = false;
                    pick.QtyPicked = pick.Qty.Value;
                    pick.PickUpDate = PickUpDate;
                    pick.PickUpBy = AdminId;
                    PickProduct.Update(pick, pick.ID);
                }

                package.BoxID = package.Label.BoxID = boxID;
                package.DispatchDate = PickUpDate;
                Packages.Update(package, package.ID);

                foreach (Items item in package.Items.Where(i => i.IsEnable.Value))
                {
                    if (serials.ContainsKey(item.ID.ToString()) && serials[item.ID.ToString()].Any())
                    {
                        MyHelp.Log("Orders", package.OrderID, string.Format("產品【{0}】存入 {1}", item.ID, string.Join("、", serials[item.ID.ToString()])), Session);

                        foreach (string serialNumber in serials[item.ID.ToString()])
                        {
                            if (!db.SerialNumbers.AsNoTracking().Any(s => s.SerialNumber.Equals(serialNumber) && s.OrderItemID.Equals(item.ID)))
                            {
                                SerialNumbers.Create(new SerialNumbers
                                {
                                    OrderID = item.OrderID,
                                    ProductID = item.ProductID,
                                    SerialNumber = serialNumber,
                                    OrderItemID = item.ID,
                                    KitItemID = 0
                                });
                            }
                        }
                    }
                }

                Packages.SaveChanges();

                MyHelp.Log("Orders", package.OrderID, string.Format("開始檢查訂單【{0}】的產品", package.OrderID), Session);

                using (StockKeepingUnit SKU = new StockKeepingUnit())
                {
                    foreach (Items item in package.Items.Where(i => i.IsEnable.Value))
                    {
                        SKU.SetItemData(item.ID);

                        if (!SKU.CheckSkuSerial())
                        {
                            using (Hubs.ServerHub server = new Hubs.ServerHub())
                                server.BroadcastProductError(package.OrderID.Value, item.ProductID, EnumData.OrderChangeStatus.產品異常);

                            throw new Exception(string.Format("產品 {0} 的 Serial Number 發現異常!", item.ProductID));
                        }
                    }
                }

                string basePath = HostingEnvironment.MapPath("~/FileUploads");
                result.data = new { fileName = new string[] { "Label.pdf" }, filePath = new string[] { Path.Combine(basePath, package.FilePath, "Label.pdf") }, amount = new int[] { 1 }, printerName = package.Method.PrinterName };

                using (Hubs.ServerHub server = new Hubs.ServerHub())
                    server.BroadcastOrderEvent(package.OrderID.Value, EnumData.OrderChangeStatus.已完成出貨);

                MyHelp.Log("Orders", package.OrderID, string.Format("訂單【{0}】出貨完成", package.OrderID));
            }
            catch (Exception e)
            {
                ResetShippedData(package, picked, serials);

                MyHelp.ErrorLog(e, string.Format("訂單【{0}】出貨失敗", package.OrderID), package.OrderID.ToString());
                result.message = string.Format("訂單【{0}】出貨失敗，錯誤：", package.OrderID) + (e.InnerException != null ? e.InnerException.Message.Trim() : e.Message.Trim());
                result.status = false;
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        private void ResetShippedData(Packages package, List<PickProduct> picked, Dictionary<string, string[]> serials)
        {
            MyHelp.Log("Orders", package.OrderID, string.Format("訂單【{0}】出貨狀態重置", package.OrderID));

            package.BoxID = package.Label.BoxID = null;

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

            Packages.Update(package, package.ID);
            Packages.SaveChanges();
        }

        public ActionResult SaveBox(string boxID, int boxNo)
        {
            AjaxResult result = new AjaxResult();

            Box box = db.Box.Find(boxID);

            try
            {
                if (box == null) throw new Exception("Not found box!");

                switch (boxNo - box.BoxNo)
                {
                    case -1:
                        if (box.Packages.Any(p => p.IsEnable.Value)) throw new Exception("已經有訂單，無法回前一箱!");

                        box.IsEnable = false;
                        db.Entry(box).State = System.Data.Entity.EntityState.Modified;
                        db.SaveChanges();

                        string prevBoxID = "";
                        foreach (Box boxData in db.Box.Where(b => b.IsEnable && b.MainBox.Equals(box.MainBox)).OrderBy(b => b.CurrentNo).ToList())
                        {
                            prevBoxID = boxData.BoxID;
                            boxData.BoxNo = boxNo;
                            db.Entry(boxData).State = System.Data.Entity.EntityState.Modified;
                        }
                        db.SaveChanges();
                        result.data = new { boxID = prevBoxID };
                        break;
                    case 0:
                        if (!box.Packages.Any(p => p.IsEnable.Value)) throw new Exception("尚未有任何訂單!");

                        var method = db.ShippingMethod.Find(box.FirstMileMethod);

                        var boxList = db.Box.Where(b => b.IsEnable && b.MainBox.Equals(box.MainBox)).OrderBy(b => b.BoxID).ToList();
                        result.data = Box_Shippping(boxList, method);

                        try
                        {
                            List<dynamic> data = new List<dynamic>();
                            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://internal.qd.com.tw:8080/Ajax/ShipmentByOrder");
                            request.ContentType = "application/json";
                            request.Method = "post";
                            //request.ProtocolVersion = HttpVersion.Version10;

                            foreach (var item in boxList.SelectMany(b => b.Packages.Where(p => p.IsEnable.Value)).SelectMany(p => p.Items.Where(i => i.IsEnable.Value)))
                            {
                                if (item.SerialNumbers.Any())
                                {
                                    data.AddRange(item.SerialNumbers.Select(s => new
                                    {
                                        OrderID = s.OrderID.Value,
                                        SkuNo = s.ProductID,
                                        SerialsNo = s.SerialNumber,
                                        QTY = 1
                                    }).ToList());
                                }
                                else
                                {
                                    data.Add(new
                                    {
                                        OrderID = item.OrderID.Value,
                                        SkuNo = item.ProductID,
                                        SerialsNo = "",
                                        QTY = item.Qty.Value
                                    });
                                }
                            }

                            if (data != null)
                            {
                                using (StreamWriter streamWriter = new StreamWriter(request.GetRequestStream()))
                                {
                                    streamWriter.Write(JsonConvert.SerializeObject(data));
                                    streamWriter.Flush();
                                }

                                HttpWebResponse httpResponse = (HttpWebResponse)request.GetResponse();
                                using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
                                {
                                    AjaxResult postResult = JsonConvert.DeserializeObject<AjaxResult>(streamReader.ReadToEnd());
                                    if (!postResult.status) throw new Exception(postResult.message);
                                    MyHelp.Log("Inventory", box.BoxID, string.Format("Box【{0}】傳送出貨資料至測試系統", box.BoxID), Session);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            string errorMsg = e.InnerException != null ? e.InnerException.Message.Trim() : e.Message.Trim();
                            MyHelp.Log("Inventory", box.BoxID, string.Format("傳送出貨資料至測試系統失敗，請通知處理人員：{0}", errorMsg), Session);
                        }

                        break;
                    case 1:
                        if (!box.Packages.Any(p => p.IsEnable.Value)) throw new Exception("尚未有任何訂單，無法前往下一箱!");

                        var newBoxID = string.Format("{0}{1}", box.MainBox, boxNo);
                        Box newBox = db.Box.Find(newBoxID);
                        if (newBox == null)
                        {
                            newBox = new Box()
                            {
                                IsEnable = true,
                                BoxID = newBoxID,
                                MainBox = box.MainBox,
                                WITID = box.WITID,
                                DirectLine = box.DirectLine,
                                FirstMileMethod = box.FirstMileMethod,
                                WarehouseFrom = box.WarehouseFrom,
                                WarehouseTo = box.WarehouseTo,
                                BoxType = box.BoxType,
                                ShippingStatus = box.ShippingStatus,
                                BoxNo = boxNo,
                                CurrentNo = boxNo,
                                Note = box.Note,
                                Create_at = DateTime.UtcNow
                            };
                            db.Entry(newBox).State = System.Data.Entity.EntityState.Added;
                        }
                        else
                        {
                            newBox.IsEnable = true;
                            db.Entry(newBox).State = System.Data.Entity.EntityState.Modified;
                        }

                        foreach (Box boxData in db.Box.Where(b => b.IsEnable && b.MainBox.Equals(box.MainBox)).ToList())
                        {
                            boxData.BoxNo = boxNo;
                            db.Entry(boxData).State = System.Data.Entity.EntityState.Modified;
                        }
                        db.SaveChanges();
                        result.data = new { boxID = newBoxID };
                        break;
                }
            }
            catch (Exception e)
            {
                result.message = string.Format("Box【{0}】出貨失敗，錯誤：", boxID) + (e.InnerException != null ? e.InnerException.Message.Trim() : e.Message.Trim());
                result.status = false;

                MyHelp.Log("Box", box.BoxID, result.message, Session);
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        private object Box_Shippping(List<Box> boxList, ShippingMethod method)
        {
            List<object> fileList = new List<object>();
            List<object> errorList = new List<object>();

            SC_WebService SCWS = new SC_WebService(Session["ApiUserName"].ToString(), Session["ApiPassword"].ToString());
            TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;

            if (!SCWS.Is_login) throw new Exception("SC is not login");

            foreach (Box box in boxList)
            {
                MyHelp.Log("Box", box.BoxID, string.Format("開始檢查 Box【{0}】內的訂單狀態", box.BoxID), Session);

                DirectLine directLine = db.DirectLine.Find(box.DirectLine);
                foreach (Packages package in box.Packages.Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨)).ToList())
                {
                    DirectLineLabel label = package.Label;
                    OrderData order = SCWS.Get_OrderData(package.OrderID.Value);
                    if (CheckOrderStatus(package, order.Order))
                    {
                        if (label.Status.Equals((byte)EnumData.LabelStatus.正常))
                        {
                            if (directLine.Abbreviation.Equals("IDS (US)"))
                            {
                                var IDS = new DirectLineApi.IDS.IDS_API(package.Method.Carriers.CarrierAPI);
                                var IDS_Result = IDS.GetTrackingNumber(package);
                                if (IDS_Result.trackingnumber.Any(t => t.First().Equals(package.OrderID.ToString())))
                                {
                                    MyHelp.Log("Packages", package.ID, string.Format("取得訂單【{0}】的Tracking Number", package.OrderID), Session);

                                    package.TrackingNumber = IDS_Result.trackingnumber.Last(t => t.First().Equals(package.OrderID.ToString()))[1];
                                }

                                //if (!string.IsNullOrEmpty(package.TrackingNumber))
                                //{
                                //    ThreadTask SyncTask = new ThreadTask(string.Format("Direct Line 訂單【{0}】SC更新", package.OrderID));
                                //    SyncTask.AddWork(factory.StartNew(() =>
                                //    {
                                //        SyncTask.Start();
                                //        SyncProcess sync = new SyncProcess(Session);
                                //        return sync.Update_Tracking(package);
                                //    }));

                                //    label.Status = (byte)EnumData.LabelStatus.完成;
                                //}
                            }
                            //else
                            //{
                            //    foreach (Items item in package.Items.Where(i => i.IsEnable.Value).ToList())
                            //    {
                            //        if (item.SerialNumbers.Any()) SCWS.Update_ItemSerialNumber(item.ID, item.SerialNumbers.Select(s => s.SerialNumber).ToArray());
                            //    }
                            //}

                            foreach (Items item in package.Items.Where(i => i.IsEnable.Value).ToList())
                            {
                                if (item.SerialNumbers.Any()) SCWS.Update_ItemSerialNumber(item.ID, item.SerialNumbers.Select(s => s.SerialNumber).ToArray());
                            }

                            package.ProcessStatus = (int)EnumData.ProcessStatus.已出貨;
                            MyHelp.Log("Packages", package.ID, string.Format("訂單【{0}】的完成出貨", package.OrderID), Session);
                        }
                    }
                    else
                    {
                        MyHelp.Log("Box", box.BoxID, string.Format("訂單【{0}】資料狀態異常", package.OrderID.Value), Session);

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

                    db.Entry(package).State = System.Data.Entity.EntityState.Modified;
                    db.Entry(label).State = System.Data.Entity.EntityState.Modified;
                }

                MyHelp.Log("Box", box.BoxID, string.Format("Box【{0}】儲存資料", box.BoxID), Session);

                box.IsReserved = false;
                box.ShippingStatus = (byte)EnumData.DirectLineStatus.運輸中;
                db.Entry(box).State = System.Data.Entity.EntityState.Modified;
            }
            db.SaveChanges();

            string boxID = boxList[0].MainBox;
            boxList = db.Box.Where(b => b.IsEnable && b.MainBox.Equals(boxID)).OrderBy(b => b.Create_at).ToList();
            DateTime create_at = boxList[0].Create_at;

            MyHelp.Log("Box", boxID, string.Format("開始產出 Box【{0}】報關資料", boxID), Session);

            ShipProcess shipProcess = new ShipProcess(SCWS);
            ShipResult boxResult = shipProcess.Dispatch(boxList);
            if (boxResult.Status)
            {
                MyHelp.Log("Box", boxID, string.Format("開始產出 Box【{0}】報關資料成功", boxID), Session);

                string[] fileName = new string[2];
                string[] filePath = new string[2];
                int[] amount = new int[] { 0, 0 };
                string basePath = HostingEnvironment.MapPath("~/FileUploads");

                /***** 提貨單 *****/
                fileName[0] = "AirWaybill.pdf";
                filePath[0] = Path.Combine(basePath, "export", "Box", create_at.ToString("yyyy/MM/dd"), boxID, fileName[0]);
                /***** 提貨單 *****/

                switch (method.Carriers.CarrierAPI.Type)
                {
                    case (byte)EnumData.CarrierType.DHL:
                        bool DHL_pdf = !System.IO.File.Exists(Path.Combine(basePath, "export", "Box", create_at.ToString("yyyy/MM/dd"), boxID, "Invoice.xls"));

                        /***** 商業發票 *****/
                        fileName[1] = DHL_pdf ? "Invoice.pdf" : "Invoice.xls";
                        filePath[1] = Path.Combine(basePath, "export", "Box", create_at.ToString("yyyy/MM/dd"), boxID, fileName[1]);
                        /***** 商業發票 *****/

                        amount = new int[] { 2, DHL_pdf ? 0 : 2 };
                        break;
                    case (byte)EnumData.CarrierType.FedEx:
                        /***** 商業發票 *****/
                        fileName[1] = "Invoice.xls";
                        filePath[1] = Path.Combine(basePath, "export", "Box", create_at.ToString("yyyy/MM/dd"), boxID, fileName[1]);
                        /***** 商業發票 *****/

                        amount = new int[] { 1, 4 };
                        break;
                }

                MyHelp.Log("Box", boxID, string.Format("寄送 Box【{0}】報關資料", boxID), Session);
                SendMailToCarrier(boxList, method, db.DirectLine.Find(boxList[0].DirectLine));

                MyHelp.Log("Box", boxID, string.Format("Box【{0}】完成出貨", boxID), Session);

                fileList.Add(new { fileName, filePath, amount, printerName = method.PrinterName });
            }
            else
            {
                foreach (Box box in boxList)
                {
                    box.ShippingStatus = (byte)EnumData.DirectLineStatus.未發貨;
                    db.Entry(box).State = System.Data.Entity.EntityState.Modified;

                    string error = string.Format("產出 Box【{0}】報關資料失敗：{1}", box.BoxID, boxResult.Message);
                    MyHelp.Log("Box", box.BoxID, error, Session);
                }
            }
            db.SaveChanges();

            boxID = "";
            return new { boxID, fileList, errorList };
        }

        private bool CheckOrderStatus(Packages package, Order order)
        {
            bool OrderCompare = package.Orders.StatusCode.Value.Equals((int)order.StatusCode);
            bool PaymentCompare = package.Orders.PaymentStatus.Value.Equals((int)order.PaymentStatus);

            return OrderCompare && PaymentCompare;
        }

        public ActionResult ProductList(string TargetID, string Type)
        {
            Packages package = null;
            List<string[]> productList = new List<string[]>();

            switch (Type)
            {
                case "WaitingOrder":
                case "BoxOrder":
                    int packageID = int.Parse(TargetID);
                    package = db.Packages.AsNoTracking().FirstOrDefault(p => p.IsEnable.Value && p.ID.Equals(packageID));
                    break;

                case "CancelLabel":
                    package = db.Packages.AsNoTracking().FirstOrDefault(p => p.IsEnable.Value && p.TagNo.Equals(TargetID));
                    break;
            }

            if (package != null)
            {
                foreach (Items item in package.Items.Where(i => i.IsEnable.Value))
                {
                    switch (Type)
                    {
                        case "WaitingOrder":
                            productList.Add(new string[] { item.ProductID, item.Skus.ProductName, item.Qty.ToString() });

                            if (item.BundleItems.Any())
                            {
                                foreach (BundleItems bundleItem in item.BundleItems)
                                {
                                    productList.Add(new string[] { bundleItem.ProductID, bundleItem.Skus.ProductName, bundleItem.Qty.ToString(), "" });
                                }
                            }
                            break;

                        case "BoxOrder":
                            for (int i = 0; i < item.Qty.Value; i++)
                            {
                                productList.Add(new string[] { item.ProductID, item.Skus.ProductName, item.SerialNumbers.Skip(i).Any() ? item.SerialNumbers.Skip(i).First().SerialNumber : "" });
                            }
                            break;

                        case "CancelLabel":
                            SerialNumberForRefundLabel[] SerialRefund = db.SerialNumberForRefundLabel.AsNoTracking().Where(f => f.oldOrderID.Equals(package.OrderID.Value)).ToArray();
                            for (int i = 0; i < item.Qty.Value; i++)
                            {
                                string serial = SerialRefund.Where(s => s.Sku.Equals(item.SKU)).Skip(i).FirstOrDefault().SerialNumber ?? "";

                                productList.Add(new string[] { item.ProductID, item.Skus.ProductName, serial });
                            }
                            break;
                    }
                }
            }

            ViewBag.productList = productList;
            return PartialView(string.Format("List_{0}", Type));
        }

        public ActionResult GetShippingMethodByWarehouse(int warehouseID)
        {
            AjaxResult result = new AjaxResult();

            try
            {
                Warehouses warehouse = db.Warehouses.AsNoTracking().First(w => w.IsEnable.Value && w.ID.Equals(warehouseID));
                if (warehouse == null) throw new Exception("找不到出貨倉!");

                int[] methodIDs = new int[] { };
                if (!string.IsNullOrEmpty(warehouse.CarrierData))
                {
                    Dictionary<string, bool> carrierData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, bool>>(warehouse.CarrierData);
                    methodIDs = carrierData.Where(c => c.Value).Select(c => int.Parse(c.Key)).ToArray();
                }

                var methodFilter = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable);
                if (methodIDs.Any()) methodFilter = methodFilter.Where(m => methodIDs.Contains(m.ID));

                string option = "";
                foreach (var method in methodFilter.ToList())
                {
                    option += string.Format("<option value='{0}'>{1}</option>", method.ID, method.Name);
                }

                result.data = option;
            }
            catch (Exception e)
            {
                result.status = false;
                result.message = e.InnerException != null && string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult ReDispatch(string labelID, int methodID, int? newOrderID)
        {
            AjaxResult result = new AjaxResult();

            try
            {
                Packages oldPackage = db.Packages.FirstOrDefault(p => p.IsEnable.Value && p.TagNo.Equals(labelID));
                if (oldPackage == null) throw new Exception("沒有找到此訂單!");

                var serials = db.SerialNumberForRefundLabel.AsNoTracking().Where(s => !s.IsUsed && s.oldLabelID.Equals(labelID) && s.oldOrderID.Equals(oldPackage.OrderID.Value)).ToList();
                if (!serials.Any()) throw new Exception("沒有找到任何序號!");

                TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;

                lock (factory)
                {
                    ThreadTask threadTask = new ThreadTask(string.Format("標籤【{0}】再次寄送", labelID));

                    HttpContextBase CurrentHttpContext = HttpContext;
                    threadTask.AddWork(factory.StartNew(session =>
                    {
                        threadTask.Start();
                        string message = "";

                        HttpSessionStateBase Session = (HttpSessionStateBase)session;

                        try
                        {
                            Orders = new GenericRepository<Orders>(db);
                            Addresses = new GenericRepository<Addresses>(db);
                            Packages = new GenericRepository<Packages>(db);
                            Items = new GenericRepository<Items>(db);
                            SerialNumbers = new GenericRepository<SerialNumbers>(db);

                            if (!newOrderID.HasValue)
                            {
                                MyHelp.Log("Orders", oldPackage.OrderID.Value, string.Format("訂單【{0}】Set Resolution to Replace", oldPackage.OrderID.Value), Session);

                                using (SCWS = new SC_WebService(Session["ApiUserName"].ToString(), Session["ApiPassword"].ToString()))
                                {
                                    if (!SCWS.Is_login) throw new Exception("SC is not login");

                                    PurchaseOrderService.RMAData SC_RMA = SCWS.Get_RMA_Data(oldPackage.RMAId.Value);
                                    foreach (var SC_RMAItem in SC_RMA.Items)
                                    {
                                        SC_RMAItem.ReturnResolution = PurchaseOrderService.ReturnResolutionCodeType.Replace;
                                        SCWS.Update_RAM_Item(SC_RMAItem);
                                    }

                                    OrderCreationService.RMAItem SC_Item = SCWS.Get_RMA_Item(oldPackage.OrderID.Value).FirstOrDefault(i => !i.NewOrderID.Equals(0));
                                    if (SC_Item == null) throw new Exception("沒有新訂單號碼!");

                                    newOrderID = SC_Item.NewOrderID;
                                    SyncNewOrder(oldPackage, newOrderID.Value);
                                }
                            }

                            Orders newOrder = Orders.Get(newOrderID.Value);
                            if (!CheckLabelSku(newOrder, serials)) throw new Exception(string.Format("新訂單【{0}】產品、數量不符合!", newOrder.OrderID));

                            MyHelp.Log("Orders", newOrder.OrderID, string.Format("訂單【{0}】更新出貨倉、運輸方式、產品序號", newOrder.OrderID), Session);

                            Packages newPackage = newOrder.Packages.First(p => p.IsEnable.Value);
                            newPackage.ShippingMethod = methodID;
                            Packages.Update(newPackage, newPackage.ID);

                            using (SCWS = new SC_WebService(Session["ApiUserName"].ToString(), Session["ApiPassword"].ToString()))
                            {
                                if (!SCWS.Is_login) throw new Exception("SC is not login");

                                int shipWarehouseID = oldPackage.Items.First(i => i.IsEnable.Value).ReturnedToWarehouseID.Value;
                                foreach (Items newItem in newPackage.Items.Where(i => i.IsEnable.Value).ToList())
                                {
                                    newItem.ShipFromWarehouseID = shipWarehouseID;
                                    Items.Update(newItem, newItem.ID);

                                    string[] serialArray = serials.Where(s => s.Sku.Equals(newItem.ProductID)).Select(s => s.SerialNumber).ToArray();
                                    SCWS.Update_ItemSerialNumber(newItem.ID, serialArray);

                                    foreach (string serial in serialArray)
                                    {
                                        SerialNumbers.Create(new SerialNumbers()
                                        {
                                            OrderID = newItem.OrderID,
                                            OrderItemID = newItem.ID,
                                            ProductID = newItem.ProductID,
                                            SerialNumber = serial,
                                            KitItemID = 0
                                        });
                                    }
                                }
                            }
                            Packages.SaveChanges();

                            MyHelp.Log("Orders", newOrder.OrderID, string.Format("檢查新訂單【{0}】寄送國家", newOrder.OrderID), Session);

                            if (!string.IsNullOrEmpty(newPackage.Method.CountryData))
                            {
                                var countryData = JsonConvert.DeserializeObject<Dictionary<string, bool>>(newPackage.Method.CountryData);
                                if (!countryData.ContainsKey(newOrder.Addresses.CountryCode.ToUpper()))
                                {
                                    throw new Exception(string.Format("新訂單【{0}】國家名稱不合，請重新確認", newOrder.OrderID));
                                }

                                if (!countryData[newOrder.Addresses.CountryCode.ToUpper()])
                                {
                                    throw new Exception(string.Format("新訂單【{0}】不可寄送至國家{1}", newOrder.OrderID, newOrder.Addresses.CountryName));
                                }
                            }

                            MyHelp.Log("Orders", newOrder.OrderID, string.Format("提交新訂單【{0}】", newOrder.OrderID), Session);

                            ShipResult ShipResult;
                            using (SCWS = new SC_WebService(Session["ApiUserName"].ToString(), Session["ApiPassword"].ToString()))
                            {
                                if (!SCWS.Is_login) throw new Exception("SC is not login");

                                var SC_order = SCWS.Get_OrderData(newPackage.OrderID.Value).Order;
                                var SC_items = SC_order.Items.Where(i => i.PackageID.Equals(newPackage.ID)).ToArray();
                                foreach (var item in SC_items)
                                {
                                    if (!db.Skus.AsNoTracking().Any(s => s.Sku.Equals(item.ProductID))) throw new Exception(string.Format("系統尚未有品號 {0} 資料!", item.ProductID));

                                    item.ShipFromWareHouseID = newPackage.Items.First(i => i.IsEnable == true && i.ID == item.ID).ShipFromWarehouseID.Value;
                                    SCWS.Update_OrderItem(item);
                                }
                                MyHelp.Log("Orders", newPackage.OrderID, "更新訂單包裹的出貨倉", Session);

                                ShipProcess Process = new ShipProcess(SCWS);
                                Process.Init(newPackage);
                                ShipResult = Process.Dispatch();
                            }

                            if (ShipResult.Status)
                            {
                                MyHelp.Log("Orders", newOrder.OrderID, string.Format("新訂單【{0}】提交成功", newOrder.OrderID), Session);

                                IRepository<Box> Box = new GenericRepository<Box>(db);
                                IRepository<DirectLineLabel> DirectLineLabel = new GenericRepository<DirectLineLabel>(db);

                                newOrder = Orders.Get(newOrder.OrderID);
                                newPackage = newOrder.Packages.First(p => p.IsEnable.Value);
                                newPackage.ProcessStatus = (int)EnumData.ProcessStatus.已出貨;

                                MyHelp.Log("Orders", newOrder.OrderID, string.Format("新訂單【{0}】置入Box", newOrder.OrderID), Session);

                                DirectLine directLine = db.DirectLine.AsNoTracking().FirstOrDefault(d => d.ID.Equals(newPackage.Method.DirectLine));
                                using (BoxManage BoxManage = new BoxManage(Session))
                                {
                                    DirectLineLabel newLabel = newPackage.Label;
                                    Box box = BoxManage.GetCurrentBox(directLine, newPackage.Items.First(i => i.IsEnable.Value).ShipFromWarehouseID.Value);
                                    box.ShippingStatus = (byte)EnumData.DirectLineStatus.已到貨;
                                    newPackage.BoxID = newLabel.BoxID = box.BoxID;
                                    Box.Update(box, box.BoxID);
                                    Packages.Update(newPackage, newPackage.ID);
                                    DirectLineLabel.Update(newLabel, newLabel.LabelID);
                                    Packages.SaveChanges();
                                }

                                MyHelp.Log("Orders", newOrder.OrderID, string.Format("新訂單【{0}】寄送 {1} 出貨通知", newOrder.OrderID, directLine.Abbreviation), Session);

                                using (CaseLog CaseLog = new CaseLog(oldPackage, Session, CurrentHttpContext))
                                {
                                    CaseLog.SendResendShipmentMail(newPackage, serials.First().Create_at);
                                }
                            }
                            else
                            {
                                foreach (var serial in SerialNumbers.GetAll().Where(s => s.OrderID.Value.Equals(newOrder.OrderID)).ToList())
                                {
                                    SerialNumbers.Delete(serial);
                                }
                                SerialNumbers.SaveChanges();

                                string msg = string.Format("新訂單【{0}】提交失敗", newOrder.OrderID);
                                MyHelp.Log("Orders", newOrder.OrderID, msg + " - " + ShipResult.Message, Session);
                                throw new Exception(msg + "! - " + ShipResult.Message);
                            }

                            MyHelp.Log("DirectLineLabel", labelID, string.Format("完成標籤【{0}】再次寄送", labelID), Session);

                            IRepository<SerialNumberForRefundLabel> RefundSerial = new GenericRepository<SerialNumberForRefundLabel>(db);
                            foreach (var serial in db.SerialNumberForRefundLabel.AsNoTracking().Where(s => !s.IsUsed && s.oldLabelID.Equals(labelID)).ToList())
                            {
                                serial.IsUsed = true;
                                serial.newLabelID = newPackage.TagNo;
                                serial.newOrderID = newPackage.OrderID;
                                RefundSerial.Update(serial, serial.ID);
                            }
                            RefundSerial.SaveChanges();
                        }
                        catch (Exception e)
                        {
                            message = e.InnerException != null && string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                            MyHelp.Log("DirectLineLabel", labelID, string.Format("標籤【{0}】再次寄送失敗 - {1}", labelID, message), Session);
                        }

                        return message;
                    }, Session));
                }


            }
            catch (Exception e)
            {
                result.status = false;
                result.message = e.InnerException != null && string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                MyHelp.Log("DirectLineLabel", labelID, string.Format("標籤【{0}】再次寄送失敗 - {1}", labelID, result.message), Session);
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        private bool SyncNewOrder(Packages oldPackage, int newOrderID)
        {
            bool syncResult = true;

            try
            {
                MyHelp.Log("Orders", newOrderID, string.Format("開始新訂單【{0}】資料同步", newOrderID), Session);

                var SC_Order = SCWS.Get_OrderData(newOrderID);
                Addresses address = new Addresses() { IsEnable = true };
                Addresses.Create(address);
                Addresses.SaveChanges();

                Orders.Create(new Orders() { OrderID = SC_Order.Order.ID, ShippingAddress = address.Id, eBayUserID = SC_Order.User.eBayUserID });
                Orders.SaveChanges();

                SyncProcess Sync = new SyncProcess(Session);
                string syncError = Sync.Sync_Order(newOrderID);
                if (!string.IsNullOrEmpty(syncError)) throw new Exception(syncError);

                Orders newOrder = Orders.Get(newOrderID);
                Orders oldOrder = oldPackage.Orders;
                if (!newOrder.ParentOrderID.Equals(oldOrder.OrderID)) throw new Exception("新、舊訂單號碼不相同!");

                newOrder.OrderCurrencyCode = oldOrder.OrderCurrencyCode;
                Orders.Update(newOrder, newOrder.OrderID);

                // 分批資料同步太過複雜，所以暫時不考慮 //
                Packages newPackage = newOrder.Packages.First(p => p.IsEnable.Value);
                newPackage.DeclaredTotal = oldPackage.DeclaredTotal;
                newPackage.DLDeclaredTotal = oldPackage.DLDeclaredTotal;
                newPackage.ShippingMethod = oldPackage.ShippingMethod;
                newPackage.Export = oldPackage.Export;
                newPackage.ExportMethod = oldPackage.ExportMethod;
                newPackage.UploadTracking = oldPackage.UploadTracking;
                Packages.Update(newPackage, newPackage.ID);

                foreach (Items newItem in newPackage.Items.Where(i => i.IsEnable.Value))
                {
                    Items oldItem = oldPackage.Items.First(i => i.IsEnable.Value && i.ProductID.Equals(newItem.ProductID));
                    newItem.ShipFromWarehouseID = oldItem.ShipFromWarehouseID;
                    newItem.DeclaredValue = oldItem.DeclaredValue;
                    newItem.DLDeclaredValue = oldItem.DLDeclaredValue;
                    Items.Update(newItem, newItem.ID);
                }

                Orders.SaveChanges();
                MyHelp.Log("Orders", newOrderID, string.Format("新訂單【{0}】資料同步完成", newOrderID), Session);
            }
            catch (Exception e)
            {
                syncResult = false;
                string msg = e.InnerException != null && string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                MyHelp.Log("Orders", newOrderID, string.Format("新訂單【{0}】同步失敗 - {1}", newOrderID, msg), Session);
            }

            return syncResult;
        }

        private bool CheckLabelSku(Orders order, List<SerialNumberForRefundLabel> serials)
        {
            bool checkResult = true;

            try
            {
                List<Items> itemList = order.Items.Where(i => i.IsEnable.Value).ToList();

                if (!itemList.Any()) throw new Exception("找不到產品資料!");
                if (itemList.Sum(i => i.Qty.Value) != serials.Count()) throw new Exception("產品總數量不相同!");
                foreach (var group in itemList.GroupBy(i => i.ProductID))
                {
                    if (group.Sum(i => i.Qty.Value) != serials.Count(s => s.Sku.Equals(group.Key)))
                        throw new Exception(string.Format("產品-{0}數量不相同!", group.Key));
                }
            }
            catch (Exception e)
            {
                checkResult = false;
                string msg = e.InnerException != null && string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                MyHelp.Log("Skus", order.OrderID, string.Format("訂單【{0}】產品核對失敗 - {1}", order.OrderID, msg), Session);
            }

            return checkResult;
        }

        [HttpGet]
        public ActionResult CheckBoxStatus()
        {
            AjaxResult result = new AjaxResult();

            TaskFactory Factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
            ThreadTask threadTask = new ThreadTask("追蹤 Direct Line Box 狀態");

            try
            {
                lock (Factory)
                {
                    threadTask.AddWork(Factory.StartNew(Session =>
                    {
                        threadTask.Start();
                        string message = "";

                        string sendMail = "dispatch-qd@hotmail.com";
                        string mailTitle;
                        string mailBody;
                        string[] receiveMails;
                        string[] ccMails = new string[] { "peter@qd.com.tw", "kelly@qd.com.tw", "demi@qd.com.tw" };

                        db = new QDLogisticsEntities();
                        HttpSessionStateBase session = (HttpSessionStateBase)Session;

                        MyHelp.Log("Box", null, "追蹤 Direct Line Box 狀態", session);

                        try
                        {
                            List<byte> statusList = new List<byte>() { (byte)EnumData.DirectLineStatus.運輸中, (byte)EnumData.DirectLineStatus.延誤中 };
                            List<Box> boxList = db.Box.Where(b => b.IsEnable && b.BoxType.Equals((byte)EnumData.DirectLineBoxType.DirectLine) && statusList.Contains(b.ShippingStatus)).ToList();
                            if (boxList.Any())
                            {
                                TrackOrder TrackOrder = new TrackOrder();
                                foreach (Box box in boxList)
                                {
                                    ShippingMethod method = db.ShippingMethod.Find(box.FirstMileMethod);

                                    TrackResult boxResult = TrackOrder.Track(box, method.Carriers.CarrierAPI);
                                    box.PickUpDate = boxResult.PickUpDate;
                                    box.DeliveryNote = boxResult.DeliveryNote;
                                    box.DeliveryDate = boxResult.DeliveryDate;
                                    db.Entry(box).State = System.Data.Entity.EntityState.Modified;

                                    if (boxResult.DeliveryStatus.Equals((int)DeliveryStatusType.Delivered))
                                    {
                                        MyHelp.Log("Box", box.BoxID, "寄送Box到貨通知", session);

                                        box.ShippingStatus = box.ShippingStatus.Equals((byte)EnumData.DirectLineStatus.延誤中) ? (byte)EnumData.DirectLineStatus.延誤後抵達 : (byte)EnumData.DirectLineStatus.已到貨;
                                        db.Entry(box).State = System.Data.Entity.EntityState.Modified;

                                        string basePath = HostingEnvironment.MapPath("~/FileUploads");
                                        DirectLine directLine = db.DirectLine.Find(box.DirectLine);
                                        switch (directLine.Abbreviation)
                                        {
                                            case "IDS":
                                            case "IDS (US)":
                                                string[] address;

                                                receiveMails = new string[] { "gloria.chiu@contin-global.com", "cherry.chen@contin-global.com", "TWCS@contin-global.com", "contincs@gmail.com", "shipping_qd@hotmail.com" };
                                                mailTitle = string.Format("TW018 至優網有限公司 First Mile 包裹 {0} {1} ({2} 件包裹) 已抵達", method.Carriers.Name, box.TrackingNumber, box.DirectLineLabel.Count(l => l.IsEnable));
                                                mailBody = "您好<br /><br />包裹已抵達:<br />{0}<br /><br />內容包含:<br />{1}<br /><br />請盡速處理並確認已經全數收到<br />謝謝!";
                                                address = new string[] { directLine.StreetLine1, directLine.StreetLine2, directLine.City, directLine.StateName, directLine.CountryName, directLine.PostalCode };
                                                mailBody = string.Format(mailBody, string.Join(" ", address.Except(new string[] { "", null })), string.Join("<br />", box.DirectLineLabel.Where(l => l.IsEnable).Select(l => l.LabelID).ToArray()));
                                                bool IDS_Status = MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, null, null, false);
                                                if (!IDS_Status) MyHelp.Log("Box", box.BoxID, "寄送Box到貨通知失敗", session);
                                                break;
                                            case "ECOF":
                                                receiveMails = new string[] { "customerservice@ecof.com.au", "sophia.wang@ecof.com.au", "Ada.chen@ecof.com.au", "mandy.liang@ecof.com.au" };
                                                var packageList = box.Packages.Where(p => p.IsEnable.Value).ToList();
                                                mailTitle = string.Format("ARRIVED: {0} {1}, {2}pcs", method.Carriers.Name, box.TrackingNumber, packageList.Count());
                                                mailBody = string.Format("Tracking {0}({1}pcs, {2}<br />", box.TrackingNumber, packageList.Count(), box.BoxID);
                                                mailBody += string.Join("<br />", packageList.Select(p => string.Format("{0}-{1}-{2}", p.Items.First(i => i.IsEnable.Value).ProductID, p.OrderID.Value, p.TrackingNumber)).ToArray());

                                                List<Tuple<Stream, string>> ECOF_File = new List<Tuple<Stream, string>>();
                                                using (var file = new ZipFile())
                                                {
                                                    var memoryStream = new MemoryStream();
                                                    foreach (Packages package in box.Packages.Where(p => p.IsEnable.Value).ToList())
                                                    {
                                                        string AWB_File = Path.Combine(basePath, package.FilePath, string.Format("{0}-{1}-{2}.pdf", package.Items.First(i => i.IsEnable.Value).ProductID, package.OrderID, package.TrackingNumber));
                                                        if (!System.IO.File.Exists(AWB_File))
                                                        {
                                                            System.IO.File.Copy(Path.Combine(basePath, package.FilePath, "AirWaybill.pdf"), AWB_File);
                                                        }
                                                        file.AddFile(AWB_File, "");
                                                    }
                                                    file.Save(memoryStream);
                                                    memoryStream.Seek(0, SeekOrigin.Begin);
                                                    ECOF_File.Add(new Tuple<Stream, string>(memoryStream, "Labels.zip"));
                                                }

                                                bool ECOF_Status = MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, null, ECOF_File, false);
                                                if (!ECOF_Status) MyHelp.Log("Box", box.BoxID, "寄送Box到貨通知失敗", session);
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        DateTime deliveryDate = MyHelp.SkipWeekend(box.Create_at.AddDays(2));
                                        if (DateTime.Compare(deliveryDate, DateTime.UtcNow) < 0 && box.ShippingStatus.Equals((byte)EnumData.DirectLineStatus.運輸中))
                                        {
                                            box.ShippingStatus = (byte)EnumData.DirectLineStatus.延誤中;
                                            db.Entry(box).State = System.Data.Entity.EntityState.Modified;
                                        }
                                    }
                                }
                                db.SaveChanges();
                            }
                        }
                        catch (Exception e)
                        {
                            message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                            MyHelp.Log("Box", null, message, session);
                        }

                        return message;
                    }, Session));
                }
            }
            catch (Exception e)
            {
                result.status = false;

                string message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                MyHelp.Log("Box", null, message, Session);
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult SendWaitingOrder(string DL)
        {
            AjaxResult result = new AjaxResult();

            TaskFactory Factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
            ThreadTask threadTask = new ThreadTask(string.Format("寄送 DL {0} 待出貨訂單", DL));

            try
            {
                lock (Factory)
                {
                    threadTask.AddWork(Factory.StartNew(Session =>
                    {
                        threadTask.Start();
                        string message = "";

                        string sendMail = "dispatch-qd@hotmail.com";
                        string mailTitle;
                        string mailBody = "";
                        string[] receiveMails;
                        string[] ccMails = new string[] { "peter@qd.com.tw", "kelly@qd.com.tw", "demi@qd.com.tw" };

                        db = new QDLogisticsEntities();
                        HttpSessionStateBase session = (HttpSessionStateBase)Session;

                        MyHelp.Log("DirectLine", null, string.Format("寄送 DL {0} 待出貨訂單", DL), session);

                        try
                        {
                            switch (DL)
                            {
                                case "IDS US":
                                    DateTime now = DateTime.Now;
                                    DateTime noon = new DateTime(now.Year, now.Month, now.Day, 11, 55, 0);
                                    DateTime evening = new DateTime(now.Year, now.Month, now.Day, 16, 55, 0);

                                    var packageFilter = db.Packages.Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨));

                                    if (now.Hour == noon.Hour)
                                    {
                                        var start = evening.AddDays(-1).ToUniversalTime();
                                        var end = noon.ToUniversalTime();
                                        packageFilter = packageFilter.Where(p => p.ShipDate.Value.CompareTo(end) <= 0 && p.ShipDate.Value.CompareTo(start) > 0);
                                    }
                                    if (now.Hour == evening.Hour)
                                    {
                                        var start = noon.ToUniversalTime();
                                        var end = evening.ToUniversalTime();
                                        packageFilter = packageFilter.Where(p => p.ShipDate.Value.CompareTo(end) <= 0 && p.ShipDate.Value.CompareTo(start) > 0);
                                    }

                                    var packageList = packageFilter.Join(db.ShippingMethod.Where(m => m.IsEnable && m.DirectLine.Equals(4)), p => p.ShippingMethod.Value, m => m.ID, (p, m) => p).ToList();
                                    if (packageList.Any())
                                    {
                                        string basePath = HostingEnvironment.MapPath("~/FileUploads");
                                        var filePath = Path.Combine(basePath, "mail", DateTime.UtcNow.ToString("yyyy/MM/dd"));
                                        if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);

                                        var FileData = new { fileName = string.Format("DirectLine-IDS({0}).xlsx", now.ToString("tt")), samplePath = Path.Combine(basePath, "sample", "DL-IDS.xlsx") };
                                        using (FileStream fsIn = new FileStream(FileData.samplePath, FileMode.Open))
                                        {
                                            XSSFWorkbook workbook = new XSSFWorkbook(fsIn);
                                            fsIn.Close();

                                            XSSFSheet sheet = (XSSFSheet)workbook.GetSheetAt(0);

                                            List<Items> itemList = packageList.SelectMany(p => p.Items.Where(i => i.IsEnable.Value)).ToList();
                                            if (itemList.Count() > 1)
                                            {
                                                int insertRow = 3, add = itemList.Count() - 1;
                                                MyHelp.ShiftRows(ref sheet, insertRow, sheet.LastRowNum, add);

                                                for (int row = insertRow; row < insertRow + add; row++)
                                                {
                                                    MyHelp.CopyRow(ref sheet, insertRow - 1, row, true, false, true, false);
                                                }
                                            }

                                            List<StockKeepingUnit.SkuData> SkuData = new List<StockKeepingUnit.SkuData>();
                                            using (StockKeepingUnit stock = new StockKeepingUnit())
                                            {
                                                var IDs = itemList.Select(i => i.ProductID).Distinct().ToArray();
                                                SkuData = stock.GetSkuData(IDs);
                                            }

                                            int rowIndex = 2, No = 1;
                                            foreach (var itemGroup in itemList.GroupBy(i => i.OrderID.Value))
                                            {
                                                if (itemGroup.Count() > 1)
                                                {
                                                    var count = itemGroup.Count() - 1;
                                                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(rowIndex, rowIndex + count, 0, 0));
                                                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(rowIndex, rowIndex + count, 1, 1));
                                                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(rowIndex, rowIndex + count, 3, 3));
                                                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(rowIndex, rowIndex + count, 4, 4));
                                                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(rowIndex, rowIndex + count, 5, 5));
                                                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(rowIndex, rowIndex + count, 6, 6));
                                                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(rowIndex, rowIndex + count, 7, 7));
                                                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(rowIndex, rowIndex + count, 8, 8));
                                                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(rowIndex, rowIndex + count, 9, 9));
                                                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(rowIndex, rowIndex + count, 10, 10));
                                                    sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(rowIndex, rowIndex + count, 11, 11));
                                                }

                                                foreach (var item in itemGroup)
                                                {
                                                    if (item.ID == itemGroup.First().ID)
                                                    {
                                                        sheet.GetRow(rowIndex).GetCell(0).SetCellValue(No++);
                                                        sheet.GetRow(rowIndex).GetCell(1).SetCellValue(item.Packages.TagNo);
                                                        sheet.GetRow(rowIndex).GetCell(3).SetCellValue("reqular");
                                                        sheet.GetRow(rowIndex).GetCell(4).SetCellValue(itemGroup.Sum(i => (SkuData.Any(s => s.Sku.Equals(i.ProductID)) ? SkuData.First(s => s.Sku.Equals(i.ProductID)).Weight : i.Skus.ShippingWeight) * i.Qty.Value));
                                                        sheet.GetRow(rowIndex).GetCell(5).SetCellValue("10*10*5 CM");
                                                        sheet.GetRow(rowIndex).GetCell(6).SetCellValue("FeDex");
                                                        sheet.GetRow(rowIndex).GetCell(7).SetCellValue("will update");
                                                        sheet.GetRow(rowIndex).GetCell(8).SetCellValue(itemGroup.Sum(i => i.Qty.Value));
                                                        sheet.GetRow(rowIndex).GetCell(10).SetCellValue(itemGroup.Sum(i => (double)i.DLDeclaredValue * i.Qty.Value));
                                                        sheet.GetRow(rowIndex).GetCell(11).SetCellValue(item.OrderID.Value);
                                                    }
                                                    sheet.GetRow(rowIndex++).GetCell(2).SetCellValue(item.ProductID);
                                                }
                                            }

                                            using (FileStream fsOut = new FileStream(Path.Combine(filePath, FileData.fileName), FileMode.Create))
                                            {
                                                workbook.Write(fsOut);

                                                fsOut.Close();
                                            }

                                            //receiveMails = new string[] { "qd.tuko@hotmail.com" };
                                            receiveMails = new string[] { "anita.chou@contin-global.com", "jennifer.siew@contin-global.com", "twcs@contin-global.com" };
                                            mailTitle = string.Format("TW018 - 台灣美國直線訂單 * {0}pcs _{1})", packageList.Count(), DateTime.Now.ToString("yyyyMMdd (tt)"));

                                            bool IDS_Status = MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, new string[] { Path.Combine(filePath, FileData.fileName) }, null, false);

                                            if (IDS_Status)
                                            {
                                                MyHelp.Log("DirectLine", null, mailTitle);
                                            }
                                            else
                                            {
                                                MyHelp.Log("DirectLine", null, string.Format("{0} 寄送失敗", mailTitle));
                                            }
                                        }
                                    }
                                    break;
                            }
                        }
                        catch (Exception e)
                        {
                            message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                            MyHelp.Log("DirectLine", null, message, session);
                        }

                        return message;
                    }, Session));
                }
            }
            catch (Exception e)
            {
                result.status = false;

                string message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                MyHelp.Log("Box", null, message, Session);
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult UploadFile(HttpPostedFileBase[] FileList)
        {
            AjaxResult result = new AjaxResult();
            List<string> Msg = new List<string>();

            try
            {
                if (FileList == null || !FileList.Any()) throw new Exception("沒有上傳檔案!");

                foreach (var file in FileList.Where(f => f.ContentLength > 0))
                {
                    var fileExtension = Path.GetExtension(file.FileName);
                    var fileName = Path.GetFileNameWithoutExtension(file.FileName);

                    try
                    {
                        if (!fileExtension.ToLower().Equals(".pdf")) throw new Exception(string.Format("此 {0} 不是PDF!", fileName));

                        var package = db.Packages.FirstOrDefault(p => p.TagNo.Equals(fileName));

                        if (package == null) throw new Exception(string.Format("此 {0} 找不到訂單!", fileName));

                        var filePath = Server.MapPath("~/FileUploads/" + package.FilePath);
                        if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);

                        var path = Path.Combine(filePath, "AirWaybill.pdf");
                        file.SaveAs(path);

                        MyHelp.Log("DL_Upload", file.FileName, null, Session);
                    }
                    catch (Exception e)
                    {
                        var msg = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                        Msg.Add(msg);

                        MyHelp.Log("DL_Upload", file.FileName, msg, Session);
                    }
                }
            }
            catch (Exception e)
            {
                Msg.Add(e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message);
            }

            if (Msg.Any())
            {
                result.status = false;
                result.message = string.Join("\n", Msg.ToArray());
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult ReserveBox(string BoxID)
        {
            AjaxResult result = new AjaxResult();

            try
            {
                var boxData = db.Box.Find(BoxID);

                if (boxData == null) throw new Exception("Not find Box");

                foreach (var box in db.Box.Where(b => b.IsEnable && b.MainBox.Equals(boxData.MainBox)))
                {
                    box.IsReserved = true;
                }

                db.SaveChanges();
            }
            catch (Exception e)
            {
                result.status = false;
                result.message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult CheckLabel(string LabelID, string BoxID, string Type)
        {
            AjaxResult result = new AjaxResult();

            try
            {
                var package = db.Packages.FirstOrDefault(p => p.TagNo.Equals(LabelID));

                if (package == null) throw new Exception("找不到此標籤號碼!");
                if (string.IsNullOrEmpty(package.BoxID) || !package.BoxID.Equals(BoxID)) throw new Exception(string.Format("此訂單【{0}】並不在 {1} 箱子裡!", package.OrderID, BoxID));

                string basePath = HostingEnvironment.MapPath("~/FileUploads");

                switch (Type)
                {
                    case "Label":
                        result.data = new
                        {
                            fileName = new string[] { "Label.pdf" },
                            filePath = new string[] { Path.Combine(basePath, package.FilePath, "Label.pdf") },
                            amount = new int[] { 1 },
                            printerName = package.Method.PrinterName
                        };
                        break;
                    case "AWB":
                        if (!System.IO.File.Exists(Path.Combine(basePath, package.FilePath, "AirWaybill.pdf")))
                        {
                            result.message = string.Format("訂單【{0}】沒有找到AWB，是否要將此包裹移出 {1} ?", package.OrderID, BoxID);
                        }
                        else
                        {
                            result.data = new
                            {
                                fileName = new string[] { "AirWaybill.pdf" },
                                filePath = new string[] { Path.Combine(basePath, package.FilePath, "AirWaybill.pdf") },
                                amount = new int[] { 1 },
                                printerName = package.Method.PrinterName
                            };
                        }
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

        [HttpPost]
        public ActionResult MoveLabel(string LabelID, string BoxID, string Action)
        {
            AjaxResult result = new AjaxResult();

            try
            {
                var package = db.Packages.FirstOrDefault(p => p.TagNo.Equals(LabelID));

                if (package == null) throw new Exception("找不到此標籤號碼!");

                switch (Action)
                {
                    case "MoveIn":
                        package.BoxID = BoxID;
                        package.Label.BoxID = BoxID;
                        package.ProcessStatus = (byte)EnumData.ProcessStatus.待出貨;
                        break;
                    case "MoveOut":
                        package.BoxID = null;
                        package.Label.BoxID = null;
                        package.ProcessStatus = (byte)EnumData.ProcessStatus.包貨;
                        break;
                }

                db.SaveChanges();
            }
            catch (Exception e)
            {
                result.status = false;
                result.message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }


        public void SendMailToCarrier(List<Box> boxList, ShippingMethod method, DirectLine directLine, bool reSend = false)
        {
            PickProduct = new GenericRepository<PickProduct>(db);

            List<Items> itemsList = boxList.SelectMany(b => b.Packages.Where(p => p.IsEnable.Value)).SelectMany(p => p.Items.Where(i => i.IsEnable.Value)).ToList();
            List<PickProduct> pickList = itemsList.Join(db.PickProduct.AsNoTracking().Where(p => p.IsEnable && p.IsPicked && (reSend || !p.IsMail)).ToList(), i => i.ID, pick => pick.ItemID, (i, pick) => pick).ToList();
            if (pickList.Any())
            {
                string boxID = boxList[0].MainBox;
                DateTime create_at = boxList[0].Create_at;
                var packageIDs = pickList.Select(pick => pick.PackageID.Value).Distinct().ToArray();
                List<Packages> packageList = db.Packages.Where(p => packageIDs.Contains(p.ID)).ToList();

                string basePath = HostingEnvironment.MapPath("~/FileUploads");
                string filePath = Path.Combine(basePath, "export", "box", create_at.ToString("yyyy/MM/dd"), boxID);
                if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);

                string sendMail = "dispatch-qd@hotmail.com";
                string mailTitle;
                string mailBody = "";
                string[] receiveMails;
                string[] ccMails = new string[] { "peter@qd.com.tw", "kelly@qd.com.tw", "demi@qd.com.tw" };

                string trackingNumber = string.Join(" & ", boxList.Select(b => b.TrackingNumber).ToArray());
                switch (directLine.Abbreviation)
                {
                    case "IDS":
                    case "IDS (US)":
                        MyHelp.Log("PickProduct", null, string.Format("寄送{0}出貨通知", directLine.Abbreviation));

                        mailTitle = string.Format("To IDS Peter and Cherry - 1 parcels-sent out via {0} under tracking {1}", method.Carriers.Name, trackingNumber);

                        if (directLine.Abbreviation.Equals("IDS (US)"))
                        {
                            receiveMails = new string[] { "anita.chou@contin-global.com", "jennifer.siew@contin-global.com", "twcs@contin-global.com" };

                            var link = "http://internal.qd.com.tw/DirectLine/BoxConfirmed?BoxID=" + boxID;
                            mailBody += string.Format("Please click the link {0} to confirm this bulk package.", string.Format("<a href='{0}' target='_bland'>{1}</a>", link, boxID));
                        }
                        else
                        {
                            receiveMails = new string[] { "gloria.chiu@contin-global.com", "cherry.chen@contin-global.com", "TWCS@contin-global.com", "contincs@gmail.com", "shipping_qd@hotmail.com" };

                            foreach (var box in boxList)
                            {
                                var labelArray = box.DirectLineLabel.Where(l => l.IsEnable).Select(l => l.LabelID).ToArray();
                                mailBody += string.Format("{0}<br /><br />the parcel was sent out via {1} under tracking {2}<br /><br />", string.Join("<br />", labelArray), method.Carriers.Name, box.TrackingNumber);
                            }
                        }

                        List<Tuple<Stream, string>> IDSFile2 = new List<Tuple<Stream, string>>();
                        using (var file = new ZipFile())
                        {
                            var memoryStream = new MemoryStream();
                            foreach (Packages package in packageList)
                            {
                                string Label_File = Path.Combine(basePath, package.FilePath, string.Format("Label-{0}.pdf", package.OrderID));
                                if (!System.IO.File.Exists(Label_File))
                                {
                                    System.IO.File.Copy(Path.Combine(basePath, package.FilePath, "Label.pdf"), Label_File);
                                }
                                file.AddFile(Label_File, "");
                            }
                            file.Save(memoryStream);
                            memoryStream.Seek(0, SeekOrigin.Begin);
                            IDSFile2.Add(new Tuple<Stream, string>(memoryStream, "Labels.zip"));
                        }

                        List<string> IDSFile1 = new List<string>();

                        if (boxList.Count() > 1)
                            IDSFile1.Add(Path.Combine(filePath, "PackageList.xlsx"));

                        if (directLine.Abbreviation.Equals("IDS (US)"))
                            IDSFile1.Add(Path.Combine(filePath, "DirectLine.xlsx"));

                        bool IDS_Status = MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, IDSFile1.ToArray(), IDSFile2, false);
                        if (IDS_Status)
                        {
                            MyHelp.Log("PickProduct", null, mailTitle);
                        }
                        else
                        {
                            MyHelp.Log("PickProduct", null, string.Format("{0} 寄送失敗", mailTitle));
                        }
                        break;
                    case "ECOF":
                        MyHelp.Log("PickProduct", null, "寄送ECOF出貨通知");

                        receiveMails = new string[] { "customerservice@ecof.com.au", "sophia.wang@ecof.com.au", "Ada.chen@ecof.com.au", "mandy.liang@ecof.com.au" };
                        mailTitle = string.Format("DISPATCHED: {0} {1}, {2}pcs", method.Carriers.Name, trackingNumber, packageList.Count());
                        foreach (Box box in boxList)
                        {
                            mailBody += string.Format("Tracking {0}({1}pcs, {2})<br />", box.TrackingNumber, box.Packages.Count(p => p.IsEnable.Value), box.BoxID);
                            mailBody += string.Join("<br />", box.Packages.Where(p => p.IsEnable.Value).Select(p => string.Format("{0}-{1}-{2}", p.Items.First().ProductID, p.OrderID.Value, p.TrackingNumber)).ToArray());
                            mailBody += "<br /><br />";
                        }

                        List<Tuple<Stream, string>> ECOF_File = new List<Tuple<Stream, string>>();
                        using (var file = new ZipFile())
                        {
                            var memoryStream = new MemoryStream();
                            foreach (Packages package in packageList)
                            {
                                string AWB_File = Path.Combine(basePath, package.FilePath, string.Format("{0}-{1}-{2}.pdf", package.Items.First(i => i.IsEnable.Value).ProductID, package.OrderID, package.TrackingNumber));
                                if (!System.IO.File.Exists(AWB_File))
                                {
                                    System.IO.File.Copy(Path.Combine(basePath, package.FilePath, "AirWaybill.pdf"), AWB_File);
                                }
                                file.AddFile(AWB_File, "");
                            }
                            file.Save(memoryStream);
                            memoryStream.Seek(0, SeekOrigin.Begin);
                            ECOF_File.Add(new Tuple<Stream, string>(memoryStream, "Labels.zip"));
                        }

                        bool ECOF_Status = MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, null, ECOF_File, false);
                        if (ECOF_Status)
                        {
                            MyHelp.Log("PickProduct", null, mailTitle);
                        }
                        else
                        {
                            MyHelp.Log("PickProduct", null, string.Format("{0} 寄送失敗", mailTitle));
                        }
                        break;
                }

                switch (method.Carriers.CarrierAPI.Type)
                {
                    case (byte)EnumData.CarrierType.DHL:
                        MyHelp.Log("PickProduct", null, "寄送DHL出口報單");

                        XLWorkbook DHL_workbook = new XLWorkbook();
                        JArray jObjects = new JArray();
                        List<string> DHLFile = new List<string>();

                        string OrderCurrencyCode = Enum.GetName(typeof(CurrencyCodeType), boxList[0].Packages.First().Orders.OrderCurrencyCode);
                        foreach (var group in itemsList.GroupBy(i => i.ProductID).ToList())
                        {
                            Skus sku = group.First().Skus;

                            JObject jo = new JObject();
                            jo.Add("1", !sku.ProductName.ToLower().Contains("htc") ? "G3" : "G5");
                            jo.Add("2", !sku.ProductName.ToLower().Contains("htc") ? "81" : "02");
                            jo.Add("3", sku.PurchaseInvoice);
                            jo.Add("4", directLine.ContactName);
                            jo.Add("5", string.Join(" - ", new string[] { sku.ProductType.ProductTypeName, sku.ProductName }));
                            jo.Add("6", trackingNumber);
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

                        filePath = Path.Combine(basePath, "mail", create_at.ToString("yyyy/MM/dd"));
                        if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);

                        string fileName = string.Format("{0} 出口報關表格.xlsx", boxID);
                        DHL_workbook.SaveAs(Path.Combine(filePath, fileName));

                        receiveMails = new string[] { "twtxwisa@dhl.com" };
                        mailTitle = string.Format("至優網 正式出口報關資料");

                        mailBody = trackingNumber;

                        DHLFile.Add(Path.Combine(filePath, fileName));
                        bool DHL_Status = MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, DHLFile.ToArray(), null, false);

                        if (DHL_Status)
                        {
                            MyHelp.Log("PickProduct", null, mailTitle);
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

                        filePath = Path.Combine(basePath, "export", "Box", create_at.ToString("yyyy/MM/dd"), boxID);

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
                        FedExFile.Add(new Tuple<Stream, string>(memoryStream, trackingNumber + ".zip"));

                        receiveMails = new string[] { "edd@fedex.com" };
                        mailTitle = string.Format("至優網 正式出口報關資料");

                        bool FedEx_Status = MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, "", true, null, FedExFile, false);

                        if (FedEx_Status)
                        {
                            MyHelp.Log("PickProduct", null, mailTitle);
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

        public void BoxConfirmed(string BoxID)
        {
            var boxList = db.Box.Where(b => b.IsEnable && b.MainBox.Equals(BoxID)).OrderBy(b => b.BoxID).ToList();
            if (boxList.Any())
            {
                try
                {
                    SC_WebService SCWS = new SC_WebService("tim@weypro.com", "timfromweypro");
                    TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;

                    if (!SCWS.Is_login) throw new Exception("SC is not login");

                    MyHelp.Log("Box", BoxID, BoxID + "Confirm!");

                    foreach (var package in boxList.SelectMany(b => b.Packages.Where(p => p.IsEnable.Value)))
                    {
                        if (!string.IsNullOrEmpty(package.TrackingNumber))
                        {
                            ThreadTask SyncTask = new ThreadTask(string.Format("Direct Line 訂單【{0}】SC更新", package.OrderID));
                            SyncTask.AddWork(factory.StartNew(() =>
                            {
                                SyncTask.Start();
                                SyncProcess sync = new SyncProcess(Session);
                                return sync.Update_Tracking(package);
                            }));

                            package.Label.Status = (byte)EnumData.LabelStatus.完成;
                        }
                    }

                    db.SaveChanges();
                    Response.Write("Success!");
                }
                catch (Exception e)
                {
                    MyHelp.Log("Box", BoxID, "Confirm error: " + e.Message);
                    Response.Write("Error!");
                }
            }
            else
            {
                Response.Write("Not find box " + BoxID);
            }
        }

        public class BoxFilter
        {
            public string BoxID { get; set; }
            public string SupplierBoxID { get; set; }
            public string LabelID { get; set; }
            public DateTime CreateDate { get; set; }
            public Nullable<int> WarehouseFrom { get; set; }
            public Nullable<int> WarehouseTo { get; set; }
            public string WITID { get; set; }
            public string Tracking { get; set; }
            public Nullable<byte> Status { get; set; }
            public Nullable<byte> Type { get; set; }
            public string Notes { get; set; }
        }

        public class CancelFilter
        {
            private string OrderIDField { get; set; }
            private string LabelIDField { get; set; }
            private string NewOrderIDField { get; set; }
            private string NewLabelIDField { get; set; }
            private string SkuField { get; set; }
            private string ProductNameField { get; set; }
            private string SerialNumberField { get; set; }
            private bool? DispatchField { get; set; }

            public bool? IsUsed { get; set; }
            public string OrderID { get { return this.OrderIDField; } set { this.OrderIDField = !string.IsNullOrEmpty(value) ? value.Trim() : value; } }
            public string LabelID { get { return this.LabelIDField; } set { this.LabelIDField = !string.IsNullOrEmpty(value) ? value.Trim() : value; } }
            public string NewOrderID { get { return this.NewOrderIDField; } set { this.NewOrderIDField = !string.IsNullOrEmpty(value) ? value.Trim() : value; } }
            public string NewLabelID { get { return this.NewLabelIDField; } set { this.NewLabelIDField = !string.IsNullOrEmpty(value) ? value.Trim() : value; } }
            public int? RMAID { get; set; }
            public string Sku { get { return this.SkuField; } set { this.SkuField = !string.IsNullOrEmpty(value) ? value.Trim() : value; } }
            public string ProductName { get { return this.ProductNameField; } set { this.ProductNameField = !string.IsNullOrEmpty(value) ? value.Trim() : value; } }
            public string SerialNumber { get { return this.SerialNumberField; } set { this.SerialNumberField = !string.IsNullOrEmpty(value) ? value.Trim() : value; } }
            public int? WarehouseID { get; set; }
            public DateTime? CreateDate { get; set; }
            public bool? Dispatch
            {
                get { return this.DispatchField; }
                set
                {
                    if (value != null)
                    {
                        this.DispatchField = bool.Parse(value.ToString());
                    }
                }
            }

            public string Sort { get; set; }
            public string Order { get; set; }
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