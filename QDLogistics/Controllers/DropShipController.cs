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
                    List<Orders> orderList = Orders.GetAll(true).Where(o => !o.StatusCode.Equals((int)OrderStatusCode.Completed) && o.PaymentStatus.Equals((int)OrderPaymentStatus2.Charged)).ToList();
                    List<OrderJoinData> results = orderSearch(orderList, EnumData.ProcessStatus.待出貨, warehouseID);
                    if (results.Any())
                    {
                        results = orderFilter(results, filter);
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
            }

            return Content(JsonConvert.SerializeObject(new { total, rows = dataList }), "appllication/json");
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
                    List<Orders> orderList = Orders.GetAll(true).Where(o => o.StatusCode.Equals((int)OrderStatusCode.Completed) || o.ShippingStatus.Equals((int)OrderShippingStatus.PartiallyShipped)).ToList();
                    List<OrderJoinData> results = orderSearch(orderList, EnumData.ProcessStatus.已出貨, warehouseID);
                    if (results.Any())
                    {
                        results = orderFilter(results, filter);
                        if (results.Any())
                        {
                            ShippingMethod = new GenericRepository<ShippingMethod>(db);
                            SerialNumbers = new GenericRepository<SerialNumbers>(db);

                            TimeZoneConvert timeZoneConvert = new TimeZoneConvert();
                            EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);

                            int length = rows;
                            int start = (page - 1) * length;
                            total = results.Count();
                            results = results.OrderByDescending(oData => oData.order.TimeOfOrder).Skip(start).Take(length).ToList();

                            int[] methodIDs = results.Where(oData => !oData.package.ShippingMethod.Equals(null)).Select(oData => oData.package.ShippingMethod.Value).ToArray();
                            Dictionary<int, string> methodList = ShippingMethod.GetAll(true).Where(m => m.IsEnable && methodIDs.Contains(m.ID)).ToDictionary(m => m.ID, m => m.Name);

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
                                ShippingMethod = oData.package.Method != null ? methodList[oData.package.ShippingMethod.Value] : "",
                                StatusCode = Enum.GetName(typeof(OrderStatusCode), oData.order.StatusCode.Value),
                                Comment = oData.package.Comment,
                                SupplierComment = oData.package.SupplierComment,
                                SerialNumber = oData.itemCount + oData.item.Qty == 2 ? (serialOfItem.ContainsKey(oData.item.ID) ? serialOfItem[oData.item.ID].First() : "") : "Multi",
                                TrackingNumber = oData.package.TrackingNumber,
                                POInvoice = oData.package.POInvoice
                            }));
                        }
                    }
                }
            }

            return Content(JsonConvert.SerializeObject(new { total, rows = dataList }), "appllication/json");
        }

        private List<OrderJoinData> orderSearch(List<Orders> orderList, EnumData.ProcessStatus processStatus, int warehouseID)
        {
            int[] orderIDs = orderList.Select(o => o.OrderID).ToArray();
            List<Packages> packageList = Packages.GetAll(true).Where(p => p.IsEnable.Equals(true) && orderIDs.Contains(p.OrderID.Value) && p.ProcessStatus.Equals((byte)processStatus)).ToList();
            var itemList = Items.GetAll(true).Where(i => i.IsEnable.Equals(true) && orderIDs.Contains(i.OrderID.Value) && i.ShipFromWarehouseID.Equals(warehouseID)).GroupBy(i => i.PackageID.Value).ToList();
            List<Payments> paymentList = Payments.GetAll(true).Where(p => p.IsEnable.Equals(true) && orderIDs.Contains(p.OrderID.Value)).ToList();

            return orderList.Join(packageList, o => o.OrderID, p => p.OrderID, (o, p) => new OrderJoinData() { order = o, package = p })
                .Join(itemList, oData => oData.package.ID, i => i.Key, (oData, i) => new OrderJoinData(oData) { item = i.First(), items = i.ToList(), itemCount = i.Sum(ii => 1 + ii.KitItemCount.Value) })
                .Join(Addresses.GetAll(true).Where(a => a.IsEnable.Equals(true)).ToList(), oData => oData.order.ShippingAddress, a => a.Id, (oData, a) => new OrderJoinData(oData) { address = a })
                .GroupJoin(paymentList, o => o.order.OrderID, p => p.OrderID, (o, p) => new { orderJoinData = o, payment = p.Take(1) })
                .SelectMany(o => o.payment.DefaultIfEmpty(), (o, p) => new OrderJoinData(o.orderJoinData) { payment = p }).ToList();
            //.Join(paymentList, oData => oData.order.OrderID, p => p.OrderID, (oData, p) => new OrderJoinData(oData) { payment = p }).ToList();
        }

        private List<OrderJoinData> orderFilter(List<OrderJoinData> results, DataFilter filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.OrderID)) results = results.Where(oData => oData.order.OrderID.ToString().Equals(filter.OrderID)).ToList();
            if (!string.IsNullOrWhiteSpace(filter.ItemName))
            {
                Skus = new GenericRepository<Skus>(db);
                string[] productIDs = results.Select(oData => oData.item.ProductID).Distinct().ToArray();
                Dictionary<string, string> skuList = Skus.GetAll(true).Where(s => productIDs.Contains(s.Sku)).ToDictionary(s => s.Sku, s => s.UPC);
                results = results.Where(oData => oData.items.Any(i => i.DisplayName.ToLower().Contains(filter.ItemName.ToLower()) || i.ProductID.Equals(filter.ItemName) || skuList[i.ProductID].Equals(filter.ItemName))).ToList();
            }
            if (!string.IsNullOrWhiteSpace(filter.UserID)) results = results.Where(oData => !string.IsNullOrWhiteSpace(oData.order.eBayUserID) && oData.order.eBayUserID.Contains(filter.UserID)).ToList();
            if (!string.IsNullOrWhiteSpace(filter.SourceID)) results = results.Where(oData => (oData.order.OrderSource.Equals(1) && oData.order.eBaySalesRecordNumber.Equals(filter.SourceID)) || (oData.order.OrderSource.Equals(4) && oData.order.OrderSourceOrderId.Equals(filter.SourceID))).ToList();

            if (!string.IsNullOrWhiteSpace(filter.Tracking)) results = results.Where(p => p.package.TrackingNumber.Equals(filter.Tracking)).ToList();
            if (!filter.MethodID.Equals(null)) results = results.Where(oData => oData.package.ShippingMethod.Equals(filter.MethodID)).ToList();
            if (!filter.StatusCode.Equals(null)) results = results.Where(oData => oData.order.StatusCode.Equals(filter.StatusCode)).ToList();
            if (!filter.ShippingStatus.Equals(null)) results = results.Where(oData => oData.order.ShippingStatus.Equals(filter.ShippingStatus)).ToList();
            if (!filter.Source.Equals(null)) results = results.Where(oData => oData.order.OrderSource.Equals(filter.Source)).ToList();
            if (!string.IsNullOrWhiteSpace(filter.Country)) results = results.Where(oData => filter.Country.Equals(oData.address.CountryCode)).ToList();

            if (!filter.DateFrom.Equals(new DateTime()))
            {
                DateTime dateFrom = new DateTime(filter.DateFrom.Year, filter.DateFrom.Month, filter.DateFrom.Day, 0, 0, 0);
                dateFrom = new TimeZoneConvert(dateFrom, MyHelp.GetTimeZone((int)Session["TimeZone"])).ConvertDateTime(EnumData.TimeZone.EST);
                results = results.Where(oData => oData.payment != null && DateTime.Compare(oData.payment.AuditDate.Value, dateFrom) >= 0).ToList();
            }
            if (!filter.DateTo.Equals(new DateTime()))
            {
                DateTime dateTo = new DateTime(filter.DateTo.Year, filter.DateTo.Month, filter.DateTo.Day + 1, 0, 0, 0);
                dateTo = new TimeZoneConvert(dateTo, MyHelp.GetTimeZone((int)Session["TimeZone"])).ConvertDateTime(EnumData.TimeZone.EST);
                results = results.Where(oData => oData.payment != null && DateTime.Compare(oData.payment.AuditDate.Value, dateTo) < 0).ToList();
            }

            return results;
        }
        
        public ActionResult AjaxOrderUpdate(List<OrderUpdateData> data, int reTry = 0, List<string> message = null)
        {
            Packages = new GenericRepository<Packages>(db);
            SerialNumbers = new GenericRepository<SerialNumbers>(db);

            if(message == null)
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
                        else {
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
    }
}