using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace QDLogistics.Models
{
    [MetadataType(typeof(Payments))]
    public partial class Payments : IEquatable<Payments>
    {
        public bool Equals(Payments other)
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

    public class PaymentComparer : IEqualityComparer<Payments>
    {
        public bool Equals(Payments x, Payments y)
        {
            if (ReferenceEquals(x, y)) return true;

            if (ReferenceEquals(x, null) || ReferenceEquals(y, null)) return false;

            return x.PaymentStatus == y.PaymentStatus && x.AuditDate == y.AuditDate;
        }

        public int GetHashCode(Payments obj)
        {
            if (ReferenceEquals(obj, null)) return 0;

            int hashID = obj.ID.GetHashCode();
            int hashPaymentStatus = obj.PaymentStatus == null ? 0 : obj.PaymentStatus.GetHashCode();
            int hashAuditDate = obj.AuditDate == null ? 0 : obj.AuditDate.GetHashCode();

            return hashID ^ hashPaymentStatus ^ hashAuditDate;
        }
    }
}