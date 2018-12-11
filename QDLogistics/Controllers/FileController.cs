using ClosedXML.Excel;
using LinqToExcel;
using Neodynamic.SDK.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QDLogistics.Commons;
using QDLogistics.Filters;
using QDLogistics.Models;
using QDLogistics.Models.Object;
using QDLogistics.Models.Repositiry;
using QDLogistics.OrderService;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;

namespace QDLogistics.Controllers
{
    public class FileController : Controller
    {
        string basePath;
        Dictionary<string, object> results;

        private QDLogisticsEntities db;
        private IRepository<Orders> Orders;
        private IRepository<Packages> Packages;
        private IRepository<Items> Items;
        private IRepository<Services> Services;
        private IRepository<ShippingMethod> ShippingMethod;
        private IRepository<Carriers> Carriers;
        private IRepository<Skus> Skus;

        public FileController()
        {
            db = new QDLogisticsEntities();
            basePath = HostingEnvironment.MapPath("~/FileUploads");
        }

        [HttpPost]
        [CheckSession]
        public ActionResult Upload(HttpPostedFileBase file, string type, string action)
        {
            results = new Dictionary<string, object>();

            if (file != null)
            {
                if (file.ContentLength > 0)
                {
                    results.Add("fileName", Path.GetFileName(file.FileName));
                    results.Add("filePath", Path.Combine(basePath, type, action));
                    Directory.CreateDirectory(results["filePath"].ToString());

                    results.Add("fullPath", Path.Combine(results["filePath"].ToString(), results["fileName"].ToString()));
                    file.SaveAs(results["fullPath"].ToString());

                    CheckResult checkResult = new CheckResult();
                    Import import = new Import(results["fullPath"].ToString());
                    switch (type)
                    {
                        case "dropShip":
                            MyHelp.Log("DropShip", null, "匯入直發商訂單資料");
                            List<Packages> dropshipData = new List<Packages>();
                            checkResult = import.CheckDropshipImportData(dropshipData, Session);
                            //if (checkResult.Success) import.SaveImportData(packageData);
                            break;

                        case "dropshipDL":
                            MyHelp.Log("DropShip", null, "匯入直發商DL訂單資料");
                            List<Packages> dropshipDLData = new List<Packages>();
                            checkResult = import.CheckDropshipDLImportData(dropshipDLData, Session);
                            //if (checkResult.Success) import.SaveImportData(packageData);
                            break;

                        case "service":
                            MyHelp.Log("Services", null, "匯入預設運輸方式");
                            List<Services> serviceData = new List<Services>();
                            checkResult = import.CheckImportData(serviceData);
                            if (checkResult.Success) import.SaveImportData(serviceData);
                            break;

                        case "country":
                            MyHelp.Log("Carriers", null, "匯入運輸國家");
                            List<ShippingMethod> carrierData = new List<ShippingMethod>();
                            checkResult = import.CheckImportData(carrierData);
                            if (checkResult.Success) import.SaveImportData(carrierData);
                            break;

                        case "sku":
                            MyHelp.Log("Skus", null, "匯入產品序號");
                            List<Skus> skuData = new List<Skus>();
                            checkResult = import.CheckImportData(skuData);
                            if (checkResult.Success) import.SaveImportData(skuData);
                            break;
                    }

                    results.Add("Result", checkResult.Success);
                    results.Add("Msg", checkResult.Success ? "資料匯入成功" : checkResult.ErrorMessage);

                    return Content(JsonConvert.SerializeObject(results), "appllication/json");
                }
            }

            return Content(JsonConvert.SerializeObject(new { Result = false, Msg = "檔案不存在" }), "appllication/json");
        }

        [HttpPost]
        [CheckSession]
        public ActionResult Export(string type, string IDs)
        {
            var ExportData = this.GetExportData(type, IDs.Split(','));
            var dataTable = JsonConvert.DeserializeObject<DataTable>(ExportData.ToString());

            var typeList = new Dictionary<string, Dictionary<string, string>>();
            typeList.Add("order", new Dictionary<string, string>() { { "sheetName", "待出貨訂單" }, { "fileName", "OrderData" } });
            typeList.Add("waiting", new Dictionary<string, string>() { { "sheetName", "待出貨訂單" }, { "fileName", "OrderData" } });
            typeList.Add("shipped", new Dictionary<string, string>() { { "sheetName", "已出貨訂單" }, { "fileName", "OrderData" } });
            typeList.Add("service", new Dictionary<string, string>() { { "sheetName", "預設運輸方式" }, { "fileName", "ServiceData" } });
            typeList.Add("country", new Dictionary<string, string>() { { "sheetName", "運送國家" }, { "fileName", "CountryData" } });
            typeList.Add("sku", new Dictionary<string, string>() { { "sheetName", "品號" }, { "fileName", "SkuData" } });
            typeList.Add("dropShip", new Dictionary<string, string>() { { "sheetName", "DropShipper" }, { "fileName", "DropShipData" } });
            typeList.Add("dropshipDL", new Dictionary<string, string>() { { "sheetName", "DropShipperDL" }, { "fileName", "DropShipData" } });

            MyHelp.Log("", null, "匯出" + typeList[type]["sheetName"]);
            return new ExportExcelResult
            {
                SheetName = typeList[type]["sheetName"],
                FileName = string.Concat(typeList[type]["fileName"], "_", DateTime.Now.ToString("yyyyMMddHHmmss"), ".xlsx"),
                ExportData = dataTable
            };
        }

