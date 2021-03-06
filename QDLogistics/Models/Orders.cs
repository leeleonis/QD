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
    
    public partial class Orders
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Orders()
        {
            this.Items = new HashSet<Items>();
            this.Payments = new HashSet<Payments>();
            this.Packages = new HashSet<Packages>();
        }
    
        public int OrderID { get; set; }
        public Nullable<int> ParentOrderID { get; set; }
        public Nullable<int> ClientID { get; set; }
        public Nullable<int> CompanyID { get; set; }
        public Nullable<int> UserID { get; set; }
        public string UserName { get; set; }
        public Nullable<int> SiteCode { get; set; }
        public Nullable<System.DateTime> TimeOfOrder { get; set; }
        public Nullable<decimal> SubTotal { get; set; }
        public Nullable<decimal> ShippingTotal { get; set; }
        public Nullable<decimal> InsuranceTotal { get; set; }
        public Nullable<decimal> OrderDiscountsTotal { get; set; }
        public Nullable<decimal> GrandTotal { get; set; }
        public Nullable<int> StatusCode { get; set; }
        public Nullable<int> PaymentStatus { get; set; }
        public Nullable<System.DateTime> PaymentDate { get; set; }
        public Nullable<int> ShippingStatus { get; set; }
        public Nullable<System.DateTime> ShipDate { get; set; }
        public Nullable<decimal> FinalShippingFee { get; set; }
        public Nullable<int> ShippingAddress { get; set; }
        public Nullable<int> OrderCurrencyCode { get; set; }
        public Nullable<int> OrderSource { get; set; }
        public string OrderSourceOrderId { get; set; }
        public Nullable<decimal> OrderSourceOrderTotal { get; set; }
        public string eBayUserID { get; set; }
        public string eBaySalesRecordNumber { get; set; }
        public string ShippingServiceSelected { get; set; }
        public Nullable<bool> RushOrder { get; set; }
        public Nullable<bool> InvoicePrinted { get; set; }
        public Nullable<System.DateTime> InvoicePrintedDate { get; set; }
        public string ShippingCarrier { get; set; }
        public string ShippingCountry { get; set; }
        public string PackageType { get; set; }
        public Nullable<int> StationID { get; set; }
        public Nullable<int> CustomerServiceStatus { get; set; }
        public Nullable<decimal> TaxRate { get; set; }
        public Nullable<decimal> TaxTotal { get; set; }
        public string GoogleOrderNumber { get; set; }
        public Nullable<bool> IsInDispute { get; set; }
        public Nullable<System.DateTime> DisputeStartedOn { get; set; }
        public Nullable<decimal> PaypalFeeTotal { get; set; }
        public Nullable<decimal> PostingFeeTotal { get; set; }
        public Nullable<decimal> FinalValueTotal { get; set; }
        public Nullable<int> OrderItemCount { get; set; }
        public Nullable<int> OrderQtyTotal { get; set; }
        public Nullable<int> ShippingWeightTotalOz { get; set; }
        public Nullable<bool> IsConfirmed { get; set; }
        public Nullable<int> ConfirmBy { get; set; }
        public Nullable<System.DateTime> ConfirmOn { get; set; }
        public Nullable<int> MarkettingSourceID { get; set; }
        public Nullable<int> ShippedBy { get; set; }
        public string Instructions { get; set; }
        public Nullable<System.DateTime> SyncOn { get; set; }
    
        public virtual Addresses Addresses { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Items> Items { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Payments> Payments { get; set; }
        public virtual Companies Companies { get; set; }
        public virtual Services Services { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Packages> Packages { get; set; }
    }
}
