using System;
using System.Linq;
using QDLogistics.Models;
using Newtonsoft.Json.Linq;
using QDLogistics.Models.Object;
using QDLogistics.OrderService;

namespace QDLogistics.Commons
{
    public static class DataProcess
    {
        public static JObject SetOrderExcelData(string type, OrderJoinData data, JObject obj)
        {
            switch (type)
            {
                case "order":
                    obj.Add("OrderID", data.order.OrderID);
                    obj.Add("OrderItemID", data.package.OrderItemID);
                    obj.Add("KitItemID", data.package.BundleItemID);
                    obj.Add("UserID", data.order.UserID);
                    obj.Add("UserName", data.order.UserName);
                    obj.Add("FirstName", data.order.Addresses.FirstName);
                    obj.Add("LastName", data.order.Addresses.LastName);
                    obj.Add("CompanyName", data.order.Addresses.CompanyName);
                    obj.Add("SiteCode", data.order.SiteCode);
                    obj.Add("TimeOfOrder", MyHelp.DateTimeWithZone(data.order.TimeOfOrder.Value).ToString("MM/dd/yyyy hh:mm:ss tt"));
                    obj.Add("SubTotal", data.order.SubTotal);
                    obj.Add("ShippingTotal", data.order.ShippingTotal);
                    obj.Add("OrderDiscountsTotal", data.order.OrderDiscountsTotal);
                    obj.Add("GrandTotal", data.order.GrandTotal);
                    obj.Add("Status", Enum.GetName(typeof(OrderStatusCode), data.order.StatusCode));
                    obj.Add("PaymentStatus", Enum.GetName(typeof(OrderPaymentStatus), data.order.PaymentStatus));
                    obj.Add("PaymentDate", data.order.Payments.Any() ? MyHelp.DateTimeWithZone(data.order.Payments.First().AuditDate.Value).ToString("MM/dd/yyyy hh:mm:ss tt") : "");
                    obj.Add("PaymentReferenceNumber", data.order.Payments.Any() ? data.order.Payments.First().TransactionReferenceNumber : "");
                    obj.Add("PaymentMethod", data.order.Payments.Any() ? Enum.GetName(typeof(PaymentMethod), data.order.Payments.First().PaymentMethod) : "");
                    obj.Add("ShippingStatus", Enum.GetName(typeof(OrderShippingStatus), data.order.ShippingStatus));
                    obj.Add("ShipDate", data.order.ShippingStatus == 3 && data.package.ShipDate != null ? MyHelp.DateTimeWithZone(data.package.ShipDate.Value).ToString("MM/dd/yyyy hh:mm:ss tt") : "");
                    obj.Add("ShipFirstName", data.order.Addresses.FirstName);
                    obj.Add("ShipLastName", data.order.Addresses.LastName);
                    obj.Add("ShipCompanyName", data.order.Addresses.CompanyName);
                    obj.Add("ShipAddress1", data.order.Addresses.StreetLine1);
                    obj.Add("ShipAddress2", data.order.Addresses.StreetLine2);
                    obj.Add("ShipCity", data.order.Addresses.City);
                    obj.Add("ShipState", data.order.Addresses.StateName);
                    obj.Add("ShipZipCode", data.order.Addresses.PostalCode);
                    obj.Add("ShipCountry", data.order.Addresses.CountryName);
                    obj.Add("OrderSource", data.order.OrderSource);
                    obj.Add("OrderSourceOrderID", data.order.OrderSourceOrderId);
                    obj.Add("eBaySalesRecordNumber", data.order.eBaySalesRecordNumber);
                    obj.Add("ShippingMethodSelected", data.package.ShippingMethodName);
                    obj.Add("IsRushOrder", data.order.RushOrder);
                    obj.Add("InvoicePrinted", data.order.InvoicePrinted);
                    obj.Add("InvoicePrintedDate", data.order.InvoicePrinted.Value ? MyHelp.DateTimeWithZone(data.order.InvoicePrintedDate.Value).ToString("MM/dd/yyyy hh:mm:ss tt") : "");
                    obj.Add("ShippingCarrier", data.package.Carriers != null ? data.package.Carriers.Carrier : data.package.ShippingServiceCode);
                    obj.Add("PackageType", data.order.PackageType);
                    obj.Add("CompanyID", data.order.CompanyID);
                    obj.Add("OrderSourceOrderTotal", data.order.OrderSourceOrderTotal);
                    obj.Add("CustomerServiceStatus", Enum.GetName(typeof(OrderCustomerServiceStatus), data.order.CustomerServiceStatus));
                    obj.Add("TaxRate", data.order.TaxRate);
                    obj.Add("TaxTotal", data.order.TaxTotal);
                    obj.Add("GoogleOrderNumber", data.order.GoogleOrderNumber);
                    obj.Add("IsInDispute", data.order.IsInDispute);
                    obj.Add("DisputeStartedOn", data.order.IsInDispute.Value ? MyHelp.DateTimeWithZone(data.order.DisputeStartedOn.Value).ToString("MM/dd/yyyy hh:mm:ss tt") : "");
                    obj.Add("PaypalFeeTotal", data.order.PaypalFeeTotal);
                    obj.Add("PostingFeeTotal", data.order.PostingFeeTotal);
                    obj.Add("FinalValueTotal", data.order.FinalValueTotal);
                    obj.Add("ShippingWeightTotalOz", data.order.ShippingWeightTotalOz);
                    obj.Add("ProductID", data.item.ProductID);
                    obj.Add("Qty", data.item.Qty);
                    obj.Add("DisplayName", data.item.DisplayName);
                    obj.Add("LineTotal", data.item.LineTotal);
                    obj.Add("eBayItemID", data.item.eBayItemID);
                    obj.Add("BackOrderQty", data.item.BackOrderQty);
                    obj.Add("TrackingNumber", data.package.TrackingNumber);
                    obj.Add("SerialNumber", data.item.SerialNumbers.Any() ? data.item.SerialNumbers.First().SerialNumber : "");
                    obj.Add("UPC", data.item.Skus.UPC);
                    obj.Add("MarkettingSourceID", data.order.MarkettingSourceID);
                    break;
                case "shipped":
                    obj.Add("OrderID", data.order.OrderID);
                    obj.Add("PaymentDate", data.order.Payments.Any() ? MyHelp.DateTimeWithZone(data.order.Payments.First().AuditDate.Value).ToString("MM/dd/yyyy hh:mm:ss tt") : "");
                    obj.Add("ProductID", data.item.ProductID);
                    obj.Add("DisplayName", data.item.DisplayName);
                    obj.Add("Qty", 1);
                    obj.Add("Country", data.order.Addresses.CountryName);
                    obj.Add("Warehouse", data.item.ShipWarehouses.Name);
                    obj.Add("ShippingMethod", data.package.Carriers != null ? data.package.Carriers.Carrier : data.package.ShippingServiceCode);
                    obj.Add("Export", Enum.GetName(typeof(EnumData.Export), data.package.Export != null ? data.package.Export : 0));
                    obj.Add("ExportMethod", Enum.GetName(typeof(EnumData.ExportMethod), data.package.ExportMethod != null ? data.package.ExportMethod : 0));
                    obj.Add("Status", Enum.GetName(typeof(OrderStatusCode), data.order.StatusCode));
                    obj.Add("Comment", data.package.Comment);
                    obj.Add("SerialNumber", data.item.SerialNumber);
                    obj.Add("PickUpDate", "");
                    obj.Add("Tracking", data.package.TrackingNumber);
                    obj.Add("DeliveryStatus", "");
                    obj.Add("DispatchTime", "");
                    obj.Add("TransitTime", "");
                    obj.Add("RedirectWarehouse", data.item.ReturnWarehouses.Name);
                    obj.Add("RMA", "");
                    break;
            }

            return obj;
        }

