using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Web.Hosting;
using CarrierApi.DHL;
using CarrierApi.FedEx;
using CarrierApi.Sendle;
using CarrierApi.Winit;
using DirectLineApi.IDS;
using GemBox.Spreadsheet;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using QDLogistics.FedExShipService;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;
using QDLogistics.OrderService;
using QDLogistics.PurchaseOrderService;
using SellerCloud_WebService;

namespace QDLogistics.Commons
{
    public class ShipProcess
    {
        private QDLogisticsEntities db;

        private Orders order;
        private Packages package;
        private SC_WebService SCWS;

        private Warehouses warehouse;

        public bool isSplitShip;
        public bool isDropShip;
        public bool isDirectLine;

        private readonly string basePath = HostingEnvironment.MapPath("~/FileUploads");
        private string filePath;

        public ShipProcess(SC_WebService SCWS)
        {
            this.db = db = new QDLogisticsEntities();
            this.SCWS = SCWS;
        }

        public void Init(Packages package)
        {
            this.order = package.Orders;
            this.package = package;
            this.isSplitShip = order.Packages.Count(p => p.IsEnable.Value) >= 2;
            this.warehouse = package.Items.First(i => i.IsEnable.Value).ShipWarehouses;
            this.isDropShip = warehouse.WarehouseType.Equals((int)OrderService.WarehouseTypeType.DropShip);
            this.isDirectLine = package.Method.IsDirectLine;
        }

        public ShipResult Dispatch()
        {
            ShipResult result = new ShipResult(false);

            package.ShipDate = SCWS.SyncOn;
            package.FilePath = Path.Combine("export", package.ShipDate.Value.ToString("yyyy/MM/dd"), package.ID.ToString());
            filePath = Path.Combine(basePath, package.FilePath);
            if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);

            if (isDirectLine)
            {
                result = DirectLine();

                if (!result.Status) return result;
            }

            if (isDropShip)
            {
                result = new ShipResult(true); //DropShip();

                if (!result.Status) return result;
            }

            switch (warehouse.Name)
            {
                case "TWN":
                    result = TWN_Carrier();
                    break;

                case "CRD":
                    result = CRD_Carrier();
                    break;

                case "MWS":
                    result = MWS_Carrier();
                    break;

                case "4PX":
                    result = _4PX_Carrier();
                    break;

                case "Winit US WC":
                case "Winit AU":
                    result = Winit_Carrier();
                    break;

                case "USA":
                    break;

                case "Amazon.com":
                case "Amazon.co.jp":
                case "Amazon.co.uk":
                    break;
            }

            return result;
        }

