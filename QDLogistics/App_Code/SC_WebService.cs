using QDLogistics.OrderCreationService;
using QDLogistics.OrderService;
using QDLogistics.PurchaseOrderService;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;

/**
* 整理 SellerCloud API
* http://developer.sellercloud.com/
*/
namespace SellerCloud_WebService
{
    public class SC_WebService
    {
        // OrderService
        private SCServiceSoapClient OS_sellerCloud;
        private QDLogistics.OrderService.AuthHeader OS_authHeader;
        private QDLogistics.OrderService.ServiceOptions OS_options;

        // OrderCreationService
        private OrderCreationServiceSoapClient OCS_sellerCloud;
        private QDLogistics.OrderCreationService.AuthHeader OCS_authHeader;

        // PurchaseOrderService
        private POServicesSoapClient PO_sellerCloud;
        private QDLogistics.PurchaseOrderService.AuthHeader PO_authHeader;
        private QDLogistics.PurchaseOrderService.ServiceOptions PO_options;

        public int UserID;
        public DateTime SyncOn;
        public DateTime Today;

        public bool Is_login
        {
            get
            {
                bool status;

                if (status = Login_test())
                {
                    UserID = OS_sellerCloud.GetCurrentUserInfo(OS_authHeader, OS_options, 0).UserID;
                }

                return status;
            }
        }

        public SC_WebService(string UserName, string Password)
        {
            // OrderService
            OS_sellerCloud = new SCServiceSoapClient();
            OS_sellerCloud.InnerChannel.OperationTimeout = new TimeSpan(0, 5, 0);
            OS_authHeader = new QDLogistics.OrderService.AuthHeader();
            OS_options = new QDLogistics.OrderService.ServiceOptions();

            // OrderCreationService
            OCS_sellerCloud = new OrderCreationServiceSoapClient();
            OCS_sellerCloud.InnerChannel.OperationTimeout = new TimeSpan(0, 5, 0);
            OCS_authHeader = new QDLogistics.OrderCreationService.AuthHeader();

            // PurchaseOrderService
            PO_sellerCloud = new POServicesSoapClient();
            PO_sellerCloud.InnerChannel.OperationTimeout = new TimeSpan(0, 5, 0);
            PO_authHeader = new QDLogistics.PurchaseOrderService.AuthHeader();
            PO_options = new QDLogistics.PurchaseOrderService.ServiceOptions();

            OS_authHeader.UserName = OCS_authHeader.UserName = PO_authHeader.UserName = UserName; //"tim@weypro.com"
            OS_authHeader.Password = OCS_authHeader.Password = PO_authHeader.Password = Password; //"timfromweypro"

            SyncOn = DateTime.UtcNow;
        }

        private bool Login_test()
        {
            try
            {
                return OS_sellerCloud.TestLogin(OS_authHeader, OS_options);
            }
            catch (Exception e)
            {
                return OS_sellerCloud.TestLogin(OS_authHeader, OS_options);
            }
        }

        /***** 取得資料 *****/
        public OrderStateInfo[] Search_Order(DateTime DateFrom, DateTime DateTo)
        {
            var searchResult = OCS_sellerCloud.SearchOrders(OCS_authHeader, DateFrom, DateTo, "", 0);
            int[] OrderIDs = searchResult.Rows.Cast<DataRow>().Select(o => (int)o.ItemArray.First()).ToArray();
            return OS_sellerCloud.Orders_GetOrderStates(OS_authHeader, OS_options, OrderIDs);
        }

        public OrderData Get_OrderData(int OrderID)
        {
            return OS_sellerCloud.Orders_GetData(OS_authHeader, OS_options, OrderID);
        }

        public OrderData[] Get_OrderData(int[] OrderID)
        {
            return OS_sellerCloud.Orders_GetDatas(OS_authHeader, OS_options, OrderID);
        }

        public ExistingOrderInfo Get_OrderInfo(int OrderID)
        {
            return OCS_sellerCloud.GetOrder(OCS_authHeader, OrderID);
        }

        public QDLogistics.OrderCreationService.Order Get_OrderFullData(int OrderID)
        {
            return OCS_sellerCloud.GetOrderFull(OCS_authHeader, OrderID);
        }

        public OrderStateInfo Get_OrderStatus(int OrderID)
        {
            return OS_sellerCloud.Orders_GetOrderState(OS_authHeader, OS_options, OrderID);
        }

        public OrderStateInfo[] Get_OrderStatus(int[] OrderIDs)
        {
            return OS_sellerCloud.Orders_GetOrderStates(OS_authHeader, OS_options, OrderIDs);
        }

        public byte[] Get_OrderInvoice(int OrderID)
        {
            return OS_sellerCloud.Orders_GetPDFInvoice(OS_authHeader, OS_options, OrderID);
        }

        public OrderPackage[] Get_OrderPackage_All(int OrderID)
        {
            return OS_sellerCloud.OrderPackages_ListAllForOrder(OS_authHeader, OS_options, OrderID);
        }

        public OrderSerialNumber[] Get_OrderItem_Serial(int OrderID)
        {
            return OS_sellerCloud.Serials_ListFor(OS_authHeader, OS_options, OrderID);
        }