        [CheckSession]
        private JArray GetExportData(string type, string[] id)
        {
            JArray jObjects = new JArray();

            List<Packages> packageList;
            List<Items> itemList;
            List<OrderJoinData> orderDataList;
            int[] packageIDs;

            switch (type)
            {
                case "order":
                case "shipped":
                    Orders = new GenericRepository<Orders>(db);
                    Packages = new GenericRepository<Packages>(db);
                    Items = new GenericRepository<Items>(db);

                    int[] OrderIDs = id.Select(int.Parse).ToArray();
                    var orderList = Orders.GetAll(true).Where(o => OrderIDs.Contains(o.OrderID)).ToList();
                    packageList = Packages.GetAll(true).Where(p => OrderIDs.Contains(p.OrderID.Value)).ToList();
                    itemList = Items.GetAll(true).Where(i => OrderIDs.Contains(i.OrderID.Value)).ToList();

                    var OrderPackage = orderList.Join(packageList, order => order.OrderID, package => package.OrderID, (order, package) => new { order, package });
                    var OrderPackageItem = OrderPackage.Join(itemList, OP => OP.order.OrderID, item => item.OrderID, (OP, item) => new OrderJoinData() { order = OP.order, package = OP.package, item = item });

                    foreach (var data in OrderPackageItem.ToList())
                    {
                        if (type == "order")
                        {
                            jObjects.Add(DataProcess.SetOrderExcelData(type, data, new JObject()));
                        }
                        else
                        {
                            List<SerialNumbers> serialNumbers = data.item.SerialNumbers.OrderBy(s => s.SerialNumber).ToList();
                            for (int i = 0; i < data.item.Qty; i++)
                            {
                                if (serialNumbers.Any()) data.item.SerialNumber = serialNumbers.Skip(i).FirstOrDefault().SerialNumber;
                                jObjects.Add(DataProcess.SetOrderExcelData(type, data, new JObject()));
                            }
                        }
                    }
                    break;

                case "waiting":
                    packageIDs = id.Select(i => int.Parse(i)).ToArray();
                    var OrderData = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && packageIDs.Contains(p.ID)).ToList()
                        .Join(db.Orders.AsNoTracking(), p => p.OrderID.Value, o => o.OrderID, (p, o) => new OrderJoinData() { order = o, package = p })
                        .Join(db.Items.AsNoTracking().Where(i => i.IsEnable.Value), oData => oData.package.ID, i => i.PackageID.Value, (oData, i) => new OrderJoinData(oData) { item = i }).ToList();

                    foreach (var oData in OrderData)
                    {
                        List<SerialNumbers> serials = oData.item.SerialNumbers.Where(s => string.IsNullOrEmpty(s.SerialNumber)).ToList();
                        for (int i = 0; i < oData.item.Qty; i++)
                        {
                            if (serials.Skip(i).Any()) oData.item.SerialNumber = serials.Skip(i).First().SerialNumber;
                            jObjects.Add(DataProcess.SetOrderExcelData(type, oData, new JObject()));
                        }
                    }
                    break;

                case "service":
                    Services = new GenericRepository<Services>(db);
                    List<Services> serviceList = Services.GetAll(true).Where(s => id.Contains(s.ServiceCode)).ToList();

                    foreach (Services service in serviceList)
                    {
                        var jo = new JObject();
                        jo.Add("ServiceCode", service.ServiceCode);
                        jo.Add("ServiceName", service.ServiceName);
                        jo.Add("ShippingMethod", service.ShippingMethod);
                        jObjects.Add(jo);
                    }
                    break;

                case "country":
                    ShippingMethod = new GenericRepository<ShippingMethod>(db);
                    List<ShippingMethod> methodList = ShippingMethod.GetAll(true).Where(m => m.IsEnable).ToList();
                    IEnumerable<Country> countries = MyHelp.GetCountries();

                    foreach (ShippingMethod method in methodList)
                    {
                        var jo = new JObject();
                        jo.Add("ID", method.ID);
                        jo.Add("Method", method.Name);

                        var countryData = GetCountryBool(countries, method.CountryData);
                        foreach (Country country in countries)
                        {
                            jo.Add(country.Name, countryData[country.ID]);
                        }
                        jObjects.Add(jo);
                    }
                    break;

                case "sku":
                    Skus = new GenericRepository<Skus>(db);
                    List<Skus> skuList = Skus.GetAll(true).Where(s => id.Contains(s.Sku)).ToList();

                    foreach (Skus sku in skuList)
                    {
                        var jo = new JObject();
                        jo.Add("Sku", sku.Sku);
                        jo.Add("ProductName", sku.ProductName);
                        jo.Add("UPC", sku.UPC);
                        jo.Add("Brand", sku.Brand.Value);
                        jo.Add("Battery", sku.Battery.Value);
                        jo.Add("Export", sku.Export.Value);
                        jo.Add("ExportMethod", sku.ExportMethod.Value);
                        jo.Add("PurchaseInvoice", sku.PurchaseInvoice);
                        jo.Add("Weight", sku.Weight);
                        jo.Add("ShippingWeight", sku.ShippingWeight);
                        jObjects.Add(jo);
                    }
                    break;

                case "dropShip":
                    Orders = new GenericRepository<Orders>(db);
                    Packages = new GenericRepository<Packages>(db);
                    Items = new GenericRepository<Items>(db);
                    IRepository<Addresses> Addresses = new GenericRepository<Addresses>(db);

                    orderDataList = Packages.GetAll(true).Where(p => p.IsEnable.Equals(true) && id.Select(i => int.Parse(i)).Contains(p.ID))
                        .Join(Items.GetAll(true).Where(i => i.IsEnable.Equals(true)).ToList(), p => p.ID, i => i.PackageID.Value, (p, i) => new OrderJoinData() { package = p, item = i })
                        .Join(Orders.GetAll(true).ToList(), oData => oData.package.OrderID.Value, o => o.OrderID, (oData, o) => new OrderJoinData(oData) { order = o })
                        .Join(Addresses.GetAll(true).Where(a => a.IsEnable.Equals(true)).ToList(), oData => oData.order.ShippingAddress.Value, a => a.Id, (oData, a) => new OrderJoinData(oData) { address = a }).ToList();

                    if (orderDataList.Any())
                    {
                        long Tracking;
                        foreach (OrderJoinData oData in orderDataList)
                        {
                            for (int i = 1; i <= oData.item.Qty; i++)
                            {
                                JObject jo = new JObject();
                                jo.Add("OrderID", oData.order.OrderID);
                                jo.Add("PO#", oData.package.POId??0);
                                jo.Add("Invoice#", !string.IsNullOrEmpty(oData.package.POInvoice) ? oData.package.POInvoice : "");
                                jo.Add("ProductID", oData.item.ProductID);
                                jo.Add("DisplayName", oData.item.DisplayName);
                                jo.Add("Qty", 1);
                                jo.Add("GrandTotal", oData.package.DeclaredTotal.ToString());
                                jo.Add("Currency", Enum.GetName(typeof(CurrencyCodeType), oData.order.OrderCurrencyCode.Value));
                                jo.Add("Shipping Method", oData.package.ShippingMethod.Value);
                                jo.Add("Tracking", !string.IsNullOrEmpty(oData.package.TrackingNumber) && long.TryParse(oData.package.TrackingNumber, out Tracking) ? Tracking : (long?)null);
                                jo.Add("Serial", oData.item.SerialNumbers.Skip(i - 1).Any() ? oData.item.SerialNumbers.Skip(i - 1).First().SerialNumber.ToString() : null);
                                jo.Add("ShipFirstName", oData.address.FirstName);
                                jo.Add("ShipLastName", oData.address.LastName);
                                jo.Add("ShipCompanyName", oData.address.CompanyName);
                                jo.Add("ShipAddress1", oData.address.StreetLine1);
                                jo.Add("ShipAddress2", oData.address.StreetLine2);
                                jo.Add("ShipCity", oData.address.City);
                                jo.Add("ShipState", string.IsNullOrEmpty(oData.address.StateName) ? oData.address.StateCode : oData.address.StateName);
                                jo.Add("ShipZipCode", oData.address.PostalCode);
                                jo.Add("ShipCountry", string.IsNullOrEmpty(oData.address.CountryName) ? MyHelp.GetCountries().First(c => c.ID.Equals(oData.address.CountryCode)).Name : oData.address.CountryName);
                                jo.Add("Tel.", oData.address.PhoneNumber);
                                jo.Add("Comment", oData.package.Comment);
                                jo.Add("Supplier Comment", !string.IsNullOrEmpty(oData.package.SupplierComment) ? oData.package.SupplierComment : "");
                                jObjects.Add(jo);
                            }
                        }
                    }
                    break;
                case "dropshipDL":
                    packageIDs = id.Select(i => int.Parse(i)).ToArray();

                    orderDataList = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && packageIDs.Contains(p.ID)).ToList()
                        .Join(db.Orders.AsNoTracking(), p => p.OrderID, o => o.OrderID, (p, o) => new OrderJoinData() { package = p, order = o })
                        .Join(db.Addresses.AsNoTracking().Where(a => a.IsEnable.Value), data => data.order.ShippingAddress.Value, a => a.Id, (data, a) => new OrderJoinData(data) { address = a })
                        .Join(db.ShippingMethod.AsNoTracking().Where(m => m.IsEnable && m.IsDirectLine), data => data.package.ShippingMethod, m => m.ID, (data, m) => new OrderJoinData(data) { method = m })
                        .Join(db.Items.AsNoTracking().Where(i => i.IsEnable.Value), data => data.package.ID, i => i.PackageID.Value, (data, i) => new OrderJoinData(data) { item = i }).ToList();


