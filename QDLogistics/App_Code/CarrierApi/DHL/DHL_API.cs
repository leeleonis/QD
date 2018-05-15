﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Serialization;
using QDLogistics.Models;

namespace CarrierApi.DHL_Test
{
    public class DHL_API
    {
        private string api_siteID;
        private string api_password;
        private string api_account;

        private DateTime today = DateTime.Now;

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
            tracking.Request = RequsetInit("6.1");
            tracking.LanguageCode = "tw";
            tracking.Items = items;
            tracking.ItemsElementName = new ItemsChoiceType[] { 0 };
            tracking.LevelOfDetails = LevelOfDetails.ALL_CHECK_POINTS;
            tracking.PiecesEnabled = KnownTrackingRequestPiecesEnabled.S;

            return tracking;
        }

        public ShipmentResponse Create(Packages package)
        {
            ShipmentResponse result;
            ShipmentRequest shipmentRequest = SetShipment(package);

            XmlSerializer serializer = new XmlSerializer(typeof(ShipmentResponse));
            string request = SendRequest(shipmentRequest);
            using (TextReader reader = new StringReader(request))
            {
                try
                {
                    result = (ShipmentResponse)serializer.Deserialize(reader);
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

        private ShipmentRequest SetShipment(Packages package)
        {
            var shipment = new ShipmentRequest();

            shipment.Request = RequsetInit("6.1");
            shipment.LanguageCode = "tw";
            shipment.PiecesEnabled = PiecesEnabled.Y;
            shipment.Reference = new Reference[] { new Reference() { ReferenceID = package.OrderID.ToString() } };
            shipment.LabelImageFormat = LabelImageFormat.PDF;
            shipment.LabelImageFormatSpecified = true;
            shipment.RequestArchiveDoc = YesNo.Y;
            shipment.RequestArchiveDocSpecified = true;
            shipment.Label = new Label() { LabelTemplate = LabelTemplate.Item8X4_A4_PDF };
            shipment.EProcShip = YesNo.N;
            shipment.EProcShipSpecified = true;

            shipment.Billing = new Billing()
            {
                ShipperAccountNumber = api_account,
                ShippingPaymentType = ShipmentPaymentType.S,
                BillingAccountNumber = api_account,
                DutyPaymentType = DutyTaxPaymentType.S,
                DutyPaymentTypeSpecified = true,
                DutyAccountNumber = api_account
            };

            Addresses address = package.Orders.Addresses;
            Contact contact = new Contact()
            {
                PersonName = string.Join(" ", new string[] { address.FirstName, address.MiddleInitial, address.LastName }),
                PhoneNumber = address.PhoneNumber
            };

            shipment.Consignee = new Consignee()
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

            List<Piece> pieceList = new List<Piece>();
            pieceList.Add(new Piece()
            {
                PackageType = PackageType.YP,
                PackageTypeSpecified = true,
                Weight = package.Items.Sum(i => i.Qty.Value * ((decimal)i.Skus.Weight / 1000)),
                PieceContents = package.Items.First(i => i.IsEnable.Equals(true)).Skus.ProductType.ProductTypeName
            });

            string currency = Enum.GetName(typeof(QDLogistics.OrderService.CurrencyCodeType2), package.Orders.OrderCurrencyCode.Value);
            shipment.ShipmentDetails = new ShipmentDetails()
            {
                NumberOfPieces = pieceList.Count().ToString(),
                Pieces = pieceList.ToArray(),
                Weight = pieceList.Sum(p => p.Weight),
                WeightUnit = WeightUnit.K,
                GlobalProductCode = "P",
                LocalProductCode = "P",
                Date = today,
                Contents = string.Join(", ", package.Items.Select(i => i.Skus.ProductType.ProductTypeName).Distinct().ToArray()),
                DoorTo = DoorTo.DD,
                DoorToSpecified = true,
                DimensionUnit = DimensionUnit.C,
                DimensionUnitSpecified = true,
                PackageType = PackageType.YP,
                PackageTypeSpecified = true,
                IsDutiable = YesNo.Y,
                IsDutiableSpecified = true,
                CurrencyCode = currency
            };

            shipment.Dutiable = new Dutiable()
            {
                DeclaredValue = (float)package.DeclaredTotal,
                DeclaredValueSpecified = true,
                DeclaredCurrency = currency,
                TermsOfTrade = TermsOfTrade.DDP,
                TermsOfTradeSpecified = true
            };

            shipment.Shipper = new Shipper()
            {
                ShipperID = api_account,
                CompanyName = "Zhi You Wan LTD",
                AddressLine = new string[] { "No.51, Sec.3 Jianguo N. Rd.,", "South Dist.," },
                City = "Taichung City",
                PostalCode = "403",
                CountryCode = "TW",
                CountryName = "Taiwan",
                Contact = new Contact() { PersonName = "Huai Wei Ho", PhoneNumber = "0423718118" }
            };

            shipment.SpecialService = new SpecialService[] { new SpecialService() {
                //SpecialServiceType = "DD",
                SpecialServiceType = "WY"
            } };

            shipment.DocImages = new DocImage[]
            {
                new DocImage() {
                    Type = Type.INV,
                    Image = File.ReadAllBytes(Path.Combine(System.Web.Hosting.HostingEnvironment.MapPath("~/FileUploads"), "sample", "A4.pdf")),
                    ImageFormat = ImageFormat.PDF
                }
            };

            return shipment;
        }

        public ShipmentResponse CreateBox(Box box, DirectLine directLine)
        {
            ShipmentResponse result;
            ShipmentRequest shipment = new ShipmentRequest();

            shipment.Request = RequsetInit("6.1");
            shipment.NewShipper = YesNo.N;
            shipment.NewShipperSpecified = true;
            shipment.LanguageCode = "tw";
            shipment.PiecesEnabled = PiecesEnabled.Y;
            shipment.Reference = new Reference[] { new Reference() { ReferenceID = box.BoxID } };
            shipment.LabelImageFormat = LabelImageFormat.PDF;
            shipment.LabelImageFormatSpecified = true;
            shipment.RequestArchiveDoc = YesNo.Y;
            shipment.RequestArchiveDocSpecified = true;
            shipment.Label = new Label() { LabelTemplate = LabelTemplate.Item8X4_A4_PDF };

            shipment.Billing = new Billing()
            {
                ShipperAccountNumber = api_account,
                ShippingPaymentType = ShipmentPaymentType.S,
                BillingAccountNumber = api_account,
                DutyPaymentType = DutyTaxPaymentType.S,
                DutyPaymentTypeSpecified = true,
                DutyAccountNumber = api_account
            };

            Contact contact = new Contact()
            {
                PersonName = directLine.ContactName,
                PhoneNumber = directLine.PhoneNumber
            };

            shipment.Consignee = new Consignee()
            {
                CompanyName = !string.IsNullOrEmpty(directLine.CompanyName) ? directLine.CompanyName : contact.PersonName,
                AddressLine = new string[] { directLine.StreetLine1, directLine.StreetLine2 },
                City = directLine.City,
                Division = directLine.StateName,
                PostalCode = directLine.PostalCode,
                CountryCode = directLine.CountryCode,
                CountryName = directLine.CountryName,
                Contact = contact
            };

            List<Items> itemList = box.Packages.Where(p => p.IsEnable.Value).SelectMany(p => p.Items.Where(i => i.IsEnable.Value)).ToList();
            List<Piece> pieceList = new List<Piece>();
            pieceList.Add(new Piece()
            {
                PackageType = PackageType.YP,
                PackageTypeSpecified = true,
                Weight = itemList.Sum(i => i.Qty.Value * ((decimal)i.Skus.Weight / 1000)),
                PieceContents = itemList.First().Skus.ProductType.ProductTypeName
            });

            shipment.ShipmentDetails = new ShipmentDetails()
            {
                NumberOfPieces = pieceList.Count().ToString(),
                Pieces = pieceList.ToArray(),
                Weight = pieceList.Sum(p => p.Weight),
                WeightUnit = WeightUnit.K,
                GlobalProductCode = "P",
                LocalProductCode = "P",
                Date = today,
                Contents = string.Join(", ", itemList.Select(i => i.Skus.ProductType.ProductTypeName).Distinct().ToArray()),
                DoorTo = DoorTo.DD,
                DoorToSpecified = true,
                DimensionUnit = DimensionUnit.C,
                DimensionUnitSpecified = true,
                PackageType = PackageType.YP,
                PackageTypeSpecified = true,
                IsDutiable = YesNo.Y,
                IsDutiableSpecified = true,
                CurrencyCode = "USD"
            };

            shipment.Dutiable = new Dutiable()
            {
                DeclaredValue = (float)box.Packages.Sum(p => p.DeclaredTotal),
                DeclaredValueSpecified = true,
                DeclaredCurrency = shipment.ShipmentDetails.CurrencyCode,
                TermsOfTrade = TermsOfTrade.DDP,
                TermsOfTradeSpecified = true
            };

            shipment.Shipper = new Shipper()
            {
                ShipperID = api_account,
                CompanyName = "Zhi You Wan LTD",
                AddressLine = new string[] { "No.51, Sec.3 Jianguo N. Rd.,", "South Dist.," },
                City = "Taichung City",
                PostalCode = "403",
                CountryCode = "TW",
                CountryName = "Taiwan",
                Contact = new Contact() { PersonName = "Huai Wei Ho", PhoneNumber = "0423718118" }
            };

            shipment.SpecialService = new SpecialService[] { new SpecialService() {
                SpecialServiceType = "DD"
            } };

            XmlSerializer serializer = new XmlSerializer(typeof(ShipmentResponse));
            string request = SendRequest(shipment);
            using (TextReader reader = new StringReader(request))
            {
                try
                {
                    result = (ShipmentResponse)serializer.Deserialize(reader);
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

        public ShipmentResponse UploadInvoice(ShipmentResponse AWBResult, byte[] image)
        {
            ShipmentResponse result;
            ShipmentRequest shipment = new ShipmentRequest()
            {
                Request = new Request()
                {
                    ServiceHeader = new ServiceHeader()
                    {
                        SiteID = api_siteID,
                        Password = api_password,
                        MessageReference = "Esteemed Courier Service of DHL",
                        MessageTime = today
                    }
                },
                RegionCode = AWBResult.RegionCode,
                RegionCodeSpecified = true,
                ShipmentDetails = new ShipmentDetails()
                {
                    GlobalProductCode = "P",
                    LocalProductCode = "P",
                    Date = today,
                },
                Shipper = new Shipper() {
                    OriginServiceAreaCode = AWBResult.Shipper.OriginServiceAreaCode,
                    OriginFacilityCode = AWBResult.Shipper.OriginFacilityCode,
                    CountryCode = AWBResult.Shipper.CountryCode
                },
                Airwaybill = AWBResult.AirwayBillNumber,
                DocImages = new DocImage[]
                {
                    new DocImage()
                    {
                        Type = Type.CIN,
                        Image = image,
                        ImageFormat = ImageFormat.PDF
                    }
                },
                schemaVersion = 1.0m
            };

            XmlSerializer serializer = new XmlSerializer(typeof(ShipmentResponse));
            string request = SendRequest(shipment);
            using (TextReader reader = new StringReader(request))
            {
                try
                {
                    result = (ShipmentResponse)serializer.Deserialize(reader);
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

        private Request RequsetInit(string version)
        {
            return new Request()
            {
                ServiceHeader = new ServiceHeader()
                {
                    SiteID = api_siteID,
                    Password = api_password,
                    MessageReference = "Esteemed Courier Service of DHL",
                    MessageTime = today
                },
                MetaData = new MetaData()
                {
                    SoftwareName = "3PV",
                    SoftwareVersion = version
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

            using (var sww = new StringWriter())
            {
                using (System.Xml.XmlWriter xmlWriter = System.Xml.XmlWriter.Create(sww))
                {
                    soapWriter.Serialize(xmlWriter, requestType);
                    var xml = sww.ToString(); // your xml
                }
            }

            soapWriter = new XmlSerializer(typeof(T));

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
    }
}
