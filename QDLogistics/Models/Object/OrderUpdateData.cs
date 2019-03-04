using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QDLogistics.Models.Object
{
    public class OrderUpdateData
    {
        public int OrderID { get; set; }
        public int PackageID { get; set; }
        public Dictionary<string, Items> Items { get; set; }
        public Nullable<decimal> DeclaredTotal { get; set; }
        public Nullable<decimal> DLDeclaredTotal { get; set; }
        public Nullable<int> OrderCurrencyCode { get; set; }
        public Nullable<int> DLCurrency { get; set; }
        public Nullable<int> ShipWarehouse { get; set; }
        public Nullable<int> MethodID { get; set; }
        public Nullable<int> FirstMile { get; set; }
        public Nullable<byte> Export { get; set; }
        public Nullable<byte> ExportMethod { get; set; }
        public Nullable<int> StatusCode { get; set; }
        public Nullable<bool> RushOrder { get; set; }
        public Nullable<bool> UploadTracking { get; set; }
        public string Comment { get; set; }

        public Dictionary<string, string[]> Serials { get; set; }
        public string SupplierComment { get; set; }
        public string TrackingNumber { get; set; }
        public string POInvoice { get; set; }
    }
}