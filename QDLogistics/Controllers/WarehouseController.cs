using QDLogistics.Filters;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;
using QDLogistics.OrderService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace QDLogistics.Controllers
{
    public class WarehouseController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<Warehouses> Warehouses;
        private IRepository<Carriers> Carriers;
        
        public WarehouseController()
        {
            db = new QDLogisticsEntities();
            Warehouses = new GenericRepository<Warehouses>(db);
            Carriers = new GenericRepository<Carriers>(db);
        }

        [CheckSession]
        public ActionResult Index()
        {
            ViewBag.Carriers = Carriers.GetAll(true).Where(c => c.IsEnable == true).OrderBy(c => c.ID);
            return View();
        }
    }
}