                    if (orderDataList.Any())
                    {
                        long Tracking;

                        int[] itemIDs = orderDataList.Select(oData => oData.item.ID).ToArray();
                        var serialGroup = db.SerialNumbers.AsNoTracking().Where(s => itemIDs.Contains(s.OrderItemID)).GroupBy(s => s.OrderItemID).ToDictionary(g => g.Key, g => g.Select(s => s.SerialNumber).ToList());

                        string[] productIDs = orderDataList.Select(oData => oData.item.ProductID).Distinct().ToArray();
                        var skuName = db.Skus.AsNoTracking().Where(s => s.IsEnable.Value && productIDs.Contains(s.Sku)).ToDictionary(s => s.Sku, s => s.ProductName);

                        foreach (var oData in orderDataList)
                        {
                            for (int i = 0; i < oData.item.Qty; i++)
                            {
                                jObjects.Add(new JObject()
                                {
                                    { "OrderID", oData.order.OrderID },
                                    { "PO#", oData.package.POId??0 },
                                    { "Invoice#", oData.package?.POInvoice },
                                    { "ProductID", oData.item.ProductID },
                                    { "DisplayName", skuName[oData.item.ProductID] },
                                    { "Qty", 1 },
                                    { "GrandTotal", oData.package.DeclaredTotal.ToString() },
                                    { "Currency", Enum.GetName(typeof(CurrencyCodeType), oData.order.OrderCurrencyCode.Value) },
                                    { "Shipping Method", oData.package.ShippingMethod.Value },
                                    { "Tracking", !string.IsNullOrEmpty(oData.package.TrackingNumber) && long.TryParse(oData.package.TrackingNumber, out Tracking) ? Tracking : (long?)null },
                                    { "Serial", serialGroup.ContainsKey(oData.item.ID) && serialGroup[oData.item.ID].Skip(i).Any() ? serialGroup[oData.item.ID].Skip(i).First() : null },
                                    { "ShipFirstName", oData.address.FirstName },
                                    { "ShipLastName", oData.address.LastName },
                                    { "ShipCompanyName", oData.address.CompanyName },
                                    { "ShipAddress1", oData.address.StreetLine1 },
                                    { "ShipAddress2", oData.address.StreetLine2 },
                                    { "ShipCity", oData.address.City },
                                    { "ShipState", string.IsNullOrEmpty(oData.address.StateName) ? oData.address.StateCode : oData.address.StateName },
                                    { "ShipZipCode", oData.address.PostalCode },
                                    { "ShipCountry", string.IsNullOrEmpty(oData.address.CountryName) ? MyHelp.GetCountries().First(c => c.ID.Equals(oData.address.CountryCode)).Name : oData.address.CountryName },
                                    { "Tel.", oData.address.PhoneNumber },
                                    { "Comment", oData.package.Comment },
                                    { "Supplier Comment", !string.IsNullOrEmpty(oData.package.SupplierComment) ? oData.package.SupplierComment : "" }
                                });
                            }
                        }
                    }
                    break;
            }

