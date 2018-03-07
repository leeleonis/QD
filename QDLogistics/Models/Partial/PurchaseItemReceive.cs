using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace QDLogistics.Models
{
    [MetadataType(typeof(PurchaseItemReceive))]
    public partial class PurchaseItemReceive : IEquatable<PurchaseItemReceive>
    {
        public bool Equals(PurchaseItemReceive other)
        {
            if (ReferenceEquals(other, null)) return false;

            if (ReferenceEquals(this, other)) return true;

            return ProductID.Equals(other.ProductID) && SerialNumber.Equals(other.SerialNumber);
        }

        public override int GetHashCode()
        {
            int hashProductID = ProductID.GetHashCode();
            int hashSerialNumber = SerialNumber.GetHashCode();

            return hashProductID ^ hashSerialNumber;
        }
    }
}