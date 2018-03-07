using QDLogistics.Commons;
using QDLogistics.Filters;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace QDLogistics.Controllers
{
    public class CompanyController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<Companies> Companies;

        public CompanyController()
        {
            db = new QDLogisticsEntities();
            Companies = new GenericRepository<Companies>(db);
        }

        [CheckSession]
        public ActionResult Index()
        {
            return View();
        }

        [CheckSession]
        public ActionResult Edit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Companies company = Companies.Get(id.Value);
            if (company == null) return HttpNotFound();

            List<SelectListItem> list = Enum.GetValues(typeof(EnumData.TimeZone)).Cast<EnumData.TimeZone>().Select(t => new SelectListItem() { Text = EnumData.GetTimeZnoe(t), Value = ((int)t).ToString() }).ToList();

            ViewBag.list = list;
            return View(company);
        }

        [CheckSession]
        [HttpPost]
        public ActionResult Edit([Bind(Include = "ID, CompanyName, TimeZone")] Companies company)
        {
            if (!MyHelp.CheckAuth("company", "index", EnumData.AuthType.Edit)) return RedirectToAction("index", "company");

            if (ModelState.IsValid)
            {
                Companies.Update(company);
                Companies.SaveChanges();
                return RedirectToAction("edit", "company", new { id = company.ID });
            }

            List<SelectListItem> list = Enum.GetValues(typeof(EnumData.TimeZone)).Cast<EnumData.TimeZone>().Select(t => new SelectListItem() { Text = EnumData.GetTimeZnoe(t), Value = ((int)t).ToString() }).ToList();

            ViewBag.list = list;
            return View(company);
        }
    }
}