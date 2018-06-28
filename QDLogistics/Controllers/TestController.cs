using CarrierApi.DHL;
using CarrierApi.FedEx;
using CarrierApi.Winit;
using DirectLineApi.IDS;
using GemBox.Spreadsheet;
using Ionic.Zip;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using QDLogistics.Commons;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;
using QDLogistics.OrderService;
using SellerCloud_WebService;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;

namespace QDLogistics.Controllers
{
    public class TestController : Controller
    {
        private QDLogisticsEntities db;

        public TestController()
        {
            db = new QDLogisticsEntities();
        }

        public void Index()
        {
            IRepository<Orders> Orders = new GenericRepository<Orders>(db);
            SC_WebService SCWS = new SC_WebService(Session["ApiUserName"].ToString(), Session["ApiPassword"].ToString());

            int OrderID = 5462611;
            var order = Orders.Get(OrderID);
            if (SCWS.Is_login)
            {
                var SC_order = SCWS.Get_OrderStatus(OrderID);
                var SC_order2 = SCWS.Get_OrderData(OrderID);
                //SyncProcess Sync = new SyncProcess(Session);
                //string message = Sync.Sync_Order(OrderID);
                //var address = DataProcess.SetAddressData(order.Addresses, SC_order.ShippingAddress, SC_order.BillingAddress);
            }
        }

        private void CheckDHL(int OrderID)
        {
            IRepository<Orders> Orders = new GenericRepository<Orders>(db);
            Orders order = Orders.Get(OrderID);
            TrackOrder track = new TrackOrder(order, order.Packages.First(p => p.IsEnable.Value));
            TrackResult result = track.track();
        }

        private void DHL_Test(int OrderID)
        {
            SC_WebService SCWS = new SC_WebService(Session["ApiUserName"].ToString(), Session["ApiPassword"].ToString());

            IRepository<Orders> Orders = new GenericRepository<Orders>(db);
            Orders order = Orders.Get(OrderID);
            Packages package = order.Packages.First(p => p.IsEnable.Value);

            try
            {
                DHL_API DHL = new DHL_API(package.Method.Carriers.CarrierAPI);
                ShipmentResponse result = DHL.Create(package);
                //String OutputImage = "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lSCkwXUE0oJA9h2hdBnTdtu629NsKdtiXSJ9gFWFnEHWjcD2HcH3Pkixy5VVC4Lf9wHWJRKQex+wFoC05wOsSuRazrarWqpuALbvCK6rWizWFHItsxYEt1e14PfXLlPmgpddgkBaEOJ+poSqJEgC1hTSkawbge0F4f149N+/JTT45pTqFTxeRsR6+3bx4vsvXr1+rQDkSkjKxWIfcb2FH/NpxZndt0CnnbG7W+TFenvwTuvvQPgVefy+BtucO/fN5pflaqFmN5v14uNrLINrWVxjVf0P11h8x2ei232WguheP0u9Xl4u1vfwshXeo+NtyAe6DVHLtbqvNzaPvkbEA9L4wSqnwvc+X081HP80/PElRp1yseExN4lQSsDRHVKAGy/acwp8IXWkPsyvF2AE3TXqSNL+Az//A3gHqF4KZW5kc3RyZWFtCmVuZG9iago3IDAgb2JqCjw8L1BhcmVudCA2IDAgUi9Db250ZW50cyA4IDAgUi9UeXBlL1BhZ2UvUmVzb3VyY2VzPDwvWE9iamVjdDw8L1hmMSA5IDAgUj4+L1Byb2NTZXRbL1BERi9UZXh0L0ltYWdlQi9JbWFnZUMvSW1hZ2VJXT4+L01lZGlhQm94WzAgMCA4NDEuODkgNTk1LjI4XT4+CmVuZG9iago2IDAgb2JqCjw8L0lUWFQoMi4xLjcpL1R5cGUvUGFnZXMvQ291bnQgMi9LaWRzWzEgMCBSIDcgMCBSXT4+CmVuZG9iagoxMiAwIG9iago8PC9UeXBlL0NhdGFsb2cvUGFnZXMgNiAwIFI+PgplbmRvYmoKMTMgMCBvYmoKPDwvUHJvZHVjZXIoaVRleHQgMi4xLjcgYnkgMVQzWFQpL01vZERhdGUoRDoyMDE3MTExNjAzNDQwMVopL0NyZWF0aW9uRGF0ZShEOjIwMTcxMTE2MDM0NDAxWik+PgplbmRvYmoKeHJlZgowIDE0CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwNDg0NSAwMDAwMCBuIAowMDAwMDAwMDE1IDAwMDAwIG4gCjAwMDAwMDAzMTMgMDAwMDAgbiAKMDAwMDAwMDEzMiAwMDAwMCBuIAowMDAwMDAwMjI1IDAwMDAwIG4gCjAwMDAwMDg1OTcgMDAwMDAgbiAKMDAwMDAwODQzNSAwMDAwMCBuIAowMDAwMDA1MDA3IDAwMDAwIG4gCjAwMDAwMDUzMDcgMDAwMDAgbiAKMDAwMDAwNTEyNCAwMDAwMCBuIAowMDAwMDA1MjE4IDAwMDAwIG4gCjAwMDAwMDg2NjYgMDAwMDAgbiAKMDAwMDAwODcxMiAwMDAwMCBuIAp0cmFpbGVyCjw8L1Jvb3QgMTIgMCBSL0lEIFs8MWU2NWUwZThhZTE1MTNiYzMxODdmZmQ4ZGE0YTQ5Mjk+PDY3ZjIzMGRlM2I1YzIwNWE4ZGU2YzNkMDE1ODk3NDY2Pl0vSW5mbyAxMyAwIFIvU2l6ZSAxND4+CnN0YXJ0eHJlZgo4ODIzCiUlRU9GCg==";
                //System.IO.File.WriteAllBytes(@"C:\Downloads\DHL_test.pdf", Crop(Convert.FromBase64String(OutputImage), 97f, 30f, 356f, 553f));
                System.IO.File.WriteAllBytes(@"C:\Users\qdtuk\Downloads\DHL_test.pdf", Crop(result.LabelImage.First().OutputImage, 97f, 30f, 356f, 553f));
                System.IO.File.WriteAllBytes(@"C:\Users\qdtuk\Downloads\DHL_Invoice_test.pdf", result.LabelImage.First().MultiLabels.First().DocImageVal);
            }
            catch (Exception e)
            {
            }
        }

