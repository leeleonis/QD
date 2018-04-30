﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using Newtonsoft.Json;
using QDLogistics.Models;

namespace DirectLineApi.IDS
{
    public class IDS_API
    {
        private string ApiUsername;
        private string ApiPassword;

        private static string Version = "v1.2";
        private static string TestUrl = string.Format("http://dev-labelservice.contin-testing-site.com/api/{0}/", Version);
        private static string ProductUrl = string.Format("http://labelservice.contin-web.com/api/{0}/", Version);
        private string Endpoint;

        public IDS_API() : this(null) { }

        public IDS_API(CarrierAPI Api)
        {
            if (Api == null)
            {
                Api = new CarrierAPI()
                {
                    IsTest = false,
                    ApiAccount = "TW018",
                    ApiPassword = "000000"
                };
            }

            ApiUsername = Api.ApiAccount;
            ApiPassword = Api.ApiPassword;

            Endpoint = Api.IsTest ? TestUrl : ProductUrl;
            //Endpoint = TestUrl;
        }

        public TokenResponse GetToken()
        {
            TokenRequest request = new TokenRequest()
            {
                username = ApiUsername,
                password = ApiPassword
            };

            TokenResponse response = Request<TokenResponse>("GetToken", "POST", request);

            return response;
        }

        public CreateOrderResponse CreateOrder(Packages package)
        {
            Orders order = package.Orders;
            Addresses address = order.Addresses;
            ShippingMethod method = package.Method;
            CreateOrderResponse response;

            try
            {
                TokenResponse userToken = GetToken();

                if (!userToken.status.Equals("200")) throw new Exception(userToken.error.ToString().Trim());

                string[] serviceTypeList = GetServiceTypeList();
                List<OrderData> orderList = new List<OrderData>();

                List<Items> itemList = package.Items.Where(i => i.IsEnable.Value).ToList();
                orderList.Add(new OrderData()
                {
                    salesRecordNumber = order.OrderID.ToString(),
                    productType = string.Join(",", itemList.Select(i => i.Skus.ProductType).Select(t => t.ProductTypeName).Distinct()),
                    serviceType = serviceTypeList[method.MethodType.Value],
                    buyerName = string.Join(" ", new string[] { address.FirstName, address.MiddleInitial, address.LastName }),
                    buyerPhone = !string.IsNullOrEmpty(address.PhoneNumber) ? address.PhoneNumber.Trim() : "",
                    buyerAddress1 = address.StreetLine1.Trim(),
                    buyerAddress2 = !string.IsNullOrEmpty(address.StreetLine2) ? address.StreetLine2.Trim() : "",
                    buyerCity = address.City.Trim(),
                    buyerCountry = address.CountryName.Trim(),
                    buyerDistrict = address.CountryCode.Equals("US") ? "" : "E",
                    buyerState = !string.IsNullOrEmpty(address.StateName) ? address.StateName : "",
                    buyerZip = address.PostalCode,
                    weight = itemList.Sum(i => i.Qty.Value * i.Skus.Weight),
                    quantity = itemList.Sum(i => i.Qty.Value),
                    cost = itemList.Sum(i => i.Qty.Value * (float)i.DeclaredValue),
                    remarks = !string.IsNullOrEmpty(package.SupplierComment) ? package.SupplierComment.Trim() : ""
                });

                CreateOrderRequest request = new CreateOrderRequest()
                {
                    userName = ApiUsername,
                    token = userToken.token,
                    orderType = "D",
                    orders = orderList.ToArray(),
                    notificationURL = ""
                };

                response = Request<CreateOrderResponse>("CreateOrder", "POST", request);
            }
            catch (Exception e)
            {
                response = new CreateOrderResponse()
                {
                    status = "400",
                    error = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message
                };
            }

            return response;
        }

        public GetTrackingNumberResponse GetTrackingNumber(Packages package)
        {
            GetTrackingNumberRequest request = new GetTrackingNumberRequest()
            {
                salesRecordNumber = new string[] { package.OrderID.ToString() }
            };

            return Request<GetTrackingNumberResponse>("GetTrackingNumber", "POST", request);
        }

        public string[] GetServiceTypeList()
        {
            return Request<string[]>("GetServiceTypeList", "GET");
        }

        public T Request<T>(string func, string method, object data = null)
        {
            string result = "";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Endpoint + func);
            request.ContentType = "application/json";
            request.Method = method;
            request.ProtocolVersion = HttpVersion.Version10;

            HttpWebResponse httpResponse;

            if (data != null)
            {
                string postData = JsonConvert.SerializeObject(data);
                using (StreamWriter streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(postData);
                    streamWriter.Flush();
                    streamWriter.Close();
                }
            }

            httpResponse = (HttpWebResponse)request.GetResponse();
            using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }

            return JsonConvert.DeserializeObject<T>(result);
        }
    }

    public class TokenRequest
    {
        public string username { get; set; }
        public string password { get; set; }
    }

    public class TokenResponse : Response
    {
        public string token { get; set; }
    }

    public class CreateOrderRequest
    {
        public string userName { get; set; }
        public string token { get; set; }
        public string orderType { get; set; }
        public string notificationURL { get; set; }
        public OrderData[] orders { get; set; }
    }

    public class OrderData
    {
        public string salesRecordNumber { get; set; }
        public string sku { get; set; }
        public string productType { get; set; }
        public string serviceType { get; set; }
        public string buyerName { get; set; }
        public string buyerPhone { get; set; }
        public string buyerAddress1 { get; set; }
        public string buyerAddress2 { get; set; }
        public string buyerCity { get; set; }
        public string buyerCountry { get; set; }
        public string buyerDistrict { get; set; }
        public string buyerState { get; set; }
        public string buyerZip { get; set; }
        public int length { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public int weight { get; set; }
        public int quantity { get; set; }
        public float cost { get; set; }
        public string remarks { get; set; }
    }

    public class CreateOrderResponse : Response
    {
        public OrderLabel[] labels { get; set; }
    }

    public class OrderLabel
    {
        public string orderid { get; set; }
        public string salesRecordNumber { get; set; }
        public string labellink { get; set; }
    }

    public class GetTrackingNumberRequest
    {
        public string[] salesRecordNumber { get; set; }
    }

    public class GetTrackingNumberResponse : Response
    {
        public List<string[]> trackingnumber { get; set; }
    }

    public class Response
    {
        public string status { get; set; }
        public object error { get; set; }
    }
}