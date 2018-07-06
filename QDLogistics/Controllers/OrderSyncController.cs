using Newtonsoft.Json;
using QDLogistics.Commons;
using QDLogistics.Filters;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;
using QDLogistics.OrderService;
using SellerCloud_WebService;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace QDLogistics.Controllers
{
    public class OrderSyncController : Controller
    {
        private QDLogisticsEntities db;

        private DateTime SyncOn;
        private DateTime Today;

        public OrderSyncController()
        {
            db = new QDLogisticsEntities();

            SyncOn = DateTime.UtcNow;
            Today = new TimeZoneConvert().ConvertDateTime(EnumData.TimeZone.EST);
        }

        public ActionResult CheckNewOrder(int day)
        {
            return CheckOrder(day);
        }

        public ActionResult CheckAllOrder(int day)
        {
            return CheckOrder(day);
        }

        public ActionResult CheckOrder(int day)
        {
            SyncResult result = new SyncResult();

            try
            {
                TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
                ThreadTask threadTask = new ThreadTask(string.Format("同步{0}天訂單資料", day));

                lock (factory)
                {
                    threadTask.AddWork(factory.StartNew(Session =>
                    {
                        threadTask.Start();

                        string message = "";
                        HttpSessionStateBase session = (HttpSessionStateBase)Session;

                        try
                        {
                            SyncProcess Sync = new SyncProcess(session, factory);
                            message = Sync.Sync_Orders(day);
                        }
                        catch (DbEntityValidationException ex)
                        {
                            var errorMessages = ex.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(x => x.ErrorMessage);
                            message = string.Join("; ", errorMessages);
                        }
                        catch (Exception e)
                        {
                            message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                        }

                        return message;
                    }, Session));
                }
            }
            catch (Exception e)
            {
                return Content(JsonConvert.SerializeObject(result.set_error(e.Message)), "appllication/json");
            }

            return Content(JsonConvert.SerializeObject(result), "appllication/json");
        }

        [HttpPost]
        public ActionResult GetOrder(List<string> orderIDs)
        {
            SyncResult result = new SyncResult();

            if (!orderIDs.Any())
            {
                return Content(JsonConvert.SerializeObject(result.set_error("沒有取得訂單號碼!")), "appllication/json");
            }

            try
            {
                TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;

                foreach (string orderID in orderIDs)
                {
                    ThreadTask threadTask = new ThreadTask(string.Format("訂單管理區 - 訂單【{0}】資料同步", orderID));

                    lock (factory)
                    {
                        threadTask.AddWork(factory.StartNew(Session =>
                        {
                            threadTask.Start();

                            string message = "";
                            HttpSessionStateBase session = (HttpSessionStateBase)Session;

                            try
                            {
                                SyncProcess Sync = new SyncProcess(session);
                                message = Sync.Sync_Order(int.Parse(orderID));
                            }
                            catch (DbEntityValidationException ex)
                            {
                                var errorMessages = ex.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(x => x.ErrorMessage);
                                message = string.Join("; ", errorMessages);
                            }
                            catch (Exception e)
                            {
                                message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                            }

                            return message;
                        }, HttpContext.Session));
                    }
                }
            }
            catch (Exception e)
            {
                return Content(JsonConvert.SerializeObject(result.set_error(e.Message)), "appllication/json");
            }

            return Content(JsonConvert.SerializeObject(result), "appllication/json");
        }

        public ActionResult CheckPurchaseItem()
        {
            SyncResult result = new SyncResult();

            try
            {
                IRepository<Packages> Packages = new GenericRepository<Packages>(db);
                IRepository<Items> Items = new GenericRepository<Items>(db);

                string[] productIDs = Packages.GetAll(true).Where(p => p.ProcessStatus == (int)EnumData.ProcessStatus.待出貨).Join(Items.GetAll(true), p => p.ID, i => i.PackageID, (p, i) => i.ProductID).Distinct().ToArray();

                if (productIDs.Length == 0) return Content(JsonConvert.SerializeObject(result.set_error("沒有需要同步的產品!")), "appllication/json");

                TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
                ThreadTask threadTask = new ThreadTask("產品序號同步工作");

                lock (factory)
                {
                    threadTask.AddWork(factory.StartNew(Session =>
                    {
                        threadTask.Start();

                        string message = "";
                        HttpSessionStateBase session = (HttpSessionStateBase)Session;

                        try
                        {
                            SyncProcess Sync = new SyncProcess(session);
                            message = Sync.Sync_PurchaseItem(productIDs);
                        }
                        catch (DbEntityValidationException ex)
                        {
                            var errorMessages = ex.EntityValidationErrors.SelectMany(x => x.ValidationErrors).Select(x => x.ErrorMessage);
                            message = string.Join("; ", errorMessages);
                        }
                        catch (Exception e)
                        {
                            message = e.Message;
                        }

                        return message;
                    }, HttpContext.Session));
                }

                result.taskID = threadTask.ID;
            }
            catch (Exception e)
            {
                return Content(JsonConvert.SerializeObject(result.set_error(e.Message)), "appllication/json");
            }

            return Content(JsonConvert.SerializeObject(result), "appllication/json");
        }

        public ActionResult CheckCompany()
        {
            TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
            ThreadTask threadTask = new ThreadTask("公司資料同步工作");

            lock (factory)
            {
                threadTask.AddWork(factory.StartNew(session =>
                {
                    threadTask.Start();

                    string error = "";

                    try
                    {
                        IRepository<Companies> Companies = new GenericRepository<Companies>(db);
                        List<Companies> dbCompany = Companies.GetAll(true).ToList();

                        HttpSessionStateBase Session = (HttpSessionStateBase)session;
                        SC_WebService SCWS = new SC_WebService(Session["ApiUserName"].ToString(), Session["ApiPassword"].ToString());
                        List<Companies> companyList = SCWS.Get_AllCompany().Select(c => new Companies() { ID = c.ID, CompanyName = c.CompanyName }).ToList();

                        IEnumerable<Companies> newCompany = companyList.Except(dbCompany).ToList();
                        if (newCompany.Any())
                        {
                            foreach (Companies company in newCompany)
                            {
                                var ncompany = new Companies { CompanyName = company.CompanyName, ID = company.ID };
                                Companies.Create(ncompany);
                            }
                        }

                        IEnumerable<Companies> updateCompany = companyList.Except(companyList.Except(dbCompany)).Except(dbCompany, new CompanyComparer()).ToList();
                        if (updateCompany.Any())
                        {
                            foreach (Companies company in updateCompany)
                            {
                                Companies update = dbCompany.Find(c => c.ID == company.ID);
                                update.CompanyName = company.CompanyName;
                                Companies.Update(update, update.ID);
                            }
                        }

                        Companies.SaveChanges();
                    }
                    catch (Exception e)
                    {
                        error = e.Message;
                    }

                    return error;
                }, HttpContext.Session));
            }

            SyncResult result = new SyncResult(threadTask.ID);
            MyHelp.Log("Companies", null, "公司資料同步");

            return Content(JsonConvert.SerializeObject(result), "appllication/json");
        }

        public ActionResult CheckWarehouse()
        {
            try
            {
                TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
                ThreadTask threadTask = new ThreadTask("出貨倉資料同步工作");
                MyHelp.Log("Warehouses", null, "出貨倉資料同步");

                lock (factory)
                {
                    threadTask.AddWork(factory.StartNew(Session =>
                    {
                        threadTask.Start();

                        string message = "";
                        HttpSessionStateBase session = (HttpSessionStateBase)Session;

                        try
                        {
                            SyncProcess Sync = new SyncProcess(session);
                            message = Sync.Sync_Warehouse();
                        }
                        catch (Exception e)
                        {
                            message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                        }

                        return message;
                    }, HttpContext.Session));
                }
            }
            finally { }

            return Content(JsonConvert.SerializeObject(new { status = true, message = "Sync starting!" }), "appllication/json");
        }

        public ActionResult CheckService()
        {
            try
            {
                TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
                ThreadTask threadTask = new ThreadTask("運輸方式資料同步工作");
                MyHelp.Log("Services", null, "運輸方式資料同步");

                lock (factory)
                {
                    threadTask.AddWork(factory.StartNew(() => SyncData(threadTask, "Services")));
                }
            }
            finally { }

            return Content(JsonConvert.SerializeObject(new { status = true, message = "Sync starting!" }), "appllication/json");
        }

        public ActionResult CheckManufacturer()
        {
            try
            {
                TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
                ThreadTask threadTask = new ThreadTask("廠商資料同步工作");
                MyHelp.Log("Manufacturers", null, "廠商資料同步");

                lock (factory)
                {
                    threadTask.AddWork(factory.StartNew(() => SyncData(threadTask, "Manufacturers")));
                }
            }
            finally { }

            return Content(JsonConvert.SerializeObject(new { status = true, message = "Sync starting!" }), "appllication/json");
        }

        public ActionResult CheckSku()
        {
            try
            {
                TaskFactory factory = System.Web.HttpContext.Current.Application.Get("TaskFactory") as TaskFactory;
                ThreadTask threadTask = new ThreadTask("品號資料同步工作");
                MyHelp.Log("Skus", null, "品號資料同步");

                lock (factory)
                {
                    threadTask.AddWork(factory.StartNew(() => SyncData(threadTask, "Skus")));
                }
            }
            finally { }

            return Content(JsonConvert.SerializeObject(new { status = true, message = "Sync starting!" }), "appllication/json");
        }

        public ActionResult CheckTaskStatus(int id)
        {
            IRepository<Models.TaskScheduler> TaskScheduler = new GenericRepository<Models.TaskScheduler>(db);

            Models.TaskScheduler task = TaskScheduler.Get(id);

            if (task == null) return Content(JsonConvert.SerializeObject(new { status = false, message = "Not found!!" }), "appllication/json");

            return Content(JsonConvert.SerializeObject(new { status = task.Status < 2, message = Enum.GetName(typeof(EnumData.TaskStatus), task.Status) }), "appllication/json");
        }

        private ActionResult SyncData(string syncType, object args = null)
        {
            string Dir = Server.MapPath("~/DataSync/");
            string data = null;

            switch (syncType)
            {
                case "Orders":
                    int day = Convert.ToInt32(args.ToString());
                    Response.Write("--- Get Orders from " + Today.AddDays(-day).ToString() + " to " + Today.ToString() + " ---");
                    data = day.ToString();
                    break;
                case "PurchaseItem":
                    string[] productIDs = args as string[];
                    if (productIDs != null)
                    {
                        data = string.Join("|", productIDs);
                    }
                    break;
            }


            Process process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = Dir + "DataSync.exe",
                    Arguments = string.Format("{0} {1}", syncType, data),
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };
            process.EnableRaisingEvents = true;
            process.SynchronizingObject = null;
            process.Exited += new EventHandler(processExited);
            process.Start();

            Session[process.Id.ToString()] = process;
            return Content(JsonConvert.SerializeObject(new { status = true, message = "Sync starting!", processID = process.Id }), "appllication/json");
        }

        private void processExited(object sender, EventArgs e)
        {
            Process process = (Process)sender;

            string error = process.StandardError.ReadToEnd();
            string output = process.StandardOutput.ReadToEnd();

            Thread.Sleep(5000);
            if (process != null)
            {
                process.Close();
            }
        }

        private string SyncData(ThreadTask threadTask, string syncType, object args = null)
        {
            threadTask.Start();

            string Dir = Server.MapPath("~/DataSync/");
            string data = null;

            switch (syncType)
            {
                case "Orders":
                    int day = Convert.ToInt32(args.ToString());
                    Response.Write("--- Get Orders from " + Today.AddDays(-day).ToString() + " to " + Today.ToString() + " ---");
                    data = day.ToString();
                    break;
                case "PurchaseItem":
                    string[] productIDs = args as string[];
                    if (productIDs != null)
                    {
                        data = string.Join("|", productIDs);
                    }
                    break;
            }

            Process process = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = Dir + "DataSync.exe",
                    Arguments = string.Format("{0} {1}", syncType, data),
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };

            process.Start();
            process.WaitForExit();

            string error = process.StandardError.ReadToEnd();
            string output = process.StandardOutput.ReadToEnd();

            if (process != null)
            {
                process.Close();
                process.Dispose();
            }

            return error;
        }

        private class SyncResult
        {
            public bool status { get; set; }
            public int taskID { get; set; }
            public string message { get; set; }

            public SyncResult()
            {
                init();
            }

            public SyncResult(int id)
            {
                this.taskID = id;
                init();
            }

            private void init()
            {
                this.status = true;
                this.message = "Sync starting!";
            }

            public SyncResult set_error(string message)
            {
                this.status = false;
                this.message = message;
                return this;
            }
        }
    }
}