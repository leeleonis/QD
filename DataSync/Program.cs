using DataSync.OrderCreationService;
using DataSync.OrderService;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace DataSync
{
    class Program
    {
        //本機
        static SqlConnection conn = new SqlConnection(Properties.Settings.Default.DataConnectionRootString);


        static SCServiceSoapClient OS_sellerCloud;
        static OrderService.AuthHeader OS_authHeader;
        static ServiceOptions OS_options;
        static SerializableDictionaryOfStringString OS_filters;

        static OrderCreationServiceSoapClient OCS_sellerCloud;
        static OrderCreationService.AuthHeader OCS_authHeader;

        static DateTime SyncOn;
        static DateTime Today;

        static void Main(string[] args)
        {
            OS_sellerCloud = new SCServiceSoapClient();
            OS_sellerCloud.InnerChannel.OperationTimeout = new TimeSpan(0, 15, 0);
            OS_authHeader = new OrderService.AuthHeader();
            OS_options = new ServiceOptions();

            OCS_sellerCloud = new OrderCreationServiceSoapClient();
            OCS_sellerCloud.InnerChannel.OperationTimeout = new TimeSpan(0, 15, 0);
            OCS_authHeader = new OrderCreationService.AuthHeader();

            OS_authHeader.UserName = OCS_authHeader.UserName = "tim@weypro.com";
            OS_authHeader.Password = OCS_authHeader.Password = "timfromweypro";

            SyncOn = DateTime.Now;
            Today = DateTimeWithZone(SyncOn, false);

            Console.WriteLine("--- Recieve <" + args.Count() + "> Arguments ---");

            if (!args.Any()) args = new string[] { "Orders", "1" };

            string SyncType = args[0];
            conn.Open();

            switch (SyncType)
            {
                case "Orders":
                    OrdersSync(Convert.ToInt32(args[1]));
                    break;
                case "Warehouses":
                    WarehousesSync();
                    break;
                case "Services":
                    ServicesSync();
                    break;
                case "Skus":
                    ManufacturersSync();
                    ProductTypeSync();
                    SkusSync();
                    break;
                case "PurchaseItem":
                    string[] productIDs = args[1].ToString().Split(new char[] { '|' });
                    PurchaseItemSync(productIDs);
                    break;
            }

            conn.Close();
        }

        static void OrdersSync(int day)
        {
            Console.WriteLine("--- Start to access Orderss ---");

            List<Orders> dbOrders = GetOrders(day);
            List<Payments> dbPayments = new List<Payments>();
            List<Packages> dbPackages = new List<Packages>();
            List<Items> dbItems = new List<Items>();
            List<SerialNumbers> dbSerialNumbers = new List<SerialNumbers>();
            List<Skus> dbSkus = new List<Skus>();

            List<Orders> OrderList = new List<Orders>();

            List<Orders> Orders = new List<Orders>();
            List<Addresses> Addresses = new List<Addresses>();
            List<Payments> Payments = new List<Payments>();
            List<Packages> Packages = new List<Packages>();
            List<Items> Items = new List<Items>();
            List<BundleItems> BundleItems = new List<BundleItems>();
            List<SerialNumbers> SerialNumbers = new List<SerialNumbers>();
            List<Skus> Skus = new List<Skus>();

            Console.WriteLine("--- Get Orders from " + Today.AddDays(-day).ToString() + " to " + Today.ToString() + " ---");
            var result = OCS_sellerCloud.SearchOrders(OCS_authHeader, Today.AddDays(-day), Today, "", 0);
            if (result.Rows.Count > 0)
            {
                int[] OrderIDs = result.Rows.Cast<DataRow>().Select(o => (int)o.ItemArray.First()).ToArray();
                OrderList = OS_sellerCloud.Orders_GetOrderStates(OS_authHeader, OS_options, OrderIDs).Where(o => o.DropShipStatus == OrderService.DropShipStatusType1.None)
                    .Select(o => new Orders()
                    {
                        OrderID = o.ID,
                        StatusCode = (int)o.StatusCode,
                        PaymentStatus = (int)o.PaymentStatus,
                        ShippingStatus = (int)o.ShippingStatus,
                        IsConfirmed = o.IsConfirmed,
                        ConfirmBy = o.ConfirmedBy,
                        ConfirmOn = o.ConfirmedOn
                    }).ToList();
            }

            Console.WriteLine("--- Get <" + OrderList.Count() + "> Orders from Sellercloud ---");
            Console.WriteLine("--- End to access Orders ---");

            IEnumerable<Orders> newOrders = OrderList.Except(dbOrders).Where(n => n.StatusCode != (int)OrderService.OrderStatusCode.Canceled).ToList();
            Console.WriteLine("--- There are <" + newOrders.Count() + "> new Orders ---");
            if (newOrders.Any())
            {
                OrderData[] newOrderList = OS_sellerCloud.Orders_GetDatas(OS_authHeader, OS_options, newOrders.Select(o => o.OrderID).ToArray());
                Dictionary<int, string> eBayUserIDs = newOrderList.ToDictionary(o => o.OrderPackages.First().OrderID, o => o.User.eBayUserID);
                //Dictionary<int, OrderSerialNumber[]> serialNumbers = newOrderList.ToDictionary(o => o.Order.ID, o=> o.Serials);

                foreach (Orders order in newOrders)
                {
                    OrderCreationService.Order orderDetail = OCS_sellerCloud.GetOrderFull(OCS_authHeader, order.OrderID);

                    Addresses address = new Addresses() { Id = DataProcess.InsertAddress(conn) };
                    Addresses.Add(DataProcess.SetAddressData(address, orderDetail.ShippingAddress));

                    order.ShippingAddress = address.Id;
                    order.eBayUserID = eBayUserIDs[order.OrderID];
                    Orders.Add(DataProcess.SetOrderData(order, orderDetail));

                    foreach (OrderCreationService.OrderPayment paymentDetail in orderDetail.Payments)
                    {
                        Payments payment = new Payments() { IsEnable = true, ID = paymentDetail.ID };
                        Payments.Add(DataProcess.SetPaymentData(payment, paymentDetail));
                    }

                    foreach (OrderCreationService.Package packageDetail in orderDetail.Packages)
                    {
                        Packages package = new Packages() { IsEnable = true, ID = packageDetail.ID };
                        Packages.Add(DataProcess.SetPackageData(package, packageDetail));
                    }

                    ExistingOrderInfo orderInfo = OCS_sellerCloud.GetOrder(OCS_authHeader, order.OrderID);
                    foreach (OrderCreationService.OrderItem itemDetail in orderDetail.Items)
                    {
                        Items item = orderInfo.Items
                            .Select(i => new Items() { IsEnable = true, ID = i.OrderItemUniqueIDInDB, SKU = i.SKU.Trim(), DisplayName = i.ItemName, UnitPrice = i.UnitPrice })
                            .First(i => i.ID == itemDetail.ID);
                        Items.Add(DataProcess.SetItemData(item, itemDetail));
                        Skus.Add(DataProcess.SetSkuData(
                            new Skus() { IsEnable = true, Sku = item.ProductID, Battery = false, ParentShadow = GetProductParent(item.ProductID) },
                            OS_sellerCloud.GetProductFullInfo(OS_authHeader, OS_options, item.ProductID)));

                        if (itemDetail.KitItemsCount > 0)
                        {
                            foreach (OrderCreationService.OrderBundleItem bundleDetail in itemDetail.BundleItems)
                            {
                                BundleItems bundle = new BundleItems() { IsEnable = true, ID = bundleDetail.ID };
                                BundleItems.Add(DataProcess.SetBundleData(bundle, bundleDetail));
                                Skus.Add(new Skus() { IsEnable = true, Sku = bundleDetail.ProductID, ProductName = bundleDetail.ProductName, Brand = 0, Battery = false });
                            }
                        }
                    }
                }
                DataProcess.BulkInsert(conn, "Orders", Orders);
            }

            IEnumerable<Orders> updateOrders = OrderList.Except(OrderList.Except(dbOrders)).Except(dbOrders, new OrdersComparer()).ToList();
            Console.WriteLine("--- There are <" + updateOrders.Count() + "> Orders need to update ---");
            if (updateOrders.Any())
            {
                foreach (Orders order in updateOrders)
                {
                    OrderCreationService.Order orderDetail = OCS_sellerCloud.GetOrderFull(OCS_authHeader, order.OrderID);

                    Addresses address = new Addresses() { Id = dbOrders.Where(o => o.OrderID == order.OrderID).FirstOrDefault().ShippingAddress.Value };
                    Addresses.Add(DataProcess.SetAddressData(address, orderDetail.ShippingAddress));

                    dbPayments = GetPayments(dbPayments, order.OrderID);
                    foreach (OrderCreationService.OrderPayment PaymentDetail in orderDetail.Payments)
                    {
                        Payments Payment = new Payments() { ID = PaymentDetail.ID };
                        Payments.Add(DataProcess.SetPaymentData(Payment, PaymentDetail));
                    }

                    dbPackages = GetPackages(dbPackages, order.OrderID);
                    foreach (OrderCreationService.Package packageDetail in orderDetail.Packages)
                    {
                        Packages package = new Packages() { ID = packageDetail.ID };
                        Packages.Add(DataProcess.SetPackageData(package, packageDetail));
                    }

                    dbItems = GetItems(dbItems, order.OrderID);
                    foreach (OrderCreationService.OrderItem itemDetail in orderDetail.Items)
                    {
                        Items item = new Items() { ID = itemDetail.ID };
                        Items.Add(DataProcess.SetItemData(item, itemDetail));
                    }

                    dbSerialNumbers = GetSerialNumbers(dbSerialNumbers, order.OrderID);
                    foreach (OrderSerialNumber serialNumberDetail in OS_sellerCloud.Serials_ListFor(OS_authHeader, OS_options, order.OrderID))
                    {
                        SerialNumbers serialNumber = new SerialNumbers();
                        SerialNumbers.Add(DataProcess.SetSerialNumberData(serialNumber, serialNumberDetail));
                    }
                }

                DataTable orderTable = ToDataTable(updateOrders.Select(o => new
                {
                    OrderID = o.OrderID,
                    StatusCode = o.StatusCode,
                    PaymentStatus = o.PaymentStatus,
                    ShippingStatus = o.ShippingStatus,
                    IsConfirmed = o.IsConfirmed,
                    ConfirmBy = o.ConfirmBy,
                    ConfirmOn = o.ConfirmOn
                }).ToList());

                DataProcess.BatchUpdate(conn, orderTable, "OrderData");
            }

            if (Addresses.Any()) DataProcess.BatchUpdate(conn, ToDataTable(Addresses), "AddressData");

            IEnumerable<Payments> newPayments = Payments.Except(dbPayments);
            if (newPayments.Any())
            {
                Console.WriteLine("--- There are <" + newPayments.Count() + "> new Payments ---");
                DataProcess.BulkInsert(conn, "Payments", newPayments);
            }
            IEnumerable<Payments> updatePayments = Payments.Except(newPayments).Except(dbPayments, new PaymentsComparer());
            if (updatePayments.Any())
            {
                Console.WriteLine("--- There are <" + updatePayments.Count() + "> Payments need to update ---");
                DataProcess.BatchUpdate(conn, ToDataTable(updatePayments), "PaymentData");
            }

            IEnumerable<Packages> newPackages = Packages.Except(dbPackages);
            if (newPackages.Any())
            {
                Console.WriteLine("--- There are <" + newPackages.Count() + "> new Packages ---");
                DataProcess.BulkInsert(conn, "Packages", newPackages);
            }

            IEnumerable<Packages> updatePackages = Packages.Except(newPackages).Except(dbPackages, new PackagesComparer());
            if (updatePackages.Any())
            {
                Console.WriteLine("--- There are <" + updatePackages.Count() + "> Packages need to update ---");
                DataProcess.BatchUpdate(conn, ToDataTable(updatePackages), "PackageData");
            }

            IEnumerable<Items> newItems = Items.Except(dbItems);
            if (newItems.Any())
            {
                Console.WriteLine("--- There are <" + newItems.Count() + "> new Items ---");
                DataProcess.BulkInsert(conn, "Items", newItems);
            }
            IEnumerable<Items> updateItems = Items.Except(newItems).Except(dbItems, new ItemsComparer());
            if (updateItems.Any())
            {
                Console.WriteLine("--- There are <" + updateItems.Count() + "> Items need to update ---");
                DataProcess.BatchUpdate(conn, ToDataTable(updateItems), "ItemData");
            }

            if (BundleItems.Any()) DataProcess.BulkInsert(conn, "BundleItems", BundleItems);

            if (Skus.Any())
            {
                IEnumerable<Skus> newSkus = Skus.Except(GetSkus());
                if (newSkus.Any()) DataProcess.BulkInsert(conn, "Skus", newSkus);
            }

            IEnumerable<SerialNumbers> newSerialNumbers = SerialNumbers.Except(dbSerialNumbers);
            if (newSerialNumbers.Any())
            {
                Console.WriteLine("--- There are <" + newSerialNumbers.Count() + "> new SerialNumbers ---");
                DataProcess.BulkInsert(conn, "SerialNumbers", newSerialNumbers);
            }
        }

        static List<Orders> GetOrders(int day)
        {
            List<Orders> OrdersList = new List<Orders>();
            SqlCommand OrdersCmd = new SqlCommand("select * from Orders where TimeOfOrder >= '" + Today.AddDays(-day).ToString("yyyy/MM/dd HH:mm:ss") + "'", conn);
            using (IDataReader OrdersData = OrdersCmd.ExecuteReader())
            {
                while (OrdersData.Read())
                {
                    var data = OrdersData;
                    OrdersList.Add(new Orders()
                    {
                        OrderID = Convert.ToInt32(OrdersData["OrderID"]),
                        StatusCode = Convert.ToInt32(OrdersData["StatusCode"]),
                        PaymentStatus = Convert.ToInt32(OrdersData["PaymentStatus"]),
                        ShippingStatus = Convert.ToInt32(OrdersData["ShippingStatus"]),
                        ShippingAddress = Convert.ToInt32(OrdersData["ShippingAddress"]),
                        IsConfirmed = Convert.ToBoolean(OrdersData["IsConfirmed"]),
                        ConfirmBy = Convert.ToInt32(OrdersData["ConfirmBy"]),
                        ConfirmOn = Convert.ToDateTime(OrdersData["ConfirmOn"])
                    });
                }
                OrdersData.Close();
            }
            OrdersCmd.Dispose();

            Console.WriteLine("--- Get <" + OrdersList.Count() + "> Orders from database ---");
            return OrdersList;
        }

        static List<Payments> GetPayments(List<Payments> PaymentList, int OrderID)
        {
            SqlCommand PaymentsCmd = new SqlCommand("select * from Payments where OrderID = " + OrderID, conn);
            using (IDataReader PaymentsData = PaymentsCmd.ExecuteReader())
            {
                while (PaymentsData.Read())
                {
                    PaymentList.Add(new Payments()
                    {
                        ID = Convert.ToInt32(PaymentsData["ID"]),
                        OrderID = Convert.ToInt32(PaymentsData["OrderID"]),
                        PaymentStatus = Convert.ToInt32(PaymentsData["PaymentStatus"]),
                        AuditDate = Convert.ToDateTime(PaymentsData["AuditDate"].Equals(DBNull.Value) ? "0001/01/01" : PaymentsData["AuditDate"]),
                    });
                }
                PaymentsData.Close();
            }
            PaymentsCmd.Dispose();

            return PaymentList;
        }

        static List<Packages> GetPackages(List<Packages> PackageList, int OrderID)
        {
            SqlCommand PackagesCmd = new SqlCommand("select * from Packages where OrderID = " + OrderID, conn);
            using (IDataReader PackagesData = PackagesCmd.ExecuteReader())
            {
                while (PackagesData.Read())
                {
                    PackageList.Add(new Packages()
                    {
                        ID = Convert.ToInt32(PackagesData["ID"]),
                        OrderID = Convert.ToInt32(PackagesData["OrderID"]),
                        OrderItemID = Convert.ToInt32(PackagesData["OrderItemID"]),
                        BundleItemID = Convert.ToInt32(PackagesData["BundleItemID"]),
                        Qty = Convert.ToInt32(PackagesData["Qty"]),
                        ShipDate = Convert.ToDateTime(PackagesData["ShipDate"].Equals(DBNull.Value) ? "0001/01/01" : PackagesData["ShipDate"]),
                        ShippingMethodName = PackagesData["ShippingMethodName"].ToString(),
                        ShippingServiceCode = PackagesData["ShippingServiceCode"].ToString(),
                        TrackingNumber = PackagesData["TrackingNumber"].ToString()
                    });
                }
                PackagesData.Close();
            }
            PackagesCmd.Dispose();

            return PackageList;
        }

        static List<Items> GetItems(List<Items> ItemsList, int OrderID)
        {
            SqlCommand ItemsCmd = new SqlCommand("select * from Items where OrderID = " + OrderID, conn);
            using (IDataReader ItemsData = ItemsCmd.ExecuteReader())
            {
                while (ItemsData.Read())
                {
                    ItemsList.Add(new Items()
                    {
                        ID = Convert.ToInt32(ItemsData["ID"]),
                        OrderID = Convert.ToInt32(ItemsData["OrderID"]),
                        PackageID = Convert.ToInt32(ItemsData["PackageID"]),
                        Qty = Convert.ToInt32(ItemsData["Qty"]),
                        QtyShipped = Convert.ToInt32(ItemsData["QtyShipped"]),
                        QtyReturned = Convert.ToInt32(ItemsData["QtyReturned"]),
                        OrderSourceItemID = ItemsData["OrderSourceItemID"].ToString(),
                        OrderSourceTransactionID = ItemsData["OrderSourceTransactionID"].ToString(),
                        ShipFromWarehouseID = Convert.ToInt32(ItemsData["ShipFromWarehouseID"]),
                        ReturnedToWarehouseID = Convert.ToInt32(ItemsData["ReturnedToWarehouseID"])
                    });
                }
                ItemsData.Close();
            }
            ItemsCmd.Dispose();

            return ItemsList;
        }

        static List<SerialNumbers> GetSerialNumbers(List<SerialNumbers> SerialNumbersList, int OrderID)
        {
            SqlCommand SerialNumbersCmd = new SqlCommand("select * from SerialNumbers where OrderID = " + OrderID, conn);
            using (IDataReader SerialNumbersData = SerialNumbersCmd.ExecuteReader())
            {
                while (SerialNumbersData.Read())
                {
                    SerialNumbersList.Add(new SerialNumbers()
                    {
                        OrderID = Convert.ToInt32(SerialNumbersData["OrderID"]),
                        ProductID = SerialNumbersData["ProductID"].ToString(),
                        SerialNumber = SerialNumbersData["SerialNumber"].ToString(),
                        OrderItemID = Convert.ToInt32(SerialNumbersData["OrderItemID"]),
                        KitItemID = !SerialNumbersData["KitItemID"].Equals(DBNull.Value) ? Convert.ToInt32(SerialNumbersData["KitItemID"]) : 0
                    });
                }
                SerialNumbersData.Close();
            }
            SerialNumbersCmd.Dispose();

            return SerialNumbersList;
        }

        static List<Skus> CheckSkuData(List<Skus> SkusList, string Sku, string ProductName)
        {
            SqlCommand SkusCmd = new SqlCommand("select * from Skus where Sku = " + Sku, conn);
            using (IDataReader SkusData = SkusCmd.ExecuteReader())
            {
                if (!SkusData.Read())
                {
                    SkusList.Add(new Skus()
                    {
                        IsEnable = true,
                        Sku = Sku,
                        ProductName = ProductName,
                        Brand = 0,
                        Battery = false
                    });
                }
                SkusData.Close();
            }
            SkusCmd.Dispose();

            return SkusList;
        }

        static void WarehousesSync()
        {
            Console.WriteLine("--- Start to access Warehouses ---");

            List<Warehouses> dbWarehouses = GetWarehouses();
            List<Warehouses> Warehouses = new List<Warehouses>();

            OrderCreationService.Warehouse[] warehouseList = OCS_sellerCloud.Warehouse_ListAll(OCS_authHeader);
            foreach (OrderCreationService.Warehouse warehouse in warehouseList)
            {
                Warehouses.Add(new Warehouses()
                {
                    IsEnable = true,
                    IsDefault = warehouse.IsDefault,
                    IsSellable = warehouse.IsSellAble,
                    AllowUseQtyForFBAShipments = warehouse.AllowUseQtyForFBAShipments,
                    EnforceBins = warehouse.EnforceBins,
                    ID = warehouse.ID,
                    CompanyID = warehouse.ClientID,
                    Name = warehouse.Name,
                    QBWarehouseName = warehouse.QBWarehouseName,
                    WarehouseType = (int)warehouse.WarehouseType,
                    DropShipCentralWarehouseCode = warehouse.DropShipCentralWarehouseCode,
                    CreatedBy = warehouse.CreatedBy,
                    CreatedOn = warehouse.CreatedOn
                });
            }

            Console.WriteLine("--- Get <" + Warehouses.Count() + "> Warehouses from Sellercloud ---");
            Console.WriteLine("--- End to access Warehouses ---");

            IEnumerable<Warehouses> newWarehouses = Warehouses.Except(dbWarehouses);
            Console.WriteLine("--- There are <" + newWarehouses.Count() + "> new Warehouses ---");
            if (newWarehouses.Any())
            {
                DataProcess.BulkInsert(conn, "Warehouses", newWarehouses.ToList());
            }
        }

        static List<Warehouses> GetWarehouses()
        {
            List<Warehouses> WarehousesList = new List<Warehouses>();
            SqlCommand WarehousesCmd = new SqlCommand("select * from Warehouses where IsEnable = 1", conn);
            using (IDataReader WarehousesData = WarehousesCmd.ExecuteReader())
            {
                while (WarehousesData.Read())
                {
                    WarehousesList.Add(new Warehouses() { ID = (int)WarehousesData["ID"] });
                }
                WarehousesData.Close();
            }
            WarehousesCmd.Dispose();

            Console.WriteLine("--- Get <" + WarehousesList.Count() + "> Warehouses from database ---");
            return WarehousesList;
        }

        static void ServicesSync()
        {
            Console.WriteLine("--- Start to access Services ---");

            List<Services> dbServices = GetServices();
            List<Services> Services = new List<Services>();

            ShippingServiceInfo[] serviceList = OCS_sellerCloud.ShippingServices_ListAll(OCS_authHeader);
            foreach (ShippingServiceInfo service in serviceList)
            {
                Services.Add(new Services()
                {
                    IsEnable = true,
                    ServiceCode = service.ServiceMethodID.Trim(),
                    ServiceName = service.ServiceMethodDescription.Trim()
                });
            }

            Console.WriteLine("--- Get <" + Services.Count() + "> Services from Sellercloud ---");
            Console.WriteLine("--- End to access Services ---");

            IEnumerable<Services> newServices = Services.Except(dbServices);
            Console.WriteLine("--- There are <" + newServices.Count() + "> new Services ---");
            if (newServices.Any())
            {
                DataProcess.BulkInsert(conn, "Services", newServices.ToList());
            }
        }

        static List<Services> GetServices()
        {
            List<Services> ServicesList = new List<Services>();
            SqlCommand ServicesCmd = new SqlCommand("select * from Services where IsEnable = 1", conn);
            using (IDataReader ServicesData = ServicesCmd.ExecuteReader())
            {
                while (ServicesData.Read())
                {
                    ServicesList.Add(new Services()
                    {
                        ServiceCode = ServicesData["ServiceCode"].ToString()
                    });
                }
                ServicesData.Close();
            }
            ServicesCmd.Dispose();

            Console.WriteLine("--- Get <" + ServicesList.Count() + "> Services from database ---");
            return ServicesList;
        }

        static void ManufacturersSync()
        {
            Console.WriteLine("--- Start to access Manufacturers ---");

            List<Manufacturers> dbManufacturers = GetManufacturers();
            List<Manufacturers> Manufacturers = new List<Manufacturers>();

            Company[] companyList = OS_sellerCloud.ListAllCompany(OS_authHeader, OS_options);
            foreach (Company company in companyList)
            {
                Manufacturers.AddRange(OS_sellerCloud.Manufacturer_ListALL(OS_authHeader, OS_options, company.ID)
                    .Select(m => new Manufacturers()
                    {
                        IsEnable = true,
                        ID = m.ID,
                        CompanyID = m.CompanyID,
                        ManufacturerName = m.ManufacturerName
                    }).ToArray());
            }

            Console.WriteLine("--- Get <" + Manufacturers.Count() + "> Manufacturers from Sellercloud ---");
            Console.WriteLine("--- End to access Manufacturers ---");

            IEnumerable<Manufacturers> newManufacturers = Manufacturers.Except(dbManufacturers);
            Console.WriteLine("--- There are <" + newManufacturers.Count() + "> new Manufacturers ---");
            if (newManufacturers.Any())
            {
                DataProcess.BulkInsert(conn, "Manufacturers", newManufacturers.ToList());
            }
        }

        static List<Manufacturers> GetManufacturers()
        {
            List<Manufacturers> ManufacturersList = new List<Manufacturers>();
            SqlCommand ManufacturersCmd = new SqlCommand("select * from Manufacturers where IsEnable = 1", conn);
            using (IDataReader ManufacturersData = ManufacturersCmd.ExecuteReader())
            {
                while (ManufacturersData.Read())
                {
                    ManufacturersList.Add(new Manufacturers() { ID = (int)ManufacturersData["ID"] });
                }
                ManufacturersData.Close();
            }
            ManufacturersCmd.Dispose();

            Console.WriteLine("--- Get <" + ManufacturersList.Count() + "> Manufacturers from database ---");
            return ManufacturersList;
        }

        static void ProductTypeSync()
        {
            Console.WriteLine("--- Start to access ProductType ---");

            List<ProductType> dbProductType = GetProductType();
            List<ProductType> ProductType = new List<ProductType>();

            Company[] companyList = OS_sellerCloud.ListAllCompany(OS_authHeader, OS_options);
            foreach (Company company in companyList)
            {
                ProductType.AddRange(OS_sellerCloud.ListProductType(OS_authHeader, OS_options, company.ID)
                .Select(t => new ProductType() { IsEnable = true, ID = t.ID, ProductTypeName = t.ProductTypeName }).ToArray());
            }
            
            Console.WriteLine("--- Get <" + ProductType.Count() + "> ProductType from Sellercloud ---");
            Console.WriteLine("--- End to access ProductType ---");

            IEnumerable<ProductType> newProductType = ProductType.Except(dbProductType);
            Console.WriteLine("--- There are <" + newProductType.Count() + "> new ProductType ---");
            if (newProductType.Any())
            {
                DataProcess.BulkInsert(conn, "ProductType", newProductType.ToList());
            }
        }

        static List<ProductType> GetProductType()
        {
            List<ProductType> ProductTypeList = new List<ProductType>();
            SqlCommand ProductTypeCmd = new SqlCommand("select * from ProductType where IsEnable = 1", conn);
            using (IDataReader ProductTypeData = ProductTypeCmd.ExecuteReader())
            {
                while (ProductTypeData.Read())
                {
                    ProductTypeList.Add(new ProductType() { ID = (int)ProductTypeData["ID"] });
                }
                ProductTypeData.Close();
            }
            ProductTypeCmd.Dispose();

            Console.WriteLine("--- Get <" + ProductTypeList.Count() + "> ProductType from database ---");
            return ProductTypeList;
        }

        static void SkusSync()
        {
            Console.WriteLine("--- Start to access Skus ---");

            List<Skus> dbSkus = GetSkus();
            List<Skus> SkuList = new List<Skus>();
            List<Skus> Skus = new List<Skus>();

            Company[] companyList = OS_sellerCloud.ListAllCompany(OS_authHeader, OS_options);
            foreach (Company company in companyList)
            {
                SkuList.AddRange(OCS_sellerCloud.Products_ListSKU(OCS_authHeader, company.ID).Select(s => new Skus() { Sku = s }));
            }

            Console.WriteLine("--- Get <" + SkuList.Count() + "> skus from Sellercloud ---");
            Console.WriteLine("--- End to access Skus ---");

            IEnumerable<Skus> newSkus = SkuList.Except(dbSkus);
            int total = newSkus.Count();
            Console.WriteLine("--- There are <" + total + "> new skus ---");

            if (newSkus.Any())
            {
                int start = 0, take = 100;
                string[] skuList = newSkus.Select(s => s.Sku).ToArray();
                do
                {
                    Console.WriteLine("--- Take new skus from " + (start + 1) + " to " + (start + take) + " ---");
                    Skus.AddRange(OS_sellerCloud.GetProductFullInfos(OS_authHeader, OS_options, skuList.Skip(start).Take(take).ToArray())
                        .Select(p => DataProcess.SetSkuData(new Skus() { IsEnable = true, Sku = p.ID, Battery = false, ParentShadow = GetProductParent(p.ID) }, p)));
                } while ((start += take) < total);

                DataProcess.BulkInsert(conn, "Skus", Skus);
            }
        }

        static List<Skus> GetSkus()
        {
            List<Skus> SkusList = new List<Skus>();
            SqlCommand SkusCmd = new SqlCommand("select * from Skus where IsEnable = 1", conn);
            using (IDataReader skuData = SkusCmd.ExecuteReader())
            {
                while (skuData.Read())
                {
                    SkusList.Add(new Skus() { Sku = skuData["Sku"].ToString() });
                }
                skuData.Close();
            }
            SkusCmd.Dispose();

            Console.WriteLine("--- Get <" + SkusList.Count() + "> Skus from database ---");
            return SkusList;
        }

        static void PurchaseItemSync(string[] productIDs)
        {
            Console.WriteLine("--- Start to access PurchaseItem ---");

            if (productIDs.Any())
            {
                List<PurchaseItemReceive> dbPurchaseItems = GetPurchaseItems(productIDs);
                List<PurchaseItemReceive> PurchaseItemList = new List<PurchaseItemReceive>();

                foreach (string productID in productIDs)
                {
                    var serials = OS_sellerCloud.PurchaseItemReceiveSerial_All(OS_authHeader, OS_options, productID);
                    if (serials.SerialsList.Any())
                    {
                        PurchaseItemList.AddRange(serials.SerialsList.Select(s => new PurchaseItemReceive()
                        {
                            IsRequireSerialScan = serials.IsRequireSerialScan,
                            OrderID = s.OrderID,
                            OrderItemID = s.OrderItemID,
                            ProductID = s.ProductID,
                            SerialNumber = s.SerialNumber.Trim(),
                            PurchaseID = s.PurchaseID,
                            PurchaseReceiveID = s.PurchaseReceiveID,
                            RMAId = s.RMAId,
                            CreditMemoID = s.CreditMemoID,
                            CreditMemoReason = s.CreditMemoReason.Trim(),
                            WarehouseID = s.WarehouseID,
                            WarehouseName = s.WarehouseName.Trim(),
                            LocationBinID = s.LocationBinID,
                            BinName = s.BinName.Trim()
                        }).ToArray());
                    }
                }

                Console.WriteLine("--- Get <" + PurchaseItemList.Count() + "> PurchaseItems from Sellercloud ---");
                Console.WriteLine("--- End to access PurchaseItems ---");

                IEnumerable<PurchaseItemReceive> newPurchaseItems = PurchaseItemList.Except(dbPurchaseItems);
                int total = newPurchaseItems.Count();
                Console.WriteLine("--- There are <" + total + "> new PurchaseItems ---");

                if (newPurchaseItems.Any())
                {
                    DataProcess.BulkInsert(conn, "PurchaseItemReceive", newPurchaseItems.ToList());
                }
            }
        }

        static List<PurchaseItemReceive> GetPurchaseItems(string[] productIDs)
        {
            List<PurchaseItemReceive> PurchaseItemList = new List<PurchaseItemReceive>();

            string command = string.Format("select * from PurchaseItemReceive where ProductID IN ({0})", string.Join(", ", productIDs.Select(p => string.Format("'{0}'", p))));
            SqlCommand PurchaseItemsCmd = new SqlCommand(command, conn);
            using (IDataReader PurchaseItemData = PurchaseItemsCmd.ExecuteReader())
            {
                while (PurchaseItemData.Read())
                {
                    PurchaseItemList.Add(new PurchaseItemReceive() { SerialNumber = PurchaseItemData["SerialNumber"].ToString().Trim() });
                }
                PurchaseItemData.Close();
            }
            PurchaseItemsCmd.Dispose();

            Console.WriteLine("--- Get <" + PurchaseItemList.Count() + "> PurchaseItems from database ---");
            return PurchaseItemList;
        }

        static DataTable ToDataTable<T>(IEnumerable<T> collection)
        {
            var dtReturn = new DataTable();

            // column names
            var oProps = typeof(T).GetProperties();
            foreach (var pi in oProps)
            {
                var colType = pi.PropertyType;
                if ((colType.IsGenericType) && (colType.GetGenericTypeDefinition() == typeof(Nullable<>)))
                {
                    colType = colType.GetGenericArguments()[0];
                }
                dtReturn.Columns.Add(new DataColumn(pi.Name, colType));
            }

            // Could add a check to verify that there is an element 0
            foreach (var rec in collection)
            {
                var dr = dtReturn.NewRow();
                foreach (var pi in oProps)
                {
                    dr[pi.Name] = pi.GetValue(rec, null) ?? DBNull.Value;
                }
                dtReturn.Rows.Add(dr);
            }

            return (dtReturn);
        }

        static DateTime DateTimeWithZone(DateTime dateTime, bool local = true) // local = true 換算成台北時間 ， local = false 換算成紐約時間
        {
            TimeZoneInfo EstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            TimeZoneInfo TstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time");

            DateTime utcDateTime = TimeZoneInfo.ConvertTimeToUtc(dateTime, local ? EstTimeZone : TimeZoneInfo.Local);
            return TimeZoneInfo.ConvertTime(utcDateTime, (local ? TstTimeZone : EstTimeZone));
        }

        static string GetProductParent(string SKU)
        {
            string productID = OS_sellerCloud.GetProductParent(OS_authHeader, OS_options, SKU);

            return !productID.Equals(SKU) ? productID : null;
        }
    }
}
