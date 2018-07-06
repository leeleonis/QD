using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace QDLogistics.Models
{
    [MetadataType(typeof(Warehouses))]
    public partial class Warehouses : IEquatable<Warehouses>
    {
        public bool Equals(Warehouses other)
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

    public class WarehouseComparer : IEqualityComparer<Warehouses>
    {
        public bool Equals(Warehouses x, Warehouses y)
        {
            if (ReferenceEquals(x, y)) return true;

            if (ReferenceEquals(x, null) || ReferenceEquals(y, null)) return false;

            return x.ID.Equals(y.ID) && x.Name.Equals(y.Name) && x.ClientID.Equals(y.ClientID);
        }

        public int GetHashCode(Warehouses obj)
        {
            if (ReferenceEquals(obj, null)) return 0;

            int hashID = obj.ID.GetHashCode();
            int hasName = obj.Name == null ? 0 : obj.Name.GetHashCode();
            int hashClientID = obj.ClientID == null ? 0 : obj.ClientID.GetHashCode();

            return hashID ^ hasName ^ hashClientID;
        }
    }
}