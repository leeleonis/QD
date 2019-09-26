using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using SellerCloud_WebService;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;
using QDLogistics.OrderService;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Threading.Tasks;

namespace QDLogistics.Commons
{
    public class SyncProcess
    {
        private QDLogisticsEntities db;
        private IRepository<Orders> Orders;
        private IRepository<Addresses> Addresses;
        private IRepository<Payments> Payments;
        private IRepository<Packages> Packages;
        private IRepository<Items> Items;
        private IRepository<BundleItems> BundleItems;
        private IRepository<SerialNumbers> SerialNumbers;
        private IRepository<PurchaseItemReceive> PurchaseItems;
        private IRepository<Warehouses> Warehouses;

        private DateTime SyncOn;
        private DateTime Today;

        private Orders orderData;
        private SC_WebService SCWS;
        private HttpSessionStateBase Session;
        public TaskFactory Factory;

        public SyncProcess(HttpSessionStateBase Session)
        {
            db = new QDLogisticsEntities();
            Orders = new GenericRepository<Orders>(db);
            Addresses = new GenericRepository<Addresses>(db);
            Payments = new GenericRepository<Payments>(db);
            Packages = new GenericRepository<Packages>(db);
            Items = new GenericRepository<Items>(db);
            BundleItems = new GenericRepository<BundleItems>(db);
            SerialNumbers = new GenericRepository<SerialNumbers>(db);
            PurchaseItems = new GenericRepository<PurchaseItemReceive>(db);

            SyncOn = DateTime.UtcNow;
            Today = new TimeZoneConvert().ConvertDateTime(EnumData.TimeZone.EST);

            this.Session = Session;
            string UserName = MyHelp.get_session("ApiUserName", Session, "tim@weypro.com").ToString();
            string Password = MyHelp.get_session("ApiPassword", Session, "timfromweypro").ToString();
            SCWS = new SC_WebService(UserName, Password);
        }

        public SyncProcess(HttpSessionStateBase Session, TaskFactory Factory) : this(Session)
        {
            this.Factory = Factory;
        }

        public string Sync_Orders(int day)
        {
            string Message = "";

            try
            {
                MyHelp.Log("Orders", null, string.Format("同步{0}天訂單資料", day), Session);

                if (!SCWS.Is_login) throw new Exception("SC is not logged in!");

                OrderStateInfo[] SC_OrderStateInfoList = SCWS.Search_Order(Today.AddDays(-day), Today);

                if (!SC_OrderStateInfoList.Any()) throw new Exception("Not found order!");

                List<Orders> orderList = Orders.GetAll(true).Where(o => SC_OrderStateInfoList.Select(info => info.ID).ToArray().Contains(o.OrderID)).ToList();
                List<Order> SC_Orders = new List<Order>();
                List<int> presetList = new List<int>();
                int[] dropshipWarehouse = db.Warehouses.Where(w => w.IsEnable.Value && w.WarehouseType.Value.Equals((int)WarehouseTypeType.DropShip)).Select(w => w.ID).ToArray();

                List<OrderSerialNumber> SC_SerialNumbers = new List<OrderSerialNumber>();

                int[] ignoreOrderIDs = new int[] { };
                foreach (OrderStateInfo orderStateInfo in SC_OrderStateInfoList.Where(o => o.DropShipStatus == DropShipStatusType1.None && !ignoreOrderIDs.Contains(o.ID)))
                {
                    OrderData order = SCWS.Get_OrderData(orderStateInfo.ID);
                    SC_Orders.Add(order.Order);

                    if (!orderList.Any(o => o.OrderID.Equals(orderStateInfo.ID)))
                    {
                        Addresses address = new Addresses() { IsEnable = true };
                        Addresses.Create(address);
                        Addresses.SaveChanges();

                        orderData = new Orders() { OrderID = orderStateInfo.ID, ShippingAddress = address.Id, eBayUserID = order.User.eBayUserID };
                        Orders.Create(orderData);
                        Orders.SaveChanges();

                        orderList.Add(orderData);
                        SC_SerialNumbers.AddRange(order.Serials);

                        presetList.Add(orderData.OrderID);
                    }
                    else
                    {
                        if (orderList.Any(o => o.OrderID.Equals(orderStateInfo.ID) && (!o.StatusCode.Equals((int)OrderStatusCode.InProcess) || !o.PaymentStatus.Equals((int)OrderPaymentStatus1.Charged))))
                        {
                            if (orderStateInfo.StatusCode.Equals(OrderStatusCode.InProcess) && orderStateInfo.PaymentStatus.Equals(OrderPaymentStatus1.Charged))
                            {
                                presetList.Add(order.Order.ID);
                            }
                        }

                        if (db.Packages.AsNoTracking().Any(p => p.IsEnable.Value && p.OrderID.Value.Equals(orderStateInfo.ID) && !p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨) && !p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.保留中)))
                        {
                            if (db.Items.AsNoTracking().Any(i => i.IsEnable.Value && i.OrderID.Value.Equals(orderStateInfo.ID) && !(dropshipWarehouse.Contains(i.ShipFromWarehouseID.Value) && i.SerialNumbers.Any())))
                            {
                                SC_SerialNumbers.AddRange(order.Serials);
                            }
                        }
                    }
                }


