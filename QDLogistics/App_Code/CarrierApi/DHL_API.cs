using QDLogistics.Models;
using QDLogistics.Models.Repositiry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml;
using System.Xml.Serialization;

namespace CarrierApi.DHL
{
    public class DHL_API
    {
        private string api_siteID;
        private string api_password;
        private string api_account;

        protected Request request;

        private static string FILE_PATH = @"D:\serialized_request.txt";

        public DHL_API(CarrierAPI Api)
        {
            api_siteID = Api.ApiKey;
            api_password = Api.ApiPassword;
            api_account = Api.ApiAccount;
        }

        public TrackingResponse Tracking(string trackingNumber)
        {
            TrackingResponse result;
            KnownTrackingRequest track = setTracking(new string[] { trackingNumber });

            XmlSerializer serializer = new XmlSerializer(typeof(TrackingResponse));
            using (TextReader reader = new StringReader(SendRequest(track)))
            {
                try
                {
                    result = (TrackingResponse)serializer.Deserialize(reader);
                }
                catch (Exception e)
                {
                    result = new TrackingResponse()
                    {
                        AWBInfo = new AWBInfo[] {
                            new AWBInfo() { Status = new Status() { ActionStatus = e.Message } }
                        }
                    };
                }
            }

            return result;
        }

        private KnownTrackingRequest setTracking(string[] items)
        {
            var tracking = new KnownTrackingRequest();
            tracking.Request = requset1Init();
            tracking.LanguageCode = "tw";
            tracking.Items = items;
            tracking.ItemsElementName = new ItemsChoiceType[] { 0 };
            tracking.LevelOfDetails = LevelOfDetails.ALL_CHECK_POINTS;
            tracking.PiecesEnabled = KnownTrackingRequestPiecesEnabled.S;

            return tracking;
        }

        public ShipmentValidateResponse Create(Packages package)
        {
            ShipmentValidateResponse result;
            ShipmentValidateRequestEA shipment = setShipment(package);

            XmlSerializer serializer = new XmlSerializer(typeof(ShipmentValidateResponse));
            string request = SendRequest(shipment);
            using (TextReader reader = new StringReader(request))
            {
                try
                {
                    result = (ShipmentValidateResponse)serializer.Deserialize(reader);
                }
                catch (Exception e)
                {
                    TextReader errorReader = new StringReader(request);
                    XmlSerializer errorSerializer = new XmlSerializer(typeof(ShipmentValidateErrorResponse));
                    ShipmentValidateErrorResponse error = errorSerializer.Deserialize(errorReader) as ShipmentValidateErrorResponse;
                    errorReader.Dispose();
                    throw new Exception(string.Join("; ", error.Response.Status.Condition.Select(c => c.ConditionData)));
                }
            }

            return result;
        }

