using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace CarrierApi.Winit_Old
{

    public class Token
    {
        public string action { get; set; }
        public string app_key { get; set; }
    }

    public class Token2 : Token
    {
        public string format { get; set; }
        public string language { get; set; }
        public string platform { get; set; }
        public string sign { get; set; }
        public string sign_method { get; set; }
        public string timestamp { get; set; }
        public string version { get; set; }
    }

    #region 獲取Token用

    public class getToken : Token
    {
        public getTokendata data { get; set; }

    }

    public class getTokendata
    {
        public string userName { get; set; }
        public string passWord { get; set; }
    }

    #endregion

    #region 查詢提貨單

    public class queryOutboundOrderList : Token2
    {
        public queryOutboundOrderList_data data { get; set; }
    }

    public class queryOutboundOrderList_data
    {
        private string _warehouseID = "";
        private string _outboundOrderNum = "";
        private string _sellerOrderNo = "";
        private string _trackingNo = "";

        public string warehouseId { get { return _warehouseID; } set { _warehouseID = value; } }
        public string outboundOrderNum { get { return _outboundOrderNum; } set { _outboundOrderNum = value; } }
        public string sellerOrderNo { get { return _sellerOrderNo; } set { _sellerOrderNo = value; } }
        public string trackingNo { get { return _trackingNo; } set { _trackingNo = value; } }
        public string dateOrderedStartDate { get; set; }
        public string dateOrderedEndDate { get; set; }
        public string status { get; set; }
        public string pageSize { get; set; }
        public string pageNum { get; set; }
    }

    public class outboundOrderListResult
    {
        public int currentPageNum { get; set; }
        public int currentPageSize { get; set; }
        public int total { get; set; }
        public outboundOrderListData[] list { get; set; }
    }

    public class outboundOrderListData
    {
        public string exwarehouseId { get; set; }
        public string ontimeStatus { get; set; }
        public string storageOntime { get; set; }
        public string serviceStandardTime { get; set; }
        public string serviceCompleteTime { get; set; }
        public string documentNo { get; set; }
        public string eBaySellerID { get; set; }
        public string status { get; set; }
        public string statusName { get; set; }
        public string sellerOrderNo { get; set; }
        public string warehouseId { get; set; }
        public string warehouseName { get; set; }
        public string deliverywayId { get; set; }
        public string deliverywayName { get; set; }
        public string trackingNo { get; set; }
        public Nullable<DateTime> dateOrdered { get; set; }
        public Nullable<DateTime> dateFinish { get; set; }
    }

    #endregion

    #region 軌跡查詢

    public class queryTrack : Token2
    {
        public queryTrack_data data { get; set; }
    }

    public class queryTrack_data
    {
        private string _trackingNum = "";
        private string _outboundNum = "";

        public string warehouseID { get; set; }
        public string trackingNum { get { return _trackingNum; } set { _trackingNum = value; } }
        public string outboundNum { get { return _outboundNum; } set { _outboundNum = value; } }
    }

    public class trackData
    {
        public string status { get; set; }
        public DateTime scandateTime { get; set; }
        public string scanlocation { get; set; }
        public string trackingmess { get; set; }
    }

    #endregion

    #region 海外倉出庫單查詢

    public class queryOutboundOrder : Token2
    {
        public queryOutboundOrder_data data { get; set; }

    }
    public class queryOutboundOrder_data
    {
        public string outboundOrderNum { get; set; }
    }

    public class outboundOrderResult
    {
        public int currentPageNum { get; set; }
        public int currentPageSize { get; set; }
        public int total { get; set; }
        public outboundOrderData[] list { get; set; }
    }

    public class outboundOrderData
    {
        public string sellerOrderNo { get; set; }
        public string eBayNo { get; set; }
        public string eBayOrderID { get; set; }
        public string outboundOrderNum { get; set; }
        public string outboundOrderID { get; set; }
        public string warehouseName { get; set; }
        public string orderedTime { get; set; }
        public string outboundDate { get; set; }
        public string status { get; set; }
        public string statusName { get; set; }
        public string state { get; set; }
        public string city { get; set; }
        public string regionName { get; set; }
        public string address1 { get; set; }
        public string address2 { get; set; }
        public string phoneNum { get; set; }
        public string recipientName { get; set; }
        public string emailAddress { get; set; }
        public string zipCode { get; set; }
        public string deliveryWay { get; set; }
        public string deliveryWayID { get; set; }
        public string insuranceType { get; set; }
        public string insuranceTypeID { get; set; }
        public string trackingNum { get; set; }
        public string carrier { get; set; }
        public string chargeableWeight { get; set; }
        public string transactionDate { get; set; }
        public string deliveryCosts { get; set; }
        public string deliveryCostsCode { get; set; }
        public string ebayName { get; set; }
        public string handlingFee { get; set; }
        public string handlingfeeCode { get; set; }
        public string totalCost { get; set; }
        public string rate { get; set; }
        public string isRepeat { get; set; }
        public string isError { get; set; }
        public string actualFinishTime { get; set; }
        public string scheduledFinishTime { get; set; }
        public string deliveryCompletionStatus { get; set; }
        public string action { get; set; }
        public string c_Country_ID { get; set; }
        public string errorMsg { get; set; }
        public string productCode { get; set; }
        public string specification { get; set; }
        public string productNum { get; set; }
        public string winitTrackingNo { get; set; }
    }

    #endregion

    #region 創建海外倉出庫單(草稿)

    public class createOutboundInfo : Token2
    {
        public createOutboundInfo_data data { get; set; }
    }

    public class createOutboundInfo_data
    {

        public string address1 { get; set; }
        public string address2 { get; set; }
        public string city { get; set; }
        public string deliveryWayID { get; set; }
        public string eBayOrderID { get; set; }
        public string emailAddress { get; set; }
        public int insuranceTypeID { get; set; }
        public string phoneNum { get; set; }

        public List<createOutboundInfo_productList> productList { get; set; }
        public string recipientName { get; set; }
        public string region { get; set; }
        public string repeatable { get; set; }
        public string sellerOrderNo { get; set; }
        public string state { get; set; }
        public int warehouseID { get; set; }
        public string zipCode { get; set; }

    }

    public class createOutboundInfo_productList
    {
        public string eBayBuyerID { get; set; }
        public string eBayItemID { get; set; }
        public string eBaySellerID { get; set; }
        public string eBayTransactionID { get; set; }
        public string productCode { get; set; }
        public string productNum { get; set; }
        public string specification { get; set; }
    }

    #endregion

    #region 創建海外倉出庫單

    public class createOutboundOrder : Token2
    {
        public createOutboundOrder_data data { get; set; }
    }

    public class createOutboundOrder_data : createOutboundInfo_data
    {
        public string isShareOrder { get; set; }
        public string fromBpartnerId { get; set; }
    }

    public class createOutboundOrderData
    {
        public string outboundOrderNum { get; set; }
    }

    #endregion

    #region 提交出庫單

    public class confirmOutboundOrder : Token2
    {
        public confirmOutboundOrder_data data { get; set; }

    }
    public class confirmOutboundOrder_data
    {
        public string outboundOrderNum { get; set; }
    }

    #endregion

    #region 作廢出庫單

    public class voidOutboundOrder : Token2
    {
        public voidOutboundOrder_data data { get; set; }

    }
    public class voidOutboundOrder_data
    {
        public string outboundOrderNum { get; set; }
    }

    #endregion*/

    #region 海外倉倉庫查詢

    public class queryWarehouse : Token2
    {
        public object data { get; set; }
    }

    public class warehouseData
    {
        public string warehouseName { get; set; }
        public string warehouseID { get; set; }
        public string warehouseAddress { get; set; }
    }

    #endregion

    #region 遞送方式查詢

    public class queryDeliveryWay : Token2
    {
        public object data { get; set; }
    }

    public class deliveryWayData
    {
        public string deliveryWay { get; set; }
        public string deliveryID { get; set; }
        public string isMandoorplateNumbers { get; set; }
        public string warehouseID { get; set; }
    }

    #endregion

    public class Received
    {
        public string code { get; set; }
        public dynamic data { get; set; }
        public string msg { get; set; }

    }

    /* #region 註冊商品用

    public class Rgpd : Token2
    {
        public getRgpd_data data { get; set; }
    }

    public class getRgpd_data
    {
        public List<productListdata> productList { get; set; }
    }

    public class productListdata
    {
        public string productCode { get; set; }
        public string chineseName { get; set; }
        public string englishName { get; set; }
        public string categoryOne { get; set; }
        public string categoryTwo { get; set; }
        public string categoryThree { get; set; }
        public int registeredWeight { get; set; }
        public string fixedVolumeWeight { get; set; }
        public int registeredLength { get; set; }
        public int registeredWidth { get; set; }
        public int registeredHeight { get; set; }
        public string branded { get; set; }
        public string brandedName { get; set; }
        public string model { get; set; }
        public string displayPageUrl { get; set; }
        public string remark { get; set; }
        public string exportCountry { get; set; }
        public string inporCountry { get; set; }
        public int inportDeclaredvalue { get; set; }
        public int exportDeclaredvalue { get; set; }
        public string battery { get; set; }

    }
    #endregion

    #region 查詢商品單品信息

    public class getItemInformation : Token2
    {
        public getItemInformation_data data { get; set; }

    }
    public class getItemInformation_data
    {
        public string itemBarcode { get; set; }
    }

    #endregion
    
    #region 查詢商品分類

    public class getProductCategoryInfo : Token2
    {
        public getProductCategoryInfo_data data { get; set; }

    }
    public class getProductCategoryInfo_data
    {
        public int categoryID { get; set; }
    }

    #endregion */
}