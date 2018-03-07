using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace QDLogistics.Models
{
    [MetadataType(typeof(Items))]
    public partial class Items : IEquatable<Items>
    {
        public bool Equals(Items other)
        {
            if (ReferenceEquals(other, null)) return false;

            if (ReferenceEquals(this, other)) return true;

            return ID.Equals(other.ID);
        }

        public override int GetHashCode()
        {
            int hashID = ID.GetHashCode();

            return hashID;
        }
    }

    public class ItemComparer : IEqualityComparer<Items>
    {
        public bool Equals(Items x, Items y)
        {
            if (ReferenceEquals(x, y)) return true;

            if (ReferenceEquals(x, null) || ReferenceEquals(y, null)) return false;

            return x.PackageID == y.PackageID && x.ProductID == y.ProductID && x.ProductIDOriginal == y.ProductIDOriginal && x.ProductIDRequest == y.ProductIDRequest
                && x.Qty == y.Qty && x.QtyShipped == y.QtyShipped && x.QtyReturned == y.QtyReturned && x.LineTotal == y.LineTotal
                && x.eBayItemID == y.eBayItemID && x.eBayTransactionId == y.eBayTransactionId && x.SalesRecordNumber == y.SalesRecordNumber
                && x.UnitPrice == y.UnitPrice && x.ShipFromWarehouseID == y.ShipFromWarehouseID && x.ReturnedToWarehouseID == y.ReturnedToWarehouseID;
        }

        public int GetHashCode(Items obj)
        {
            if (ReferenceEquals(obj, null)) return 0;

            int hashID = obj.ID.GetHashCode();
            int hashPackageID = obj.PackageID == null ? 0 : obj.PackageID.GetHashCode();
            int hashProductID = obj.ProductID == null ? 0 : obj.ProductID.GetHashCode();
            int hashProductIDOriginal = obj.ProductIDOriginal == null ? 0 : obj.ProductIDOriginal.GetHashCode();
            int hashProductIDRequest = obj.ProductIDRequest == null ? 0 : obj.ProductIDRequest.GetHashCode();
            int hashQty = obj.Qty == null ? 0 : obj.Qty.GetHashCode();
            int hashQtyShipped = obj.QtyShipped == null ? 0 : obj.QtyShipped.GetHashCode();
            int hashQtyReturned = obj.QtyReturned == null ? 0 : obj.QtyReturned.GetHashCode();
            int hashLineTotal = obj.LineTotal == null ? 0 : obj.LineTotal.GetHashCode();
            int hashEBayItemID = obj.eBayItemID == null ? 0 : obj.eBayItemID.GetHashCode();
            int hashEBayTransactionId = obj.eBayTransactionId == null ? 0 : obj.eBayTransactionId.GetHashCode();
            int hashSalesRecordNumber = obj.SalesRecordNumber == null ? 0 : obj.SalesRecordNumber.GetHashCode();
            int hashUnitPrice = obj.UnitPrice == null ? 0 : obj.UnitPrice.GetHashCode();
            int hashShipFromWarehouseID = obj.ShipFromWarehouseID == null ? 0 : obj.ShipFromWarehouseID.GetHashCode();
            int hashReturnedToWarehouseID = obj.ReturnedToWarehouseID == null ? 0 : obj.ReturnedToWarehouseID.GetHashCode();

            return hashID ^ hashPackageID ^ hashProductID ^ hashProductIDOriginal ^ hashProductIDRequest ^ hashQty ^ hashQtyShipped ^ hashQtyReturned ^ hashLineTotal
                ^ hashEBayItemID ^ hashEBayTransactionId ^ hashSalesRecordNumber ^ hashUnitPrice ^ hashShipFromWarehouseID ^ hashReturnedToWarehouseID;
        }
    }
}