        public static void UpdateOrder(IRepository<Orders> Orders, Orders order, Order orderDetail, bool complex = true)
        {
            if (complex)
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
                order.MarkettingSourceID = orderDetail.MarkettingSourceID;
                order.ShippedBy = orderDetail.ShippedBy;
                order.Instructions = orderDetail.Instructions;
            }

            order.StatusCode = (int)orderDetail.StatusCode;
            order.PaymentStatus = (int)orderDetail.PaymentStatus;
            order.ShippingCarrier = orderDetail.ShippingCarrier.Trim();
            order.IsConfirmed = orderDetail.IsConfirmed;
            order.ConfirmBy = orderDetail.ConfirmedBy;
            order.ConfirmOn = orderDetail.ConfirmedOn;

            Orders.Update(order);
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
            order.PaymentStatus = (int)orderDetail.PaymentStatus;
            order.ShippingStatus = (int)orderDetail.ShippingStatus;
            order.ShipDate = orderDetail.ShipDate;
            order.FinalShippingFee = orderDetail.Packages.Any() ? orderDetail.Packages.First().FinalShippingFee : 0;
            order.OrderSource = (int)orderDetail.OrderSource;
            order.OrderSourceOrderId = orderDetail.OrderSourceOrderId.Trim();
            order.OrderSourceOrderTotal = orderDetail.OrderSourceOrderTotal;
            order.eBaySalesRecordNumber = orderDetail.eBaySellingManagerSalesRecordNumber.Trim();
            order.ShippingServiceSelected = orderDetail.ShippingServiceSelected;
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

