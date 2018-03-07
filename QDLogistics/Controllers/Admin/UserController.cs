using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using QDLogistics.Models;
using QDLogistics.Models.Object;
using QDLogistics.Models.Repositiry;
using QDLogistics.Filters;
using Newtonsoft.Json;
using QDLogistics.Commons;
using QDLogistics.OrderService;

namespace QDLogistics.Controllers.Admin
{
    public class UserController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<Warehouses> Warehouses;
        private IRepository<AdminGroups> AdminGroups;
        private IRepository<AdminUsers> AdminUsers;

        public UserController()
        {
            db = new QDLogisticsEntities();
            AdminGroups = new GenericRepository<AdminGroups>(db);
            AdminUsers = new GenericRepository<AdminUsers>(db);
        }

        [CheckSession]
        public ActionResult Index([Bind(Include = "start,length,search,gId")] RouteValue routeValue)
        {
            ViewBag.groupList = AdminGroups.GetAll(true).Where(g => g.IsEnable && g.IsVisible).OrderBy(g => g.Order).ToList();

            ViewBag.routeValue = routeValue;
            return View("~/Views/admin/users/index.cshtml");
        }
        
        public ActionResult Create()
        {
            if (!MyHelp.CheckAuth("user", "index", EnumData.AuthType.Insert)) return RedirectToAction("index", "main");

            var group = AdminGroups.GetAll(true).First(g => g.IsEnable && g.IsVisible);
            if (group != null)
            {
                AdminUsers newUser = new AdminUsers() { GroupId = group.Id };
                AdminUsers.Create(newUser);
                AdminUsers.SaveChanges();

                MyHelp.Log("AdminUsers", null, "新增管理員");
                return RedirectToAction("edit", new { ID = newUser.Id });
            }

            return RedirectToAction("index");
        }
        
        public ActionResult Edit(int? id, [Bind(Include = "start,length,search,gId")] RouteValue routeValue)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            AdminUsers user = AdminUsers.Get(id.Value);
            if (user == null) return HttpNotFound();

            Warehouses = new GenericRepository<Warehouses>(db);
            ViewBag.warehouseList = Warehouses.GetAll(true).Where(w => w.IsEnable == true && w.IsSellable == true).ToList();
            ViewBag.timeZoneList = new Dictionary<EnumData.TimeZone, string>() {
                { EnumData.TimeZone.EST, "Eastern Standard Time" }, { EnumData.TimeZone.TST, "Taipei Standard Time" }, { EnumData.TimeZone.PST, "Pacific Standard Time" },
                { EnumData.TimeZone.GMT, "Greenwich Mean Time" }, { EnumData.TimeZone.AEST, "AUS Eastern Standard Time" }, { EnumData.TimeZone.JST, "Tokyo Standard Time" }
            };

            IEnumerable<AdminGroups> groupList = AdminGroups.GetAll(true).Where(g => g.IsEnable && g.IsVisible).OrderBy(g => g.Order);
            ViewData["groupList"] = new SelectList(groupList, "Id", "name", user.GroupId);

            ViewBag.routeValue = routeValue;
            return View("~/Views/admin/users/edit.cshtml", user);
        }

        [HttpPost]
        public ActionResult Edit(int id, AdminUsers data, List<int> warehouse, [Bind(Include = "start,length,search,gId,id")] RouteValue routeValue)
        {
            if (!MyHelp.CheckAuth("user", "index", EnumData.AuthType.Edit)) return RedirectToAction("index", "main");

            AdminUsers user = AdminUsers.Get(id);
            if (user != null)
            {
                user.IsEnable = data.IsEnable;
                user.IsVisible = data.IsVisible;
                user.GroupId = data.GroupId;
                user.Name = data.Name;
                user.Account = data.Account;
                user.Password = string.IsNullOrEmpty(data.Password) ? user.Password : MyHelp.Encrypt(data.Password);
                user.ApiUserName = data.ApiUserName;
                user.ApiPassword = data.ApiPassword;
                user.Warehouse = warehouse != null ? JsonConvert.SerializeObject(warehouse) : null;
                user.TimeZone = data.TimeZone;

                AdminUsers.Update(user);
                AdminUsers.SaveChanges();

                MyHelp.Log("AdminUsers", user.Id, "編輯管理員");
                return RedirectToAction("edit", "user", routeValue);
            }

            ViewBag.warehouseList = Warehouses.GetAll().Where(w => w.IsEnable == true && w.IsSellable == true).ToList();
            ViewBag.timeZoneList = new Dictionary<EnumData.TimeZone, string>() {
                { EnumData.TimeZone.EST, "Eastern Standard Time" }, { EnumData.TimeZone.TST, "Taipei Standard Time" }, { EnumData.TimeZone.PST, "Pacific Standard Time" },
                { EnumData.TimeZone.GMT, "Greenwich Mean Time" }, { EnumData.TimeZone.AEST, "AUS Eastern Standard Time" }, { EnumData.TimeZone.JST, "Tokyo Standard Time" }
            };
            ViewBag.routeValue = routeValue;
            return View("~/Views/admin/users/edit.cshtml", user);
        }
        
        public ActionResult Delete(int id, [Bind(Include = "start,length,search,gId")] RouteValue routeValue)
        {
            if (!MyHelp.CheckAuth("user", "index", EnumData.AuthType.Delete)) return RedirectToAction("index", "main");

            AdminUsers user = AdminUsers.Get(id);

            user.IsEnable = false;
            AdminUsers.Update(user);
            AdminUsers.SaveChanges();

            MyHelp.Log("AdminUsers", user.Id, "刪除管理員");
            return RedirectToAction("index", "user", routeValue);
        }
    }
}
