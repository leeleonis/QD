using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using DirectLineApi.IDS.Data;
using Newtonsoft.Json;
using QDLogistics.Commons;
using QDLogistics.Models;

namespace DirectLineApi.IDS
{
    public class IDS_API
    {
        private string ApiUsername;
        private string ApiPassword;

        private static string TestUrl = "http://label-api.contin-testing-site.com/";
        private static string ProductUrl = "http://label-api.contin-web.com/";
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
                    ApiPassword = "CPl78h"
                };
            }

            ApiUsername = Api.ApiAccount;
            ApiPassword = Api.ApiPassword;

            Endpoint = Api.IsTest ? TestUrl : ProductUrl;
            //Endpoint = TestUrl;
        }

        public Response<Token> GetToken()
        {
            try
            {
                var request = new
                {
                    data = new TokenRequest()
                    {
                        account = ApiUsername,
                        password = ApiPassword
                    }
                };

                var result = Request<Token>("login", "POST", request);

                if (!result.status.Equals(200))
                    throw new Exception(result.message);

                return result;
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Get Token Error: {0}", e.InnerException?.Message ?? e.Message));
            }
        }

        public Response<OrderResponse> CreateOrder(Packages package, ShippingMethod method = null)
        {
            Orders order = package.Orders;
            Addresses address = order.Addresses;
            if (method == null) method = package.Method;

            try
            {
                var serviceTypeList = GetServiceTypeList();

                string buyerName = string.Join(" ", new string[] { address.FirstName, address.MiddleInitial, address.LastName });
                string buyerAddress2 = !string.IsNullOrEmpty(address.StreetLine2) ? address.StreetLine2.Trim() : "";
                if (!string.IsNullOrEmpty(address.CompanyName))
                {
                    string companyName = address.CompanyName.Trim();
                    if (string.Format("{0} {1}", buyerName, companyName).Length <= 50)
                    {
                        buyerName = string.Format("{0} {1}", buyerName, companyName).Trim();
                    }
                    else
                    {
                        buyerAddress2 = string.Format("{0} {1}", companyName, buyerAddress2).Trim();
                    }
                }

                List<Items> itemList = package.Items.Where(i => i.IsEnable.Value).ToList();

                var orderData = new OrderData()
                {
                    service_type = serviceTypeList[method.MethodType.Value],
                    sales_record_number = string.Format("{0}-{1}", package.OrderID, Convert.ToInt32(package.ShipDate.Value.Subtract(new DateTime(1970, 1, 1)).TotalSeconds)),
                    product_type = string.Join(",", itemList.Select(i => i.Skus.ProductType).Select(t => t.ProductTypeName).Distinct()),
                    buyer_name = buyerName,
                    buyer_phone = !string.IsNullOrEmpty(address.PhoneNumber) ? address.PhoneNumber.Trim() : "",
                    buyer_address1 = address.StreetLine1.Trim(),
                    buyer_address2 = buyerAddress2,
                    buyer_city = address.City.Trim(),
                    buyer_country = address.CountryName.Trim(),
                    buyer_state = !string.IsNullOrEmpty(address.StateName) ? (address.CountryCode.Equals("US") && address.StateName.Length > 2 ? EnumData.StateAbbreviationExpand(address.StateName.Trim()) : address.StateName.Trim()) : "",
                    buyer_zip = address.CountryCode.Equals("US") ? address.PostalCode.Split('-').First() : address.PostalCode.Trim(),
                    cost = itemList.Sum(i => i.Qty.Value * i.DLDeclaredValue)
                };

                var request = new
                {
                    data = new CreateOrderRequest
                    {
                        orders = new OrderData[] { orderData }
                    }
                };

                return Request<OrderResponse>("orders", "POST", request);
            }
            catch (Exception e)
            {
                return new Response<OrderResponse>()
                {
                    status = 400,
                    message = e.InnerException?.Message ?? e.Message
                };
            }
        }

        public Response<OrderResponse> GetLabelByOrderID(string order_id)
        {
            return Request<OrderResponse>("label/bbcode/" + order_id, "Get");
        }

        public Response<OrderResponse> GetLabelByTracking(string tracking_number)
        {
            return Request<OrderResponse>("label/tracking_number/" + tracking_number, "Get");
        }

        public string GetTrackingNumber(Packages package)
        {
            string trackingNumber = "";

            var result = GetLabelByOrderID(package.TagNo);
            if (result.status.Equals(200))
            {
                trackingNumber = result.data.tracking_number ?? "";
            }
            else
            {
                MyHelp.Log("IDS_API", null, "Get label tracking number failed： " + result.message);
            }

            return trackingNumber;
        }

        public string[] GetServiceTypeList()
        {
            //string json = JsonConvert.SerializeObject(Request<ServiceTypes>("service_types", "GET").data.service_types);
            //string serviceTypeJson = "[\"AM\",\"AML\",\"AMR\",\"AUCU\",\"AUHV\",\"AULL\",\"AUOE\",\"AUPT\",\"AURLL\",\"AURP\",\"AUSK\",\"AZP\",\"CND\",\"CNF\",\"COR\",\"DEDHL\",\"DEDHLUK\",\"DELL\",\"DEML\",\"DEP\",\"DEPUK\",\"DGM\",\"DGMR\",\"DHLG\",\"DPDG\",\"ES48\",\"ESLL\",\"EUCU\",\"FBAAU\",\"FBADE\",\"FBAFR\",\"FBAJP\",\"FBAUK\",\"FBAUS\",\"FBAUSFE\",\"FBAUSFP\",\"FC\",\"FCI\",\"FCS\",\"FR100\",\"FR250\",\"FR50\",\"FSI\",\"FSP\",\"HKCU\",\"HS\",\"HSS\",\"ILES\",\"IPFRES\",\"ITGLS\",\"KRCU\",\"MI\",\"MII\",\"MIP\",\"ND\",\"PIS\",\"PM\",\"PMB\",\"PMD\",\"PMFR\",\"PMI\",\"PMS\",\"PSI\",\"RM48LL\",\"RM48P\",\"RMLL\",\"RMP\",\"RMRLL\",\"SG\",\"SGE\",\"SGR\",\"SUP\",\"T24\",\"T24S\",\"T48\",\"T48S\",\"TP\",\"TWCU\",\"TWFCS\",\"TWMFCS\",\"TWMPMS\",\"TWMUGS\",\"TWPMS\",\"TWUGS\",\"U2D\",\"UG\",\"UGR\",\"UGS\",\"UGSR\",\"UKCU\",\"UKM\",\"UPSG\",\"USOE\",\"X08FCF\",\"X08PMD\",\"X08PMED\",\"X08PMEF\",\"X08PMF\"]";
            string serviceTypeJson = "[\"AM\",\"AML\",\"AMR\",\"AULL\",\"AURLL\",\"AURP\",\"CND\",\"CNF\",\"COR\",\"DELL\",\"DEDHL\",\"DEML\",\"DGM\",\"DGMR\",\"DEP\",\"ES48\",\"EUCU\",\"ESLL\",\"FBAAU\",\"FBADE\",\"FBAJP\",\"FBAUK\",\"FBAUS\",\"FBAFR\",\"FC\",\"HS\",\"ND\",\"PM\",\"RM48LL\",\"RM48P\",\"RMLL\",\"RMP\",\"RMRLL\",\"SG\",\"SGR\",\"T24\",\"T24S\",\"T48\",\"T48S\",\"TP\",\"TS\",\"UG\",\"UKCU\",\"USOE\",\"X08FCF\",\"X08PMF\",\"X08PMEF\",\"X08PMD\",\"X08PMED\",\"FCI\",\"FCS\",\"FSI\",\"PMI\",\"PMS\",\"PSI\",\"ILES\",\"PIS\",\"IPFRES\",\"PMFR\",\"HSS\",\"UPSG\",\"PMD\",\"MI\",\"DPDG\",\"FBAUSFE\",\"FBAUSFP\",\"UGS\",\"UGR\",\"UGSR\",\"AUPT\",\"AUHV\",\"AUOE\",\"U2D\",\"AUCU\",\"KRCU\",\"MII\",\"MIP\",\"HKCU\",\"DHLG\",\"FR50\",\"FR100\",\"FR250\",\"AUSK\",\"DEDHLUK\",\"DEPUK\",\"AZP\",\"TWCU\",\"UKM\",\"ITGLS\",\"SGE\",\"TWFCS\",\"TWPMS\",\"TWUGS\",\"TWMFCS\",\"TWMPMS\",\"TWMUGS\",\"PMB\",\"SUP\"]";
            return JsonConvert.DeserializeObject<string[]>(serviceTypeJson);
        }

        public string GetServiceType(int index)
        {
            string[] serviceTypeList = GetServiceTypeList();

            return serviceTypeList.Skip(index).FirstOrDefault();
        }

        public Response<T> Request<T>(string func, string method, object data = null)
        {
            string result = "";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Endpoint + func);
            request.ContentType = "application/json";
            request.Method = method;
            request.Timeout = 20000;
            request.ProtocolVersion = HttpVersion.Version10;

            if (!func.Equals("login"))
                request.Headers.Add("Authorization", GetToken().data.token);

            if (data != null)
            {
                var jsonSetting = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
                string postData = JsonConvert.SerializeObject(data, jsonSetting);
                using (StreamWriter streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(postData);
                    streamWriter.Flush();
                }
            }

            try
            {
                using (HttpWebResponse httpResponse = (HttpWebResponse)request.GetResponse())
                using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    if(httpResponse.Headers["X-RateLimit-Remaining"] != null)
                        MyHelp.Log("IDS_API", null, "X-RateLimit-Remaining: " + httpResponse.Headers["X-RateLimit-Remaining"]);

                    result = streamReader.ReadToEnd();
                }
            }
            catch (WebException wex)
            {
                if(wex.Response != null)
                {
                    using (StreamReader streamReader = new StreamReader(wex.Response.GetResponseStream()))
                    {
                        if (wex.Response.Headers["X-RateLimit-Remaining"] != null)
                            MyHelp.Log("IDS_API", null, "X-RateLimit-Remaining: " + wex.Response.Headers["X-RateLimit-Remaining"]);

                        result = streamReader.ReadToEnd();
                    }
                }
                else
                {
                    throw new Exception(wex.InnerException?.Message ?? wex.Message);
                }
            }

            return JsonConvert.DeserializeObject<Response<T>>(result);
        }
    }
}