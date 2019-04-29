using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CarrierApi.Sendle;
using DirectLineApi.IDS;
using Newtonsoft.Json;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;
using QDLogistics.OrderService;
using SellerCloud_WebService;

namespace QDLogistics.Commons
{
    public class CaseLog : IDisposable
    {
        private QDLogisticsEntities db;
        private IRepository<CaseEvent> CaseEvent;
        private IRepository<Packages> Packages;
        private IRepository<Items> Items;
        private IRepository<SerialNumberForRefundLabel> RefundLabelSerial;

        private Orders orderData;
        private Packages packageData;
        private List<Items> itemList;

        private IDS_API IDS_Api;
        private Sendle_API Sendle_Api;
        private SC_WebService SCWS;

        private bool disposed = false;
        private HttpSessionStateBase Session;

        private string sendMail = "dispatch-qd@hotmail.com";
        private string mailTitle;
        private string mailBody;
        private string[] receiveMails;
        private string[] ccMails = new string[] { "kelly@qd.com.tw", "demi@qd.com.tw" };
        //private string[] ccMails = new string[] { };

        private Dictionary<string, string[]> MailList = new Dictionary<string, string[]>()
        {
            { "IDS", new string[]{ "gloria.chiu@contin-global.com", "cherry.chen@contin-global.com", "TWCS@contin-global.com", "contincs@gmail.com", "shipping_qd@hotmail.com" } },
            { "ECOF", new string[]{ "customerservice@ecof.com.au", "sophia.wang@ecof.com.au", "Ada.chen@ecof.com.au", "mandy.liang@ecof.com.au" } }
        };
        
        //private string BaseUrl { get { return string.Format("{0}://{1}".Request.Url.Scheme.Request.Url.Host); } }
        private string BaseUrl = "http://internal.qd.com.tw";

        public CaseLog(HttpSessionStateBase session) : this(null, session) { }

        public CaseLog(Packages package, HttpSessionStateBase session)
        {
            db = new QDLogisticsEntities();

            if (package != null)
            {
                OrderInit(package);
            }

            this.Session = session;
        }

        public void OrderInit(Packages package)
        {
            orderData = package.Orders;
            packageData = package;
            itemList = package.Items.Where(i => i.IsEnable.Value).ToList();
        }

        public CaseEvent GetCaseEvent(EnumData.CaseEventType caseType)
        {
            if (!CaseExit(caseType)) return CreateEvent(orderData.OrderID, packageData.ID, packageData.TagNo, caseType);

            return packageData.CaseEvent.First(c => c.PackageID.Equals(packageData.ID) && c.Type.Equals((byte)caseType));
        }

        public bool CaseExit(EnumData.CaseEventType caseType)
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            return packageData.CaseEvent.Where(c => c.PackageID.Equals(packageData.ID) && c.Type.Equals((byte)caseType)).Any();
        }

        public void SendTrackingMail(string directLine, List<DirectLineLabel> labelList)
        {
            if (!labelList.Any()) return;

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);

            try
            {
                MyHelp.Log("CaseEvent", null, string.Format("開始寄送 {0} - Direct Line Tracking通知", directLine), Session);

                CaseEvent eventData;
                int[] packageIDs = labelList.Select(l => l.PackageID).ToArray();
                foreach (Packages package in db.Packages.AsNoTracking().Where(p => packageIDs.Contains(p.ID)).ToList())
                {
                    OrderInit(package);

                    eventData = GetCaseEvent(EnumData.CaseEventType.UpdateTracking);
                    eventData.Request_at = DateTime.UtcNow;

                    CaseEvent.Update(eventData, eventData.ID);
                }
                CaseEvent.SaveChanges();

                switch (directLine)
                {
                    case "IDS":
                    case "IDS (US)":
                        receiveMails = MailList["IDS"];
                        //receiveMails = new string[] { "qd.tuko@hotmail.com" };
                        mailTitle = string.Format("TW018 - {0} Orders Awaiting Dispatch", labelList.Count());
                        mailBody = "Hello<br /><br />We still could not find the last mile tracking for the below packages:<br />{0}<br />...<br /><br />Please update tracking info ASAP.Thank you<br /><br />Regards<br /><br />QD Team";
                        mailBody = string.Format(mailBody, string.Join("<br />", labelList.Select(l => l.LabelID)));

                        if (!MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, null, null, false))
                        {
                            throw new Exception("寄送 IDS - Direct Line Tracking 通知失敗");
                        }
                        break;
                }

