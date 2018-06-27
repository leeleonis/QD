using Ionic.Zip;
using Newtonsoft.Json;
using QDLogistics.Commons;
using QDLogistics.Filters;
using QDLogistics.Models;
using QDLogistics.Models.Object;
using QDLogistics.Models.Repositiry;
using QDLogistics.OrderService;
using SellerCloud_WebService;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;

namespace QDLogistics.Controllers
{
    public class DropShipController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<Orders> Orders;
        private IRepository<Packages> Packages;
        private IRepository<Items> Items;
        private IRepository<Skus> Skus;
        private IRepository<SerialNumbers> SerialNumbers;
        private IRepository<Payments> Payments;
        private IRepository<Addresses> Addresses;
        private IRepository<Warehouses> Warehouses;
        private IRepository<ShippingMethod> ShippingMethod;
        private IRepository<Carriers> Carriers;

        public DropShipController()
        {
            db = new QDLogisticsEntities();
        }

        [CheckSession]
        public ActionResult Wait()
        {
            return View();
        }

        [CheckSession]
        public ActionResult DirectLine()
        {
            return View();
        }

        [CheckSession]
        public ActionResult Shipped()
        {
            return View();
        }

        public ActionResult AjaxCarrierOption()
        {
            Warehouses = new GenericRepository<Warehouses>(db);
            ShippingMethod = new GenericRepository<ShippingMethod>(db);

            int warehouseID = 0;
            List<ShippingMethod> methodList = new List<ShippingMethod>();

            if (int.TryParse(Session["warehouseId"].ToString(), out warehouseID))
            {
                Warehouses warehouse = Warehouses.Get(warehouseID);
                if (warehouse != null && warehouse.WarehouseType.Equals((int)WarehouseTypeType.DropShip))
                {
                    if (!string.IsNullOrEmpty(warehouse.CarrierData))
                    {
                        Dictionary<int, bool> methodData = JsonConvert.DeserializeObject<Dictionary<int, bool>>(warehouse.CarrierData);
                        methodList = ShippingMethod.GetAll(true).Where(m => m.IsEnable && methodData.Keys.Contains(m.ID) && methodData[m.ID]).ToList();
                    }
                }
            }

            return Content(JsonConvert.SerializeObject(new { carrier = methodList.Select(c => new { text = string.Format("{0}-{1}", c.ID, c.Name), value = c.ID }) }));
        }

