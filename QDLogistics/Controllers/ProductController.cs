using Newtonsoft.Json;
using QDLogistics.Filters;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;
using SellerCloud_WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace QDLogistics.Controllers
{
    public class ProductController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<Skus> Skus;
        private IRepository<ProductType> ProductType;

        public ProductController()
        {
            db = new QDLogisticsEntities();
            Skus = new GenericRepository<Skus>(db);
            ProductType = new GenericRepository<ProductType>(db);
        }

        [CheckSession]
        public ActionResult type()
        {
            return View();
        }

        [CheckSession]
        public ActionResult sku()
        {
            return View();
        }
        
        public void CheckSku()
        {
            SC_WebService SCWS = new SC_WebService(Session["ApiUserName"].ToString(), Session["ApiPassword"].ToString());

            int index = 0;
            string[] productIDs = Skus.GetAll(true).Select(s => s.Sku).ToArray();
            do
            {
                var productList = SCWS.Get_ProductFullInfos(productIDs.Skip(index += 100).Take(100).ToArray());
            } while (index < 1000);
        }
    }
}