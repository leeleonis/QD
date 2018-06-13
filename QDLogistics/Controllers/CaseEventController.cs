using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using QDLogistics.Commons;
using QDLogistics.Models;

namespace QDLogistics.Controllers
{
    public class CaseEventController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<CaseEvent> CaseEvent;

        public CaseEventController()
        {
            db = new QDLogisticsEntities();
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

                    Packages package = db.Packages.AsNoTracking().FirstOrDefault(p => p.ID.Equals(eventData.PackageID));
                    if (package == null) throw new Exception("找不到訂單!");

                    using (CaseLog CaseLog = new CaseLog(package, Session))
                    {
                        switch (receive.Type)
                        {
                            case (byte)EnumData.CaseEventType.CancelShipment:
                                if (!receive.ReturnWarehouseID.HasValue) throw new Exception("沒有選擇退貨倉!");

                                CaseLog.CancelShipmentResponse(receive.Request, receive.ReturnWarehouseID.Value);
                                break;

                            case (byte)EnumData.CaseEventType.ChangeShippingMethod:
                                CaseLog.ChangeShippingMethodResponse(receive.Request);
                                break;

                            case (byte)EnumData.CaseEventType.UpdateShipment:
                                CaseLog.UpdateShipmentResponse(receive.Request);
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
    }
}