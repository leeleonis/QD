using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using QDLogistics.Filters;
using QDLogistics.Models;

namespace QDLogistics.Controllers
{
    public class InventoryController : Controller
    {
        private QDLogisticsEntities db;

        public InventoryController()
        {
            db = new QDLogisticsEntities();
        }

        [CheckSession]
        public ActionResult Cancel()
        {
            return View();
        }
    }
}