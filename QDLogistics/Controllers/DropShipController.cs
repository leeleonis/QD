using Newtonsoft.Json;
using QDLogistics.Commons;
using QDLogistics.Filters;
using QDLogistics.Models;
using QDLogistics.Models.Object;
using QDLogistics.Models.Repositiry;
using QDLogistics.OrderService;
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
                    var MethodFilter = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable);
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
                        SerialNumbers = new GenericRepository<SerialNumbers>(db);

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
                        Dictionary<int, string[]> serialOfItem = SerialNumbers.GetAll(true).Where(s => itemIDs.Contains(s.OrderItemID)).GroupBy(s => s.OrderItemID).ToDictionary(s => s.Key, s => s.Select(ss => ss.SerialNumber).ToArray());

                        dataList.AddRange(results.Select(oData => new
                        {
                            OrderID = oData.order.OrderID,
                            POId = oData.package.POId,
                            PackageID = oData.package.ID,
                            ItemID = oData.item.ID,
                            PaymentDate = timeZoneConvert.InitDateTime(oData.payment.AuditDate.Value, EnumData.TimeZone.EST).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy<br />hh:mm tt"),
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
                .Join(ItemFilter.GroupBy(i => i.PackageID.Value), oData => oData.package.ID, i => i.Key, (oData, i) => new OrderJoinData(oData) { item = i.First(), items = i.ToList(), itemCount = i.Sum(ii => 1 + ii.KitItemCount).Value })
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

                foreach(OrderUpdateData oData in data)
                {
                    MyHelp.Log("Orders", oData.OrderID, string.Format("直發商訂單【{0}】開始更新", oData.OrderID));

                    Packages package = Packages.Get(oData.PackageID);

                    package.POInvoice = oData.POInvoice;
                    List<SerialNumbers> serialList = db.SerialNumbers.AsNoTracking().Where(s => s.OrderID.Equals(package.OrderID)).ToList();
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
                    Packages.SaveChanges();
                    MyHelp.Log("Orders", oData.OrderID, string.Format("直發商訂單【{0}】更新完成", oData.OrderID));
                }
            }catch(Exception e)
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

            string basePath = HostingEnvironment.MapPath("~/FileUploads");
            List<Packages> packageList = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && packageIDs.Contains(p.ID)).ToList();

            return Json(result, JsonRequestBehavior.AllowGet);
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