//------------------------------------------------------------------------------
// <auto-generated>
//     這個程式碼是由範本產生。
//
//     對這個檔案進行手動變更可能導致您的應用程式產生未預期的行為。
//     如果重新產生程式碼，將會覆寫對這個檔案的手動變更。
// </auto-generated>
//------------------------------------------------------------------------------

namespace QDLogistics.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class ShippingMethod
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public ShippingMethod()
        {
            this.Services = new HashSet<Services>();
            this.Packages = new HashSet<Packages>();
            this.FirstMilePackages = new HashSet<Packages>();
        }
    
        public bool IsEnable { get; set; }
        public bool IsDirectLine { get; set; }
        public int ID { get; set; }
        public string Name { get; set; }
        public Nullable<int> CarrierID { get; set; }
        public Nullable<int> MethodType { get; set; }
        public bool InBox { get; set; }
        public Nullable<int> BoxType { get; set; }
        public int DirectLine { get; set; }
        public string PrinterName { get; set; }
        public Nullable<bool> IsExport { get; set; }
        public Nullable<bool> IsBattery { get; set; }
        public string CountryData { get; set; }
        public Nullable<System.DateTime> SyncOn { get; set; }
        public string ContactName { get; set; }
        public string ConpanyName { get; set; }
        public string PhoneNumber { get; set; }
        public string CountryName { get; set; }
        public string CountryCode { get; set; }
        public string City { get; set; }
        public string StreetLine1 { get; set; }
        public string StreetLine2 { get; set; }
        public string StateName { get; set; }
        public string PostalCode { get; set; }
    
        public virtual Carriers Carriers { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Services> Services { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Packages> Packages { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Packages> FirstMilePackages { get; set; }
    }
}