        private byte[] Crop(byte[] pdfbytes, float llx, float lly, float urx, float ury)
        {
            byte[] rslt = null;
            // Allows PdfReader to read a pdf document without the owner's password
            iTextSharp.text.pdf.PdfReader.unethicalreading = true;
            // Reads the PDF document
            using (iTextSharp.text.pdf.PdfReader pdfReader = new iTextSharp.text.pdf.PdfReader(pdfbytes))
            {
                // Set which part of the source document will be copied.
                // PdfRectangel(bottom-left-x, bottom-left-y, upper-right-x, upper-right-y)
                iTextSharp.text.pdf.PdfRectangle rect = new iTextSharp.text.pdf.PdfRectangle(llx, lly, urx, ury);

                using (MemoryStream ms = new MemoryStream())
                {
                    // Create a new document
                    //using (iTextSharp.text.Document doc = 
                    //	new iTextSharp.text.Document(new iTextSharp.text.Rectangle(288f,432f)))
                    using (iTextSharp.text.Document doc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4))
                    {
                        // Make a copy of the document
                        iTextSharp.text.pdf.PdfSmartCopy smartCopy = new iTextSharp.text.pdf.PdfSmartCopy(doc, ms)
                        {
                            PdfVersion = iTextSharp.text.pdf.PdfWriter.VERSION_1_7
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
                            page.Put(iTextSharp.text.pdf.PdfName.CROPBOX, rect);
                            page.Put(iTextSharp.text.pdf.PdfName.MEDIABOX, rect);
                            // Copy the content and insert into the new document
                            var copiedPage = smartCopy.GetImportedPage(pdfReader, i);
                            smartCopy.AddPage(copiedPage);

                            if (i.Equals(pdfReader.NumberOfPages))
                            {
                                doc.NewPage();
                                smartCopy.AddPage(copiedPage);
                            }
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

        private void CheckFedEx(int OrderID)
        {
            IRepository<Orders> Orders = new GenericRepository<Orders>(db);
            Orders order = Orders.Get(OrderID);
            TrackOrder track = new TrackOrder(order, order.Packages.First());
            TrackResult result = track.track();
        }

        private void FedEx_Test(int OrderID)
        {
            IRepository<Orders> Orders = new GenericRepository<Orders>(db);

            Orders order = Orders.Get(OrderID);

            FedEx_API FedEx = new FedEx_API(order.Packages.First(p => p.IsEnable.Equals(true)).Method.Carriers.CarrierAPI);
            var result = FedEx.Create(order.Packages.First(p => p.IsEnable.Equals(true)));

            if (result.HighestSeverity.Equals(FedExShipService.NotificationSeverityType.SUCCESS))
            {
                var Data = result.CompletedShipmentDetail.CompletedPackageDetails.First();
                string TrackingNumber = Data.TrackingIds.First().TrackingNumber;
                //System.IO.File.WriteAllBytes(@"G:\Downloads\FedEx_Test3.pdf", Crop(Data.Label.Parts.First().Image, 29f, 324f, 319f, 762f));
                //System.IO.File.WriteAllBytes(@"G:\Downloads\FedEx_Test4.pdf", Data.Label.Parts.First().Image);

                var zpl = Data.Label.Parts.First().Image;
                string str = Encoding.ASCII.GetString(zpl);
                var request = (HttpWebRequest)WebRequest.Create("http://api.labelary.com/v1/printers/8dpmm/labels/4x6.75/");
                request.Method = "POST";
                request.Accept = "application/pdf"; // omit this line to get PNG images back
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = zpl.Length;

                var requestStream = request.GetRequestStream();
                requestStream.Write(zpl, 0, zpl.Length);
                requestStream.Close();

                try
                {
                    var response = (HttpWebResponse)request.GetResponse();
                    var responseStream = response.GetResponseStream();
                    var fileStream = System.IO.File.Create(@"G:\Downloads\FedEx_Test5.pdf"); // change file name for PNG images
                    responseStream.CopyTo(fileStream);
                    responseStream.Close();
                    fileStream.Close();
                }
                catch (WebException e)
                {
                    Console.WriteLine("Error: {0}", e.Status);
                }
            }

            iTextSharp.text.pdf.PdfReader.unethicalreading = true;
            // Reads the PDF document
            using (iTextSharp.text.pdf.PdfReader pdfReader = new iTextSharp.text.pdf.PdfReader(@"G:\Downloads\FedEx_Test5.pdf"))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    // Create a new document
                    //using (iTextSharp.text.Document doc = 
                    //	new iTextSharp.text.Document(new iTextSharp.text.Rectangle(288f,432f)))
                    using (iTextSharp.text.Document doc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4))
                    {
                        // Make a copy of the document
                        iTextSharp.text.pdf.PdfSmartCopy smartCopy = new iTextSharp.text.pdf.PdfSmartCopy(doc, ms)
                        {
                            PdfVersion = iTextSharp.text.pdf.PdfWriter.VERSION_1_7
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
                        System.IO.File.WriteAllBytes(@"G:\Downloads\FedEx_Test6.pdf", ms.GetBuffer());
                        // Close the output document
                        doc.Close();
                    }
                }
            }
        }

        private void Preset_Test(int OrderID)
        {
            using (IRepository<Orders> Orders = new GenericRepository<Orders>(db))
            {
                using (var orderPreset = new OrderPreset(Session, Orders.Get(OrderID)))
                {
                    orderPreset.Save();
                }
            }
        }

        private ActionResult Hub_Test(string clientName)
        {
            ViewData["clientName"] = clientName;
            return View();
        }

        private void Hub_Push()
        {
            using (Hubs.ServerHub server = new Hubs.ServerHub())
            {
                server.BroadcastOrderChange(123, EnumData.OrderChangeStatus.提交至待出貨區);
            }
        }

        private ActionResult Hub_Clients()
        {
            using (Hubs.ServerHub server = new Hubs.ServerHub())
            {
                return Json(server.GetAllClinet(), JsonRequestBehavior.AllowGet);
            }
        }

        public void Check_Winit(int OrderID)
        {
            using (Winit_API winitAPI = new Winit_API())
            {
                Received result = winitAPI.Order(OrderID);
            }
        }

        private void Update_Carrier()
        {
            int[] OrderIDs = new int[] { 5453707, 5453746, 5453757, 5453766, 5453775, 5453802, 5453835, 5453902, 5453909, 5453928, 5453929, 5453950, 5453955, 5453958, 5453962, 5454001, 5454002, 5454022, 5454029, 5454030, 5454046, 5454047, 5454089, 5453707, 5454090, 5454091, 5454098, 5454099, 5454167, 5454171, 5454173, 5454175, 5454180, 5454181, 5454182, 5454183, 5454184, 5454186, 5454189, 5454190, 5454191, 5454192, 5454193, 5454195, 5454196, 5454197, 5454200, 5454219 };

            SC_WebService SCWS = new SC_WebService(Session["ApiUserName"].ToString(), Session["ApiPassword"].ToString());

            using (IRepository<Packages> Packages = new GenericRepository<Packages>(db))
            {
                foreach (int orderID in OrderIDs)
                {
                    OrderService.OrderData orderData = SCWS.Get_OrderData(orderID);
                    OrderService.Order SC_order = orderData.Order;
                    string shippingCarrier = SC_order.ShippingCarrier;

                    Packages package = Packages.GetAll().First(p => p.OrderID.Value.Equals(orderID));
                    string newShippingCarrier = package.Method.Carriers.Name;

                    if (!shippingCarrier.Equals(newShippingCarrier))
                    {
                        SCWS.Update_OrderShippingStatus(SC_order, newShippingCarrier);
                        Response.Write(string.Format("Order【{0}】Carrier - {1} change to Carrier - {2}", orderID, shippingCarrier, newShippingCarrier));
                    }

                    package.Orders.ShippingCarrier = newShippingCarrier;
                    Packages.Update(package);
                }
                Packages.SaveChanges();
            }
        }

        private void Update_Warehouse()
        {
            int[] OrderIDs = new int[] { 5455086, 5455090, 5455158, 5455216 };

            SC_WebService SCWS = new SC_WebService(Session["ApiUserName"].ToString(), Session["ApiPassword"].ToString());

            using (IRepository<Items> Items = new GenericRepository<Items>(db))
            {
                foreach (int orderID in OrderIDs)
                {
                    OrderService.OrderData orderData = SCWS.Get_OrderData(orderID);
                    OrderService.Order SC_order = orderData.Order;

                    var SC_items = SC_order.Items.ToArray();
                    foreach (var item in SC_items)
                    {
                        item.ShipFromWareHouseID = 111;
                    }
                    SCWS.Update_OrderItem(SC_items);
                }
            }
        }

        private void IDS_Test(int orderID)
        {
            Packages package = db.Packages.AsNoTracking().First(p => p.OrderID.Value.Equals(orderID));

            IDS_API IDS = new IDS_API(package.Method.Carriers.CarrierAPI);
            var result = IDS.GetTrackingNumber(package);
            if (result.trackingnumber.Any(t => t.First().Equals(package.OrderID.ToString())))
            {
                string tracking = result.trackingnumber.Last(t => t.First().Equals(package.OrderID.ToString()))[1];
            }
        }

        private void Box_Test(string boxID)
        {
            using (IRepository<Box> Box = new GenericRepository<Box>(db))
            {
                SC_WebService SCWS = new SC_WebService(Session["ApiUserName"].ToString(), Session["ApiPassword"].ToString());
                ShipProcess shipProcess = new ShipProcess(SCWS);
                ShipResult boxResult = shipProcess.Dispatch(Box.Get(boxID));
            }
        }

        private void Excel_Test()
        {
            string basePath = HostingEnvironment.MapPath("~/FileUploads");

            using (FileStream fsIn = new FileStream(@"C:\Users\qdtuk\Downloads\Invoice.xls", FileMode.Open))
            {
                HSSFWorkbook workbook = new HSSFWorkbook(fsIn);
                fsIn.Close();

                HSSFSheet sheet = (HSSFSheet)workbook.GetSheetAt(0);

                int insertRow = 47, add = 8;
                MyHelp.ShiftRows(ref sheet, insertRow, sheet.LastRowNum, add);

                for (int row = insertRow; row < insertRow + add; row++)
                {
                    MyHelp.CopyRow(ref sheet, insertRow - 1, row, true, false, true, false);
                }

                byte[] picData = System.IO.File.ReadAllBytes(@"C:\Users\qdtuk\Downloads\company.png");
                int picIndex = workbook.AddPicture(picData, PictureType.PNG);
                var drawing = sheet.CreateDrawingPatriarch();
                var anchor = new HSSFClientAnchor(400, 50, 500, 50, 4, insertRow + add + 1, 6, insertRow + add + 5);
                var pictuer = drawing.CreatePicture(anchor, picIndex);

                using (FileStream fsOut = new FileStream(@"C:\Users\qdtuk\Downloads\Invoice_test.xls", FileMode.Create))
                {
                    workbook.Write(fsOut);

                    fsOut.Close();
                }
            }
        }

        private void Case_Test()
        {
            TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;

            lock (factory)
            {
                ThreadTask threadTask = new ThreadTask("檢查 Case Event 進度");
                threadTask.AddWork(factory.StartNew(Session =>
                {
                    threadTask.Start();

                    db = new QDLogisticsEntities();
                    IRepository<CaseEvent> CaseEvent = new GenericRepository<CaseEvent>(db);

                    string message = "";

                    try
                    {
                        HttpSessionStateBase session = (HttpSessionStateBase)Session;
                        MyHelp.Log("CaseEvent", null, "開始檢查 Case Event 進度", session);

                        List<byte> CaseType = new List<byte>() { (byte)EnumData.CaseEventType.CancelShipment, (byte)EnumData.CaseEventType.ChangeShippingMethod };
                        List<CaseEvent> CaseEventList = db.CaseEvent.AsNoTracking().Where(c => CaseType.Contains(c.Type) && c.Request.Equals((byte)EnumData.CaseEventRequest.None) && c.Status.Equals((byte)EnumData.CaseEventStatus.Open)).ToList();
                        if (CaseEventList.Any())
                        {
                            using (CaseLog CaseLog = new CaseLog(session))
                            {
                                DateTime today = DateTime.UtcNow;
                                foreach (CaseEvent eventData in CaseEventList)
                                {
                                    DateTime RequestDate = MyHelp.SkipWeekend(eventData.Request_at.Value.AddDays(1));
                                    DateTime CreateDate = MyHelp.SkipWeekend(eventData.Create_at.AddDays(2));
                                    if (RequestDate.CompareTo(today) <= 0)
                                    {
                                        if (CreateDate.CompareTo(today) <= 0)
                                        {
                                            eventData.Request = (byte)EnumData.CaseEventRequest.Failed;
                                            CaseEvent.Update(eventData, eventData.ID);
                                        }
                                        else
                                        {
                                            CaseLog.OrderInit(eventData.Packages);
                                            switch (eventData.Type)
                                            {
                                                case (byte)EnumData.CaseEventType.CancelShipment:
                                                    //CaseLog.SendCancelMail();
                                                    break;

                                                case (byte)EnumData.CaseEventType.ChangeShippingMethod:
                                                    //CaseLog.SendChangeShippingMethodMail(eventData.MethodID, eventData.NewLabelID);
                                                    break;
                                            }
                                        }
                                    }
                                }
                                CaseEvent.SaveChanges();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                    }

                    return message;
                }, HttpContext.Session));

            }
        }

        private void Mail_Test()
        {
            string mailFrom = "dispatch-qd@hotmail.com";
            string[] mailTo = new string[] { "qd.tuko@hotmail.com" };

            bool mailResult = MyHelp.Mail_Send(mailFrom, mailTo, null, "Mail Test", "Mail Test", true, null, null, false);
            if (mailResult)
            {
                Response.Write("Success!");
            }
        }

        public void RMA_Test(int OrderID)
        {
            Packages package = db.Packages.AsNoTracking().First(p => p.IsEnable.Value && p.OrderID.Value.Equals(OrderID));

            SC_WebService SCWS = new SC_WebService(Session["ApiUserName"].ToString(), Session["ApiPassword"].ToString());
            PurchaseOrderService.RMAData order_RMA = SCWS.Get_RMA_Data(package.RMAId.Value);
            foreach (var SC_Item in order_RMA.Items)
            {
                Items item = db.Items.AsNoTracking().First(i => i.ID.Equals(SC_Item.OriginalOrderItemID));
                string serialsList = string.Join(", ", item.SerialNumbers.Where(s => !string.IsNullOrEmpty(s.SerialNumber)).Select(s => s.SerialNumber).ToArray());
                bool result = SCWS.Delete_ItemSerials(item.OrderID.Value, item.ID);
                //bool result = SCWS.Receive_RMA_Item(SC_Item.RMAID, SC_Item.ID, item.ProductID, item.Qty.Value, item.ReturnedToWarehouseID.Value, serialsList);
                //bool result = SCWS.Update_RAM_Item(SC_Item);
            }
        }
    }
}