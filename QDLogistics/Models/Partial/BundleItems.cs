using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace QDLogistics.Models
{
    [MetadataType(typeof(BundleItems))]
    public partial class BundleItems : IEquatable<BundleItems>
    {
        public bool Equals(BundleItems other)
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

    public class BundleItemComparer : IEqualityComparer<BundleItems>
    {
        public bool Equals(BundleItems x, BundleItems y)
        {
            if (ReferenceEquals(x, y)) return true;

            if (ReferenceEquals(x, null) || ReferenceEquals(y, null)) return false;

            return x.OrderID == y.OrderID && x.OrderItemId == y.OrderItemId && x.PackageID == y.PackageID && x.ProductID == y.ProductID && x.ProductName == y.ProductName
                && x.Qty == y.Qty && x.QtyReturned == y.QtyReturned && x.BackOrderOty == y.BackOrderOty;
        }

        public int GetHashCode(BundleItems obj)
        {
            if (ReferenceEquals(obj, null)) return 0;

            int hashID = obj.ID.GetHashCode();
            int hashOrderID = obj.OrderID == null ? 0 : obj.OrderID.GetHashCode();
            int hashOrderItemId = obj.OrderItemId == null ? 0 : obj.OrderItemId.GetHashCode();
            int hashPackageID = obj.PackageID == null ? 0 : obj.PackageID.GetHashCode();
            int hashProductID = obj.ProductID == null ? 0 : obj.ProductID.GetHashCode();
            int hashProductName = obj.ProductName == null ? 0 : obj.ProductName.GetHashCode();
            int hashQty = obj.Qty == null ? 0 : obj.Qty.GetHashCode();
            int hashQtyReturned = obj.QtyReturned == null ? 0 : obj.QtyReturned.GetHashCode();
            int hashBackOrderOty = obj.BackOrderOty == null ? 0 : obj.BackOrderOty.GetHashCode();

            return hashID ^ hashOrderID ^ hashOrderItemId ^ hashPackageID ^ hashProductID ^ hashProductName ^ hashQty ^ hashQtyReturned ^ hashBackOrderOty;
        }
    }
}