                List<Orders> orderDatas = orderList.Where(o => SC_Orders.Select(order => order.ID).Contains(o.OrderID)).ToList();
                Check_Order(orderDatas, SC_Orders);
                Orders.SaveChanges();

                List<Payments> paymentDatas = orderDatas.SelectMany(o => o.Payments.Where(p => p.IsEnable.Equals(true))).ToList();
                Check_Payment(paymentDatas, SC_Orders.SelectMany(o => o.Payments).ToList());
                Orders.SaveChanges();

                int[] PackageIDs = SC_Orders.SelectMany(o => o.Items).Select(i => i.PackageID).Distinct().ToArray();
                List<Packages> packageDatas = orderDatas.SelectMany(o => o.Packages.Where(p => p.IsEnable.Equals(true))).ToList();
                Check_Package(packageDatas, SC_Orders.SelectMany(o => o.Packages).Where(p => PackageIDs.Contains(p.ID)).ToList());
                Orders.SaveChanges();

                List<Items> itemDatas = packageDatas.SelectMany(p => p.Items.Where(i => i.IsEnable.Equals(true))).ToList();
                Check_Item(itemDatas, SC_Orders.SelectMany(o => o.Items).ToList());
                Orders.SaveChanges();

                int[] OrderIDs = packageDatas.Where(p => !p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨) && !p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.保留中))
                    .SelectMany(p => p.Items.Where(i => !dropshipWarehouse.Contains(i.ShipFromWarehouseID.Value))).Select(i => i.OrderID.Value).ToArray();
                List<SerialNumbers> serialNumberDatas = SerialNumbers.GetAll().Where(serial => OrderIDs.Contains(serial.OrderID.Value)).ToList();
                Check_Serial(serialNumberDatas, SC_SerialNumbers);
                Orders.SaveChanges();

                foreach (OrderStateInfo orderStateInfo in SC_OrderStateInfoList.Where(o => o.DropShipStatus != DropShipStatusType1.None))
                {
                    if (orderList.Any(o => o.OrderID.Equals(orderStateInfo.ID)))
                    {
                        orderStateInfo.StatusCode = OrderStatusCode.Completed;
                        orderData = orderList.First(o => o.OrderID.Equals(orderStateInfo.ID));
                        Update_OrderState(orderData, orderStateInfo);
                        Orders.Update(orderData);
                    }
                }

                Orders.SaveChanges();
                MyHelp.Log("Orders", null, "訂單資料同步完成", Session);

                if (presetList.Any())
                {
                    using (OrderPreset preset = new OrderPreset(Session))
                    {
                        preset.Factory = this.Factory;
                        using (StockKeepingUnit Stock = new StockKeepingUnit())
                        {
                            foreach (int OrderID in presetList)
                            {
                                preset.Init(OrderID);
                                preset.Save();

                                try
                                {
                                    Stock.RecordOrderSkuStatement(OrderID, "New");
                                }
                                catch (Exception e)
                                {
                                    string errorMsg = string.Format("傳送訂單狀態至PO系統失敗，請通知處理人員：{0}", e.InnerException != null ? e.InnerException.Message.Trim() : e.Message.Trim());
                                    MyHelp.Log("SkuStatement", OrderID, string.Format("訂單【{0}】{1}", OrderID, errorMsg), Session);
                                }
                            }
                        }
                    }
                }
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(x => x.ErrorMessage);
                Message = string.Join("; ", errorMessages);
            }
            catch (Exception e)
            {
                MyHelp.ErrorLog(e, string.Format("同步{0}天訂單資料失敗", day));
                Message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
            }

            return Message;
        }

        public string Sync_Order(int OrderID)
        {
            string Message = "";

            try
            {
                MyHelp.Log("Orders", OrderID, "訂單資料同步開始", Session);

                if (!SCWS.Is_login) throw new Exception("SC is not logged in!");

                OrderStateInfo orderStateInfo = SCWS.Get_OrderStatus(OrderID);

                if (orderStateInfo == null) throw new Exception("Not found order!");

                orderData = Orders.Get(OrderID);

                if (orderData == null)
                {
                    Addresses address = new Addresses() { IsEnable = true };
                    Addresses.Create(address);
                    Addresses.SaveChanges();

                    orderData = new Orders() { OrderID = orderStateInfo.ID, ShippingAddress = address.Id };
                    Orders.Create(orderData);
                    Orders.SaveChanges();
                }

                if (orderStateInfo.DropShipStatus == DropShipStatusType1.None)
                {
                    OrderData order = SCWS.Get_OrderData(OrderID);
                    orderData.eBayUserID = order.User.eBayUserID;

                    Order orderDetail = order.Order;
                    DataProcess.SetOrderData(orderData, orderDetail);

                    DataProcess.SetAddressData(orderData.Addresses, orderDetail.ShippingAddress, orderDetail.BillingAddress);

                    Check_Payment(orderData.Payments.Where(p => p.IsEnable.Equals(true)).ToList(), orderDetail.Payments.ToList());

                    int[] PackageIDs = orderDetail.Items.Select(i => i.PackageID).ToArray();
                    Check_Package(orderData.Packages.Where(p => p.IsEnable.Equals(true)).ToList(), orderDetail.Packages.Where(p => PackageIDs.Contains(p.ID)).ToList());

                    Check_Item(orderData.Items.Where(i => i.IsEnable.Equals(true)).ToList(), orderDetail.Items.ToList());

                    if (orderData.Packages.All(p => p.IsEnable.Value && !p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.待出貨) && !p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.保留中)))
                    {
                        if (orderData.Packages.SelectMany(p => p.Items.Select(i => i.ShipWarehouses)).All(w => !w.WarehouseType.Equals(WarehouseTypeType.DropShip)))
                        {
                            OrderSerialNumber[] SC_SerialNumbers = SCWS.Get_OrderItem_Serial(OrderID);
                            List<SerialNumbers> serialNumberDatas = orderData.Packages.Where(p => p.IsEnable.Value).SelectMany(p => p.Items.Where(i => i.IsEnable.Value).SelectMany(i => i.SerialNumbers)).ToList();
                            Check_Serial(serialNumberDatas, SC_SerialNumbers.ToList());
                        }
                    }
                }
                else
                {
                    orderStateInfo.StatusCode = OrderStatusCode.Completed;
                    Update_OrderState(orderData, orderStateInfo);
                }

                Orders.Update(orderData);
                Orders.SaveChanges();
                MyHelp.Log("Orders", OrderID, "訂單資料同步完成", Session);
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(x => x.ErrorMessage);
                Message = string.Join("; ", errorMessages);
            }
            catch (Exception e)
            {
                MyHelp.ErrorLog(e, string.Format("訂單【{0}】資料同步失敗", orderData.OrderID), orderData.OrderID.ToString());
                Message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
            }

            return Message;
        }

        private void Check_Order(List<Orders> orderDatas, List<Order> SC_Orders)
        {
            List<Orders> orderList = SC_Orders.Select(o => DataProcess.SetOrderData(new Orders() { OrderID = o.ID }, o)).ToList();

            // Update order data
            IEnumerable<Orders> updateOrder = orderDatas.Except(orderList, new OrdersComparer());
            foreach (Orders order in updateOrder)
            {
                Order orderDetail = SC_Orders.First(o => o.ID == order.OrderID);
                Address addressDetail = orderDetail.ShippingAddress;

                Orders.Update(DataProcess.SetOrderData(order, orderDetail), order.OrderID);
                Addresses.Update(DataProcess.SetAddressData(order.Addresses, addressDetail, orderDetail.BillingAddress), order.ShippingAddress);
            }
        }

        private void Check_Payment(List<Payments> paymentDatas, List<OrderPayment> SC_Payments)
        {
            List<Payments> paymentList = SC_Payments.Select(p => DataProcess.SetPaymentData(new Payments() { IsEnable = true, ID = p.ID }, p)).ToList();

            // New payment data
            IEnumerable<Payments> newPayment = paymentList.Except(paymentDatas);
            foreach (Payments payment in newPayment)
            {
                Payments.Create(payment);
            }

            // Old payment data
            IEnumerable<Payments> oldPayment = paymentDatas.Except(paymentList);
            foreach (Payments payment in oldPayment)
            {
                payment.IsEnable = false;
                Payments.Update(payment, payment.ID);
            }

            // Update payment data
            IEnumerable<Payments> updatePayment = paymentDatas.Except(oldPayment).Except(paymentList, new PaymentComparer());
            foreach (Payments payment in updatePayment)
            {
                Payments.Update(DataProcess.SetPaymentData(payment, SC_Payments.First(p => p.ID == payment.ID)), payment.ID);
            }
        }

        private void Check_Package(List<Packages> packageDatas, List<Package> SC_Packages)
        {
            List<Packages> packageList = SC_Packages.Select(p => DataProcess.SetPackageData(new Packages() { IsEnable = true, ID = p.ID }, p)).ToList();

            // New package data
            IEnumerable<Packages> newPackage = packageList.Except(packageDatas);
            foreach (Packages package in newPackage)
            {
                Packages.Create(package);
            }

            // Old package data
            IEnumerable<Packages> oldPackage = packageDatas.Except(packageList);
            foreach (Packages package in oldPackage)
            {
                package.IsEnable = false;
                Packages.Update(package, package.ID);
            }

            // Update package data
            IEnumerable<Packages> updatePackage = packageDatas.Except(oldPackage).Except(packageList, new PackageComparer());
            foreach (Packages package in updatePackage)
            {
                Packages.Update(DataProcess.SetPackageData(package, SC_Packages.First(p => p.ID == package.ID)), package.ID);
            }
        }

        private void Check_Item(List<Items> itemDatas, List<OrderItem> SC_Items)
        {
            List<Items> itemList = SC_Items.Where(i => !i.PackageID.Equals(0)).Select(i => DataProcess.SetItemData(new Items() { IsEnable = true, ID = i.ID }, i)).ToList();

            if (SC_Items.Any(i => i.PackageID.Equals(0)))
            {
                foreach (OrderItem orderItem in SC_Items.Where(i => i.PackageID.Equals(0)).ToArray())
                {
                    Items item = DataProcess.SetItemData(new Items() { IsEnable = true, ID = orderItem.ID }, orderItem);

                    if (SC_Items.Any(i => !i.ID.Equals(orderItem.ID) && i.OrderID.Equals(orderItem.OrderID)))
                    {
                        item.PackageID = SC_Items.First(i => !i.ID.Equals(orderItem.ID) && i.OrderID.Equals(orderItem.OrderID)).PackageID;
                    }

                    if (!item.PackageID.Equals(0))
                    {
                        itemList.Add(item);
                    }

                }
            }

            // New item data
            IEnumerable<Items> newItem = itemList.Except(itemDatas);
            foreach (Items item in newItem)
            {
                if (Items.Get(item.ID) != null)
                {
                    Items.Update(item, item.ID);
                }
                else
                {
                    Items.Create(item);
                }
            }

            // Old item data
            IEnumerable<Items> oldItem = itemDatas.Except(itemList);
            foreach (Items item in oldItem)
            {
                item.IsEnable = false;
                Items.Update(item, item.ID);
            }

            // Update item data
            IEnumerable<Items> updateItem = itemDatas.Except(oldItem).Except(itemList, new ItemComparer());
            foreach (Items item in updateItem)
            {
                Items.Update(DataProcess.SetItemData(item, SC_Items.First(i => i.ID == item.ID)), item.ID);
            }


            Check_BundleItem(itemDatas.SelectMany(i => i.BundleItems).ToList(), SC_Items.SelectMany(i => i.BundleItems).ToList());
        }

        private void Check_BundleItem(List<BundleItems> bundleItemDatas, List<OrderBundleItem1> SC_BundleItems)
        {
            List<BundleItems> bundleItemList = SC_BundleItems.Select(bundleItem => DataProcess.SetBundleItemData(new BundleItems() { IsEnable = true, ID = bundleItem.ID }, bundleItem)).ToList();

            // New item data
            IEnumerable<BundleItems> newBundleItem = bundleItemList.Except(bundleItemDatas);
            foreach (BundleItems bundleItem in newBundleItem)
            {
                BundleItems.Create(bundleItem);
            }

            // Old item data
            IEnumerable<BundleItems> oldBundleItem = bundleItemDatas.Except(bundleItemList);
            foreach (BundleItems bundleItem in oldBundleItem)
            {
                bundleItem.IsEnable = false;
                BundleItems.Update(bundleItem, bundleItem.ID);
            }

            // Update item data
            IEnumerable<BundleItems> updateBundleItem = bundleItemDatas.Except(oldBundleItem).Except(bundleItemList, new BundleItemComparer());
            foreach (BundleItems bundleItem in updateBundleItem)
            {
                BundleItems.Update(DataProcess.SetBundleItemData(bundleItem, SC_BundleItems.First(bi => bi.ID == bundleItem.ID)), bundleItem.ID);
            }
        }

        private void Check_Serial(List<SerialNumbers> serialNumberDatas, List<OrderSerialNumber> SC_SerialNumbers)
        {
            List<SerialNumbers> serialNumberList = SC_SerialNumbers.Select(serial => DataProcess.SetSerialNumberData(new SerialNumbers() { }, serial)).ToList();

            // New serialNumber data
            var newSerialNumber = serialNumberList.Except(serialNumberDatas).ToList();
            foreach (SerialNumbers serialNumber in newSerialNumber)
            {
                SerialNumbers.Create(serialNumber);
            }

            // Old serialNumber data
            var oldSerialNumber = serialNumberDatas.Except(serialNumberList).ToList();
            //foreach (SerialNumbers serialNumber in oldSerialNumber)
            //{
            //    SerialNumbers.Delete(serialNumber);
            //}

            // Update serialNumber data
            var updateSerialNumber = serialNumberDatas.Except(oldSerialNumber).Except(serialNumberList, new SerialNumberComparer()).ToList();
            foreach (SerialNumbers serialNumber in updateSerialNumber)
            {
                SerialNumbers.Update(DataProcess.SetSerialNumberData(serialNumber, SC_SerialNumbers.First(s => s.SerialNumber == serialNumber.SerialNumber && s.OrderItemID == serialNumber.OrderItemID)));
            }
        }

        private void Update_OrderState(Orders orderData, OrderStateInfo orderInfo)
        {
            orderData.StatusCode = (int)orderInfo.StatusCode;
            orderData.ShippingStatus = (int)orderInfo.ShippingStatus;
            orderData.IsConfirmed = orderInfo.IsConfirmed;
            orderData.ConfirmBy = orderInfo.ConfirmedBy;
            orderData.ConfirmOn = orderInfo.ConfirmedOn;
        }

        public string Update_Tracking(Packages package)
        {
            string Message = "";

            try
            {
                MyHelp.Log("Packages", package.ID, string.Format("訂單【{0}】包裹SC更新", package.OrderID), Session);

                if (!SCWS.Is_login) throw new Exception("SC is not logged in!");

                OrderData orderData = SCWS.Get_OrderData(package.OrderID.Value);
                Order SC_order = orderData.Order;
                Package SC_package = SC_order.Packages.FirstOrDefault(p => p.ID.Equals(package.ID));

                if (SC_package == null)
                {
                    MyHelp.Log("Packages", package.ID, "Not find package on SC website!", Session);

                    package.IsEnable = false;
                    SC_package = SC_order.Packages.First(p => p.ID.Equals(SC_order.Items.First(i => i.ID.Equals(package.Items.First(ii => ii.IsEnable.Value).ID)).PackageID));
                    var newPackage = db.Packages.AsNoTracking().First(p => p.ID.Equals(package.ID));
                    newPackage.ID = SC_package.ID;
                    db.Packages.Add(newPackage);
                    foreach (var item in package.Items)
                    {
                        item.PackageID = newPackage.ID;
                    }
                    foreach (var pick in db.PickProduct.Where(pick => pick.PackageID.Value.Equals(package.ID)))
                    {
                        pick.PackageID = newPackage.ID;
                    }
                    if (package.Label != null) package.Label.PackageID = newPackage.ID;
                    db.SaveChanges();

                    MyHelp.Log("Packages", package.ID, string.Format("Change package {0} to {1}", package.ID, newPackage.ID), Session);
                    package = newPackage;
                }

                string carrier = "";
                try
                {
                    if (package.Method == null) throw new Exception("Not find method!");
                    if (package.Method.Carriers == null) throw new Exception("Not find carrir");
                    carrier = package.Method.Carriers.Name;
                }
                catch (Exception e)
                {
                    MyHelp.Log("Packages", package.ID, e.Message, Session);
                    carrier = db.ShippingMethod.Find(package.ShippingMethod.Value).Carriers.Name;
                }
                SCWS.Update_PackageShippingStatus(SC_package, (package.UploadTracking ? package.TrackingNumber : ""), carrier);

                if (db.Packages.AsNoTracking().Where(p => p.IsEnable.Value && p.OrderID.Value.Equals(package.OrderID.Value)).All(p => p.ProcessStatus.Equals((byte)EnumData.ProcessStatus.已出貨)))
                {
                    if (SCWS.Update_OrderShippingStatus(SC_order, carrier))
                    {
                        var updatePackage = db.Packages.Find(package.ID);
                        updatePackage.WorkDays = 0;
                        var paymentDate = new TimeZoneConvert(package.Orders.Payments.FirstOrDefault()?.AuditDate ?? package.Orders.TimeOfOrder.Value, EnumData.TimeZone.EST).Utc;
                        var updateDate = DateTime.UtcNow;
                        var checkPoint = new DateTime(paymentDate.Year, paymentDate.Month, paymentDate.Day, 7, 0, 0, DateTimeKind.Utc);
                        do
                        {
                            if (paymentDate.CompareTo(checkPoint) < 0)
                                if (!checkPoint.DayOfWeek.Equals(DayOfWeek.Saturday) && !checkPoint.DayOfWeek.Equals(DayOfWeek.Sunday))
                                    updatePackage.WorkDays++;

                            checkPoint = checkPoint.AddDays(1);
                        } while (checkPoint.CompareTo(updateDate) < 0);
                        db.SaveChanges();

                        MyHelp.Log("Packages", package.ID, string.Format("訂單【{0}】SC完成出貨", package.OrderID), Session);
                    }
                }

                foreach (Items item in package.Items.Where(i => i.IsEnable.Value).ToList())
                {
                    if (item.SerialNumbers.Any()) SCWS.Update_ItemSerialNumber(item.ID, item.SerialNumbers.Select(s => s.SerialNumber).ToArray());
                }

                Message = Sync_Order(package.OrderID.Value);
            }
            catch (Exception e)
            {
                string[] serials = package.Items.Where(i => i.IsEnable.Value).SelectMany(i => i.SerialNumbers.Select(s => s.SerialNumber)).ToArray();
                MyHelp.ErrorLog(e, string.Format("訂單包裹【{0}】更新失敗: #serials {1}", package.OrderID, string.Join(",", serials)), package.OrderID.ToString());
                Message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
            }

            return Message;
        }

        public string Update_PurchaseOrder(int PackageID, bool needUpload = true)
        {
            string Message = "";
            Packages package = db.Packages.Find(PackageID);

            try
            {
                MyHelp.Log("Orders", package.OrderID, string.Format("訂單包裹 - PO【{0}】更新", package.POId), Session);

                try
                {
                    using (StockKeepingUnit stock = new StockKeepingUnit())
                    {
                        if (package.POId.HasValue)
                        {
                            stock.CreatePO(package.ID);
                        }
                        stock.RecordShippedOrder(package.ID);
                        MyHelp.Log("Inventory", package.OrderID, string.Format("訂單【{0}】傳送出貨資料至PO系統", package.OrderID), Session);
                    }
                }
                catch (Exception e)
                {
                    string errorMsg = string.Format("傳送出貨資料至PO系統失敗，請通知處理人員：{0}", e.InnerException != null ? e.InnerException.Message.Trim() : e.Message.Trim());
                    MyHelp.Log("Inventory", package.OrderID, string.Format("訂單【{0}】{1}", package.OrderID, errorMsg), Session);
                }

                MyHelp.Log("Orders", package.OrderID, string.Format("訂單包裹 - PO【{0}】更新完成", package.POId), Session);

                if (needUpload) Message = Update_Tracking(package);
            }
            catch (Exception e)
            {
                MyHelp.ErrorLog(e, string.Format("PO【{0}】更新失敗", package.POId), package.OrderID.ToString());
                Message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
            }

            return Message;
        }

        public string Sync_PurchaseItem(string[] ProductIDs)
        {
            string Message = "";

            try
            {
                MyHelp.Log("PurchaseItemReceive", null, "產品序號同步開始", Session);

                List<PurchaseItemReceive> purchaseItemDatas = PurchaseItems.GetAll(true).Where(p => ProductIDs.Contains(p.ProductID)).ToList();
                List<PurchaseItemReceive_All_Response> SC_PurchaseItems = new List<PurchaseItemReceive_All_Response>();

                if (!SCWS.Is_login) throw new Exception("SC is not logged in!");

                foreach (string productID in ProductIDs)
                {
                    SC_PurchaseItems.Add(SCWS.Get_ProductAllSerials(productID));
                }

                Check_PurchaseItem(purchaseItemDatas, SC_PurchaseItems);

                PurchaseItems.SaveChanges();
                MyHelp.Log("PurchaseItemReceive", null, "產品序號同步完成", Session);
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(x => x.ErrorMessage);
                Message = string.Join("; ", errorMessages);
            }
            catch (Exception e)
            {
                Message = e.Message;
            }

            return Message;
        }

        private void Check_PurchaseItem(List<PurchaseItemReceive> purchaseItemDatas, List<PurchaseItemReceive_All_Response> SC_PurchaseItems)
        {
            List<PurchaseItemReceive> purchaseItemList = SC_PurchaseItems.SelectMany(product => product.SerialsList.Select(item => DataProcess.SetPurchaseItemData(new PurchaseItemReceive() { }, item, product.IsRequireSerialScan))).ToList();

            // New purchaseItem data
            IEnumerable<PurchaseItemReceive> newPurchaseItem = purchaseItemList.Except(purchaseItemDatas);
            var count = newPurchaseItem.Count();
            foreach (PurchaseItemReceive purchaseItem in newPurchaseItem)
            {
                PurchaseItems.Create(purchaseItem);
            }

            // Old purchaseItem data
            IEnumerable<PurchaseItemReceive> oldPurchaseItem = purchaseItemDatas.Except(purchaseItemList);
            foreach (PurchaseItemReceive purchaseItem in oldPurchaseItem)
            {
                PurchaseItems.Delete(purchaseItem);
            }
        }

        public string Sync_Warehouse()
        {
            string Message = "";

            try
            {
                MyHelp.Log("Warehouses", null, "開始出貨倉資料同步");

                if (!SCWS.Is_login) throw new Exception("SC is not logged in!");

                Warehouses = new GenericRepository<Warehouses>(db);

                Warehouse[] SC_Warehouse = SCWS.Get_Warehouses();
                List<Warehouses> warehouseData = db.Warehouses.AsNoTracking().ToList();
                List<Warehouses> WarehouseList = SC_Warehouse.Select(w => DataProcess.SetWarehouseData(new Warehouses() { IsEnable = true, ID = w.ID }, w)).ToList();

                IEnumerable<Warehouses> newWarehouse = WarehouseList.Except(warehouseData).ToList();
                foreach (Warehouses warehouse in newWarehouse)
                {
                    Warehouses.Create(warehouse);
                }

                IEnumerable<Warehouses> oldWarehouse = warehouseData.Except(WarehouseList);
                foreach (Warehouses warehouse in oldWarehouse)
                {
                    warehouse.IsEnable = false;
                    Warehouses.Update(warehouse, warehouse.ID);
                }

                IEnumerable<Warehouses> updateWarehouse = warehouseData.Except(oldWarehouse).Except(WarehouseList, new WarehouseComparer());
                foreach (Warehouses warehouse in updateWarehouse)
                {
                    Warehouses.Update(DataProcess.SetWarehouseData(warehouse, SC_Warehouse.First(w => w.ID.Equals(warehouse.ID))), warehouse.ID);
                }

                Warehouses.SaveChanges();

                MyHelp.Log("Warehouses", null, "完成出貨倉資料同步");
            }
            catch (Exception e)
            {
                Message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
            }

            return Message;
        }
    }
}