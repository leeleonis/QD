﻿//------------------------------------------------------------------------------
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
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class QDLogisticsEntities : DbContext
    {
        public QDLogisticsEntities()
            : base("name=QDLogisticsEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<Addresses> Addresses { get; set; }
        public virtual DbSet<BundleItems> BundleItems { get; set; }
        public virtual DbSet<CarrierAPI> CarrierAPI { get; set; }
        public virtual DbSet<Items> Items { get; set; }
        public virtual DbSet<Manufacturers> Manufacturers { get; set; }
        public virtual DbSet<Orders> Orders { get; set; }
        public virtual DbSet<Packages> Packages { get; set; }
        public virtual DbSet<Payments> Payments { get; set; }
        public virtual DbSet<Skus> Skus { get; set; }
        public virtual DbSet<Warehouses> Warehouses { get; set; }
        public virtual DbSet<AdminGroups> AdminGroups { get; set; }
        public virtual DbSet<AdminUsers> AdminUsers { get; set; }
        public virtual DbSet<Menu> Menu { get; set; }
        public virtual DbSet<PickProduct> PickProduct { get; set; }
        public virtual DbSet<ProductType> ProductType { get; set; }
        public virtual DbSet<TaskLog> TaskLog { get; set; }
        public virtual DbSet<ActionLog> ActionLog { get; set; }
        public virtual DbSet<Companies> Companies { get; set; }
        public virtual DbSet<SerialNumbers> SerialNumbers { get; set; }
        public virtual DbSet<PurchaseItemReceive> PurchaseItemReceive { get; set; }
        public virtual DbSet<TaskScheduler> TaskScheduler { get; set; }
        public virtual DbSet<Services> Services { get; set; }
        public virtual DbSet<Preset> Preset { get; set; }
        public virtual DbSet<Carriers> Carriers { get; set; }
        public virtual DbSet<ShippingMethod> ShippingMethod { get; set; }
        public virtual DbSet<Box> Box { get; set; }
        public virtual DbSet<DirectLineLabel> DirectLineLabel { get; set; }
        public virtual DbSet<DirectLine> DirectLine { get; set; }
    }
}
