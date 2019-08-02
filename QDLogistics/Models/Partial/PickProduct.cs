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

        public decimal DeclaredValue { get; set; }
        public PickProduct SetDeclaredValue(decimal value)
        {
            this.DeclaredValue = value;
            return this;
        }

        public bool InBox { get; set; }
        public PickProduct SetInBox(bool inBox)
        {
            this.InBox = inBox;
            return this;
        }

        public bool IsBattery { get; set; }
        public PickProduct SetBattery(bool isBattery)
        {
            this.IsBattery = isBattery;
            return this;
        }

        public string Note { get; set; }
        public PickProduct SetNote(string note)
        {
            this.Note = note;
            return this;
        }

        public string TagNo { get; set; }
        public PickProduct SetTagNo(string tagNo)
        {
            this.TagNo = tagNo;
            return this;
        }

        public int Weight { get; set; }
        public PickProduct SetWeight(int weight)
        {
            this.Weight = weight;
            return this;
        }

        public string Tracking { get; set; }
        public PickProduct SetTracking(string tracking)
        {
            this.Tracking = tracking;
            return this;
        }
    }
}