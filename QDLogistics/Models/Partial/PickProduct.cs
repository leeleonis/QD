using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace QDLogistics.Models
{
    [MetadataType(typeof(PickProduct))]
    public partial class PickProduct
    {
        public string Country { get; set; }

        public PickProduct SetCountry(string coutry)
        {
            this.Country = coutry;

            return this;
        }
    }
}