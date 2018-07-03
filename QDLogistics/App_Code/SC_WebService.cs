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
    public class SC_WebService : IDisposable
    {
        // OrderService
        private SCServiceSoapClient OS_SellerCloud;
        private QDLogistics.OrderService.AuthHeader OS_AuthHeader;
        private QDLogistics.OrderService.ServiceOptions OS_Options;

        // OrderCreationService
        private OrderCreationServiceSoapClient OCS_SellerCloud;
        private QDLogistics.OrderCreationService.AuthHeader OCS_AuthHeader;

        // PurchaseOrderService
        private POServicesSoapClient PO_SellerCloud;
        private QDLogistics.PurchaseOrderService.AuthHeader PO_AuthHeader;
        private QDLogistics.PurchaseOrderService.ServiceOptions PO_Options;

        public int UserID;
        public DateTime SyncOn;
        private bool disposed = false;

        public bool Is_login
        {
            get
            {
                bool status;

                if (status = Login_test())
                {
                    UserID = OS_SellerCloud.GetCurrentUserInfo(OS_AuthHeader, OS_Options, 0).UserID;
                }

                return status;
            }
        }

        public SC_WebService(string UserName, string Password)
        {
            // OrderService
            OS_SellerCloud = new SCServiceSoapClient();
            OS_SellerCloud.InnerChannel.OperationTimeout = new TimeSpan(0, 10, 0);
            OS_AuthHeader = new QDLogistics.OrderService.AuthHeader();
            OS_Options = new QDLogistics.OrderService.ServiceOptions();

            // OrderCreationService
            OCS_SellerCloud = new OrderCreationServiceSoapClient();
            OCS_SellerCloud.InnerChannel.OperationTimeout = new TimeSpan(0, 10, 0);
            OCS_AuthHeader = new QDLogistics.OrderCreationService.AuthHeader();

            // PurchaseOrderService
            PO_SellerCloud = new POServicesSoapClient();
            PO_SellerCloud.InnerChannel.OperationTimeout = new TimeSpan(0, 10, 0);
            PO_AuthHeader = new QDLogistics.PurchaseOrderService.AuthHeader();
            PO_Options = new QDLogistics.PurchaseOrderService.ServiceOptions();

            OS_AuthHeader.UserName = OCS_AuthHeader.UserName = PO_AuthHeader.UserName = UserName; //"tim@weypro.com"
            OS_AuthHeader.Password = OCS_AuthHeader.Password = PO_AuthHeader.Password = Password; //"timfromweypro"

            SyncOn = DateTime.UtcNow;
        }

        private bool Login_test()
        {
            try
            {
                return OS_SellerCloud.TestLogin(OS_AuthHeader, OS_Options);
            }
            catch (Exception)
            {
                return OS_SellerCloud.TestLogin(OS_AuthHeader, OS_Options);
            }
        }

        /***** 取得資料 *****/
        public OrderStateInfo[] Search_Order(DateTime DateFrom, DateTime DateTo)
        {
            var searchResult = OCS_SellerCloud.SearchOrders(OCS_AuthHeader, DateFrom, DateTo, "", 0);
            int[] OrderIDs = searchResult.Rows.Cast<DataRow>().Select(o => (int)o.ItemArray.First()).ToArray();
            return OS_SellerCloud.Orders_GetOrderStates(OS_AuthHeader, OS_Options, OrderIDs);
        }

        public OrderData Get_OrderData(int OrderID)
        {
            return OS_SellerCloud.Orders_GetData(OS_AuthHeader, OS_Options, OrderID);
        }

        public OrderData[] Get_OrderData(int[] OrderID)
        {
            return OS_SellerCloud.Orders_GetDatas(OS_AuthHeader, OS_Options, OrderID);
        }

        public ExistingOrderInfo Get_OrderInfo(int OrderID)
        {
            return OCS_SellerCloud.GetOrder(OCS_AuthHeader, OrderID);
        }

        public QDLogistics.OrderCreationService.Order Get_OrderFullData(int OrderID)
        {
            return OCS_SellerCloud.GetOrderFull(OCS_AuthHeader, OrderID);
        }

        public OrderStateInfo Get_OrderStatus(int OrderID)
        {
            return OS_SellerCloud.Orders_GetOrderState(OS_AuthHeader, OS_Options, OrderID);
        }

        public OrderStateInfo[] Get_OrderStatus(int[] OrderIDs)
        {
            return OS_SellerCloud.Orders_GetOrderStates(OS_AuthHeader, OS_Options, OrderIDs);
        }

        public byte[] Get_OrderInvoice(int OrderID)
        {
            return OS_SellerCloud.Orders_GetPDFInvoice(OS_AuthHeader, OS_Options, OrderID);
        }

        public OrderPackage[] Get_OrderPackage_All(int OrderID)
        {
            return OS_SellerCloud.OrderPackages_ListAllForOrder(OS_AuthHeader, OS_Options, OrderID);
        }

        public OrderSerialNumber[] Get_OrderItem_Serial(int OrderID)
        {
            return OS_SellerCloud.Serials_ListFor(OS_AuthHeader, OS_Options, OrderID);
        }

        public OrderItemSerialRequiredResult[] Get_OrderItems_NeedSerial(int OrderID)
        {
            return OS_SellerCloud.Orders_GetOrderItemsNeedingSerialScan(OS_AuthHeader, OS_Options, OrderID);
        }

        public ProductFullInfo Get_ProductFullInfo(string SKU)
        {
            return OS_SellerCloud.GetProductFullInfo(OS_AuthHeader, OS_Options, SKU);
        }

        public ProductFullInfo[] Get_ProductFullInfos(string[] SKUs)
        {
            return OS_SellerCloud.GetProductFullInfos(OS_AuthHeader, OS_Options, SKUs);
        }

        public ProductInformation Get_ProductInformation(string SKU)
        {
            return OS_SellerCloud.Products_GetInformation(OS_AuthHeader, OS_Options, SKU);
        }

        public ProductSerial Get_ProductSerial(string ProductID, string Serial = "")
        {
            return OS_SellerCloud.Products_SerialNumber_GetSerial(OS_AuthHeader, OS_Options, ProductID, Serial);
        }

        public PurchaseItemReceive_All_Response Get_ProductAllSerials(string ProductID)
        {
            return OS_SellerCloud.PurchaseItemReceiveSerial_All(OS_AuthHeader, OS_Options, ProductID);
        }

        public ProductType[] Get_ProductType(int CompanyID)
        {
            return OS_SellerCloud.ListProductType(OS_AuthHeader, OS_Options, CompanyID);
        }

        public string Get_ProductParent(string SKU)
        {
            return OS_SellerCloud.GetProductParent(OS_AuthHeader, OS_Options, SKU);
        }

        public Company Get_Company(int CompanyID)
        {
            return OS_SellerCloud.GetCompany(OS_AuthHeader, OS_Options, CompanyID);
        }

        public Company[] Get_AllCompany()
        {
            return OS_SellerCloud.ListAllCompany(OS_AuthHeader, OS_Options);
        }

        public QDLogistics.OrderCreationService.AmazonMerchant[] Get_AllCompany2()
        {
            return OCS_SellerCloud.Companies_ListAll(OCS_AuthHeader);
        }

        public POVendor[] Get_Vendor_All(int CompanyID)
        {
            return OS_SellerCloud.ListAllVendors(OS_AuthHeader, OS_Options, CompanyID);
        }

        public Vendor Get_Vendor(int VendorID)
        {
            return OS_SellerCloud.Vendors_GetVendor(OS_AuthHeader, OS_Options, VendorID);
        }
        /***** 取得資料 *****/

        /***** 更新資料 *****/
        public bool Update_Order(QDLogistics.OrderService.Order order)
        {
            return OS_SellerCloud.Orders_SaveOrder(OS_AuthHeader, OS_Options, order);
        }

        public bool Update_OrderStatus(int OrderID, int StatusCode)
        {
            return OS_SellerCloud.Orders_UpdateStatus(OS_AuthHeader, OS_Options, OrderID, (QDLogistics.OrderService.OrderStatusCode)StatusCode);
        }

        public bool Update_OrderShippingStatus(QDLogistics.OrderService.Order order, string Carrier = "", string Service = "")
        {
            return OS_SellerCloud.Orders_UpdateShippingStatusOrder(OS_AuthHeader, OS_Options, order.ID, Carrier, Service, order.StationID, order.ShippingLocationID, false);
        }

        public bool Update_OrderUnShip(int OrderID)
        {
            return OS_SellerCloud.Orders_Unship(OS_AuthHeader, OS_Options, OrderID);
        }

        public int Update_PackageShippingStatus(QDLogistics.OrderService.Package package, string TrackingNumber, string Carrier = "", string Service = "")
        {
            return OS_SellerCloud.Orders_UpdateShippingStatusPackage(OS_AuthHeader, OS_Options, package.OrderID, package, TrackingNumber, Carrier, Service, package.Weight.ToString(), package.FinalShippingFee.ToString(), package.StationID, 0);
        }

        public QDLogistics.OrderService.OrderItem Update_OrderItem(QDLogistics.OrderService.OrderItem item)
        {
            return OS_SellerCloud.Orders_UpdateItem(OS_AuthHeader, OS_Options, item);
        }

        public bool Update_OrderItem(QDLogistics.OrderService.OrderItem[] itemList)
        {
            return OS_SellerCloud.Orders_UpdateOrderItems(OS_AuthHeader, OS_Options, itemList);
        }

        public bool Update_ItemSerialNumber(int ItemID, string[] SerialNumbers)
        {
            return OS_SellerCloud.Orders_UpdateItemSerialNum(OS_AuthHeader, OS_Options, ItemID, SerialNumbers);
        }

        public bool Update_OrderConfirm(int OrderID)
        {
            return OS_SellerCloud.Orders_Confirm(OS_AuthHeader, OS_Options, OrderID);
        }

        public void Update_OrderPackage(OrderPackage[] OrderPackages)
        {
            OS_SellerCloud.OrderPackages_AddOrUpdateMultiple(OS_AuthHeader, OS_Options, OrderPackages);
        }

        public bool Update_PackageData(QDLogistics.OrderService.Package Package)
        {
            return OS_SellerCloud.Orders_UpdatePackage(OS_AuthHeader, OS_Options, ref Package);
        }
        /***** 更新資料 *****/

        /***** 新增資料 *****/
        public QDLogistics.OrderService.Package Add_OrderNewPackage(QDLogistics.OrderService.Package NewPckage)
        {
            NewPckage.ID = -1;
            return OS_SellerCloud.Orders_AddPackagesToOrder(OS_AuthHeader, OS_Options, NewPckage.OrderID, new QDLogistics.OrderService.Package[] { NewPckage }).First();
        }

        public QDLogistics.OrderService.OrderItem Add_OrderNewItem(QDLogistics.OrderService.OrderItem NewItem)
        {
            NewItem.ID = -1;
            return OS_SellerCloud.Orders_UpdateItem(OS_AuthHeader, OS_Options, NewItem);
        }
        /***** 新增資料 *****/

        /***** 刪除資料 *****/
        public bool Delete_Package(int PackageID)
        {
            return OS_SellerCloud.Orders_DeletePackage(OS_AuthHeader, OS_Options, PackageID);
        }

        public bool Delete_Item(int OrderID, int ItemID)
        {
            return OCS_SellerCloud.OrderItem_Delete(OCS_AuthHeader, OrderID, ItemID);
        }

        public bool Delete_ItemSerials(int OrderID, int ItemID)
        {
            return OCS_SellerCloud.OrderItem_DeleteAllSerialsByOrderItemID(OCS_AuthHeader, OrderID, ItemID);
        }
        /***** 刪除資料 *****/

        /***** 商品退貨 *****/
        public QDLogistics.OrderCreationService.ReturnReason[] Get_RMA_Reason_List()
        {
            return OCS_SellerCloud.RMA_ListReasons(OCS_AuthHeader);
        }

        public QDLogistics.OrderCreationService.RMA Get_RMA_by_ID(int RMAID)
        {
            return OCS_SellerCloud.RMA_GetRMAByID(OCS_AuthHeader, RMAID);
        }

        public RMAData Get_RMA_Data(int RMAID)
        {
            return PO_SellerCloud.RMA_GetRMA(PO_AuthHeader, PO_Options, RMAID);
        }

        public QDLogistics.OrderCreationService.RMA Get_RMA_by_OrderID(int OrderID)
        {
            return OCS_SellerCloud.RMA_GetRMAByOrderID(OCS_AuthHeader, OrderID);
        }

        public QDLogistics.OrderCreationService.RMAItem[] Get_RMA_Item(int OrderID)
        {
            return OCS_SellerCloud.RMA_GetRMAItems(OCS_AuthHeader, OrderID);
        }

        public int Create_RMA(int OrderID)
        {
            return OCS_SellerCloud.RMA_CreateNew(OCS_AuthHeader, OrderID);
        }

        public int Create_RMA_Item(int OrderID, int OrderItemID, int RMAID, int QtyToReturn, int RMAReason, string RMADescription, string KitProductID = "")
        {
            return OCS_SellerCloud.RMAItem_CreateNew(OCS_AuthHeader, OrderID, OrderItemID, RMAID, KitProductID, QtyToReturn, RMAReason, RMADescription);
        }

        public bool Update_RMA(RMAData RMAData, bool IsUpdateItems = false)
        {
            return PO_SellerCloud.RMA_UpdateRMA(PO_AuthHeader, RMAData, IsUpdateItems, false);
        }

        public bool Update_RAM_Item(QDLogistics.PurchaseOrderService.RMAItem item)
        {
            return PO_SellerCloud.RMA_UpdateRMAItem(PO_AuthHeader, item);
        }

        public bool Receive_RMA_Item(int RMAItemID, string ProductID, int QtyToReceive, int WarehouseID, string DestinationBinName = "")
        {
            return PO_SellerCloud.RMA_ReceiveItem_New(PO_AuthHeader, RMAItemID, ProductID, QtyToReceive, DestinationBinName, WarehouseID);
        }

        public bool Receive_RMA_Item(int RMAID, int RMAItemID, string ProductID, int QtyToReceive, int WarehouseID, string SerialsList, string DestinationBinName = "", int QtyReturnedDoNotCount = 0)
        {
            return PO_SellerCloud.RMA_ReceiveItem_New2(PO_AuthHeader, RMAID, RMAItemID, ProductID, QtyToReceive, DestinationBinName, WarehouseID, SerialsList, QtyReturnedDoNotCount);
        }

        /***** 採購單 *****/
        public Purchase Get_PurchaseOrder(int POId)
        {
            return PO_SellerCloud.GetPurchaseOrder(PO_AuthHeader, PO_Options, POId);
        }

        public PurchaseOrderInfo Get_PurchaseOrder_Info(int PurchaseID, int WarehouseID)
        {
            return PO_SellerCloud.GetPurchaseOrderInfo(PO_AuthHeader, PO_Options, PurchaseID, WarehouseID);
        }

        public int Get_CurrentCompanyID()
        {
            return PO_SellerCloud.GetCurrentCompanyID(PO_AuthHeader);
        }

        public Purchase Create_PurchaseOrder(Purchase PurchaseOrder)
        {
            return PO_SellerCloud.CreateNewPurchaseOrder(PO_AuthHeader, PurchaseOrder);
        }

        public PurchaseItem Create_PurchaseOrder_Item(PurchaseItem PurchaseItem)
        {
            return PO_SellerCloud.PurchaseOrderItems_CreateNew(PO_AuthHeader, PurchaseItem);
        }

        public PurchaseItemReceive[] Create_PurchaseOrder_ItemReceive(PurchaseItemReceiveRequest Receive)
        {
            return PO_SellerCloud.PurchaseItemReceive_AddNew_Bulk(PO_AuthHeader, Receive);
        }

        public bool Update_PurchaseOrder(Purchase PurchaseOrder)
        {
            return PO_SellerCloud.UpdatePurchaseOrder(PO_AuthHeader, PurchaseOrder);
        }

        public bool Update_PurchaseOrder_ItemReceive_Serials(QDLogistics.PurchaseOrderService.PurchaseItemReceiveSerial[] Serials)
        {
            return PO_SellerCloud.PurchaseItem_SerialNumbersNew_SaveMultiple(PO_AuthHeader, Serials);
        }

        public bool Delete_PurchaseOrder(int POId)
        {
            return PO_SellerCloud.DeletePurchaseOrder(PO_AuthHeader, POId);
        }

        public bool Delete_PurchaseOrder_ItemReceive(PurchaseItemReceive Receive)
        {
            return PO_SellerCloud.PurchaseItemReceive_Delete(PO_AuthHeader, Receive.Id);
        }

        public bool Delete_PurchaseOrder_ItemReceive_Serials(QDLogistics.PurchaseOrderService.PurchaseItemReceiveSerial[] Serials)
        {
            return PO_SellerCloud.PurchaseItem_SerialNumbersNew_DeleteMultiple(PO_AuthHeader, Serials);
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
            }

            OS_SellerCloud = null;
            OS_AuthHeader = null;
            OS_Options = null;

            OCS_SellerCloud = null;
            OCS_AuthHeader = null;

            PO_SellerCloud = null;
            PO_AuthHeader = null;
            PO_Options = null;

            disposed = true;
        }
    }
}