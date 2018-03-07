using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace QDLogistics.Models
{
    [MetadataType(typeof(CompanyMetaData))]
    public partial class Companies : IEquatable<Companies>
    {
        public class CompanyMetaData
        {
            public int ID { get; set; }
            public string CompanyName { get; set; }
            public Nullable<int> TimeZone { get; set; }
        }

        public bool Equals(Companies other)
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

    public class CompanyComparer : IEqualityComparer<Companies>
    {
        public bool Equals(Companies x, Companies y)
        {
            if (ReferenceEquals(x, y)) return true;

            if (ReferenceEquals(x, null) || ReferenceEquals(y, null)) return false;

            return x.ID == y.ID && x.CompanyName == y.CompanyName;
        }

        public int GetHashCode(Companies obj)
        {
            if (ReferenceEquals(obj, null)) return 0;

            int hashID = obj.ID.GetHashCode();
            int hashCompanyName = obj.CompanyName == null ? 0 : obj.CompanyName.GetHashCode();

            return hashID ^ hashCompanyName;
        }
    }
}