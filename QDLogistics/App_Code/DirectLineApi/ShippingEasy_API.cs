using System;
using System.Collections.Generic;
using System.Linq;
using DirectLineApi.ShippingEasy;
using QDLogistics.Models;
using ShippingEasy.Request;
using ShippingEasy.Responses;

namespace QDLogistics.DirectLineApi.ShippingEasy
{
    public class SE_API
    {
        // from Settings > API Credentials
        private readonly string apiKey = "ba208f2033828c018a39b9fce044c697";
        private readonly string apiSecret = "da2be296c1e7adfb2e66592c40d7371843da6ee3885ebb501f365e2d2bdc8ba2";

        // from Settings > Stores
        private readonly string storeApiKey = "b5e3710124b3ae026c48f40e094476f9";

        private Client client;

        public SE_API()
        {
            client = new Client(apiKey, apiSecret);
        }

        public OrderQueryResponse GetOrderByID(string ID)
        {
            OrderQuery query = new OrderQuery()
            {
                OrderNumber = ID,
                Status = "shipped,ready_for_shipment"
            };

            return client.GetOrders(query);
        }

        public OrderQueryResponse GetOrdersByDate(int days)
        {
            OrderQuery query = new OrderQuery()
            {
                LastUpdated = new DateTimeOffset(DateTime.UtcNow.AddDays(1 - days))
            };

            return client.GetOrders(query);
        }

        public CreateOrderResponse CreateOrder(Packages package)
        {
            DateTimeOffset now = new DateTimeOffset(DateTime.Now);
            Addresses address = package.Orders.Addresses;

            var recipient = new Recipient
            {
                Company = address.CompanyName,
                FirstName = address.FirstName,
                LastName = address.LastName,
                Address = address.StreetLine1,
                Address2 = address.StreetLine2,
                City = address.City,
                State = address.StateName,
                Country = address.CountryName,
                PostalCode = address.PostalCode,
                PhoneNumber = address.PhoneNumber,
                Email = address.EmailAddress,
                LineItems = package.Items.Where(i => i.IsEnable.Value).Select(i => new LineItem()
                {
                    Sku = i.ProductID,
                    ItemName = i.Skus.ProductName,
                    Quantity = i.Qty
                }).ToArray()
            };

            Order order = new Order()
            {
                ExternalOrderIdentifier = string.Format("{0}-{1}", package.OrderID, Convert.ToInt32(package.ShipDate.Value.Subtract(new DateTime(1970, 1, 1)).TotalSeconds)),
                OrderedAt = now,
                Recipients = new List<Recipient>() { recipient },
                Notes = string.Format("Order # {0}", package.OrderID),
                UpdatedAt = now
            };

            return client.CreateOrder(storeApiKey, order);
        }

        public CancelOrderResponse CancelOrder(string ID)
        {
            return client.CancelOrder(storeApiKey, ID);
        }
    }
}