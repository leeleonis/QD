using QDLogistics.Commons;
using QDLogistics.FedExShipService;
using QDLogistics.FedExTrackService;
using QDLogistics.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Web;
using System.Web.Hosting;
using System.Xml.Linq;

namespace CarrierApi.FedEx
{
    public class FedEx_API
    {
        private string api_key;
        private string api_password;
        private string api_accountNumber;
        private string api_meterNumber;

        public string endpoint;

        public FedEx_API(CarrierAPI Api)
        {
            api_key = Api.ApiKey;
            api_password = Api.ApiPassword;
            api_accountNumber = Api.ApiAccount;
            api_meterNumber = Api.ApiMeter;
        }

        public ProcessShipmentReply Create(Packages package)
        {
            ProcessShipmentRequest request = _shipmentInit();

            request.TransactionDetail = new QDLogistics.FedExShipService.TransactionDetail();
            request.TransactionDetail.CustomerTransactionId = "*** Process Shipment Request ***";

            request.RequestedShipment = new RequestedShipment()
            {
                ShipTimestamp = DateTime.Today,
                DropoffType = DropoffType.REGULAR_PICKUP,
                ServiceType = (QDLogistics.FedExShipService.ServiceType)package.Method.MethodType,
                PackagingType = (QDLogistics.FedExShipService.PackagingType)package.Method.BoxType,
                Shipper = _shipperInit(),
                ShippingChargesPayment = new Payment() { PaymentType = PaymentType.SENDER, Payor = new Payor() { ResponsibleParty = _shipperInit() } },
                PackageCount = "1"
            };

            Addresses address = package.Orders.Addresses;
            request.RequestedShipment.Recipient = new Party()
            {
                Contact = new QDLogistics.FedExShipService.Contact()
                {
                    PersonName = string.Join(" ", new string[] { address.FirstName, address.MiddleInitial, address.LastName }),
                    CompanyName = address.CompanyName,
                    PhoneNumber = address.PhoneNumber
                },
                Address = new QDLogistics.FedExShipService.Address()
                {
                    StreetLines = new string[] { address.StreetLine1, address.StreetLine2 },
                    City = address.City,
                    StateOrProvinceCode = address.StateName,
                    PostalCode = address.PostalCode,
                    CountryName = address.CountryName,
                    CountryCode = address.CountryCode
                }
            };

            List<StockKeepingUnit.SkuData> SkuData = new List<StockKeepingUnit.SkuData>();
            using (StockKeepingUnit stock = new StockKeepingUnit())
            {
                var IDs = package.Items.Where(i => i.IsEnable.Value).Select(i => i.ProductID).ToArray();
                SkuData = stock.GetSkuData(IDs);
            }

            string currency = Enum.GetName(typeof(QDLogistics.OrderService.CurrencyCodeType2), package.Orders.OrderCurrencyCode.Value);
            QDLogistics.FedExShipService.Money customsValue = new QDLogistics.FedExShipService.Money() { Currency = currency, Amount = package.DeclaredTotal };
            QDLogistics.FedExShipService.Commodity commodity = new QDLogistics.FedExShipService.Commodity
            {
                NumberOfPieces = "1",
                Description = string.Join(", ", package.Items.Select(i => i.Skus.ProductType.ProductTypeName).Distinct().ToArray()),
                CountryOfManufacture = "CN",
                Weight = new QDLogistics.FedExShipService.Weight()
                {
                    Units = request.RequestedShipment.Shipper.Address.CountryCode.Equals("US") ? QDLogistics.FedExShipService.WeightUnits.LB : QDLogistics.FedExShipService.WeightUnits.KG,
                    Value = package.Items.Where(i => i.IsEnable.Equals(true)).Sum(i => i.Qty.Value * ((decimal)(SkuData.Any(s => s.Sku.Equals(i.ProductID)) ? SkuData.First(s => s.Sku.Equals(i.ProductID)).Weight : i.Skus.ShippingWeight) / (request.RequestedShipment.Shipper.Address.CountryCode.Equals("US") ? 453 : 1000)))
                },
                Quantity = 1,
                QuantityUnits = "EA",
                UnitPrice = customsValue,
                CustomsValue = customsValue,
                QuantitySpecified = true
            };

            request.RequestedShipment.CustomsClearanceDetail = new CustomsClearanceDetail()
            {
                DutiesPayment = new Payment() { PaymentType = PaymentType.RECIPIENT },
                DocumentContent = InternationalDocumentContentType.DOCUMENTS_ONLY,
                CustomsValue = customsValue,
                Commodities = new QDLogistics.FedExShipService.Commodity[] { commodity },
                DocumentContentSpecified = true
            };

            request.RequestedShipment.RequestedPackageLineItems = new RequestedPackageLineItem[] {
                new RequestedPackageLineItem()
                {
                    SequenceNumber = "1",
                    InsuredValue = new QDLogistics.FedExShipService.Money() { Amount = 0, Currency = currency },
                    Weight = commodity.Weight,
                    CustomerReferences = new CustomerReference[]
                    {
                        new CustomerReference()
                        {
                            CustomerReferenceType = CustomerReferenceType.CUSTOMER_REFERENCE,
                            Value = package.OrderID.ToString()
                        }
                    }
                }
            };

            request.RequestedShipment.LabelSpecification = new LabelSpecification()
            {
                LabelFormatType = LabelFormatType.COMMON2D,
                ImageType = ShippingDocumentImageType.ZPLII,
                LabelStockType = LabelStockType.STOCK_4X6,
                LabelPrintingOrientation = LabelPrintingOrientationType.BOTTOM_EDGE_OF_TEXT_FIRST,
                ImageTypeSpecified = true,
                LabelStockTypeSpecified = true,
                LabelPrintingOrientationSpecified = true
            };

            ProcessShipmentReply reply;
            using (ShipPortTypeClient client = new ShipPortTypeClient())
            {
                var endpoint = client.Endpoint;
                ConsoleOutputBehavior consoleOutputBehavior = new ConsoleOutputBehavior();
                client.Endpoint.Behaviors.Add(consoleOutputBehavior);

                try
                {
                    reply = client.processShipment(request);
                }
                catch (Exception e)
                {
                    QDLogistics.FedExShipService.Notification notification = new QDLogistics.FedExShipService.Notification();

                    if (!string.IsNullOrEmpty(consoleOutputBehavior.ConsoleOutputInspector.ResponseXML))
                    {
                        XElement element = XElement.Parse(consoleOutputBehavior.ConsoleOutputInspector.ResponseXML);
                        notification.Message = element.Attributes("Message").Any() ? element.Attributes("Message").First().Value : element.Attributes("Desc").First().Value;
                    }
                    else
                    {
                        notification.Message = e.Message;
                    }

                    reply = new ProcessShipmentReply() { Notifications = new QDLogistics.FedExShipService.Notification[] { notification } };
                }
            }

            return reply;
        }

