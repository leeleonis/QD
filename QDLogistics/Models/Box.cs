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
    
    public partial class Box
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Box()
        {
            this.BoxNo = 1;
            this.CurrentNo = 1;
            this.DirectLineLabel = new HashSet<DirectLineLabel>();
            this.Packages = new HashSet<Packages>();
        }
    
        public bool IsEnable { get; set; }
        public bool IsReserved { get; set; }
        public string BoxID { get; set; }
        public string MainBox { get; set; }
        public string SupplierBoxID { get; set; }
        public string WITID { get; set; }
        public int DirectLine { get; set; }
        public int FirstMileMethod { get; set; }
        public int WarehouseFrom { get; set; }
        public int WarehouseTo { get; set; }
        public byte BoxType { get; set; }
        public byte ShippingStatus { get; set; }
        public int BoxNo { get; set; }
        public int CurrentNo { get; set; }
        public string TrackingNumber { get; set; }
        public Nullable<System.DateTime> PickUpDate { get; set; }
        public string DeliveryNote { get; set; }
        public Nullable<System.DateTime> DeliveryDate { get; set; }
        public string Note { get; set; }
        public System.DateTime Create_at { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<DirectLineLabel> DirectLineLabel { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Packages> Packages { get; set; }
    }
}
