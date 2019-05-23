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
    public class IDS_Track
    {
        private string ApiUsername;
        private string ApiPassword;

        private static string TestUrl = "http://track-api.contin-testing-site.com/";
        private static string ProductUrl = "http://track-api.contin-web.com/";
        private string Endpoint;

        public IDS_Track() : this(null) { }

        public IDS_Track(CarrierAPI Api)
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

        public Response<TrackToken> GetToken()
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

                var result = Request<TrackToken>("login", "POST", request);

                if (!result.status.Equals(200))
                    throw new Exception(result.message);

                return result;
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Get Token Error: {0}", e.InnerException?.Message ?? e.Message));
            }
        }

        public Response<Tracking> GetTrackingNumber(string tracking)
        {
           return Request<Tracking>("trackings/"+tracking, "GET");
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
                request.Headers.Add("token", GetToken().data.connection.token);

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