            return jObjects;
        }

        [AllowAnonymous]
        public void PrintFile(List<string> fileName, List<string> filePath, List<int> amount, string printerName = null)
        {
            if (fileName.Any())
            {
                Process[] processList = Process.GetProcesses().Where(p => p.ProcessName == "wcpp").ToArray();
                if (processList.Any())
                {
                    Thread t = new Thread(WaitProcess);
                    t.IsBackground = true;
                    t.Start(processList.First());
                }

                string[] pdfList = new string[] { "AirWaybill.pdf", "Label.pdf" };
                ClientPrintJobGroup cpjg = new ClientPrintJobGroup();

                if (fileName.Any(f => pdfList.Contains(f)))
                {
                    ClientPrintJob cpj2 = new ClientPrintJob()
                    {
                        ClientPrinter = new InstalledPrinter(HttpUtility.UrlDecode(printerName))
                    };

                    int index = 0;
                    if (fileName.Any(f => f.Equals("AirWaybill.pdf")))
                    {
                        index = fileName.IndexOf("AirWaybill.pdf");
                    }
                    else if (fileName.Any(f => f.Equals("Label.pdf")))
                    {
                        index = fileName.IndexOf("Label.pdf");
                    }

                    if (amount[index] > 0)
                    {
                        cpj2.PrintFile = new PrintFile(HttpUtility.UrlDecode(filePath[index]), HttpUtility.UrlDecode(fileName[index]), amount[index]);
                        cpjg.Add(cpj2);
                    }
                }

                var normalList = fileName.Select((value, i) => new { i, value }).Where(n => !string.IsNullOrEmpty(n.value) && !n.value.Equals("AirWaybill.pdf") && !n.value.Equals("Label.pdf")).ToList();
                if (normalList.Count() > 0)
                {
                    ClientPrintJob cpj1 = new ClientPrintJob()
                    {
                        ClientPrinter = new DefaultPrinter()
                    };

                    foreach (var name in normalList)
                    {
                        if (amount[name.i] > 0)
                        {
                            cpj1.PrintFileGroup.Add(new PrintFile(HttpUtility.UrlDecode(filePath[name.i]), HttpUtility.UrlDecode(name.value), amount[name.i]));
                        }
                    }

                    if (cpj1.PrintFileGroup.Any()) cpjg.Add(cpj1);
                }

                System.Web.HttpContext.Current.Response.ContentType = "application/octet-stream";
                System.Web.HttpContext.Current.Response.BinaryWrite(cpjg.GetContent());
                System.Web.HttpContext.Current.Response.End();
            }
        }

        private void WaitProcess(object param)
        {
            Process process = (Process)param;
            process.EnableRaisingEvents = true;
            process.Disposed += new EventHandler(processExited);

            while (process.MainWindowHandle == IntPtr.Zero)
            {
                Thread.Sleep(500);
                process.Refresh();
            }

            CloseProcess(process);
        }

        private void processExited(object sender, EventArgs e)
        {
            Process process = (Process)sender;

            string[] processName = new string[] { "AcroRd32", "EXCEL" };
            Process[] processList = Process.GetProcesses().Where(p => processName.Contains(p.ProcessName)).OrderBy(p => p.Id).ToArray();
            foreach (Process p in processList)
            {
                CloseProcess(p);
            }
        }

        private void CloseProcess(Process process)
        {
            if (!process.HasExited)
            {
                bool result = false;
                //測試處理序是否還有回應
                if (process.Responding)
                {
                    //關閉使用者介面的處理序
                    result = process.CloseMainWindow();
                }

                if (!result)
                {
                    //立即停止相關處理序。意即，處理序沒回應，強制關閉
                    process.Kill();
                }
            }

            if (process != null)
            {
                process.Close();
                process.Dispose();
            }
        }

        private Dictionary<string, bool> GetCountryBool(IEnumerable<Country> countries, string CountryData)
        {
            if (string.IsNullOrEmpty(CountryData)) return countries.ToDictionary(c => c.ID, c => false);

            Dictionary<string, bool> Data = JsonConvert.DeserializeObject<Dictionary<string, bool>>(CountryData);
            return countries.ToDictionary(c => c.ID, c => Data.ContainsKey(c.ID) ? Data[c.ID] : false);
        }
    }

    public class Import
    {
        private CheckResult result;
        private FileInfo targetFile;
        private ExcelQueryFactory excelFile;

        private QDLogisticsEntities db;
        private IRepository<Packages> Packages;
        private IRepository<SerialNumbers> SerialNumbers;
        private IRepository<Services> Services;
        private IRepository<ShippingMethod> ShippingMethod;
        private IRepository<Carriers> Carriers;
        private IRepository<Skus> Skus;

        public Import(string fileName)
        {
            result = new CheckResult();
            targetFile = new FileInfo(fileName);
            excelFile = new ExcelQueryFactory(fileName);

            db = new QDLogisticsEntities();
        }

        [CheckSession]
        public CheckResult CheckDropshipImportData(List<Packages> importData, HttpSessionStateBase Session)
        {
            if (!checkExists()) return result;

            var excelContent = excelFile.Worksheet("DropShipper").Where(row => !row["OrderID"].Equals(null));

            int errorCount = 0;
            var importErrorMessages = new List<string>();

            if (excelContent.Count() > 0)
            {
                TaskFactory factory = HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
                ThreadTask threadTask = new ThreadTask("直發商待出貨區 - 匯入訂單資料");

                lock (factory)
                {
                    threadTask.AddWork(factory.StartNew(() =>
                    {
                        threadTask.Start();

                        string error = "";
                        List<string> message = new List<string>();
                        Packages = new GenericRepository<Packages>(db);
                        SerialNumbers = new GenericRepository<SerialNumbers>(db);

                        try
                        {
                            int warehouseID = 0, MethodID = 0;
                            int[] OrderIDs = excelContent.Select(row => int.Parse(row["OrderID"].ToString())).ToArray();
                            List<Packages> packageList = Packages.GetAll().Where(p => p.IsEnable.Equals(true) && OrderIDs.Contains(p.OrderID.Value) && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨)).ToList();
                            if (packageList.Any() && int.TryParse(Session["warehouseId"].ToString(), out warehouseID))
                            {
                                foreach (Packages package in packageList)
                                {
                                    bool needUpload = false;

                                    try
                                    {
                                        List<Items> itemList = package.Items.Where(i => i.IsEnable.Equals(true) && i.ShipFromWarehouseID.Equals(warehouseID)).ToList();
                                        if (itemList.Any())
                                        {
                                            var packageData = excelContent.First(row => row["OrderID"].Equals(package.OrderID));
                                            needUpload = string.IsNullOrEmpty(package.TrackingNumber) && !string.IsNullOrEmpty(packageData["Tracking"].ToString());

                                            package.POInvoice = packageData["Invoice#"].ToString();
                                            package.ShippingMethod = int.TryParse(packageData["Shipping Method"].ToString(), out MethodID) ? MethodID : package.ShippingMethod;
                                            package.TrackingNumber = packageData["Tracking"].ToString();
                                            package.SupplierComment = packageData["Supplier Comment"].ToString();
                                            if (needUpload) package.ProcessStatus = (int)EnumData.ProcessStatus.已出貨;
                                            Packages.Update(package, package.ID);

                                            foreach (Items item in itemList)
                                            {
                                                foreach (SerialNumbers serial in item.SerialNumbers.ToList())
                                                {
                                                    SerialNumbers.Delete(serial);
                                                }

                                                foreach (var row in excelContent.Where(row => row["OrderID"].Equals(item.OrderID) && row["ProductID"].Equals(item.ProductID) && !row["Serial"].Equals(null)).ToList())
                                                {
                                                    string serialNumber;
                                                    try
                                                    {
                                                        serialNumber = Convert.ToDecimal(row["Serial"].Value).ToString();
                                                    }
                                                    catch
                                                    {
                                                        serialNumber = row["Serial"].Value.ToString();
                                                    }

                                                    SerialNumbers.Create(new SerialNumbers()
                                                    {
                                                        OrderID = item.OrderID,
                                                        OrderItemID = item.ID,
                                                        ProductID = item.ProductID,
                                                        SerialNumber = serialNumber
                                                    });
                                                }
                                            }

                                            Packages.SaveChanges();
                                            MyHelp.Log("Orders", package.OrderID, string.Format("直發商訂單【{0}】更新完成", package.OrderID));
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        needUpload = false;
                                        package.ProcessStatus = (int)EnumData.ProcessStatus.待出貨;
                                        package.TrackingNumber = "";
                                        Packages.Update(package, package.ID);
                                        Packages.SaveChanges();

                                        MyHelp.ErrorLog(e, string.Format("直發商訂單【{0}】更新失敗", package.OrderID), package.OrderID.ToString());
                                        message.Add(string.Format("直發商訂單【{0}】更新失敗，錯誤：", package.OrderID) + (e.InnerException != null ? e.InnerException.Message.Trim() : e.Message.Trim()));
                                    }

                                    if (needUpload)
                                    {
                                        try
                                        {
                                            ThreadTask PO_ThreadTask = new ThreadTask(string.Format("直發商待出貨區 - 更新訂單【{0}】以及PO【{1}】資料至SC", package.OrderID, package.POId), Session);

                                            lock (factory)
                                            {
                                                PO_ThreadTask.AddWork(factory.StartNew(() => UploadPO(PO_ThreadTask, Session, package)));
                                            }
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

                                error = string.Join("; ", message);
                            }
                        }
                        catch (DbEntityValidationException ex)
                        {
                            var errorMessages = ex.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(x => x.ErrorMessage);
                            error = string.Join("; ", errorMessages);
                        }
                        catch (Exception e)
                        {
                            MyHelp.ErrorLog(e, "匯入訂單資料失敗");
                            error = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                        }

                        return error;
                    }));
                }
            }

            return returnResult(errorCount, excelContent.Count(), importErrorMessages);
        }

        private string UploadPO(ThreadTask threadTask, HttpSessionStateBase session, Packages package)
        {
            threadTask.Start();

            string error = "";

            try
            {
                SyncProcess Sync = new SyncProcess(session);
                error = Sync.Update_PurchaseOrder(package.ID);
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(x => x.ErrorMessage);
                error = string.Join("; ", errorMessages);
            }
            catch (Exception e)
            {
                error = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
            }

            return error;
        }

        public CheckResult CheckDropshipDLImportData(List<Packages> importData, HttpSessionStateBase Session)
        {
            if (!checkExists()) return result;

            var excelContent = excelFile.Worksheet("DropShipperDL").Where(row => !row["OrderID"].Equals(null));

            int errorCount = 0;
            var importErrorMessages = new List<string>();

            if (excelContent.Count() > 0)
            {
                TaskFactory factory = HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
                ThreadTask threadTask = new ThreadTask("直發商待出貨區 - 匯入DL訂單資料");

                lock (factory)
                {
                    threadTask.AddWork(factory.StartNew(() =>
                    {
                        threadTask.Start();

                        string error = "";
                        List<string> message = new List<string>();
                        Packages = new GenericRepository<Packages>(db);
                        SerialNumbers = new GenericRepository<SerialNumbers>(db);

                        try
                        {
                            int warehouseID = 0;
                            int[] OrderIDs = excelContent.Select(row => int.Parse(row["OrderID"].ToString())).ToArray();
                            List<Packages> packageList = Packages.GetAll().Where(p => p.IsEnable.Equals(true) && OrderIDs.Contains(p.OrderID.Value) && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨)).ToList();
                            if (packageList.Any() && int.TryParse(Session["warehouseId"].ToString(), out warehouseID))
                            {
                                foreach (Packages package in packageList)
                                {
                                    try
                                    {
                                        List<Items> itemList = package.Items.Where(i => i.IsEnable.Equals(true) && i.ShipFromWarehouseID.Equals(warehouseID)).ToList();
                                        if (itemList.Any())
                                        {
                                            var packageData = excelContent.First(row => row["OrderID"].Equals(package.OrderID));

                                            package.POInvoice = packageData["Invoice#"].ToString();
                                            Packages.Update(package, package.ID);

                                            foreach (Items item in itemList)
                                            {
                                                foreach (SerialNumbers serial in item.SerialNumbers.ToList())
                                                {
                                                    SerialNumbers.Delete(serial);
                                                }

                                                foreach (var row in excelContent.Where(row => row["OrderID"].Equals(item.OrderID) && row["ProductID"].Equals(item.ProductID) && !row["Serial"].Equals(null)).ToList())
                                                {
                                                    string serialNumber;
                                                    try
                                                    {
                                                        serialNumber = Convert.ToDecimal(row["Serial"].Value).ToString();
                                                    }catch
                                                    {
                                                        serialNumber = row["Serial"].Value.ToString();
                                                    }

                                                    SerialNumbers.Create(new SerialNumbers()
                                                    {
                                                        OrderID = item.OrderID,
                                                        OrderItemID = item.ID,
                                                        ProductID = item.ProductID,
                                                        SerialNumber = serialNumber
                                                    });
                                                }
                                            }

                                            Packages.SaveChanges();
                                            MyHelp.Log("Orders", package.OrderID, string.Format("直發商訂單【{0}】更新完成", package.OrderID));
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        MyHelp.ErrorLog(e, string.Format("直發商訂單【{0}】更新失敗", package.OrderID), package.OrderID.ToString());
                                        message.Add(string.Format("直發商訂單【{0}】更新失敗，錯誤：", package.OrderID) + (e.InnerException != null ? e.InnerException.Message.Trim() : e.Message.Trim()));
                                    }
                                }

                                error = string.Join("; ", message);
                            }
                        }
                        catch (DbEntityValidationException ex)
                        {
                            var errorMessages = ex.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(x => x.ErrorMessage);
                            error = string.Join("; ", errorMessages);
                        }
                        catch (Exception e)
                        {
                            MyHelp.ErrorLog(e, "匯入訂單資料失敗");
                            error = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                        }

                        return error;
                    }));
                }
            }

            return returnResult(errorCount, excelContent.Count(), importErrorMessages);
        }

        public CheckResult CheckImportData(List<Services> importData)
        {
            if (!checkExists()) return result;

            Services = new GenericRepository<Services>(db);
            ShippingMethod = new GenericRepository<ShippingMethod>(db);

            excelFile.AddMapping<Services>(s => s.ServiceCode, "ServiceCode");
            excelFile.AddMapping<Services>(s => s.ServiceName, "ServiceName");
            excelFile.AddMapping<Services>(s => s.ShippingMethod, "ShippingMethod");
            var excelContent = excelFile.Worksheet<Services>("預設運輸方式");

            int errorCount = 0;
            int rowIndex = 1;
            var importErrorMessages = new List<string>();
            var methodList = ShippingMethod.GetAll(true).Select(m => m.ID).ToList();

            foreach (var row in excelContent)
            {
                var errorMessage = new StringBuilder();

                Services service = Services.Get(row.ServiceCode);
                if (service == null)
                {
                    service = new Services() { IsEnable = true, ServiceCode = row.ServiceCode };
                    Services.Create(service);
                    Services.SaveChanges();
                }
                service.ServiceName = row.ServiceName;
                service.ShippingMethod = row.ShippingMethod;

                if (string.IsNullOrWhiteSpace(row.ShippingMethod.ToString()))
                {
                    // errorMessage.Append("CarrierID - 不可空白.");
                }
                else
                {
                    if (!methodList.Contains(row.ShippingMethod.Value)) errorMessage.Append("MethodID - 無法找到.");
                }

                if (errorMessage.Length > 0)
                {
                    errorCount += 1;
                    importErrorMessages.Add(string.Format("第 {0} 列資料發現錯誤：{1}{2}", rowIndex, errorMessage, "\r\n"));
                }

                importData.Add(service);
                rowIndex += 1;
            }

            return returnResult(errorCount, importData.Count, importErrorMessages);
        }

        public CheckResult CheckImportData(List<ShippingMethod> importData)
        {
            if (!checkExists()) return result;

            ShippingMethod = new GenericRepository<ShippingMethod>(db);
            IEnumerable<Country> countries = MyHelp.GetCountries();
            var codes = countries.Select(c => c.ID).ToArray();

            var excelContent = excelFile.Worksheet("運送國家");

            int errorCount = 0;
            int rowIndex = 1;
            var importErrorMessages = new List<string>();

            foreach (var row in excelContent)
            {
                var errorMessage = new StringBuilder();

                ShippingMethod method = ShippingMethod.Get(Convert.ToInt32(row[0]));

                JObject countryData = new JObject();
                foreach (var data in row.Skip(2).Select((value, index) => new { value, index }))
                {
                    countryData.Add(codes[data.index], Convert.ToBoolean(data.value));
                }
                method.CountryData = JsonConvert.SerializeObject(countryData);

                if (errorMessage.Length > 0)
                {
                    errorCount += 1;
                    importErrorMessages.Add(string.Format("第 {0} 列資料發現錯誤：{1}{2}", rowIndex, errorMessage, "\r\n"));
                }

                importData.Add(method);
                rowIndex += 1;
            }

            return returnResult(errorCount, importData.Count, importErrorMessages);
        }

        public CheckResult CheckImportData(List<Skus> importData)
        {
            if (!checkExists()) return result;

            Skus = new GenericRepository<Skus>(db);

            excelFile.AddMapping<Skus>(s => s.Sku, "Sku");
            excelFile.AddMapping<Skus>(s => s.ProductName, "ProductName");
            excelFile.AddMapping<Skus>(s => s.Battery, "Battery");
            excelFile.AddMapping<Skus>(s => s.Export, "Export");
            excelFile.AddMapping<Skus>(s => s.ExportMethod, "ExportMethod");
            excelFile.AddMapping<Skus>(s => s.PurchaseInvoice, "PurchaseInvoice");
            excelFile.AddMapping<Skus>(s => s.Weight, "Weight");
            excelFile.AddMapping<Skus>(s => s.ShippingWeight, "ShippingWeight");
            var excelContent = excelFile.Worksheet<Skus>("品號");

            int errorCount = 0;
            int rowIndex = 1;
            var importErrorMessages = new List<string>();

            foreach (var row in excelContent)
            {
                var errorMessage = new StringBuilder();

                Skus sku = Skus.Get(row.Sku);
                sku.ProductName = row.ProductName;
                sku.Battery = row.Battery;
                sku.Export = row.Export;
                sku.ExportMethod = row.ExportMethod;
                sku.PurchaseInvoice = row.PurchaseInvoice;
                sku.Weight = row.Weight;
                sku.ShippingWeight = row.ShippingWeight;

                //if (string.IsNullOrWhiteSpace(row.Sku)) errorMessage.Append("Sku - 不可空白.");

                importData.Add(sku);
                rowIndex += 1;
            }

            return returnResult(errorCount, importData.Count, importErrorMessages);
        }

        public void SaveImportData<TEntity>(IEnumerable<TEntity> importData) where TEntity : class
        {
            try
            {
                IRepository<TEntity> dataDB = new GenericRepository<TEntity>(db);
                using (dataDB)
                {
                    foreach (var item in importData)
                    {
                        dataDB.Update(item);
                    }

                    dataDB.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private bool checkExists()
        {
            if (!targetFile.Exists)
            {
                result.ID = Guid.NewGuid();
                result.Success = false;
                result.ErrorCount = 0;
                result.ErrorMessage = "匯入的資料檔案不存在";

                return false;
            }

            return true;
        }

        private CheckResult returnResult(int errorCount, int RowCount, List<string> importErrorMessages)
        {
            try
            {
                result.ID = Guid.NewGuid();
                result.Success = errorCount.Equals(0);
                result.RowCount = RowCount;
                result.ErrorCount = errorCount;

                string allErrorMessage = string.Empty;

                foreach (var message in importErrorMessages)
                {
                    allErrorMessage += message;
                }

                result.ErrorMessage = allErrorMessage;

                return result;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }

    public class ExportExcelResult : ActionResult
    {
        public string SheetName { get; set; }
        public string FileName { get; set; }
        public DataTable ExportData { get; set; }

        public ExportExcelResult()
        {

        }

        public override void ExecuteResult(ControllerContext context)
        {
            if (ExportData == null)
            {
                throw new InvalidDataException("ExportData");
            }
            if (string.IsNullOrWhiteSpace(this.SheetName))
            {
                this.SheetName = "Sheet1";
            }
            if (string.IsNullOrWhiteSpace(this.FileName))
            {
                this.FileName = string.Concat(
                    "ExportData_",
                    DateTime.Now.ToString("yyyyMMddHHmmss"),
                    ".xlsx");
            }

            this.ExportExcelEventHandler(context);
        }

        private void ExportExcelEventHandler(ControllerContext context)
        {
            try
            {
                var workbook = new XLWorkbook();

                if (this.ExportData != null)
                {
                    context.HttpContext.Response.Clear();

                    // 編碼
                    context.HttpContext.Response.ContentEncoding = Encoding.UTF8;

                    // 設定網頁ContentType
                    context.HttpContext.Response.ContentType =
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                    // 匯出檔名
                    var browser = context.HttpContext.Request.Browser.Browser;
                    var exportFileName = browser.Equals("Firefox", StringComparison.OrdinalIgnoreCase)
                        ? this.FileName
                        : HttpUtility.UrlEncode(this.FileName, Encoding.UTF8);

                    context.HttpContext.Response.AddHeader(
                        "Content-Disposition",
                        string.Format("attachment;filename={0}", exportFileName));

                    // Add all DataTables in the DataSet as a worksheets
                    workbook.Worksheets.Add(this.ExportData, this.SheetName);

                    using (var memoryStream = new MemoryStream())
                    {
                        workbook.SaveAs(memoryStream);
                        memoryStream.WriteTo(context.HttpContext.Response.OutputStream);
                        memoryStream.Close();
                    }
                }
                workbook.Dispose();
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }

    public class CheckResult
    {
        public Guid ID { get; set; }
        public bool Success { get; set; }
        public int RowCount { get; set; }
        public int ErrorCount { get; set; }
        public string ErrorMessage { get; set; }
    }
}