            if (!order.Packages.Any(p => p.ProcessStatus == (int)EnumData.ProcessStatus.待出貨))
            {
                if (order.Packages.Any())
                {
                    foreach (Packages package in order.Packages)
                    {
                        switch (orderDetail.StatusCode)
                        {
                            case OrderStatusCode.Completed:
                                package.ProcessStatus = (int)EnumData.ProcessStatus.已出貨;
                                break;
                            case OrderStatusCode.Canceled:
                            case OrderStatusCode.OnHold:
                            case OrderStatusCode.Void:
                                package.ProcessStatus = (int)EnumData.ProcessStatus.訂單管理;
                                break;
                        }
                    }
                }

                order.StatusCode = (int)orderDetail.StatusCode;
                order.OrderCurrencyCode = (int)orderDetail.OrderCurrencyCode;
                order.RushOrder = orderDetail.RushOrder;
            }

            return order;
        }

        public static Packages SetPackageData(Packages package, Package packageDetail)
        {
            package.OrderID = packageDetail.OrderID;
            package.OrderItemID = packageDetail.OrderItemID;
            package.BundleItemID = packageDetail.OrderItemBundleItemID;
            package.Qty = packageDetail.Qty;
            package.ShipDate = packageDetail.ShipDate;
            package.ShippingMethodName = packageDetail.ShippingMethodName.Trim();
            package.EstimatedDeliveryDate = packageDetail.EstimatedDeliveryDate;
            package.DeliveryDate = packageDetail.DeliveryDate;
            package.DeliveryStatus = (int)packageDetail.DeliveryStatus;
            package.FinalShippingFee = packageDetail.FinalShippingFee;
            package.Weight = packageDetail.Weight;
            package.Length = packageDetail.Length;
            package.Width = packageDetail.Width;
            package.Height = packageDetail.Height;

            if (package.ProcessStatus != (int)EnumData.ProcessStatus.待出貨)
            {
                package.ShippingServiceCode = packageDetail.ShippingServiceCode.Trim();

                if (package.UploadTracking)
                {
                    package.TrackingNumber = packageDetail.TrackingNumber.Trim();
                }
            }

            return package;
        }

        public static Items SetItemData(Items item, OrderItem itemDetail)
        {
            item.OrderID = itemDetail.OrderID;
            item.PackageID = itemDetail.PackageID;
            item.SKU = itemDetail.ProductID.Trim();
            item.OriginalSKU = itemDetail.OriginalSKU.Trim();
            item.ProductID = itemDetail.ProductID.Trim();
            item.ProductIDOriginal = itemDetail.ProductIDOriginal.Trim();
            item.ProductIDRequest = itemDetail.ProductIDRequested.Trim();
            item.Qty = itemDetail.Qty;
            item.QtyReturned = itemDetail.QtyReturned;
            item.QtyShipped = itemDetail.QtyShipped;
            item.DisplayName = itemDetail.DisplayName.Trim();
            item.LineTotal = itemDetail.LineTotal;
            item.KitItemCount = itemDetail.KitItemsCount;
            item.eBayItemID = itemDetail.eBayItemID.Trim();
            item.eBayTransactionId = itemDetail.eBayTransactionId.Trim();
            item.SalesRecordNumber = itemDetail.SalesRecordNumber.Trim();
            item.BackOrderQty = itemDetail.BackOrderQty;
            item.UnitPrice = itemDetail.PricePerCase;
            item.ReturnedToWarehouseID = itemDetail.ReturnedToWarehouseID;
            item.Weight = itemDetail.Weight;

            if (item.Packages == null || item.Packages.ProcessStatus != (int)EnumData.ProcessStatus.待出貨)
            {
                item.ShipFromWarehouseID = itemDetail.ShipFromWareHouseID;
            }

            return item;
        }

