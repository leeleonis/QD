using DataSync.OrderCreationService;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataSync
{
    public static class DataProcess
    {
        public static int InsertAddress(SqlConnection conn)
        {
            using (SqlCommand AddressCmd = new SqlCommand("INSERT INTO Addresses(IsEnable) VALUES(@IsEnable); SELECT SCOPE_IDENTITY();", conn))
            {
                AddressCmd.Parameters.AddWithValue("@IsEnable", true);

                if (conn.State == ConnectionState.Closed) conn.Open();
                int modified = Convert.ToInt32(AddressCmd.ExecuteScalar());
                
                AddressCmd.Dispose();
                return modified;
            }
        }

        public static Addresses SetAddressData(Addresses address, Address addressDetail)
        {
            address.FirstName = addressDetail.FirstName.Trim();
            address.MiddleInitial = addressDetail.MiddleInitial.Trim();
            address.LastName = addressDetail.LastName.Trim();
            address.CompanyName = addressDetail.CompanyName.Trim();
            address.HomePhone = addressDetail.HomePhone.Trim();
            address.PhoneNumber = addressDetail.PhoneNumber.Trim();
            address.FaxNumber = addressDetail.FaxNumber.Trim();
            address.EmailAddress = addressDetail.EmailAddress.Trim();
            address.CountryName = addressDetail.CountryName.Trim();
            address.CountryCode = addressDetail.CountryCode.Trim();
            address.StateCode = addressDetail.StateCode.Trim();
            address.StateName = addressDetail.StateName.Trim();
            address.City = addressDetail.City.Trim();
            address.PostalCode = addressDetail.PostalCode.Trim();
            address.StreetLine1 = addressDetail.StreetLine1.Trim();
            address.StreetLine2 = addressDetail.StreetLine2.Trim();
            address.AddressSource = (int)addressDetail.AddressSource;
            address.AddressState = (int)addressDetail.AddressStatus;
            address.Notes = addressDetail.Notes.Trim();

            if (String.IsNullOrEmpty(address.CountryName) || address.CountryCode.Length > 0)
            {
                var country = MyHelp.GetCountries().Where(c => c.Name == address.CountryCode);
                if (country.Any())
                {
                    address.CountryName = country.First().Name;
                    address.CountryCode = country.First().ID;
                }
            }

            return address;
        }

        public static Orders SetOrderData(Orders order, Order orderDetail)
        {
            order.ParentOrderID = orderDetail.ParentOrderID;
            order.ClientID = orderDetail.ClientId;
            order.CompanyID = orderDetail.CompanyId;
            order.UserID = orderDetail.UserID;
            order.UserName = orderDetail.UserName.Trim();
            order.SiteCode = (int)orderDetail.SiteCode;
            order.TimeOfOrder = orderDetail.TimeOfOrder;
            order.SubTotal = orderDetail.SubTotal;
            order.ShippingTotal = orderDetail.ShippingTotal;
            order.OrderDiscountsTotal = orderDetail.OrderDiscountsTotal;
            order.GrandTotal = orderDetail.GrandTotal;
            order.ShippingStatus = (int)orderDetail.ShippingStatus;
            order.ShipDate = orderDetail.ShipDate;
            order.FinalShippingFee = orderDetail.Packages.Any() ? orderDetail.Packages.First().FinalShippingFee : 0;
            order.OrderCurrencyCode = (int)orderDetail.OrderCurrencyCode;
            order.OrderSource = (int)orderDetail.OrderSource;
            order.OrderSourceOrderId = orderDetail.OrderSourceOrderId.Trim();
            order.OrderSourceOrderTotal = orderDetail.OrderSourceOrderTotal;
            order.eBaySalesRecordNumber = orderDetail.eBaySellingManagerSalesRecordNumber.Trim();
            order.ShippingServiceSelected = orderDetail.ShippingServiceSelected;
            order.RushOrder = orderDetail.RushOrder;
            order.InvoicePrinted = orderDetail.InvoicePrinted;
            order.InvoicePrintedDate = orderDetail.InvoicePrintedDate;
            order.ShippingCarrier = orderDetail.ShippingCarrier.Trim();
            order.ShippingCountry = orderDetail.ShippingCountry.Trim();
            order.PackageType = orderDetail.PackageType.Trim();
            order.StationID = orderDetail.StationID;
            order.CustomerServiceStatus = (int)orderDetail.CustomerServiceStatus;
            order.TaxRate = orderDetail.TaxRate;
            order.TaxTotal = orderDetail.TaxTotal;
            order.GoogleOrderNumber = orderDetail.GoogleOrderNumber.Trim();
            order.IsInDispute = orderDetail.IsInDispute;
            order.DisputeStartedOn = orderDetail.DisputeStartedOn;
            order.PaypalFeeTotal = orderDetail.PaypalFeeTotal;
            order.PostingFeeTotal = orderDetail.PostingFeeTotal;
            order.FinalValueTotal = orderDetail.FinalValueTotal;
            order.OrderItemCount = orderDetail.OrderItemsCount;
            order.OrderQtyTotal = orderDetail.OrderQtyTotal;
            order.ShippingWeightTotalOz = orderDetail.ShippingWeightTotalOz;
            order.IsConfirmed = orderDetail.IsConfirmed;
            order.ConfirmBy = orderDetail.ConfirmedBy;
            order.ConfirmOn = orderDetail.ConfirmedOn;
            order.MarkettingSourceID = orderDetail.MarkettingSourceID;
            order.ShippedBy = orderDetail.ShippedBy;
            order.Instructions = orderDetail.Instructions;

            return order;
        }

        public static Payments SetPaymentData(Payments payment, OrderPayment paymentDetail)
        {
            payment.OrderID = paymentDetail.OrderID;
            payment.CurrentApplicationID = (int)paymentDetail.CurrentApplicationID;
            payment.PaymentMethod = (int)paymentDetail.PaymentMethod;
            payment.PaymentStatus = (int)paymentDetail.PaymentStatus;
            payment.PaymentType = (int)paymentDetail.PaymentType;
            payment.AuditDate = paymentDetail.AuditDate;
            payment.Amount = paymentDetail.Amount;
            payment.TransactionReferenceNumber = paymentDetail.TransactionReferenceNumber;
            payment.Note = paymentDetail.Note;

            return payment;
        }
        
        public static Packages SetPackageData(Packages package, Package packageDetail)
        {
            package.OrderID = packageDetail.OrderID;
            package.OrderItemID = packageDetail.OrderItemID;
            package.BundleItemID = packageDetail.OrderItemBundleItemID;
            package.Qty = packageDetail.Qty;
            package.ShipDate = packageDetail.ShipDate;
            package.ShippingMethodName = packageDetail.ShippingMethodName;
            package.ShippingServiceCode = packageDetail.ShippingServiceCode;
            package.EstimatedDeliveryDate = packageDetail.EstimatedDeliveryDate;
            package.DeliveryDate = packageDetail.DeliveryDate;
            package.DeliveryStatus = (int)packageDetail.DeliveryStatus;
            package.FinalShippingFee = packageDetail.FinalShippingFee;
            package.TrackingNumber = packageDetail.TrackingNumber;
            package.Weight = packageDetail.Weight;
            package.Length = packageDetail.Length;
            package.Width = packageDetail.Width;
            package.Height = packageDetail.Height;

            return package;
        }

        public static Items SetItemData(Items item, OrderItem itemDetail)
        {
            item.OrderID = itemDetail.OrderID;
            item.PackageID = itemDetail.PackageID;
            item.OriginalSKU = itemDetail.OriginalSKU;
            item.ProductID = itemDetail.ProductID;
            item.ProductIDOriginal = itemDetail.ProductIDOriginal;
            item.ProductIDRequest = itemDetail.ProductIDRequested;
            item.Qty = itemDetail.Qty;
            item.QtyReturned = itemDetail.QtyReturned;
            item.QtyShipped = itemDetail.QtyShipped;
            item.DisplayName = itemDetail.DisplayName;
            item.LineTotal = itemDetail.LineTotal;
            item.KitItemCount = itemDetail.KitItemsCount;
            item.eBayItemID = itemDetail.eBayItemID;
            item.BackOrderQty = itemDetail.BackOrderQty;
            item.ShipFromWarehouseID = itemDetail.ShipFromWareHouseID;
            item.ReturnedToWarehouseID = itemDetail.ReturnedToWarehouseID;
            item.Weight = itemDetail.Weight;

            if (item.UnitPrice == null) item.UnitPrice = itemDetail.LineTotal / itemDetail.Qty;

            return item;
        }

        public static BundleItems SetBundleData(BundleItems bundle, OrderBundleItem bundleDetail)
        {
            bundle.OrderID = bundleDetail.OrderID;
            bundle.OrderItemId = bundleDetail.OrderItemId;
            bundle.PackageID = bundleDetail.PackageID;
            bundle.ProductID = bundleDetail.ProductID;
            bundle.ProductName = bundleDetail.ProductName;
            bundle.Qty = bundleDetail.Qty;
            bundle.QtyReturned = bundleDetail.QtyReturned;
            bundle.BackOrderOty = bundleDetail.BackOrderQty;

            return bundle;
        }

        public static SerialNumbers SetSerialNumberData(SerialNumbers serialNumber, OrderService.OrderSerialNumber SerialNumberDetail)
        {
            serialNumber.OrderID = SerialNumberDetail.OrderID;
            serialNumber.ProductID = SerialNumberDetail.ProductID;
            serialNumber.SerialNumber = SerialNumberDetail.SerialNumber;
            serialNumber.OrderItemID = SerialNumberDetail.OrderItemID;
            serialNumber.KitItemID = SerialNumberDetail.KitItemID;

            return serialNumber;
        }

        public static Skus SetSkuData(Skus sku, OrderService.ProductFullInfo SkuDetail)
        {
            sku.ProductName = SkuDetail.ProductName;
            sku.ProductTypeID = SkuDetail.ProductTypeID;
            sku.UPC = SkuDetail.UPC;
            sku.Brand = SkuDetail.ManufacturerID;

            return sku;
        }

        public static void BulkInsert<T>(SqlConnection connection, string tableName, IEnumerable<T> list)
        {
            using (var bulkCopy = new SqlBulkCopy(connection))
            {
                bulkCopy.BatchSize = list.Count();
                bulkCopy.BulkCopyTimeout = 2000;
                bulkCopy.DestinationTableName = tableName;

                var table = new DataTable();
                var props = TypeDescriptor.GetProperties(typeof(T))
                //Dirty hack to make sure we only have system data types
                //i.e. filter out the relationships/collections
                .Cast<PropertyDescriptor>()
                .Where(propertyInfo => propertyInfo.PropertyType.Namespace.Equals("System"))
                .ToArray();
                foreach (var propertyInfo in props)
                {
                    bulkCopy.ColumnMappings.Add(propertyInfo.Name, propertyInfo.Name);
                    table.Columns.Add(propertyInfo.Name, Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType);
                }

                var values = new object[props.Length];
                foreach (var item in list)
                {
                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = props[i].GetValue(item);
                    }

                    table.Rows.Add(values);
                }

                bulkCopy.WriteToServer(table);
                bulkCopy.Close();

                Console.WriteLine("--- " + tableName + " insert successed ---");
            }
        }

        public static void BatchUpdate(SqlConnection conn, DataTable tb, string status)
        {
            string sql = string.Empty;

            switch (status)
            {
                case "OrderData":
                    sql = "UpdateOrderData";
                    break;
                case "AddressData":
                    sql = "UpdateAddressData";
                    break;
                case "PaymentData":
                    sql = "UpdatePaymentData";
                    break;
                case "PackageData":
                    sql = "UpdatePackageData";
                    break;
                case "ItemData":
                    sql = "UpdateItemData";
                    break;
            }

            using (SqlCommand Cmd = new SqlCommand(sql, conn))
            {
                SqlTransaction trans = conn.BeginTransaction();
                Cmd.Transaction = trans;
                Cmd.CommandType = CommandType.StoredProcedure;
                Cmd.Parameters.Add("@mytable", SqlDbType.Structured).Value = tb;

                int result = Cmd.ExecuteNonQuery();
                if (result == 0)
                {
                    trans.Rollback();
                    Console.WriteLine("--- update " + status + " failed ---");
                }
                else
                {
                    trans.Commit();
                    Console.WriteLine("--- update " + status + " successed ---");
                }
            }
        }
    }
}