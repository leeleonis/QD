using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DirectLineApi.ShippingEasy
{
    public class Order
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("external_order_identifier")]
        public string ExternalOrderIdentifier { get; set; }

        [JsonProperty("ext_order_reference_id")]
        public string ExtOrderReferenceId { get; set; }

        [JsonProperty("owner_id")]
        public int? OwnerId { get; set; }

        [JsonProperty("ordered_at")]
        public DateTimeOffset? OrderedAt { get; set; }

        [JsonProperty("order_status")]
        public string OrderStatus { get; set; }

        [JsonProperty("parent_order_id")]
        public int? ParentOrderId { get; set; }

        [JsonProperty("source_order_ids")]
        public int? SourceOrderIds { get; set; }

        [JsonProperty("total_including_tax")]
        public decimal? TotalIncludingTax { get; set; }

        [JsonProperty("total_excluding_tax")]
        public decimal? TotalExcludingTax { get; set; }

        [JsonProperty("discount_amount")]
        public decimal? DiscountAmount { get; set; }

        [JsonProperty("coupon_discount")]
        public decimal? CouponDiscount { get; set; }

        [JsonProperty("subtotal_including_tax")]
        public decimal? SubtotalIncludingTax { get; set; }

        [JsonProperty("subtotal_excluding_tax")]
        public decimal? SubtotalExcludingTax  { get; set; }

        [JsonProperty("subtotal_tax")]
        public decimal? SubtotalTax { get; set; }

        [JsonProperty("total_tax")]
        public decimal? TotalTax { get; set; }

        [JsonProperty("base_shipping_cost")]
        public decimal? BaseShippingCost { get; set; }

        [JsonProperty("shipping_cost_including_tax")]
        public decimal? ShippingCostIncludingTax { get; set; }

        [JsonProperty("shipping_cost_excluding_tax")]
        public decimal? ShippingCostExcludingTax { get; set; }

        [JsonProperty("shipping_cost_tax")]
        public decimal? ShippingCostTax { get; set; }

        [JsonProperty("base_handling_cost")]
        public decimal? BaseHandlingCost { get; set; }

        [JsonProperty("handling_cost_excluding_tax")]
        public decimal? HandlingCostExcludingTax { get; set; }

        [JsonProperty("handling_cost_including_tax")]
        public decimal? HandlingCostIncludingTax { get; set; }

        [JsonProperty("handling_cost_tax")]
        public decimal? HandlingCostTax { get; set; }

        [JsonProperty("base_wrapping_cost")]
        public decimal? BaseWrappingCost { get; set; }

        [JsonProperty("wrapping_cost_excluding_tax")]
        public decimal? WrappingCostExcludingTax { get; set; }

        [JsonProperty("wrapping_cost_including_tax")]
        public decimal? WrappingCostIncludingTax { get; set; }

        [JsonProperty("wrapping_cost_tax")]
        public decimal? WrappingCostTax { get; set; }

        [JsonProperty("billing_company")]
        public string BillingCompany { get; set; }

        [JsonProperty("billing_first_name")]
        public string BillingFirstName { get; set; }

        [JsonProperty("billing_last_name")]
        public string BillingLastName { get; set; }

        [JsonProperty("billing_address")]
        public string BillingAddress { get; set; }

        [JsonProperty("billing_address2")]
        public string BillingAddress2 { get; set; }

        [JsonProperty("billing_city")]
        public string BillingCity { get; set; }

        [JsonProperty("billing_state")]
        public string BillingState { get; set; }

        [JsonProperty("billing_country")]
        public string BillingCountry { get; set; }

        [JsonProperty("billing_postal_code")]
        public string BillingPostalCode { get; set; }

        [JsonProperty("billing_phone_number")]
        public string BillingPhoneNumber { get; set; }

        [JsonProperty("billing_email")]
        public string BillingEmail { get; set; }

        [JsonProperty("recipients")]
        public IList<Recipient> Recipients { get; set; }

        [JsonProperty("store_api_key")]
        public string StoreApiKey { get; set; }

        [JsonProperty("shipments")]
        public Shipment[] Shipments { get; set; }

        [JsonProperty("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonProperty("internal_notes")]
        public string InternalNotes { get; set; }

        [JsonProperty("meta_data")]
        public object MetaData { get; set; }

        [JsonProperty("prime_order_id")]
        public int? PrimeOrderId { get; set; }

        [JsonProperty("is_prime")]
        public object IsPrime { get; set; }

        [JsonProperty("split_from_order_id")]
        public int? SplitFromOrderId { get; set; }

        [JsonProperty("notes", NullValueHandling = NullValueHandling.Ignore)]
        public string Notes { get; set; }
    }

    public partial class Recipient
    {
        [JsonProperty("company")]
        public string Company { get; set; }

        [JsonProperty("first_name")]
        public string FirstName { get; set; }

        [JsonProperty("last_name")]
        public string LastName { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("address2")]
        public string Address2 { get; set; }

        [JsonProperty("address3")]
        public string Address3 { get; set; }

        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("residential")]
        public bool Residential { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("province")]
        public string Province { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("postal_code")]
        public string PostalCode { get; set; }

        [JsonProperty("postal_code_plus_4")]
        public string PostalCodePlus4 { get; set; }

        [JsonProperty("phone_number")]
        public string PhoneNumber { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("base_cost")]
        public string BaseCost { get; set; }

        [JsonProperty("cost_excluding_tax")]
        public string CostExcludingTax { get; set; }

        [JsonProperty("cost_including_tax")]
        public string CostIncludingTax { get; set; }

        [JsonProperty("cost_tax")]
        public string CostTax { get; set; }

        [JsonProperty("base_handling_cost")]
        public string BaseHandlingCost { get; set; }

        [JsonProperty("handling_cost_excluding_tax")]
        public string HandlingCostExcludingTax { get; set; }

        [JsonProperty("handling_cost_including_tax")]
        public string HandlingCostIncludingTax { get; set; }

        [JsonProperty("handling_cost_tax")]
        public string HandlingCostTax { get; set; }

        [JsonProperty("shipping_zone_id")]
        public string ShippingZoneId { get; set; }

        [JsonProperty("shipping_zone_name")]
        public string ShippingZoneName { get; set; }

        [JsonProperty("items_total")]
        public int? ItemsTotal { get; set; }

        [JsonProperty("shipping_method")]
        public string ShippingMethod { get; set; }

        [JsonProperty("items_shipped")]
        public int? ItemsShipped { get; set; }

        [JsonProperty("ext_shipping_detail_id")]
        public int? ExtShippingDetailId { get; set; }

        [JsonProperty("line_items")]
        public LineItem[] LineItems { get; set; }

        [JsonProperty("original_order")]
        public OriginalOrder OriginalOrder { get; set; }
    }

    public partial class LineItem
    {
        [JsonProperty("item_name")]
        public string ItemName { get; set; }

        [JsonProperty("sku")]
        public string Sku { get; set; }

        [JsonProperty("bin_picking_number")]
        public string BinPickingNumber { get; set; }

        [JsonProperty("bundled_product")]
        public string BundledProduct { get; set; }

        [JsonProperty("weight_in_ounces")]
        public string WeightInOunces { get; set; }

        [JsonProperty("quantity")]
        public int? Quantity { get; set; }

        [JsonProperty("total_excluding_tax")]
        public decimal? TotalExcludingTax { get; set; }

        [JsonProperty("price_excluding_tax")]
        public string PriceExcludingTax { get; set; }

        [JsonProperty("unit_price")]
        public string UnitPrice { get; set; }

        [JsonProperty("ext_line_item_id")]
        public string ExtLineItemId { get; set; }

        [JsonProperty("ext_product_id")]
        public string ExtProductId { get; set; }

        [JsonProperty("product_options")]
        public ProductOptions ProductOptions { get; set; }

        [JsonProperty("uuid")]
        public Guid Uuid { get; set; }

        [JsonProperty("order_source_id")]
        public int? OrderSourceId { get; set; }

        [JsonProperty("gift_message")]
        public string GiftMessage { get; set; }
    }

    public partial class ProductOptions
    {
        [JsonProperty("variant_id")]
        public int? VariantId { get; set; }

        [JsonProperty("variant_title")]
        public string VariantTitle { get; set; }

        [JsonProperty("variant_vendor")]
        public string VariantVendor { get; set; }

        [JsonProperty("variant_inventory_management")]
        public string VariantInventoryManagement { get; set; }

        [JsonProperty("Custom B Field")]
        public string CustomBField { get; set; }
    }

    public partial class OriginalOrder
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("customer_id")]
        public int? CustomerId { get; set; }

        [JsonProperty("store_id")]
        public int? StoreId { get; set; }

        [JsonProperty("order_detail_id")]
        public int? OrderDetailId { get; set; }

        [JsonProperty("workflow_state")]
        public string WorkflowState { get; set; }

        [JsonProperty("shipment_id")]
        public int? ShipmentId { get; set; }

        [JsonProperty("destination_id")]
        public int? DestinationId { get; set; }

        [JsonProperty("split_from_order_id")]
        public int? SplitFromOrderId { get; set; }

        [JsonProperty("total_weight_in_ounces")]
        public string TotalWeightInOunces { get; set; }

        [JsonProperty("total_quantity")]
        public int? TotalQuantity { get; set; }

        [JsonProperty("is_international")]
        public bool IsInternational { get; set; }

        [JsonProperty("usps_shipping_zone")]
        public int? UspsShippingZone { get; set; }

        [JsonProperty("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonProperty("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonProperty("ext_shipment_confirmation_id")]
        public int? ExtShipmentConfirmationId { get; set; }

        [JsonProperty("store_order_status")]
        public string StoreOrderStatus { get; set; }

        [JsonProperty("drop_ship_tracking_number")]
        public string DropShipTrackingNumber { get; set; }

        [JsonProperty("category_id")]
        public int? CategoryId { get; set; }

        [JsonProperty("internal_notes")]
        public string InternalNotes { get; set; }

        [JsonProperty("line_item_name")]
        public string LineItemName { get; set; }

        [JsonProperty("line_item_sku")]
        public string LineItemSku { get; set; }

        [JsonProperty("address_kind")]
        public string AddressKind { get; set; }

        [JsonProperty("recipient_last_name")]
        public string RecipientLastName { get; set; }

        [JsonProperty("recipient_requested_service")]
        public string RecipientRequestedService { get; set; }

        [JsonProperty("gift")]
        public bool Gift { get; set; }

        [JsonProperty("prime_order_id")]
        public int? PrimeOrderId { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("owner_id")]
        public int? OwnerId { get; set; }

        [JsonProperty("denormalized_ordered_at")]
        public DateTimeOffset? DenormalizedOrderedAt { get; set; }

        [JsonProperty("grab_bag")]
        public GrabBag GrabBag { get; set; }

        [JsonProperty("line_item_bin_picking_number")]
        public string LineItemBinPickingNumber { get; set; }

        [JsonProperty("denormalized_ext_order_id")]
        public string DenormalizedExtOrderId { get; set; }

        [JsonProperty("packing_slip_generated")]
        public bool? PackingSlipGenerated { get; set; }

        [JsonProperty("denormalized_ext_order_reference_id")]
        public string DenormalizedExtOrderReferenceId { get; set; }

        [JsonProperty("denormalized_alternate_order_id")]
        public string DenormalizedAlternateOrderId { get; set; }

        [JsonProperty("denormalized_sales_channel")]
        public string DenormalizedSalesChannel { get; set; }

        [JsonProperty("custom_1")]
        public string Custom1 { get; set; }

        [JsonProperty("custom_2")]
        public string Custom2 { get; set; }

        [JsonProperty("custom_3")]
        public string Custom3 { get; set; }
    }

    public partial class GrabBag
    {
    }

    public partial class Shipment
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("tracking_number")]
        public string TrackingNumber { get; set; }

        [JsonProperty("carrier_key")]
        public string CarrierKey { get; set; }

        [JsonProperty("carrier_service_key")]
        public string CarrierServiceKey { get; set; }

        [JsonProperty("shipment_cost")]
        public int? ShipmentCost { get; set; }

        [JsonProperty("ship_date")]
        public DateTimeOffset? ShipDate { get; set; }

        [JsonProperty("workflow_state")]
        public string WorkflowState { get; set; }

        [JsonProperty("cloned_from_shipment_id")]
        public int? ClonedFromShipmentId { get; set; }

        [JsonProperty("weight_in_ounces")]
        public string WeightInOunces { get; set; }

        [JsonProperty("length_in_inches")]
        public string LengthInInches { get; set; }

        [JsonProperty("width_in_inches")]
        public string WidthInInches { get; set; }

        [JsonProperty("height_in_inches")]
        public string HeightInInches { get; set; }

        [JsonProperty("additional_packages")]
        public AdditionalPackage[] AdditionalPackages { get; set; }
    }

    public partial class AdditionalPackage
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("tracking_number")]
        public string TrackingNumber { get; set; }

        [JsonProperty("weight_in_ounces")]
        public string WeightInOunces { get; set; }

        [JsonProperty("length_in_inches")]
        public string LengthInInches { get; set; }

        [JsonProperty("width_in_inches")]
        public string WidthInInches { get; set; }

        [JsonProperty("height_in_inches")]
        public string HeightInInches { get; set; }
    }
}