        private ShipmentValidateRequestEA setShipment(Packages package)
        {
            var shipment = new ShipmentValidateRequestEA();

            shipment.Request = requsetInit();
            //shipment.RegionCode = "AP";
            shipment.NewShipper = YesNo1.N;
            shipment.NewShipperSpecified = true;
            shipment.LanguageCode = "tw";
            shipment.PiecesEnabled = PiecesEnabled.Y;
            shipment.PiecesEnabledSpecified = true;
            shipment.Reference = new Reference1[] { new Reference1() { ReferenceID = package.OrderID.ToString() } };
            shipment.LabelImageFormat = LabelImageFormat.PDF;
            shipment.LabelImageFormatSpecified = true;
            shipment.RequestArchiveDoc = YesNo1.Y;
            shipment.RequestArchiveDocSpecified = true;
            shipment.Label = new Label() { LabelTemplate = "8X4_A4_PDF" };

            shipment.Billing = new Billing1()
            {
                ShipperAccountNumber = api_account,
                ShippingPaymentType = ShipmentPaymentType.S,
                BillingAccountNumber = api_account,
                DutyPaymentType = DutyTaxPaymentType1.S,
                DutyPaymentTypeSpecified = true,
                DutyAccountNumber = api_account
            };

            Addresses address = package.Orders.Addresses;
            Contact2 contact = new Contact2()
            {
                PersonName = string.Join(" ", new string[] { address.FirstName, address.MiddleInitial, address.LastName }),
                PhoneNumber = address.PhoneNumber
            };

            shipment.Consignee = new Consignee1()
            {
                CompanyName = !string.IsNullOrEmpty(address.CompanyName) ? address.CompanyName : contact.PersonName,
                AddressLine = new string[] { address.StreetLine1, address.StreetLine2 },
                City = address.City,
                Division = address.StateName,
                PostalCode = address.PostalCode,
                CountryCode = address.CountryCode,
                CountryName = address.CountryName,
                Contact = contact
            };

            List<Piece2> pieceList = new List<Piece2>();
            pieceList.Add(new Piece2()
            {
                PackageType = PackageType1.YP,
                Weight = package.Items.Sum(i => i.Qty.Value * ((decimal)i.Skus.Weight / 1000)),
                PieceContents = package.Items.First(i => i.IsEnable.Equals(true)).Skus.ProductType.ProductTypeName
            });

            shipment.ShipmentDetails = new ShipmentDetails2()
            {
                NumberOfPieces = pieceList.Count().ToString(),
                Pieces = pieceList.ToArray(),
                Weight = pieceList.Sum(p => p.Weight),
                WeightUnit = WeightUnit.K,
                GlobalProductCode = "P",
                LocalProductCode = "P",
                Date = DateTime.Now,
                Contents = string.Join(", ", package.Items.Select(i => i.Skus.ProductType.ProductTypeName).Distinct().ToArray()),
                DoorTo = DoorTo.DD,
                DoorToSpecified = true,
                DimensionUnit = DimensionUnit.C,
                DimensionUnitSpecified = true,
                //InsuredAmount = "0.00",
                PackageType = PackageType1.YP,
                IsDutiable = YesNo1.Y,
                IsDutiableSpecified = true,
                CurrencyCode = Enum.GetName(typeof(QDLogistics.OrderService.CurrencyCodeType2), package.Orders.OrderCurrencyCode.Value)
            };

            shipment.Dutiable = new Dutiable1()
            {
                DeclaredValue = package.DeclaredTotal.ToString(),
                DeclaredCurrency = Enum.GetName(typeof(QDLogistics.OrderService.CurrencyCodeType2), package.Orders.OrderCurrencyCode.Value),
                TermsOfTrade = "DDP"
            };

            shipment.Shipper = new Shipper1()
            {
                ShipperID = api_account,
                CompanyName = "Zhi You Wan LTD",
                AddressLine = new string[] { "No.51, Sec.3 Jianguo N. Rd.,", "South Dist.," },
                City = "Taichung City",
                PostalCode = "403",
                CountryCode = "TW",
                CountryName = "Taiwan",
                Contact = new Contact2() { PersonName = "Huai Wei Ho", PhoneNumber = "0423718118" }
            };

            shipment.SpecialService = new SpecialService1[] { new SpecialService1() {
                SpecialServiceType = "DD",
                ChargeValue = package.DeclaredTotal.ToString(),
                CurrencyCode = Enum.GetName(typeof(QDLogistics.OrderService.CurrencyCodeType2), package.Orders.OrderCurrencyCode.Value)
            } };

            return shipment;
        }

        private Request requsetInit()
        {
            return new Request()
            {
                ServiceHeader = new ServiceHeader()
                {
                    SiteID = api_siteID,
                    Password = api_password,
                    MessageReference = "Esteemed Courier Service of DHL",
                    MessageTime = DateTime.Now
                }
            };
        }

        private Request1 requset1Init()
        {
            return new Request1()
            {
                ServiceHeader = new ServiceHeader1()
                {
                    SiteID = api_siteID,
                    Password = api_password,
                    MessageReference = "Esteemed Courier Service of DHL",
                    MessageTime = DateTime.Now
                }
            };
        }