        public ShipResult Dispatch(List<Box> boxList)
        {
            ShipResult result = new ShipResult(true);

            ShippingMethod method = db.ShippingMethod.Find(boxList.First().FirstMileMethod);
            DirectLine directLine = db.DirectLine.Find(boxList.First().DirectLine);
            CarrierAPI api = method.Carriers.CarrierAPI;

            string currency = directLine.Abbreviation.Equals("IDS") ? "USD" : Enum.GetName(typeof(OrderService.CurrencyCodeType2), boxList[0].Packages.First(p => p.IsEnable.Value).Orders.OrderCurrencyCode.Value);

            string basePath, filePath;
            try
            {
                switch (api.Type)
                {
                    //case (int)EnumData.CarrierType.DHL:
                    //    DHL_API DHL = new DHL_API(api);
                    //    ShipmentResponse boxResult = DHL.CreateBox(box, directLine);
                    //    box.TrackingNumber = boxResult.AirwayBillNumber;

                    //    basePath = HostingEnvironment.MapPath("~/FileUploads");
                    //    filePath = Path.Combine(basePath, "export", "box", box.Create_at.ToString("yyyy/MM/dd"), box.BoxID);
                    //    if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);

                    //    /***** Air Waybill *****/
                    //    File.WriteAllBytes(Path.Combine(filePath, "AirWaybill.pdf"), Crop(boxResult.LabelImage.First().OutputImage, 97f, 30f, 356f, 553f));

                    //    /***** Commercial Invoice *****/
                    //    File.WriteAllBytes(Path.Combine(filePath, "Invoice.pdf"), boxResult.LabelImage.First().MultiLabels.First().DocImageVal);
                    //    //Box_CreateInvoice(box, directLine, basePath, filePath);
                    //    break;
                    case (int)EnumData.CarrierType.FedEx:
                        FedEx_API FedEx = new FedEx_API(api);
                        ProcessShipmentReply fedexResult = FedEx.CreateBox(boxList, method, directLine);

                        if (!fedexResult.HighestSeverity.Equals(NotificationSeverityType.SUCCESS) && !fedexResult.HighestSeverity.Equals(NotificationSeverityType.NOTE))
                        {
                            throw new Exception(string.Join("\n", fedexResult.Notifications.Select(n => n.Message).ToArray()));
                        }

                        basePath = HostingEnvironment.MapPath("~/FileUploads");
                        filePath = Path.Combine(basePath, "export", "box", boxList[0].Create_at.ToString("yyyy/MM/dd"), boxList[0].MainBox);
                        if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);

                        /***** Air Waybill *****/
                        Download_FedEx_PDF(boxList, filePath, "AirWaybill.pdf");

                        /***** Commercial Invoice *****/
                        Box_CreateInvoice(boxList, directLine, basePath, filePath, currency);

                        /***** Recognizance Book *****/
                        var CheckList = new { fileName = "CheckList-{0}.xlsx", samplePath = Path.Combine(basePath, "sample", "Fedex_CheckList.xlsx") };
                        using (FileStream fsIn = new FileStream(CheckList.samplePath, FileMode.Open))
                        {
                            XSSFWorkbook workbook = new XSSFWorkbook(fsIn);
                            fsIn.Close();

                            ISheet sheet = workbook.GetSheetAt(0);
                            sheet.GetRow(5).GetCell(3).SetCellValue(boxList[0].TrackingNumber);

                            List<Items> itemList = boxList.SelectMany(b => b.Packages.Where(p => p.IsEnable.Value)).SelectMany(p => p.Items.Where(i => i.IsEnable.Value)).ToList();
                            foreach (var group in itemList.GroupBy(i => i.ProductID).ToList())
                            {
                                Skus sku = group.First().Skus;

                                if (sku.ProductName.ToLower().Contains("htc"))
                                {
                                    sheet.GetRow(9).GetCell(2).SetCellValue("✔");
                                    sheet.GetRow(19).GetCell(2).SetCellValue("✔");
                                }
                                else
                                {
                                    sheet.GetRow(9).GetCell(11).SetCellValue("✔");
                                    sheet.GetRow(19).GetCell(13).SetCellValue("✔");
                                }

                                sheet.GetRow(26).GetCell(6).SetCellValue(!string.IsNullOrEmpty(sku.ProductType.ChtName) ? sku.ProductType.ChtName : sku.ProductType.ProductTypeName);

                                sheet.GetRow(28).GetCell(!sku.Brand.Equals(0) ? 8 : 4).SetCellValue("✔");
                                sheet.GetRow(28).GetCell(11).SetCellValue(!sku.Brand.Equals(0) ? sku.Manufacturers.ManufacturerName : "");

                                sheet.GetRow(32).GetCell(9).SetCellValue(group.Sum(i => i.Qty.Value * i.DeclaredValue).ToString("N"));
                                sheet.GetRow(32).GetCell(10).SetCellValue(currency);

                                using (FileStream fsOut = new FileStream(Path.Combine(filePath, string.Format(CheckList.fileName, sku.Sku)), FileMode.Create))
                                {
                                    workbook.Write(fsOut);
                                    fsOut.Close();
                                }

                            }

                        }
                        break;
                }
            }
            catch (Exception e)
            {
                return new ShipResult(false, e.Message);
            }

