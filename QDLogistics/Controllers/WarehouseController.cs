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
        private IRepository<ShippingMethod> Method;
        
        public WarehouseController()
        {
            db = new QDLogisticsEntities();
            Warehouses = new GenericRepository<Warehouses>(db);
            Method = new GenericRepository<ShippingMethod>(db);
        }

        [CheckSession]
        public ActionResult Index()
        {
            ViewBag.MethodList = Method.GetAll(true).Where(m => m.IsEnable).OrderBy(m => m.ID);
            return View();
        }
    }
}