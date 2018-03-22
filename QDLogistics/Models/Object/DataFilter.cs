﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QDLogistics.Models.Object
{
    public class DataFilter
    {
        /***** Order Filter *****/
        public Nullable<int> WarehouseID { get; set; }
        public Nullable<int> MethodID { get; set; }
        public Nullable<byte> Export { get; set; }
        public Nullable<byte> ExportMethod { get; set; }
        public Nullable<int> StatusCode { get; set; }
        public Nullable<byte> ProccessStatus { get; set; }

        public string OrderID { get; set; }
        public string ItemName { get; set; }
        public string Country { get; set; }
        public string CountryCode { get; set; }
        public Nullable<int> CurrencyCode { get; set; }
        public Nullable<int> Source { get; set; }
        public string UserID { get; set; }
        public string Tracking { get; set; }
        public string Comment { get; set; }
        public decimal DeclaredTotal { get; set; }
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public Nullable<int> PaymentStatus { get; set; }
        public DateTime PaymentDate { get; set; }
        public DateTime DispatchDate { get; set; }
        public string TagNo { get; set; }

        public Nullable<int> ShippingStatus { get; set; }
        public string SourceID { get; set; }
        public DateTime PickUpDateFrom { get; set; }
        public DateTime PickUpDateTo { get; set; }

        /***** Sku Filter *****/
        public Nullable<bool> Battery { get; set; }
        // public Nullable<byte> Export { get; set; }
        // public Nullable<byte> ExportMethod { get; set; }
        public string Sku { get; set; }
        public string ProductName { get; set; }
        public string PurchaseInvoice { get; set; }

        /***** Task Filter *****/
        public Nullable<int> TaskID { get; set; }
        public Nullable<byte> TaskStatus { get; set; }
        public string TaskName { get; set; }
        public Nullable<int> AdminID { get; set; }
        //public DateTime DateFrom { get; set; }
        //public DateTime DateTo { get; set; }
    }
}