        public static BundleItems SetBundleItemData(BundleItems bundleItem, OrderBundleItem bundleItemDetail)
        {
            bundleItem.OrderID = bundleItemDetail.OrderID;
            bundleItem.OrderItemId = bundleItemDetail.OrderItemId;
            bundleItem.PackageID = bundleItemDetail.PackageID;
            bundleItem.ProductID = bundleItemDetail.ProductID.Trim();
            bundleItem.ProductName = bundleItemDetail.ProductName.Trim();
            bundleItem.Qty = bundleItemDetail.Qty;
            bundleItem.QtyReturned = bundleItemDetail.QtyReturned;
            bundleItem.BackOrderOty = bundleItemDetail.BackOrderQty;

            return bundleItem;
        }

        public static SerialNumbers SetSerialNumberData(SerialNumbers serialNumber, OrderSerialNumber SerialNumberDetail)
        {
            serialNumber.OrderID = SerialNumberDetail.OrderID;
            serialNumber.ProductID = SerialNumberDetail.ProductID.Trim();
            serialNumber.SerialNumber = SerialNumberDetail.SerialNumber.Trim();
            serialNumber.OrderItemID = SerialNumberDetail.OrderItemID;
            serialNumber.KitItemID = SerialNumberDetail.KitItemID;

            return serialNumber;
        }

        public static Addresses SetAddressData(Addresses address, Address addressDetail, Address billingAddress)
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
            address.AddressStatus = (int)addressDetail.AddressStatus;
            address.Notes = addressDetail.Notes.Trim();

            if (string.IsNullOrEmpty(addressDetail.PhoneNumber))
            {
                address.PhoneNumber = billingAddress.PhoneNumber.Trim();
            }

            if (string.IsNullOrEmpty(address.CountryName) || address.CountryCode.Equals(address.CountryName))
            {
                var countryList = MyHelp.GetCountries();
                if(countryList.Any(c => c.Name.Equals(address.CountryCode)))
                {
                    address.CountryName = address.CountryCode;
                    address.CountryCode = countryList.First(c => c.Name.Equals(address.CountryName)).TwoCode;
                }
            }

            if (address.CountryCode.Length > 2)
            {
                var countryList = MyHelp.GetCountries();
                if (countryList.Any(c => c.Name.Equals(address.CountryName)))
                {
                    address.CountryCode = countryList.First(c => c.Name.Equals(address.CountryName)).TwoCode;
                }
            }

            return address;
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
            payment.TransactionReferenceNumber = paymentDetail.TransactionReferenceNumber.Trim();
            payment.Note = paymentDetail.Note.Trim();

            return payment;
        }

        public static PurchaseItemReceive SetPurchaseItemData(PurchaseItemReceive purchaseItem, PurchaseItemReceive_All_Item purchaseItemDetail, bool isRequire)
        {
            purchaseItem.IsRequireSerialScan = isRequire;
            purchaseItem.OrderID = purchaseItemDetail.OrderID;
            purchaseItem.OrderItemID = purchaseItemDetail.OrderItemID;
            purchaseItem.ProductID = purchaseItemDetail.ProductID;
            purchaseItem.SerialNumber = purchaseItemDetail.SerialNumber.Trim();
            purchaseItem.PurchaseID = purchaseItemDetail.PurchaseID;
            purchaseItem.PurchaseReceiveID = purchaseItemDetail.PurchaseReceiveID;
            purchaseItem.RMAId = purchaseItemDetail.RMAId;
            purchaseItem.CreditMemoID = purchaseItemDetail.CreditMemoID;
            purchaseItem.CreditMemoReason = purchaseItemDetail.CreditMemoReason.Trim();
            purchaseItem.WarehouseID = purchaseItemDetail.WarehouseID;
            purchaseItem.WarehouseName = purchaseItemDetail.WarehouseName.Trim();
            purchaseItem.LocationBinID = purchaseItemDetail.LocationBinID;
            purchaseItem.BinName = purchaseItemDetail.BinName.Trim();

            return purchaseItem;
        }

        public static void setPickProductData(PickProduct pick, Items item)
        {
            pick.OrderID = item.OrderID;
            pick.PackageID = item.PackageID;
            pick.ItemID = item.ID;
            pick.ProductID = item.ProductID;
            pick.ProductName = item.Skus.ProductName;
            pick.UPC = item.Skus.UPC;
            pick.WarehouseID = item.ShipFromWarehouseID;
            pick.Qty = item.Qty;
        }
    }
}