using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace QDLogistics.Models
{
    [MetadataType(typeof(Orders))]

    public partial class Orders : IEquatable<Orders>
    {
        public bool Equals(Orders other)
        {
            if (ReferenceEquals(other, null)) return false;

            if (ReferenceEquals(this, other)) return true;

            return OrderID.Equals(other.OrderID);
        }

        public override int GetHashCode()
        {
            int hashOrderID = OrderID.GetHashCode();

            return hashOrderID;
        }
    }

    public class OrdersComparer : IEqualityComparer<Orders>
    {
        public bool Equals(Orders x, Orders y)
        {
            if (ReferenceEquals(x, y)) return true;

            if (ReferenceEquals(x, null) || ReferenceEquals(y, null)) return false;

            return x.OrderID == y.OrderID && x.StatusCode == y.StatusCode && x.PaymentStatus == y.PaymentStatus && x.ShippingStatus == y.ShippingStatus;
        }

        public int GetHashCode(Orders obj)
        {
            if (ReferenceEquals(obj, null)) return 0;

            int hashOrderID = obj.OrderID.GetHashCode();
            int hashStatusCode = obj.StatusCode == null ? 0 : obj.StatusCode.GetHashCode();
            int hashPaymentStatus = obj.PaymentStatus == null ? 0 : obj.PaymentStatus.GetHashCode();
            int hashShippingStatus = obj.ShippingStatus == null ? 0 : obj.ShippingStatus.GetHashCode();

            return hashOrderID ^ hashStatusCode ^ hashPaymentStatus ^ hashShippingStatus;
        }
    }
}