using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using DirectLineApi.IDS;
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

        private Orders orderData;
        private Packages packageData;
        private List<Items> itemList;

        private IDS_API IDS_Api;
        private SC_WebService SCWS;

        private bool disposed = false;
        private HttpSessionStateBase session;
        private string baseUrl = string.Format("{0}://{1}", HttpContext.Current.Request.Url.Scheme, HttpContext.Current.Request.Url.Host);

        private string sendMail = "dispatch-qd@hotmail.com";
        private string mailTitle;
        private string mailBody;
        private string[] receiveMails;
        //private string[] ccMails = new string[] { "peter@qd.com.tw", "kelly@qd.com.tw", "demi@qd.com.tw" };
        private string[] ccMails = new string[] { };

        public CaseLog(HttpSessionStateBase session) : this(null, session) { }

        public CaseLog(Packages package, HttpSessionStateBase session)
        {
            db = new QDLogisticsEntities();

            if (package != null)
            {
                OrderInit(package);
            }

            this.session = session;
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

            return db.CaseEvent.AsNoTracking().First(c => c.PackageID.Equals(packageData.ID) && c.LabelID.Equals(packageData.TagNo) && c.Type.Equals((byte)caseType));
        }

        public bool CaseExit(EnumData.CaseEventType caseType)
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            return db.CaseEvent.AsNoTracking().Where(c => c.PackageID.Equals(packageData.ID) && c.LabelID.Equals(packageData.TagNo) && c.Type.Equals((byte)caseType)).Any();
        }

        public void SendTrackingMail(string directLine, List<DirectLineLabel> labelList)
        {
            if (!labelList.Any()) return;

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);

            try
            {
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
                        receiveMails = new string[] { "gloria.chiu@contin-global.com", "cherry.chen@contin-global.com", "TWCS@contin-global.com", "contincs@gmail.com" };
                        //receiveMails = new string[] { "qd.tuko@hotmail.com" };
                        mailTitle = string.Format("TW018 - {0} Orders Awaiting Dispatch", labelList.Count());
                        mailBody = "Hello<br /><br />We still could not find the last mile tracking for the below packages:<br />{0}<br />...<br /><br />Please update tracking info ASAP.Thank you<br /><br />Regards<br /><br />QD Team";
                        mailBody = string.Format(mailBody, string.Join("<br />", labelList.Select(l => l.LabelID)));

                        if (!MyHelp.Mail_Send(sendMail, receiveMails, ccMails, mailTitle, mailBody, true, null, null, false))
                        {
                            throw new Exception("寄送IDS Direct Line Tracking通知失敗");
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                MyHelp.Log("CaseEvent", null, e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message, session);
            }
        }

        public void TrackingResponse()
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);

            CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.UpdateTracking);
            eventData.Response_at = DateTime.UtcNow;
            eventData.Request = (byte)EnumData.CaseEventRequest.Successful;

            CaseEvent.Update(eventData, eventData.ID);
            CaseEvent.SaveChanges();
        }

        public void SendCancelMail()
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);

            try
            {
                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】開始寄送退貨通知", orderData.OrderID), session);

                CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.CancelShipment);
                eventData.Request_at = DateTime.UtcNow;

                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();

                DirectLine directLine = db.DirectLine.AsNoTracking().FirstOrDefault(d => d.ID.Equals(packageData.Method.DirectLine));
                if (directLine == null) throw new Exception("找不到Direct Line運輸廠商!");

                switch (directLine.Abbreviation)
                {
                    case "IDS":
                        //receiveMails = new string[] { "gloria.chiu@contin-global.com", "cherry.chen@contin-global.com", "TWCS@contin-global.com", "contincs@gmail.com" };
                        receiveMails = new string[] { "qd.tuko@hotmail.com" };
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
                }

                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】完成寄送退貨通知", orderData.OrderID), session);
            }
            catch (Exception e)
            {
                MyHelp.Log("CaseEvent", orderData.OrderID, e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message, session);
            }
        }

        public void CancelShipmentResponse(byte request, int returnWarehouseID)
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);
            if (Items == null) Items = new GenericRepository<Items>(db);

            CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.CancelShipment);
            try
            {
                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】開始退貨動作", orderData.OrderID), session);

                eventData.Request = request;
                eventData.Status = (byte)EnumData.CaseEventStatus.Locked;
                eventData.Request_at = DateTime.UtcNow;
                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();

                if (eventData.Request.Equals((byte)EnumData.CaseEventRequest.Successful))
                {
                    try
                    {
                        MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】開始更新退貨倉", orderData.OrderID), session);

                        if (SCWS != null) SCWS = new SC_WebService("tim@weypro.com", "timfromweypro");

                        var SC_items = SCWS.Get_OrderData(orderData.OrderID).Order.Items;
                        foreach (Items item in itemList)
                        {
                            SC_items.First(i => i.ID.Equals(item.ID)).ReturnedToWarehouseID = returnWarehouseID;
                            item.ReturnedToWarehouseID = returnWarehouseID;
                            Items.Update(item, item.ID);
                        }

                        SCWS.Update_OrderItem(SC_items);
                        Items.SaveChanges();

                        MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0} 完成更新退貨倉", orderData.OrderID), session);
                    }
                    catch (Exception e)
                    {
                        string msg = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;

                        throw new Exception(string.Format("訂單【{0}】更新退貨倉失敗! - {1}", orderData.OrderID, msg));
                    }

                    CreateRMA();
                }

                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0} 完成退貨動作", orderData.OrderID), session);
            }
            catch (Exception e)
            {
                eventData.Status = (byte)EnumData.CaseEventStatus.Error;
                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();

                MyHelp.Log("CaseEvent", orderData.OrderID, e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message, session);
            }
        }

        private void CreateRMA()
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (Packages == null) Packages = new GenericRepository<Packages>(db);
            if (SCWS != null) SCWS = new SC_WebService("tim@weypro.com", "timfromweypro");

            try
            {
                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0}】開始建立RMA", orderData.OrderID), session);

                Order order = SCWS.Get_OrderData(orderData.OrderID).Order;
                order.OrderCreationSourceApplication = OrderCreationSourceApplicationType.PointOfSale;
                if (SCWS.Update_Order(order))
                {
                    int RMAId = SCWS.Create_RMA(order.ID);

                    if (itemList.Any())
                    {
                        foreach (Items item in itemList)
                        {
                            SCWS.Create_RMA_Item(item.OrderID.Value, item.ID, RMAId, item.Qty.Value, 16, "");

                            if (item.BundleItems.Any())
                            {
                                foreach (var bundleItem in item.BundleItems.ToList())
                                {
                                    SCWS.Create_RMA_Item(bundleItem.OrderID.Value, bundleItem.ID, RMAId, bundleItem.Qty.Value, 16, "");
                                }
                            }
                        }
                    }

                    packageData.RMAId = RMAId;
                    Packages.Update(packageData, packageData.ID);
                    Packages.SaveChanges();

                    order.OrderCreationSourceApplication = OrderCreationSourceApplicationType.Default;
                    SCWS.Update_Order(order);
                }

                MyHelp.Log("CaseEvent", orderData.OrderID, string.Format("訂單【{0} 完成建立RMA", orderData.OrderID), session);
            }
            catch (Exception e)
            {
                string msg = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;

                throw new Exception(string.Format("訂單【{0}】建立RMA失敗! - {1}", orderData.OrderID, msg));
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
                        //receiveMails = new string[] { "gloria.chiu@contin-global.com", "cherry.chen@contin-global.com", "TWCS@contin-global.com", "contincs@gmail.com" };
                        receiveMails = new string[] { "qd.tuko@hotmail.com" };

                        IDS_Api = new IDS_API();
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
                MyHelp.Log("CaseEvent", orderData.OrderID, e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message, session);
            }
        }

        public void UpdateShipmentResponse(byte request)
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);

            try
            {
                CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.UpdateShipment);
                eventData.Request = request;
                eventData.Request_at = DateTime.UtcNow;

                if (eventData.Request.Equals((byte)EnumData.CaseEventRequest.InTransit))
                {
                    eventData.Status = (byte)EnumData.CaseEventStatus.Locked;
                }

                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();
            }
            catch (Exception e)
            {
                MyHelp.Log("CaseEvent", orderData.OrderID, e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message, session);
            }
        }

        public void SendChangeShippingMethodMail(int methodID)
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);

            try
            {
                ShippingMethod method = db.ShippingMethod.AsNoTracking().FirstOrDefault(m => m.IsEnable && m.ID.Equals(methodID));
                if (method == null) throw new Exception("找不到選擇的運輸方式!!");

                CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.ChangeShippingMethod);
                eventData.MethodID = methodID;
                eventData.Request_at = DateTime.UtcNow;

                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();

                DirectLine directLine = db.DirectLine.AsNoTracking().FirstOrDefault(d => d.ID.Equals(packageData.Method.DirectLine));
                if (directLine == null) throw new Exception("找不到Direct Line運輸廠商!");

                switch (directLine.Abbreviation)
                {
                    case "IDS":
                        //receiveMails = new string[] { "gloria.chiu@contin-global.com", "cherry.chen@contin-global.com", "TWCS@contin-global.com", "contincs@gmail.com" };
                        receiveMails = new string[] { "qd.tuko@hotmail.com" };

                        IDS_Api = new IDS_API();
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
                }
            }
            catch (Exception e)
            {
                MyHelp.Log("CaseEvent", orderData.OrderID, e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message, session);
            }
        }

        public void ChangeShippingMethodResponse(byte request)
        {
            if (packageData == null) throw new Exception("未設定訂單!");

            if (CaseEvent == null) CaseEvent = new GenericRepository<CaseEvent>(db);
            if (Packages == null) Packages = new GenericRepository<Packages>(db);

            try
            {
                CaseEvent eventData = GetCaseEvent(EnumData.CaseEventType.UpdateShipment);
                eventData.Request = request;
                eventData.Request_at = DateTime.UtcNow;
                
                packageData.ShippingMethod = eventData.MethodID;
                Packages.Update(packageData, packageData.ID);

                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();
            }
            catch (Exception e)
            {
                MyHelp.Log("CaseEvent", orderData.OrderID, e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message, session);
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
                throw new Exception("建立Case失敗!");
            }
        }

        private string CreateCancelMailBody(string directLine, CaseEvent eventData)
        {
            string mailBody = "";
            string receiveUrl = AddQueryString(string.Format("{0}/CaseEvent/Receive?", baseUrl), new Dictionary<string, object> { { "caseID", eventData.ID }, { "type", (byte)EnumData.CaseEventType.CancelShipment } });

            switch (directLine)
            {
                case "IDS":
                    string HK_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Successful } });
                    string UK_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Successful } });
                    string failed_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Failed } });
                    mailBody = "Hi All<br /><br />Please cancel the shipment for {0} and keep it in inventory.<br /><br />";
                    mailBody += "If you have successfully cancelled in Hong Kong, click <a href='{1}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "If you have successfully cancelled in the UK, click <a href='{2}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "If you have failed to cancel, click <a href='{3}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "You can only click on either of the links above ONCE.Please make sure to choose correctly.<br /><br />Please email us if the above sitaution doesn't apply.<br /><br />Regards<br /><br />QD Shipping";
                    mailBody = string.Format(mailBody, eventData.LabelID, HK_link, UK_link, failed_link);
                    break;
            }

            return mailBody;
        }

        private string CreateUpdateShipmentMailBody(string directLine, string methodType, CaseEvent eventData)
        {
            string mailBody = "";
            string receiveUrl = AddQueryString(string.Format("{0}/CaseEvent/Receive?", baseUrl), new Dictionary<string, object> { { "caseID", eventData.ID }, { "type", (byte)EnumData.CaseEventType.UpdateShipment } });

            switch (directLine)
            {
                case "IDS":
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
        private string CreateChangeShippingMethodMailBody(string directLine, string methodTypeChange, CaseEvent eventData)
        {
            string mailBody = "";
            string receiveUrl = AddQueryString(string.Format("{0}/CaseEvent/Receive?", baseUrl), new Dictionary<string, object> { { "caseID", eventData.ID }, { "type", (byte)EnumData.CaseEventType.ChangeShippingMethod } });

            switch (directLine)
            {
                case "IDS":
                    string confirm_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Successful } });
                    string faild_link = AddQueryString(receiveUrl, new Dictionary<string, object>() { { "request", (byte)EnumData.CaseEventRequest.Failed } });
                    mailBody = "Hi All<br /><br />The shipping method for {0} needs to change from {1}.<br /><br />";
                    mailBody += "If you confirm the change, click <a href='{2}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "If you could not change, click <a href='{3}' target='_bland'>here</a>.<br /><br />";
                    mailBody += "You can only click on either of the links above ONCE. Please make sure to choose correctly.<br /><br />Please email us if the above sitaution doesn't apply.<br /><br />Regards<br /><br />QD Shipping";
                    mailBody = string.Format(mailBody, eventData.LabelID, methodTypeChange, confirm_link, faild_link);
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
            }

            db.Dispose();
            db = null;
            orderData = null;
            packageData = null;
            itemList = null;
            IDS_Api = null;
            SCWS = null;
            session = null;
            disposed = true;
        }
    }
}