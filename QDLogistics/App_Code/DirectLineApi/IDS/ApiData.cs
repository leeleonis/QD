using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace DirectLineApi.IDS.Data
{
    public class TokenRequest
    {
        public string account { get; set; }
        public string password { get; set; }
    }

    public class Token
    {
        public string token { get; set; }
        public int token_validity_period { get; set; }
        public string expiration_type { get; set; }
    }

    public class TrackToken
    {
        public Token connection { get; set; }
    }

    public class CreateOrderRequest
    {
        public string notificationURL { get; set; }
        public OrderData[] orders { get; set; }
    }

    public class OrderData
    {
        // Request
        public string sales_record_number { get; set; }
        public string service_type { get; set; }
        public decimal cost { get; set; }
        public string product_type { get; set; }
        public string buyer_name { get; set; }
        public string buyer_address1 { get; set; }
        public string buyer_city { get; set; }
        public string buyer_country { get; set; }
        public string buyer_zip { get; set; }

        // Optional
        public string buyer_address2 { get; set; }
        public string buyer_phone { get; set; }
        public string buyer_state { get; set; }
        public string tracking_number { get; set; }
        public string sku { get; set; }
    }

    public class OrderResponse
    {
        public string bbcode { get; set; }
        public string tracking_number { get; set; }
        public OrderLabel label { get; set; }
        public OrderLabel[] labels { get; set; }
    }

    public class OrderLabel
    {
        public string bbcode { get; set; }
        public string sales_record_number { get; set; }
        public string content { get; set; }
    }

    public class Tracking
    {
        public string ols_key { get; set; }
        public string sales_record_number { get; set; }
        public string tracking_number { get; set; }
        public string slug { get; set; }
        public  CheckPoint[] checkpoints { get; set; }
    }

    public class CheckPoint
    {
        public string date { get; set; }
        public string time { get; set; }
        public string message { get; set; }
        public string location { get; set; }
        public string tag { get; set; }
    }

    public class ServiceTypes
    {
        public string[] service_types { get; set; }
    }

    public class Response<T>
    {
        private string messageField { get; set; }

        public int status { get; set; }

        public T data { get; set; }

        public string message { get { return error != null && error.Any() ? string.Join(", ", error.Select(e => e.key + "-" + e.message).ToArray()) : messageField; } set { messageField = value; } }

        public Error[] error { get; set; }
    }

    public class Error
    {
        private string messageField { get; set; }
        public string key { get; set; }

        public string code { get; set; }

        public string message { get { return messageField; } set { messageField = value; } }
        public string messages { set { messageField = value; } }
    }
}