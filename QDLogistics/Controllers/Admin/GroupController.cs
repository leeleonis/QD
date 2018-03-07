using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using QDLogistics.Models;
using QDLogistics.Models.Object;
using QDLogistics.Models.Repositiry;
using QDLogistics.Filters;
using QDLogistics.Commons;

namespace QDLogistics.Controllers.Admin
{
    public class GroupController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<AdminGroups> AdminGroups;
        private IRepository<Menu> Menu;
        
        public GroupController()
        {
            db = new QDLogisticsEntities();
            AdminGroups = new GenericRepository<AdminGroups>(db);
            Menu = new GenericRepository<Menu>(db);
        }

        [CheckSession]
        public ActionResult Index([Bind(Include = "start,length,search")] RouteValue routeValue)
        {
            ViewBag.routeValue = routeValue;
            return View("~/Views/admin/groups/index.cshtml");
        }
        
        public ActionResult Create()
        {
            if (!MyHelp.CheckAuth("group", "index", EnumData.AuthType.Insert)) return RedirectToAction("index", "main");

            AdminGroups newGroup = new AdminGroups();
            newGroup.Order = AdminGroups.GetAll().Where(g => g.IsEnable).Count() + 1;
            AdminGroups.Create(newGroup);
            AdminGroups.SaveChanges();

            MyHelp.Log("AdminGroups", null, "新增管理員群組");
            return RedirectToAction("edit", new { ID = newGroup.Id });
        }

        public ActionResult Edit(int? id, [Bind(Include = "start,length,search")] RouteValue routeValue)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            AdminGroups group = AdminGroups.Get(id.Value);
            if (group == null) return HttpNotFound();

            ViewBag.routeValue = routeValue;
            ViewBag.menuList = Menu.GetAll().Where(m => m.IsEnable == true && m.PrevId == 0).OrderBy(m => m.Order).ToList();
            return View("~/Views/admin/groups/edit.cshtml", group);
        }

        [HttpPost]
        public ActionResult Edit(int id, AdminGroups group, Dictionary<string, List<bool>> auth, [Bind(Include = "start,length,search")] RouteValue routeValue)
        {
            if (!MyHelp.CheckAuth("group", "index", EnumData.AuthType.Edit)) return RedirectToAction("index", "main");

            group.Auth = JsonConvert.SerializeObject(auth.ToDictionary(a => Convert.ToInt32(a.Key), a => a.Value));
            if (group!= null)
            {
                AdminGroups.Update(group);
                AdminGroups.SaveChanges();
                routeValue.id = id;

                MyHelp.Log("AdminGroups", group.Id, "編輯管理員群組");
                return RedirectToAction("edit", "group", routeValue);
            }

            ViewBag.routeValue = routeValue;
            ViewBag.menuList = Menu.GetAll().Where(m => m.IsEnable == true && m.PrevId == 0).OrderBy(m => m.Order).ToList();
            return View("~/Views/admin/groups/edit.cshtml", group);
        }
        
        public ActionResult Delete(int id, [Bind(Include = "start,length,search")] RouteValue routeValue)
        {
            if (!MyHelp.CheckAuth("group", "index", EnumData.AuthType.Insert)) return RedirectToAction("index", "main");

            AdminGroups group = AdminGroups.Get(id);
            if (group == null) return HttpNotFound();

            group.IsEnable = false;
            AdminGroups.Update(group);
            AdminGroups.SaveChanges();

            MyHelp.Log("AdminGroups", group.Id, "刪除管理員群組");

            IEnumerable<AdminGroups> results = AdminGroups.GetAll().Where(g => g.IsEnable).OrderBy(g => g.Order).ToList();
            if (results.Any())
            {
                int order = 1;
                foreach (AdminGroups data in results)
                {
                    data.Order = order++;
                    AdminGroups.Update(data);
                }
            }
            AdminGroups.SaveChanges();

            return RedirectToAction("index", "group", routeValue);
        }
    }
}