        private static string SendRequest<T>(T requestType) where T : class
        {
            // Create a request for the URL. 
            WebRequest request = WebRequest.Create("https://xmlpi-ea.dhl.com/XMLShippingServlet");

            // If required by the server, set the credentials.
            // request.Credentials = CredentialCache.DefaultCredentials;

            // Wrap the request stream with a text-based writer
            request.Method = "POST";        // Post method
            request.ContentType = "text/xml";

            var stream = request.GetRequestStream();
            StreamWriter writer = new StreamWriter(stream);

            // Write the XML text into the stream
            var soapWriter = new XmlSerializer(typeof(T));

            //add namespaces and/or prefixes ( e.g " <req:BookPickupRequestEA xmlns:req="http://www.dhl.com"> ... </req:BookPickupRequestEA>"
            var ns = new XmlSerializerNamespaces();
            ns.Add("req", "http://www.dhl.com");
            soapWriter.Serialize(writer, requestType, ns);
            writer.Close();

            // Get the response.
            WebResponse response = request.GetResponse();

            // Display the status.
            Trace.WriteLine(((HttpWebResponse)response).StatusDescription);

            // Get the stream containing content returned by the server.
            Stream dataStream = response.GetResponseStream();

            // Open the stream using a StreamReader for easy access.
            StreamReader reader = new StreamReader(dataStream);

            // Read the content.
            string responseFromServer = reader.ReadToEnd();

            // Display the content.
            Trace.WriteLine(responseFromServer);

            // Clean up the streams and the response.
            reader.Close();
            response.Close();

            return responseFromServer;
        }

        private static BookPickupRequestEA CreateNewBookPickup()
        {
            var bookPickup = new BookPickupRequestEA();
            bookPickup.Pickup = new Pickup()
            {
                PickupDate = DateTime.Now.AddDays(5),
                ReadyByTime = "10:20",
                CloseTime = "17:20"
            };
            bookPickup.PickupContact = new Contact()
            {
                PersonName = "Huai Wei Ho",
                Phone = "0423718118"
            };
            bookPickup.Place = new Place()
            {
                LocationType = PlaceLocationType.B,
                CompanyName = "afadasd",
                Address1 = "adsdas",
                Address2 = "dasdasd",
                Address3 = "dadsadas",
                City = "adasd",
                CountryCode = "GB",
                DivisionName = "dasdasd",
                PackageLocation = "dasdasd",
                PostalCode = "W12 7TQ",
                StateCode = "UK"

            };
            bookPickup.Request = new Request()
            {
                ServiceHeader = new ServiceHeader()
                {
                    SiteID = "ZhiYouWan", //Valid Site ID
                    Password = "za62oJQHAh", //Valid Password 
                    MessageReference = "Esteemed Courier Service of DHL", //Message Reference - used for tracking meesages
                    MessageTime = DateTime.Now
                }
            };
            bookPickup.Requestor = new Requestor()
            {
                AccountNumber = "620907538",  //Valid account number
                AccountType = RequestorAccountType.D,
                CompanyName = "Zhi You Wan LTD",
                RequestorContact = new RequestorContact()
                {
                    PersonName = "Huai Wei Ho",
                    Phone = "0423718118"
                }
            };

            return bookPickup;
        }

        #region FOR_DEBUGGING

        private static string GetTextFromXmlStream(Stream xmlStream)
        {
            StreamReader reader = new StreamReader(xmlStream);
            string ret = reader.ReadToEnd();
            reader.Close();
            return ret;
        }

        private static void SerializeEntity(BookPickupRequestEA bookPickup)
        {
            using (FileStream serializationStream = new FileStream(FILE_PATH, FileMode.Create, FileAccess.Write))
            {

                var soapWriter = new XmlSerializer(typeof(BookPickupRequestEA));
                var ns = new XmlSerializerNamespaces();
                ns.Add("req", "http://www.dhl.com");
                soapWriter.Serialize(serializationStream, bookPickup, ns);

            }
        }

        #endregion 
    }


}
