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

        public bool InBox { get; set; }

        public PickProduct SetInBox(bool inBox)
        {
            this.InBox = inBox;

            return this;
        }

        public string TagNo { get; set; }

        public PickProduct SetTagNo(string tagNo)
        {
            this.TagNo = tagNo;

            return this;
        }

        public string Note { get; set; }

        public PickProduct SetNote(string note)
        {
            this.Note = note;

            return this;
        }

        public bool IsBattery { get; set; }

        public PickProduct SetBattery(bool isBattery)
        {
            this.IsBattery = isBattery;

            return this;
        }
    }
}