using QDLogistics.Filters;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace QDLogistics.Controllers
{
    public class TaskController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<AdminUsers> AdminUsers;
        private IRepository<TaskScheduler> TaskScheduler;

        public TaskController()
        {
            db = new QDLogisticsEntities();
        }

        [CheckSession]
        public ActionResult Index()
        {
            AdminUsers = new GenericRepository<AdminUsers>(db);

            ViewData["adminList"] = AdminUsers.GetAll(true).Where(user => user.IsEnable == true && user.IsVisible == true).ToList();
            return View();
        }
        
        public ActionResult Scheduler(string TaskIDs)
        {
            TaskScheduler = new GenericRepository<TaskScheduler>(db);

            ViewData["taskList"] = TaskScheduler.GetAll(true).Where(t => TaskIDs.Split(new char[] { '_' }).Contains(t.ID.ToString())).ToList();
            return View();
        }
    }
}