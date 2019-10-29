using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Serialization;
using QDLogistics.Commons;
using QDLogistics.Models;

namespace CarrierApi.DHL
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
            KnownTrackingRequest track = SetTracking(new string[] { trackingNumber });

            XmlSerializer serializer = new XmlSerializer(typeof(TrackingResponse));
            string request = SendRequest(track);
            using (TextReader reader = new StringReader(request))
            {
                try
                {
                    result = (TrackingResponse)serializer.Deserialize(reader);
                }
                catch (Exception e)
                {
                    TextReader errorReader = new StringReader(request);
                    XmlSerializer errorSerializer = new XmlSerializer(typeof(ShipmentTrackingErrorResponse));
                    ShipmentTrackingErrorResponse error = errorSerializer.Deserialize(errorReader) as ShipmentTrackingErrorResponse;
                    errorReader.Dispose();

                    result = new TrackingResponse()
                    {
                        AWBInfo = new AWBInfo[] {
                            new AWBInfo() { Status = new Status() { ActionStatus = string.Join("; ", error.Response.Status.Condition.Select(c => c.ConditionData)) } }
                        }
                    };
                }
            }

            return result;
        }

        private KnownTrackingRequest SetTracking(string[] items)
        {
            var tracking = new KnownTrackingRequest()
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
                LanguageCode = "tw",
                Items = items,
                ItemsElementName = new ItemsChoiceType[] { ItemsChoiceType.AWBNumber },
                LevelOfDetails = LevelOfDetails.ALL_CHECK_POINTS,
                PiecesEnabled = KnownTrackingRequestPiecesEnabled.S,
                PiecesEnabledSpecified = true,
                schemaVersion = 1.0M
            };

            return tracking;
        }

        public ShipmentResponse Create(Packages package)
        {
            ShipmentResponse result;
            ShipmentRequest shipmentRequest = SetShipment(package);

            MyHelp.Log("Packages", package.ID, "執行DHL Create Api");

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
                    string errorMsg;
                    TextReader errorReader = new StringReader(request);
                    XmlSerializer errorSerializer = new XmlSerializer(typeof(ShipmentValidateErrorResponse));
                    try
                    {
                        ShipmentValidateErrorResponse error = errorSerializer.Deserialize(errorReader) as ShipmentValidateErrorResponse;
                        errorMsg = string.Join("; ", error.Response.Status.Condition.Select(c => c.ConditionData));
                    }
                    catch (Exception)
                    {
                        errorSerializer = new XmlSerializer(typeof(OtherErrorResponse));
                        OtherErrorResponse error = errorSerializer.Deserialize(new StringReader(request)) as OtherErrorResponse;
                        errorMsg = string.Join("; ", error.Response.Status.Condition.Select(c => c.ConditionData));
                    }
                    errorReader.Dispose();

                    MyHelp.Log("Packages", package.ID, string.Format("執行DHL Create Api失敗 - {0}", errorMsg));
                    throw new Exception(errorMsg);
                }
            }

            return result;
        }

        private ShipmentRequest SetShipment(Packages package)
        {
            MyHelp.Log("Packages", package.ID, "設定Create Data");

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

            decimal weight = shipment.Shipper.CountryCode.Equals("US") ? 453 : 1000;
            WeightUnit weightUnit = shipment.Shipper.CountryCode.Equals("US") ? WeightUnit.L : WeightUnit.K;

            Dictionary<string, StockKeepingUnit.SkuData> SkuData;
            using (StockKeepingUnit stock = new StockKeepingUnit())
            {
                var IDs = package.Items.Where(i => i.IsEnable.Value).Select(i => i.ProductID).Distinct().ToArray();
                SkuData = stock.GetSkuData(IDs);
            }

            List<Piece> pieceList = new List<Piece>();
            pieceList.Add(new Piece()
            {
                PackageType = PackageType.YP,
                PackageTypeSpecified = true,
                Weight = package.Items.Sum(i => i.Qty.Value * ((decimal)(SkuData[i.ProductID]?.Weight ?? i.Skus.ShippingWeight) / weight)),
                WeightSpecified = true,
                PieceContents = package.Items.First(i => i.IsEnable.Equals(true)).Skus.ProductType.ProductTypeName
                });

            string currency = Enum.GetName(typeof(QDLogistics.OrderService.CurrencyCodeType2), package.Orders.OrderCurrencyCode.Value);
            shipment.ShipmentDetails = new ShipmentDetails()
            {
                NumberOfPieces = pieceList.Count().ToString(),
                Pieces = pieceList.ToArray(),
                Weight = pieceList.Sum(p => p.Weight),
                WeightUnit = weightUnit,
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
                DeclaredValue = package.DeclaredTotal,
                DeclaredValueSpecified = true,
                DeclaredCurrency = currency,
                TermsOfTrade = TermsOfTrade.DDP,
                TermsOfTradeSpecified = true
            };

            shipment.SpecialService = new SpecialService[] { new SpecialService() {
                //SpecialServiceType = "DD",
                SpecialServiceType = "WY"
            } };

            int lineNo = 1;
            shipment.UseDHLInvoice = YesNo.Y;
            shipment.DHLInvoiceLanguageCode = InvLanguageCode.en;
            shipment.DHLInvoiceType = InvoiceType.CMI;
            shipment.ExportDeclaration = new ExportDeclaration()
            {
                SignatureName = "Demi Tian",
                InvoiceNumber = package.OrderID.ToString(),
                InvoiceDate = today,
                BillToCompanyName = shipment.Shipper.CompanyName,
                BillToContanctName = shipment.Shipper.Contact.PersonName,
                BillToAddressLine = shipment.Shipper.AddressLine,
                BillToCity = shipment.Shipper.City,
                BillToPostcode = shipment.Shipper.PostalCode,
                BillToCountryName = shipment.Shipper.CountryName,
                BillToPhoneNumber = shipment.Shipper.Contact.PhoneNumber,
                ExportLineItem = package.Items.Where(i => i.IsEnable.Value).Select(i => new ExportLineItem()
                {
                    LineNumber = (lineNo++).ToString(),
                    Quantity = i.Qty.ToString(),
                    QuantityUnit = QuantityUnit.PCS,
                    Description = i.Skus.ProductType.ProductTypeName,
                    Value = (float)i.DeclaredValue,
                    Weight = new ExportLineItemWeight() { Weight = (decimal)(SkuData[i.ProductID]?.Weight ?? i.Skus.ShippingWeight) / weight, WeightUnit = weightUnit },
                    GrossWeight = new ExportLineItemGrossWeight() { Weight = (decimal)(SkuData[i.ProductID]?.Weight ?? i.Skus.ShippingWeight) / weight, WeightSpecified = true, WeightUnit = weightUnit, WeightUnitSpecified = true },
                    ManufactureCountryCode = i.Skus.Origin
                }).ToArray()
            };

            MyHelp.Log("Packages", package.ID, "設定Create Data完成");

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

            decimal weight = shipment.Shipper.CountryCode.Equals("US") ? 453 : 1000;
            WeightUnit weightUnit = shipment.Shipper.CountryCode.Equals("US") ? WeightUnit.L : WeightUnit.K;

            List<Items> itemList = box.Packages.Where(p => p.IsEnable.Value).SelectMany(p => p.Items.Where(i => i.IsEnable.Value)).ToList();

            Dictionary<string, StockKeepingUnit.SkuData> SkuData;
            using (StockKeepingUnit stock = new StockKeepingUnit())
            {
                var IDs = itemList.Where(i => i.IsEnable.Value).Select(i => i.ProductID).Distinct().ToArray();
                SkuData = stock.GetSkuData(IDs);
            }

            List<Piece> pieceList = new List<Piece>();
            pieceList.Add(new Piece()
            {
                PackageType = PackageType.YP,
                PackageTypeSpecified = true,
                Weight = itemList.Sum(i => i.Qty.Value * ((decimal)(SkuData[i.ProductID]?.Weight ?? i.Skus.ShippingWeight) / weight)),
                WeightSpecified = true,
                PieceContents = itemList.First().Skus.ProductType.ProductTypeName
            });

            shipment.ShipmentDetails = new ShipmentDetails()
            {
                NumberOfPieces = pieceList.Count().ToString(),
                Pieces = pieceList.ToArray(),
                Weight = pieceList.Sum(p => p.Weight),
                WeightUnit = weightUnit,
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
                DeclaredValue = box.Packages.Sum(p => p.DeclaredTotal),
                DeclaredValueSpecified = true,
                DeclaredCurrency = shipment.ShipmentDetails.CurrencyCode,
                TermsOfTrade = TermsOfTrade.DDP,
                TermsOfTradeSpecified = true
            };

            shipment.SpecialService = new SpecialService[] { new SpecialService() {
                //SpecialServiceType = "DD",
                SpecialServiceType = "WY"
            } };

            int lineNo = 1;
            shipment.UseDHLInvoice = YesNo.Y;
            shipment.DHLInvoiceLanguageCode = InvLanguageCode.en;
            shipment.DHLInvoiceType = InvoiceType.CMI;
            shipment.ExportDeclaration = new ExportDeclaration()
            {
                SignatureName = "Demi Tian",
                InvoiceNumber = box.BoxID,
                InvoiceDate = today,
                BillToCompanyName = shipment.Shipper.CompanyName,
                BillToContanctName = shipment.Shipper.Contact.PersonName,
                BillToAddressLine = shipment.Shipper.AddressLine,
                BillToCity = shipment.Shipper.City,
                BillToPostcode = shipment.Shipper.PostalCode,
                BillToCountryName = shipment.Shipper.CountryName,
                BillToPhoneNumber = shipment.Shipper.Contact.PhoneNumber,
                ExportLineItem = itemList.Select(i => new ExportLineItem()
                {
                    LineNumber = (lineNo++).ToString(),
                    Quantity = i.Qty.ToString(),
                    QuantityUnit = QuantityUnit.PCS,
                    Description = i.Skus.ProductType.ProductTypeName + " - " + i.Skus.ProductName,
                    Value = (float)i.UnitPrice.Value,
                    Weight = new ExportLineItemWeight() { Weight = (decimal)(SkuData[i.ProductID]?.Weight ?? i.Skus.ShippingWeight) / weight, WeightUnit = weightUnit },
                    GrossWeight = new ExportLineItemGrossWeight() { Weight = (decimal)(SkuData[i.ProductID]?.Weight ?? i.Skus.ShippingWeight) / weight, WeightSpecified = true, WeightUnit = weightUnit, WeightUnitSpecified = true },
                    ManufactureCountryCode = i.Skus.Origin
                }).ToArray()
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
                Shipper = new Shipper()
                {
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
                    string errorMsg;
                    TextReader errorReader = new StringReader(request);
                    XmlSerializer errorSerializer = new XmlSerializer(typeof(ShipmentValidateErrorResponse));
                    try
                    {
                        ShipmentValidateErrorResponse error = errorSerializer.Deserialize(errorReader) as ShipmentValidateErrorResponse;
                        errorMsg = string.Join("; ", error.Response.Status.Condition.Select(c => c.ConditionData));
                    }
                    catch (Exception)
                    {
                        errorSerializer = new XmlSerializer(typeof(OtherErrorResponse));
                        OtherErrorResponse error = errorSerializer.Deserialize(errorReader) as OtherErrorResponse;
                        errorMsg = string.Join("; ", error.Response.Status.Condition.Select(c => c.ConditionData));

                    }
                    errorReader.Dispose();
                    throw new Exception(errorMsg);
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
            try
            {
                // Create a request for the URL. 
                WebRequest request = WebRequest.Create("https://xmlpi-ea.dhl.com/XMLShippingServlet");
                //request.Timeout = 60000;

                // If required by the server, set the credentials.
                // request.Credentials = CredentialCache.DefaultCredentials;

                // Wrap the request stream with a text-based writer
                request.Method = "POST";        // Post method
                request.ContentType = "text/xml";

                var stream = request.GetRequestStream();
                StreamWriter writer = new StreamWriter(stream);

                // Write the XML text into the stream
                //var soapWriter = new XmlSerializer(typeof(T));

                //using (var sww = new StringWriter())
                //{
                //    using (System.Xml.XmlWriter xmlWriter = System.Xml.XmlWriter.Create(sww))
                //    {
                //        soapWriter.Serialize(xmlWriter, requestType);
                //        var xml = sww.ToString(); // your xml
                //    }
                //}

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
            catch (Exception e)
            {
                string errorMsg = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                MyHelp.Log("", null, string.Format("SendRequest Error - {0}", errorMsg));
                throw new Exception(errorMsg);
            }
        }
    }
}