            return result;
        }

        private ShipResult DirectLine()
        {
            Carriers carrier = package.Method.Carriers;
            CarrierAPI api = carrier.CarrierAPI;

            package.ShippingServiceCode = carrier.Name;
            if (api != null)
            {
                switch (api.Type)
                {
                    case (int)EnumData.CarrierType.IDS:
                        try
                        {
                            IDS_API IDS = new IDS_API(package.Method.Carriers.CarrierAPI);
                            CreateOrderResponse result = IDS.CreateOrder(package);

                            if (!result.status.Equals("200"))
                            {
                                var error = JsonConvert.DeserializeObject<List<List<List<object>>>>(JsonConvert.SerializeObject(result.error));
                                var msg = JsonConvert.SerializeObject(error.SelectMany(e => e).First(e => e[0].Equals(package.OrderID.ToString()))[1]);
                                throw new Exception(JsonConvert.DeserializeObject<string[]>(msg)[0]);
                            }

                            package.TagNo = result.labels.First(l => l.salesRecordNumber.Equals(package.OrderID.ToString())).orderid;
                            using (var client = new WebClient())
                            {
                                client.DownloadFile(result.labels.First().labellink, Path.Combine(filePath, "Label.zip"));
                            }

                            using (ZipArchive archive = ZipFile.OpenRead(Path.Combine(filePath, "Label.zip")))
                            {
                                if (File.Exists(Path.Combine(filePath, "Label.pdf")))
                                {
                                    File.Delete(Path.Combine(filePath, "Label.pdf"));
                                }

                                foreach (ZipArchiveEntry entry in archive.Entries)
                                {
                                    if (entry.FullName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                                    {
                                        entry.ExtractToFile(Path.Combine(filePath, "Label.pdf"));
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            MyHelp.Log("Packages", package.ID, string.Format("建立IDS提單失敗 - {0}", e.Message));
                            return new ShipResult(false, e.Message);
                        }
                        break;

                    case (int)EnumData.CarrierType.Sendle:
                        try
                        {
                            Sendle_API Sendle = new Sendle_API(api);
                            Sendle_API.OrderResponse result = Sendle.Create(package);

                            package.TagNo = result.order_id;
                            package.TrackingNumber = result.sendle_reference;

                            while (Sendle.Order(result.order_id).labels == null)
                            {
                                System.Threading.Thread.Sleep(1000);
                            }

                            string code = string.Format("{0}-{1}-{2}", package.Items.First().ProductID, package.OrderID.Value, result.sendle_reference);
                            Sendle.Label(result.order_id, code, filePath);
                        }
                        catch (Exception e)
                        {
                            MyHelp.Log("Packages", package.ID, string.Format("建立Sendle提單失敗 - {0}", e.Message));
                            return new ShipResult(false, e.Message);
                        }
                        break;
                }
            }
            else
            {
                try
                {
                    package.TagNo = string.Format("{0}-{1}", package.Items.First(i => i.IsEnable.Value).ProductID, package.OrderID.Value);

                    if (File.Exists(Path.Combine(filePath, "Label.pdf"))) File.Delete(Path.Combine(filePath, "Label.pdf"));

                    Document document = new Document();
                    PdfWriter writer = PdfWriter.GetInstance(document, new FileStream(Path.Combine(filePath, "Label.pdf"), FileMode.Create));

                    document.Open();
                    PdfContentByte cb = writer.DirectContent;
                    PdfReader reader = new PdfReader(Path.Combine(basePath, "sample/Label.pdf"));
                    document.SetPageSize(reader.GetPageSizeWithRotation(1));

                    document.NewPage();
                    Paragraph pp = new Paragraph();
                    Barcode128 barcode = new Barcode128 { CodeType = Barcode.CODE128_UCC, Code = package.TagNo };
                    Image barcodeImage = barcode.CreateImageWithBarcode(cb, null, null);
                    barcodeImage.Alignment = Element.ALIGN_CENTER;
                    pp.Add(barcodeImage);
                    document.Add(pp);

                    document.Close();
                    writer.Close();
                }
                catch (Exception e)
                {
                    return new ShipResult(false, e.Message);
                }
            }

            db.DirectLineLabel.Add(new DirectLineLabel()
            {
                IsEnable = true,
                LabelID = package.TagNo,
                OrderID = package.OrderID.Value,
                PackageID = package.ID
            });
            db.SaveChanges();

            return new ShipResult(true);
        }

        private ShipResult DropShip()
        {
            try
            {
                int CompanyID = order.CompanyID.Value; // SCWS.Get_CurrentCompanyID();
                POVendor VendorData = SCWS.Get_Vendor_All(CompanyID).FirstOrDefault(v => v.DisplayName.Equals(warehouse.Name));

                Purchase newPurchase = SCWS.Create_PurchaseOrder(new Purchase()
                {
                    ID = 0,
                    CompanyID = CompanyID,
                    Priority = PriorityCodeType.Normal,
                    Status = PurchaseOrderService.PurchaseStatus.Ordered,
                    PurchaseTitle = string.Format("{0} dropship {1} {2}", warehouse.Name, package.OrderID.Value, SCWS.SyncOn.ToString("MMddyyyy")),
                    VendorID = VendorData != null ? VendorData.ID : 0,
                    VendorInvoiceNumber = "",
                    Memo = !string.IsNullOrEmpty(package.SupplierComment) ? package.SupplierComment : "",
                    DefaultWarehouseID = warehouse.ID,
                    CreatedBy = SCWS.UserID,
                    CreatedOn = SCWS.SyncOn
                });

                foreach (Items item in package.Items.Where(i => i.IsEnable.Equals(true)).ToList())
                {
                    PurchaseItem newPurchaseItem = SCWS.Create_PurchaseOrder_Item(new PurchaseItem()
                    {
                        PurchaseID = newPurchase.ID,
                        ProductID = item.ProductID,
                        ProductName = item.Skus.ProductName,
                        UPC = item.Skus.UPC,
                        QtyOrdered = item.Qty.Value,
                        QtyReceived = 0,
                        QtyReceivedToDate = 0,
                        DefaultWarehouseID = warehouse.ID
                    });
                }

                package.POId = newPurchase.ID;
                MyHelp.Log("PurchaseOrder", package.OrderID, string.Format("開啟 Purchase Order【{0}】成功", package.POId));
            }
            catch (Exception e)
            {
                return new ShipResult(false, e.Message);
            }

            return new ShipResult(true);
        }

        private ShipResult TWN_Carrier()
        {
            Carriers carrier = package.Method.Carriers;
            CarrierAPI api = carrier.CarrierAPI;

            package.ShippingServiceCode = carrier.Name;
            if(api != null)
            {
                switch (api.Type)
                {
                    case (int)EnumData.CarrierType.DHL:
                        try
                        {
                            MyHelp.Log("Packages", package.ID, "開始建立DHL提單");

                            DHL_API DHL = new DHL_API(api);
                            ShipmentResponse result = DHL.Create(package);

                            package.TrackingNumber = result.AirwayBillNumber;

                            MyHelp.Log("Packages", package.ID, "完成建立DHL提單");

                            if (package.Export == (byte)EnumData.Export.正式)
                            {
                                DHL_SaveFile(result);
                            }
                        }
                        catch (Exception e)
                        {
                            string DHL_error = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                            MyHelp.Log("Packages", package.ID, string.Format("建立DHL提單失敗 - {0}", DHL_error));
                            return new ShipResult(false, DHL_error);
                        }
                        break;
                    case (int)EnumData.CarrierType.FedEx:
                        try
                        {
                            FedEx_API FedEx = new FedEx_API(api);
                            ProcessShipmentReply result = FedEx.Create(package);

                            if (!result.HighestSeverity.Equals(NotificationSeverityType.SUCCESS))
                            {
                                throw new Exception(string.Join("\n", result.Notifications.Select(n => n.Message).ToArray()));
                            }

                            CompletedPackageDetail data = result.CompletedShipmentDetail.CompletedPackageDetails.First();
                            package.TrackingNumber = data.TrackingIds.Select(t => t.TrackingNumber).First();

                            if (package.Export == (byte)EnumData.Export.正式)
                            {
                                FedEx_SaveFile(data);
                            }
                        }
                        catch (Exception e)
                        {
                            return new ShipResult(false, e.Message);
                        }
                        break;
                }
            }

            return new ShipResult(true);
        }

        private void DHL_SaveFile(ShipmentResponse result)
        {
            MyHelp.Log("Packages", package.ID, "開始建立AWB、Invoice");

            /***** Air Waybill *****/
            File.WriteAllBytes(Path.Combine(filePath, "AirWaybill.pdf"), Crop(result.LabelImage.First().OutputImage, 97f, 30f, 356f, 553f));

            /***** Commercial Invoice *****/
            File.WriteAllBytes(Path.Combine(filePath, "Invoice.pdf"), result.LabelImage.First().MultiLabels.First().DocImageVal);
            //TWN_CreateInvoice(basePath, filePath, date);

            MyHelp.Log("Packages", package.ID, "完成建立AWB、Invoice");
        }

        private void FedEx_SaveFile(CompletedPackageDetail data)
        {
            /***** Air Waybill *****/
            byte[] zpl = data.Label.Parts.First().Image;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://api.labelary.com/v1/printers/8dpmm/labels/4x6/");
            request.Method = "POST";
            request.Accept = "application/pdf";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = zpl.Length;

            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(zpl, 0, zpl.Length);

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                using (Stream responseStream = response.GetResponseStream())
                {
                    using (FileStream fileStream = File.Create(Path.Combine(filePath, "pdf_temp.pdf")))
                    {
                        responseStream.CopyTo(fileStream);
                    }
                }
            }

            PdfReader.unethicalreading = true;
            // Reads the PDF document
            using (PdfReader pdfReader = new PdfReader(Path.Combine(filePath, "pdf_temp.pdf")))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    // Create a new document
                    //using (iTextSharp.text.Document doc = 
                    //	new iTextSharp.text.Document(new iTextSharp.text.Rectangle(288f,432f)))
                    using (Document doc = new Document(PageSize.A4))
                    {
                        // Make a copy of the document
                        PdfSmartCopy smartCopy = new PdfSmartCopy(doc, ms)
                        {
                            PdfVersion = PdfWriter.VERSION_1_7
                        };
                        smartCopy.CloseStream = false;
                        // Open the newly created document                        
                        doc.Open();
                        // Loop through all pages of the source document
                        for (int i = pdfReader.NumberOfPages; i >= 1; i--)
                        {
                            doc.NewPage();// net necessary line
                                          // Get a page
                            var page = pdfReader.GetPageN(i);
                            // Copy the content and insert into the new document
                            var copiedPage = smartCopy.GetImportedPage(pdfReader, i);
                            smartCopy.AddPage(copiedPage);

                            if (i.Equals(1))
                            {
                                doc.NewPage();
                                smartCopy.AddPage(copiedPage);
                            }
                        }
                        smartCopy.FreeReader(pdfReader);
                        smartCopy.Close();
                        ms.Position = 0;
                        File.WriteAllBytes(Path.Combine(filePath, "AirWaybill.pdf"), ms.GetBuffer());
                        // Close the output document
                        doc.Close();
                    }
                }
            }
            File.Delete(Path.Combine(filePath, "pdf_temp.pdf"));

            /***** Commercial Invoice *****/
            TWN_CreateInvoice();

            /***** Recognizance Book *****/
            var CheckList = new { fileName = "CheckList.xlsx", samplePath = Path.Combine(basePath, "sample", "Fedex_CheckList.xlsx") };
            using (FileStream fsIn = new FileStream(CheckList.samplePath, FileMode.Open))
            {
                XSSFWorkbook workbook = new XSSFWorkbook(fsIn);
                fsIn.Close();

                ISheet sheet = workbook.GetSheetAt(0);
                sheet.GetRow(5).GetCell(3).SetCellValue(package.TrackingNumber);

                if (package.ExportMethod == (int)EnumData.ExportMethod.國貨出口)
                {
                    sheet.GetRow(9).GetCell(2).SetCellValue("✔");
                    sheet.GetRow(19).GetCell(2).SetCellValue("✔");
                }
                else if (package.ExportMethod == (int)EnumData.ExportMethod.外貨復出口)
                {
                    sheet.GetRow(9).GetCell(11).SetCellValue("✔");
                    sheet.GetRow(19).GetCell(13).SetCellValue("✔");
                }

                string[] productType = package.Items.Where(i => i.IsEnable == true).Select(i => i.Skus.ProductType.ChtName).Distinct().ToArray();
                sheet.GetRow(26).GetCell(6).SetCellValue(string.Join(", ", productType));

                string[] brandName = package.Items.Where(i => i.IsEnable.Value && !i.Skus.Brand.Equals(0) && !i.Skus.Brand.Equals(-1)).Select(i => i.Skus.Manufacturers.ManufacturerName).Distinct().ToArray();
                sheet.GetRow(28).GetCell(brandName.Any() ? 8 : 4).SetCellValue("✔");
                sheet.GetRow(28).GetCell(11).SetCellValue(string.Join(", ", brandName));

                sheet.GetRow(32).GetCell(9).SetCellValue(package.Items.Where(i => i.IsEnable == true).Sum(i => i.Qty.Value * i.DeclaredValue).ToString("N"));
                sheet.GetRow(32).GetCell(10).SetCellValue(Enum.GetName(typeof(OrderService.CurrencyCodeType2), package.Orders.OrderCurrencyCode.Value));

                using (FileStream fsOut = new FileStream(Path.Combine(filePath, CheckList.fileName), FileMode.Create))
                {
                    workbook.Write(fsOut);
                    fsOut.Close();
                }
            }
        }

        private void Download_FedEx_PDF(List<Box> boxList, string filePath, string fileName)
        {
            Document document = new Document();
            //create newFileStream object which will be disposed at the end
            using (FileStream newFileStream = new FileStream(Path.Combine(filePath, fileName), FileMode.Create))
            {
                // step 2: we create a writer that listens to the document
                PdfCopy writer = new PdfCopy(document, newFileStream);
                if (writer == null)
                {
                    return;
                }

                // step 3: we open the document
                document.Open();

                string[] fileList = boxList.Select(b => Path.Combine(filePath, b.BoxID + ".pdf")).ToArray();
                foreach (string file in fileList)
                {
                    // we create a reader for a certain document
                    PdfReader reader = new PdfReader(file);
                    reader.ConsolidateNamedDestinations();

                    // step 4: we add content
                    for (int i = 1; i <= reader.NumberOfPages; i++)
                    {
                        PdfImportedPage page = writer.GetImportedPage(reader, i);
                        writer.AddPage(page);

                        if (fileList[0].Equals(file) && i.Equals(reader.NumberOfPages))
                        {
                            writer.AddPage(page);
                        }
                    }

                    PRAcroForm form = reader.AcroForm;
                    if (form != null)
                    {
                        writer.CopyDocumentFields(reader);
                    }

                    reader.Close();
                }

                // step 5: we close the document and writer
                writer.Close();
                document.Close();
            }//disposes the newFileStream object
        }

        private void TWN_CreateInvoice()
        {
            var Invoice = new { fileName = "Invoice.xls", samplePath = Path.Combine(basePath, "sample", "Invoice.xls") };
            using (FileStream fsIn = new FileStream(Invoice.samplePath, FileMode.Open))
            {
                HSSFWorkbook workbook = new HSSFWorkbook(fsIn);
                fsIn.Close();

                ISheet sheet = workbook.GetSheetAt(0);
                sheet.GetRow(4).GetCell(3).SetCellValue(package.TrackingNumber);
                sheet.GetRow(7).GetCell(1).SetCellValue(package.ShipDate.Value.ToString("MMM dd, yyyy", CultureInfo.CreateSpecificCulture("en-US")));
                sheet.GetRow(7).GetCell(8).SetCellValue(package.OrderID.Value);

                sheet.GetRow(10).GetCell(1).SetCellValue("Zhi You Wan LTD (53362065)");
                sheet.GetRow(11).GetCell(1).SetCellValue("51 Section 3 Jianguo North Road");
                sheet.GetRow(12).GetCell(1).SetCellValue("Taichung City West District");
                sheet.GetRow(13).GetCell(1).SetCellValue("Taiwan (R.O.C) 40243");

                int rowIndex = 10;
                Addresses address = package.Orders.Addresses;
                string fullName = string.Join(" ", new string[] { address.FirstName, address.MiddleInitial, address.LastName });
                if (!string.IsNullOrWhiteSpace(fullName))
                    sheet.GetRow(rowIndex++).GetCell(5).SetCellValue(fullName);
                if (!string.IsNullOrWhiteSpace(address.CompanyName))
                    sheet.GetRow(rowIndex++).GetCell(5).SetCellValue(address.CompanyName);
                if (!string.IsNullOrWhiteSpace(address.StreetLine1))
                    sheet.GetRow(rowIndex++).GetCell(5).SetCellValue(address.StreetLine1);
                if (!string.IsNullOrWhiteSpace(address.StreetLine2))
                    sheet.GetRow(rowIndex++).GetCell(5).SetCellValue(address.StreetLine2);
                string cityArea = address.City + " " + address.StateName + " " + address.PostalCode;
                if (!string.IsNullOrWhiteSpace(cityArea))
                    sheet.GetRow(rowIndex++).GetCell(5).SetCellValue(cityArea);
                if (!string.IsNullOrWhiteSpace(address.CountryName))
                    sheet.GetRow(rowIndex).GetCell(5).SetCellValue(address.CountryName);

                sheet.GetRow(17).GetCell(1).SetCellValue("Taiwan");
                sheet.GetRow(21).GetCell(1).SetCellValue(address.CountryName);

                rowIndex = 24;
                foreach (Items item in package.Items.Where(i => i.IsEnable == true))
                {
                    Country country = MyHelp.GetCountries().FirstOrDefault(c => c.ID == item.Skus.Origin);
                    sheet.GetRow(rowIndex).GetCell(1).SetCellValue(country.OriginName);
                    sheet.GetRow(rowIndex).GetCell(4).SetCellValue(item.Skus.ProductType.ProductTypeName);
                    sheet.GetRow(rowIndex).GetCell(5).SetCellValue(item.Skus.ProductType.HSCode);
                    sheet.GetRow(rowIndex).GetCell(8).SetCellValue(item.Qty.Value);
                    sheet.GetRow(rowIndex).GetCell(9).SetCellValue("pieces");
                    sheet.GetRow(rowIndex).GetCell(10).SetCellValue(item.Qty.Value * ((double)item.Skus.ShippingWeight / 1000) + "kg");
                    sheet.GetRow(rowIndex).GetCell(11).SetCellValue(item.DeclaredValue.ToString("N"));
                    sheet.GetRow(rowIndex++).GetCell(16).SetCellValue((item.DeclaredValue * item.Qty.Value).ToString("N"));
                }
                sheet.GetRow(47).GetCell(3).SetCellValue(1);
                sheet.GetRow(47).GetCell(10).SetCellValue(package.Items.Where(i => i.IsEnable == true).Sum(i => i.Qty.Value * ((double)i.Skus.ShippingWeight / 1000)) + "kg");
                sheet.GetRow(47).GetCell(11).SetCellValue(Enum.GetName(typeof(OrderService.CurrencyCodeType2), package.Orders.OrderCurrencyCode.Value));
                sheet.GetRow(47).GetCell(16).SetCellValue(package.Items.Where(i => i.IsEnable == true).Sum(i => i.Qty.Value * i.DeclaredValue).ToString("N"));
                sheet.GetRow(57).GetCell(9).SetCellValue(package.ShipDate.Value.ToString("yyyy-MM-dd"));

                using (FileStream fsOut = new FileStream(Path.Combine(filePath, Invoice.fileName), FileMode.Create))
                {
                    workbook.Write(fsOut);

                    fsOut.Close();
                }
            }
        }

        private void Box_CreateInvoice(List<Box> boxList, DirectLine directLine, string basePath, string filePath, string currency)
        {
            var Invoice = new { fileName = "Invoice.xls", samplePath = Path.Combine(basePath, "sample", "Invoice-2.xls") };
            using (FileStream fsIn = new FileStream(Invoice.samplePath, FileMode.Open))
            {
                HSSFWorkbook workbook = new HSSFWorkbook(fsIn);
                fsIn.Close();

                HSSFSheet sheet = (HSSFSheet)workbook.GetSheetAt(0);
                sheet.GetRow(4).GetCell(3).SetCellValue(boxList[0].TrackingNumber);
                sheet.GetRow(7).GetCell(1).SetCellValue(boxList[0].Create_at.ToString("MMM dd, yyyy", CultureInfo.CreateSpecificCulture("en-US")));
                //sheet.GetRow(7).GetCell(8).SetCellValue(boxList[0].MainBox);

                sheet.GetRow(10).GetCell(1).SetCellValue("Zhi You Wan LTD (53362065)");
                sheet.GetRow(11).GetCell(1).SetCellValue("51 Section 3 Jianguo North Road");
                sheet.GetRow(12).GetCell(1).SetCellValue("Taichung City West District");
                sheet.GetRow(13).GetCell(1).SetCellValue("Taiwan (R.O.C) 40243");

                int rowIndex = 10;
                if (!string.IsNullOrWhiteSpace(directLine.ContactName))
                    sheet.GetRow(rowIndex++).GetCell(5).SetCellValue(directLine.ContactName);
                if (!string.IsNullOrWhiteSpace(directLine.CompanyName))
                    sheet.GetRow(rowIndex++).GetCell(5).SetCellValue(directLine.CompanyName);
                if (!string.IsNullOrWhiteSpace(directLine.StreetLine1))
                    sheet.GetRow(rowIndex++).GetCell(5).SetCellValue(directLine.StreetLine1);
                if (!string.IsNullOrWhiteSpace(directLine.StreetLine2))
                    sheet.GetRow(rowIndex++).GetCell(5).SetCellValue(directLine.StreetLine2);
                string cityArea = directLine.City + " " + directLine.StateName + " " + directLine.PostalCode;
                if (!string.IsNullOrWhiteSpace(cityArea))
                    sheet.GetRow(rowIndex++).GetCell(5).SetCellValue(cityArea);
                if (!string.IsNullOrWhiteSpace(directLine.CountryName))
                    sheet.GetRow(rowIndex).GetCell(5).SetCellValue(directLine.CountryName);

                sheet.GetRow(17).GetCell(1).SetCellValue("Taiwan");
                sheet.GetRow(21).GetCell(1).SetCellValue(directLine.CountryName);

                rowIndex = 24;
                List<Items> itemList = boxList.SelectMany(b => b.Packages.Where(p => p.IsEnable.Value)).SelectMany(p => p.Items.Where(i => i.IsEnable.Value)).ToList();
                if (itemList.Count() > 22)
                {
                    int insertRow = 45, add = itemList.Count() - 22;
                    MyHelp.ShiftRows(ref sheet, insertRow, sheet.LastRowNum, add);

                    for (int row = insertRow; row < insertRow + add; row++)
                    {
                        MyHelp.CopyRow(ref sheet, insertRow - 1, row, true, false, true, false);
                    }
                }

                foreach (var item in itemList)
                {
                    Skus sku = item.Skus;
                    Country country = MyHelp.GetCountries().FirstOrDefault(c => c.ID.Equals(sku.Origin));
                    sheet.GetRow(rowIndex).GetCell(1).SetCellValue(country.OriginName);
                    string productName = sku.ProductType.ProductTypeName + " - " + sku.ProductName;
                    sheet.GetRow(rowIndex).GetCell(4).SetCellValue(productName);
                    sheet.GetRow(rowIndex).GetCell(5).SetCellValue(sku.ProductType.HSCode);
                    sheet.GetRow(rowIndex).GetCell(8).SetCellValue(item.Qty.Value);
                    sheet.GetRow(rowIndex).GetCell(9).SetCellValue("pieces");
                    sheet.GetRow(rowIndex).GetCell(10).SetCellValue((item.Qty.Value * (double)sku.ShippingWeight / 1000) + "kg");
                    sheet.GetRow(rowIndex).GetCell(11).SetCellValue(item.DeclaredValue.ToString("N"));
                    sheet.GetRow(rowIndex).GetCell(16).SetCellValue((item.Qty.Value * item.DeclaredValue).ToString("N"));
                    sheet.GetRow(rowIndex++).HeightInPoints = (productName.Length / 20 + 1) * sheet.DefaultRowHeight / 20;
                }

                rowIndex = (rowIndex > 46) ? rowIndex + 1 : 47;

                byte[] picData = File.ReadAllBytes(Path.Combine(basePath, "sample", "company.png"));
                int picIndex = workbook.AddPicture(picData, PictureType.PNG);
                var drawing = sheet.CreateDrawingPatriarch();
                var anchor = new HSSFClientAnchor(400, 50, 500, 50, 4, rowIndex - 1, 6, rowIndex + 3);
                var pictuer = drawing.CreatePicture(anchor, picIndex);

                sheet.GetRow(rowIndex).GetCell(3).SetCellValue(1);
                sheet.GetRow(rowIndex).GetCell(10).SetCellValue(itemList.Sum(i => i.Qty.Value * ((double)i.Skus.ShippingWeight / 1000)) + "kg");
                sheet.GetRow(rowIndex).GetCell(11).SetCellValue(currency);
                sheet.GetRow(rowIndex).GetCell(16).SetCellValue(itemList.Sum(i => i.Qty.Value * i.DeclaredValue).ToString("N"));
                sheet.GetRow(rowIndex + 10).GetCell(9).SetCellValue(boxList[0].Create_at.ToString("yyyy-MM-dd"));

                using (FileStream fsOut = new FileStream(Path.Combine(filePath, Invoice.fileName), FileMode.Create))
                {
                    workbook.Write(fsOut);

                    fsOut.Close();
                }
            }
        }

        private ShipResult CRD_Carrier()
        {
            Carriers carrier = package.Method.Carriers;
            CarrierAPI api = carrier.CarrierAPI;

            switch (api.Type)
            {
                case (int)EnumData.CarrierType.Sendle:
                    break;
            }

            return new ShipResult(true);
        }

        private ShipResult MWS_Carrier()
        {
            Carriers carrier = package.Method.Carriers;
            CarrierAPI api = carrier.CarrierAPI;

            switch (api.Type)
            {
                case (int)EnumData.CarrierType.Sendle:
                    break;
            }

            return new ShipResult(true);
        }

        private ShipResult _4PX_Carrier()
        {
            Carriers carrier = package.Method.Carriers;
            CarrierAPI api = carrier.CarrierAPI;

            switch (api.Type)
            {
                case (int)EnumData.CarrierType.Sendle:
                    break;
            }

            return new ShipResult(true);
        }

        private ShipResult Winit_Carrier()
        {
            Carriers carrier = package.Method.Carriers;
            CarrierAPI api = carrier.CarrierAPI;
            Winit_API winit = new Winit_API(api);

            Addresses address = order.Addresses;

            try
            {
                createOutboundOrder_data data = new createOutboundOrder_data()
                {
                    warehouseID = int.Parse(winit.warehouseIDs[package.Items.First(i => i.IsEnable.Value).ShipFromWarehouseID.Value]),
                    eBayOrderID = order.OrderSourceOrderId,
                    repeatable = "Y",
                    deliveryWayID = package.Method.MethodType.ToString(),
                    insuranceTypeID = 1000000,
                    sellerOrderNo = order.OrderID.ToString(),
                    recipientName = string.Join(" ", new string[] { address.FirstName, address.MiddleInitial, address.LastName }),
                    phoneNum = address.PhoneNumber,
                    zipCode = address.PostalCode,
                    emailAddress = address.EmailAddress,
                    state = address.CountryName,
                    region = address.StateName,
                    city = address.City,
                    address1 = address.StreetLine1,
                    address2 = address.StreetLine2,
                    isShareOrder = "N",
                    fromBpartnerId = "",
                    productList = new List<createOutboundInfo_productList>()
                };

                foreach (Items item in package.Items.Where(i => i.IsEnable.Value))
                {
                    data.productList.Add(new createOutboundInfo_productList()
                    {
                        eBayBuyerID = order.eBayUserID,
                        eBayItemID = item.eBayItemID,
                        eBaySellerID = "",
                        eBayTransactionID = item.eBayTransactionId,
                        productCode = item.ProductID,
                        productNum = item.Qty.ToString()
                    });
                }

                Received result = winit.Create(data);

                if (result.code != "0") return new ShipResult(false, result.code + "-" + result.msg);

                createOutboundOrderData outboundOrderData = result.data.ToObject<createOutboundOrderData>();
                package.WinitNo = outboundOrderData.outboundOrderNum;
                package.ShippingServiceCode = carrier.Name;
            }
            catch (Exception e)
            {
                return new ShipResult(false, e.Message);
            }

            return new ShipResult(true);
        }

        private byte[] Crop(byte[] pdfbytes, float llx, float lly, float urx, float ury)
        {
            byte[] rslt = null;
            // Allows PdfReader to read a pdf document without the owner's password
            PdfReader.unethicalreading = true;
            // Reads the PDF document
            using (PdfReader pdfReader = new PdfReader(pdfbytes))
            {
                // Set which part of the source document will be copied.
                // PdfRectangel(bottom-left-x, bottom-left-y, upper-right-x, upper-right-y)
                PdfRectangle rect = new PdfRectangle(llx, lly, urx, ury);

                using (MemoryStream ms = new MemoryStream())
                {
                    // Create a new document
                    //using (Document doc = 
                    //	new Document(new Rectangle(288f,432f)))
                    using (Document doc = new Document(PageSize.A4))
                    {
                        // Make a copy of the document
                        PdfSmartCopy smartCopy = new PdfSmartCopy(doc, ms)
                        {
                            PdfVersion = PdfWriter.VERSION_1_7
                        };
                        smartCopy.CloseStream = false;
                        // Open the newly created document                        
                        doc.Open();
                        // Loop through all pages of the source document
                        for (int i = 1; i <= pdfReader.NumberOfPages; i++)
                        {
                            doc.NewPage();// net necessary line
                            // Get a page
                            var page = pdfReader.GetPageN(i);
                            // Apply the rectangle filter we created
                            page.Put(PdfName.CROPBOX, rect);
                            page.Put(PdfName.MEDIABOX, rect);
                            // Copy the content and insert into the new document
                            var copiedPage = smartCopy.GetImportedPage(pdfReader, i);
                            smartCopy.AddPage(copiedPage);
                        }
                        smartCopy.FreeReader(pdfReader);
                        smartCopy.Close();
                        ms.Position = 0;
                        rslt = ms.GetBuffer();
                        // Close the output document
                        doc.Close();
                    }
                }
                return rslt;
            }
        }
    }

    public class ShipResult
    {
        private bool status;
        private string message;

        public bool Status { get { return status; } }
        public string Message { get { return message; } }

        public ShipResult(bool status = false, string message = "")
        {
            this.status = status;
            this.message = message;
        }
    }
}
