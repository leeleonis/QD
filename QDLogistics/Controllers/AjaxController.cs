using CarrierApi.Sendle;
using CarrierApi.Winit;
using ClosedXML.Excel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QDLogistics.Commons;
using QDLogistics.Filters;
using QDLogistics.Models;
using QDLogistics.Models.Object;
using QDLogistics.Models.Repositiry;
using QDLogistics.OrderService;
using SellerCloud_WebService;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;

namespace QDLogistics.Controllers
{
    public class AjaxController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<Orders> Orders;
        private IRepository<Addresses> Addresses;
        private IRepository<Payments> Payments;
        private IRepository<Packages> Packages;
        private IRepository<Items> Items;
        private IRepository<Warehouses> Warehouses;
        private IRepository<Companies> Companies;
        private IRepository<ShippingMethod> ShippingMethod;
        private IRepository<Carriers> Carriers;
        private IRepository<Services> Services;
        private IRepository<CarrierAPI> CarrierAPI;
        private IRepository<Manufacturers> Manufacturers;
        private IRepository<Models.ProductType> ProductType;
        private IRepository<Skus> Skus;
        private IRepository<SerialNumbers> SerialNumbers;
        private IRepository<PickProduct> PickProduct;
        private IRepository<PurchaseItemReceive> PurchaseItemReceive;
        private IRepository<AdminGroups> AdminGroups;
        private IRepository<AdminUsers> AdminUsers;
        private IRepository<Models.TaskScheduler> TaskScheduler;

        public AjaxController()
        {
            db = new QDLogisticsEntities();
        }

        [CheckSession]
        public ActionResult GetWorkingTask()
        {
            AjaxResult result = new AjaxResult();
            AdminUsers = new GenericRepository<AdminUsers>(db);
            TaskScheduler = new GenericRepository<Models.TaskScheduler>(db);

            List<Models.TaskScheduler> schedulerList = db.TaskScheduler.AsNoTracking().Where(s => s.Status.Equals((byte)EnumData.TaskStatus.執行完)).OrderByDescending(s => s.CreateDate).Take(30).ToList();
            int[] adminIDs = schedulerList.Select(s => s.UpdateBy.Value).ToArray();
            var admins = AdminUsers.GetAll(true).Where(user => adminIDs.Contains(user.Id)).ToDictionary(user => user.Id, user => user.Name);
            admins = admins.Concat(new Dictionary<int, string> { { -1, "工作排程" }, { 0, "Weypro" } }).ToDictionary(x => x.Key, x => x.Value);

            result.data = new
            {
                list = MyHelp.RenderViewToString(ControllerContext, "_TaskList", null, new ViewDataDictionary() { { "schedulers", schedulerList }, { "admins", admins } }),
                date = new TimeZoneConvert().ConvertDateTime(MyHelp.GetTimeZone((int)Session["TimeZone"])).ToString("MM/dd/yyyy hh:mm tt")
            };

            return Content(JsonConvert.SerializeObject(result), "appllication/json");
        }

        [CheckSession]
        public ActionResult OrderData(DataFilter filter, int page = 1, int rows = 100)
        {
            int total = 0;
            List<object> dataList = new List<object>();

            /** Order Filter **/
            int[] hiddenList = { (int)OrderStatusCode.Void, (int)OrderStatusCode.Canceled };
            var OrderFilter = db.Orders.AsNoTracking().Where(o => !o.StatusCode.Value.Equals((int)OrderStatusCode.Completed));
            OrderFilter = OrderFilter.Where(o => filter.StatusCode.Equals(null) ? !hiddenList.Contains(o.StatusCode.Value) : o.StatusCode.Value.Equals(filter.StatusCode.Value));

            if (!string.IsNullOrWhiteSpace(filter.OrderID)) OrderFilter = OrderFilter.Where(o => o.OrderID.ToString().Equals(filter.OrderID));
            if (!string.IsNullOrWhiteSpace(filter.UserID)) OrderFilter = OrderFilter.Where(o => o.eBayUserID.Contains(filter.UserID));
            if (!string.IsNullOrWhiteSpace(filter.SourceID)) OrderFilter = OrderFilter.Where(o => (o.OrderSource.Value.Equals(1) && o.eBaySalesRecordNumber.Equals(filter.SourceID)) || (o.OrderSource.Value.Equals(4) && o.OrderSourceOrderId.Equals(filter.SourceID)));
            if (!filter.PaymentStatus.Equals(null)) OrderFilter = OrderFilter.Where(o => o.PaymentStatus.Value.Equals(filter.PaymentStatus.Value));
            if (!filter.Source.Equals(null)) OrderFilter = OrderFilter.Where(o => o.OrderSource.Value.Equals(filter.Source.Value));

            /** Package Filter **/
            var PackageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus == (byte)EnumData.ProcessStatus.訂單管理);
            if (!filter.MethodID.Equals(null)) PackageFilter = PackageFilter.Where(p => p.ShippingMethod.Value.Equals(filter.MethodID.Value));
            if (!filter.Export.Equals(null)) PackageFilter = PackageFilter.Where(p => p.Export.Value.Equals(filter.Export.Value));
            if (!filter.ExportMethod.Equals(null)) PackageFilter = PackageFilter.Where(p => p.ExportMethod.Equals(filter.ExportMethod.Value));

            /** Item Filter **/
            var ItemFilter = db.Items.AsNoTracking().Where(i => i.IsEnable.Value);
            if (!filter.WarehouseID.Equals(null)) ItemFilter = ItemFilter.Where(i => i.ShipFromWarehouseID.Value.Equals(filter.WarehouseID.Value));
            if (!string.IsNullOrWhiteSpace(filter.ItemName))
            {
                string[] ProductIDs = db.Skus.AsNoTracking().Where(s => s.UPC.Equals(filter.ItemName)).Select(s => s.Sku).ToArray();
                ItemFilter = ItemFilter.Where(i => i.DisplayName.ToLower().Contains(filter.ItemName.ToLower()) || i.ProductID.Equals(filter.ItemName) || ProductIDs.Contains(i.ProductID));
            }

            /** Address Filter **/
            var AddressFilter = db.Addresses.AsNoTracking().Where(a => a.IsEnable.Value);
            if (!string.IsNullOrWhiteSpace(filter.Country)) AddressFilter = AddressFilter.Where(a => a.CountryCode.Equals(filter.Country));

            /** Payment Filter **/
            var PaymentFilter = db.Payments.AsNoTracking().Where(p => p.IsEnable.Value);
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

            var results = OrderFilter.ToList()
                .Join(PackageFilter, o => o.OrderID, p => p.OrderID, (o, p) => new OrderJoinData() { order = o, package = p })
                .Join(ItemFilter.GroupBy(i => i.PackageID.Value), oData => oData.package.ID, i => i.Key, (oData, i) => new OrderJoinData(oData) { item = i.First(), items = i.ToList(), itemCount = i.Sum(ii => 1 + ii.KitItemCount).Value })
                .Join(AddressFilter, oData => oData.order.ShippingAddress, a => a.Id, (oData, a) => new OrderJoinData(oData) { address = a })
                .GroupJoin(PaymentFilter, oData => oData.order.OrderID, p => p.OrderID, (oData, p) => new { orderJoinData = oData, payment = p.Take(1) })
                .SelectMany(o => o.payment.DefaultIfEmpty(), (o, p) => new OrderJoinData(o.orderJoinData) { payment = p }).ToList();

            if (results.Any())
            {
                int length = rows;
                int start = (page - 1) * length;
                total = results.Count();
                results = results.OrderBy(data => data.order.TimeOfOrder).OrderByDescending(data => data.order.RushOrder).Skip(start).Take(length).ToList();

                TimeZoneConvert TimeZoneConvert = new TimeZoneConvert();
                EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);

                Dictionary<string, int?> carrierOfService = db.Services.AsNoTracking().ToDictionary(s => s.ServiceCode, s => s.ShippingMethod);
                carrierOfService.Add("Expedited", 9);
                List<string> uploadDefault = new List<string>() { "AU_StandardDelivery", "AU_Courier" };

                string[] productIDs = results.SelectMany(data => data.items.Select(i => i.ProductID)).ToArray();
                var methodOfSku = db.Skus.AsNoTracking().Where(s => productIDs.Contains(s.Sku)).ToDictionary(s => s.Sku, s => new Dictionary<string, byte?>() { { "export", s.Export }, { "exportMethod", s.ExportMethod } });

