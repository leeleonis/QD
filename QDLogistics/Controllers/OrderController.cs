using AutoMapper;
using CarrierApi.Winit;
using ClosedXML.Excel;
using DirectLineApi.IDS;
using Ionic.Zip;
using Neodynamic.SDK.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using QDLogistics.Commons;
using QDLogistics.Filters;
using QDLogistics.Models;
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
    public class OrderController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<Orders> Orders;
        private IRepository<Payments> Payments;
        private IRepository<Packages> Packages;
        private IRepository<Items> Items;
        private IRepository<SerialNumbers> SerialNumbers;
        private IRepository<ShippingMethod> ShippingMethod;
        private IRepository<Carriers> Carriers;
        private IRepository<PickProduct> PickProduct;
        private IRepository<Warehouses> Warehouses;
        private IRepository<Box> Box;
        private IRepository<DirectLineLabel> Label;

        private SCServiceSoapClient OS_sellerCloud;
        private AuthHeader OS_authHeader;
        private ServiceOptions OS_options;
        private SerializableDictionaryOfStringString OS_filters;

        private OrderCreationService.OrderCreationServiceSoapClient OCS_sellerCloud;
        private OrderCreationService.AuthHeader OCS_authHeader;

        private DateTime SyncOn;
        private DateTime Today;

        public OrderController()
        {
            db = new QDLogisticsEntities();
        }

        [CheckSession]
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult OrderToWaiting(List<string> packageIDs)
        {
            if (!MyHelp.CheckAuth("order", "index", EnumData.AuthType.Edit))
            {
                return Content(JsonConvert.SerializeObject(new { status = false, message = "你沒有權限執行這個動作!" }), "appllication/json");
            }

            Orders = new GenericRepository<Orders>(db);
            Packages = new GenericRepository<Packages>(db);
            List<Packages> packageList = Packages.GetAll(true).Where(p => p.IsEnable == true && packageIDs.Contains(p.ID.ToString())).ToList();
            List<Orders> orderList = Orders.GetAll(true).Where(o => packageList.Select(p => p.OrderID.Value).Contains(o.OrderID)).ToList();

            if (packageList.Any())
            {
                try
                {
                    List<string> errorMessages = new List<string>();
                    if (packageList.Any(p => !p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.訂單管理)))
                        errorMessages.AddRange(packageList.Where(p => !p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.訂單管理)).Select(p => string.Format("訂單【{0}】無法提交因為已在【{1}】的狀態", p.OrderID, Enum.GetName(typeof(EnumData.ProcessStatus), p.ProcessStatus))).ToList());

                    if (orderList.Any(o => !o.StatusCode.Equals((int)OrderStatusCode.InProcess)))
                        errorMessages.AddRange(orderList.Where(o => !o.StatusCode.Equals((int)OrderStatusCode.InProcess)).Select(o => string.Format("訂單【{0}】無法提交因為並非InProcess的狀態", o.OrderID)).ToList());

                    if (orderList.Any(o => !o.PaymentStatus.Equals((int)OrderPaymentStatus.Charged)))
                        errorMessages.AddRange(orderList.Where(o => !o.PaymentStatus.Equals((int)OrderPaymentStatus.Charged)).Select(o => string.Format("訂單【{0}】無法提交因為並非Payment的狀態", o.OrderID)).ToList());

                    TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;

                    orderList = orderList.Where(o => o.StatusCode.Equals((int)OrderStatusCode.InProcess) && o.PaymentStatus.Equals((int)OrderPaymentStatus.Charged)).ToList();
                    foreach (Packages package in packageList.Where(p => p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.訂單管理) && orderList.Any(o => o.OrderID.Equals(p.OrderID))).ToList())
                    {
                        ThreadTask threadTask = new ThreadTask(string.Format("訂單管理區 - 提交訂單【{0}】至待出貨區", package.OrderID));

                        package.ProcessStatus = (int)EnumData.ProcessStatus.鎖定中;
                        Packages.Update(package, package.ID);

                        lock (factory)
                        {
                            threadTask.AddWork(factory.StartNew(Session =>
                            {
                                threadTask.Start();

                                string message = "";
                                Packages packageData = Packages.Get(package.ID);

                                try
                                {
                                    HttpSessionStateBase session = (HttpSessionStateBase)Session;
                                    SC_WebService SCWS = new SC_WebService(session["ApiUserName"].ToString(), session["ApiPassword"].ToString());

                                    if (!SCWS.Is_login) throw new Exception("SC is not login");

                                    OrderStateInfo order = SCWS.Get_OrderStatus(packageData.OrderID.Value);

                                    if ((int)order.PaymentStatus == package.Orders.PaymentStatus)
                                    {
                                        ShipProcess shipProcess = new ShipProcess(SCWS);

                                        MyHelp.Log("Orders", packageData.OrderID, "提交至待出貨區", session);

                                        /***** 上傳Item出貨倉 *****/
                                        var SC_order = SCWS.Get_OrderData(packageData.OrderID.Value).Order;
                                        var SC_items = SC_order.Items.Where(i => i.PackageID.Equals(packageData.ID)).ToArray();
                                        foreach (var item in SC_items)
                                        {
                                            if (!db.Skus.AsNoTracking().Any(s => s.Sku.Equals(item.ProductID))) throw new Exception(string.Format("系統尚未有品號 {0} 資料!", item.ProductID));

                                            item.ShipFromWareHouseID = packageData.Items.First(i => i.IsEnable == true && i.ID == item.ID).ShipFromWarehouseID.Value;
                                            SCWS.Update_OrderItem(item);
                                        }
                                        MyHelp.Log("Orders", packageData.OrderID, "更新訂單包裏的出貨倉", session);

                                        /***** 更新客戶地址 *****/
                                        var address = SC_order.ShippingAddress;
                                        DataProcess.SetAddressData(packageData.Orders.Addresses, address, SC_order.BillingAddress);

                                        /***** 檢查運送國家 *****/
                                        if (!string.IsNullOrEmpty(packageData.Method.CountryData))
                                        {
                                            var countryData = JsonConvert.DeserializeObject<Dictionary<string, bool>>(packageData.Method.CountryData);
                                            if (!countryData.ContainsKey(packageData.Orders.Addresses.CountryCode.ToUpper()))
                                            {
                                                throw new Exception(string.Format("訂單【{0}】國家名稱不合，請重新確認", packageData.OrderID));
                                            }

                                            if (!countryData[packageData.Orders.Addresses.CountryCode.ToUpper()])
                                            {
                                                throw new Exception(string.Format("訂單【{0}】不可寄送至國家{1}", packageData.OrderID, packageData.Orders.Addresses.CountryName));
                                            }
                                        }

                                        MyHelp.Log("Orders", packageData.OrderID, "訂單開始提交", session);

                                        shipProcess.Init(packageData);
                                        var result = shipProcess.Dispatch();

                                        if (result.Status)
                                        {
                                            MyHelp.Log("Orders", packageData.OrderID, "訂單提交完成", session);

                                            byte[] carrierType = new byte[] { (byte)EnumData.CarrierType.DHL, (byte)EnumData.CarrierType.FedEx, (byte)EnumData.CarrierType.IDS };
                                            if (!shipProcess.isDropShip && carrierType.Contains(package.Method.Carriers.CarrierAPI.Type.Value))
                                            {
                                                PickProduct = new GenericRepository<PickProduct>(db);

                                                List<PickProduct> pickList = PickProduct.GetAll(true).Where(p => packageData.Items.Where(i => i.IsEnable == true).Select(i => i.ID).Contains(p.ItemID.Value)).ToList();
                                                foreach (Items item in packageData.Items.Where(i => i.IsEnable == true))
                                                {
                                                    PickProduct pick = pickList.FirstOrDefault(pk => pk.ItemID == item.ID);

                                                    if (pick != null)
                                                    {
                                                        pick.IsEnable = true;
                                                        pick.IsPicked = false;
                                                        pick.IsMail = false;
                                                        pick.QtyPicked = 0;
                                                        DataProcess.setPickProductData(pick, item);
                                                        PickProduct.Update(pick, pick.ID);
                                                    }
                                                    else
                                                    {
                                                        pick = new PickProduct() { IsEnable = true };
                                                        DataProcess.setPickProductData(pick, item);
                                                        PickProduct.Create(pick);
                                                    }
                                                }
                                                PickProduct.SaveChanges();
                                            }

                                            packageData.ProcessStatus = (int)EnumData.ProcessStatus.待出貨;
                                        }
                                        else
                                        {
                                            message = result.Message;
                                            packageData.ProcessStatus = (int)EnumData.ProcessStatus.訂單管理;
                                        }
                                    }
                                    else
                                    {
                                        message = "Payment status is different";
                                        packageData.Orders.StatusCode = (int)OrderStatusCode.OnHold;
                                        packageData.ProcessStatus = (int)EnumData.ProcessStatus.訂單管理;
                                    }

                                    Packages.Update(packageData, packageData.ID);
                                    Packages.SaveChanges();

                                    if (packageData.ProcessStatus.Equals((int)EnumData.ProcessStatus.待出貨))
                                        using (Hubs.ServerHub server = new Hubs.ServerHub())
                                            server.BroadcastOrderChange(packageData.OrderID.Value, EnumData.OrderChangeStatus.提交至待出貨區);
                                }
                                catch (Exception e)
                                {
                                    message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                                    packageData.ProcessStatus = (int)EnumData.ProcessStatus.訂單管理;

                                    if (!string.IsNullOrEmpty(package.WinitNo))
                                    {
                                        Winit_API winit = new Winit_API(package.Method.Carriers.CarrierAPI);
                                        Received received = winit.Void(package.WinitNo);
                                        package.WinitNo = null;
                                    }

                                    Packages.Update(packageData, packageData.ID);
                                    Packages.SaveChanges();
                                }

                                return message;
                            }, HttpContext.Session));
                        }
                    }

                    Packages.SaveChanges();

                    if (errorMessages.Any())
                        return Content(JsonConvert.SerializeObject(new { status = true, message = string.Join("\n", errorMessages) }), "appllication/json");
                }
                catch (DbEntityValidationException ex)
                {
                    var errorMessages = ex.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(x => x.ErrorMessage);
                    return Content(JsonConvert.SerializeObject(new { status = false, message = string.Join("\n", errorMessages) }), "appllication/json");
                }
                catch (Exception e)
                {
                    return Content(JsonConvert.SerializeObject(new { status = false, message = e.Message }), "appllication/json");
                }

                return Content(JsonConvert.SerializeObject(new { status = true, message = "訂單提交開始執行!" }), "appllication/json");
            }

            return Content(JsonConvert.SerializeObject(new { status = false, message = "沒有找到訂單!" }), "appllication/json");
        }

        public ActionResult SplitPackage(int PackageID, List<Dictionary<string, int>> splitItems)
        {
            if (!MyHelp.CheckAuth("order", "index", EnumData.AuthType.Edit))
            {
                return Content(JsonConvert.SerializeObject(new { status = false, message = "你沒有權限執行這個動作!" }), "appllication/json");
            }

            Packages = new GenericRepository<Packages>(db);
            Items = new GenericRepository<Items>(db);

            Packages package = Packages.Get(PackageID);
            if (package == null) return Content(JsonConvert.SerializeObject(new { status = false, message = "找不到包裏!" }), "appllication /json");

            try
            {
                TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
                ThreadTask threadTask = new ThreadTask(string.Format("訂單管理區 - 訂單【{0}】分批寄送", package.OrderID.ToString()));

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
                            MyHelp.Log("Packages", package.ID, "分批寄送", session);

                            if (!SCWS.Is_login) throw new Exception("SC is not login");

                            Order order = SCWS.Get_OrderData(package.OrderID.Value).Order;
                            if (order.ShippingStatus.Equals(OrderShippingStatus2.FullyShipped))
                            {
                                throw new Exception("Order is FullyShipped. Please unship the order first.");
                            }

                            Package oldPackage = order.Packages.First(p => p.ID == PackageID);
                            List<OrderItem> oldItems = order.Items.Where(i => i.PackageID == PackageID).ToList();
                            List<OrderPackage> orderPackages = SCWS.Get_OrderPackage_All(package.OrderID.Value).ToList();

                            Dictionary<string, int> itemQty = splitItems.First();
                            oldPackage.Qty = itemQty.Sum(item => item.Value);
                            SCWS.Update_PackageData(oldPackage);
                            Packages.Update(DataProcess.SetPackageData(package, oldPackage), oldPackage.ID);
                            foreach (OrderItem item in oldItems)
                            {
                                item.Qty = itemQty[item.ID.ToString()];
                                Items oldItem = package.Items.First(i => i.ID == item.ID);
                                oldItem.IsEnable = !item.Qty.Equals(0);
                                if (oldItem.IsEnable.Value)
                                {
                                    SCWS.Update_OrderItem(item);
                                }
                                else
                                {
                                    SCWS.Delete_Item(item.OrderID, item.ID);
                                    orderPackages.First(op => op.PackageID == oldPackage.ID && op.OrderItemID == item.ID).Qty = item.Qty;
                                }
                                Items.Update(DataProcess.SetItemData(oldItem, item), item.ID);
                            }

                            MapperConfiguration packageConfig = new MapperConfiguration(cfg => cfg.CreateMap<Package, Package>());
                            packageConfig.AssertConfigurationIsValid();//←證驗應對
                            IMapper packageMapper = packageConfig.CreateMapper();

                            MapperConfiguration itemConfig = new MapperConfiguration(cfg => cfg.CreateMap<OrderItem, OrderItem>());
                            itemConfig.AssertConfigurationIsValid();//←證驗應對
                            IMapper itemMapper = itemConfig.CreateMapper();

                            foreach (var qty in splitItems.Skip(1).ToArray())
                            {
                                Tuple<Package, List<OrderItem>> newData = Create_Package(oldPackage, oldItems, qty, packageMapper, itemMapper);
                                newData.Item1.Qty = newData.Item2.Sum(i => i.Qty);
                                Package newPackage = SCWS.Add_OrderNewPackage(newData.Item1);
                                Packages.Create(DataProcess.SetPackageData(new Packages() { IsEnable = true, ID = newPackage.ID }, newPackage));
                                foreach (OrderItem item in newData.Item2)
                                {
                                    OrderItem newItem = SCWS.Add_OrderNewItem(item);
                                    item.ID = newItem.ID;
                                    newItem.PackageID = item.PackageID = newPackage.ID;
                                    Items.Create(DataProcess.SetItemData(new Items() { IsEnable = true, ID = newItem.ID }, newItem));
                                    SCWS.Update_OrderItem(newItem);
                                }
                                newPackage.OrderItemID = newData.Item2.First().ID;
                                SCWS.Update_PackageData(newPackage);

                                orderPackages.Add(new OrderPackage()
                                {
                                    ID = -1,
                                    OrderID = newPackage.OrderID,
                                    PackageID = newPackage.ID,
                                    OrderItemID = newPackage.OrderItemID,
                                    OrderItemBundleItemID = newPackage.OrderItemBundleItemID,
                                    ProductID = newData.Item2.First().ProductID,
                                    Qty = newPackage.Qty
                                });
                            }

                            SCWS.Update_OrderPackage(orderPackages.ToArray());
                            Packages.SaveChanges();
                        }
                        catch (Exception e)
                        {
                            message = e.Message;
                        }

                        return message;
                    }, HttpContext.Session));
                }
            }
            catch (Exception e)
            {
                return Content(JsonConvert.SerializeObject(new { status = false, message = e.Message }), "appllication/json");
            }

            return Content(JsonConvert.SerializeObject(new { status = true, message = "訂單分批開始執行!" }), "appllication /json");
        }

        private Tuple<Package, List<OrderItem>> Create_Package(Package oldPackage, List<OrderItem> oldItems, Dictionary<string, int> itemQty, IMapper packageMapper, IMapper itemMapper)
        {
            Package newPackage = packageMapper.Map<Package>(oldPackage);
            List<OrderItem> newItems = new List<OrderItem>();

            foreach (var qty in itemQty.Where(qty => !qty.Value.Equals(0)).ToArray())
            {
                OrderItem item = itemMapper.Map<OrderItem>(oldItems.First(i => i.ID == int.Parse(qty.Key)));
                item.Qty = qty.Value;
                newItems.Add(item);
            }

            return Tuple.Create(newPackage, newItems);
        }

        [CheckSession]
        public ActionResult Waiting()
        {
            Warehouses = new GenericRepository<Warehouses>(db);

            ViewBag.warehouseList = Warehouses.GetAll(true).Where(w => w.IsEnable == true && w.IsSellable == true).OrderByDescending(w => w.IsDefault).OrderBy(w => w.ID).ToList();
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
                    MethodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && MethodIDs.Contains(m.ID.ToString())).ToList();
                }
            }

            string packageSelect = string.Format("SELECT * FROM Packages WHERE IsEnable = 1 AND ProcessStatus = {0}", (byte)EnumData.ProcessStatus.待出貨);
            string orderSelect = string.Format("SELECT * FROM Orders WHERE StatusCode = {0}", (int)OrderStatusCode.InProcess);
            string itemSelect = string.Format("SELECT * FROM Items WHERE IsEnable = 1 AND ShipFromWarehouseID = {0}", warehouseId);

            var methodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && !m.IsDirectLine).ToList();

            ObjectContext context = new ObjectContext("name=QDLogisticsEntities");
            var ProductList = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨)).ToList()
                .Join(methodList, p => p.ShippingMethod, m => m.ID, (p, m) => p)
                .Join(context.ExecuteStoreQuery<Orders>(orderSelect).ToList(), p => p.OrderID, o => o.OrderID, (package, order) => new { order, package })
                .OrderBy(data => data.order.TimeOfOrder).OrderByDescending(data => data.order.RushOrder)
                .Join(context.ExecuteStoreQuery<Items>(itemSelect).ToList(), op => op.package.ID, i => i.PackageID, (op, item) => op.package).Distinct().ToList();

            ViewBag.packageList = ProductList;
            ViewBag.methodList = MethodList;
            ViewBag.WCPScript = WebClientPrint.CreateScript(Url.Action("ProcessRequest", "WebClientPrintAPI", null, HttpContext.Request.Url.Scheme), Url.Action("PrintFile", "File", null, HttpContext.Request.Url.Scheme), HttpContext.Session.SessionID);
            ViewData["warehouseId"] = (int)Session["WarehouseID"];
            ViewData["adminId"] = (int)Session["AdminId"];
            ViewData["adminName"] = Session["AdminName"].ToString();
            ViewData["total"] = ProductList.Sum(p => p.Items.Where(i => i.IsEnable == true).Count());
            return View();
        }

        [CheckSession]
        public ActionResult Shipped()
        {
            Warehouses = new GenericRepository<Warehouses>(db);


            ViewBag.warehouseList = Warehouses.GetAll(true).Where(w => w.IsEnable == true && w.IsSellable == true).OrderByDescending(w => w.IsDefault).OrderBy(w => w.ID).ToList();
            return View();
        }

        public ActionResult TrackOrder()
        {
            Orders = new GenericRepository<Orders>(db);
            Packages = new GenericRepository<Packages>(db);
            Items = new GenericRepository<Items>(db);
            PickProduct = new GenericRepository<PickProduct>(db);
            Payments = new GenericRepository<Payments>(db);
            ShippingMethod = new GenericRepository<ShippingMethod>(db);

            try
            {
                TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;

                lock (factory)
                {
                    ThreadTask threadTask = new ThreadTask("已出貨區 - 追蹤已出貨訂單");
                    threadTask.AddWork(factory.StartNew(Session =>
                    {
                        threadTask.Start();

                        string message = "";

                        try
                        {
                            HttpSessionStateBase session = (HttpSessionStateBase)Session;
                            MyHelp.Log("Orders", null, "追蹤已出貨訂單", session);

                            TrackResult result;
                            TrackOrder track = new TrackOrder();
                            List<PickProduct> pickList = PickProduct.GetAll(true).Where(pick => pick.IsEnable == true && pick.IsPicked == true).OrderByDescending(pick => pick.PickUpDate).ToList();
                            List<Packages> packageList = pickList
                                .Join(Packages.GetAll(true).Where(p => p.IsEnable.Value && !p.DeliveryStatus.Equals((int)DeliveryStatusType.Delivered) && !p.ShippingMethod.Equals(0) && !string.IsNullOrEmpty(p.TrackingNumber)), pick => pick.PackageID, p => p.ID, (pick, p) => p).ToList()
                                .Join(ShippingMethod.GetAll(true).Where(m => m.IsEnable && !m.IsDirectLine), p => p.ShippingMethod, method => method.ID, (p, method) => p).ToList()
                                .Join(Payments.GetAll(true), p => p.OrderID, payment => payment.OrderID, (p, payment) => p).ToList();

                            foreach (var data in packageList.Join(Orders.GetAll(true), p => p.OrderID, o => o.OrderID, (p, o) => new { order = o, package = p }).ToList())
                            {
                                track.setOrder(data.order, data.package);
                                result = track.track();

                                data.package.PickUpDate = result.PickUpDate;
                                data.package.DeliveryDate = result.DeliveryDate;
                                data.package.DeliveryStatus = result.DeliveryStatus;
                                data.package.DeliveryNote = result.DeliveryNote;

                                Packages.Update(data.package, data.package.ID);
                            }

                            Packages.SaveChanges();
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

                lock (factory)
                {
                    ThreadTask threadTask = new ThreadTask("已出貨區 - 追蹤Winit訂單");
                    threadTask.AddWork(factory.StartNew(Session =>
                    {
                        threadTask.Start();

                        string message = "";

                        try
                        {
                            HttpSessionStateBase session = (HttpSessionStateBase)Session;
                            MyHelp.Log("Orders", null, "追蹤Winit訂單", session);

                            Winit_API winit = new Winit_API();

                            Received searchOrder = null;
                            List<outboundOrderListData> trackList = new List<outboundOrderListData>();
                            int page = 1, size = 100, total = 0;

                            do
                            {
                                searchOrder = winit.SearchOrder(page.ToString(), size.ToString(), 14);

                                if (searchOrder.code != "0") throw new Exception(searchOrder.msg);

                                outboundOrderListResult result = searchOrder.data.ToObject<outboundOrderListResult>();

                                if (total == 0) total = result.total;

                                trackList.AddRange(result.list.ToList());
                            } while (page++ * size < total);

                            if (trackList.Any())
                            {
                                var dataList = Orders.GetAll(true).Where(o => trackList.Any(track => track.sellerOrderNo.Equals(o.OrderID.ToString()))).ToList()
                                    .Join(Packages.GetAll(true).Where(p => p.IsEnable == true && p.DeliveryStatus != (int)DeliveryStatusType.Delivered).ToList(), o => o.OrderID, p => p.OrderID, (o, p) => new { order = o, package = p }).ToList();

                                if (dataList.Any())
                                {
                                    TrackResult result;
                                    TrackOrder track = new TrackOrder();

                                    List<int> uploadTracking = new List<int>();

                                    foreach (var data in dataList)
                                    {
                                        track.setOrder(data.order, data.package);
                                        outboundOrderListData trackData = trackList.First(t => t.sellerOrderNo == data.order.OrderID.ToString());
                                        result = track.track(trackData.warehouseId, trackData.documentNo, trackData.trackingNo);

                                        if (string.IsNullOrEmpty(data.package.TrackingNumber) && !string.IsNullOrEmpty(trackData.trackingNo))
                                        {
                                            data.package.TrackingNumber = trackData.trackingNo;
                                            data.package.ProcessStatus = (int)EnumData.ProcessStatus.已出貨;

                                            ShippingMethod method = ShippingMethod.GetAll(true).FirstOrDefault(m => m.MethodType == int.Parse(trackData.deliverywayId));
                                            if (method != null)
                                            {
                                                data.package.ShippingMethod = method.ID;
                                            }

                                            int warehouseId = winit.warehouseIDs.First(w => w.Value == trackData.warehouseId).Key;
                                            foreach (Items item in data.package.Items.ToArray())
                                            {
                                                item.ShipFromWarehouseID = warehouseId;
                                                Items.Update(item, item.ID);
                                            }

                                            uploadTracking.Add(data.package.ID);
                                        }

                                        data.package.WinitNo = trackData.documentNo;
                                        data.package.PickUpDate = result.PickUpDate;
                                        data.package.DeliveryDate = result.DeliveryDate;
                                        data.package.DeliveryStatus = result.DeliveryStatus;
                                        data.package.DeliveryNote = result.DeliveryNote;

                                        Packages.Update(data.package, data.package.ID);
                                    }

                                    Packages.SaveChanges();

                                    if (uploadTracking.Any())
                                    {
                                        SyncProcess Sync = new SyncProcess(session);
                                        List<string> error = new List<string>();

                                        foreach (int packageID in uploadTracking)
                                        {
                                            error.Add(Sync.Update_Tracking(Packages.Get(packageID)));
                                        }

                                        if (error.Any(e => !string.IsNullOrEmpty(e)))
                                        {
                                            message = string.Join("; ", error.Where(e => !string.IsNullOrEmpty(e)));
                                        }
                                    }
                                }
                            }
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

                HttpContextBase currentHttpContext = HttpContext;
                lock (factory)
                {
                    ThreadTask threadTask = new ThreadTask("追蹤Direct Line訂單");
                    threadTask.AddWork(factory.StartNew(Session =>
                    {
                        threadTask.Start();

                        db = new QDLogisticsEntities();
                        Box = new GenericRepository<Box>(db);
                        Label = new GenericRepository<DirectLineLabel>(db);

                        string message = "";
                        string sendMail = "dispatch-qd@hotmail.com";
                        string mailTitle;
                        string mailBody;
                        string[] receiveMails;
                        string[] ccMails = new string[] { "peter@qd.com.tw", "kelly@qd.com.tw", "demi@qd.com.tw" };
                        //string[] ccMails = new string[] { };

                        try
                        {
                            HttpSessionStateBase session = (HttpSessionStateBase)Session;
                            MyHelp.Log("Box", null, "追蹤Direct Line訂單開始", session);

                            List<Box> boxList = Box.GetAll(true).Where(b => b.IsEnable && b.BoxType.Equals((byte)EnumData.DirectLineBoxType.DirectLine) && b.ShippingStatus.Equals((byte)EnumData.DirectLineStatus.運輸中)).ToList();
                            if (boxList.Any())
                            {
                                int[] methodList = boxList.Select(b => b.FirstMileMethod).Distinct().ToArray();
                                var boxShippingMethodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && methodList.Contains(m.ID)).ToList(); ;
                                foreach (Box box in boxList)
                                {
                                    ShippingMethod method = boxShippingMethodList.First(m => m.ID.Equals(box.FirstMileMethod));

                                    TrackOrder TrackOrder = new TrackOrder();
                                    TrackResult boxResult = TrackOrder.Track(box, method.Carriers.CarrierAPI);
                                    if (boxResult.DeliveryStatus.Equals((int)DeliveryStatusType.Delivered))
                                    {
                                        MyHelp.Log("Box", box.BoxID, "寄送Box到貨通知", session);

                                        box.ShippingStatus = (byte)EnumData.DirectLineStatus.已到貨;
                                        Box.Update(box, box.BoxID);

                                        DirectLine directLine = db.DirectLine.AsNoTracking().First(d => d.IsEnable && d.ID.Equals(box.DirectLine));
                                        string[] address, boxLabel;
                                        switch (directLine.Abbreviation)
                                        {
                                            case "IDS":
                                                receiveMails = new string[] { "gloria.chiu@contin-global.com", "cherry.chen@contin-global.com", "TWCS@contin-global.com", "contincs@gmail.com", "shipping_qd@hotmail.com" };
                                                //receiveMails = new string[] { "qd.tuko@hotmail.com" };
                                                mailTitle = string.Format("TW018 至優網有限公司 First Mile 包裹 {0} {1} ({2} 件包裹) 已抵達", method.Carriers.Name, box.TrackingNumber, box.DirectLineLabel.Count(l => l.IsEnable));
                                                mailBody = "您好<br /><br />包裹已抵達:<br />{0}<br /><br />內容包含:<br />{1}<br /><br />請盡速處理並確認已經全數收到<br />謝謝!";
                                                address = new string[] { directLine.StreetLine1, directLine.StreetLine2, directLine.City, directLine.StateName, directLine.CountryName, directLine.PostalCode };
                                                boxLabel = box.DirectLineLabel.Where(l => l.IsEnable).Select(l => l.LabelID).ToArray();
                                                mailBody = string.Format(mailBody, string.Join(" ", address.Except(new string[] { "", null })), string.Join("<br />", boxLabel));
                                                bool IDS_Status = MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, null, null, false);
                                                if (!IDS_Status) MyHelp.Log("Box", box.BoxID, "寄送Box到貨通知失敗", session);
                                                break;
                                        }
                                    }
                                }
                                Box.SaveChanges();
                            }

                            List<DirectLineLabel> labelList = Label.GetAll(true).Where(l => l.IsEnable && !string.IsNullOrEmpty(l.BoxID) && l.Status.Equals((byte)EnumData.LabelStatus.正常)).ToList();
                            if (labelList.Any())
                            {
                                Packages = new GenericRepository<Packages>(db);

                                SC_WebService SCWS = new SC_WebService("tim@weypro.com", "timfromweypro");

                                List<DirectLineLabel> remindList = new List<DirectLineLabel>();
                                DateTime today = new TimeZoneConvert().ConvertDateTime(EnumData.TimeZone.EST);

                                foreach (DirectLineLabel label in labelList)
                                {
                                    Packages package = label.Packages.First();

                                    Order orderData = SCWS.Get_OrderData(package.OrderID.Value).Order;

                                    if (package.Orders.StatusCode.Value.Equals((int)orderData.StatusCode) && package.Orders.PaymentStatus.Value.Equals((int)orderData.PaymentStatus))
                                    {

                                        if (string.IsNullOrEmpty(package.TrackingNumber))
                                        {
                                            CarrierAPI api = package.Method.Carriers.CarrierAPI;
                                            switch (api.Type)
                                            {
                                                case (byte)EnumData.CarrierType.IDS:
                                                    IDS_API IDS = new IDS_API(api);
                                                    var IDS_Result = IDS.GetTrackingNumber(package);
                                                    if (IDS_Result.trackingnumber.Any(t => t.First().Equals(package.OrderID.ToString())))
                                                    {
                                                        package.TrackingNumber = IDS_Result.trackingnumber.Last(t => t.First().Equals(package.OrderID.ToString()))[1];
                                                        Packages.Update(package, package.ID);
                                                        Packages.SaveChanges();
                                                    }
                                                    break;
                                            }
                                        }

                                        DateTime paymentDate = package.Orders.Payments.Any() ? package.Orders.Payments.First().AuditDate.Value : package.Orders.TimeOfOrder.Value;

                                        int checkDays = package.Items.First(i => i.IsEnable.Value).ShipWarehouses.WarehouseType.Value.Equals((int)WarehouseTypeType.DropShip) ? 2 : 3;

                                        paymentDate = paymentDate.AddDays(checkDays);
                                        if (paymentDate.DayOfWeek == DayOfWeek.Saturday) paymentDate = paymentDate.AddDays(2);
                                        if (paymentDate.DayOfWeek == DayOfWeek.Sunday) paymentDate = paymentDate.AddDays(1);

                                        if (label.Box.ShippingStatus.Equals((byte)EnumData.DirectLineStatus.已到貨) && DateTime.Compare(today, paymentDate) > 0)
                                        {
                                            if (string.IsNullOrEmpty(package.TrackingNumber))
                                            {
                                                if (today.Hour >= paymentDate.Hour && today.Hour <= paymentDate.Hour + 2) remindList.Add(label);
                                            }
                                            else
                                            {
                                                ThreadTask syncTask = new ThreadTask(string.Format("Direct Line 訂單【{0}】SC更新", package.OrderID));
                                                syncTask.AddWork(factory.StartNew(() =>
                                                {
                                                    syncTask.Start();
                                                    SyncProcess sync = new SyncProcess(session);
                                                    return sync.Update_Tracking(package);
                                                }));

                                                using (CaseLog CaseLog = new CaseLog(package, session, currentHttpContext))
                                                {
                                                    if (CaseLog.CaseExit(EnumData.CaseEventType.UpdateTracking))
                                                    {
                                                        CaseLog.TrackingResponse();
                                                    }
                                                }

                                                label.Status = (byte)EnumData.LabelStatus.完成;
                                                Label.Update(label, label.LabelID);
                                                Label.SaveChanges();
                                            }
                                        }
                                    }
                                    else
                                    {
                                        MyHelp.Log("Box ", label.BoxID, string.Format("Direct Line訂單【{0}】資料狀態異常", package.OrderID.Value), session);

                                        package.Orders.StatusCode = (int)orderData.StatusCode;
                                        package.Orders.PaymentStatus = (int)orderData.PaymentStatus;
                                        label.Status = (byte)EnumData.LabelStatus.鎖定中;

                                        if (orderData.StatusCode.Equals((int)OrderStatusCode.Canceled))
                                        {
                                            using (CaseLog CaseLog = new CaseLog(package, session, currentHttpContext))
                                            {
                                                CaseLog.SendCancelMail();
                                            }

                                            label.Status = (byte)EnumData.LabelStatus.作廢;
                                        }

                                        Label.Update(label, label.LabelID);
                                        Packages.Update(package, package.ID);
                                        Packages.SaveChanges();
                                    }
                                }

                                if (remindList.Any())
                                {
                                    MyHelp.Log("Box", null, "開始寄送Direct Line Tracking通知", session);

                                    using (CaseLog CaseLog = new CaseLog(session, currentHttpContext))
                                    {
                                        foreach (var group in remindList.GroupBy(l => l.BoxID))
                                        {
                                            Box box = db.Box.AsNoTracking().First(b => b.IsEnable && b.BoxID.Equals(group.Key));
                                            DirectLine directLine = db.DirectLine.AsNoTracking().First(d => d.IsEnable && d.ID.Equals(box.DirectLine));

                                            CaseLog.SendTrackingMail(directLine.Abbreviation, group.ToList());
                                        }
                                    }

                                    MyHelp.Log("Box", null, "完成寄送Direct Line Tracking通知", session);
                                }
                            }

                            MyHelp.Log("Box", null, "追蹤Direct Line訂單結束", session);
                        }
                        catch (Exception e)
                        {
                            message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                        }

                        return message;
                    }, Session));
                }

                lock (factory)
                {
                    ThreadTask threadTask = new ThreadTask("檢查 Case Event 進度");
                    threadTask.AddWork(factory.StartNew(Session =>
                    {
                        threadTask.Start();

                        db = new QDLogisticsEntities();
                        IRepository<CaseEvent> CaseEvent = new GenericRepository<CaseEvent>(db);

                        string message = "";

                        try
                        {
                            HttpSessionStateBase session = (HttpSessionStateBase)Session;
                            MyHelp.Log("CaseEvent", null, "開始檢查 Case Event 進度", session);

                            List<byte> CaseType = new List<byte>() { (byte)EnumData.CaseEventType.CancelShipment, (byte)EnumData.CaseEventType.ChangeShippingMethod };
                            List<CaseEvent> CaseEventList = db.CaseEvent.Where(c => CaseType.Contains(c.Type) && c.Request.Equals((byte)EnumData.CaseEventRequest.None) && c.Status.Equals((byte)EnumData.CaseEventStatus.Open)).ToList();
                            if (CaseEventList.Any())
                            {
                                using (CaseLog CaseLog = new CaseLog(session, currentHttpContext))
                                {
                                    DateTime today = DateTime.UtcNow;
                                    foreach (CaseEvent eventData in CaseEventList)
                                    {
                                        DateTime RequestDate = MyHelp.SkipWeekend(eventData.Request_at.Value.AddDays(2));
                                        //DateTime CreateDate = MyHelp.SkipWeekend(MyHelp.SkipWeekend(eventData.Create_at, 2, 2).AddDays(2));
                                        if (RequestDate.CompareTo(today) <= 0)
                                        {

                                            CaseLog.OrderInit(eventData.Packages);
                                            switch (eventData.Type)
                                            {
                                                case (byte)EnumData.CaseEventType.CancelShipment:
                                                    CaseLog.SendCancelMail();
                                                    break;

                                                case (byte)EnumData.CaseEventType.ChangeShippingMethod:
                                                    CaseLog.SendChangeShippingMethodMail(eventData.MethodID, eventData.NewLabelID);
                                                    break;
                                            }
                                        }
                                    }
                                    CaseEvent.SaveChanges();
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                        }

                        return message;
                    }, Session));
                }
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(x => x.ErrorMessage);
                return Content(JsonConvert.SerializeObject(new { status = false, message = string.Join("; ", errorMessages) }), "appllication/json");
            }
            catch (Exception e)
            {
                return Content(JsonConvert.SerializeObject(new { status = false, message = e.Message }), "appllication/json");
            }

            return Content(JsonConvert.SerializeObject(new { status = true, message = "提單追踨開始執行!" }), "appllication/json");
        }

        private void WebServiceInit(HttpSessionStateBase session)
        {
            OS_sellerCloud = new SCServiceSoapClient();
            OS_sellerCloud.InnerChannel.OperationTimeout = new TimeSpan(0, 15, 0);
            OS_authHeader = new AuthHeader();
            OS_options = new ServiceOptions();

            OCS_sellerCloud = new OrderCreationService.OrderCreationServiceSoapClient();
            OCS_sellerCloud.InnerChannel.OperationTimeout = new TimeSpan(0, 15, 0);
            OCS_authHeader = new OrderCreationService.AuthHeader();

            OS_authHeader.UserName = OCS_authHeader.UserName = session["ApiUserName"].ToString();
            OS_authHeader.Password = OCS_authHeader.Password = session["ApiPassword"].ToString();

            SyncOn = DateTime.UtcNow;
        }

        public ActionResult SendMailToCarrier()
        {
            PickProduct = new GenericRepository<PickProduct>(db);
            ShippingMethod = new GenericRepository<ShippingMethod>(db);

            List<PickProduct> pickList = db.PickProduct.AsNoTracking().Where(p => p.IsEnable && p.IsPicked && !p.IsMail)
                .Join(db.Warehouses.AsNoTracking().Where(w => w.IsEnable.Value && !w.WarehouseType.Value.Equals((int)WarehouseTypeType.DropShip)), pick => pick.WarehouseID, w => w.ID, (pick, w) => pick).ToList();
            if (pickList.Any())
            {
                int[] packageIDs = pickList.Select(p => p.PackageID.Value).ToArray();
                List<Packages> packageList = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.已出貨) && packageIDs.Contains(p.ID)).ToList();

                int[] methodIDs = packageList.Where(p => p.ShippingMethod != null).Select(p => p.ShippingMethod.Value).Distinct().ToArray();
                var carrierList = ShippingMethod.GetAll(true).Where(m => methodIDs.Contains(m.ID)).Distinct().ToDictionary(m => m.ID, m => m.Carriers.Name);
                var groupList = packageList.GroupBy(p => p.ShippingMethod != null && carrierList.ContainsKey(p.ShippingMethod.Value) ? carrierList[p.ShippingMethod.Value] : "", p => p)
                    .ToDictionary(g => g.Key, g => g.ToList());

                DateTime now = new TimeZoneConvert().ConvertDateTime(EnumData.TimeZone.TST);
                DateTime noon = new DateTime(now.Year, now.Month, now.Day, 12, 0, 0);
                DateTime evening = new DateTime(now.Year, now.Month, now.Day, 18, 0, 0);

                string basePath = HostingEnvironment.MapPath("~/FileUploads");
                string filePath;

                string sendMail = "dispatch-qd@hotmail.com";
                string mailTitle;
                string mailBody;
                string[] receiveMails;
                string[] ccMails = new string[] { "peter@qd.com.tw", "kelly@qd.com.tw", "demi@qd.com.tw" };
                //string[] ccMails = new string[] { };

                TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
                foreach (var serviceCode in groupList.Keys)
                {
                    switch (serviceCode)
                    {
                        case "DHL":
                            lock (factory)
                            {
                                ThreadTask DHL_MailTask = new ThreadTask("寄送DHL出口報關資料");
                                DHL_MailTask.AddWork(factory.StartNew(Session =>
                                {
                                    DHL_MailTask.Start();

                                    string message = "";
                                    MyHelp.Log("PickProduct", null, "寄送DHL出口報單");

                                    try
                                    {
                                        string[] ProductIDs = groupList[serviceCode].SelectMany(p => p.Items.Where(i => i.IsEnable.Value).Select(i => i.ProductID)).Distinct().ToArray();
                                        Dictionary<string, string> skuList = db.Skus.AsNoTracking().Where(sku => sku.IsEnable.Value && ProductIDs.Contains(sku.Sku)).ToDictionary(sku => sku.Sku, sku => sku.PurchaseInvoice);

                                        XLWorkbook DHL_workbook = new XLWorkbook();
                                        JArray jObjects = new JArray();
                                        List<string> DHLFile = new List<string>();

                                        foreach (Packages package in groupList[serviceCode])
                                        {
                                            Orders order = package.Orders;
                                            Addresses address = order.Addresses;
                                            string name = string.Join(" ", new string[] { address.FirstName, address.MiddleInitial, address.LastName });

                                            List<Items> itemList = package.Items.Where(i => i.IsEnable.Value).ToList();
                                            foreach (Items item in itemList)
                                            {
                                                JObject jo = new JObject();
                                                jo.Add("1", package.ExportMethod.Value.Equals(0) ? "G3" : "G5");
                                                jo.Add("2", package.ExportMethod.Value.Equals(0) ? "81" : "02");
                                                jo.Add("3", skuList[item.ProductID]);
                                                jo.Add("4", name);
                                                jo.Add("5", string.Join(" - ", new string[] { item.Skus.ProductType.ProductTypeName, item.Skus.ProductName }));
                                                jo.Add("6", package.TrackingNumber);
                                                jo.Add("7", Enum.GetName(typeof(CurrencyCodeType), order.OrderCurrencyCode));
                                                jo.Add("8", (item.DeclaredValue * item.Qty.Value).ToString("N"));
                                                jo.Add("9", address.StreetLine1);
                                                jo.Add("10", address.City);
                                                jo.Add("11", address.StateName);
                                                jo.Add("12", address.PostalCode);
                                                jo.Add("13", address.CountryName);
                                                jo.Add("14", address.PhoneNumber);
                                                if (item.Qty > 1) jo.Add("15", item.Qty);
                                                jObjects.Add(jo);
                                            }
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

                                        filePath = Path.Combine(basePath, "mail", now.ToString("yyyy/MM/dd"));
                                        if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);

                                        string fileName = string.Format("DHL 出口報關表格 第{0}批.xlsx", DateTime.Compare(now, noon.AddHours(3)) <= 0 ? "1" : "2");
                                        DHL_workbook.SaveAs(Path.Combine(filePath, fileName));

                                        receiveMails = new string[] { "twtxwisa@dhl.com" };
                                        //receiveMails = new string[] { "qd.tuko@hotmail.com" };
                                        mailTitle = string.Format("至優網 {0} 第{1}批 {2}筆提單 正式出口報關資料", now.ToString("yyyy-MM-dd"), DateTime.Compare(now, noon.AddHours(3)) <= 0 ? "1" : "2", groupList[serviceCode].Count());

                                        mailBody = string.Join("<br />", groupList[serviceCode].Select(p => p.TrackingNumber).ToArray());

                                        DHLFile.Add(Path.Combine(filePath, fileName));
                                        bool DHL_Status = MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, DHLFile.ToArray(), null, false);

                                        if (DHL_Status)
                                        {
                                            MyHelp.Log("PickProduct", null, mailTitle);
                                            foreach (PickProduct pick in pickList.Where(pick => groupList[serviceCode].Select(p => p.ID).ToArray().Contains(pick.PackageID.Value)).ToList())
                                            {
                                                pick.IsMail = true;
                                                PickProduct.Update(pick, pick.ID);
                                            }

                                            PickProduct.SaveChanges();
                                        }
                                        else
                                        {
                                            message = string.Format("{0} 寄送失敗", mailTitle);
                                            MyHelp.Log("PickProduct", null, message);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                                    }

                                    return message;
                                }, Session));
                            }
                            break;

                        case "FedEx":
                            lock (factory)
                            {
                                ThreadTask FedEx_MailTask = new ThreadTask("寄送FedEx出口報關資料");
                                FedEx_MailTask.AddWork(factory.StartNew(Session =>
                                {
                                    FedEx_MailTask.Start();

                                    string message = "";
                                    MyHelp.Log("PickProduct", null, "寄送FedEx出口報單");

                                    try
                                    {
                                        List<Tuple<Stream, string>> FedExFile = new List<Tuple<Stream, string>>();
                                        foreach (Packages package in groupList[serviceCode])
                                        {
                                            filePath = string.Join("", package.FilePath.Skip(package.FilePath.IndexOf("export")));

                                            decimal declaredTotal = package.DeclaredTotal;
                                            List<Items> itemList = package.Items.Where(i => i.IsEnable.Value).ToList();
                                            int lastItemID = itemList.Last().ID;

                                            using (FileStream fsIn = new FileStream(Path.Combine(basePath, filePath, "Invoice.xls"), FileMode.Open))
                                            {
                                                HSSFWorkbook FedEx_workbook = new HSSFWorkbook(fsIn);
                                                fsIn.Close();

                                                ISheet FedEx_sheet = FedEx_workbook.GetSheetAt(0);

                                                int rowIndex = 26;
                                                foreach (Items item in itemList)
                                                {
                                                    Country country = MyHelp.GetCountries().FirstOrDefault(c => c.ID == item.Skus.Origin);
                                                    FedEx_sheet.GetRow(rowIndex).GetCell(1).SetCellValue(country.OriginName);
                                                    string productName = item.Skus.ProductType.ProductTypeName + " - " + item.Skus.ProductName;
                                                    FedEx_sheet.GetRow(rowIndex).GetCell(5).SetCellValue(productName);
                                                    FedEx_sheet.GetRow(rowIndex).GetCell(8).SetCellValue(item.Qty.Value);
                                                    FedEx_sheet.GetRow(rowIndex).GetCell(9).SetCellValue("pieces");
                                                    FedEx_sheet.GetRow(rowIndex).GetCell(10).SetCellValue(item.Qty * ((double)item.Skus.Weight / 1000) + "kg");
                                                    FedEx_sheet.GetRow(rowIndex).GetCell(11).SetCellValue(item.DeclaredValue.ToString("N"));
                                                    FedEx_sheet.GetRow(rowIndex).GetCell(16).SetCellValue((item.DeclaredValue * item.Qty.Value).ToString("N"));
                                                    FedEx_sheet.GetRow(rowIndex).HeightInPoints = (productName.Length / 30 + 1) * FedEx_sheet.DefaultRowHeight / 20;
                                                    FedEx_sheet.GetRow(rowIndex++).RowStyle.VerticalAlignment = VerticalAlignment.Center;
                                                }

                                                using (FileStream fsOut = new FileStream(Path.Combine(basePath, filePath, "Invoice2.xls"), FileMode.Create))
                                                {
                                                    FedEx_workbook.Write(fsOut);
                                                    fsOut.Close();
                                                }
                                            }

                                            var memoryStream = new MemoryStream();

                                            using (var file = new ZipFile())
                                            {
                                                file.AddFile(Path.Combine(basePath, filePath, "Invoice2.xls"), "");
                                                file.AddFile(Path.Combine(basePath, filePath, "CheckList.xlsx"), "");
                                                file.AddFile(Path.Combine(basePath, "sample", "Fedex_Recognizances.pdf"), "");

                                                file.Save(memoryStream);
                                            }

                                            memoryStream.Seek(0, SeekOrigin.Begin);
                                            FedExFile.Add(new Tuple<Stream, string>(memoryStream, package.TrackingNumber + ".zip"));
                                        }

                                        receiveMails = new string[] { "edd@fedex.com" };
                                        //receiveMails = new string[] { "qd.tuko@hotmail.com" };
                                        mailTitle = string.Format("至優網 {0} 第{1}批 {2}筆提單 正式出口報關資料", now.ToString("yyyy-MM-dd"), DateTime.Compare(now, noon.AddHours(3)) <= 0 ? "1" : "2", groupList[serviceCode].Count());

                                        bool FedEx_Status = MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, "", true, null, FedExFile, false);

                                        if (FedEx_Status)
                                        {
                                            MyHelp.Log("PickProduct", null, mailTitle);
                                            foreach (PickProduct pick in pickList.Where(pick => groupList[serviceCode].Select(p => p.ID).ToArray().Contains(pick.PackageID.Value)).ToList())
                                            {
                                                pick.IsMail = true;
                                                PickProduct.Update(pick, pick.ID);
                                            }

                                            PickProduct.SaveChanges();
                                        }
                                        else
                                        {
                                            MyHelp.Log("PickProduct", null, string.Format("{0} 寄送失敗", mailTitle));
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                                    }

                                    return message;
                                }, Session));
                            }
                            break;

                        default:
                            foreach (PickProduct pick in pickList.Where(pick => groupList[serviceCode].Select(p => p.ID).ToArray().Contains(pick.PackageID.Value)).ToList())
                            {
                                pick.IsMail = true;
                                PickProduct.Update(pick, pick.ID);
                            }

                            PickProduct.SaveChanges();
                            break;
                    }
                }
            }

            return Content("");
        }
    }
}