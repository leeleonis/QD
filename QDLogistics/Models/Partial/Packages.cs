using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace QDLogistics.Models
{
    [MetadataType(typeof(Packages))]

    public partial class Packages : IEquatable<Packages>
    {
        public bool Equals(Packages other)
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

    public class PackageComparer : IEqualityComparer<Packages>
    {
        public bool Equals(Packages x, Packages y)
        {
            if (ReferenceEquals(x, y)) return true;

            if (ReferenceEquals(x, null) || ReferenceEquals(y, null)) return false;

            return x.OrderItemID == y.OrderItemID && x.BundleItemID == y.BundleItemID && x.ShipDate == y.ShipDate && x.ShippingMethodName == y.ShippingMethodName
                 && x.ShippingServiceCode == y.ShippingServiceCode && x.TrackingNumber == y.TrackingNumber;
        }

        public int GetHashCode(Packages obj)
        {
            if (ReferenceEquals(obj, null)) return 0;

            int hashID = obj.ID.GetHashCode();
            int hashOrderItemID = obj.OrderItemID == null ? 0 : obj.OrderItemID.GetHashCode();
            int hashBundleItemID = obj.BundleItemID == null ? 0 : obj.BundleItemID.GetHashCode();
            int hashShipDate = obj.ShipDate == null ? 0 : obj.ShipDate.GetHashCode();
            int hashShippingMethodName = obj.ShippingMethodName == null ? 0 : obj.ShippingMethodName.GetHashCode();
            int hashShippingServiceCode = obj.ShippingServiceCode == null ? 0 : obj.ShippingServiceCode.GetHashCode();
            int hashTrackingNumber = obj.TrackingNumber == null ? 0 : obj.TrackingNumber.GetHashCode();

            return hashID ^ hashOrderItemID ^ hashBundleItemID ^ hashShipDate ^ hashShippingMethodName ^ hashShippingServiceCode ^ hashTrackingNumber;
        }
    }
}