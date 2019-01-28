using System;
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
        private readonly string storeApiKey = "";

        private readonly string baseUrl = "https://app.shippingeasy.com/";

        public SE_API()
        {

        }

        public OrderQueryResponse GetOrderByID(string ID)
        {
            OrderQuery query = new OrderQuery()
            {
                OrderNumber = ID
            };

            var client = new Client(apiKey, apiSecret);
            return client.GetOrders(query);
        }

        public OrderQueryResponse GetOrdersByDate(int days)
        {
            OrderQuery query = new OrderQuery()
            {
                LastUpdated = new DateTimeOffset(DateTime.UtcNow.AddDays(1 - days))
            };

            var client = new Client(apiKey, apiSecret);
            return client.GetOrders(query);
        }

        public CreateOrderResponse CreateOrder(Packages package)
        {
            DateTimeOffset now = new DateTimeOffset(DateTime.Now);

            Order order = new Order()
            {
                ExternalOrderIdentifier = string.Format("{0}-{1}", package.OrderID, Convert.ToInt32(package.ShipDate.Value.Subtract(new DateTime(1970, 1,1)).TotalSeconds)),
                OrderedAt = now
            };

            var client = new Client(apiKey, apiSecret);
            return client.CreateOrder(storeApiKey, order);
        }

        //public OrderData CreateOrder(Packages package)
        //{
        //    Addresses address = package.Orders.Addresses;

        //    var recipient = new RecipientData
        //    {
        //        first_name = address.FirstName,
        //        last_name = address.LastName,
        //        phone_number = address.PhoneNumber,
        //        email = address.EmailAddress,
        //        address = address.StreetLine1,
        //        address2 = address.StreetLine1,
        //        postal_code = address.PostalCode,
        //        city = address.City,
        //        state = address.StateName,
        //        country = address.CountryName
        //    };

        //    Order orderData = new Order()
        //    {
        //        ExternalOrderIdentifier = string.Format("{0}-{1}", package.OrderID.ToString(), Convert.ToInt32(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds)),
        //        OrderedAt = DateTime.UtcNow,
        //        Recipients = { recipient }
        //    };

        //    return client.CreateOrder(storeApiKey, orderData);
        //}

        //public void CancelOrder(int tracking)
        //{
        //    return client.GetOrders();
        //}
    }
}