        public OrderItemSerialRequiredResult[] Get_OrderItems_NeedSerial(int OrderID)
        {
            return OS_sellerCloud.Orders_GetOrderItemsNeedingSerialScan(OS_authHeader, OS_options, OrderID);
        }

        public ProductFullInfo Get_ProductFullInfo(string SKU)
        {
            return OS_sellerCloud.GetProductFullInfo(OS_authHeader, OS_options, SKU);
        }

        public ProductFullInfo[] Get_ProductFullInfos(string[] SKUs)
        {
            return OS_sellerCloud.GetProductFullInfos(OS_authHeader, OS_options, SKUs);
        }

        public ProductInformation Get_ProductInformation(string SKU)
        {
            return OS_sellerCloud.Products_GetInformation(OS_authHeader, OS_options, SKU);
        }

        public ProductSerial Get_ProductSerial(string ProductID, string Serial = "")
        {
            return OS_sellerCloud.Products_SerialNumber_GetSerial(OS_authHeader, OS_options, ProductID, Serial);
        }

        public PurchaseItemReceive_All_Response Get_ProductAllSerials(string ProductID)
        {
            return OS_sellerCloud.PurchaseItemReceiveSerial_All(OS_authHeader, OS_options, ProductID);
        }

        public ProductType[] Get_ProductType(int CompanyID)
        {
            return OS_sellerCloud.ListProductType(OS_authHeader, OS_options, CompanyID);
        }

        public string Get_ProductParent(string SKU)
        {
            return OS_sellerCloud.GetProductParent(OS_authHeader, OS_options, SKU);
        }

        public Company Get_Company(int CompanyID)
        {
            return OS_sellerCloud.GetCompany(OS_authHeader, OS_options, CompanyID);
        }

        public Company[] Get_AllCompany()
        {
            return OS_sellerCloud.ListAllCompany(OS_authHeader, OS_options);
        }

        public QDLogistics.OrderCreationService.AmazonMerchant[] Get_AllCompany2()
        {
            return OCS_sellerCloud.Companies_ListAll(OCS_authHeader);
        }

        public POVendor[] Get_Vendor_All(int CompanyID)
        {
            return OS_sellerCloud.ListAllVendors(OS_authHeader, OS_options, CompanyID);
        }

        public Vendor Get_Vendor(int VendorID)
        {
            return OS_sellerCloud.Vendors_GetVendor(OS_authHeader, OS_options, VendorID);
        }
        /***** 取得資料 *****/

        /***** 更新資料 *****/
        public bool Update_Order(QDLogistics.OrderService.Order order)
        {
            return OS_sellerCloud.Orders_SaveOrder(OS_authHeader, OS_options, order);
        }

        public bool Update_OrderStatus(int OrderID, int StatusCode)
        {
            return OS_sellerCloud.Orders_UpdateStatus(OS_authHeader, OS_options, OrderID, (QDLogistics.OrderService.OrderStatusCode)StatusCode);
        }

        public bool Update_OrderShippingStatus(QDLogistics.OrderService.Order order, string Carrier = "", string Service = "")
        {
            return OS_sellerCloud.Orders_UpdateShippingStatusOrder(OS_authHeader, OS_options, order.ID, Carrier, Service, order.StationID, order.ShippingLocationID, false);
        }

        public bool Update_OrderUnShip(int OrderID)
        {
            return OS_sellerCloud.Orders_Unship(OS_authHeader, OS_options, OrderID);
        }

        public int Update_PackageShippingStatus(QDLogistics.OrderService.Package package, string TrackingNumber, string Carrier = "", string Service = "")
        {
            return OS_sellerCloud.Orders_UpdateShippingStatusPackage(OS_authHeader, OS_options, package.OrderID, package, TrackingNumber, Carrier, Service, package.Weight.ToString(), package.FinalShippingFee.ToString(), package.StationID, 0);
        }

        public QDLogistics.OrderService.OrderItem Update_OrderItem(QDLogistics.OrderService.OrderItem[] list)
        {
            return OS_sellerCloud.Orders_UpdateItem(OS_authHeader, OS_options, list.First());
        }

        public bool Update_ItemSerialNumber(int ItemID, string[] SerialNumbers)
        {
            return OS_sellerCloud.Orders_UpdateItemSerialNum(OS_authHeader, OS_options, ItemID, SerialNumbers);
        }

        public bool Update_OrderConfirm(int OrderID)
        {
            return OS_sellerCloud.Orders_Confirm(OS_authHeader, OS_options, OrderID);
        }

        public void Update_OrderPackage(OrderPackage[] OrderPackages)
        {
            OS_sellerCloud.OrderPackages_AddOrUpdateMultiple(OS_authHeader, OS_options, OrderPackages);
        }

        public bool Update_PackageData(QDLogistics.OrderService.Package Package)
        {
            return OS_sellerCloud.Orders_UpdatePackage(OS_authHeader, OS_options, ref Package);
        }

