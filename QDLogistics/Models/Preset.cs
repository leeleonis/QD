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
    
    public partial class Preset
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Preset()
        {
            this.Priority = 1;
        }
    
        public bool IsEnable { get; set; }
        public bool IsVisible { get; set; }
        public bool IsDispatch { get; set; }
        public int Id { get; set; }
        public byte Type { get; set; }
        public int Priority { get; set; }
        public decimal Value { get; set; }
        public byte ValueType { get; set; }
        public int WarehouseID { get; set; }
        public int MethodID { get; set; }
        public decimal Total { get; set; }
        public byte TotalType { get; set; }
        public string Country { get; set; }
        public string State { get; set; }
        public int ZipCodeFrom { get; set; }
        public int ZipCodeTo { get; set; }
        public int CompanyID { get; set; }
        public int SourceID { get; set; }
        public int Amount { get; set; }
        public byte AmountType { get; set; }
        public string ShippingMethod { get; set; }
        public string Sku { get; set; }
        public int Weight { get; set; }
        public byte WeightType { get; set; }
    }
}
