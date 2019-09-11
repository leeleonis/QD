using ClosedXML.Excel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QDLogistics.Commons;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;
using QDLogistics.OrderService;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;

namespace QDLogistics.Controllers
{
    public class ApiController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<Packages> Packages;
        private IRepository<PickProduct> PickProduct;
        private IRepository<SerialNumbers> SerialNumbers;
        private IRepository<Warehouses> Warehouses;
        private IRepository<AdminUsers> AdminUsers;

        public ApiController()
        {
            db = new QDLogisticsEntities();
            Packages = new GenericRepository<Packages>(db);
            PickProduct = new GenericRepository<PickProduct>(db);
            SerialNumbers = new GenericRepository<SerialNumbers>(db);
            AdminUsers = new GenericRepository<AdminUsers>(db);
        }

        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login([Bind(Include = "UserName,Password,WarehouseID,GUID")] ReceiveData data)
        {
            string userSelect = string.Format("SELECT * FROM AdminUsers WHERE IsEnable = 1 AND IsVisible = 1 AND Account = '{0}'", data.UserName);

            ObjectContext context = new ObjectContext("name=QDLogisticsEntities");
            AdminUsers user = context.ExecuteStoreQuery<AdminUsers>(userSelect).FirstOrDefault();
            string saltPassword = MyHelp.Encrypt(data.Password);

            ApiResult result = Check_User(user, saltPassword, data.WarehouseID);
            if (result.status)
            {
                List<LoginInfo> LoginList = System.Web.HttpContext.Current.Application.Get("WebAppLogin") as List<LoginInfo>;

                string ApiUserName = string.IsNullOrEmpty(user.ApiUserName) ? data.UserName : user.ApiUserName;
                string ApiPassword = string.IsNullOrEmpty(user.ApiPassword) ? data.Password : user.ApiPassword;

                LoginList.RemoveAll(info => DateTime.Compare(info.Login_at, DateTime.UtcNow.AddDays(-1)) < 0);
                LoginList.Add(new LoginInfo(user.Id, user.Name, data.WarehouseID, data.GUID, ApiUserName, ApiPassword));

                System.Web.HttpContext.Current.Application.Set("WebAppLogin", LoginList);
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        private ApiResult Check_User(AdminUsers user, string saltPassword, int warehouseID)
        {
            ApiResult result = new ApiResult();

            if (user == null) return result.Error("找不到此使用者!");

            if (!string.Equals(user.Password, saltPassword)) return result.Error("密碼不正確!");

            List<int> userWarehouses = !string.IsNullOrEmpty(user.Warehouse) ? JsonConvert.DeserializeObject<List<int>>(user.Warehouse) : new List<int>();
            if (!userWarehouses.Any(w => w == warehouseID)) return result.Error("出貨倉權限不足!");

            result.data = new { AdminID = user.Id, AdminName = user.Name };

            return result;
        }

        public ActionResult Logout([Bind(Include = "GUID")] ReceiveData data)
        {
            ApiResult result = new ApiResult();

            try
            {
                LoginInfo userInfo = GetUser(data.GUID);

                List<LoginInfo> LoginList = System.Web.HttpContext.Current.Application.Get("WebAppLogin") as List<LoginInfo>;
                LoginList.Remove(userInfo);
                System.Web.HttpContext.Current.Application.Set("WebAppLogin", LoginList);
            }
            catch (Exception e)
            {
                result.Error(e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message.Trim() : e.Message.Trim());
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Sync_ProductSerial([Bind(Include = "GUID")] ReceiveData data)
        {
            ApiResult result = new ApiResult();

            try
            {
                LoginInfo userInfo = GetUser(data.GUID);

                string[] productIDs = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((int)EnumData.ProcessStatus.待出貨)).ToList()
                .Join(db.Items.AsNoTracking().Where(i => i.IsEnable.Value), p => p.ID, i => i.PackageID, (p, i) => i.ProductID).Distinct().ToArray();

                if (productIDs.Length == 0) return Json(result.Error("沒有需要同步的產品!"), JsonRequestBehavior.AllowGet);

                TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
                ThreadTask threadTask = new ThreadTask("產品序號同步工作", Session);

                lock (factory)
                {
                    threadTask.AddWork(factory.StartNew(Session =>
                    {
                        threadTask.Start();

                        string message = "";
                        HttpSessionStateBase session = (HttpSessionStateBase)Session;

                        try
                        {
                            SyncProcess Sync = new SyncProcess(session);
                            message = Sync.Sync_PurchaseItem(productIDs);
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
                    }, Session));
                }

                result.data = new { taskID = threadTask.ID };
            }
            catch (Exception e)
            {
                result.Error(e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message.Trim() : e.Message.Trim());
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Check_TaskStatus([Bind(Include = "TaskID,GUID")] ReceiveData data)
        {
            ApiResult result = new ApiResult();

            using (IRepository<Models.TaskScheduler> TaskScheduler = new GenericRepository<Models.TaskScheduler>(db))
            {
                try
                {
                    LoginInfo userInfo = GetUser(data.GUID);

                    Models.TaskScheduler task = TaskScheduler.Get(data.TaskID);

                    if (task == null) return Json(result.Error("Not found task!"), JsonRequestBehavior.AllowGet);

                    result.data = new { isFinished = task.Status >= 2, statusText = Enum.GetName(typeof(EnumData.TaskStatus), task.Status) };
                }
                catch (Exception e)
                {
                    result.Error(e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message.Trim() : e.Message.Trim());
                }
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Product_List([Bind(Include = "GUID")] ReceiveData data)
        {
            ApiResult result = new ApiResult();

            try
            {
                LoginInfo userInfo = GetUser(data.GUID);

                string packageSelect = string.Format("SELECT * FROM Packages WHERE IsEnable = 1 AND ProcessStatus = {0}", (byte)EnumData.ProcessStatus.待出貨);
                string orderSelect = string.Format("SELECT * FROM Orders WHERE StatusCode = {0}", (int)OrderStatusCode.InProcess);
                string itemSelect = string.Format("SELECT * FROM Items WHERE IsEnable = 1 AND ShipFromWarehouseID = {0}", userInfo.WarehouseID);

                var methodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && !m.IsDirectLine).ToList();

                ObjectContext context = new ObjectContext("name=QDLogisticsEntities");
                var ProductList = context.ExecuteStoreQuery<Packages>(packageSelect).ToList().Join(methodList, p => p.ShippingMethod, m => m.ID, (p, m) => p)
                    .Join(context.ExecuteStoreQuery<Orders>(orderSelect).ToList(), p => p.OrderID, o => o.OrderID, (package, order) => new { order, package })
                    .Join(context.ExecuteStoreQuery<Items>(itemSelect).ToList(), op => op.package.ID, i => i.PackageID, (op, item) => new { op.order, op.package, item }).Distinct()
                    .OrderBy(oData => oData.order.TimeOfOrder).OrderByDescending(oData => oData.order.RushOrder).ToList();

                string[] ProductIDs = ProductList.Select(oData => oData.item.ProductID).Distinct().ToArray();
                Dictionary<string, Skus> SkuList = db.Skus.AsNoTracking().Where(s => s.IsEnable.Value && ProductIDs.Contains(s.Sku)).ToDictionary(s => s.Sku, s => s);
                Dictionary<int, string> MethodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable).ToDictionary(m => m.ID, m => m.Name);

                List<StockKeepingUnit.SkuData> SkuData = new List<StockKeepingUnit.SkuData>();
                using (StockKeepingUnit stock = new StockKeepingUnit())
                {
                    SkuData = stock.GetSkuData(ProductIDs);
                }

                result.data = ProductList.Select(p => new
                {
                    OrderID = p.item.OrderID,
                    SKU = p.item.SKU,
                    UPC = SkuList[p.item.ProductID].UPC,
                    SkuList[p.item.ProductID].ProductName,
                    Qty = p.item.Qty,
                    Weight = (SkuData.Any(s => s.Sku.Equals(p.item.ProductID)) ? SkuData.First(s => s.Sku.Equals(p.item.ProductID)).Weight : SkuList[p.item.ProductID].ShippingWeight),
                    ShippingMethod = MethodList[p.package.ShippingMethod.Value],
                    Country = p.order.ShippingCountry,
                    Export = Enum.GetName(typeof(EnumData.Export), p.package.Export),
                    Status = p.order.RushOrder.Value ? "Rush" : "Normal",
                    Comment = p.package.Comment
                }).ToArray();
            }
            catch (Exception e)
            {
                result.Error(e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message.Trim() : e.Message.Trim());
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Product_PickUp([Bind(Include = "CarrierID,GUID")] ReceiveData data)
        {
            ApiResult result = new ApiResult();

            try
            {
                LoginInfo userInfo = GetUser(data.GUID);

                string packageSelect = string.Format("SELECT * FROM Packages WHERE IsEnable = 1 AND ProcessStatus = {0}", (byte)EnumData.ProcessStatus.待出貨);
                string orderSelect = string.Format("SELECT * FROM Orders WHERE StatusCode = {0}", (int)OrderStatusCode.InProcess);
                string itemSelect = string.Format("SELECT * FROM Items WHERE IsEnable = 1 AND ShipFromWarehouseID = {0}", userInfo.WarehouseID);
                string pickSelect = string.Format("SELECT * FROM PickProduct WHERE IsEnable = 1 AND IsPicked = 0 AND WarehouseID = {0}", userInfo.WarehouseID);

                var PackageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨));
                if (!data.CarrierID.Equals(0)) PackageFilter = PackageFilter.Where(p => p.ShippingMethod.Value.Equals(data.CarrierID));

                var methodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && !m.IsDirectLine).ToList();

                ObjectContext context = new ObjectContext("name=QDLogisticsEntities");
                var ProductList = PackageFilter.ToList().Join(methodList, p => p.ShippingMethod, m => m.ID, (p, m) => p)
                    .Join(context.ExecuteStoreQuery<Items>(itemSelect).ToList(), p => p.ID, i => i.PackageID, (p, i) => p)
                    .Join(context.ExecuteStoreQuery<Orders>(orderSelect).ToList(), p => p.OrderID, o => o.OrderID, (package, order) => new { order, package })
                    .Join(context.ExecuteStoreQuery<PickProduct>(pickSelect).ToList(), op => op.package.ID, pk => pk.PackageID, (op, pick) => new { op.order, op.package, pick = pick.SetCountry(op.order.ShippingCountry) }).Distinct()
                    .OrderBy(oData => oData.package.Qty).OrderBy(oData => oData.order.TimeOfOrder).OrderByDescending(oData => oData.order.RushOrder).ToList();

                if (ProductList.Any())
                {
                    string[] productIDs = ProductList.Select(p => p.pick.ProductID).Distinct().ToArray();
                    Dictionary<string, bool> skuList = db.Skus.AsNoTracking().Where(s => s.IsEnable.Value && productIDs.Contains(s.Sku)).ToDictionary(s => s.Sku, s => s.Battery.HasValue ? s.Battery.Value : false);
                    var productList = ProductList.Select(p => p.pick.SetBattery(skuList[p.pick.ProductID]).SetTracking(p.package.TrackingNumber))
                        .GroupBy(p => p.ProductID).ToDictionary(group => group.Key.ToString(), group => group.ToDictionary(p => p.ItemID.ToString()));

                    var pickupCompare = DateTime.UtcNow.AddHours(-12);
                    var shippedItem = db.PickProduct.AsNoTracking().Where(p => p.IsEnable && productIDs.Contains(p.ProductID) && p.PickUpDate.Value.CompareTo(pickupCompare) >= 0).Select(p => p.ItemID).ToArray();
                    List<SerialNumbers> itemSerials = db.SerialNumbers.AsNoTracking().Where(s => productIDs.Contains(s.ProductID) && shippedItem.Contains(s.OrderItemID)).ToList();
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

                    var fileList = ProductList.Select(p => p.package).Distinct().ToDictionary(p => p.ID.ToString(), p => GetFileData(p));

                    result.data = new { productList, groupList, serialList, fileList };
                }
            }
            catch (Exception e)
            {
                result.Error(e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message.Trim() : e.Message.Trim());
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        private object GetFileData(Packages package)
        {
            string[] fileName = new string[2];
            string[] filePath = new string[2];
            int[] amount = new int[] { 0, 0 };

            string baseURL = string.Format("{0}://{1}", HttpContext.Request.Url.Scheme, HttpContext.Request.Url.Host);
            string path = string.Join("", package.FilePath.Skip(package.FilePath.IndexOf("export"))).Replace(@"\", @"/");

            /***** 提貨單 *****/
            fileName[0] = "AirWaybill.pdf";
            filePath[0] = string.Format("{0}/FileUploads/{1}/{2}", baseURL, path, fileName[0]);
            /***** 提貨單 *****/

            switch (package.Method.Carriers.CarrierAPI.Type)
            {
                case (byte)EnumData.CarrierType.DHL:
                    bool DHL_pdf = !System.IO.File.Exists(Path.Combine(HostingEnvironment.MapPath("~/FileUploads"), string.Join("", package.FilePath.Skip(package.FilePath.IndexOf("export"))), "Invoice.xls"));

                    /***** 商業發票 *****/
                    fileName[1] = DHL_pdf ? "Invoice.pdf" : "Invoice.xls";
                    filePath[1] = string.Format("{0}/FileUploads/{1}/{2}", baseURL, path, fileName[1]);
                    /***** 商業發票 *****/

                    amount = new int[] { 2, DHL_pdf ? 0 : 2 };
                    break;
                case (byte)EnumData.CarrierType.FedEx:
                    /***** 商業發票 *****/
                    fileName[1] = "Invoice.xls";
                    filePath[1] = string.Format("{0}/FileUploads/{1}/{2}", baseURL, path, fileName[1]);
                    /***** 商業發票 *****/

                    amount = new int[] { 1, 4 };
                    break;
                case (int)EnumData.CarrierType.Sendle:
                    amount = new int[] { 1, 0 };
                    break;
                case (int)EnumData.CarrierType.USPS:
                    break;
            }

            // 取得熱感應印表機名稱
            string printerName = package.Method.PrinterName;
            int carrierID = package.ShippingMethod.Value;

            return new { fileName, filePath, amount, printerName, carrierID };
        }

        public ActionResult Print_PickUpList([Bind(Include = "GUID")] ReceiveData data)
        {
            ApiResult result = new ApiResult();

            try
            {
                LoginInfo userInfo = GetUser(data.GUID);

                string packageSelect = string.Format("SELECT * FROM Packages WHERE IsEnable = 1 AND ProcessStatus = {0}", (byte)EnumData.ProcessStatus.待出貨);
                string orderSelect = string.Format("SELECT * FROM Orders WHERE StatusCode = {0}", (int)OrderStatusCode.InProcess);
                string itemSelect = string.Format("SELECT * FROM Items WHERE IsEnable = 1 AND ShipFromWarehouseID = {0}", userInfo.WarehouseID);

                var methodList = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && !m.IsDirectLine).ToList();

                ObjectContext context = new ObjectContext("name=QDLogisticsEntities");
                List<Items> ItemList = context.ExecuteStoreQuery<Packages>(packageSelect).ToList().Join(methodList, p => p.ShippingMethod, m => m.ID, (p, m) => p)
                    .Join(context.ExecuteStoreQuery<Orders>(orderSelect).ToList(), p => p.OrderID, o => o.OrderID, (package, order) => new { order, package })
                    .OrderBy(oData => oData.order.TimeOfOrder).OrderByDescending(oData => oData.order.RushOrder)
                    .Join(db.Items.AsNoTracking().Where(i => i.IsEnable.Value && i.ShipFromWarehouseID.Value.Equals(userInfo.WarehouseID)), op => op.package.ID, i => i.PackageID, (op, item) => item).Distinct().ToList();

                List<IGrouping<int?, Items>> itemGroupList = ItemList.GroupBy(i => i.PackageID).ToList();

                XLWorkbook workbook;
                string baseURL = string.Format("{0}://{1}", HttpContext.Request.Url.Scheme, HttpContext.Request.Url.Host);
                string[] filePath = new string[2];

                if (itemGroupList.Where(i => i.Sum(ii => ii.Qty) == 1).Any())
                {
                    workbook = new XLWorkbook();

                    if (SetWorkSheet(workbook, "單項產品", itemGroupList.Where(i => i.Sum(ii => ii.Qty) == 1).ToList(), userInfo.AdminName))
                    {
                        filePath[0] = string.Format("{0}/FileUploads/pickup/Single.xlsx", baseURL);
                        workbook.SaveAs(Path.Combine(HostingEnvironment.MapPath("~/FileUploads"), "pickup/Single.xlsx"));
                    }
                }

                if (itemGroupList.Where(i => i.Sum(ii => ii.Qty) > 1).Any())
                {
                    workbook = new XLWorkbook();

                    if (SetWorkSheet(workbook, "多項產品", itemGroupList.Where(i => i.Sum(ii => ii.Qty) > 1).ToList(), userInfo.AdminName))
                    {
                        filePath[1] = string.Format("{0}/FileUploads/pickup/Multiple.xlsx", baseURL);
                        workbook.SaveAs(Path.Combine(HostingEnvironment.MapPath("~/FileUploads"), "pickup/Multiple.xlsx"));
                    }
                }

                result.data = filePath;
            }
            catch (Exception e)
            {
                result.Error(e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message.Trim() : e.Message.Trim());
            }

            return Json(result, JsonRequestBehavior.AllowGet);
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

                var sheet = workbook.Worksheets.Add(JsonConvert.DeserializeObject<System.Data.DataTable>(jObjects.ToString()), sheetName);
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

        public ActionResult Update_PickUp(string jsonData)
        {
            ApiResult result = new ApiResult();

            ReceiveData receiveData = null;

            Packages package = null;
            List<PickProduct> pickList = null;

            try
            {
                receiveData = JsonConvert.DeserializeObject<ReceiveData>(jsonData);

                LoginInfo userInfo = GetUser(receiveData.GUID);

                int[] PickIDs = receiveData.itemData.Select(i => i.Item1).ToArray();
                pickList = db.PickProduct.Where(pick => PickIDs.Contains(pick.ID)).ToList();

                package = Packages.Get(pickList.First().PackageID.Value);

                MyHelp.Log("Orders", package.OrderID, string.Format("訂單【{0}】APP 出貨", package.OrderID), Session);
                if (!package.Orders.StatusCode.Equals((int)OrderStatusCode.InProcess)) throw new Exception(string.Format("訂單【{0}】無法出貨因為並非InProcess的狀態", package.OrderID));
                if (!package.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨)) throw new Exception(string.Format("訂單【{0}】無法出貨因為並非待出貨的狀態", package.OrderID));

                MyHelp.Log("Orders", package.OrderID, string.Format("訂單【{0}】開始更新", package.OrderID), Session);

                DateTime PickUpDate = new TimeZoneConvert().Utc;

                foreach (PickProduct pick in pickList.Where(pick => pick.PackageID.Equals(package.ID)))
                {
                    pick.IsPicked = true;
                    pick.IsMail = false;
                    pick.QtyPicked = pick.Qty.Value;
                    pick.PickUpDate = PickUpDate;
                    pick.PickUpBy = userInfo.AdminID;
                    PickProduct.Update(pick, pick.ID);
                }

                package.ProcessStatus = (int)EnumData.ProcessStatus.已出貨;
                package.DispatchDate = PickUpDate;
                Packages.Update(package, package.ID);

                foreach (var itemData in receiveData.itemData.Where(i => i.Item2.Any()).ToList())
                {
                    PickProduct pick = pickList.First(p => p.ID.Equals(itemData.Item1));

                    MyHelp.Log("Orders", package.OrderID, string.Format("產品【{0}】存入 {1}", pick.ItemID.Value, string.Join("、", itemData.Item2.ToArray())), Session);

                    foreach (string serialNumber in itemData.Item2)
                    {
                        if (!db.SerialNumbers.AsNoTracking().Any(s => s.SerialNumber.Equals(serialNumber) && s.OrderItemID.Equals(pick.ItemID.Value)))
                        {
                            SerialNumbers.Create(new SerialNumbers
                            {
                                OrderID = pick.OrderID,
                                ProductID = pick.ProductID,
                                SerialNumber = serialNumber,
                                OrderItemID = pick.ItemID.Value,
                                KitItemID = 0
                            });
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
                    server.BroadcastOrderEvent(package.OrderID.Value, EnumData.OrderChangeStatus.已完成出貨);

                MyHelp.Log("Packages", package.ID, string.Format("訂單【{0}】出貨完成", package.OrderID), Session);
            }
            catch (Exception e)
            {
                string errorMessage = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message.Trim() : e.Message.Trim();

                if (package != null)
                {
                    ResetShippedData(package, pickList, receiveData.itemData.Where(i => i.Item2.Any()).SelectMany(i => i.Item2).ToArray());

                    MyHelp.ErrorLog(e, string.Format("訂單【{0}】出貨失敗", package.OrderID), package.OrderID.ToString());
                    errorMessage = string.Format("訂單【{0}】出貨失敗，錯誤：", package.OrderID) + errorMessage;
                }

                result.Error(errorMessage);
            }

            if (result.status)
            {
                try
                {
                    /***** SC 更新 *****/
                    TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
                    ThreadTask threadTask = new ThreadTask(string.Format("包貨區 - 更新訂單【{0}】資料至SC", package.OrderID), Session);

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
                                error = Sync.Update_Tracking(db.Packages.Find(package.ID));
                            }
                            catch (Exception e)
                            {
                                ResetShippedData(package, pickList, receiveData.itemData.Where(i => i.Item2.Any()).SelectMany(i => i.Item2).ToArray());

                                error = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                            }

                            return error;
                        }, HttpContext.Session));
                    }
                    /***** SC 更新 *****/
                }
                catch (Exception e)
                {
                    string errorMessage = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message.Trim() : e.Message.Trim();

                    ResetShippedData(package, pickList, receiveData.itemData.Where(i => i.Item2.Any()).SelectMany(i => i.Item2).ToArray());

                    MyHelp.ErrorLog(e, string.Format("更新訂單【{0}】資料至SC失敗", package.OrderID), package.OrderID.ToString());
                    result.Error(string.Format("更新訂單【{0}】資料至SC失敗，錯誤：", package.OrderID) + errorMessage);
                }

                try
                {
                    List<dynamic> data = new List<dynamic>();
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://internal.qd.com.tw:8080/Ajax/ShipmentByOrder");
                    request.ContentType = "application/json";
                    request.Method = "post";
                    //request.ProtocolVersion = HttpVersion.Version10;

                    foreach (Items item in package.Items.Where(i => i.IsEnable.Value))
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
                            ApiResult postResult = JsonConvert.DeserializeObject<ApiResult>(streamReader.ReadToEnd());
                            if (!postResult.status) throw new Exception(postResult.message);
                            MyHelp.Log("Inventory", package.OrderID, string.Format("訂單【{0}】傳送出貨資料至PO系統", package.OrderID), Session);
                        }
                    }
                }
                catch (Exception e)
                {
                    string errorMsg = string.Format("傳送出貨資料至PO系統失敗，請通知處理人員：{0}", e.InnerException != null ? e.InnerException.Message.Trim() : e.Message.Trim());
                    MyHelp.Log("Inventory", package.OrderID, string.Format("訂單【{0}】{1}", package.OrderID, errorMsg), Session);
                    //result.Error(string.Format("訂單【{0}】{1}", package.OrderID, errorMsg));
                }
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        private void ResetShippedData(Packages package, List<PickProduct> picked, string[] serialArray)
        {
            MyHelp.Log("Orders", package.OrderID.Value, string.Format("訂單【{0}】出貨狀態重置", package.OrderID));

            foreach (PickProduct data in picked)
            {
                data.IsPicked = false;
                data.QtyPicked = 0;
                PickProduct.Update(data, data.ID);
            }

            foreach (var ss in db.SerialNumbers.Where(s => s.OrderID.Equals(package.OrderID) && serialArray.Contains(s.SerialNumber)).ToList())
            {
                SerialNumbers.Delete(ss);
            };

            package.ProcessStatus = (int)EnumData.ProcessStatus.待出貨;
            Packages.Update(package, package.ID);
            Packages.SaveChanges();
        }

        private LoginInfo GetUser(string GUID)
        {
            List<LoginInfo> LoginList = System.Web.HttpContext.Current.Application.Get("WebAppLogin") as List<LoginInfo>;
            if (!LoginList.Any(info => info.GUID.Equals(GUID))) throw new Exception("找不到此使用者!");

            LoginInfo userInfo = LoginList.First(info => info.GUID.Equals(GUID));
            Session["AdminID"] = userInfo.AdminID;
            Session["AdminName"] = userInfo.AdminName;
            Session["ApiUserName"] = userInfo.ApiUserName;
            Session["ApiPassword"] = userInfo.ApiPassword;

            return userInfo;
        }

        public ActionResult Warehouse()
        {
            ApiResult result = new ApiResult();

            try
            {
                string warehouseSelect = string.Format("SELECT * FROM Warehouses WHERE IsEnable = 1 AND IsSellable = 1 AND WarehouseType = {0}", (int)WarehouseTypeType.Normal);

                ObjectContext context = new ObjectContext("name=QDLogisticsEntities");
                result.data = context.ExecuteStoreQuery<Warehouses>(warehouseSelect).Select(w => new { ID = w.ID, name = w.Name, }).ToArray();
            }
            catch (Exception e)
            {
                result.Error(e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message.Trim() : e.Message.Trim());
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Carrier([Bind(Include = "GUID")] ReceiveData data)
        {
            ApiResult result = new ApiResult();

            using (Warehouses = new GenericRepository<Warehouses>(db))
            {
                try
                {
                    LoginInfo userInfo = GetUser(data.GUID);

                    Warehouses warehouse = Warehouses.Get(userInfo.WarehouseID);
                    if (warehouse != null && !string.IsNullOrEmpty(warehouse.CarrierData))
                    {
                        string[] MethodIDs = GetMethod(warehouse.CarrierData);
                        result.data = db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && MethodIDs.Contains(m.ID.ToString()))
                            .Select(c => new { ID = c.ID, name = c.Name }).ToArray();
                    }
                }
                catch (Exception e)
                {
                    result.Error(e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message.Trim() : e.Message.Trim());
                }
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetSkuInventoryQTY(string[] Skus, int[] WarehouseIDs)
        {
            var ItemFilter = db.Items.AsNoTracking().Where(i => i.IsEnable.Value && !i.Packages.ProcessStatus.Equals((byte)EnumData.ProcessStatus.已出貨) && i.Orders.StatusCode.Value.Equals((int)OrderStatusCode.InProcess) && i.Orders.PaymentStatus.Value.Equals((int)OrderPaymentStatus2.Charged));
            if (Skus != null && Skus.Any()) ItemFilter = ItemFilter.Where(i => Skus.Contains(i.ProductID));
            if (WarehouseIDs != null && WarehouseIDs.Any()) ItemFilter = ItemFilter.Where(i => WarehouseIDs.Contains(i.ShipFromWarehouseID.Value));

            var result = ItemFilter.GroupBy(i => i.ShipFromWarehouseID.Value, i => new { SKU = i.ProductID, QTY = i.Qty.Value }, (SCID, item) => item.GroupBy(i => i.SKU, i => i.QTY, (SKU, QTY) => new { SCID, SKU, QTY = QTY.Sum() })).SelectMany(g => g).ToList();

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetOrderItemData(int? OrderID, string SourceID, string UserID, byte? Status)
        {
            if (OrderID.HasValue || !string.IsNullOrEmpty(SourceID) || !string.IsNullOrEmpty(UserID) || Status.HasValue)
            {
                var OrderFilter = db.Orders.Include("Packages").Include("Items").AsNoTracking().Where(o => o.StatusCode.Value.Equals((int)OrderStatusCode.Completed));
                if (OrderID.HasValue) OrderFilter = OrderFilter.Where(o => o.OrderID.Equals(OrderID.Value));
                if (!string.IsNullOrEmpty(SourceID)) OrderFilter = OrderFilter.Where(o => (o.OrderSource.Value.Equals(1) && (o.eBaySalesRecordNumber.Equals(SourceID) || o.OrderSourceOrderId.Contains(SourceID))) || (!o.OrderSource.Value.Equals(1) && o.OrderSourceOrderId.Equals(SourceID)));
                if (!string.IsNullOrEmpty(UserID)) OrderFilter = OrderFilter.Where(o => o.eBayUserID.Equals(UserID));
                if (Status.HasValue) OrderFilter = OrderFilter.Where(o => o.Packages.Where(p => p.IsEnable.Value).All(p => p.ProcessStatus.Equals(Status.Value)));

                var result = OrderFilter.ToList().Select(o => new
                {
                    o.OrderID,
                    o.OrderSource,
                    o.OrderSourceOrderId,
                    o.eBayUserID,
                    o.CompanyID,
                    o.Addresses.CountryCode,
                    o.FinalShippingFee,
                    Items = o.Items.Where(i => i.IsEnable.Value).Select(i => new { SKU = i.ProductID, QTY = i.Qty, i.UnitPrice, Serials = i.SerialNumbers.Select(s => s.SerialNumber).ToList() }).ToList(),
                    WarehouseID = o.Items.First(i => i.IsEnable.Value).ShipFromWarehouseID
                }).ToList();

                return Json(result, JsonRequestBehavior.AllowGet);
            }

            return Json(false);
        }

        public ActionResult GetOrderData(int OrderID)
        {
            ApiResult result = new ApiResult();

            try
            {
                var order = db.Orders.Find(OrderID);

                if (order == null) throw new Exception("Not found order-" + OrderID);

                int? intNull = null;
                TimeZoneConvert TimeZoneConvert = new TimeZoneConvert();
                var PaymentDate = !order.PaymentDate.Equals(DateTime.MinValue) ? order.PaymentDate : order.Payments.OrderByDescending(p => p.AuditDate.Value).FirstOrDefault(p => p.IsEnable.Value && p.PaymentStatus.Value.Equals((int)PaymentStatus.Cleared))?.AuditDate;
                var ShipDate = order.Packages.FirstOrDefault(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.已出貨) && !p.ShipDate.Equals(DateTime.MinValue))?.ShipDate ?? order.ShipDate;
                var Address = order.Addresses;
                result.data = new
                {
                    IsRush = order.RushOrder,
                    OrderParent = order.ParentOrderID.HasValue && !order.ParentOrderID.Equals(0) ? order.ParentOrderID.Value : intNull,
                    OrderSourceID = order.OrderSourceOrderId,
                    SCID = order.OrderID,
                    Company = order.CompanyID,
                    Channel = order.OrderSource,
                    CustomerID = order.eBayUserID,
                    CustomerEmail = order.UserName,
                    OrderStatus = order.StatusCode,
                    OrderDate = TimeZoneConvert.InitDateTime(order.TimeOfOrder.Value, EnumData.TimeZone.EST).Utc,
                    PaymentStatus = order.PaymentStatus.Value,
                    PaymentDate = PaymentDate.HasValue ? TimeZoneConvert.InitDateTime(PaymentDate.Value, EnumData.TimeZone.EST).Utc : PaymentDate,
                    FulfilledDate =  ShipDate.HasValue && !ShipDate.Equals(DateTime.MinValue) ? TimeZoneConvert.InitDateTime(ShipDate.Value, EnumData.TimeZone.EST).Utc : ShipDate,
                    RMAID = order.Packages.FirstOrDefault(p => p.IsEnable.Value && p.RMAId.HasValue)?.RMAId,
                    BuyerNote = order.Instructions,
                    Comment = string.Join("\r\n", order.Packages.Where(p => p.IsEnable.Value && !string.IsNullOrEmpty(p.Comment)).Select(p => p.Comment)),

                    Payments = order.Payments.Where(p => p.IsEnable.Value).Select(p => new
                    {
                        SCID = p.ID,
                        Status = p.PaymentStatus,
                        Date = TimeZoneConvert.InitDateTime(p.AuditDate.Value, EnumData.TimeZone.EST).Utc,
                        Gateway = p.PaymentMethod.Value,
                        TotalValue = order.SubTotal ?? 0,
                        ShippingCharge = order.ShippingTotal ?? 0,
                        InsuranceCharge = order.InsuranceTotal ?? 0,
                        GrandTotal = order.GrandTotal ?? 0,
                        PaymentTotal = p.Amount ?? 0,
                        TransactionID = p.TransactionReferenceNumber
                    }).ToList(),

                    Addresses = new List<object>(){ new {
                        Address.FirstName,
                        Address.LastName,
                        AddressLine1 = Address.StreetLine1,
                        AddressLine2 = Address.StreetLine2,
                        Address.City,
                        State = Address.StateName,
                        Postcode = Address.PostalCode,
                        Address.CountryCode,
                        Address.CountryName,
                        Address.PhoneNumber,
                        Address.EmailAddress
                    } },

                    Packages = order.Packages.Where(p => p.IsEnable.Value).Select(p => new
                    {
                        SCID = p.ID,
                        CarrierBox = p.BoxID,
                        ShippingMethod = p.ShippingMethod ?? 0,
                        Exprot = p.Export ?? 0,
                        ExportMethod = p.ExportMethod ?? 0,
                        ExportValue = p.DeclaredTotal,
                        ExportCurrency = order.OrderCurrencyCode.Value,
                        UploadTracking = p.UploadTracking,
                        Tracking = p.TrackingNumber,
                        DLExport = p.Export ?? 0,
                        DLExportMethod = p.ExportMethod ?? 0,
                        DLExportValue = p.DLDeclaredTotal,
                        DLExportCurrency = p.DLCurrency,
                        DLTracking = p.Box?.TrackingNumber,
                        ShipWarehouse = p.Items.First(i => i.IsEnable.Value).ShipFromWarehouseID.Value,
                        ReturnWarehouse = p.Items.First(i => i.IsEnable.Value).ReturnedToWarehouseID.Value,
                        ShippingStatus = p.ProcessStatus,
                        FulfillmentDate = p.ShipDate.HasValue && !p.ShipDate.Equals(DateTime.MinValue) ? p.ShipDate : null
                    }).ToList(),

                    Items = order.Items.Where(i => i.IsEnable.Value).Select(i => new
                    {
                        SCID = i.ID,
                        PackageID = i.PackageID.Value,
                        Sku = i.ProductID,
                        UnitPrice = i.UnitPrice,
                        ExportValue = i.DeclaredValue,
                        DLExportValue = i.DLDeclaredValue,
                        Qty = i.Qty.Value,
                        eBayItemID = i.eBayItemID,
                        eBayTransationID = i.eBayTransactionId,
                        SalesRecordNumber = i.SalesRecordNumber,
                        RMAID = i.Packages.RMAId
                    }).ToList(),

                    Serials = order.Packages.Where(p => p.IsEnable.Value).SelectMany(p => p.Items.Where(i => i.IsEnable.Value).SelectMany(i => i.SerialNumbers)).Select(s => new
                    {
                        s.SerialNumber,
                        ItemID = s.OrderItemID,
                        Sku = s.ProductID
                    })
                };
            }
            catch (Exception e)
            {
                result.Error(e.InnerException?.Message ?? e.Message);
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        private string[] GetMethod(string CarrierData)
        {
            Dictionary<string, bool> methodList = JsonConvert.DeserializeObject<Dictionary<string, bool>>(CarrierData);
            return methodList.Where(m => m.Value).Select(c => c.Key).ToArray();
        }

        [System.ComponentModel.DataAnnotations.MetadataType(typeof(PickProduct))]
        public partial class PickData
        {
            public string Country { get; set; }
        }
        public ActionResult GetCountryToType()
        {
            var result = db.CountryType.ToList();
            return Json(result, JsonRequestBehavior.AllowGet);
        }
        public class ReceiveData
        {
            public string UserName { get; set; }
            public string Password { get; set; }
            public int WarehouseID { get; set; }
            public int CarrierID { get; set; }
            public int TaskID { get; set; }
            public string GUID { get; set; }

            public List<Tuple<int, List<string>>> itemData { get; set; }
        }

        public class LoginInfo
        {
            public int AdminID { get; }
            public string AdminName { get; }
            public int WarehouseID { get; }
            public string GUID { get; }
            public DateTime Login_at { get; }
            public string ApiUserName { get; }
            public string ApiPassword { get; }

            public LoginInfo(int AdminID, string AdminName, int WarehouseID, string GUID, string ApiUserName, string ApiPassword)
            {
                this.AdminID = AdminID;
                this.AdminName = AdminName;
                this.WarehouseID = WarehouseID;
                this.GUID = GUID;
                Login_at = DateTime.UtcNow;
                this.ApiUserName = ApiUserName;
                this.ApiPassword = ApiPassword;
            }
        }

        public class ApiResult
        {
            public bool status;
            public string message;
            public object data;

            private System.Diagnostics.Stopwatch sw;
            public double Time
            {
                get
                {
                    sw.Stop();
                    return sw.Elapsed.TotalMilliseconds / 1000;
                }
            }

            public ApiResult()
            {
                this.status = true;
                this.message = null;
                this.data = null;

                this.sw = new System.Diagnostics.Stopwatch();
                this.sw.Start();
            }

            public ApiResult Error(string message)
            {
                this.status = false;
                this.message = message;

                return this;
            }
        }
    }
}