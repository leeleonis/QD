using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace QDLogistics.Models
{
    [MetadataType(typeof(SerialNumbers))]

    public partial class SerialNumbers : IEquatable<SerialNumbers>
    {
        public bool Equals(SerialNumbers other)
        {
            if (ReferenceEquals(other, null)) return false;

            if (ReferenceEquals(this, other)) return true;

            return SerialNumber.Equals(other.SerialNumber) && OrderItemID.Equals(other.OrderItemID);
        }

        public override int GetHashCode()
        {
            int hashSerialNumber = SerialNumber.GetHashCode();
            int hashOrderItemID = OrderItemID.GetHashCode();

            return hashSerialNumber ^ hashOrderItemID;
        }
    }

    public class SerialNumberComparer : IEqualityComparer<SerialNumbers>
    {
        public bool Equals(SerialNumbers x, SerialNumbers y)
        {
            if (ReferenceEquals(x, y)) return true;

            if (ReferenceEquals(x, null) || ReferenceEquals(y, null)) return false;

            return x.OrderID == y.OrderID && x.ProductID == y.ProductID && x.SerialNumber == y.SerialNumber && x.OrderItemID == y.OrderItemID && x.KitItemID == y.KitItemID;
        }

        public int GetHashCode(SerialNumbers obj)
        {
            if (ReferenceEquals(obj, null)) return 0;

            int hashOrderID = obj.OrderID == null ? 0 : obj.OrderID.GetHashCode();
            int hashProductID = obj.ProductID == null ? 0 : obj.ProductID.GetHashCode();
            int hashSerialNumber = obj.SerialNumber.GetHashCode();
            int hashOrderItemID = obj.OrderItemID.GetHashCode();
            int hashKitItemID = obj.KitItemID == null ? 0 : obj.KitItemID.GetHashCode();

            return hashOrderID ^ hashProductID ^ hashSerialNumber ^ hashOrderItemID ^ hashKitItemID;
        }
    }
}