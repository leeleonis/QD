using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataSync
{
    public static class MyHelp
    {
        public static IEnumerable<Country> GetCountries()
        {
            var result = CultureInfo.GetCultures(CultureTypes.SpecificCultures).Where(x => x.LCID != 4096)
                 .Select(x => new Country { ID = new RegionInfo(x.LCID).Name, Name = new RegionInfo(x.LCID).EnglishName })
                 .GroupBy(c => c.ID).Select(c => c.First()).OrderBy(x => x.Name);

            return result;
        }
    }

    public class Orders : IEquatable<Orders>
    {
        public int OrderID { get; set; }
        public Nullable<int> ParentOrderID { get; set; }
        public Nullable<int> ClientID { get; set; }
        public Nullable<int> CompanyID { get; set; }
        public Nullable<int> UserID { get; set; }
        public string UserName { get; set; }
        public Nullable<int> SiteCode { get; set; }
        public Nullable<DateTime> TimeOfOrder { get; set; }
        public Nullable<decimal> SubTotal { get; set; }
        public Nullable<decimal> ShippingTotal { get; set; }
        public Nullable<decimal> OrderDiscountsTotal { get; set; }
        public Nullable<decimal> GrandTotal { get; set; }
        public Nullable<int> StatusCode { get; set; }
        public Nullable<int> PaymentStatus { get; set; }
        public Nullable<int> ShippingStatus { get; set; }
        public Nullable<DateTime> ShipDate { get; set; }
        public Nullable<decimal> FinalShippingFee { get; set; }
        public Nullable<int> ShippingAddress { get; set; }
        public Nullable<int> OrderCurrencyCode { get; set; }
        public Nullable<int> OrderSource { get; set; }
        public string OrderSourceOrderId { get; set; }
        public Nullable<decimal> OrderSourceOrderTotal { get; set; }
        public string eBayUserID { get; set; }
        public string eBaySalesRecordNumber { get; set; }
        public string ShippingServiceSelected { get; set; }
        public Nullable<bool> RushOrder { get; set; }
        public Nullable<bool> InvoicePrinted { get; set; }
        public Nullable<DateTime> InvoicePrintedDate { get; set; }
        public string ShippingCarrier { get; set; }
        public string ShippingCountry { get; set; }
        public string PackageType { get; set; }
        public Nullable<int> StationID { get; set; }
        public Nullable<int> CustomerServiceStatus { get; set; }
        public Nullable<decimal> TaxRate { get; set; }
        public Nullable<decimal> TaxTotal { get; set; }
        public string GoogleOrderNumber { get; set; }
        public Nullable<bool> IsInDispute { get; set; }
        public Nullable<DateTime> DisputeStartedOn { get; set; }
        public Nullable<decimal> PaypalFeeTotal { get; set; }
        public Nullable<decimal> PostingFeeTotal { get; set; }
        public Nullable<decimal> FinalValueTotal { get; set; }
        public Nullable<int> OrderItemCount { get; set; }
        public Nullable<int> OrderQtyTotal { get; set; }
        public Nullable<int> ShippingWeightTotalOz { get; set; }
        public Nullable<bool> IsConfirmed { get; set; }
        public Nullable<int> ConfirmBy { get; set; }
        public Nullable<System.DateTime> ConfirmOn { get; set; }
        public Nullable<int> MarkettingSourceID { get; set; }
        public Nullable<int> ShippedBy { get; set; }
        public string Instructions { get; set; }
        public Nullable<DateTime> SyncOn { get; set; }

        public bool Equals(Orders other)
        {
            if (Object.ReferenceEquals(other, null)) return false;

            if (Object.ReferenceEquals(this, other)) return true;

            return OrderID.Equals(other.OrderID);
        }

        public override int GetHashCode()
        {
            int hashVendorID = OrderID.GetHashCode();

            return hashVendorID;
        }
    }

    public class OrdersComparer : IEqualityComparer<Orders>
    {
        public bool Equals(Orders x, Orders y)
        {
            if (Object.ReferenceEquals(x, y)) return true;

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null)) return false;

            return x.OrderID == y.OrderID && x.StatusCode == y.StatusCode && x.PaymentStatus == y.PaymentStatus && x.ShippingStatus == y.ShippingStatus
                && x.IsConfirmed == y.IsConfirmed && x.ConfirmBy == y.ConfirmBy && x.ConfirmOn == y.ConfirmOn;
        }

        public int GetHashCode(Orders obj)
        {
            if (Object.ReferenceEquals(obj, null)) return 0;

            int hashOrderID = obj.OrderID.GetHashCode();
            int hashStatusCode = obj.StatusCode == null ? 0 : obj.StatusCode.GetHashCode();
            int hashPaymentStatus = obj.PaymentStatus == null ? 0 : obj.PaymentStatus.GetHashCode();
            int hashShippingStatus = obj.ShippingStatus == null ? 0 : obj.ShippingStatus.GetHashCode();
            int hashIsConfirmed = obj.IsConfirmed == null ? 0 : obj.IsConfirmed.GetHashCode();
            int hashConfirmBy = obj.ConfirmBy == null ? 0 : obj.ConfirmBy.GetHashCode();
            int hashConfirmOn = obj.ConfirmOn == null ? 0 : obj.ConfirmOn.GetHashCode();

            return hashOrderID ^ hashStatusCode ^ hashPaymentStatus ^ hashShippingStatus ^ hashIsConfirmed ^ hashConfirmBy ^ hashConfirmOn;
        }
    }

    public class Addresses
    {
        public Nullable<bool> IsEnable { get; set; }
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string MiddleInitial { get; set; }
        public string LastName { get; set; }
        public string CompanyName { get; set; }
        public string HomePhone { get; set; }
        public string PhoneNumber { get; set; }
        public string FaxNumber { get; set; }
        public string EmailAddress { get; set; }
        public string CountryName { get; set; }
        public string CountryCode { get; set; }
        public string City { get; set; }
        public string StateCode { get; set; }
        public string StateName { get; set; }
        public string PostalCode { get; set; }
        public string StreetLine1 { get; set; }
        public string StreetLine2 { get; set; }
        public Nullable<int> AddressSource { get; set; }
        public Nullable<int> AddressState { get; set; }
        public string Notes { get; set; }
    }

    public class Payments : IEquatable<Payments>
    {
        public Nullable<bool> IsEnable { get; set; }
        public int ID { get; set; }
        public Nullable<int> OrderID { get; set; }
        public Nullable<int> CurrentApplicationID { get; set; }
        public Nullable<int> PaymentMethod { get; set; }
        public Nullable<int> PaymentStatus { get; set; }
        public Nullable<int> PaymentType { get; set; }
        public Nullable<DateTime> AuditDate { get; set; }
        public Nullable<decimal> Amount { get; set; }
        public string TransactionReferenceNumber { get; set; }
        public string Note { get; set; }

        public bool Equals(Payments other)
        {
            if (Object.ReferenceEquals(other, null)) return false;

            if (Object.ReferenceEquals(this, other)) return true;

            return ID.Equals(other.ID);
        }

        public override int GetHashCode()
        {
            int hashID = ID.GetHashCode();

            return hashID;
        }
    }

    public class PaymentsComparer : IEqualityComparer<Payments>
    {
        public bool Equals(Payments x, Payments y)
        {
            if (Object.ReferenceEquals(x, y)) return true;

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null)) return false;

            return x.PaymentStatus == y.PaymentStatus && x.AuditDate == y.AuditDate;
        }

        public int GetHashCode(Payments obj)
        {
            if (Object.ReferenceEquals(obj, null)) return 0;

            int hashPaymentStatus = obj.PaymentStatus == null ? 0 : obj.PaymentStatus.GetHashCode();
            int hashAuditDate = obj.AuditDate == null ? 0 : obj.AuditDate.GetHashCode();

            return hashPaymentStatus ^ hashAuditDate;
        }
    }

    public class Packages : IEquatable<Packages>
    {
        public Nullable<bool> IsEnable { get; set; }
        public int ID { get; set; }
        public Nullable<int> OrderID { get; set; }
        public Nullable<int> OrderItemID { get; set; }
        public Nullable<int> BundleItemID { get; set; }
        public byte ProcessStatus { get; set; }
        public bool ProcessBack { get; set; }
        public Nullable<int> Qty { get; set; }
        public Nullable<DateTime> ShipDate { get; set; }
        public string ShippingMethodName { get; set; }
        public string ShippingServiceCode { get; set; }
        public Nullable<int> CarrierID { get; set; }
        public Nullable<byte> Export { get; set; }
        public Nullable<byte> ExportMethod { get; set; }
        public Nullable<DateTime> EstimatedDeliveryDate { get; set; }
        public Nullable<DateTime> DeliveryDate { get; set; }
        public Nullable<int> DeliveryStatus { get; set; }
        public Nullable<decimal> FinalShippingFee { get; set; }
        public string TrackingNumber { get; set; }
        public string Comment { get; set; }
        public Nullable<double> Weight { get; set; }
        public Nullable<double> Length { get; set; }
        public Nullable<double> Width { get; set; }
        public Nullable<double> Height { get; set; }

        public bool Equals(Packages other)
        {
            if (Object.ReferenceEquals(other, null)) return false;

            if (Object.ReferenceEquals(this, other)) return true;

            return ID.Equals(other.ID);
        }

        public override int GetHashCode()
        {
            int hashID = ID.GetHashCode();

            return hashID;
        }
    }

    public class PackagesComparer : IEqualityComparer<Packages>
    {
        public bool Equals(Packages x, Packages y)
        {
            if (Object.ReferenceEquals(x, y)) return true;

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null)) return false;

            return x.OrderItemID == y.OrderItemID && x.BundleItemID == y.BundleItemID && x.ShipDate == y.ShipDate && x.ShippingMethodName == y.ShippingMethodName
                 && x.ShippingServiceCode == y.ShippingServiceCode && x.TrackingNumber == y.TrackingNumber;
        }

        public int GetHashCode(Packages obj)
        {
            if (Object.ReferenceEquals(obj, null)) return 0;
            
            int hashOrderItemID = obj.OrderItemID == null ? 0 : obj.OrderItemID.GetHashCode();
            int hashBundleItemID = obj.BundleItemID == null ? 0 : obj.BundleItemID.GetHashCode();
            int hashShipDate = obj.ShipDate == null ? 0 : obj.ShipDate.GetHashCode();
            int hashShippingMethodName = obj.ShippingMethodName == null ? 0 : obj.ShippingMethodName.GetHashCode();
            int hashShippingServiceCode = obj.ShippingServiceCode == null ? 0 : obj.ShippingServiceCode.GetHashCode();
            int hashTrackingNumber = obj.TrackingNumber == null ? 0 : obj.TrackingNumber.GetHashCode();

            return hashOrderItemID ^ hashBundleItemID ^ hashShipDate ^ hashShippingMethodName ^ hashShippingServiceCode ^ hashTrackingNumber;
        }
    }

    public class Items : IEquatable<Items>
    {
        public Nullable<bool> IsEnable { get; set; }
        public int ID { get; set; }
        public Nullable<int> OrderID { get; set; }
        public Nullable<int> PackageID { get; set; }
        public string SKU { get; set; }
        public string OriginalSKU { get; set; }
        public string ProductID { get; set; }
        public string ProductIDOriginal { get; set; }
        public string ProductIDRequest { get; set; }
        public Nullable<int> Qty { get; set; }
        public Nullable<int> QtyReturned { get; set; }
        public Nullable<int> QtyShipped { get; set; }
        public string DisplayName { get; set; }
        public Nullable<decimal> LineTotal { get; set; }
        public Nullable<int> KitItemCount { get; set; }
        public string eBayItemID { get; set; }
        public string OrderSourceItemID { get; set; }
        public string OrderSourceTransactionID { get; set; }
        public Nullable<int> BackOrderQty { get; set; }
        public Nullable<decimal> UnitPrice { get; set; }
        public string SerialNumber { get; set; }
        public Nullable<int> ShipFromWarehouseID { get; set; }
        public Nullable<int> ReturnedToWarehouseID { get; set; }
        public Nullable<double> Weight { get; set; }

        public bool Equals(Items other)
        {
            if (Object.ReferenceEquals(other, null)) return false;

            if (Object.ReferenceEquals(this, other)) return true;

            return ID.Equals(other.ID);
        }

        public override int GetHashCode()
        {
            int hashID = ID.GetHashCode();

            return hashID;
        }
    }

    public class ItemsComparer : IEqualityComparer<Items>
    {
        public bool Equals(Items x, Items y)
        {
            if (Object.ReferenceEquals(x, y)) return true;

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null)) return false;

            return x.PackageID == y.PackageID && x.Qty == y.Qty && x.QtyShipped == y.QtyShipped && x.QtyReturned == y.QtyReturned && x.OrderSourceItemID == y.OrderSourceItemID
                 && x.OrderSourceTransactionID == y.OrderSourceTransactionID && x.ShipFromWarehouseID == y.ShipFromWarehouseID && x.ReturnedToWarehouseID == y.ReturnedToWarehouseID;
        }

        public int GetHashCode(Items obj)
        {
            if (Object.ReferenceEquals(obj, null)) return 0;

            int hashPackageID = obj.PackageID == null ? 0 : obj.PackageID.GetHashCode();
            int hashQty = obj.Qty == null ? 0 : obj.Qty.GetHashCode();
            int hashQtyShipped = obj.QtyShipped == null ? 0 : obj.QtyShipped.GetHashCode();
            int hashQtyReturned = obj.QtyReturned == null ? 0 : obj.QtyReturned.GetHashCode();
            int hashOrderSourceItemID = obj.OrderSourceItemID == null ? 0 : obj.OrderSourceItemID.GetHashCode();
            int hashOrderSourceTransactionID = obj.OrderSourceTransactionID == null ? 0 : obj.OrderSourceTransactionID.GetHashCode();
            int hashShipFromWarehouseID = obj.ShipFromWarehouseID == null ? 0 : obj.ShipFromWarehouseID.GetHashCode();
            int hashReturnedToWarehouseID = obj.ReturnedToWarehouseID == null ? 0 : obj.ReturnedToWarehouseID.GetHashCode();

            return hashPackageID ^ hashQty ^ hashQtyShipped ^ hashQtyReturned ^ hashOrderSourceItemID ^ hashOrderSourceTransactionID ^ hashShipFromWarehouseID ^ hashReturnedToWarehouseID;
        }
    }

    public class BundleItems
    {
        public Nullable<bool> IsEnable { get; set; }
        public int ID { get; set; }
        public Nullable<int> OrderID { get; set; }
        public Nullable<int> OrderItemId { get; set; }
        public Nullable<int> PackageID { get; set; }
        public string ProductID { get; set; }
        public string ProductName { get; set; }
        public Nullable<int> Qty { get; set; }
        public Nullable<int> QtyReturned { get; set; }
        public Nullable<int> BackOrderOty { get; set; }
    }

    public class SerialNumbers: IEquatable<SerialNumbers>
    {
        public Nullable<int> OrderID { get; set; }
        public string ProductID { get; set; }
        public string SerialNumber { get; set; }
        public Nullable<int> OrderItemID { get; set; }
        public Nullable<int> KitItemID { get; set; }

        public bool Equals(SerialNumbers other)
        {
            if (Object.ReferenceEquals(other, null)) return false;

            if (Object.ReferenceEquals(this, other)) return true;

            return SerialNumber.Equals(other.SerialNumber);
        }

        public override int GetHashCode()
        {
            int hashSerialNumber = SerialNumber.GetHashCode();

            return hashSerialNumber;
        }
    }

    public class Warehouses : IEquatable<Warehouses>
    {
        public Nullable<bool> IsEnable { get; set; }
        public Nullable<bool> IsDefault { get; set; }
        public Nullable<bool> IsSellable { get; set; }
        public Nullable<bool> AllowUseQtyForFBAShipments { get; set; }
        public Nullable<bool> EnforceBins { get; set; }
        public int ID { get; set; }
        public Nullable<int> CompanyID { get; set; }
        public string Name { get; set; }
        public string QBWarehouseName { get; set; }
        public Nullable<int> WarehouseType { get; set; }
        public string DropShipCentralWarehouseCode { get; set; }
        public string CarrierData { get; set; }
        public Nullable<int> CreatedBy { get; set; }
        public Nullable<DateTime> CreatedOn { get; set; }

        public bool Equals(Warehouses other)
        {
            if (Object.ReferenceEquals(other, null)) return false;

            if (Object.ReferenceEquals(this, other)) return true;

            return ID.Equals(other.ID);
        }

        public override int GetHashCode()
        {
            int hashID = ID.GetHashCode();

            return hashID;
        }
    }

    public class Services : IEquatable<Services>
    {
        public Nullable<bool> IsEnable { get; set; }
        public string ServiceCode { get; set; }
        public string ServiceName { get; set; }
        public Nullable<int> CarrierID { get; set; }

        public bool Equals(Services other)
        {
            if (Object.ReferenceEquals(other, null)) return false;

            if (Object.ReferenceEquals(this, other)) return true;

            return ServiceCode.Equals(other.ServiceCode);
        }

        public override int GetHashCode()
        {
            int hashServiceCode = ServiceCode.GetHashCode();

            return hashServiceCode;
        }
    }

    public class Manufacturers : IEquatable<Manufacturers>
    {
        public Nullable<bool> IsEnable { get; set; }
        public int ID { get; set; }
        public Nullable<int> CompanyID { get; set; }
        public string ManufacturerName { get; set; }

        public bool Equals(Manufacturers other)
        {
            if (Object.ReferenceEquals(other, null)) return false;

            if (Object.ReferenceEquals(this, other)) return true;

            return ID.Equals(other.ID);
        }

        public override int GetHashCode()
        {
            int hashID = ID.GetHashCode();

            return hashID;
        }
    }

    public class ProductType : IEquatable<ProductType>
    {
        public ProductType()
        {
            this.IsEnable = true;
        }

        public bool IsEnable { get; set; }
        public int ID { get; set; }
        public string ProductTypeName { get; set; }

        public bool Equals(ProductType other)
        {
            if (Object.ReferenceEquals(other, null)) return false;

            if (Object.ReferenceEquals(this, other)) return true;

            return ID.Equals(other.ID);
        }

        public override int GetHashCode()
        {
            int hashVendorID = ID.GetHashCode();

            return hashVendorID;
        }
    }

    public class Skus : IEquatable<Skus>
    {
        public Nullable<bool> IsEnable { get; set; }
        public string Sku { get; set; }
        public string ProductName { get; set; }
        public Nullable<int> ProductTypeID { get; set; }
        public string ParentShadow { get; set; }
        public string ParentKit { get; set; }
        public string UPC { get; set; }
        public string Origin { get; set; }
        public Nullable<int> Brand { get; set; }
        public Nullable<bool> Battery { get; set; }
        public Nullable<byte> Export { get; set; }
        public Nullable<byte> ExportMethod { get; set; }

        public bool Equals(Skus other)
        {
            if (Object.ReferenceEquals(other, null)) return false;

            if (Object.ReferenceEquals(this, other)) return true;

            return Sku.Equals(other.Sku);
        }

        public override int GetHashCode()
        {
            int hashSku = Sku.GetHashCode();

            return hashSku;
        }
    }

    class PurchaseItemReceive : IEquatable<PurchaseItemReceive>
    {
        public bool IsRequireSerialScan { get; set; }
        public Nullable<int> OrderID { get; set; }
        public Nullable<int> OrderItemID { get; set; }
        public string ProductID { get; set; }
        public string SerialNumber { get; set; }
        public Nullable<int> PurchaseID { get; set; }
        public Nullable<int> PurchaseReceiveID { get; set; }
        public Nullable<int> RMAId { get; set; }
        public Nullable<int> CreditMemoID { get; set; }
        public string CreditMemoReason { get; set; }
        public Nullable<int> WarehouseID { get; set; }
        public string WarehouseName { get; set; }
        public Nullable<int> LocationBinID { get; set; }
        public string BinName { get; set; }

        public bool Equals(PurchaseItemReceive other)
        {
            if (Object.ReferenceEquals(other, null)) return false;

            if (Object.ReferenceEquals(this, other)) return true;

            return SerialNumber.Equals(other.SerialNumber);
        }

        public override int GetHashCode()
        {
            int hashSerialNumber = SerialNumber.GetHashCode();

            return hashSerialNumber;
        }
    }

    public class Country
    {
        public string ID { get; set; }
        public string Name { get; set; }
    }
}