                MyHelp.Log("CaseEvent", null, string.Format("完成寄送 {0} - Direct Line Tracking通知", directLine), Session);
            }
            catch (Exception e)
            {
                string errorMsg = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;

                MyHelp.Log("CaseEvent", null, errorMsg, Session);

                throw new Exception(errorMsg);
            }
        }

        public void TrackingResponse()
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);

            CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.UpdateTracking);
            eventData.Response_at = DateTime.UtcNow;
            eventData.Request = (byte)EnumData.CaseEventRequest.Successful;
            eventData.Status = (byte)EnumData.CaseEventStatus.Close;

            CaseEvent.Update(eventData, eventData.ID);
            CaseEvent.SaveChanges();
        }

        public void SendCancelMail()
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);

            try
            {
                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】開始寄送退貨通知", orderData.OrderID), Session);

                CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.CancelShipment);
                eventData.Request_at = DateTime.UtcNow;

                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();

                DirectLine directLine = db.DirectLine.AsNoTracking().FirstOrDefault(d => d.ID.Equals(packageData.Method.DirectLine));
                if (directLine == null) throw new Exception("找不到Direct Line運輸廠商!");

                switch (directLine.Abbreviation)
                {
                    case "IDS":
                    case "IDS (US)":
                        receiveMails = MailList["IDS"];
                        //receiveMails = new string[] { "qd.tuko@hotmail.com" };
                        mailTitle = string.Format("TW018 - Cancel Shipment Request for {0} (in {1} tracking {2})", packageData.TagNo, packageData.Method.Carriers.Name, packageData.TrackingNumber);
                        mailBody = CreateCancelMailBody(directLine.Abbreviation, eventData);

                        if (!MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, null, null, false))
                        {
                            eventData.Status = (byte)EnumData.CaseEventStatus.Error;
                            CaseEvent.Update(eventData, eventData.ID);
                            CaseEvent.SaveChanges();

                            throw new Exception("寄送IDS Direct Line Cancel Shipment通知失敗");
                        }
                        break;
                    case "ECOF":
                        ShippingMethod method = db.ShippingMethod.Find(packageData.Box.FirstMileMethod);

                        //receiveMails = new string[] { "peter@qd.com.tw", "qd.tuko@hotmail.com" };
                        //List<string> CC_Mails = new List<string>();
                        receiveMails = MailList["ECOF"];
                        string labelID = string.Format("{0}-{1}-{2}", packageData.Items.First(i => i.IsEnable.Value).ProductID, packageData.OrderID, packageData.TrackingNumber);
                        mailTitle = string.Format("CANCEL SHIPMENT Request from Quality Deals for <{0}> (in <{1}> <{2}>)", labelID, method.Carriers.Name, packageData.Box.TrackingNumber);
                        mailBody = CreateCancelMailBody(directLine.Abbreviation, eventData);

                        if (!MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, null, null, false))
                        {
                            eventData.Status = (byte)EnumData.CaseEventStatus.Error;
                            CaseEvent.Update(eventData, eventData.ID);
                            CaseEvent.SaveChanges();

                            throw new Exception("寄送ECOF Direct Line Cancel Shipment通知失敗");
                        }
                        break;
                }

                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】完成寄送退貨通知", orderData.OrderID), Session);
            }
            catch (Exception e)
            {
                string errorMsg = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;

                MyHelp.Log("CaseEvent", orderData.OrderID, errorMsg, Session);

                throw new Exception(errorMsg);
            }
        }

        public void CancelShipmentResponse(byte request, int? returnWarehouseID)
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);

            CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.CancelShipment);
            try
            {
                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】開始退貨動作", orderData.OrderID), Session);

                eventData.Request = request;
                eventData.Status = (byte)EnumData.CaseEventStatus.Locked;
                eventData.Response_at = DateTime.UtcNow;
                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();

                if (eventData.Request.Equals((byte)EnumData.CaseEventRequest.Successful))
                {
                    if (Items == null) Items = new GenericRepository<Items>(db);

                    Carriers carrier = packageData.Method.Carriers;
                    DirectLine directLine = db.DirectLine.Find(packageData.Method.DirectLine);
                    switch (directLine.Abbreviation)
                    {
                        case "ECOF":
                            if (carrier.CarrierAPI != null && carrier.CarrierAPI.Type.Value.Equals((byte)EnumData.CarrierType.Sendle))
                            {
                                Sendle_Api = new Sendle_API(carrier.CarrierAPI);
                                Sendle_Api.Cancel(packageData.TagNo);
                            }

                            ShippingMethod method = db.ShippingMethod.Find(packageData.Box.FirstMileMethod);

                            receiveMails = MailList["ECOF"];
                            string labelID = string.Format("{0}-{1}-{2}", packageData.Items.First(i => i.IsEnable.Value).ProductID, packageData.OrderID, packageData.TrackingNumber);
                            mailTitle = string.Format("CONFIRMED: CANCEL SHIPMENT Request from Quality Deals for <{0}> (in <{1}> <{2}>)", labelID, method.Carriers.Name, packageData.Box.TrackingNumber);
                            mailBody = "We have confirmed that you have successfully cancelled the above order/s. Thank you!";
                            MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, null, null, false);
                            break;
                    }

                    try
                    {
                        MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】開始更新退貨倉", orderData.OrderID), Session);

                        if (SCWS == null) SCWS = new SC_WebService("tim@weypro.com", "timfromweypro");

                        var SC_order = SCWS.Get_OrderData(orderData.OrderID).Order;
                        SCWS.Update_PackageShippingStatus(SC_order.Packages.First(p => p.ID.Equals(packageData.ID)), packageData.TrackingNumber, carrier.Name);
                        var SC_items = SC_order.Items.Where(i => i.PackageID.Equals(packageData.ID)).ToArray();
                        foreach (Items item in itemList)
                        {
                            var SC_item = SC_items.First(i => i.ID.Equals(item.ID));
                            SC_item.ReturnedToWarehouseID = returnWarehouseID.Value;
                            SCWS.Update_OrderItem(SC_item);

                            item.ReturnedToWarehouseID = returnWarehouseID;
                            Items.Update(item, item.ID);
                        }
                        Items.SaveChanges();
                        SCWS.Update_OrderStatus(orderData.OrderID, (int)OrderStatusCode.Canceled);

                        try
                        {
                            using (StockKeepingUnit Stock = new StockKeepingUnit())
                            {
                                Stock.RecordOrderSkuStatement(orderData.OrderID, OrderStatusCode.Canceled.ToString());
                            }
                        }
                        catch (Exception e)
                        {
                            string errorMsg = string.Format("傳送訂單狀態至測試系統失敗，請通知處理人員：{0}", e.InnerException != null ? e.InnerException.Message.Trim() : e.Message.Trim());
                            MyHelp.Log("SkuStatement", orderData.OrderID, string.Format("訂單【{0}】{1}", orderData.OrderID, errorMsg), Session);
                        }

                        MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】 完成更新退貨倉", orderData.OrderID), Session);
                    }
                    catch (Exception e)
                    {
                        string msg = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;

                        throw new Exception(string.Format("訂單【{0}】更新退貨倉失敗! - {1}", orderData.OrderID, msg));
                    }

                    CreateRMA(returnWarehouseID.Value);
                    MoveSku();
                }

                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】完成退貨動作", orderData.OrderID), Session);
            }
            catch (Exception e)
            {
                eventData.Status = (byte)EnumData.CaseEventStatus.Error;
                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();

                MyHelp.Log("CaseEvent", orderData.OrderID, e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message, Session);
            }
        }

        private void CreateRMA(int ReturnedToWarehouseID)
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (Packages == null) Packages = new GenericRepository<Packages>(db);
            //if (SCWS == null) SCWS = new SC_WebService("tim@weypro.com", "timfromweypro");

            try
            {
                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】開始建立RMA", orderData.OrderID), Session);

                using (StockKeepingUnit stock = new StockKeepingUnit())
                {
                    packageData.RMAId = stock.CreateRMA(orderData.OrderID, ReturnedToWarehouseID);
                    Packages.Update(packageData, packageData.ID);
                    Packages.SaveChanges();
                }

                //Order order = SCWS.Get_OrderData(orderData.OrderID).Order;
                //order.OrderCreationSourceApplication = OrderCreationSourceApplicationType.PointOfSale;
                //if (SCWS.Update_Order(order))
                //{
                //    int RMAId = SCWS.Create_RMA(order.ID);

                //    MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】開始Receive RMA", orderData.OrderID), Session);
                //    foreach (Items item in itemList)
                //    {
                //        int RMAItemID = SCWS.Create_RMA_Item(item.OrderID.Value, item.ID, RMAId, item.Qty.Value, 16, "");
                //        string serialsList = string.Join(", ", item.SerialNumbers.Where(s => !string.IsNullOrEmpty(s.SerialNumber)).Select(s => s.SerialNumber).ToArray());
                //        SCWS.Receive_RMA_Item(RMAId, RMAItemID, item.ProductID, item.Qty.Value, item.ReturnedToWarehouseID.Value, serialsList);
                //        SCWS.Delete_ItemSerials(item.OrderID.Value, item.ID);
                //    }
                //    MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】完成Receive RMA", orderData.OrderID), Session);

                //    packageData.RMAId = RMAId;
                //    Packages.Update(packageData, packageData.ID);
                //    Packages.SaveChanges();

                //    order.OrderCreationSourceApplication = OrderCreationSourceApplicationType.Default;
                //    SCWS.Update_Order(order);
                //}

                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0} 完成建立RMA", orderData.OrderID), Session);
            }
            catch (Exception e)
            {
                string msg = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;

                throw new Exception(string.Format("訂單【{0}】建立RMA失敗! - {1}", orderData.OrderID, msg));
            }
        }

        private void MoveSku()
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.CancelShipment);

            try
            {
                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("記錄訂單【{0}】取消出貨產品", orderData.OrderID), Session);

                foreach (Items item in packageData.Items.Where(i => i.IsEnable.Value).ToList())
                {
                    for (int i = 0; i < item.Qty; i++)
                    {
                        db.SerialNumberForRefundLabel.Add(new SerialNumberForRefundLabel()
                        {
                            IsUsed = false,
                            oldOrderID = item.OrderID.Value,
                            oldLabelID = eventData.LabelID,
                            RMAID = packageData.RMAId.Value,
                            Sku = item.ProductID,
                            SerialNumber = item.SerialNumbers.Skip(i).Any() ? item.SerialNumbers.Skip(i).First().SerialNumber : "",
                            WarehouseID = item.ReturnedToWarehouseID.Value,
                            Create_at = DateTime.UtcNow
                        });
                    }
                }

                db.SaveChanges();

                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("記錄訂單【{0}】取消出貨產品成功", orderData.OrderID), Session);
            }
            catch (Exception e)
            {
                string msg = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;

                throw new Exception(string.Format("記錄訂單【{0}】取消出貨產品失敗! - {1}", orderData.OrderID, msg));
            }
        }

        public void SendUpdateShipmentMail()
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);

            try
            {
                CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.UpdateShipment);
                eventData.Request_at = DateTime.UtcNow;

                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();

                DirectLine directLine = db.DirectLine.AsNoTracking().FirstOrDefault(d => d.ID.Equals(packageData.Method.DirectLine));
                if (directLine == null) throw new Exception("找不到Direct Line運輸廠商!");

                switch (directLine.Abbreviation)
                {
                    case "IDS":
                    case "IDS (US)":
                        receiveMails = MailList["IDS"];
                        //receiveMails = new string[] { "qd.tuko@hotmail.com" };

                        IDS_Api = new IDS_API(packageData.Method.Carriers.CarrierAPI);
                        string methodType = IDS_Api.GetServiceType(packageData.Method.MethodType.Value);
                        mailTitle = string.Format("TW018 - Update Request for {0} (sent via {1})", packageData.TagNo, methodType);
                        mailBody = CreateUpdateShipmentMailBody(directLine.Abbreviation, methodType, eventData);

                        if (!MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, null, null, false))
                        {
                            eventData.Status = (byte)EnumData.CaseEventStatus.Error;
                            CaseEvent.Update(eventData, eventData.ID);
                            CaseEvent.SaveChanges();

                            throw new Exception("寄送IDS Direct Line Update Shipment通知失敗");
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                string errorMsg = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;

                MyHelp.Log("CaseEvent", orderData.OrderID, errorMsg, Session);

                throw new Exception(errorMsg);
            }
        }

        public void UpdateShipmentResponse(byte request)
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);

            CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.UpdateShipment);
            try
            {
                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】更新運輸進度", orderData.OrderID), Session);

                eventData.Request = request;
                eventData.Response_at = DateTime.UtcNow;

                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();
            }
            catch (Exception e)
            {
                eventData.Status = (byte)EnumData.CaseEventStatus.Error;
                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();

                MyHelp.Log("CaseEvent", orderData.OrderID, e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message, Session);
            }
        }

        public void SendChangeShippingMethodMail(int methodID, string newLabelID = null)
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);

            try
            {
                ShippingMethod method = db.ShippingMethod.AsNoTracking().FirstOrDefault(m => m.IsEnable && m.ID.Equals(methodID));
                if (method == null) throw new Exception("找不到選擇的運輸方式!!");

                DirectLine directLine = db.DirectLine.AsNoTracking().FirstOrDefault(d => d.ID.Equals(packageData.Method.DirectLine));
                if (directLine == null) throw new Exception("找不到Direct Line運輸廠商!");

                string oldTracking = packageData.TrackingNumber;
                CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.ChangeShippingMethod);
                eventData.NewLabelID = string.IsNullOrEmpty(newLabelID) ? CreateNewLabel(directLine.Abbreviation, method) : newLabelID;
                eventData.MethodID = method.ID;
                eventData.Request_at = DateTime.UtcNow;

                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();

                switch (directLine.Abbreviation)
                {
                    case "IDS":
                    case "IDS (US)":
                        receiveMails = MailList["IDS"];
                        //receiveMails = new string[] { "qd.tuko@hotmail.com" };

                        IDS_Api = new IDS_API(method.Carriers.CarrierAPI);
                        string oldMethodType = IDS_Api.GetServiceType(packageData.Method.MethodType.Value);
                        string newMethodType = IDS_Api.GetServiceType(method.MethodType.Value);
                        mailTitle = string.Format("TW018 - Change Shipping Method for {0} (from {1} to {2})", packageData.TagNo, oldMethodType, newMethodType);
                        mailBody = CreateChangeShippingMethodMailBody(directLine.Abbreviation, string.Format("{0} to {1}", oldMethodType, newMethodType), eventData);

                        if (!MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, null, null, false))
                        {
                            eventData.Status = (byte)EnumData.CaseEventStatus.Error;
                            CaseEvent.Update(eventData, eventData.ID);
                            CaseEvent.SaveChanges();

                            throw new Exception("寄送IDS Direct Line Change Shipping Method通知失敗");
                        }
                        break;
                        //case "ECOF":
                        //    receiveMails = MailList["ECOF"];

                        //    mailTitle = string.Format("Quality Deals Change Shipping Method for <{0}> (in <{1}> )", packageData.TagNo, oldTracking);
                        //    mailBody = CreateChangeShippingMethodMailBody(directLine.Abbreviation, string.Format("{0} to {1}", oldMethodType, newMethodType), eventData);

                        //    if (!MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, null, null, false))
                        //    {
                        //        eventData.Status = (byte)EnumData.CaseEventStatus.Error;
                        //        CaseEvent.Update(eventData, eventData.ID);
                        //        CaseEvent.SaveChanges();

                        //        throw new Exception("寄送IDS Direct Line Change Shipping Method通知失敗");
                        //    }
                        //    break;
                }
            }
            catch (Exception e)
            {
                string errorMsg = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;

                MyHelp.Log("CaseEvent", orderData.OrderID, errorMsg, Session);

                throw new Exception(errorMsg);
            }
        }

        private string CreateNewLabel(string directLine, ShippingMethod method)
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("開始建立訂單【{0}】新標籤號碼", orderData.OrderID), Session);

            string labelID = "";

            try
            {
                switch (directLine)
                {
                    case "IDS":
                    case "IDS (US)":
                        IDS_Api = new IDS_API(method.Carriers.CarrierAPI);
                        CreateOrderResponse IDS_Result = IDS_Api.CreateOrder(packageData, method);

                        if (!IDS_Result.status.Equals("200"))
                        {
                            var error = JsonConvert.DeserializeObject<List<List<List<object>>>>(JsonConvert.SerializeObject(IDS_Result.error));
                            var msg = JsonConvert.SerializeObject(error.SelectMany(e => e).First(e => e[0].Equals(packageData.OrderID.ToString()))[1]);
                            throw new Exception(JsonConvert.DeserializeObject<string[]>(msg)[0]);
                        }

                        labelID = IDS_Result.labels.First(l => l.salesRecordNumber.Equals(packageData.OrderID.ToString())).orderid;
                        break;
                    case "ECOF":
                        Carriers carrier = method.Carriers;
                        if (carrier.CarrierAPI != null)
                        {
                            switch (carrier.CarrierAPI.Type.Value)
                            {
                                case (byte)EnumData.CarrierType.Sendle:
                                    Sendle_Api = new Sendle_API(carrier.CarrierAPI);
                                    Sendle_API.OrderResponse Sendle_Result = Sendle_Api.Create(packageData);

                                    labelID = Sendle_Result.order_id;
                                    packageData.TrackingNumber = Sendle_Result.sendle_reference;
                                    break;
                            }
                        }
                        else
                        {
                            labelID = string.Format("{0}-{1}", packageData.Items.First(i => i.IsEnable.Value).ProductID, packageData.OrderID.Value);
                            packageData.TrackingNumber = "";
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                string msg = e.InnerException != null && string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                throw new Exception(string.Format("建立【{0}】標籤號碼失敗", directLine));
            }

            MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("成功建立訂單【{0}】新標籤號碼", orderData.OrderID), Session);

            return labelID;
        }

        public CaseEvent ChangeShippingMethodResponse(byte request)
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);
            if (Packages == null) Packages = new GenericRepository<Packages>(db);

            CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.ChangeShippingMethod);
            try
            {
                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】開始更新運輸方式", orderData.OrderID), Session);

                eventData.Request = request;
                eventData.Status = (byte)EnumData.CaseEventStatus.Locked;
                eventData.Response_at = DateTime.UtcNow;
                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();

                if (eventData.Request.Equals((byte)EnumData.CaseEventRequest.Successful))
                {
                    packageData.TrackingNumber = GetTrackingNumber();
                    packageData.ShippingMethod = eventData.MethodID;
                    packageData.TagNo = eventData.NewLabelID;
                    Packages.Update(packageData, packageData.ID);

                    eventData.Status = (byte)EnumData.CaseEventStatus.Close;
                    CaseEvent.Update(eventData, eventData.ID);
                    CaseEvent.SaveChanges();
                }

                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】完成更新運輸方式", orderData.OrderID), Session);
            }
            catch (Exception e)
            {
                eventData.Status = (byte)EnumData.CaseEventStatus.Error;
                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();

                MyHelp.Log("CaseEvent", orderData.OrderID, e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message, Session);
            }

            return eventData;
        }

        public void SendResendShipmentMail(Packages newPackage, DateTime confirmDate)
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);

            try
            {
                MyHelp.Log("CaseEvent", newPackage.OrderID.Value, "開始寄送DirectLine Resend Shipment通知", Session);

                CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.ResendShipment);
                eventData.NewLabelID = newPackage.TagNo;
                eventData.Request_at = DateTime.UtcNow;

                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();

                DirectLine directLine = db.DirectLine.AsNoTracking().FirstOrDefault(d => d.ID.Equals(packageData.Method.DirectLine));
                if (directLine == null) throw new Exception("找不到Direct Line運輸廠商!");

                switch (directLine.Abbreviation)
                {
                    case "IDS":
                    case "IDS (US)":
                        receiveMails = MailList["IDS"];
                        //receiveMails = new string[] { "qd.tuko@hotmail.com" };

                        mailTitle = string.Format("TW018 - Reship inventory {0} to {1}", eventData.LabelID, eventData.NewLabelID);
                        mailBody = CreateResendShipmentMailBody(directLine.Abbreviation, confirmDate, eventData);

                        if (!MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, null, null, false))
                        {
                            eventData.Status = (byte)EnumData.CaseEventStatus.Error;
                            CaseEvent.Update(eventData, eventData.ID);
                            CaseEvent.SaveChanges();

                            throw new Exception("寄送IDS Direct Line Resend Shipment通知失敗");
                        }
                        break;
                    case "ECOF":
                        receiveMails = MailList["ECOF"];
                        string oldLabelID = string.Format("{0}-{1}-{2}", packageData.Items.First(i => i.IsEnable.Value).ProductID, packageData.OrderID, packageData.TrackingNumber);
                        mailTitle = string.Format("Quality Deals Reship inventory for <Label ID1>", oldLabelID);
                        mailBody = CreateResendShipmentMailBody(directLine.Abbreviation, confirmDate, eventData, newPackage);

                        if (!MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, null, null, false))
                        {
                            eventData.Status = (byte)EnumData.CaseEventStatus.Error;
                            CaseEvent.Update(eventData, eventData.ID);
                            CaseEvent.SaveChanges();

                            throw new Exception("寄送IDS Direct Line Resend Shipment通知失敗");
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                string errorMsg = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;

                MyHelp.Log("CaseEvent", orderData.OrderID, errorMsg, Session);

                throw new Exception(errorMsg);
            }
        }

        public CaseEvent ResendShipmentResponse(byte request)
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);
            if (Packages == null) Packages = new GenericRepository<Packages>(db);

            CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.ResendShipment);
            try
            {
                eventData.Request = request;
                eventData.Response_at = DateTime.UtcNow;

                if (eventData.Request.Equals((byte)EnumData.CaseEventRequest.Successful))
                {
                    Packages newPackage = db.Packages.AsNoTracking().FirstOrDefault(p => p.IsEnable.Value && p.TagNo.Equals(eventData.NewLabelID));
                    OrderInit(newPackage);
                    newPackage.TrackingNumber = GetTrackingNumber();
                    Packages.Update(newPackage, newPackage.ID);
                    eventData.Status = (byte)EnumData.CaseEventStatus.Close;
                }

                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();
            }
            catch (Exception e)
            {
                eventData.Status = (byte)EnumData.CaseEventStatus.Error;
                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();

                MyHelp.Log("CaseEvent", orderData.OrderID, e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message, Session);
            }

            return eventData;
        }

        public CaseEvent ReturnPackage()
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);

            try
            {
                MyHelp.Log("CaseEvent", orderData.OrderID, "回收已出貨訂單包裹", Session);

                return GetCaseEvent(EnumData.CaseEventType.ReturnPackage);
            }
            catch (Exception e)
            {
                string errorMsg = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;

                MyHelp.Log("CaseEvent", orderData.OrderID, errorMsg, Session);

                throw new Exception(errorMsg);
            }
        }

        public void ReturnPackageResponse(byte request)
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);
            if (Packages == null) Packages = new GenericRepository<Packages>(db);

            CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.ReturnPackage);
            try
            {
                eventData.Request = request;
                eventData.Response_at = DateTime.UtcNow;
                eventData.Status = (byte)EnumData.CaseEventStatus.Close;

                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();
            }
            catch (Exception e)
            {
                eventData.Status = (byte)EnumData.CaseEventStatus.Error;
                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();

                MyHelp.Log("CaseEvent", orderData.OrderID, e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message, Session);
            }
        }

        private CaseEvent CreateEvent(int orderID, int packageID, string labelID, EnumData.CaseEventType caseType)
        {
            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);

            try
            {
                CaseEvent eventData = new CaseEvent()
                {
                    OrderID = orderID,
                    PackageID = packageID,
                    LabelID = labelID,
                    Type = (byte)caseType,
                    Create_at = DateTime.UtcNow
                };

                CaseEvent.Create(eventData);
                CaseEvent.SaveChanges();

                return eventData;
            }
            catch (Exception e)
            {
                string msg = e.InnerException != null && string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                throw new Exception("建立Case失敗! - " + msg);
            }
        }

        private string GetTrackingNumber()
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            string tracking = "";

            try
            {
                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("取得訂單【{0}】Tracking Number", orderData.OrderID), Session);

                DirectLine directLine = db.DirectLine.AsNoTracking().FirstOrDefault(d => d.ID.Equals(packageData.Method.DirectLine));
                if (directLine == null) throw new Exception("找不到Direct Line運輸廠商!");

                switch (directLine.Abbreviation)
                {
                    case "IDS":
                    case "IDS (US)":
                        IDS_Api = new IDS_API();
                        var IDS_Result = IDS_Api.GetTrackingNumber(packageData);
                        if (IDS_Result.trackingnumber.Any(t => t.First().Equals(packageData.OrderID.ToString())))
                        {
                            tracking = IDS_Result.trackingnumber.Last(t => t.First().Equals(packageData.OrderID.ToString()))[1];
                        }
                        break;
                    case "ECOF":
                        tracking = packageData.TrackingNumber;
                        break;
                }
            }
            catch (Exception e)
            {
                string msg = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;

                throw new Exception(string.Format("取得訂單【{0}】Tracking Number失敗! - {1}", orderData.OrderID, msg));
            }

            return tracking;
        }

        private string CreateCancelMailBody(string directLine, CaseEvent eventData)
        {
            string mailBody = "", failed_link;
            string receiveUrl = AddQueryString(string.Format("{0}/CaseEvent/Receive?", BaseUrl), new Dictionary<string, object> { { "caseID", eventData.ID }, { "type", (byte)EnumData.CaseEventType.CancelShipment } });

            switch (directLine)
            {
                case "IDS":
                case "IDS (US)":
                    string HK_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Successful }, { "returnWarehouseID", 163 } });
                    string UK_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Successful }, { "returnWarehouseID", 215 } });
                    string US_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Successful }, { "returnWarehouseID", 234 } });
                    failed_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Failed } });
                    mailBody = "Hi All<br /><br />Please cancel the shipment for {0} and keep it in inventory.<br /><br />";
                    mailBody += "If you have successfully cancelled in Hong Kong, click <a href='{1}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "If you have successfully cancelled in the UK, click <a href='{2}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "If you have successfully cancelled in the US, click <a href='{3}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "If you have failed to cancel, click <a href='{4}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "You can only click on either of the links above ONCE.Please make sure to choose correctly.<br /><br />Please email us if the above sitaution doesn't apply.<br /><br />Regards<br /><br />QD Shipping";
                    mailBody = string.Format(mailBody, eventData.LabelID, HK_link, UK_link, US_link, failed_link);
                    break;
                case "ECOF":
                    string labelID = string.Format("{0}-{1}-{2}", packageData.Items.First(i => i.IsEnable.Value).ProductID, packageData.OrderID, packageData.TrackingNumber);
                    string ECOF_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Successful }, { "returnWarehouseID", 118 } });
                    failed_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Failed } });
                    mailBody = "Hi<br /><br />Please cancel the shipment for <{0}> and keep it in inventory.<br /><br />";
                    mailBody += "If you have successfully cancelled in the Regents Park, click <a href='{1}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "If you have failed to cancel, click <a href='{2}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "You can only click on either of the links above ONCE.Please make sure to choose correctly.<br /><br />Please email us if the above sitaution doesn't apply.<br /><br />Regards<br /><br />QD Shipping";
                    mailBody = string.Format(mailBody, labelID, ECOF_link, failed_link);
                    break;
            }

            return mailBody;
        }

        private string CreateUpdateShipmentMailBody(string directLine, string methodType, CaseEvent eventData)
        {
            string mailBody = "";
            string receiveUrl = AddQueryString(string.Format("{0}/CaseEvent/Receive?", BaseUrl), new Dictionary<string, object> { { "caseID", eventData.ID }, { "type", (byte)EnumData.CaseEventType.UpdateShipment } });

            switch (directLine)
            {
                case "IDS":
                case "IDS (US)":
                    string investigating_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Investigating } });
                    string inTransit_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.InTransit } });
                    string lost_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Lost } });
                    mailBody = "Hi All<br /><br />Shipment {0} (sent via {1}) requires an update on the last mile shipment.<br /><br />";
                    mailBody += "If you are investigating, click <a href='{2}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "If you confirm the item is in transit, click <a href='{3}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "If the package is lost, click <a href='{4}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "You may click on the links above as the status change. Please email us if the above sitaution doesn't apply.<br /><br />Regards<br /><br />QD Shipping";
                    mailBody = string.Format(mailBody, eventData.LabelID, methodType, investigating_link, inTransit_link, lost_link);
                    break;
            }

            return mailBody;
        }

        private string CreateChangeShippingMethodMailBody(string directLine, string methodTypeChange, CaseEvent eventData, string oldTracking = null)
        {
            string mailBody = "", confirm_link, faild_link;
            string receiveUrl = AddQueryString(string.Format("{0}/CaseEvent/Receive?", BaseUrl), new Dictionary<string, object> { { "caseID", eventData.ID }, { "type", (byte)EnumData.CaseEventType.ChangeShippingMethod } });

            switch (directLine)
            {
                case "IDS":
                case "IDS (US)":
                    confirm_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Successful } });
                    faild_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Failed } });
                    mailBody = "Hi All<br /><br />The shipping method for {0} needs to change from {1}. The new code is {2}. <br /><br />";
                    mailBody += "Old: {0} <br />New: {2}<br /><br />";
                    mailBody += "If you confirm the change, click <a href='{3}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "If you could not change, click <a href='{4}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "You can only click on either of the links above ONCE. Please make sure to choose correctly.<br /><br />Please email us if the above sitaution doesn't apply.<br /><br />Regards<br /><br />QD Shipping";
                    mailBody = string.Format(mailBody, eventData.LabelID, methodTypeChange, eventData.NewLabelID, confirm_link, faild_link);
                    break;
                case "ECOF":
                    string oldLabelID = string.Format("{0}-{1}-{2}", packageData.Items.First(i => i.IsEnable.Value).ProductID, packageData.OrderID, oldTracking);
                    string newLabelID = string.Format("{0}-{1}-{2}", packageData.Items.First(i => i.IsEnable.Value).ProductID, packageData.OrderID, packageData.TrackingNumber);
                    confirm_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Successful } });
                    faild_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Failed } });
                    mailBody = "Hi All<br /><br />The shipping method for {0} needs to change from {1}. The new code is {2}. <br /><br />";
                    mailBody += "Old: {0} <br />New: {2}<br /><br />";
                    mailBody += "If you confirm the change, click <a href='{3}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "If you could not change, click <a href='{4}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "You can only click on either of the links above ONCE. Please make sure to choose correctly.<br /><br />Please email us if the above sitaution doesn't apply.<br /><br />Regards<br /><br />QD Shipping";
                    mailBody = string.Format(mailBody, oldLabelID, methodTypeChange, newLabelID, confirm_link, faild_link);
                    break;
            }

            return mailBody;
        }

        private string CreateResendShipmentMailBody(string directLine, DateTime confirmDate, CaseEvent eventData, Packages newPackage = null)
        {
            string mailBody = "", successful_link;
            string receiveUrl = AddQueryString(string.Format("{0}/CaseEvent/Receive?", BaseUrl), new Dictionary<string, object> { { "caseID", eventData.ID }, { "type", (byte)EnumData.CaseEventType.ResendShipment } });

            switch (directLine)
            {
                case "IDS":
                case "IDS (US)":
                    successful_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Successful } });
                    mailBody = "Hello<br /><br />Order ({0}) confirmed cancelled on {1} requires reship with new # {2}.<br /><br />";
                    mailBody += "Please click <a href='{3}' target='_bland'>here</a> to confirm the change.<br /><br />";
                    mailBody += "Regards<br /><br />QD Shipping";
                    mailBody = string.Format(mailBody, eventData.LabelID, confirmDate.ToString("MM/dd/yyyy hh:mm tt"), eventData.NewLabelID, successful_link);
                    break;
                case "ECOF":
                    string oldLabelID = string.Format("{0}-{1}-{2}", packageData.Items.First(i => i.IsEnable.Value).ProductID, packageData.OrderID, packageData.TrackingNumber);
                    string newLabelID = string.Format("{0}-{1}-{2}", packageData.Items.First(i => i.IsEnable.Value).ProductID, newPackage.OrderID, newPackage.TrackingNumber);
                    successful_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Successful } });
                    mailBody = "Hi<br /><br /><{0}> confirmed cancelled on <{1}> requires reship with new # <{2}>.<br /><br />";
                    mailBody += "Please click <a href='{3}' target='_bland'>here</a> to confirm the change.<br /><br />";
                    mailBody += "Regards<br /><br />QD Shipping";
                    mailBody = string.Format(mailBody, oldLabelID, confirmDate.ToString("MM/dd/yyyy hh:mm tt"), newLabelID, successful_link);
                    break;
            }

            return mailBody;
        }

        private string AddQueryString(string url, Dictionary<string, object> queryString)
        {
            var builder = new System.Text.StringBuilder(url);

            if (queryString.Any())
            {
                foreach (var query in queryString)
                {
                    builder.AppendFormat("&{0}={1}", query.Key, query.Value);
                }
            }

            return builder.ToString();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                if (CaseEvent != null) CaseEvent.Dispose();
                if (Packages != null) Packages.Dispose();
                if (Items != null) Items.Dispose();
                if (RefundLabelSerial != null) RefundLabelSerial.Dispose();
                if (SCWS != null) SCWS.Dispose();
            }

            db = null;
            orderData = null;
            packageData = null;
            itemList = null;
            IDS_Api = null;
            Sendle_Api = null;
            MailList = null;
            Session = null;
            disposed = true;
        }
    }
}