                dataList.AddRange(results.Select(data => new
                {
                    PackageID = data.package.ID,
                    OrderID = data.package.OrderID,
                    ParentOrderID = data.order.ParentOrderID,
                    OrderSourceOrderId = data.order.OrderSourceOrderId,
                    eBayUserID = data.order.eBayUserID,
                    Items = data.items.ToDictionary(i => i.ID.ToString(), i => new { ItemID = i.ID, i.DeclaredValue, i.DLDeclaredValue, i.Qty }),
                    ItemCount = data.itemCount,
                    ShipWarehouse = data.item.ShipFromWarehouseID,
                    PaymentDate = data.payment != null ? TimeZoneConvert.InitDateTime(data.payment.AuditDate.Value, EnumData.TimeZone.EST).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy<br />hh:mm tt") : "",
                    Sku = data.itemCount == 1 ? data.item.ProductID : "Multi",
                    DisplayName = data.itemCount == 1 ? data.item.DisplayName : "Multi",
                    //OrderQtyTotal = data.order.OrderQtyTotal,
                    OrderQtyTotal = data.items.Sum(i => i.Qty),
                    ShippingCountry = data.address.CountryName,
                    PostalCode = data.address.PostalCode,
                    //SubTotal = data.order.SubTotal.Value.ToString("N"),
                    SubTotal = data.items.Sum(i => i.Qty * i.UnitPrice).Value.ToString("N"),
                    DeclaredTotal = data.package.DeclaredTotal != 0 ? data.package.DeclaredTotal.ToString("N") : "",
                    DLDeclaredTotal = !data.package.DLDeclaredTotal.Equals(0) ? data.package.DLDeclaredTotal.ToString("N") : "",
                    OrderCurrencyCode = data.order.OrderCurrencyCode,
                    AvailableQty = 1,
                    ShippingServiceSelected = data.order.ShippingServiceSelected,
                    MethodID = !data.package.ShippingMethod.HasValue ? (carrierOfService.ContainsKey(data.order.ShippingServiceSelected) ? carrierOfService[data.order.ShippingServiceSelected] : 9) : data.package.ShippingMethod,
                    FirstMile = data.package.FirstMile,
                    Export = !data.package.Export.HasValue ? data.items.Min(i => methodOfSku.ContainsKey(i.ProductID) ? methodOfSku[i.ProductID]["export"] : 0) : data.package.Export,
                    ExportMethod = !data.package.ExportMethod.HasValue ? data.items.Min(i => methodOfSku.ContainsKey(i.ProductID) ? methodOfSku[i.ProductID]["exportMethod"] : 0) : data.package.ExportMethod,
                    StatusCode = data.order.StatusCode,
                    RushOrder = data.order.RushOrder,
                    UploadTracking = uploadDefault.Contains(data.order.ShippingServiceSelected) ? false : data.package.UploadTracking,
                    Instructions = data.order.Instructions,
                    Comment = string.IsNullOrEmpty(data.package.Comment) ? "" : data.package.Comment,
                    ProcessBack = data.package.ProcessBack
                }));
            }

            return Json(new { total = total, rows = dataList }, JsonRequestBehavior.AllowGet);
        }

        [CheckSession]
        [HttpPost]
        public ActionResult OrderUpdate(List<OrderUpdateData> data)
        {
            Packages = new GenericRepository<Packages>(db);
            Items = new GenericRepository<Items>(db);

            AjaxResult result = new AjaxResult();
            List<int> taskList = new List<int>();

            try
            {
                if (!data.Any()) throw new Exception("未取得任何資料!");

                foreach (OrderUpdateData oData in data)
                {
                    Packages package = Packages.Get(oData.PackageID);

                    if (package == null) throw new Exception(string.Format("找不到{0}資料!", oData.OrderID));

                    // update order data
                    if (!package.Orders.StatusCode.Equals(oData.StatusCode))
                    {
                        taskList.Add(SyncOrderStatus(package.Orders, oData.StatusCode.Value));
                    }

                    package.Orders.StatusCode = oData.StatusCode.Value;
                    package.Orders.OrderCurrencyCode = oData.OrderCurrencyCode.Value;
                    package.Orders.RushOrder = oData.RushOrder.Value;

                    // update package data
                    package.DeclaredTotal = oData.DeclaredTotal.Value;
                    package.DLDeclaredTotal = oData.DLDeclaredTotal.Value;
                    package.ShippingMethod = oData.MethodID.Value;
                    package.FirstMile = oData.FirstMile.Value;
                    package.Export = oData.Export.Value;
                    package.ExportMethod = oData.ExportMethod.Value;
                    package.UploadTracking = oData.UploadTracking.Value;
                    package.Comment = oData.Comment;
                    Packages.Update(package, oData.PackageID);

                    // update item data
                    foreach (Items item in package.Items.Where(i => i.IsEnable.Value))
                    {
                        item.ShipFromWarehouseID = oData.ShipWarehouse.Value;
                        item.DeclaredValue = oData.Items[item.ID.ToString()].DeclaredValue;
                        item.DLDeclaredValue = oData.Items[item.ID.ToString()].DLDeclaredValue;
                        Items.Update(item, item.ID);
                    }

                    Packages.SaveChanges();

                    MyHelp.Log("Orders", oData.OrderID, "更新訂單資料");
                }
            }
            catch (Exception e)
            {
                result.status = false;
                result.message = e.Message;
            }

            if (taskList.Any())
            {
                result.data = Url.Action("Scheduler", "Task", new { TaskIDs = string.Join("_", taskList.ToArray()) });
            }

            return Content(JsonConvert.SerializeObject(result), "appllication/json");
        }

        private int SyncOrderStatus(Orders order, int StatusCode)
        {
            TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
            ThreadTask threadTask = new ThreadTask(string.Format("訂單管理區 - 更新訂單【{0}】訂單狀態至SC", order.OrderID));

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

        [CheckSession]
        public ActionResult OrderSplit(int PackageID, int Amount)
        {
            Packages = new GenericRepository<Packages>(db);

            Packages package = Packages.Get(PackageID);

            if (package != null)
            {
                ViewBag.package = package;
                ViewBag.amount = Amount;
                return PartialView("_SplitDispatch");
            }

            return new EmptyResult();
        }

        [CheckSession]
        public ActionResult RecoveryOrder()
        {
            AjaxResult result = new AjaxResult();
            Packages = new GenericRepository<Packages>(db);

            List<Packages> packageList = Packages.GetAll().Where(p => p.IsEnable == true && p.ProcessStatus == (int)EnumData.ProcessStatus.鎖定中).ToList();
            if (packageList.Any())
            {
                foreach (Packages package in packageList)
                {
                    package.ProcessStatus = (int)EnumData.ProcessStatus.訂單管理;
                    Packages.Update(package);
                }

                Packages.SaveChanges();
                result.message = "訂單復原結束!";
            }
            else
            {
                result.status = false;
                result.message = "沒有需要復原的訂單!";
            }

            return Content(JsonConvert.SerializeObject(result), "appllication/json");
        }

        [CheckSession]
        public ActionResult OrderWaitingData(DataFilter filter, int page = 1, int rows = 100)
        {
            Orders = new GenericRepository<Orders>(db);
            Addresses = new GenericRepository<Addresses>(db);
            Payments = new GenericRepository<Payments>(db);
            Packages = new GenericRepository<Packages>(db);
            Items = new GenericRepository<Items>(db);

            int total = 0;
            List<object> dataList = new List<object>();

            /** Order Filter **/
            var OrderFilter = db.Orders.AsNoTracking().Where(o => !o.StatusCode.Value.Equals((int)OrderStatusCode.Completed) && o.PaymentStatus.Value.Equals((int)OrderPaymentStatus2.Charged));
            if (!string.IsNullOrWhiteSpace(filter.OrderID)) OrderFilter = OrderFilter.Where(o => o.OrderID.ToString().Equals(filter.OrderID));
            if (!string.IsNullOrWhiteSpace(filter.UserID)) OrderFilter = OrderFilter.Where(o => o.eBayUserID.Contains(filter.UserID));
            if (!string.IsNullOrWhiteSpace(filter.SourceID)) OrderFilter = OrderFilter.Where(o => (o.OrderSource.Value.Equals(1) && o.eBaySalesRecordNumber.Equals(filter.SourceID)) || (o.OrderSource.Value.Equals(4) && o.OrderSourceOrderId.Equals(filter.SourceID)));
            if (!filter.StatusCode.Equals(null)) OrderFilter = OrderFilter.Where(o => o.StatusCode.Value.Equals(filter.StatusCode.Value));
            if (!filter.Source.Equals(null)) OrderFilter = OrderFilter.Where(o => o.OrderSource.Value.Equals(filter.Source.Value));

            /** Package Filter **/
            var PackageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus == (byte)EnumData.ProcessStatus.待出貨);

            /** Item Filter **/
            var ItemFilter = db.Items.AsNoTracking().Where(i => i.IsEnable.Value);
            if (!filter.WarehouseID.Equals(null)) ItemFilter = ItemFilter.Where(i => i.ShipFromWarehouseID.Value.Equals(filter.WarehouseID.Value));
            if (!string.IsNullOrWhiteSpace(filter.ItemName))
            {
                string[] ProductIDs = db.Skus.AsNoTracking().Where(s => s.UPC.Equals(filter.ItemName)).Select(s => s.Sku).ToArray();
                ItemFilter = ItemFilter.Where(i => i.DisplayName.ToLower().Contains(filter.ItemName.ToLower()) || i.ProductID.Equals(filter.ItemName) || ProductIDs.Contains(i.ProductID));
            }

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

            var results = OrderFilter.ToList()
                .Join(PackageFilter, o => o.OrderID, p => p.OrderID, (o, p) => new OrderJoinData() { order = o, package = p })
                .Join(ItemFilter.GroupBy(i => i.PackageID.Value), oData => oData.package.ID, i => i.Key, (oData, i) => new OrderJoinData(oData) { item = i.First(), items = i.ToList(), itemCount = i.Sum(ii => 1 + ii.KitItemCount).Value })
                .Join(AddressFilter, oData => oData.order.ShippingAddress, a => a.Id, (oData, a) => new OrderJoinData(oData) { address = a })
                .GroupJoin(PaymentFilter, oData => oData.order.OrderID, p => p.OrderID, (oData, p) => new { orderJoinData = oData, payment = p.Take(1) })
                .SelectMany(o => o.payment.DefaultIfEmpty(), (o, p) => new OrderJoinData(o.orderJoinData) { payment = p }).ToList();

            if (results.Any())
            {
                int length = rows;
                int start = (page - 1) * length;
                total = results.Count();
                results = results.OrderByDescending(data => data.order.TimeOfOrder).Skip(start).Take(length).ToList();

                Warehouses = new GenericRepository<Warehouses>(db);
                ShippingMethod = new GenericRepository<ShippingMethod>(db);

                TimeZoneConvert timeZoneConvert = new TimeZoneConvert();
                EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);

                Dictionary<int, string> warehouses = db.Warehouses.AsNoTracking().Where(w => w.IsSellable.Value).ToDictionary(w => w.ID, w => w.Name);
                Dictionary<int, string> methodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable).ToDictionary(m => m.ID, m => m.Name);

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
                    OrderQtyTotal = data.items.Sum(i => i.Qty),
                    ShippingCountry = data.address.CountryName,
                    Warehouse = warehouses[data.item.ShipFromWarehouseID.Value],
                    ShippingMethod = data.package.ShippingMethod.HasValue ? methodList[data.package.ShippingMethod.Value] : data.package.ShippingServiceCode,
                    Export = Enum.GetName(typeof(EnumData.Export), data.package.Export != null ? data.package.Export : 0),
                    ExportMethod = Enum.GetName(typeof(EnumData.ExportMethod), data.package.ExportMethod != null ? data.package.ExportMethod : 0),
                    StatusCode = Enum.GetName(typeof(OrderStatusCode), data.order.StatusCode),
                    Comment = data.package.Comment,
                    Confirmed = data.order.IsConfirmed,
                    DispatchDate = timeZoneConvert.InitDateTime(data.package.ShipDate.Value, EnumData.TimeZone.UTC).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy<br />hh:mm tt"),
                    TrackingNumber = data.package.TrackingNumber
                }));
            }

            return Json(new { total = total, rows = dataList }, JsonRequestBehavior.AllowGet);
        }

        [CheckSession]
        [HttpPost]
        public ActionResult CancelWaiting(List<string> packageIDs)
        {
            AjaxResult result = new AjaxResult();
            Packages = new GenericRepository<Packages>(db);
            PickProduct = new GenericRepository<PickProduct>(db);
            SerialNumbers = new GenericRepository<SerialNumbers>(db);

            TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;

            List<Packages> packageList = Packages.GetAll().Where(p => p.IsEnable == true && packageIDs.Contains(p.ID.ToString())).ToList();

            foreach (Packages package in packageList.Where(p => p.ProcessStatus != (int)EnumData.ProcessStatus.已出貨))
            {
                ThreadTask threadTask = new ThreadTask(string.Format("待出貨區 - 取消訂單【{0}】至訂單管理區", package.OrderID));

                lock (factory)
                {
                    threadTask.AddWork(factory.StartNew(Session =>
                    {
                        threadTask.Start();

                        string message = "";

                        try
                        {
                            Carriers carrier = package.Method.Carriers;
                            if (carrier != null)
                            {
                                switch (carrier.CarrierAPI.Type.Value)
                                {
                                    case (byte)EnumData.CarrierType.Winit:
                                        if (!string.IsNullOrEmpty(package.WinitNo))
                                        {
                                            Winit_API winit = new Winit_API(carrier.CarrierAPI);
                                            Received received = winit.Void(package.WinitNo);
                                            if (received.code != "0") throw new Exception(received.code + "-" + received.msg);
                                            package.WinitNo = null;
                                        }
                                        break;
                                    case (byte)EnumData.CarrierType.Sendle:
                                        if (!string.IsNullOrEmpty(package.TagNo))
                                        {
                                            Sendle_API sendle = new Sendle_API(carrier.CarrierAPI);
                                            Sendle_API.CancelResponse cancel = sendle.Cancel(package.TagNo);
                                            if (string.IsNullOrEmpty(cancel.cancellation_message)) throw new Exception("取消訂單出貨失敗!");
                                        }
                                        break;
                                }
                            }

                            if (!string.IsNullOrEmpty(package.TagNo))
                            {
                                if (!string.IsNullOrEmpty(package.BoxID))
                                {
                                    CaseLog caseLog = new CaseLog((HttpSessionStateBase)Session);
                                    caseLog.OrderInit(package);
                                    caseLog.CreateSerialError();
                                }

                                package.Label.IsEnable = false;
                                package.TagNo = package.BoxID = null;
                            }

                            package.ProcessStatus = (int)EnumData.ProcessStatus.訂單管理;
                            package.TrackingNumber = "";
                            Packages.Update(package, package.ID);
                            Packages.SaveChanges();

                            foreach (PickProduct pick in PickProduct.GetAll(true).Where(pick => pick.PackageID == package.ID).ToList())
                            {
                                pick.IsEnable = false;
                                pick.IsPicked = false;
                                pick.IsMail = false;
                                pick.QtyPicked = 0;
                                PickProduct.Update(pick, pick.ID);
                            }
                            PickProduct.SaveChanges();

                            if (db.SerialNumbers.AsNoTracking().Any(s => s.OrderID.Value.Equals(package.OrderID.Value)))
                            {
                                using (Hubs.ServerHub server = new Hubs.ServerHub())
                                    server.BroadcastProductError(package.OrderID.Value, null, EnumData.OrderChangeStatus.產品異常);

                                var serialList = package.Items.Where(i => i.IsEnable.Value).SelectMany(i => i.SerialNumbers);

                                MyHelp.Log("Orders", package.OrderID, string.Format("訂單【{0}】移除 Serial Number - {1}", package.OrderID, string.Join("、", serialList.Select(s => s.SerialNumber))), (HttpSessionStateBase)Session);

                                foreach (SerialNumbers serial in serialList)
                                {
                                    SerialNumbers.Delete(serial);
                                }
                                SerialNumbers.SaveChanges();
                            }

                            using (Hubs.ServerHub server = new Hubs.ServerHub())
                                server.BroadcastOrderChange(package.OrderID.Value, EnumData.OrderChangeStatus.取消出貨);

                            MyHelp.Log("Orders", package.OrderID, "取消訂單出貨", (HttpSessionStateBase)Session);
                        }
                        catch (DbEntityValidationException ex)
                        {
                            var errorMessages = ex.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(x => x.ErrorMessage);
                            message = string.Join("; ", errorMessages);
                        }
                        catch (Exception e)
                        {
                            message = e.Message;
                        }

                        return message;
                    }, HttpContext.Session));
                }
            }

            if (packageList.Any(p => p.ProcessStatus == (int)EnumData.ProcessStatus.已出貨))
            {
                string[] message = packageList.Where(p => p.ProcessStatus == (int)EnumData.ProcessStatus.已出貨)
                    .Select(p => string.Format("訂單【{0}】已出貨，無法取消!", p.OrderID)).ToArray();

                result.status = false;
                result.message = string.Join("\n", message);
            }

            return Content(JsonConvert.SerializeObject(result), "appllication/json");
        }

        [CheckSession]
        public ActionResult GetSerialData(int methodID)
        {
            AjaxResult result = new AjaxResult();

            int warehouseId = int.Parse(Session["warehouseId"].ToString());

            string packageSelect = string.Format("SELECT * FROM Packages WHERE IsEnable = 1 AND ProcessStatus = {0}", (byte)EnumData.ProcessStatus.待出貨);
            string orderSelect = string.Format("SELECT * FROM Orders WHERE StatusCode = {0}", (int)OrderStatusCode.InProcess);
            string itemSelect = string.Format("SELECT * FROM Items WHERE IsEnable = 1 AND ShipFromWarehouseID = {0}", warehouseId);
            string pickSelect = string.Format("SELECT * FROM PickProduct WHERE IsEnable = 1 AND IsPicked = 0 AND WarehouseID = {0}", warehouseId);

            var packageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨));
            if (!methodID.Equals(0)) packageFilter = packageFilter.Where(p => p.ShippingMethod.Value.Equals(methodID));

            var methodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && !m.IsDirectLine).ToList();

            ObjectContext context = new ObjectContext("name=QDLogisticsEntities");
            var ProductList = packageFilter.ToList().Join(methodList, p => p.ShippingMethod, m => m.ID, (p, m) => p)
                .Join(context.ExecuteStoreQuery<Items>(itemSelect).ToList(), p => p.ID, i => i.PackageID, (p, i) => p)
                .Join(context.ExecuteStoreQuery<Orders>(orderSelect).ToList(), p => p.OrderID, o => o.OrderID, (package, order) => new { order, package })
                .Join(context.ExecuteStoreQuery<PickProduct>(pickSelect).ToList(), op => op.package.ID, pk => pk.PackageID, (op, pick) => new { op.order, op.package, pick }).Distinct()
                .OrderBy(data => data.package.Qty).OrderBy(data => data.order.TimeOfOrder).OrderByDescending(data => data.order.RushOrder).ToList();

            string[] productIDs = ProductList.Select(p => p.pick.ProductID).Distinct().ToArray();
            var productList = ProductList.Select(p => p.pick).GroupBy(p => p.ProductID).ToDictionary(group => group.Key.ToString(), group => group.ToDictionary(p => p.ItemID.ToString()));

            List<SerialNumbers> itemSerials = db.SerialNumbers.AsNoTracking().Where(s => productIDs.Contains(s.ProductID)).ToList();
            List<PurchaseItemReceive> purchaseItemSerial = db.PurchaseItemReceive.AsNoTracking().Where(s => productIDs.Contains(s.ProductID)).ToList();
            var serialList = productIDs.ToDictionary(p => p, p => new
            {
                isRequire = purchaseItemSerial.Any(s => s.ProductID.Equals(p)) ? purchaseItemSerial.Where(s => s.ProductID.Equals(p)).Max(sn => sn.IsRequireSerialScan) : false,
                serials = purchaseItemSerial.Where(sn => sn.ProductID.Equals(p)).Select(sn => sn.SerialNumber.Trim()).ToArray(),
                used = itemSerials.Where(i => i.ProductID.Equals(p)).Select(i => i.SerialNumber.Trim()).ToArray()
            });

            var groupList = ProductList.Select(p => p.pick).GroupBy(p => p.PackageID).GroupBy(p => p.First().OrderID)
                .ToDictionary(o => o.Key.ToString(), o => o.ToDictionary(p => p.Key.ToString(), p => p.ToDictionary(pp => pp.ItemID.ToString(),
                pp => new { data = pp, serial = itemSerials.Where(sn => sn.OrderItemID == pp.ItemID).Select(sn => sn.SerialNumber.Trim()).ToArray() })))
                .GroupBy(o => o.Value.Sum(p => p.Value.Sum(pp => pp.Value.data.Qty)) > 1).ToDictionary(g => g.Key ? "Multiple" : "Single", g => g.ToDictionary(o => o.Key.ToString(), o => o.Value));

            var fileList = ProductList.Select(p => p.package).Distinct().ToDictionary(p => p.ID.ToString(), p => getFileData(p));

            result.data = new { productList, groupList, serialList, fileList };

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [CheckSession]
        public ActionResult PickOrder(int orderID, int warehouseId)
        {
            AjaxResult result = new AjaxResult();
            Orders = new GenericRepository<Orders>(db);
            Items = new GenericRepository<Items>(db);
            PickProduct = new GenericRepository<PickProduct>(db);
            SerialNumbers = new GenericRepository<SerialNumbers>(db);
            PurchaseItemReceive = new GenericRepository<PurchaseItemReceive>(db);

            List<Packages> packageList = null;
            List<PickProduct> itemList = null;

            Orders order = Orders.Get(orderID);
            if (order != null && order.StatusCode == (int)OrderStatusCode.InProcess)
            {
                packageList = order.Packages.Where(p => p.IsEnable == true && p.ProcessStatus == (int)EnumData.ProcessStatus.待出貨)
                    .Join(Items.GetAll(true).Where(i => i.IsEnable == true && i.ShipFromWarehouseID == warehouseId), p => p.ID, i => i.PackageID, (p, i) => p).ToList();
                itemList = packageList.Join(PickProduct.GetAll(true).Where(pick => pick.IsEnable == true), p => p.ID, pp => pp.PackageID, (p, pp) => pp).Where(pp => pp.WarehouseID == warehouseId).ToList();

                if (itemList.Any())
                {
                    string[] productIDs = itemList.Select(p => p.ProductID).ToArray();
                    List<SerialNumbers> itemSerials = SerialNumbers.GetAll(true).Where(s => productIDs.Contains(s.ProductID)).ToList();

                    var groupList = itemList.GroupBy(p => p.PackageID).ToDictionary(p => p.Key, p => p.ToDictionary(pp => pp.ItemID,
                        pp => new { data = pp, serial = itemSerials.Where(sn => sn.OrderItemID == pp.ItemID).Select(sn => sn.SerialNumber.Trim()).ToArray() }));

                    var productList = itemList.GroupBy(i => i.ProductID).ToDictionary(i => i.First().ProductID, i => i.ToDictionary(p => p.ItemID));

                    var serialList = PurchaseItemReceive.GetAll(true).Where(s => productIDs.Contains(s.ProductID)).GroupBy(s => s.ProductID)
                    .ToDictionary(s => s.First().ProductID, s => new { isRequire = s.Max(sn => sn.IsRequireSerialScan), serials = s.Select(sn => sn.SerialNumber.Trim()).ToArray(), used = itemSerials.Where(i => i.ProductID == s.Key).Select(i => i.SerialNumber.Trim()).ToArray() });

                    var fileList = packageList.ToDictionary(p => p.ID, p => getFileData(p));

                    result.data = new { groupList, productList, serialList, fileList };
                }
            }

            result.status = !(order == null || itemList == null);
            return Content(JsonConvert.SerializeObject(result), "appllication/json");
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

            switch (package.Method.Carriers.CarrierAPI.Type)
            {
                case (byte)EnumData.CarrierType.DHL:
                    bool DHL_pdf = !System.IO.File.Exists(Path.Combine(basePath, string.Join("", package.FilePath.Skip(package.FilePath.IndexOf("export"))), "Invoice.xls"));

                    /***** 商業發票 *****/
                    fileName[1] = DHL_pdf ? "Invoice.pdf" : "Invoice.xls";
                    filePath[1] = Path.Combine(basePath, string.Join("", package.FilePath.Skip(package.FilePath.IndexOf("export"))), fileName[1]);
                    /***** 商業發票 *****/

                    amount = new int[] { 2, DHL_pdf ? 0 : 2 };
                    break;
                case (byte)EnumData.CarrierType.FedEx:
                    /***** 商業發票 *****/
                    fileName[1] = "Invoice.xls";
                    filePath[1] = Path.Combine(basePath, string.Join("", package.FilePath.Skip(package.FilePath.IndexOf("export"))), fileName[1]);
                    /***** 商業發票 *****/

                    amount = new int[] { 1, 4 };
                    break;
                case (byte)EnumData.CarrierType.Sendle:
                    amount = new int[] { 1, 0 };
                    break;
                case (byte)EnumData.CarrierType.USPS:
                    break;
            }

            // 取得熱感應印表機名稱
            string printerName = package.Method.PrinterName;

            return new { fileName, filePath, amount, printerName };
        }

        [CheckSession]
        public ActionResult PackagePickUpList()
        {
            int warehouseId = int.Parse(Session["warehouseId"].ToString());

            string packageSelect = string.Format("SELECT * FROM Packages WHERE IsEnable = 1 AND ProcessStatus = {0}", (byte)EnumData.ProcessStatus.待出貨);
            string orderSelect = string.Format("SELECT * FROM Orders WHERE StatusCode = {0}", (int)OrderStatusCode.InProcess);
            string itemSelect = string.Format("SELECT * FROM Items WHERE IsEnable = 1 AND ShipFromWarehouseID = {0}", warehouseId);

            var methodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && !m.IsDirectLine).ToList();

            ObjectContext context = new ObjectContext("name=QDLogisticsEntities");
            var ItemList = context.ExecuteStoreQuery<Packages>(packageSelect).ToList().Join(methodList, p => p.ShippingMethod, m => m.ID, (p, m) => p)
                .Join(context.ExecuteStoreQuery<Orders>(orderSelect).ToList(), p => p.OrderID, o => o.OrderID, (package, order) => new { order, package })
                .OrderBy(data => data.order.TimeOfOrder).OrderByDescending(data => data.order.RushOrder)
                .Join(db.Items.AsNoTracking().Where(i => i.IsEnable.Value && i.ShipFromWarehouseID.Value.Equals(warehouseId)), op => op.package.ID, i => i.PackageID, (op, item) => item).Distinct()
                .GroupBy(i => i.PackageID).ToList();

            ViewBag.itemList = ItemList;
            return PartialView("_PickUpList");
        }

        [CheckSession]
        [HttpPost]
        public ActionResult PrintPickUpList(int warehouseId, int adminId)
        {
            AdminUsers = new GenericRepository<AdminUsers>(db);

            AdminUsers admin = AdminUsers.Get(adminId);

            string packageSelect = string.Format("SELECT * FROM Packages WHERE IsEnable = 1 AND ProcessStatus = {0}", (byte)EnumData.ProcessStatus.待出貨);
            string orderSelect = string.Format("SELECT * FROM Orders WHERE StatusCode = {0}", (int)OrderStatusCode.InProcess);
            string itemSelect = string.Format("SELECT * FROM Items WHERE IsEnable = 1 AND ShipFromWarehouseID = {0}", warehouseId);

            var methodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && !m.IsDirectLine).ToList();

            ObjectContext context = new ObjectContext("name=QDLogisticsEntities");
            List<Items> itemList = context.ExecuteStoreQuery<Packages>(packageSelect).ToList().Join(methodList, p => p.ShippingMethod, m => m.ID, (p, m) => p)
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
                fileName[0] = "Single.xlsx";
                filePath[0] = Path.Combine(DirPath, fileName[0]);
                workbook1.SaveAs(filePath[0]);
            }

            XLWorkbook workbook2 = new XLWorkbook();
            if (SetWorkSheet(workbook2, "多項產品", itemGroupList.Where(i => i.Sum(ii => ii.Qty) > 1).ToList(), admin.Name))
            {
                fileName[1] = "Multiple.xlsx";
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

        [CheckSession]
        [HttpPost]
        public ActionResult UpdatePicked(List<PickProduct> picked, Dictionary<string, string[]> serial)
        {
            Packages = new GenericRepository<Packages>(db);
            PickProduct = new GenericRepository<PickProduct>(db);
            SerialNumbers = new GenericRepository<SerialNumbers>(db);

            AjaxResult result = new AjaxResult();

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

                int AdminId = 0;
                int.TryParse(Session["AdminId"].ToString(), out AdminId);
                DateTime PickUpDate = new TimeZoneConvert().Utc;

                foreach (PickProduct data in picked)
                {
                    data.IsMail = false;
                    data.PickUpDate = PickUpDate;
                    data.PickUpBy = AdminId;
                    PickProduct.Update(data, data.ID);
                }

                package.ProcessStatus = (int)EnumData.ProcessStatus.已出貨;
                package.DispatchDate = PickUpDate;
                Packages.Update(package, package.ID);

                foreach (Items item in package.Items.Where(i => i.IsEnable.Value))
                {
                    if (serial.ContainsKey(item.ID.ToString()) && serial[item.ID.ToString()].Any())
                    {
                        MyHelp.Log("Orders", package.OrderID, string.Format("產品【{0}】存入 {1}", item.ID, string.Join("、", serial[item.ID.ToString()])), Session);

                        foreach (string serialNumber in serial[item.ID.ToString()])
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

                using (Hubs.ServerHub server = new Hubs.ServerHub())
                    server.BroadcastOrderChange(package.OrderID.Value, EnumData.OrderChangeStatus.已完成出貨);

                MyHelp.Log("Packages", package.ID, string.Format("訂單【{0}】出貨完成", package.OrderID));
            }
            catch (Exception e)
            {
                ResetShippedData(package, picked, serial);

                MyHelp.ErrorLog(e, string.Format("訂單【{0}】出貨失敗", package.OrderID), package.OrderID.ToString());
                result.message = string.Format("訂單【{0}】出貨失敗，錯誤：", package.OrderID) + (e.InnerException != null ? e.InnerException.Message.Trim() : e.Message.Trim());
                result.status = false;
            }

            if (result.status)
            {
                try
                {
                    /***** SC 更新 *****/
                    TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
                    ThreadTask threadTask = new ThreadTask(string.Format("包貨區 - 更新訂單【{0}】資料至SC", package.OrderID));

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
                                error = Sync.Update_Tracking(Packages.Get(package.ID));
                            }
                            catch (Exception e)
                            {
                                ResetShippedData(package, picked, serial);

                                error = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                            }

                            return error;
                        }, HttpContext.Session));
                    }
                    /***** SC 更新 *****/
                }
                catch (Exception e)
                {
                    ResetShippedData(package, picked, serial);

                    MyHelp.ErrorLog(e, string.Format("更新訂單【{0}】資料至SC失敗", package.OrderID), package.OrderID.ToString());
                    result.message = string.Format("更新訂單【{0}】資料至SC失敗，錯誤：", package.OrderID) + (e.InnerException != null ? e.InnerException.Message.Trim() : e.Message.Trim());
                    result.status = false;
                }
            }

            return Content(JsonConvert.SerializeObject(result), "appllication/json");
        }

        [CheckSession]
        private void ResetShippedData(Packages package, List<PickProduct> picked, Dictionary<string, string[]> serial)
        {
            MyHelp.Log("OrderID", package.OrderID, string.Format("訂單【{0}】出貨狀態重置", package.OrderID));

            foreach (PickProduct data in picked)
            {
                data.IsPicked = false;
                data.QtyPicked = 0;
                PickProduct.Update(data, data.ID);
            }

            var serialArray = serial.SelectMany(s => s.Value).ToArray();
            MyHelp.Log("Orders", package.OrderID, string.Format("訂單【{0}】移除 Serial Number - {1}", package.OrderID, string.Join("、", serialArray)), Session);
            foreach (var ss in SerialNumbers.GetAll().Where(s => s.OrderID.Equals(package.OrderID) && serialArray.Contains(s.SerialNumber)))
            {
                SerialNumbers.Delete(ss);
            }

            package.ProcessStatus = (int)EnumData.ProcessStatus.待出貨;
            Packages.Update(package, package.ID);
            Packages.SaveChanges();
        }

        [CheckSession]
        public ActionResult OrderShippedData(DataFilter filter, int page = 1, int rows = 100)
        {
            Orders = new GenericRepository<Orders>(db);
            Addresses = new GenericRepository<Addresses>(db);
            Payments = new GenericRepository<Payments>(db);
            Packages = new GenericRepository<Packages>(db);
            Items = new GenericRepository<Items>(db);

            int total = 0;
            List<object> dataList = new List<object>();

            try
            {
                /** Order Filter **/
                var OrderFilter = db.Orders.AsNoTracking().Where(o => o.StatusCode.Value.Equals((int)OrderStatusCode.Completed) || o.ShippingStatus.Value.Equals((int)OrderShippingStatus.PartiallyShipped));

                if (!string.IsNullOrWhiteSpace(filter.OrderID)) OrderFilter = OrderFilter.Where(o => o.OrderID.ToString().Equals(filter.OrderID) || o.eBayUserID.Contains(filter.UserID));
                if (!string.IsNullOrWhiteSpace(filter.SourceID)) OrderFilter = OrderFilter.Where(o => (o.OrderSource.Value.Equals(1) && o.eBaySalesRecordNumber.Equals(filter.SourceID)) || (o.OrderSource.Value.Equals(4) && o.OrderSourceOrderId.Equals(filter.SourceID)));
                if (filter.Source.HasValue) OrderFilter = OrderFilter.Where(o => o.OrderSource.Value.Equals(filter.Source.Value));

                /** Package Filter **/
                var PackageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus == (byte)EnumData.ProcessStatus.已出貨);
                if (filter.MethodID.HasValue) PackageFilter = PackageFilter.Where(p => p.ShippingMethod.Value.Equals(filter.MethodID.Value));
                if (!string.IsNullOrWhiteSpace(filter.Tracking)) PackageFilter = PackageFilter.Where(p => p.TrackingNumber.Equals(filter.Tracking));
                if (filter.Export.HasValue) PackageFilter = PackageFilter.Where(p => p.Export.Value.Equals(filter.Export.Value));
                if (filter.ExportMethod.HasValue) PackageFilter = PackageFilter.Where(p => p.ExportMethod.Value.Equals(filter.Export.Value));
                if (!filter.PickUpDateFrom.Equals(new DateTime()))
                {
                    DateTime pickUpDateFrom = new DateTime(filter.PickUpDateFrom.Year, filter.PickUpDateFrom.Month, filter.PickUpDateFrom.Day, 0, 0, 0);
                    pickUpDateFrom = new TimeZoneConvert(pickUpDateFrom, MyHelp.GetTimeZone((int)Session["TimeZone"])).ConvertDateTime(EnumData.TimeZone.TST);
                    PackageFilter = PackageFilter.Where(p => DateTime.Compare(p.PickUpDate.Value, pickUpDateFrom) >= 0);
                }
                if (!filter.PickUpDateTo.Equals(new DateTime()))
                {
                    DateTime pickUpDateTo = new DateTime(filter.PickUpDateTo.Year, filter.PickUpDateTo.Month, filter.PickUpDateTo.Day + 1, 0, 0, 0);
                    pickUpDateTo = new TimeZoneConvert(pickUpDateTo, MyHelp.GetTimeZone((int)Session["TimeZone"])).ConvertDateTime(EnumData.TimeZone.TST);
                    PackageFilter = PackageFilter.Where(p => DateTime.Compare(p.PickUpDate.Value, pickUpDateTo) < 0);
                }

                /** Item Filter **/
                var ItemFilter = db.Items.AsNoTracking().Where(i => i.IsEnable.Value);
                if (filter.WarehouseID.HasValue) ItemFilter = ItemFilter.Where(i => i.ShipFromWarehouseID.Value.Equals(filter.WarehouseID.Value));
                if (!string.IsNullOrEmpty(filter.Sku) || !string.IsNullOrWhiteSpace(filter.ItemName))
                {
                    var SkuFilter = db.Skus.AsNoTracking().Where(s => s.IsEnable.Value);
                    if (!string.IsNullOrEmpty(filter.Sku)) SkuFilter = SkuFilter.Where(sku => sku.Sku.Equals(filter.Sku) || sku.UPC.Equals(filter.Sku));
                    if (!string.IsNullOrEmpty(filter.ItemName)) SkuFilter = SkuFilter.Where(sku => sku.ProductName.ToLower().Contains(filter.ItemName.ToLower()));
                    ItemFilter = ItemFilter.Join(SkuFilter, i => i.ProductID, sku => sku.Sku, (i, sku) => i);
                }

                /** Address Filter **/
                var AddressFilter = db.Addresses.AsNoTracking().Where(a => a.IsEnable.Value);
                if (!string.IsNullOrWhiteSpace(filter.CountryCode)) AddressFilter = AddressFilter.Where(a => a.CountryCode.Equals(filter.CountryCode));

                /** Payment Filter **/
                var PaymentFilter = db.Payments.AsNoTracking().Where(p => p.IsEnable.Value);
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

                var results = OrderFilter.ToList()
                    .Join(PackageFilter, o => o.OrderID, p => p.OrderID, (o, p) => new OrderJoinData() { order = o, package = p })
                    .Join(ItemFilter.GroupBy(i => i.PackageID.Value), oData => oData.package.ID, i => i.Key, (oData, i) => new OrderJoinData(oData) { item = i.First(), items = i.ToList(), itemCount = i.Sum(ii => 1 + ii.KitItemCount).Value })
                    .Join(AddressFilter, oData => oData.order.ShippingAddress, a => a.Id, (oData, a) => new OrderJoinData(oData) { address = a }).ToList();

                if (!filter.DateFrom.Equals(new DateTime()) || !filter.DateTo.Equals(new DateTime()))
                    results = results.Join(PaymentFilter, oData => oData.order.OrderID, p => p.OrderID, (oData, p) => new OrderJoinData(oData) { payment = p }).ToList();
                else
                    results = results
                        .GroupJoin(PaymentFilter, oData => oData.order.OrderID, p => p.OrderID, (oData, p) => new { orderJoinData = oData, payment = p.Take(1) })
                        .SelectMany(o => o.payment.DefaultIfEmpty(), (o, p) => new OrderJoinData(o.orderJoinData) { payment = p }).ToList();

                if (results.Any())
                {
                    int length = rows;
                    int start = (page - 1) * length;
                    total = results.Count();

                    int[] itemIDs;
                    if (!string.IsNullOrEmpty(filter.Serial))
                    {
                        itemIDs = db.SerialNumbers.AsNoTracking().Where(serial => serial.SerialNumber.Contains(filter.Serial)).Select(serial => serial.OrderItemID).ToArray();
                        results = results.Where(data => itemIDs.Contains(data.item.ID)).ToList();
                    }

                    results = results.OrderByDescending(data => data.order.TimeOfOrder).Skip(start).Take(length).ToList();

                    TimeZoneConvert TimeZoneConvert = new TimeZoneConvert();
                    EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);

                    Dictionary<int, string> method = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable).ToDictionary(m => m.ID, m => m.Name);

                    Dictionary<int, string> warehouses = db.Warehouses.AsNoTracking().Where(w => w.IsEnable.Value).ToDictionary(w => w.ID, w => w.Name);

                    itemIDs = results.SelectMany(data => data.items.Select(i => i.ID)).ToArray();
                    Dictionary<int, string> serialOfItem = db.SerialNumbers.AsNoTracking().Where(s => itemIDs.Contains(s.OrderItemID)).GroupBy(s => s.OrderItemID).ToDictionary(s => s.Key, s => s.First().SerialNumber);

                    dataList.AddRange(results.Select(data => new
                    {
                        PackageID = data.package.ID,
                        OrderID = data.package.OrderID,
                        ParentOrderID = data.order.ParentOrderID,
                        OrderSourceOrderId = data.order.OrderSourceOrderId,
                        ItemCount = data.itemCount,
                        Items = data.items.Where(i => i.Qty != 0).Select(i => new { ItemID = i.ID, Sku = i.ProductID, Qty = i.Qty }),
                        Warehouse = warehouses[data.item.ShipFromWarehouseID.Value],
                        PaymentDate = data.payment != null ? TimeZoneConvert.InitDateTime(data.payment.AuditDate.Value, EnumData.TimeZone.EST).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy<br />hh:mm tt") : "",
                        Sku = data.itemCount == 1 ? data.item.ProductID : "Multi",
                        DisplayName = data.itemCount == 1 ? data.item.DisplayName : "Multi",
                        OrderQtyTotal = data.items.Sum(i => i.Qty),
                        ShippingCountry = data.address.CountryName,
                        ShippingMethod = method.ContainsKey(data.package.ShippingMethod.Value) ? method[data.package.ShippingMethod.Value] : data.order.ShippingCarrier,
                        Export = Enum.GetName(typeof(EnumData.Export), data.package.Export != null ? data.package.Export : 0),
                        ExportMethod = Enum.GetName(typeof(EnumData.ExportMethod), data.package.ExportMethod != null ? data.package.ExportMethod : 0),
                        StatusCode = Enum.GetName(typeof(OrderStatusCode), data.order.StatusCode),
                        Comment = !string.IsNullOrEmpty(data.package.Comment) ? data.package.Comment : "",
                        SerialNumber = data.itemCount == 1 ? (serialOfItem.ContainsKey(data.item.ID) ? serialOfItem[data.item.ID] : "None") : "Multi",
                        PickUpDate = data.package.PickUpDate != null && !data.package.PickUpDate.Equals(DateTime.MinValue) ? TimeZoneConvert.InitDateTime(data.package.PickUpDate.Value, EnumData.TimeZone.UTC).ConvertDateTime(EnumData.TimeZone.TST).ToString("MM/dd/yyyy<br />hh:mm tt") : "",
                        TrackingNumber = data.package.TrackingNumber,
                        DeliveryStatus = data.package.DeliveryNote,
                        DispatchTime = data.payment != null ? formatTime(data.package.PickUpDate, TimeZoneConvert.InitDateTime(data.payment.AuditDate.Value, EnumData.TimeZone.EST).Utc) : "",
                        TransitTime = formatTime(data.package.DeliveryDate, data.package.PickUpDate),
                        RedirectWarehouse = warehouses[data.item.ReturnedToWarehouseID.Value],
                        RMA = data.package.RMAId,
                        Download = !string.IsNullOrEmpty(data.package.FilePath) ? "FileUploads/" + string.Join("", data.package.FilePath.Skip(data.package.FilePath.IndexOf("export"))) : ""
                    }));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.InnerException == null ? e.Message : e.InnerException.Message);
            }

            return Json(new { total = total, rows = dataList }, JsonRequestBehavior.AllowGet);
        }

        private string formatTime(DateTime? dateTime1, DateTime? dateTime2)
        {
            string format = "";

            if (dateTime1.HasValue && !dateTime1.Equals(DateTime.MinValue))
            {
                if (dateTime2.HasValue && !dateTime2.Equals(DateTime.MinValue))
                {
                    TimeSpan time = dateTime1.Value - dateTime2.Value;
                    format = string.Format("{0} day {1} hr {2} min", time.Days, time.Hours, time.Minutes);
                }
            }

            return format;
        }

        [CheckSession]
        [HttpPost]
        public ActionResult CreateRMA(List<Dictionary<string, int>> itemData, int reasonID, string description)
        {
            AjaxResult result = new AjaxResult();

            if (itemData.Any())
            {
                Packages = new GenericRepository<Packages>(db);
                Items = new GenericRepository<Items>(db);
                IRepository<BundleItems> BundleItems = new GenericRepository<BundleItems>(db);

                int[] itemIDs = itemData.Select(id => id["itemID"]).ToArray();
                List<Items> itemList = Items.GetAll(true).Where(i => itemIDs.Contains(i.ID)).ToList();
                List<BundleItems> bundleItemList = BundleItems.GetAll(true).Where(i => itemIDs.Contains(i.ID)).ToList();
                Packages package = Packages.Get(itemList.Any() ? itemList.First().PackageID.Value : bundleItemList.First().PackageID.Value);
                int OrderID = package.OrderID.Value;

                TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
                ThreadTask threadTask = new ThreadTask(string.Format("已出貨區 - 回收已出貨訂單【{0}】", OrderID));

                lock (factory)
                {
                    threadTask.AddWork(factory.StartNew(session =>
                    {
                        threadTask.Start();

                        string error = "";

                        try
                        {
                            HttpSessionStateBase Session = (HttpSessionStateBase)session;
                            SC_WebService SCWS = new SC_WebService(Session["ApiUserName"].ToString(), Session["ApiPassword"].ToString());

                            if (SCWS.Is_login)
                            {
                                Order order = SCWS.Get_OrderData(OrderID).Order;
                                order.OrderCreationSourceApplication = OrderCreationSourceApplicationType.PointOfSale;
                                if (SCWS.Update_Order(order))
                                {
                                    int RMAId = SCWS.Create_RMA(OrderID);

                                    if (reasonID == 16)
                                    {

                                        Carriers carrier = package.Method.Carriers;
                                        description = string.Format("{0} {1}, {2}", carrier.Name, package.TrackingNumber, description);

                                        if (carrier != null && !string.IsNullOrEmpty(carrier.Email))
                                        {
                                            if (!string.IsNullOrEmpty(carrier.Email))
                                            {
                                                string sendMail = "dispatch-qd@hotmail.com";
                                                string[] receiveMails = new string[] { carrier.Email };
                                                string[] ccMails = new string[] { "peter@qd.com.tw", "kelly@qd.com.tw", "demi@qd.com.tw" };
                                                string mailTitle = string.Format("至優網有限公司 (53362065) 退件請求 提單號碼: {0}", package.TrackingNumber);
                                                string mailBody = "<p>你好<p>";
                                                mailBody += string.Format("<p>提單號碼 {0} 急需安排退件. 請退回到以下地址:</p>", package.TrackingNumber);
                                                mailBody += "<p>至優網有限公司 (53362065)<br />403 台中市西區建國北路三段51號</p>";
                                                mailBody += "<p>若有相關退件問題請撥打以下電話:<br />(04) 2371 8118(上班時間)<br />0972907887(非上班時間)</p>";
                                                mailBody += "<p>謝謝</p>";

                                                var mailResult = MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, null, null, false);
                                            }
                                        }
                                    }

                                    if (itemList.Any())
                                    {
                                        foreach (Items item in itemList)
                                        {
                                            SCWS.Create_RMA_Item(item.OrderID.Value, item.ID, RMAId, itemData.First(i => i["itemID"] == item.ID)["qty"], reasonID, description);
                                        }
                                    }

                                    if (bundleItemList.Any())
                                    {
                                        foreach (BundleItems bundleItem in bundleItemList)
                                        {
                                            SCWS.Create_RMA_Item(bundleItem.OrderID.Value, bundleItem.OrderItemId.Value, RMAId, itemData.First(i => i["itemID"] == bundleItem.ID)["qty"], reasonID, description, bundleItem.ProductID);
                                        }
                                    }

                                    package.RMAId = RMAId;
                                    Packages.Update(package, package.ID);
                                    Packages.SaveChanges();

                                    order = SCWS.Get_OrderData(OrderID).Order;
                                    order.OrderCreationSourceApplication = OrderCreationSourceApplicationType.Default;
                                    SCWS.Update_Order(order);

                                    MyHelp.Log("Orders", OrderID, "完成回收已出貨訂單");
                                }
                            }
                        }
                        catch (DbEntityValidationException ex)
                        {
                            var errorMessages = ex.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(x => x.ErrorMessage);
                            error = string.Join("; ", errorMessages);
                        }
                        catch (Exception e)
                        {
                            error = e.Message;
                        }

                        return error;
                    }, HttpContext.Session));
                }
            }
            else
            {
                result.status = false;
                result.message = "沒有取得產品資料!";
            }

            return Content(JsonConvert.SerializeObject(result));
        }

        [CheckSession]
        public ActionResult OrderProductList(int PackageID, string Type, string Index)
        {
            Packages = new GenericRepository<Packages>(db);

            Packages package = Packages.Get(PackageID);

            if (package != null)
            {
                ViewBag.Index = Index;
                ViewBag.package = package;

                return PartialView("_" + Type + "ProductList");
            }

            return new EmptyResult();
        }

        [CheckSession]
        public ActionResult OrderColumOption()
        {
            Warehouses = new GenericRepository<Warehouses>(db);
            ShippingMethod = new GenericRepository<ShippingMethod>(db);

            List<object> currencyCode = new List<object>();
            foreach (int code in Enum.GetValues(typeof(CurrencyCodeType2)))
            {
                currencyCode.Add(new { text = Enum.GetName(typeof(CurrencyCodeType2), code), value = code });
            }

            List<Warehouses> warehouseList = Warehouses.GetAll(true).Where(w => w.IsEnable == true && w.IsSellable == true).OrderByDescending(w => w.IsDefault).OrderBy(w => w.ID).ToList();
            List<ShippingMethod> methodList = ShippingMethod.GetAll(true).Where(m => m.IsEnable).OrderBy(m => m.ID).ToList();
            var carrierOfWarehouse = warehouseList.ToDictionary(w => w.ID, w => GetCarrierBool(methodList, w.CarrierData));

            List<object> export = new List<object>();
            foreach (int code in Enum.GetValues(typeof(EnumData.Export)))
            {
                export.Add(new { text = Enum.GetName(typeof(EnumData.Export), code), value = code });
            }

            List<object> exportMethod = new List<object>();
            foreach (int code in Enum.GetValues(typeof(EnumData.ExportMethod)))
            {
                exportMethod.Add(new { text = EnumData.GetExportMethod(code), value = code });
            }

            List<object> statusCode = new List<object>();
            foreach (int code in Enum.GetValues(typeof(OrderStatusCode)))
            {
                statusCode.Add(new { text = Enum.GetName(typeof(OrderStatusCode), code), value = code });
            }

            object result = new
            {
                currencyCode = currencyCode,
                warehouse = warehouseList.Select(w => new { text = w.Name, value = w.ID }).ToList(),
                carrier = methodList.Select(m => new { text = m.Name, value = m.ID }).ToList(),
                export = export,
                exportMethod = exportMethod,
                statusCode = statusCode,
                carrierOfWarehouse
            };

            return Content(JsonConvert.SerializeObject(result), "appllication/json");
        }

        [CheckSession]
        public ActionResult WarehouseData()
        {
            Warehouses = new GenericRepository<Warehouses>(db);
            ShippingMethod = new GenericRepository<ShippingMethod>(db);

            int total = 0;
            List<object> dataList = new List<object>();

            IEnumerable<ShippingMethod> methodLIst = ShippingMethod.GetAll(true).Where(m => m.IsEnable).OrderBy(m => m.ID).ToList();
            IEnumerable<Warehouses> results = Warehouses.GetAll(true).Where(w => w.IsEnable == true).ToList();
            if (results.Any())
            {
                total = results.Count();

                dataList.AddRange(results.OrderBy(w => w.Name).Select(warehouse => new
                {
                    ID = warehouse.ID,
                    Name = warehouse.Name,
                    WarehouseType = Enum.GetName(typeof(WarehouseTypeType), warehouse.WarehouseType),
                    IsSellable = warehouse.IsSellable,
                    WinitWarehouseID = warehouse.WinitWarehouseID,
                    CarrierData = GetCarrierBool(methodLIst, warehouse.CarrierData)
                }));
            }

            return Content(JsonConvert.SerializeObject(new { total = total, rows = dataList }), "appllication/json");
        }

        [CheckSession]
        [HttpPost]
        public ActionResult WarehouseUpdate(List<Warehouses> data)
        {
            Warehouses = new GenericRepository<Warehouses>(db);

            foreach (Warehouses wData in data)
            {
                Warehouses warehouse = Warehouses.Get(wData.ID);
                if (warehouse != null)
                {
                    warehouse.IsSellable = wData.IsSellable;
                    warehouse.WinitWarehouseID = wData.WinitWarehouseID;
                    warehouse.CarrierData = wData.CarrierData;
                    Warehouses.Update(warehouse);
                }

                MyHelp.Log("Warehouses", wData.ID, "更新出貨倉資料");
            }

            Warehouses.SaveChanges();
            return Content(JsonConvert.SerializeObject(new { status = true }), "appllication/json");
        }

        [CheckSession]
        public ActionResult WinitWarehouseOption()
        {
            Winit_API winit = new Winit_API();

            Received result = winit.getWarehouses();
            warehouseData[] warehouseList = result.data.ToObject<warehouseData[]>();
            var WinitWarehouse = warehouseList.Select(w => new { text = w.warehouseName, value = w.warehouseID }).ToList();
            WinitWarehouse.Insert(0, new { text = "無", value = "0" });

            var data = new { WinitWarehouse };

            return Content(JsonConvert.SerializeObject(data), "appllication/json");
        }

        [CheckSession]
        public ActionResult CompanyData()
        {
            Companies = new GenericRepository<Companies>(db);

            int total = 0;
            List<object> dataList = new List<object>();
            IEnumerable<Companies> results = Companies.GetAll(true).ToList();
            if (results.Any())
            {
                total = results.Count();

                dataList.AddRange(results.OrderBy(w => w.ID).Select(company => new
                {
                    ID = company.ID,
                    Name = company.CompanyName
                }));
            }

            return Content(JsonConvert.SerializeObject(new { total = total, rows = dataList }), "appllication/json");
        }


        [CheckSession]
        public ActionResult ShippingMethodData()
        {
            int total = 0;
            List<object> dataList = new List<object>();

            using (ShippingMethod = new GenericRepository<ShippingMethod>(db))
            {
                List<ShippingMethod> results = ShippingMethod.GetAll(true).Where(m => m.IsEnable).OrderBy(m => m.ID).ToList();

                Carriers = new GenericRepository<Carriers>(db);
                var carrierList = Carriers.GetAll(true).Where(c => c.IsEnable && c.CarrierAPI != null).ToDictionary(c => c.ID, c => Enum.GetName(typeof(EnumData.CarrierType), c.CarrierAPI.Type));

                if (results.Any())
                {
                    total = results.Count();

                    dataList.AddRange(results.OrderBy(m => m.Name).Select(m => new
                    {
                        m.ID,
                        CarrierType = carrierList.ContainsKey(m.CarrierID.Value) ? carrierList[m.CarrierID.Value] : "",
                        m.IsDirectLine,
                        m.Name,
                        m.CarrierID,
                        m.MethodType,
                        m.BoxType,
                        m.IsExport,
                        m.IsBattery,
                        m.InBox
                    }).ToList());
                }
            }

            return Json(new { total, rows = dataList }, JsonRequestBehavior.AllowGet);
        }

        [CheckSession]
        [HttpPost]
        public ActionResult ShippingMethodUpdate(List<ShippingMethod> data)
        {
            if (!MyHelp.CheckAuth("shipping", "shippingMethod", EnumData.AuthType.Edit))
            {
                return Content(JsonConvert.SerializeObject(new { status = false, message = "沒有權限!" }), "appllication/json");
            }

            ShippingMethod = new GenericRepository<ShippingMethod>(db);

            foreach (ShippingMethod sData in data)
            {
                ShippingMethod method = ShippingMethod.Get(sData.ID);
                if (method != null)
                {
                    method.IsDirectLine = sData.IsDirectLine;
                    method.Name = sData.Name;
                    method.CarrierID = sData.CarrierID;
                    method.MethodType = sData.MethodType;
                    method.BoxType = sData.BoxType;
                    method.IsExport = sData.IsExport;
                    method.IsBattery = sData.IsBattery;
                    method.InBox = sData.InBox;
                    ShippingMethod.Update(method);
                }

                MyHelp.Log("ShippingMethod", sData.ID, "更新運輸方式資料");
            }

            ShippingMethod.SaveChanges();
            return Content(JsonConvert.SerializeObject(new { status = true }), "appllication/json");
        }

        [CheckSession]
        public ActionResult CarrierData()
        {
            int total = 0;
            List<object> dataList = new List<object>();

            using (Carriers = new GenericRepository<Carriers>(db))
            {
                List<Carriers> results = Carriers.GetAll(true).Where(c => c.IsEnable).OrderBy(c => c.ID).ToList();

                if (results.Any())
                {
                    total = results.Count();

                    dataList.AddRange(results.OrderBy(c => c.Api).Select(c => new
                    {
                        c.ID,
                        c.Name,
                        c.Email,
                        c.Api
                    }).ToList());
                }
            }

            return Json(new { total, rows = dataList }, JsonRequestBehavior.AllowGet);
        }

        [CheckSession]
        [HttpPost]
        public ActionResult CarrierUpdate(List<Carriers> data)
        {
            if (!MyHelp.CheckAuth("shipping", "carrier", EnumData.AuthType.Edit))
            {
                return Content(JsonConvert.SerializeObject(new { status = false, message = "沒有權限!" }), "appllication/json");
            }

            Carriers = new GenericRepository<Carriers>(db);

            foreach (Carriers cData in data)
            {
                Carriers carrier = Carriers.Get(cData.ID);
                if (carrier != null)
                {
                    carrier.Name = cData.Name;
                    carrier.Email = cData.Email;
                    carrier.Api = cData.Api;
                    Carriers.Update(carrier);
                }

                MyHelp.Log("Carriers", cData.ID, "更新運輸商資料");
            }

            Carriers.SaveChanges();
            return Content(JsonConvert.SerializeObject(new { status = true }), "appllication/json");
        }

        public ActionResult GetDirectLineData(int draw, int start, int length, Dictionary<string, string> search)
        {
            using (IRepository<DirectLine> DirectLine = new GenericRepository<DirectLine>(db))
            {
                int total = 0;
                string value = search["value"];
                List<Object> dataList = new List<Object>();

                var DirectLineFilter = DirectLine.GetAll(true).Where(d => d.IsEnable);
                if (!string.IsNullOrEmpty(value)) DirectLineFilter = DirectLineFilter.Where(d => d.Name.Contains(value));
                var results = DirectLineFilter.ToList();
                if (results.Any())
                {
                    total = results.Count();
                    dynamic routeValue = new RouteValue(start, length, value);

                    dataList.AddRange(results.OrderBy(d => d.ID).Skip(start).Take(length).Select(d => new
                    {
                        abbrevation = d.Abbreviation,
                        name = d.Name,
                        action = getButton("Default", "shipping", "directLineEdit", d.ID, routeValue) + getButton("Default", "shipping", "directLineDelete", d.ID, routeValue)
                    }));
                }

                return Json(new { draw = draw, data = dataList, recordsFiltered = total, recordsTotal = total }, JsonRequestBehavior.AllowGet);
            }
        }

        [CheckSession]
        public ActionResult ServiceData(int page = 1, int rows = 100)
        {
            Services = new GenericRepository<Services>(db);

            int total = 0;
            List<object> dataList = new List<object>();

            IEnumerable<Services> results = Services.GetAll(true).Where(s => s.IsEnable == true).ToList();
            if (results.Any())
            {
                int length = rows;
                int start = (page - 1) * length;
                total = results.Count();

                dataList.AddRange(results.OrderBy(s => s.ServiceCode).Skip(start).Take(length).Select(service => new
                {
                    service.ServiceCode,
                    service.ServiceName,
                    service.ShippingMethod
                }));
            }

            return Content(JsonConvert.SerializeObject(new { total = total, rows = dataList }), "appllication/json");
        }

        [CheckSession]
        [HttpPost]
        public ActionResult ServiceUpdate(List<Services> data)
        {
            Services = new GenericRepository<Services>(db);

            foreach (Services sData in data)
            {
                Services service = Services.Get(sData.ServiceCode);
                if (service != null)
                {
                    service.ShippingMethod = sData.ShippingMethod;
                    Services.Update(service);
                }

                MyHelp.Log("Services", sData.ServiceCode, "更新預設運輸方式");
            }

            Services.SaveChanges();
            return Content(JsonConvert.SerializeObject(new { status = true }), "appllication/json");
        }

        [CheckSession]
        public ActionResult CountryData()
        {
            ShippingMethod = new GenericRepository<ShippingMethod>(db);

            int total = 0;
            List<object> dataList = new List<object>();

            IEnumerable<Country> countries = MyHelp.GetCountries();
            IEnumerable<ShippingMethod> results = ShippingMethod.GetAll(true).Where(c => c.IsEnable == true).ToList();
            if (results.Any())
            {
                total = results.Count();

                dataList.AddRange(results.OrderBy(c => c.ID).Select(method => new
                {
                    ID = method.ID,
                    Name = method.Name,
                    CountryData = GetCountryBool(countries, method.CountryData)
                }));
            }

            return Content(JsonConvert.SerializeObject(new { total = total, rows = dataList }), "appllication/json");
        }

        [CheckSession]
        public ActionResult plugData()
        {
            var CountryType = db.CountryType.AsQueryable();
            int total = 0;
            List<object> dataList = new List<object>();
            IEnumerable<Country> countries = MyHelp.GetCountries();
            if (CountryType.Any())
            {
                total = CountryType.Count();
                dataList.AddRange(CountryType.OrderBy(c => c.TypeImg).Select(method => new
                {
                    No = method.No,
                    TypeImg = method.TypeImg,
                    Country = method.Country
                }));
            }
            return Content(JsonConvert.SerializeObject(new { total = total, rows = dataList }), "appllication/json");
        }

        [CheckSession]
        public ActionResult plugUpdate(List<CountryType> data)
        {
            if (data.Any())
            {
                foreach (var item in data)
                {
                    var oCountryType = db.CountryType.Find(item.No);
                    oCountryType.Country = item.Country;
                }
                db.SaveChanges();
                return Content(JsonConvert.SerializeObject(new { status = true }), "appllication/json");
            }
            else
            {
                return Content(JsonConvert.SerializeObject(new { status = false }), "appllication/json");
            }
        }

        [CheckSession]
        [HttpPost]
        public ActionResult CountryUpdate(List<ShippingMethod> data)
        {
            ShippingMethod = new GenericRepository<ShippingMethod>(db);

            foreach (ShippingMethod sData in data)
            {
                ShippingMethod method = ShippingMethod.Get(sData.ID);
                if (method != null)
                {
                    method.CountryData = sData.CountryData;
                    ShippingMethod.Update(method);
                }

                MyHelp.Log("ShippingMethod", sData.ID, "更新運輸國家");
            }

            ShippingMethod.SaveChanges();
            return Content(JsonConvert.SerializeObject(new { status = true }), "appllication/json");
        }

        [CheckSession]
        public ActionResult ShippingApiData(int draw, int start, int length, Dictionary<string, string> search)
        {
            CarrierAPI = new GenericRepository<CarrierAPI>(db);

            int total = 0;
            string value = search["value"];
            List<Object> dataList = new List<Object>();

            IEnumerable<CarrierAPI> results = CarrierAPI.GetAll(true);
            if (!string.IsNullOrEmpty(value)) results = results.Where(a => a.Name.Contains(value));
            results = results.Where(a => a.IsEnable == true).ToList();
            if (results.Any())
            {
                total = results.Count();
                dynamic routeValue = new RouteValue(start, length, value);

                dataList.AddRange(results.OrderBy(a => a.Id).Skip(start).Take(length).Select(api => new
                {
                    name = api.Name,
                    action = getButton("Default", "shipping", "apiedit", api.Id, routeValue) + getButton("Default", "shipping", "apidelete", api.Id, routeValue)
                }));
            }

            return Content(JsonConvert.SerializeObject(new { draw = draw, data = dataList, recordsFiltered = total, recordsTotal = total }), "appllication/json");
        }

        [CheckSession]
        public ActionResult ProductTypeData(int page = 1, int rows = 100)
        {
            ProductType = new GenericRepository<Models.ProductType>(db);

            int total = 0;
            List<object> dataList = new List<object>();

            IEnumerable<Models.ProductType> results = ProductType.GetAll(true).Where(t => t.IsEnable == true).ToList();
            if (results.Any())
            {
                int length = rows;
                int start = (page - 1) * length;
                total = results.Count();

                dataList.AddRange(results.OrderBy(t => t.ID).Skip(start).Take(length).Select(type => new
                {
                    type.ID,
                    Name = type.ProductTypeName,
                    ChtName = type.ChtName ?? "",
                    HSCode = type.HSCode ?? ""
                }));
            }

            return Content(JsonConvert.SerializeObject(new { total = total, rows = dataList }), "appllication/json");
        }

        [CheckSession]
        [HttpPost]
        public ActionResult ProductTypeUpdate(List<Models.ProductType> data)
        {
            ProductType = new GenericRepository<Models.ProductType>(db);

            foreach (Models.ProductType tData in data)
            {
                Models.ProductType type = ProductType.Get(tData.ID);
                if (type != null)
                {
                    type.ChtName = tData.ChtName;
                    type.HSCode = tData.HSCode;
                    ProductType.Update(type, type.ID);
                }

                MyHelp.Log("ProductType", tData.ID, "更新品項資料");
            }

            ProductType.SaveChanges();
            return Content(JsonConvert.SerializeObject(new { status = true }), "appllication/json");
        }

        [CheckSession]
        public ActionResult SkuData(DataFilter filter, int page = 1, int rows = 100)
        {
            Skus = new GenericRepository<Skus>(db);
            Manufacturers = new GenericRepository<Manufacturers>(db);

            int total = 0;
            List<object> dataList = new List<object>();

            IEnumerable<Skus> results = db.Skus.AsNoTracking().Where(s => s.IsEnable.Value).AsQueryable();
            if (!filter.Battery.Equals(null)) results = results.Where(s => s.Battery == filter.Battery);
            if (!filter.Export.Equals(null)) results = results.Where(s => s.Export == filter.Export);
            if (!filter.ExportMethod.Equals(null)) results = results.Where(s => s.ExportMethod == filter.ExportMethod);
            if (!string.IsNullOrEmpty(filter.Sku)) results = results.Where(s => s.Sku.Contains(filter.Sku));
            if (!string.IsNullOrEmpty(filter.ProductName)) results = results.Where(s => s.ProductName.ToLower().Contains(filter.ProductName.ToLower()));
            if (!string.IsNullOrEmpty(filter.PurchaseInvoice)) results = results.Where(p => p.PurchaseInvoice.Contains(filter.PurchaseInvoice));
            //results = results.Where(s => s.IsEnable == true).ToList();
            if (results.Any())
            {
                int length = rows;
                int start = (page - 1) * length;
                int[] brand = new int[] { 0, -1 };
                total = results.Count();
                results = results.OrderBy(s => s.Sku).Skip(start).Take(length).ToList();

                var brandName = Manufacturers.GetAll(true).Where(m => results.Select(s => s.Brand).ToArray().Contains(m.ID)).ToDictionary(m => m.ID, m => m.ManufacturerName);

                dataList.AddRange(results.Select(sku => new
                {
                    Sku = sku.Sku,
                    ProductName = sku.ProductName,
                    ProductType = sku.ProductTypeID.Value,
                    Brand = !brand.Contains(sku.Brand.Value) ? brandName[sku.Brand.Value] : "",
                    Origin = sku.Origin,
                    Battery = sku.Battery,
                    Export = sku.Export,
                    ExportMethod = sku.ExportMethod,
                    PurchaseInvoice = !string.IsNullOrEmpty(sku.PurchaseInvoice) ? sku.PurchaseInvoice : "",
                    Weight = sku.Weight
                }));
            }

            return Content(JsonConvert.SerializeObject(new { total = total, rows = dataList }), "appllication/json");
        }

        public ActionResult SkuUpdate(List<Skus> data)
        {
            Skus = new GenericRepository<Skus>(db);

            foreach (Skus sData in data)
            {
                Skus sku = Skus.Get(sData.Sku);
                if (sku != null)
                {
                    sku.Battery = sData.Battery;
                    sku.ProductTypeID = sData.ProductTypeID;
                    sku.Origin = sData.Origin;
                    sku.Export = sData.Export;
                    sku.ExportMethod = sData.ExportMethod;
                    sku.PurchaseInvoice = sData.PurchaseInvoice;
                    sku.Weight = sData.Weight;
                    Skus.Update(sku);
                }

                MyHelp.Log("Skus", sData.Sku, "更新產品序號資料");
            }

            Skus.SaveChanges();
            return Content(JsonConvert.SerializeObject(new { status = true }), "appllication/json");
        }

        [CheckSession]
        public ActionResult SkuColumOption()
        {
            ProductType = new GenericRepository<Models.ProductType>(db);

            var productType = ProductType.GetAll(true).Where(w => w.IsEnable == true).OrderBy(w => w.ID).Select(t => new { text = t.ProductTypeName, value = t.ID }).ToList();
            var country = MyHelp.GetCountries().Select(c => new { text = c.Name + "/" + c.ChtName, value = c.ID }).ToList();

            List<object> export = new List<object>();
            foreach (int code in Enum.GetValues(typeof(EnumData.Export)))
            {
                export.Add(new { text = Enum.GetName(typeof(EnumData.Export), code), value = code });
            }

            List<object> exportMethod = new List<object>();
            foreach (int code in Enum.GetValues(typeof(EnumData.ExportMethod)))
            {
                exportMethod.Add(new { text = EnumData.GetExportMethod((int)code), value = code });
            }

            object result = new { productType, country, export, exportMethod };

            return Content(JsonConvert.SerializeObject(result), "appllication/json");
        }

        [CheckSession]
        public ActionResult AdminGroupData(int draw, int start, int length, Dictionary<string, string> search)
        {
            AdminGroups = new GenericRepository<AdminGroups>(db);

            int total = 0;
            string value = search["value"];
            List<object> dataList = new List<object>();

            IEnumerable<AdminGroups> results = AdminGroups.GetAll(true).Where(g => g.IsEnable);
            if (!string.IsNullOrEmpty(value)) results = results.Where(g => g.Name.Contains(value));
            results = results.ToList();
            if (results.Any())
            {
                total = results.Count();
                dynamic routeValue = new RouteValue(start, length, value);
                dataList.AddRange(results.OrderBy(g => g.Order).Skip(start).Take(length).Select(group => new
                {
                    isVisible = "<td><img src=\"/content/img/bullet_" + (group.IsVisible ? "tick" : "cross") + ".png\" width=\"25\"></td>",
                    name = group.Name,
                    order = group.Order,
                    action = getButton("Admin", "group", "edit", group.Id, routeValue) + getButton("Admin", "group", "delete", group.Id, routeValue)
                }));
            }

            return Content(JsonConvert.SerializeObject(new { draw = draw, data = dataList, recordsFiltered = total, recordsTotal = total }), "appllication/json");
        }

        [CheckSession]
        public ActionResult AdminUserData(int draw, int start, int length, Dictionary<string, string> search, int? gId)
        {
            AdminUsers = new GenericRepository<AdminUsers>(db);

            var total = 0;
            string value = search["value"];
            List<object> dataList = new List<object>();

            IEnumerable<AdminUsers> results = AdminUsers.GetAll(true).Where(u => u.IsEnable);
            if (!string.IsNullOrEmpty(value)) results = results.Where(u => u.Name.Contains(value));
            if (!int.Equals(gId, null)) results = results.Where(u => u.GroupId == gId);
            results = results.ToList();
            if (results.Any())
            {
                total = results.Count();
                dynamic routeValue = new RouteValue(start, length, value);
                routeValue.gId = gId;
                dataList.AddRange(results.OrderBy(u => u.Id).Skip(start).Take(length).Select(user => new
                {
                    isVisible = "<td><img src=\"/content/img/bullet_" + (user.IsVisible ? "tick" : "cross") + ".png\" width=\"25\"></td>",
                    name = user.Name,
                    groupName = user.AdminGroups.Name,
                    action = getButton("Admin", "user", "edit", user.Id, routeValue) + getButton("Admin", "user", "delete", user.Id, routeValue)
                }));
            }

            return Content(JsonConvert.SerializeObject(new { draw = draw, data = dataList, recordsFiltered = total, recordsTotal = total }), "appllication/json");
        }

        [CheckSession]
        public ActionResult AdminGroupOption(int? selectId)
        {
            AdminGroups = new GenericRepository<AdminGroups>(db);

            var option = "";
            var results = AdminGroups.GetAll(true).Where(a => a.IsEnable & a.IsVisible);

            if (results.Any())
            {
                foreach (AdminGroups group in results)
                {
                    option += "<option value='" + group.Id + "'>" + group.Name + "</option>";
                }
            }

            return Content(JsonConvert.SerializeObject(new { option = option }), "appllication/json");
        }

        [CheckSession]
        public ActionResult TaskSchedulerData(DataFilter filter, int page = 1, int rows = 100)
        {
            AdminUsers = new GenericRepository<AdminUsers>(db);
            TaskScheduler = new GenericRepository<Models.TaskScheduler>(db);

            int total = 0;
            List<object> dataList = new List<object>();

            IEnumerable<Models.TaskScheduler> results = TaskScheduler.GetAll(true);
            if (!filter.TaskID.Equals(null)) results = results.Where(task => task.ID == filter.TaskID);
            if (!filter.TaskStatus.Equals(null)) results = results.Where(task => task.Status == filter.TaskStatus);
            if (!filter.AdminID.Equals(null)) results = results.Where(task => task.UpdateBy == filter.AdminID);
            if (!string.IsNullOrWhiteSpace(filter.TaskName)) results = results.Where(task => task.Description.Contains(filter.TaskName));
            if (!filter.DateFrom.Equals(new DateTime()))
            {
                DateTime dateFrom = new DateTime(filter.DateFrom.Year, filter.DateFrom.Month, filter.DateFrom.Day, 0, 0, 0);
                dateFrom = new TimeZoneConvert(dateFrom, MyHelp.GetTimeZone((int)Session["TimeZone"])).Utc;
                results = results.Where(task => DateTime.Compare(task.CreateDate.Value, dateFrom) >= 0);
            }
            if (!filter.DateTo.Equals(new DateTime()))
            {
                DateTime dateTo = new DateTime(filter.DateTo.Year, filter.DateTo.Month, filter.DateTo.Day + 1, 0, 0, 0);
                dateTo = new TimeZoneConvert(dateTo, MyHelp.GetTimeZone((int)Session["TimeZone"])).Utc;
                results = results.Where(task => DateTime.Compare(task.CreateDate.Value, dateTo) < 0);
            }
            results = results.ToList();
            if (results.Any())
            {
                int length = rows;
                int start = (page - 1) * length;
                total = results.Count();

                TimeZoneConvert DateTimeConvert = new TimeZoneConvert();
                EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);
                var admins = AdminUsers.GetAll(true).Where(user => results.Select(s => s.UpdateBy).Contains(user.Id)).ToDictionary(user => user.Id, user => user.Name);
                admins = admins.Concat(new Dictionary<int, string> { { -1, "工作排程" }, { 0, "Weypro" } }).ToDictionary(x => x.Key, x => x.Value);

                dataList.AddRange(results.OrderByDescending(task => task.ID).Skip(start).Take(length).Select(task => new
                {
                    ID = task.ID,
                    Date = DateTimeConvert.InitDateTime(task.CreateDate.Value, EnumData.TimeZone.UTC).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy hh:mm tt"),
                    Message = task.Message,
                    Description = task.Description,
                    AdminName = admins[task.UpdateBy.Value],
                    Status = Enum.GetName(typeof(EnumData.TaskStatus), task.Status)
                }));
            }

            return Content(JsonConvert.SerializeObject(new { total = total, rows = dataList }), "appllication/json");
        }

        [CheckSession]
        public ActionResult TaskLogList(int TaskID, string Index)
        {
            TaskScheduler = new GenericRepository<Models.TaskScheduler>(db);

            Models.TaskScheduler task = TaskScheduler.Get(TaskID);

            if (task != null)
            {
                ViewBag.Index = Index;
                ViewBag.task = task;

                return PartialView("_TaskLogList", task);
            }

            return new EmptyResult();
        }


        private string getButton(string route, string controller, string action, int id, RouteValue routeValue)
        {
            string button = "";
            routeValue.setUrl(controller, action, id);
            string url = Url.RouteUrl(route, routeValue);

            switch (action)
            {
                case "edit":
                case "directLineEdit":
                case "apiedit":
                    button = "<a href='" + url + "' class='btn btn-default'><i class='fa fa-gear'></i><span class='hidden-mobile'> 編輯</span></a>";
                    break;
                case "view":
                    button = "<a href='" + url + "' class='btn btn-default'><i class='fa fa-gear'></i><span class='hidden-mobile'> 瀏覽</span></a>";
                    break;
                case "delete":
                case "directLineDelete":
                case "apidelete":
                    button = "<a href='" + url + "' class='btn btn-danger' onclick='return confirm(\'確定要刪除?\');'><i class='glyphicon glyphicon-trash'></i><span class='hidden-mobile'> 刪除</span></a>";
                    break;
            }

            return button;
        }

        private Dictionary<int, bool> GetCarrierBool(IEnumerable<ShippingMethod> methodList, string CarrierData)
        {
            if (string.IsNullOrEmpty(CarrierData)) return methodList.ToDictionary(m => m.ID, m => false);

            Dictionary<string, bool> Data = JsonConvert.DeserializeObject<Dictionary<string, bool>>(CarrierData);
            return methodList.ToDictionary(m => m.ID, m => Data.ContainsKey(m.ID.ToString()) ? Data[m.ID.ToString()] : false);
        }

        private Dictionary<string, bool> GetCountryBool(IEnumerable<Country> countries, string CountryData)
        {
            if (string.IsNullOrEmpty(CountryData)) return countries.ToDictionary(c => c.ID, c => false);

            Dictionary<string, bool> Data = JsonConvert.DeserializeObject<Dictionary<string, bool>>(CountryData);
            return countries.ToDictionary(c => c.ID, c => Data.ContainsKey(c.ID) ? Data[c.ID] : false);
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