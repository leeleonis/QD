using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Objects;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using Neodynamic.SDK.Web;
using Newtonsoft.Json;
using QDLogistics.Commons;
using QDLogistics.Filters;
using QDLogistics.Models;
using QDLogistics.Models.Object;
using QDLogistics.Models.Repositiry;
using QDLogistics.OrderService;

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
        private IRepository<Warehouses> Warehouses;
        private IRepository<ShippingMethod> Method;

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
            int warehouseId;
            List<ShippingMethod> MethodList = new List<ShippingMethod>();

            if (int.TryParse(Session["warehouseId"].ToString(), out warehouseId))
            {
                Warehouses = new GenericRepository<Warehouses>(db);

                Warehouses warehouse = Warehouses.Get(warehouseId);
                if (warehouse != null && !string.IsNullOrEmpty(warehouse.CarrierData))
                {
                    Dictionary<string, bool> MethodData = JsonConvert.DeserializeObject<Dictionary<string, bool>>(warehouse.CarrierData);
                    string[] MethodIDs = MethodData.Where(mData => mData.Value).Select(mData => mData.Key).ToArray();
                    MethodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && m.IsDirectLine && MethodIDs.Contains(m.ID.ToString())).ToList();
                }
            }

            //string packageSelect = string.Format("SELECT * FROM Packages WHERE IsEnable = 1 AND ProcessStatus = {0}", (byte)EnumData.ProcessStatus.待出貨);
            //string orderSelect = string.Format("SELECT * FROM Orders WHERE StatusCode = {0}", (int)OrderStatusCode.InProcess);
            //string itemSelect = string.Format("SELECT * FROM Items WHERE IsEnable = 1 AND ShipFromWarehouseID = {0}", warehouseId);

            //ObjectContext context = new ObjectContext("name=QDLogisticsEntities");
            //var ProductList = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨)).ToList()
            //    .Join(context.ExecuteStoreQuery<Orders>(orderSelect).ToList(), p => p.OrderID, o => o.OrderID, (package, order) => new { order, package })
            //    .OrderBy(data => data.order.TimeOfOrder).OrderByDescending(data => data.order.RushOrder)
            //    .Join(context.ExecuteStoreQuery<Items>(itemSelect).ToList(), op => op.package.ID, i => i.PackageID, (op, item) => op.package).Distinct().ToList();

            //ViewBag.packageList = ProductList;
            ViewBag.methodList = MethodList;
            ViewBag.WCPScript = WebClientPrint.CreateScript(Url.Action("ProcessRequest", "WebClientPrintAPI", null, HttpContext.Request.Url.Scheme), Url.Action("PrintFile", "File", null, HttpContext.Request.Url.Scheme), HttpContext.Session.SessionID);
            ViewData["warehouseId"] = (int)Session["WarehouseID"];
            ViewData["adminId"] = (int)Session["AdminId"];
            ViewData["adminName"] = Session["AdminName"].ToString();
            ViewData["total"] = 0; /*ProductList.Sum(p => p.Items.Where(i => i.IsEnable == true).Count());*/
            return View();
        }

        [CheckSession]
        public ActionResult Delivery()
        {
            return View();
        }

        public ActionResult BoxEdit(string id)
        {
            if (!MyHelp.CheckAuth("directLine", "delivery", EnumData.AuthType.Insert)) return RedirectToAction("index", "main");

            Box = new GenericRepository<Box>(db);

            Box box = Box.Get(id);

            if (box == null) return RedirectToAction("delivery", "directLine");
            
            //ViewBag.logList = db.ActionLog.AsNoTracking().Where(log => log.TableName.Equals("Box") && log.TargetID.Equals(box.BoxID)).OrderBy(log => log.CreateDate).ToList();
            ViewBag.logList = db.ActionLog.AsNoTracking().Where(log => log.TableName.Equals("Orders") && log.TargetID.Equals("5455634")).OrderBy(log => log.CreateDate).ToList();

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
            var ItemFilter = db.Items.AsNoTracking().Where(i => i.IsEnable.Value);
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
                .Join(MethodFilter, oData => oData.package.ShippingMethod, m => m.ID, (oData, m) => new OrderJoinData(oData) { method = m });

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
            if (!filter.Warehouse.Equals(null)) BoxFilter = BoxFilter.Where(b => b.WarehouseID.Equals(filter.Warehouse.Value));
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

                Warehouses = new GenericRepository<Warehouses>(db);

                TimeZoneConvert timeZoneConvert = new TimeZoneConvert();
                EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);

                Dictionary<int, string> warehouses = db.Warehouses.AsNoTracking().Where(w => w.IsSellable.Value).ToDictionary(w => w.ID, w => w.Name);

                dataList.AddRange(results.Select(data => new
                {
                    data.BoxID,
                    data.SupplierBoxID,
                    CreateDate = timeZoneConvert.InitDateTime(data.Create_at, EnumData.TimeZone.UTC).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy<br />hh:mm tt"),
                    WharehouseTO = "",
                    BoxQty = 0,
                    TotalWeight = 0,
                    data.WITID,
                    Tracking = data.TrackingNumber,
                    Status = Enum.GetName(typeof(EnumData.DirectLineStatus), data.ShippingStatus),
                    Type = EnumData.BoxTypeList()[(EnumData.DirectLineBoxType)data.BoxType],
                    data.Note
                }));
            }

            return Json(new { total, rows = dataList }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetBoxOrderData(string BoxID, int page = 1, int rows = 100)
        {
            int total = 0;
            List<object> dataList = new List<object>();

            var OrderFilter = db.Orders.AsNoTracking();
            var PackageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.BoxID.Equals(BoxID));
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

        public ActionResult GetOrderSerialData(int methodID)
        {
            AjaxResult result = new AjaxResult();

            int warehouseId = int.Parse(Session["warehouseId"].ToString());

            string orderSelect = string.Format("SELECT * FROM Orders WHERE StatusCode <> {0}", (int)OrderStatusCode.Completed);
            string itemSelect = string.Format("SELECT * FROM Items WHERE IsEnable = 1 AND ShipFromWarehouseID = {0}", warehouseId);
            string pickSelect = string.Format("SELECT * FROM PickProduct WHERE IsEnable = 1 AND IsPicked = 0 AND WarehouseID = {0}", warehouseId);

            var packageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨));
            if (!methodID.Equals(0)) packageFilter = packageFilter.Where(p => p.ShippingMethod.Value.Equals(methodID));

            ObjectContext context = new ObjectContext("name=QDLogisticsEntities");
            var ProductList = packageFilter.ToList()
                .Join(context.ExecuteStoreQuery<Items>(itemSelect).ToList(), p => p.ID, i => i.PackageID, (p, i) => p)
                .Join(context.ExecuteStoreQuery<Orders>(orderSelect).ToList(), p => p.OrderID, o => o.OrderID, (package, order) => new { order, package })
                .Join(context.ExecuteStoreQuery<PickProduct>(pickSelect).ToList(), op => op.package.ID, pk => pk.PackageID, (op, pick) => new { op.order, op.package, pick }).Distinct()
                .OrderBy(data => data.package.Qty).OrderBy(data => data.order.TimeOfOrder).OrderByDescending(data => data.order.RushOrder).ToList();

            string[] productIDs = ProductList.Select(p => p.pick.ProductID).Distinct().ToArray();
            //var productList = ProductList.Select(p => p.pick.SetTagNo(p.package.TagNo).SetNote(p.package.Comment))
            var productList = ProductList.Select(p => p.pick.SetTagNo("TagTest-" + p.order.OrderID).SetNote("NoteTest-" + p.order.OrderID))
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

            var fileList = ProductList.Select(p => p.package).Distinct().ToDictionary(p => p.ID.ToString(), p => getFileData(p));

            result.data = new { productList, groupList, serialList, fileList };

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        private object getFileData(Packages package)
        {
            string[] fileName = new string[2];
            string[] filePath = new string[2];
            int[] amount = new int[] { 0, 0 };

            string basePath = HostingEnvironment.MapPath("~/FileUploads");

            /***** 提貨單 *****/
            fileName[0] = "AirWaybill.pdf";
            filePath[0] = Path.Combine(basePath, string.Join("", package.FilePath.Skip(package.FilePath.IndexOf("export"))), fileName[0]);
            /***** 提貨單 *****/

            /***** 商業發票 *****/
            fileName[1] = "Invoice.xls";
            filePath[1] = Path.Combine(basePath, string.Join("", package.FilePath.Skip(package.FilePath.IndexOf("export"))), fileName[1]);
            /***** 商業發票 *****/

            switch (package.Method.Carriers.CarrierAPI.Type)
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

            // 取得熱感應印表機名稱
            string printerName = package.Method.PrinterName;

            return new { fileName, filePath, amount, printerName };
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