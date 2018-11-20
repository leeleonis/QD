using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using QDLogistics.Commons;
using QDLogistics.Filters;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;

namespace QDLogistics.Controllers
{
    public class CaseEventController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<DirectLineLabel> Label;
        private IRepository<CaseEvent> CaseEvent;

        public CaseEventController()
        {
            db = new QDLogisticsEntities();
        }

        [CheckSession]
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult CreateCaseEvent(int packageID, byte caseType, int methodID)
        {
            AjaxResult result = new AjaxResult();

            try
            {
                Packages package = db.Packages.AsNoTracking().First(p => p.IsEnable.Value && p.ID.Equals(packageID));
                if (package == null) throw new Exception("找不到訂單!");


                using (CaseLog CaseLog = new CaseLog(package, Session))
                {
                    switch (caseType)
                    {
                        case (byte)EnumData.CaseEventType.CancelShipment:
                            CaseLog.SendCancelMail();
                            break;

                        case (byte)EnumData.CaseEventType.UpdateShipment:
                            CaseLog.SendUpdateShipmentMail();
                            break;

                        case (byte)EnumData.CaseEventType.ChangeShippingMethod:
                            DirectLineLabel label = db.DirectLineLabel.Find(package.TagNo);
                            label.Status = (byte)EnumData.LabelStatus.完成;
                            db.Entry(label).State = System.Data.Entity.EntityState.Modified;

                            CaseLog.SendChangeShippingMethodMail(methodID);
                            break;
                    }
                }
                db.Entry(package).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();
            }
            catch (Exception e)
            {
                result.status = false;
                result.message = e.InnerException != null && string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public void Receive()
        {
            try
            {
                using (LinkReceive receive = new LinkReceive(HttpContext.Request.Url.Query))
                {
                    CaseEvent eventData = db.CaseEvent.AsNoTracking().FirstOrDefault(c => c.ID.Equals(receive.CaseID));
                    if (eventData == null) throw new Exception("找不到Case Event!");
                    if (!eventData.Status.Equals((byte)EnumData.CaseEventStatus.Open)) throw new Exception("執行動作無效!");

                    Packages package = db.Packages.AsNoTracking().FirstOrDefault(p => p.IsEnable.Value && p.ID.Equals(eventData.PackageID));
                    if (package == null) throw new Exception("找不到訂單!");

                    using (CaseLog CaseLog = new CaseLog(package, Session))
                    {
                        Label = new GenericRepository<DirectLineLabel>(db);

                        TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;

                        switch (receive.Type)
                        {
                            case (byte)EnumData.CaseEventType.CancelShipment:
                                if (!receive.Request.Equals((byte)EnumData.CaseEventRequest.Failed) && !receive.ReturnWarehouseID.HasValue) throw new Exception("沒有選擇退貨倉!");

                                CaseLog.CancelShipmentResponse(receive.Request, receive.ReturnWarehouseID);

                                eventData = CaseLog.GetCaseEvent(EnumData.CaseEventType.CancelShipment);
                                if (eventData.Request.Equals((byte)EnumData.CaseEventRequest.Successful) && eventData.Status.Equals((byte)EnumData.CaseEventStatus.Locked))
                                {
                                    ThreadTask threadTask = new ThreadTask(string.Format("Direct Line 訂單【{0}】同步資料", package.OrderID));
                                    threadTask.AddWork(factory.StartNew(() =>
                                    {
                                        threadTask.Start();
                                        SyncProcess sync = new SyncProcess(Session);
                                        return sync.Sync_Order(package.OrderID.Value);
                                    }));

                                    DirectLineLabel label = db.DirectLineLabel.AsNoTracking().First(l => l.IsEnable && l.LabelID.Equals(eventData.LabelID));
                                    label.Status = (byte)EnumData.LabelStatus.作廢;
                                    Label.Update(label, label.LabelID);
                                    Label.SaveChanges();
                                }
                                break;

                            case (byte)EnumData.CaseEventType.UpdateShipment:
                                CaseLog.UpdateShipmentResponse(receive.Request);
                                break;

                            case (byte)EnumData.CaseEventType.ChangeShippingMethod:
                                eventData = CaseLog.ChangeShippingMethodResponse(receive.Request);
                                if (eventData.Request.Equals((byte)EnumData.CaseEventRequest.Successful) && eventData.Status.Equals((byte)EnumData.CaseEventStatus.Close))
                                {
                                    package = db.Packages.Find(eventData.PackageID);
                                    if (!string.IsNullOrEmpty(package.TrackingNumber))
                                    {
                                        ThreadTask threadTask = new ThreadTask(string.Format("Direct Line 訂單【{0}】SC更新", package.OrderID));
                                        threadTask.AddWork(factory.StartNew(() =>
                                        {
                                            threadTask.Start();
                                            SyncProcess sync = new SyncProcess(Session);
                                            return sync.Update_Tracking(package);
                                        }));
                                    }
                                }
                                break;

                            case (byte)EnumData.CaseEventType.ResendShipment:
                                eventData = CaseLog.ResendShipmentResponse(receive.Request);
                                if (eventData.Request.Equals((byte)EnumData.CaseEventRequest.Successful) && eventData.Status.Equals((byte)EnumData.CaseEventStatus.Close))
                                {
                                    DirectLineLabel label = db.DirectLineLabel.AsNoTracking().First(l => l.IsEnable && l.LabelID.Equals(eventData.NewLabelID));
                                    package = db.Packages.AsNoTracking().First(p => p.ID.Equals(label.PackageID));
                                    if (!string.IsNullOrEmpty(package.TrackingNumber))
                                    {
                                        label.Status = (byte)EnumData.LabelStatus.完成;

                                        ThreadTask threadTask = new ThreadTask(string.Format("Direct Line 訂單【{0}】SC更新", package.OrderID));
                                        threadTask.AddWork(factory.StartNew(() =>
                                        {
                                            threadTask.Start();
                                            SyncProcess sync = new SyncProcess(Session);
                                            return sync.Update_Tracking(package);
                                        }));
                                    }

                                    Label.Update(label, label.LabelID);
                                    Label.SaveChanges();
                                }
                                break;

                            case (byte)EnumData.CaseEventType.ReturnPackage:
                                CaseLog.ReturnPackageResponse(receive.Request);
                                break;
                        }
                    }

                }

                Response.Write("Success!");
            }
            catch (Exception e)
            {
                Response.Write(e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message);
            }
        }

        public ActionResult GetCaseEventData(CaseFilter filter, int page = 1, int rows = 100)
        {
            int total = 0;
            List<object> dataList = new List<object>();

            TimeZoneConvert timeZoneConvert = new TimeZoneConvert();
            EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);

            var CaseFilter = db.CaseEvent.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(filter.OrderID)) CaseFilter = CaseFilter.Where(c => c.OrderID.ToString().Equals(filter.OrderID));
            if (!string.IsNullOrEmpty(filter.LabelID)) CaseFilter = CaseFilter.Where(c => c.LabelID.ToString().Equals(filter.LabelID));
            if (filter.CreateDate.HasValue)
            {
                DateTime dateFrom = timeZoneConvert.InitDateTime(filter.CreateDate.Value, TimeZone).Utc;
                DateTime dateTO = timeZoneConvert.Utc.AddDays(1);
                CaseFilter = CaseFilter.Where(c => c.Create_at.CompareTo(dateFrom) >= 0 && c.Create_at.CompareTo(dateTO) < 0);
            }
            if (filter.RequestDate.HasValue)
            {
                DateTime dateFrom = timeZoneConvert.InitDateTime(filter.RequestDate.Value, TimeZone).Utc;
                DateTime dateTO = timeZoneConvert.Utc.AddDays(1);
                CaseFilter = CaseFilter.Where(c => c.Request_at.HasValue && c.Request_at.Value.CompareTo(dateFrom) >= 0 && c.Request_at.Value.CompareTo(dateTO) < 0);
            }
            if (filter.ResponseDate.HasValue)
            {
                DateTime dateFrom = timeZoneConvert.InitDateTime(filter.ResponseDate.Value, TimeZone).Utc;
                DateTime dateTO = timeZoneConvert.Utc.AddDays(1);
                CaseFilter = CaseFilter.Where(c => c.Response_at.HasValue && c.Response_at.Value.CompareTo(dateFrom) >= 0 && c.Response_at.Value.CompareTo(dateTO) < 0);
            }
            if (filter.CaseType.HasValue) CaseFilter = CaseFilter.Where(c => c.Type.Equals(filter.CaseType.Value));
            if (filter.CaseRequest.HasValue) CaseFilter = CaseFilter.Where(c => c.Request.Equals(filter.CaseRequest.Value));
            if (filter.CaseStatus.HasValue) CaseFilter = CaseFilter.Where(c => c.Status.Equals(filter.CaseStatus.Value));

            List<CaseEvent> results = CaseFilter.ToList();
            if (results.Any())
            {
                int length = rows;
                int start = (page - 1) * length;
                total = results.Count();

                if (!string.IsNullOrEmpty(filter.Sort) && filter.Sort.Equals("RequestDate"))
                    results = filter.Order.Equals("asc") ? results.OrderBy(c => c.Request_at).ToList() : results.OrderByDescending(c => c.Request_at).ToList();

                Dictionary<int, string> DirectLine = db.DirectLine.ToDictionary(d => d.ID, d => d.Abbreviation);
                Dictionary<int, string> AdminName = db.AdminUsers.AsNoTracking().Where(u => u.IsEnable).ToDictionary(u => u.Id, u => u.Name);

                dataList.AddRange(results.Skip(start).Take(length).Select(c => new
                {
                    CaseID = c.ID,
                    c.OrderID,
                    LabelID = DirectLine[c.Packages.Method.DirectLine].Equals("Sendle") ? string.Format("{0}-{1}-{2}", c.Packages.Items.First(i => i.IsEnable.Value).ProductID, c.OrderID, c.Packages.TrackingNumber) : c.LabelID,
                    CreateDate = timeZoneConvert.InitDateTime(c.Create_at, EnumData.TimeZone.UTC).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy tt hh:mm"),
                    RequestDate = c.Request_at.HasValue ? timeZoneConvert.InitDateTime(c.Request_at.Value, EnumData.TimeZone.UTC).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy tt hh:mm") : "",
                    ResponseDate = c.Response_at.HasValue ? timeZoneConvert.InitDateTime(c.Response_at.Value, EnumData.TimeZone.UTC).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy tt hh:mm") : "",
                    CaseType = EnumData.CaseEventTypeList()[(EnumData.CaseEventType)c.Type],
                    CaseRequest = Enum.GetName(typeof(EnumData.CaseEventRequest), c.Request),
                    CaseStatus = c.Status,
                    UpdateBy = AdminName.ContainsKey(c.Update_by) ? string.Format("{0}﹙{1}﹚", AdminName[c.Update_by], DirectLine[c.Packages.Method.DirectLine]) : DirectLine[c.Packages.Method.DirectLine]
                }));
            }

            return Json(new { total, rows = dataList }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetSelectOption(List<string> optionType)
        {
            AjaxResult result = new AjaxResult();

            try
            {
                if (!optionType.Any()) throw new Exception("沒有給項目!");

                Dictionary<string, object> optionList = new Dictionary<string, object>();

                foreach (string type in optionType)
                {
                    switch (type)
                    {
                        case "CaseType":
                            optionList.Add(type, EnumData.CaseEventTypeList().Select(t => new { text = t.Value.ToString(), value = (byte)t.Key }).ToArray());
                            break;
                        case "CaseRequest":
                            optionList.Add(type, Enum.GetValues(typeof(EnumData.CaseEventRequest)).Cast<EnumData.CaseEventRequest>().Select(r => new { text = r.ToString(), value = (byte)r }).ToArray());
                            break;
                        case "CaseStatus":
                            optionList.Add(type, Enum.GetValues(typeof(EnumData.CaseEventStatus)).Cast<EnumData.CaseEventStatus>().Select(s => new { text = s.ToString(), value = (byte)s }).ToArray());
                            break;
                    }
                }

                result.data = optionList;
            }
            catch (Exception e)
            {
                result.status = false;
                result.message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult UpdateStatus(int CaseID, byte CaseStatus)
        {
            AjaxResult result = new AjaxResult();

            CaseEvent = new GenericRepository<CaseEvent>(db);

            try
            {
                CaseEvent eventData = CaseEvent.Get(CaseID);
                if (eventData == null) throw new Exception("找不到資料!");

                eventData.Status = CaseStatus;
                eventData.Update_by = int.Parse(Session["AdminId"].ToString());
                CaseEvent.Update(eventData, eventData.ID);
                CaseEvent.SaveChanges();
            }
            catch (Exception e)
            {
                result.status = false;
                result.message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public class CaseFilter
        {
            private string OrderIDField { get; set; }
            private string LabelIDField { get; set; }

            public string OrderID { get { return this.OrderIDField; } set { this.OrderIDField = !string.IsNullOrEmpty(value) ? value.Trim() : value; } }
            public string LabelID { get { return this.LabelIDField; } set { this.LabelIDField = !string.IsNullOrEmpty(value) ? value.Trim() : value; } }
            public DateTime? CreateDate { get; set; }
            public DateTime? RequestDate { get; set; }
            public DateTime? ResponseDate { get; set; }
            public byte? CaseType { get; set; }
            public byte? CaseRequest { get; set; }
            public byte? CaseStatus { get; set; }

            public string Sort { get; set; }
            public string Order { get; set; }
        }

        public class LinkReceive : IDisposable
        {
            public int CaseID;
            public byte Type;
            public byte Request;
            public Nullable<int> ReturnWarehouseID;

            private bool disposed = false;

            public LinkReceive(string queryString)
            {
                System.Collections.Specialized.NameValueCollection queryData = HttpUtility.ParseQueryString(queryString);

                if (!int.TryParse(queryData.Get("caseID"), out this.CaseID) || !byte.TryParse(queryData.Get("type"), out this.Type) || !byte.TryParse(queryData.Get("request"), out this.Request))
                {
                    throw new Exception("資料不完整!");
                }

                if (!string.IsNullOrEmpty(queryData.Get("returnWarehouseID"))) this.ReturnWarehouseID = int.Parse(queryData.Get("returnWarehouseID"));
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
                }

                disposed = true;
            }
        }
        public class AjaxResult
        {
            public bool status { get; set; }
            public string message { get; set; }
            public object data { get; set; }

            public AjaxResult()
            {
                this.status = true;
                this.message = null;
                this.data = null;
            }
        }
    }
}