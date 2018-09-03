using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Newtonsoft.Json;
using QDLogistics.Models;

namespace CarrierApi.Sendle
{
    public class Sendle_API
    {
        readonly string Sendbox_Server = "https://sandbox.sendle.com/api/";
        readonly string Product_Server = "https://api.sendle.com/api/";

        readonly string Sendle_ID;
        readonly string Api_Key;
        readonly string Endpoint;

        public Sendle_API(CarrierAPI Api)
        {
            Endpoint = Api.IsTest ? Sendbox_Server : Product_Server;
            Sendle_ID = Api.ApiAccount;
            Api_Key = Api.ApiKey;
        }

        public OrderResponse Create(Packages package)
        {
            Addresses address = package.Orders.Addresses;

            DateTime pickup_date = new TimeZoneConvert().ConvertDateTime(QDLogistics.Commons.EnumData.TimeZone.AEST);
            decimal weight = package.Items.Where(i => i.IsEnable.Value).Sum(i => (i.Qty.Value * (decimal)i.Skus.Weight) / 1000);

            OrderRequest request = new OrderRequest()
            {
                pickup_date = MyHelp.SkipWeekend(pickup_date.AddDays(2)).ToString("yyyy-MM-dd"),
                description = "Merchandise",
                kilogram_weight = weight.ToString(),
                customer_reference = package.OrderID.Value.ToString(),
                sender = SetSender(),
                receiver = new AddressDetail()
                {
                    instructions = weight > 0.5M ? "Must have signature on delivery. Thank you!" : null,
                    contact = new Contact()
                    {
                        name = string.Join(" ", new string[] { address.FirstName, address.MiddleInitial, address.LastName }),
                        email = address.EmailAddress,
                        phone = address.PhoneNumber,
                        company = address.CompanyName
                    },
                    address = new Address()
                    {
                        address_line1 = address.StreetLine1,
                        address_line2 = address.StreetLine2,
                        suburb = address.City,
                        postcode = address.PostalCode,
                        state_name = address.StateName,
                        country = address.CountryName
                    }
                }
            };

            return Request<OrderResponse>("orders", "POST", request);
        }

        public OrderResponse Order(string order_id)
        {
            return Request<OrderResponse>("orders/" + order_id, "GET");
        }

        public void Label(string order_id, string code, string filePath)
        {
            string pdf_url = string.Format("{0}/orders/{1}/labels/cropped.pdf", Endpoint, order_id);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(pdf_url);
            request.Accept = "application/pdf";
            request.Credentials = new NetworkCredential(Sendle_ID, Api_Key);
            request.PreAuthenticate = true;
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

            if (File.Exists(Path.Combine(filePath, "AirWaybill.pdf"))) File.Delete(Path.Combine(filePath, "AirWaybill.pdf"));
            using (HttpWebResponse httpResponse = (HttpWebResponse)request.GetResponse())
            {
                var response = (HttpWebResponse)request.GetResponse();
                var responseStream = response.GetResponseStream();
                var fileStream = File.Create(Path.Combine(filePath, "AirWaybill.pdf"));
                responseStream.CopyTo(fileStream);
                responseStream.Close();
                fileStream.Close();
                response.Close();
            }

            if (File.Exists(Path.Combine(filePath, "Label.pdf"))) File.Delete(Path.Combine(filePath, "Label.pdf"));
            Document document = new Document();
            PdfWriter writer = PdfWriter.GetInstance(document, new FileStream(Path.Combine(filePath, "Label.pdf"), FileMode.Create));
            document.Open();
            PdfContentByte cb = writer.DirectContent;
            PdfReader reader = new PdfReader(Path.Combine(filePath, "AirWaybill.pdf"));
            document.SetPageSize(reader.GetPageSizeWithRotation(1));
            document.NewPage();

            Paragraph pp = new Paragraph();
            Barcode128 barcode = new Barcode128 { CodeType = Barcode.CODE128_UCC, Code = code };
            Image barcodeImage = barcode.CreateImageWithBarcode(cb, null, null);
            barcodeImage.Alignment = Element.ALIGN_CENTER;
            pp.Add(barcodeImage);
            document.Add(pp);

            document.Close();
            writer.Close();
        }

        public TrackResponse Track(string reference)
        {
            return Request<TrackResponse>("tracking/" + reference, "GET");
        }

        public CancelResponse Cancel(string order_id)
        {
            return Request<CancelResponse>("orders/" + order_id, "DELETE");
        }

        public T Request<T>(string func, string method, object data = null)
        {
            string result = "";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Endpoint + func);
            request.ContentType = "application/json";
            request.Method = method;
            request.Credentials = new NetworkCredential(Sendle_ID, Api_Key);
            request.PreAuthenticate = true;
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

            HttpWebResponse httpResponse;