        public ProcessShipmentReply CreateBox(List<Box> boxList, ShippingMethod method, DirectLine directLine)
        {
            ProcessShipmentRequest request = _shipmentInit();

            request.TransactionDetail = new QDLogistics.FedExShipService.TransactionDetail();
            request.TransactionDetail.CustomerTransactionId = "*** Process Shipment Request ***";

            request.RequestedShipment = new RequestedShipment()
            {
                ShipTimestamp = DateTime.Today,
                DropoffType = DropoffType.REGULAR_PICKUP,
                ServiceType = (QDLogistics.FedExShipService.ServiceType)method.MethodType,
                PackagingType = (QDLogistics.FedExShipService.PackagingType)method.BoxType,
                Shipper = _shipperInit(),
                ShippingChargesPayment = new Payment() { PaymentType = PaymentType.SENDER, Payor = new Payor() { ResponsibleParty = _shipperInit() } },
                PackageCount = boxList.Count().ToString()
            };

            request.RequestedShipment.Recipient = new Party()
            {
                Contact = new QDLogistics.FedExShipService.Contact()
                {
                    PersonName = directLine.ContactName,
                    CompanyName = directLine.CompanyName,
                    PhoneNumber = directLine.PhoneNumber
                },
                Address = new QDLogistics.FedExShipService.Address()
                {
                    StreetLines = new string[] { directLine.StreetLine1, directLine.StreetLine2 },
                    City = directLine.City,
                    StateOrProvinceCode = directLine.StateName,
                    PostalCode = directLine.PostalCode,
                    CountryName = directLine.CountryName,
                    CountryCode = directLine.CountryCode
                }
            };

            int NumberOfPieces = 1;
            string[] IDS = new string[] { "IDS", "IDS (US)" };
            string currency = IDS.Contains(directLine.Abbreviation) ? "USD" : Enum.GetName(typeof(QDLogistics.OrderService.CurrencyCodeType2), boxList[0].Packages.First(p => p.IsEnable.Value).Orders.OrderCurrencyCode.Value);
            //string currency = Enum.GetName(typeof(QDLogistics.OrderService.CurrencyCodeType2), box.Packages.First(p => p.IsEnable.Value).Orders.OrderCurrencyCode.Value);
            var commodityList = new List<QDLogistics.FedExShipService.Commodity>();
            var itemLineList = new List<RequestedPackageLineItem>();
            foreach (Box box in boxList)
            {
                List<Items> itemList = box.Packages.Where(p => p.IsEnable.Value).SelectMany(p => p.Items.Where(i => i.IsEnable.Value)).ToList();

                List<StockKeepingUnit.SkuData> SkuData = new List<StockKeepingUnit.SkuData>();
                using (StockKeepingUnit stock = new StockKeepingUnit())
                {
                    var IDs = itemList.Where(i => i.IsEnable.Value).Select(i => i.ProductID).ToArray();
                    SkuData = stock.GetSkuData(IDs);
                }

                QDLogistics.FedExShipService.Money customsValue = new QDLogistics.FedExShipService.Money() { Currency = currency, Amount = box.Packages.Where(p => p.IsEnable.Value).Sum(p => p.DeclaredTotal) };
                QDLogistics.FedExShipService.Commodity commodity = new QDLogistics.FedExShipService.Commodity
                {
                    NumberOfPieces = boxList.Count().ToString(),
                    Description = string.Join(", ", itemList.Select(i => i.Skus.ProductType.ProductTypeName).Distinct().ToArray()),
                    CountryOfManufacture = "CN",
                    Weight = new QDLogistics.FedExShipService.Weight()
                    {
                        Units = request.RequestedShipment.Shipper.Address.CountryCode.Equals("US") ? QDLogistics.FedExShipService.WeightUnits.LB : QDLogistics.FedExShipService.WeightUnits.KG,
                        Value = itemList.Sum(i => i.Qty.Value * ((decimal)(SkuData.Any(s => s.Sku.Equals(i.ProductID)) ? SkuData.First(s => s.Sku.Equals(i.ProductID)).Weight : i.Skus.ShippingWeight) / (request.RequestedShipment.Shipper.Address.CountryCode.Equals("US") ? 453 : 1000)))
                    },
                    Quantity = 1,
                    QuantityUnits = "EA",
                    UnitPrice = customsValue,
                    CustomsValue = customsValue,
                    QuantitySpecified = true
                };

                commodityList.Add(commodity);
                itemLineList.Add(new RequestedPackageLineItem()
                {
                    SequenceNumber = NumberOfPieces++.ToString(),
                    InsuredValue = new QDLogistics.FedExShipService.Money() { Amount = 0, Currency = currency },
                    Weight = commodity.Weight,
                    CustomerReferences = new CustomerReference[]
                    {
                        new CustomerReference()
                        {
                            CustomerReferenceType = CustomerReferenceType.CUSTOMER_REFERENCE,
                            Value = box.BoxID
                        }
                    }
                });
            }

            request.RequestedShipment.TotalWeight = new QDLogistics.FedExShipService.Weight()
            {
                Units = request.RequestedShipment.Shipper.Address.CountryCode.Equals("US") ? QDLogistics.FedExShipService.WeightUnits.LB : QDLogistics.FedExShipService.WeightUnits.KG,
                Value = commodityList.Select(c => c.Weight).Sum(w => w.Value)
            };

            request.RequestedShipment.CustomsClearanceDetail = new CustomsClearanceDetail()
            {
                DutiesPayment = new Payment() { PaymentType = PaymentType.SENDER, Payor = new Payor() { ResponsibleParty = _shipperInit() } },
                DocumentContent = InternationalDocumentContentType.DOCUMENTS_ONLY,
                Commodities = new QDLogistics.FedExShipService.Commodity[1],
                DocumentContentSpecified = true
            };

            request.RequestedShipment.RequestedPackageLineItems = new RequestedPackageLineItem[1];

            request.RequestedShipment.LabelSpecification = new LabelSpecification()
            {
                LabelOrder = LabelOrderType.SHIPPING_LABEL_FIRST,
                LabelFormatType = LabelFormatType.COMMON2D,
                ImageType = ShippingDocumentImageType.ZPLII,
                LabelStockType = LabelStockType.STOCK_4X6,
                LabelPrintingOrientation = LabelPrintingOrientationType.BOTTOM_EDGE_OF_TEXT_FIRST,
                LabelOrderSpecified = true,
                ImageTypeSpecified = true,
                LabelStockTypeSpecified = true,
                LabelPrintingOrientationSpecified = true
            };

            ProcessShipmentReply reply = new ProcessShipmentReply();
            using (ShipPortTypeClient client = new ShipPortTypeClient())
            {
                var endpoint = client.Endpoint;
                ConsoleOutputBehavior consoleOutputBehavior = new ConsoleOutputBehavior();
                client.Endpoint.Behaviors.Add(consoleOutputBehavior);

                try
                {
                    var basePath = HostingEnvironment.MapPath("~/FileUploads");
                    var filePath = Path.Combine(basePath, "export", "box", boxList[0].Create_at.ToString("yyyy/MM/dd"), boxList[0].MainBox);
                    if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);

                    for (int i = 0; i < itemLineList.Count(); i++)
                    {
                        if (!i.Equals(0))
                        {
                            request.RequestedShipment.TotalWeight = null;
                            request.RequestedShipment.MasterTrackingId = reply.CompletedShipmentDetail.MasterTrackingId;
                        }
                        request.RequestedShipment.CustomsClearanceDetail.CustomsValue = commodityList[i].CustomsValue;
                        request.RequestedShipment.CustomsClearanceDetail.Commodities[0] = commodityList[i];
                        request.RequestedShipment.RequestedPackageLineItems[0] = itemLineList[i];

                        reply = client.processShipment(request);
                        if (reply.HighestSeverity.Equals(QDLogistics.FedExShipService.NotificationSeverityType.ERROR) || reply.HighestSeverity.Equals(QDLogistics.FedExShipService.NotificationSeverityType.FAILURE))
                        {
                            throw new Exception(string.Join("\n", reply.Notifications.Select(n => n.Message).ToArray()));
                        }

                        boxList[i].TrackingNumber = reply.CompletedShipmentDetail.CompletedPackageDetails.First().TrackingIds.Select(t => t.TrackingNumber).First();

                        var content = reply.CompletedShipmentDetail.CompletedPackageDetails.First().Label.Parts.First().Image;
                        System.Net.HttpWebRequest webRequest = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("http://api.labelary.com/v1/printers/8dpmm/labels/4x6/");
                        webRequest.Method = "POST";
                        webRequest.Accept = "application/pdf";
                        webRequest.ContentType = "application/x-www-form-urlencoded";
                        webRequest.ContentLength = content.Length;

                        using (Stream requestStream = webRequest.GetRequestStream())
                        {
                            requestStream.Write(content, 0, content.Length);

                            System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)webRequest.GetResponse();
                            using (Stream responseStream = response.GetResponseStream())
                            {
                                using (FileStream fileStream = File.Create(Path.Combine(filePath, boxList[i].BoxID + ".pdf")))
                                {
                                    responseStream.CopyTo(fileStream);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    QDLogistics.FedExShipService.Notification notification = new QDLogistics.FedExShipService.Notification();

                    if (!string.IsNullOrEmpty(consoleOutputBehavior.ConsoleOutputInspector.ResponseXML))
                    {
                        XElement element = XElement.Parse(consoleOutputBehavior.ConsoleOutputInspector.ResponseXML);
                        notification.Message = element.Attributes("Message").Any() ? element.Attributes("Message").First().Value : element.Attributes("Desc").First().Value;
                    }
                    else
                    {
                        notification.Message = e.Message;
                    }

                    reply = new ProcessShipmentReply() { Notifications = new QDLogistics.FedExShipService.Notification[] { notification } };
                }
            }

            return reply;
        }

        private ProcessShipmentRequest _shipmentInit()
        {
            ProcessShipmentRequest request = new ProcessShipmentRequest();

            request.WebAuthenticationDetail = new QDLogistics.FedExShipService.WebAuthenticationDetail();
            request.WebAuthenticationDetail.UserCredential = new QDLogistics.FedExShipService.WebAuthenticationCredential();
            request.WebAuthenticationDetail.UserCredential.Key = api_key;
            request.WebAuthenticationDetail.UserCredential.Password = api_password;

            request.ClientDetail = new QDLogistics.FedExShipService.ClientDetail();
            request.ClientDetail.AccountNumber = api_accountNumber;
            request.ClientDetail.MeterNumber = api_meterNumber;

            request.Version = new QDLogistics.FedExShipService.VersionId();

            return request;
        }

        private Party _shipperInit()
        {
            Party shipper = new Party()
            {
                AccountNumber = api_accountNumber,
                Contact = new QDLogistics.FedExShipService.Contact()
                {
                    PersonName = "Demi Tian",
                    CompanyName = "Zhi You Wan LTD",
                    PhoneNumber = "0423718118",
                },
                Address = new QDLogistics.FedExShipService.Address()
                {
                    StreetLines = new string[] { "No.51, Sec.3 Jianguo N. Rd.,", "South Dist.," },
                    City = "Taichung City",
                    PostalCode = "403",
                    CountryName = "Taiwan",
                    CountryCode = "TW"
                }
            };

            return shipper;
        }

        public TrackReply Tracking(string trackingNumber)
        {
            TrackRequest request = _trackingInit();

            request.TransactionDetail = new QDLogistics.FedExTrackService.TransactionDetail();
            request.TransactionDetail.CustomerTransactionId = "*** Track Request ***";

            request.Version = new QDLogistics.FedExTrackService.VersionId();

            request.SelectionDetails = new TrackSelectionDetail[1] { new TrackSelectionDetail() };
            request.SelectionDetails[0].PackageIdentifier = new TrackPackageIdentifier();
            request.SelectionDetails[0].PackageIdentifier.Value = trackingNumber;
            request.SelectionDetails[0].PackageIdentifier.Type = TrackIdentifierType.TRACKING_NUMBER_OR_DOORTAG;

            TrackPortTypeClient client = new TrackPortTypeClient();
            TrackReply reply = client.track(request);

            return reply;
        }

        private TrackRequest _trackingInit()
        {
            TrackRequest request = new TrackRequest();

            request.WebAuthenticationDetail = new QDLogistics.FedExTrackService.WebAuthenticationDetail();
            request.WebAuthenticationDetail.UserCredential = new QDLogistics.FedExTrackService.WebAuthenticationCredential();
            request.WebAuthenticationDetail.UserCredential.Key = api_key;
            request.WebAuthenticationDetail.UserCredential.Password = api_password;

            request.ClientDetail = new QDLogistics.FedExTrackService.ClientDetail();
            request.ClientDetail.AccountNumber = api_accountNumber;
            request.ClientDetail.MeterNumber = api_meterNumber;

            request.ProcessingOptions = new TrackRequestProcessingOptionType[1];
            request.ProcessingOptions[0] = TrackRequestProcessingOptionType.INCLUDE_DETAILED_SCANS;

            return request;
        }
    }

    public class ConsoleOutputBehavior : IEndpointBehavior
    {
        public ConsoleOutputInspector ConsoleOutputInspector { get; private set; }
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            ConsoleOutputInspector = new ConsoleOutputInspector();
            clientRuntime.MessageInspectors.Add(ConsoleOutputInspector);
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            throw new Exception("Behavior not supported on the server side!");
        }

        public void Validate(ServiceEndpoint endpoint)
        {
        }
    }

    public class ConsoleOutputInspector : IClientMessageInspector
    {
        public string ResponseXML = string.Empty;

        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
            ResponseXML = reply.ToString();
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            return null;
        }
    }
}