        public QDLogistics.OrderService.OrderItem Update_ItemData(QDLogistics.OrderService.OrderItem item)
        {
            return OS_sellerCloud.Orders_UpdateItem(OS_authHeader, OS_options, item);
        }
        /***** 更新資料 *****/

        /***** 新增資料 *****/
        public QDLogistics.OrderService.Package Add_OrderNewPackage(QDLogistics.OrderService.Package NewPckage)
        {
            NewPckage.ID = -1;
            return OS_sellerCloud.Orders_AddPackagesToOrder(OS_authHeader, OS_options, NewPckage.OrderID, new QDLogistics.OrderService.Package[] { NewPckage }).First();
        }

        public QDLogistics.OrderService.OrderItem Add_OrderNewItem(QDLogistics.OrderService.OrderItem NewItem)
        {
            NewItem.ID = -1;
            return OS_sellerCloud.Orders_UpdateItem(OS_authHeader, OS_options, NewItem);
        }
        /***** 新增資料 *****/

        /***** 刪除資料 *****/
        public bool Delete_Package(int PackageID)
        {
            return OS_sellerCloud.Orders_DeletePackage(OS_authHeader, OS_options, PackageID);
        }

        public bool Delete_Items(int OrderID, int ItemID)
        {
            return OCS_sellerCloud.OrderItem_Delete(OCS_authHeader, OrderID, ItemID);
        }
        /***** 刪除資料 *****/

        /***** 商品退貨 *****/
        public QDLogistics.OrderCreationService.ReturnReason[] Get_RMA_Reason_List()
        {
            return OCS_sellerCloud.RMA_ListReasons(OCS_authHeader);
        }

        public QDLogistics.OrderCreationService.RMA Get_RMA_by_ID(int RMAID)
        {
            return OCS_sellerCloud.RMA_GetRMAByID(OCS_authHeader, RMAID);
        }

        public QDLogistics.OrderCreationService.RMA Get_RMA_by_OrderID(int OrderID)
        {
            return OCS_sellerCloud.RMA_GetRMAByOrderID(OCS_authHeader, OrderID);
        }

        public int Create_RMA(int OrderID)
        {
            return OCS_sellerCloud.RMA_CreateNew(OCS_authHeader, OrderID);
        }

        public int Create_RMA_Item(int OrderID, int OrderItemID, int RMAID, int QtyToReturn, int RMAReason, string RMADescription, string KitProductID = "")
        {
            return OCS_sellerCloud.RMAItem_CreateNew(OCS_authHeader, OrderID, OrderItemID, RMAID, KitProductID, QtyToReturn, RMAReason, RMADescription);
        }

        /***** 採購單 *****/
        public Purchase Get_PurchaseOrder(int POId)
        {
            return PO_sellerCloud.GetPurchaseOrder(PO_authHeader, PO_options, POId);
        }

        public PurchaseOrderInfo Get_PurchaseOrder_Info(int PurchaseID, int WarehouseID)
        {
            return PO_sellerCloud.GetPurchaseOrderInfo(PO_authHeader, PO_options, PurchaseID, WarehouseID);
        }

        public int Get_CurrentCompanyID()
        {
            return PO_sellerCloud.GetCurrentCompanyID(PO_authHeader);
        }
        
        public Purchase Create_PurchaseOrder(Purchase PurchaseOrder)
        {
            return PO_sellerCloud.CreateNewPurchaseOrder(PO_authHeader, PurchaseOrder);
        }

        public PurchaseItem Create_PurchaseOrder_Item(PurchaseItem PurchaseItem)
        {
            return PO_sellerCloud.PurchaseOrderItems_CreateNew(PO_authHeader, PurchaseItem);
        }

        public PurchaseItemReceive[] Create_PurchaseOrder_ItemReceive(PurchaseItemReceiveRequest Receive)
        {
            return PO_sellerCloud.PurchaseItemReceive_AddNew_Bulk(PO_authHeader, Receive);
        }

        public bool Update_PurchaseOrder(Purchase PurchaseOrder)
        {
            return PO_sellerCloud.UpdatePurchaseOrder(PO_authHeader, PurchaseOrder);
        }

        public bool Update_PurchaseOrder_ItemReceive_Serials(QDLogistics.PurchaseOrderService.PurchaseItemReceiveSerial[] Serials)
        {
            return PO_sellerCloud.PurchaseItem_SerialNumbersNew_SaveMultiple(PO_authHeader, Serials);
        }

        public bool Delete_PurchaseOrder(int POId)
        {
            return PO_sellerCloud.DeletePurchaseOrder(PO_authHeader, POId);
        }

        public bool Delete_PurchaseOrder_ItemReceive(PurchaseItemReceive Receive)
        {
            return  PO_sellerCloud.PurchaseItemReceive_Delete(PO_authHeader, Receive.Id);
        }

        public bool Delete_PurchaseOrder_ItemReceive_Serials(QDLogistics.PurchaseOrderService.PurchaseItemReceiveSerial[] Serials)
        {
            return PO_sellerCloud.PurchaseItem_SerialNumbersNew_DeleteMultiple(PO_authHeader, Serials);
        }
    }
}