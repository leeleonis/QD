﻿using CarrierApi.Winit;
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
        private bool disposed = false;
        private HttpSessionStateBase Session;

        public OrderPreset(HttpSessionStateBase session) : this(session, null) { }

        public OrderPreset(HttpSessionStateBase session, Orders order)
        {
            db = new QDLogisticsEntities();
            Orders = new GenericRepository<Orders>(db);
            Packages = new GenericRepository<Packages>(db);
            Items = new GenericRepository<Items>(db);
            Preset = new GenericRepository<Preset>(db);

            PresetList = db.Preset.AsNoTracking().Where(p => p.IsEnable && p.IsVisible).OrderBy(p => p.Type).OrderBy(p => p.Priority).ToList();
            MethodOfService = db.Services.AsNoTracking().Where(s => s.IsEnable.Value && s.ShippingMethod.HasValue).ToDictionary(s => s.ServiceCode, s => s.ShippingMethod.Value);
            MethodOfService.Add("Expedited", 9);
            this.Order = order;
            this.Session = session;
        }

        public void Init(int OrderID)
        {
            this.Order = Orders.Get(OrderID);
        }

        public void Save()
        {
            if (Order == null) throw new Exception("Not found order!");

            MyHelp.Log("Orders", Order.OrderID, string.Format("訂單下載 - 預設訂單【{0}】資料", Order.OrderID), Session);

            foreach (Packages package in Order.Packages.Where(p => p.IsEnable.Value && p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.訂單管理)).ToList())
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
                package.DLCurrency = Order.OrderCurrencyCode.Value;
                package.ShippingMethod = MethodOfService.ContainsKey(Order.ShippingServiceSelected) ? MethodOfService[Order.ShippingServiceSelected] : 9;
                package.FirstMile = 35;
                package.Export = itemList.Min(i => methodOfSku.ContainsKey(i.ProductID) ? methodOfSku[i.ProductID]["export"] : 0);
                package.ExportMethod = itemList.Min(i => methodOfSku.ContainsKey(i.ProductID) ? methodOfSku[i.ProductID]["exportMethod"] : 0);
                package.UploadTracking = true;

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
                                subTotal = itemList.Sum(i => i.UnitPrice.Value * i.Qty.Value);
                                package.DeclaredTotal = preset.ValueType.Equals(0) ? subTotal * preset.Value / 100 : preset.Value;
                                if (itemList.Sum(i => i.Qty.Value).Equals(1))
                                {
                                    Items item = itemList.First();
                                    item.DeclaredValue = package.DeclaredTotal;
                                    Items.Update(item, item.ID);
                                }
                                else
                                {
                                    foreach (Items item in itemList)
                                    {
                                        item.DeclaredValue = item.UnitPrice.Value * (package.DeclaredTotal / subTotal);
                                        Items.Update(item, item.ID);
                                    }
                                }
                                break;
                            case 4: //Warehouse & shipping method
                                package.ShippingMethod = preset.MethodID;
                                foreach (Items item in itemList)
                                {
                                    item.ShipFromWarehouseID = preset.WarehouseID;
                                    Items.Update(item, item.ID);
                                }
                                break;
                            case 5: //DL Declare Value
                                subTotal = itemList.Sum(i => i.UnitPrice.Value * i.Qty.Value);
                                package.DLDeclaredTotal = preset.ValueType.Equals(0) ? subTotal * preset.Value / 100 : preset.Value;
                                if (itemList.Sum(i => i.Qty.Value).Equals(1))
                                {
                                    Items item = itemList.First();
                                    item.DLDeclaredValue = package.DLDeclaredTotal;
                                    Items.Update(item, item.ID);
                                }
                                else
                                {
                                    foreach (Items item in itemList)
                                    {
                                        item.DLDeclaredValue = item.UnitPrice.Value * (package.DLDeclaredTotal / subTotal);
                                        Items.Update(item, item.ID);
                                    }
                                }
                                break;
                            case 6: //Warehouse & First Mile
                                package.FirstMile = preset.MethodID;
                                foreach (Items item in itemList)
                                {
                                    item.ShipFromWarehouseID = preset.WarehouseID;
                                    Items.Update(item, item.ID);
                                }
                                break;
                        }

                        needDispatch = preset.IsDispatch;
                    }
                }

                Packages.Update(package, package.ID);
                Packages.SaveChanges();

                if (needDispatch && Order.StatusCode.Equals(OrderStatusCode.InProcess) && Order.PaymentStatus.Equals(OrderPaymentStatus1.Charged)) Dispatch(package);
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
                                    MyHelp.Log("Orders", packageData.OrderID, "更新訂單包裹的出貨倉", session);

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

                                        if (packageData.Items.First(i => i.IsEnable.Value).ShipWarehouses.Name.Equals("TWN"))
                                        {
                                            int[] itemIDs = packageData.Items.Where(i => i.IsEnable.Value).Select(i => i.ID).ToArray();
                                            List<PickProduct> pickList = PickProduct.GetAll(true).Where(p => itemIDs.Contains(p.ItemID.Value)).ToList();
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
                                    Winit_API winit = new Winit_API();
                                    winit.CancelOutboundOrder(package.WinitNo);
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
            List<Items> itemList = package.Items.Where(i => i.IsEnable.Value).ToList();
            List<Skus> skuList = itemList.Select(i => i.Skus).ToList();

            int zipCode;
            Dictionary<string, StockKeepingUnit.SkuData> SkuData = new Dictionary<string, StockKeepingUnit.SkuData>();
            if (!preset.Weight.Equals(0))
            {
                using (StockKeepingUnit stock = new StockKeepingUnit())
                {
                    var IDs = itemList.Select(i => i.ProductID).Distinct().ToArray();
                    SkuData = stock.GetSkuData(IDs);
                }
            }

            bool total = preset.Total.Equals(0) || Compare(preset.TotalType, preset.Total, itemList.Sum(i => i.Qty * i.UnitPrice).Value);
            bool country = string.IsNullOrEmpty(preset.Country) || preset.Country.Equals(Order.Addresses.CountryCode);
            bool state = string.IsNullOrEmpty(preset.State) || preset.State.Equals(Order.Addresses.StateCode);
            bool zipCodeFrom = preset.ZipCodeFrom.Equals(0) || (int.TryParse(Order.Addresses.PostalCode, out zipCode) && preset.ZipCodeFrom <= int.Parse(Order.Addresses.PostalCode));
            bool zipCodeTo = preset.ZipCodeTo.Equals(0) || (int.TryParse(Order.Addresses.PostalCode, out zipCode) && preset.ZipCodeTo >= int.Parse(Order.Addresses.PostalCode));
            bool company = preset.CompanyID.Equals(0) || preset.CompanyID.Equals(Order.CompanyID.Value);
            bool source = preset.SourceID.Equals(0) || preset.SourceID.Equals(Order.OrderSource.Value + 1);
            bool qty = preset.Amount.Equals(0) || Compare(preset.AmountType, preset.Amount, itemList.Sum(i => i.Qty).Value);
            bool method = string.IsNullOrEmpty(preset.ShippingMethod) || preset.ShippingMethod.Equals(Order.ShippingServiceSelected);
            bool sku = string.IsNullOrEmpty(preset.Sku) || itemList.Any(i => i.ProductID.Equals(preset.Sku));
            bool suffix = string.IsNullOrEmpty(preset.Suffix) || itemList.Any(i => i.ProductID.Substring(i.ProductID.Length - preset.Suffix.Length).Equals(preset.Suffix));
            bool productType = preset.ProductType.Equals(0) || skuList.Any(s => s.ProductTypeID.Value.Equals(preset.ProductType));
            bool brand = preset.ProductType.Equals(0) || skuList.Any(s => s.Brand.Value.Equals(preset.Brand));
            bool battery = !preset.Battery.HasValue || skuList.Any(s => s.Battery.Value.Equals(preset.Battery.Value));
            bool weight = preset.Weight.Equals(0) || Compare(preset.WeightType, preset.Weight, itemList.Sum(i => i.Qty * (SkuData.ContainsKey(i.ProductID) ? SkuData[i.ProductID].Weight : i.Skus.ShippingWeight)).Value);

            bool checkStock = !(preset.CheckSkuStock ?? false);
            if (!checkStock)
            {
                checkStock = true;
                using (StockKeepingUnit stock = new StockKeepingUnit())
                {
                    foreach(Items item in itemList)
                    {
                        stock.SetItemData(item.ID);
                        checkStock &= (stock.CheckInventory() >= item.Qty);
                    }
                }
            }

            return total && country && state && zipCodeFrom && zipCodeTo && company && source && method && sku && suffix && productType && brand && battery && weight && checkStock;
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                if (Orders != null) Orders.Dispose();
                if (Packages != null) Packages.Dispose();
                if (Items != null) Items.Dispose();
                if (Preset != null) Preset.Dispose();
            }

            db = null;
            Session = null;
            disposed = true;
        }
    }
}