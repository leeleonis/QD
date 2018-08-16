using Newtonsoft.Json;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;
using QDLogistics.OrderService;
using SellerCloud_WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace QDLogistics.Commons
{
    public class OrderPreset : IDisposable
    {
        private QDLogisticsEntities db;
        private IRepository<Orders> Orders;
        private IRepository<Packages> Packages;
        private IRepository<Items> Items;
        private IRepository<Preset> Preset;

        private Orders Order;
        private List<Preset> PresetList;
        private Dictionary<string, int> MethodOfService;

        public TaskFactory Factory;
        private HttpSessionStateBase Session;

        public OrderPreset(HttpSessionStateBase session) : this(session, null) { }

        public OrderPreset(HttpSessionStateBase session, Orders order)
        {
            db = new QDLogisticsEntities();
            Orders = new GenericRepository<Orders>(db);
            Packages = new GenericRepository<Packages>(db);
            Items = new GenericRepository<Items>(db);
            Preset = new GenericRepository<Preset>(db);

            PresetList = db.Preset.AsNoTracking().Where(p => p.IsEnable && p.IsVisible).ToList();
            MethodOfService = db.Services.AsNoTracking().Where(s => s.IsEnable.Value && s.ShippingMethod.HasValue).ToDictionary(s => s.ServiceCode, s => s.ShippingMethod.Value);
            MethodOfService.Add("Expedited", 9);
            this.Order = order;
            this.Session = session;
        }

        public void init(int OrderID)
        {
            this.Order = Orders.Get(OrderID);
        }

        public void Save()
        {
            if (Order == null) throw new Exception("Not found order!");

            MyHelp.Log("Orders", Order.OrderID, string.Format("訂單下載 - 預設訂單【{0}】資料", Order.OrderID), Session);

            foreach (Packages package in Order.Packages.Where(p => p.IsEnable.Value).ToList())
            {
                bool needDispatch = false;
                decimal subTotal = 0;

                List<Items> itemList = package.Items.Where(i => i.IsEnable.Value).ToList();
                string[] ProductIDs = itemList.Select(i => i.ProductID).ToArray();
                var methodOfSku = db.Skus.AsNoTracking().Where(s => ProductIDs.Contains(s.Sku)).ToDictionary(s => s.Sku, s => new Dictionary<string, byte?>() { { "export", s.Export }, { "exportMethod", s.ExportMethod } });

                package.DeclaredTotal = package.DLDeclaredTotal = itemList.Sum(i => i.UnitPrice.Value * i.Qty.Value);
                foreach (Items item in itemList)
                {
                    item.DeclaredValue = item.DLDeclaredValue = item.UnitPrice.Value;
                    Items.Update(item, item.ID);
                }
                package.ShippingMethod = MethodOfService.ContainsKey(Order.ShippingServiceSelected) ? MethodOfService[Order.ShippingServiceSelected] : 9;
                package.FirstMile = 0;
                package.Export = itemList.Min(i => methodOfSku.ContainsKey(i.ProductID) ? methodOfSku[i.ProductID]["export"] : 0);
                package.ExportMethod = itemList.Min(i => methodOfSku.ContainsKey(i.ProductID) ? methodOfSku[i.ProductID]["exportMethod"] : 0);

                Packages.Update(package, package.ID);
                Packages.SaveChanges();

                foreach (Preset preset in PresetList)
                {
                    if (Valid(package, preset))
                    {
                        switch (preset.Type)
                        {
                            case 1: //Upload Tracking
                                package.UploadTracking = preset.Value.Equals(1);
                                break;
                            case 2: //Rush order
                                Order.RushOrder = preset.Value.Equals(1);
                                Orders.Update(Order, package.OrderID.Value);
                                break;
                            case 3: //Declare Value
                                subTotal = package.Items.Sum(i => i.UnitPrice.Value * i.Qty.Value);
                                package.DeclaredTotal = preset.ValueType.Equals(0) ? subTotal * preset.Value / 100 : preset.Value;
                                foreach (Items item in itemList)
                                {
                                    item.DeclaredValue = item.UnitPrice.Value * (package.DeclaredTotal / subTotal);
                                    Items.Update(item, item.ID);
                                }
                                break;
                            case 4: //warehouse & shipping method
                                package.ShippingMethod = preset.MethodID;
                                foreach (Items item in itemList)
                                {
                                    item.ShipFromWarehouseID = preset.WarehouseID;
                                    Items.Update(item, item.ID);
                                }
                                break;
                            case 5: //DL Declare Value
                                subTotal = package.Items.Sum(i => i.UnitPrice.Value * i.Qty.Value);
                                package.DLDeclaredTotal = preset.ValueType.Equals(0) ? subTotal * preset.Value / 100 : preset.Value;
                                foreach (Items item in itemList)
                                {
                                    item.DLDeclaredValue = item.UnitPrice.Value * (package.DLDeclaredTotal / subTotal);
                                    Items.Update(item, item.ID);
                                }
                                break;
                        }

                        needDispatch = needDispatch || preset.IsDispatch;
                    }
                }

                Packages.Update(package, package.ID);
                Packages.SaveChanges();

                if (needDispatch) Dispatch(package);
            }
        }

        private void Dispatch(Packages package)
        {
            if (Order.StatusCode.Value.Equals((int)OrderStatusCode.InProcess) && Order.PaymentStatus.Equals((int)OrderPaymentStatus.Charged))
            {
                ThreadTask threadTask = new ThreadTask(string.Format("訂單下載 - 自動提交訂單【{0}】至待出貨區", Order.OrderID), Session);
                MyHelp.Log("Orders", Order.OrderID, string.Format("訂單下載 - 自動提交訂單【{0}】至待出貨區", Order.OrderID), Session);

                package.ProcessStatus = (int)EnumData.ProcessStatus.鎖定中;
                Packages.Update(package, package.ID);

                lock (Factory)
                {
                    threadTask.AddWork(Factory.StartNew(Session =>
                    {
                        threadTask.Start();

                        string message = "";
                        using (QDLogisticsEntities db = new QDLogisticsEntities())
                        {
                            IRepository<Packages> Packages = new GenericRepository<Packages>(db);
                            IRepository<PickProduct> PickProduct = new GenericRepository<PickProduct>(db);

                            Packages packageData = Packages.Get(package.ID);

                            try
                            {
                                HttpSessionStateBase session = (HttpSessionStateBase)Session;
                                SC_WebService SCWS = new SC_WebService("tim@weypro.com", "timfromweypro");

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
                                        SCWS.Update_OrderItem(SC_items.First(i => i.ID.Equals(item.ID)));
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

                                    shipProcess.Init(packageData);
                                    var result = shipProcess.Dispatch();

                                    if (result.Status)
                                    {
                                        MyHelp.Log("Orders", packageData.OrderID, "訂單提交完成", session);

                                        byte[] carrierType = new byte[] { (byte)EnumData.CarrierType.DHL, (byte)EnumData.CarrierType.FedEx, (byte)EnumData.CarrierType.IDS };
                                        if (!shipProcess.isDropShip && carrierType.Contains(package.Method.Carriers.CarrierAPI.Type.Value))
                                        {
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
                            }
                            catch (Exception e)
                            {
                                message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                                packageData.ProcessStatus = (int)EnumData.ProcessStatus.訂單管理;

                                if (!string.IsNullOrEmpty(package.WinitNo))
                                {
                                    CarrierApi.Winit.Winit_API winit = new CarrierApi.Winit.Winit_API(package.Method.Carriers.CarrierAPI);
                                    CarrierApi.Winit.Received received = winit.Void(package.WinitNo);
                                    package.WinitNo = null;
                                }

                                Packages.Update(packageData, packageData.ID);
                                Packages.SaveChanges();
                            }
                        }

                        return message;
                    }, Session));
                }
            }
        }

        private bool Valid(Packages package, Preset preset)
        {
            List<Items> itemList = package.Items.Where(i => i.IsEnable.Equals(true)).ToList();

            bool total = preset.Total.Equals(0) || Compare(preset.TotalType, preset.Total, itemList.Sum(i => i.Qty * i.UnitPrice).Value);
            bool country = string.IsNullOrEmpty(preset.Country) || preset.Country.Equals(Order.Addresses.CountryCode);
            bool company = preset.CompanyID.Equals(0) || preset.CompanyID.Equals(Order.CompanyID.Value);
            bool source = preset.SourceID.Equals(0) || preset.SourceID.Equals(Order.OrderSource.Value + 1);
            bool qty = preset.Amount.Equals(0) || Compare(preset.AmountType, preset.Amount, itemList.Sum(i => i.Qty).Value);
            bool method = string.IsNullOrEmpty(preset.ShippingMethod) || preset.ShippingMethod.Equals(Order.ShippingServiceSelected);
            bool sku = string.IsNullOrEmpty(preset.Sku) || itemList.Any(i => i.ProductID.Substring(i.ProductID.Length - preset.Sku.Length).Equals(preset.Sku));
            bool weight = preset.Weight.Equals(0) || Compare(preset.WeightType, preset.Weight, itemList.Sum(i => i.Qty * i.Skus.Weight).Value);

            return total && country && company && source && method && sku && weight;
        }

        private bool Compare(byte type, decimal compare, decimal value)
        {
            bool compareResult = false;

            switch (type)
            {
                case 0:
                    compareResult = value > compare;
                    break;
                case 1:
                    compareResult = value < compare;
                    break;
                case 2:
                    compareResult = value == compare;
                    break;
                case 3:
                    compareResult = value >= compare;
                    break;
                case 4:
                    compareResult = value <= compare;
                    break;
            }

            return compareResult;
        }

        public void Dispose() { }
    }
}