        public ActionResult AjaxWaitingData(DataFilter filter, int page = 1, int rows = 100)
        {
            int warehouseID = 0, total = 0;
            List<object> dataList = new List<object>();

            if (int.TryParse(Session["warehouseId"].ToString(), out warehouseID))
            {
                Warehouses = new GenericRepository<Warehouses>(db);
                Warehouses warehouse = Warehouses.Get(warehouseID);
                if (warehouse != null && warehouse.WarehouseType.Equals((int)WarehouseTypeType.DropShip))
                {
                    /** Order Filter **/
                    var OrderFilter = db.Orders.AsNoTracking().Where(o => !o.StatusCode.Value.Equals((int)OrderStatusCode.Completed) && o.PaymentStatus.Value.Equals((int)OrderPaymentStatus2.Charged));
                    /** Shipping Method Filter **/
                    var MethodFilter = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && !m.IsDirectLine);
                    List<OrderJoinData> results = DataFilter(filter, OrderFilter, MethodFilter, EnumData.ProcessStatus.待出貨, warehouseID);
                    if (results.Any())
                    {
                        TimeZoneConvert timeZoneConvert = new TimeZoneConvert();
                        EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);

                        int length = rows;
                        int start = (page - 1) * length;
                        total = results.Count();
                        results = results.OrderByDescending(oData => oData.order.TimeOfOrder).Skip(start).Take(length).ToList();

                        string[] skus = results.SelectMany(oData => oData.items.Select(i => i.ProductID)).Distinct().ToArray();
                        Dictionary<string, string> skuNameList = db.Skus.AsNoTracking().Where(s => s.IsEnable.Value && skus.Contains(s.Sku)).ToDictionary(s => s.Sku, s => s.ProductName);

                        int[] itemIDs = results.SelectMany(oData => oData.items.Select(i => i.ID)).ToArray();
                        Dictionary<int, string[]> serialOfItem = db.SerialNumbers.AsNoTracking().Where(s => itemIDs.Contains(s.OrderItemID)).GroupBy(s => s.OrderItemID).ToDictionary(s => s.Key, s => s.Select(ss => ss.SerialNumber).ToArray());

                        dataList.AddRange(results.Select(oData => new
                        {
                            OrderID = oData.order.OrderID,
                            POId = oData.package.POId,
                            PackageID = oData.package.ID,
                            ItemID = oData.item.ID,
                            PaymentDate = oData.payment != null ? timeZoneConvert.InitDateTime(oData.payment.AuditDate.Value, EnumData.TimeZone.EST).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy<br />hh:mm tt") : "",
                            Sku = oData.itemCount == 1 ? oData.item.ProductID : "Multi",
                            DisplayName = oData.itemCount == 1 ? skuNameList[oData.item.ProductID] : "Multi",
                            ItemCount = oData.itemCount,
                            OrderQtyTotal = oData.items.Sum(i => i.Qty),
                            ShippingCountry = oData.address.CountryName,
                            ShippingMethod = oData.package.ShippingMethod.Value,
                            StatusCode = Enum.GetName(typeof(OrderStatusCode), oData.order.StatusCode.Value),
                            Comment = oData.package.Comment,
                            SupplierComment = string.IsNullOrEmpty(oData.package.SupplierComment) ? "" : oData.package.SupplierComment,
                            Serials = oData.items.Where(i => serialOfItem.ContainsKey(i.ID)).ToDictionary(i => i.ID, i => serialOfItem[i.ID]),
                            SerialNumber = oData.itemCount + oData.item.Qty == 2 ? (serialOfItem.ContainsKey(oData.item.ID) ? serialOfItem[oData.item.ID].First() : "") : "Multi",
                            TrackingNumber = string.IsNullOrEmpty(oData.package.TrackingNumber) ? "" : oData.package.TrackingNumber,
                            POInvoice = string.IsNullOrEmpty(oData.package.POInvoice) ? "" : oData.package.POInvoice
                        }));
                    }
                }
            }

            return Content(JsonConvert.SerializeObject(new { total, rows = dataList }), "appllication/json");
        }

        public ActionResult AjaxDirectLineData(DataFilter filter, int page = 1, int rows = 100)
        {
            int warehouseID = 0, total = 0;
            List<object> dataList = new List<object>();

            if (int.TryParse(Session["warehouseId"].ToString(), out warehouseID))
            {
                Warehouses = new GenericRepository<Warehouses>(db);
                Warehouses warehouse = Warehouses.Get(warehouseID);
                if (warehouse != null && warehouse.WarehouseType.Value.Equals((int)WarehouseTypeType.DropShip))
                {
                    /** Order Filter **/
                    var OrderFilter = db.Orders.AsNoTracking().Where(o => !o.StatusCode.Value.Equals((int)OrderStatusCode.Completed) && o.PaymentStatus.Value.Equals((int)OrderPaymentStatus2.Charged));
                    /** Shipping Method Filter **/
                    var MethodFilter = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && m.IsDirectLine);
                    List<OrderJoinData> results = DataFilter(filter, OrderFilter, MethodFilter, EnumData.ProcessStatus.待出貨, warehouseID);
                    if (results.Any())
                    {
                        TimeZoneConvert timeZoneConvert = new TimeZoneConvert();
                        EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);

                        int length = rows;
                        int start = (page - 1) * length;
                        total = results.Count();
                        results = results.OrderByDescending(oData => oData.order.TimeOfOrder).Skip(start).Take(length).ToList();

                        string[] skus = results.SelectMany(oData => oData.items.Select(i => i.ProductID)).Distinct().ToArray();
                        Dictionary<string, string> skuNameList = db.Skus.AsNoTracking().Where(s => s.IsEnable.Value && skus.Contains(s.Sku)).ToDictionary(s => s.Sku, s => s.ProductName);

                        int[] methodIDs = results.Select(oData => oData.package.ShippingMethod.Value).Distinct().ToArray();
                        Dictionary<int, string> methodNameList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && methodIDs.Contains(m.ID)).ToDictionary(m => m.ID, m => m.ID + "-" + m.Name);

                        int[] itemIDs = results.SelectMany(oData => oData.items.Select(i => i.ID)).ToArray();
                        Dictionary<int, string[]> serialOfItem = db.SerialNumbers.AsNoTracking().Where(s => itemIDs.Contains(s.OrderItemID)).GroupBy(s => s.OrderItemID).ToDictionary(s => s.Key, s => s.Select(ss => ss.SerialNumber).ToArray());

                        dataList.AddRange(results.Select(oData => new
                        {
                            OrderID = oData.order.OrderID,
                            POId = oData.package.POId,
                            PackageID = oData.package.ID,
                            ItemID = oData.item.ID,
                            PaymentDate = oData.payment != null ? timeZoneConvert.InitDateTime(oData.payment.AuditDate.Value, EnumData.TimeZone.EST).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy<br />hh:mm tt") : "",
                            Sku = oData.itemCount == 1 ? oData.item.ProductID : "Multi",
                            DisplayName = oData.itemCount == 1 ? skuNameList[oData.item.ProductID] : "Multi",
                            ItemCount = oData.itemCount,
                            OrderQtyTotal = oData.items.Sum(i => i.Qty),
                            ShippingCountry = oData.address.CountryName,
                            ShippingMethod = methodNameList[oData.package.ShippingMethod.Value],
                            StatusCode = Enum.GetName(typeof(OrderStatusCode), oData.order.StatusCode.Value),
                            Comment = string.IsNullOrEmpty(oData.package.Comment) ? "" : oData.package.Comment,
                            SupplierComment = string.IsNullOrEmpty(oData.package.SupplierComment) ? "" : oData.package.SupplierComment,
                            Serials = oData.items.Where(i => serialOfItem.ContainsKey(i.ID)).ToDictionary(i => i.ID.ToString(), i => serialOfItem[i.ID]),
                            SerialNumber = oData.itemCount + oData.item.Qty == 2 ? (serialOfItem.ContainsKey(oData.item.ID) ? serialOfItem[oData.item.ID].First() : "") : "Multi",
                            TagNo = oData.package.TagNo,
                            POInvoice = string.IsNullOrEmpty(oData.package.POInvoice) ? "" : oData.package.POInvoice
                        }));
                    }
                }
            }

            return Json(new { total, rows = dataList }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult AjaxShippedData(DataFilter filter, int page = 1, int rows = 100)
        {
            Orders = new GenericRepository<Orders>(db);
            Packages = new GenericRepository<Packages>(db);
            Items = new GenericRepository<Items>(db);
            Payments = new GenericRepository<Payments>(db);
            Addresses = new GenericRepository<Addresses>(db);
            Warehouses = new GenericRepository<Warehouses>(db);

            int warehouseID = 0, total = 0;
            List<object> dataList = new List<object>();

            if (int.TryParse(Session["warehouseId"].ToString(), out warehouseID))
            {
                Warehouses warehouse = Warehouses.Get(warehouseID);
                if (warehouse != null && warehouse.WarehouseType.Equals((int)WarehouseTypeType.DropShip))
                {
                    /** Order Filter **/
                    var OrderFilter = db.Orders.AsNoTracking().Where(o => o.StatusCode.Value.Equals((int)OrderStatusCode.Completed) || o.ShippingStatus.Value.Equals((int)OrderShippingStatus.PartiallyShipped));
                    /** Shipping Method Filter **/
                    var MethodFilter = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable);
                    List<OrderJoinData> results = DataFilter(filter, OrderFilter, MethodFilter, EnumData.ProcessStatus.已出貨, warehouseID);
                    if (results.Any())
                    {
                        SerialNumbers = new GenericRepository<SerialNumbers>(db);

                        TimeZoneConvert timeZoneConvert = new TimeZoneConvert();
                        EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);

                        int length = rows;
                        int start = (page - 1) * length;
                        total = results.Count();
                        results = results.OrderByDescending(oData => oData.order.TimeOfOrder).Skip(start).Take(length).ToList();

                        int[] itemIDs = results.SelectMany(oData => oData.items.Select(i => i.ID)).ToArray();
                        Dictionary<int, string[]> serialOfItem = SerialNumbers.GetAll(true).Where(s => itemIDs.Contains(s.OrderItemID)).GroupBy(s => s.OrderItemID).ToDictionary(s => s.Key, s => s.Select(ss => ss.SerialNumber).ToArray());

                        dataList.AddRange(results.Select(oData => new
                        {
                            OrderID = oData.order.OrderID,
                            POId = oData.package.POId,
                            PackageID = oData.package.ID,
                            ItemID = oData.item.ID,
                            PaymentDate = oData.payment != null ? timeZoneConvert.InitDateTime(oData.payment.AuditDate.Value, EnumData.TimeZone.EST).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy<br />hh:mm tt") : "",
                            Sku = oData.itemCount == 1 ? oData.item.ProductID : "Multi",
                            DisplayName = oData.itemCount == 1 ? oData.item.DisplayName : "Multi",
                            ItemCount = oData.itemCount,
                            OrderQtyTotal = oData.items.Sum(i => i.Qty),
                            ShippingCountry = oData.address.CountryName,
                            ShippingMethod = oData.method.Name,
                            Type = oData.method.IsDirectLine ? "Direct Line" : "Dropship",
                            StatusCode = Enum.GetName(typeof(OrderStatusCode), oData.order.StatusCode.Value),
                            Comment = oData.package.Comment,
                            SupplierComment = oData.package.SupplierComment,
                            SerialNumber = oData.itemCount + oData.item.Qty == 2 ? (serialOfItem.ContainsKey(oData.item.ID) ? serialOfItem[oData.item.ID].First() : "") : "Multi",
                            LabelID = string.IsNullOrEmpty(oData.package.TagNo) ? "" : oData.package.TagNo,
                            TrackingNumber = oData.package.TrackingNumber,
                            POInvoice = oData.package.POInvoice
                        }));
                    }
                }
            }

            return Content(JsonConvert.SerializeObject(new { total, rows = dataList }), "appllication/json");
        }

        private List<OrderJoinData> DataFilter(DataFilter filter, IQueryable<Orders> OrderFilter, IQueryable<ShippingMethod> MethodFilter, EnumData.ProcessStatus processStatus, int warehouseID)
        {
            /** Order Filter **/
            if (!filter.StatusCode.Equals(null)) OrderFilter = OrderFilter.Where(o => o.StatusCode.Value.Equals(filter.StatusCode.Value));
            if (!filter.ShippingStatus.Equals(null)) OrderFilter = OrderFilter.Where(o => o.ShippingStatus.Equals(filter.ShippingStatus));
            if (!filter.Source.Equals(null)) OrderFilter = OrderFilter.Where(o => o.OrderSource.Equals(filter.Source));
            if (!string.IsNullOrWhiteSpace(filter.OrderID)) OrderFilter = OrderFilter.Where(o => o.OrderID.ToString().Equals(filter.OrderID));
            if (!string.IsNullOrWhiteSpace(filter.UserID)) OrderFilter = OrderFilter.Where(o => !string.IsNullOrWhiteSpace(o.eBayUserID) && o.eBayUserID.Contains(filter.UserID));
            if (!string.IsNullOrWhiteSpace(filter.SourceID)) OrderFilter = OrderFilter.Where(o => (o.OrderSource.Equals(1) && o.eBaySalesRecordNumber.Equals(filter.SourceID)) || (o.OrderSource.Equals(4) && o.OrderSourceOrderId.Equals(filter.SourceID)));

            /** Package Filter **/
            var PackageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)processStatus));
            if (!filter.MethodID.Equals(null)) PackageFilter = PackageFilter.Where(p => p.ShippingMethod.Value.Equals(filter.MethodID.Value));
            if (!string.IsNullOrWhiteSpace(filter.Tracking)) PackageFilter = PackageFilter.Where(p => p.TrackingNumber.ToLower().Contains(filter.Tracking.ToLower()));

            /** Item Filter **/
            var ItemFilter = db.Items.AsNoTracking().Where(i => i.IsEnable.Value && i.ShipFromWarehouseID.Value.Equals(warehouseID));
            if (!string.IsNullOrWhiteSpace(filter.ItemName)) ItemFilter = ItemFilter.Where(i => i.DisplayName.ToLower().Contains(filter.ItemName.ToLower()) || i.ProductID.Equals(filter.ItemName));

            /** Address Filter **/
            var AddressFilter = db.Addresses.AsNoTracking().Where(a => a.IsEnable.Value);
            if (!string.IsNullOrWhiteSpace(filter.Country)) AddressFilter = AddressFilter.Where(a => a.CountryCode.Equals(filter.Country));

            /** Payment Filter **/
            var PaymentFilter = db.Payments.AsNoTracking().Where(p => p.IsEnable.Value && p.PaymentType.Value.Equals((int)PaymentRecordType.Payment));
            if (!filter.DateFrom.Equals(new DateTime()))
            {
                DateTime dateFrom = new DateTime(filter.DateFrom.Year, filter.DateFrom.Month, filter.DateFrom.Day, 0, 0, 0);
                dateFrom = new TimeZoneConvert(dateFrom, MyHelp.GetTimeZone((int)Session["TimeZone"])).ConvertDateTime(EnumData.TimeZone.EST);
                PaymentFilter = PaymentFilter.Where(p => DateTime.Compare(p.AuditDate.Value, dateFrom) >= 0);
            }
            if (!filter.DateTo.Equals(new DateTime()))
            {
                DateTime dateTo = new DateTime(filter.DateTo.Year, filter.DateTo.Month, filter.DateTo.Day + 1, 0, 0, 0);
                dateTo = new TimeZoneConvert(dateTo, MyHelp.GetTimeZone((int)Session["TimeZone"])).ConvertDateTime(EnumData.TimeZone.EST);
                PaymentFilter = PaymentFilter.Where(p => DateTime.Compare(p.AuditDate.Value, dateTo) < 0);
            }

            return OrderFilter.ToList()
                .Join(PackageFilter, o => o.OrderID, p => p.OrderID, (o, p) => new OrderJoinData() { order = o, package = p })
                .Join(ItemFilter.GroupBy(i => i.PackageID.Value), oData => oData.package.ID, i => i.Key, (oData, i) => new OrderJoinData(oData) { item = i.First(), items = i.ToList(), itemCount = i.Sum(ii => ii.Qty + ii.KitItemCount).Value })
                .Join(AddressFilter, oData => oData.order.ShippingAddress, a => a.Id, (oData, a) => new OrderJoinData(oData) { address = a })
                .Join(MethodFilter, oData => oData.package.ShippingMethod, m => m.ID, (oData, m) => new OrderJoinData(oData) { method = m })
                .GroupJoin(PaymentFilter, oData => oData.order.OrderID, p => p.OrderID.Value, (oData, p) => new { orderJoinData = oData, payment = p.Take(1) })
                .SelectMany(o => o.payment.DefaultIfEmpty(), (o, p) => new OrderJoinData(o.orderJoinData) { payment = p }).ToList();
        }

        public ActionResult AjaxOrderUpdate(List<OrderUpdateData> data, int reTry = 0, List<string> message = null)
        {
            Packages = new GenericRepository<Packages>(db);
            SerialNumbers = new GenericRepository<SerialNumbers>(db);

            if (message == null)
                message = new List<string>();

            foreach (OrderUpdateData oData in data)
            {
                Packages package = Packages.Get(oData.PackageID);
                if (package != null)
                {
                    bool needUpload = false;

                    try
                    {
                        needUpload = string.IsNullOrEmpty(package.TrackingNumber) && !string.IsNullOrEmpty(oData.TrackingNumber);

                        package.ShippingMethod = oData.MethodID.Value;
                        package.TrackingNumber = oData.TrackingNumber;
                        package.SupplierComment = oData.SupplierComment;
                        package.POInvoice = oData.POInvoice;
                        if (needUpload) package.ProcessStatus = (int)EnumData.ProcessStatus.已出貨;
                        Packages.Update(package, package.ID);

                        List<SerialNumbers> serialList = SerialNumbers.GetAll(true).Where(s => s.OrderID.Equals(package.OrderID)).ToList();
                        if (oData.Serials != null)
                        {
                            foreach (var serials in oData.Serials)
                            {
                                int itemID = int.Parse(serials.Key);

                                foreach (string serial in serials.Value.Where(s => !serialList.Select(ss => ss.SerialNumber).Contains(s)))
                                {
                                    SerialNumbers.Create(new SerialNumbers()
                                    {
                                        OrderID = package.OrderID,
                                        OrderItemID = int.Parse(serials.Key),
                                        ProductID = package.Items.First(i => i.ID == itemID).ProductID,
                                        SerialNumber = serial,
                                        KitItemID = 0
                                    });
                                }

                                foreach (SerialNumbers serial in serialList.Where(s => !oData.Serials.SelectMany(ss => ss.Value).Contains(s.SerialNumber)))
                                {
                                    SerialNumbers.Delete(serial);
                                }
                            }
                        }

                        Packages.SaveChanges();
                        MyHelp.Log("Orders", oData.OrderID, string.Format("直發商訂單【{0}】更新完成", oData.OrderID));
                    }
                    catch (Exception e)
                    {
                        needUpload = false;
                        package.ProcessStatus = (int)EnumData.ProcessStatus.待出貨;
                        package.TrackingNumber = "";
                        Packages.Update(package, package.ID);
                        Packages.SaveChanges();

                        if (reTry <= 2)
                            return AjaxOrderUpdate(new List<OrderUpdateData>() { oData }, reTry + 1, message);
                        else
                        {
                            MyHelp.ErrorLog(e, string.Format("直發商訂單【{0}】更新失敗", oData.OrderID), oData.OrderID.ToString());
                            message.Add(string.Format("直發商訂單【{0}】更新失敗，錯誤：", oData.OrderID) + (e.InnerException != null ? e.InnerException.Message.Trim() : e.Message.Trim()));
                        }
                    }

                    if (needUpload)
                    {
                        try
                        {
                            UpdatePurchaseOrder(package);
                        }
                        catch (Exception e)
                        {
                            package.ProcessStatus = (int)EnumData.ProcessStatus.待出貨;
                            package.TrackingNumber = "";
                            Packages.Update(package, package.ID);
                            Packages.SaveChanges();

                            MyHelp.ErrorLog(e, string.Format("直發商訂單【{0}】以及PO【{1}】更新至SC失敗", package.OrderID, package.POId), package.OrderID.ToString());
                            message.Add(string.Format("直發商訂單【{0}】以及PO【{1}】更新至SC失敗，錯誤：", package.OrderID, package.POId) + (e.InnerException != null ? e.InnerException.Message.Trim() : e.Message.Trim()));
                        }
                    }
                }
            }

            return Content(JsonConvert.SerializeObject(new { status = !message.Any(), message = string.Join("\n", message) }), "appllication/json");
        }

        public ActionResult AjaxDirectLineUpdate(List<OrderUpdateData> data, int reTry = 0)
        {
            AjaxResult result = new AjaxResult();

            Packages = new GenericRepository<Packages>(db);
            SerialNumbers = new GenericRepository<SerialNumbers>(db);

            try
            {
                if (data == null || !data.Any()) throw new Exception("沒有資料!");

                foreach (OrderUpdateData oData in data)
                {
                    MyHelp.Log("Orders", oData.OrderID, string.Format("直發商訂單【{0}】開始更新", oData.OrderID));

                    Packages package = Packages.Get(oData.PackageID);

                    package.POInvoice = oData.POInvoice;
                    List<SerialNumbers> serialList = db.SerialNumbers.AsNoTracking().Where(s => s.OrderID.Value.Equals(package.OrderID.Value)).ToList();
                    if (oData.Serials != null)
                    {
                        foreach (var serials in oData.Serials)
                        {
                            int itemID = int.Parse(serials.Key);

                            foreach (string serial in serials.Value.Where(s => !serialList.Select(ss => ss.SerialNumber).Contains(s)))
                            {
                                SerialNumbers.Create(new SerialNumbers()
                                {
                                    OrderID = package.OrderID,
                                    OrderItemID = int.Parse(serials.Key),
                                    ProductID = package.Items.First(i => i.ID.Equals(itemID)).ProductID,
                                    SerialNumber = serial,
                                    KitItemID = 0
                                });
                            }

                            foreach (SerialNumbers serial in serialList.Where(s => !oData.Serials.SelectMany(ss => ss.Value).Contains(s.SerialNumber)))
                            {
                                SerialNumbers.Delete(serial);
                            }
                        }
                    }

                    Packages.Update(package, package.ID);
                    Packages.SaveChanges();
                    MyHelp.Log("Orders", oData.OrderID, string.Format("直發商訂單【{0}】更新完成", oData.OrderID));
                }
            }
            catch (Exception e)
            {
                result.status = false;
                result.message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        private void UpdatePurchaseOrder(Packages package, int reTry = 0)
        {
            try
            {
                TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
                ThreadTask threadTask = new ThreadTask(string.Format("直發商待出貨區 - 更新訂單【{0}】以及PO【{1}】資料至SC", package.OrderID, package.POId));

                lock (factory)
                {
                    threadTask.AddWork(factory.StartNew(session =>
                    {
                        threadTask.Start();

                        string error = "";

                        try
                        {
                            HttpSessionStateBase Session = (HttpSessionStateBase)session;
                            SyncProcess Sync = new SyncProcess(Session);
                            error = Sync.Update_PurchaseOrder(package.ID);
                        }
                        catch (Exception e)
                        {
                            error = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                        }

                        return error;
                    }, HttpContext.Session));
                }
            }
            catch (Exception e)
            {
                if (reTry <= 2)
                    UpdatePurchaseOrder(package, reTry + 1);
                else
                    throw e;
            }
        }

        public ActionResult DownloadLabel(int[] packageIDs)
        {
            AjaxResult result = new AjaxResult();

            try
            {
                if (!packageIDs.Any()) throw new Exception("沒有給訂單!");

                string basePath = HostingEnvironment.MapPath("~/FileUploads");
                List<Packages> packageList = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && packageIDs.Contains(p.ID)).ToList();

                if (!Directory.Exists(Path.Combine(basePath, "download"))) Directory.CreateDirectory(Path.Combine(basePath, "download"));
                using (var file = new ZipFile())
                {
                    foreach (Packages package in packageList)
                    {
                        string labelFile = Path.Combine(basePath, package.FilePath, package.TagNo + ".pdf");
                        if (!System.IO.File.Exists(labelFile))
                        {
                            System.IO.File.Copy(Path.Combine(basePath, package.FilePath, "Label.pdf"), labelFile);
                        }

                        file.AddFile(labelFile, "");
                    }

                    file.Save(Path.Combine(basePath, "download", "Labels.zip"));
                }

                string baseUrl = Request.Url.Scheme + "://" + Request.Url.Authority + Request.ApplicationPath.TrimEnd('/') + "/fileUploads";
                result.data = Path.Combine(baseUrl, "download", "Labels.zip");
            }
            catch (Exception e)
            {
                result.status = false;
                result.message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Dispatch(int[] packageIDs)
        {
            AjaxResult result = new AjaxResult();

            try
            {
                if (!packageIDs.Any()) throw new Exception("沒有給訂單!");

                int warehouseID = 0;
                if (!int.TryParse(Session["warehouseId"].ToString(), out warehouseID)) throw new Exception("找不到出貨倉!");

                SC_WebService SCWS = new SC_WebService(Session["ApiUserName"].ToString(), Session["ApiPassword"].ToString());

                if (!SCWS.Is_login) throw new Exception("SC is not login");

                Packages  = new GenericRepository<Packages>(db);
                IRepository<Box> Box = new GenericRepository<Box>(db);
                IRepository<DirectLineLabel> Label = new GenericRepository<DirectLineLabel>(db);

                TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
                ThreadTask threadTask = new ThreadTask("Dropship DL 訂單 Dispatch");

                lock (factory)
                {
                    threadTask.AddWork(factory.StartNew(Session =>
                    {
                        threadTask.Start();

                        string message = "";

                        HttpSessionStateBase session = (HttpSessionStateBase)Session;

                        try
                        {
                            List<Packages> packageList = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && packageIDs.Contains(p.ID)).ToList();
                            int[] methodIDs = packageList.Select(p => p.ShippingMethod.Value).Distinct().ToArray();
                            List<ShippingMethod> methodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && m.IsDirectLine && methodIDs.Contains(m.ID)).ToList();
                            List<DirectLine> directLineList = db.DirectLine.AsNoTracking().Where(d => d.IsEnable).ToList();

                            TimeZoneConvert timeZoneConvert = new TimeZoneConvert();

                            List<Packages> dispatchList;
                            List<string> errorList = new List<string>();
                            var groupList = methodList.GroupBy(m => m.DirectLine).ToList();
                            foreach (var group in groupList)
                            {
                                if (group.Join(packageList, m => m.ID, p => p.ShippingMethod, (m, p) => p).Any())
                                {
                                    DirectLine directLine = directLineList.First(d => d.ID.Equals(group.Key));
                                    string boxID = string.Format("{0}-{1}", directLine.Abbreviation, timeZoneConvert.Utc.ToString("yyyyMMdd"));
                                    int count = Box.GetAll(true).Count(b => b.IsEnable && b.DirectLine.Equals(directLine.ID) && b.BoxID.Contains(boxID)) + 1;
                                    byte[] Byte = BitConverter.GetBytes(count);
                                    Byte[0] += 64;
                                    boxID = string.Format("{0}-{1}", boxID, System.Text.Encoding.ASCII.GetString(Byte.Take(1).ToArray()));

                                    dispatchList = new List<Packages>();
                                    foreach (Packages package in group.Join(packageList, m => m.ID, p => p.ShippingMethod, (m, p) => p))
                                    {
                                        DirectLineLabel label = package.Label;
                                        OrderData order = SCWS.Get_OrderData(package.OrderID.Value);
                                        if (CheckOrderStatus(package, order.Order))
                                        {
                                            ThreadTask uploadPOTask = new ThreadTask(string.Format("直發商待出貨區 - 更新訂單【{0}】以及PO【{1}】資料至SC", package.OrderID, package.POId), session);

                                            lock (factory)
                                            {
                                                uploadPOTask.AddWork(factory.StartNew(() =>
                                                {
                                                    uploadPOTask.Start();

                                                    string error = "";

                                                    try
                                                    {
                                                        SyncProcess Sync = new SyncProcess(session, factory);
                                                        error = Sync.Update_PurchaseOrder(package.ID, false);

                                                        foreach (Items item in package.Items.Where(i => i.IsEnable.Value).ToList())
                                                        {
                                                            if (item.SerialNumbers.Any()) SCWS.Update_ItemSerialNumber(item.ID, item.SerialNumbers.Select(s => s.SerialNumber).ToArray());
                                                        }
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        error = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                                                    }

                                                    return error;
                                                }));
                                            }

                                            package.ProcessStatus = (int)EnumData.ProcessStatus.已出貨;
                                            package.BoxID = label.BoxID = boxID;
                                            label.Status = (byte)EnumData.LabelStatus.正常;
                                            dispatchList.Add(package);
                                        }
                                        else
                                        {
                                            MyHelp.Log("DirectLineLabel", label.LabelID, string.Format("標籤【{0}】狀態異常", label.LabelID), session);

                                            package.Orders.StatusCode = (int)order.Order.StatusCode;
                                            package.Orders.PaymentStatus = (int)order.Order.PaymentStatus;
                                            label.Status = (byte)EnumData.LabelStatus.鎖定中;

                                            if (order.Order.StatusCode.Equals((int)OrderStatusCode.Canceled))
                                            {
                                                label.Status = (byte)EnumData.LabelStatus.作廢;

                                                SerialNumbers = new GenericRepository<SerialNumbers>(db);
                                                foreach (var ss in SerialNumbers.GetAll().Where(s => s.OrderID.Equals(package.OrderID)))
                                                {
                                                    SerialNumbers.Delete(ss);
                                                };
                                            }

                                            errorList.Add(string.Format("標籤【{0}】狀態異常，請重新取出!", package.OrderID.Value));
                                        }
                                        Packages.Update(package, package.ID);
                                        Label.Update(label, label.LabelID);
                                    }

                                    if (dispatchList.Any())
                                    {
                                        Box box = new Box()
                                        {
                                            IsEnable = true,
                                            BoxID = boxID,
                                            DirectLine = directLine.ID,
                                            WarehouseFrom = warehouseID,
                                            ShippingStatus = (byte)EnumData.DirectLineStatus.已到貨,
                                            BoxType = (byte)EnumData.DirectLineBoxType.DirectLine,
                                            Create_at = timeZoneConvert.Utc
                                        };

                                        Box.Create(box);
                                        Box.SaveChanges();
                                        MyHelp.Log("Box", boxID, string.Format("Box【{0}】建立成功", boxID), session);

                                        MyHelp.Log("Box", box.BoxID, string.Format("寄送 Box【{0}】DL資料", box.BoxID), session);
                                        SendMailToCarrier(box, db.DirectLine.AsNoTracking().First(d => d.ID.Equals(box.DirectLine)));

                                        MyHelp.Log("Box", box.BoxID, string.Format("Box【{0}】完成出貨", box.BoxID), session);
                                    }

                                    Packages.SaveChanges();
                                }
                            }

                            if (errorList.Any()) message = string.Join("\n", errorList.ToArray());
                        }
                        catch (Exception e)
                        {
                            message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                        }                       

                        return message;
                    }, HttpContext.Session));
                }
            }
            catch (Exception e)
            {
                result.status = false;
                result.message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
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
                    case "DirectLine":
                        break;
                }
            }

            ViewBag.package = package;
            return PartialView(string.Format("List_{0}", Type));
        }

        public void SendMailToCarrier(Box box, DirectLine directLine)
        {
            List<Items> itemsList = box.Packages.Where(p => p.IsEnable.Value).SelectMany(p => p.Items.Where(i => i.IsEnable.Value)).ToList();
            if (itemsList.Any())
            {
                string sendMail = "dispatch-qd@hotmail.com";
                string mailTitle;
                string mailBody;
                string[] receiveMails;
                //string[] ccMails = new string[] { "peter@qd.com.tw", "kelly@qd.com.tw", "demi@qd.com.tw" };
                string[] ccMails = new string[] { "peter@qd.com.tw", "kelly@qd.com.tw", "demi@qd.com.tw" };

                switch (directLine.Abbreviation)
                {
                    case "IDS":
                        MyHelp.Log("PickProduct", null, "寄送IDS出貨通知");

                        receiveMails = new string[] { "gloria.chiu@contin-global.com", "cherry.chen@contin-global.com", "TWCS@contin-global.com", "contincs@gmail.com", "shipping_qd@hotmail.com" };
                        mailTitle = "To IDS Peter and Cherry - 1 parcels-sent out";
                        mailBody = string.Format("{0}<br /><br />Box 1 will send out", string.Join("<br />", box.DirectLineLabel.Where(l => l.IsEnable).Select(l => l.LabelID)));

                        bool IDS_Status = MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, null, null, false);
                        if (IDS_Status)
                        {
                            MyHelp.Log("PickProduct", null, mailTitle);
                        }
                        else
                        {
                            MyHelp.Log("PickProduct", null, string.Format("{0} 寄送失敗", mailTitle));
                        }
                        break;
                }
            }
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