            try
            {
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
                var status = httpResponse.StatusCode;
                using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    result = streamReader.ReadToEnd();
                }
            }
            catch (WebException e)
            {
                using (WebResponse response = e.Response)
                {
                    httpResponse = (HttpWebResponse)response;
                    using (Stream responseData = response.GetResponseStream())
                    using (var reader = new StreamReader(responseData))
                    {
                        string msg = reader.ReadToEnd();
                        MyHelp.Log("Sendle", null, string.Format("建立Sendle提單失敗 - {0}", msg));
                        ErrorResponse error = JsonConvert.DeserializeObject<ErrorResponse>(msg);
                        throw new Exception(GetErrorMsg(error.messages));
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }

            return JsonConvert.DeserializeObject<T>(result);
        }

        private AddressDetail SetSender()
        {
            return new AddressDetail()
            {
                instructions = "Please pickup from ECOF Block T, unit 3 /391 Park Rd, Regents Park, NSW, 2143",
                contact = new Contact()
                {
                    name = "Nina Kuo",
                    company = "ECOF"
                },
                address = new Address()
                {
                    address_line1 = "Block T, unit 3 /391 Park Road",
                    suburb = "Regents Park",
                    postcode = "2143",
                    state_name = "NSW",
                    country = "Australia"
                }
            };
        }

        public class OrderDetail
        {
            public string description { get; set; }
            public string kilogram_weight { get; set; }
            public string cubic_metre_volume { get; set; }
            public string customer_reference { get; set; }
            public Dictionary<string, object> metadata { get; set; }
            public AddressDetail sender { get; set; }
            public AddressDetail receiver { get; set; }
        }

        public class OrderRequest : OrderDetail
        {
            public string pickup_date { get; set; }
        }

        public class AddressDetail
        {
            public string instructions { get; set; }
            public Contact contact { get; set; }
            public Address address { get; set; }
        }

        public class Contact
        {
            public string name { get; set; }
            public string email { get; set; }
            public string phone { get; set; }
            public string company { get; set; }
        }

        public class Address
        {
            public string address_line1 { get; set; }
            public string address_line2 { get; set; }
            public string suburb { get; set; }
            public string postcode { get; set; }
            public string state_name { get; set; }
            public string country { get; set; }
        }

        public class OrderResponse : OrderDetail
        {
            public string order_id { get; set; }
            public string state { get; set; }
            public StatusData status { get; set; }
            public string order_url { get; set; }
            public string sendle_reference { get; set; }
            public string tracking_url { get; set; }
            public LabelData[] labels { get; set; }
            public SchedulingData scheduling { get; set; }
            public RouteData route { get; set; }
            public PriceDetail price { get; set; }
        }

        public class StatusData
        {
            public string description { get; set; }
            public string last_changed_at { get; set; }
        }

        public class LabelData
        {
            public string format { get; set; }
            public string size { get; set; }
            public string url { get; set; }
        }

        public class SchedulingData
        {
            public bool is_cancellable { get; set; }
            public string pickup_date { get; set; }
        }

        public class RouteData
        {
            public string description { get; set; }
            public string type { get; set; }
            public string delivery_guarantee_status { get; set; }
        }

        public class PriceDetail
        {
            public ChargeData tax { get; set; }
            public ChargeData net { get; set; }
            public ChargeData gross { get; set; }
        }

        public class ChargeData
        {
            public string currency { get; set; }
            public decimal amount { get; set; }
        }

        public class TrackResponse
        {
            public string state { get; set; }
            public EventData[] tracking_events { get; set; }
        }

        public class EventData
        {
            public string event_type { get; set; }
            public DateTime scan_time { get; set; }
            public string description { get; set; }
            public string origin_location { get; set; }
            public string destination_location { get; set; }
            public string reason { get; set; }
        }

        public class CancelResponse
        {
            public string order_id { get; set; }
            public string state { get; set; }
            public string order_url { get; set; }
            public string sendle_reference { get; set; }
            public string tracking_url { get; set; }
            public string customer_reference { get; set; }
            public string cancelled_at { get; set; }
            public string cancellation_message { get; set; }

        }

        public class ErrorResponse
        {
            public Dictionary<string, List<object>> messages { get; set; }
            public string error { get; set; }
            public string error_description { get; set; }
        }

        private string GetErrorMsg(object data)
        {
            string msg = "";

            if (data is Dictionary<string, List<object>>)
            {
                foreach (var oData in (Dictionary<string, List<object>>)data)
                {
                    if (oData.Value.Any())
                    {
                        msg += string.Format("{0} => {1}", oData.Key, GetErrorMsg(oData.Value.First()));
                    }
                }
            }
            else if (data is string)
            {
                msg = data.ToString() + "; ";
            }
            else
            {
                var JData = (Newtonsoft.Json.Linq.JObject)data;
                msg = GetErrorMsg(JData.ToObject<Dictionary<string, List<object>>>());
            }

            return msg;
        }
    }
}