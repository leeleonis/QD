using Newtonsoft.Json;
using QDLogistics.Filters;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;
using QDLogistics.OrderService;
using QDLogistics.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace QDLogistics.Controllers
{
    public class MainController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<AdminUsers> AdminUsers;

        public MainController()
        {
            db = new QDLogisticsEntities();
            AdminUsers = new GenericRepository<AdminUsers>(db);
        }

        //空首頁
        [CheckSession]
        public ActionResult Index()
        {
            return View();
        }

        //登入
        public ActionResult Login()
        {
            if (Session["IsLogin"] == null ? false : (bool)Session["IsLogin"])
            {
                return RedirectToAction("Index");
            }
            IRepository<Warehouses> Warehouses = new GenericRepository<Warehouses>(db);

            string host = Request.Url.Host;
            WarehouseTypeType warehouseType = host.Equals("dropshipper.qd.com.tw") ? WarehouseTypeType.DropShip : WarehouseTypeType.Normal;

            ViewBag.warehouseList = Warehouses.GetAll(true).Where(w => w.IsEnable.Equals(true) && w.IsSellable.Equals(true) && w.WarehouseType.Equals((int)warehouseType)).ToList();
            return View();
        }

        //登入(POST)
        [HttpPost]
        public ActionResult Login(string username, string password, int warehouse)
        {
            if (ValidateUser(username, password, warehouse))
            {
                return RedirectToAction("Index");
            }

            return RedirectToAction("Login");
        }

        // 驗證帳號密碼
        private bool ValidateUser(string username, string password, int warehouseID)
        {
            if (username == "weypro" && password == "weypro12ab")
            {
                return SetSessionData(true, "weypro", "tim@weypro.com", "timfromweypro");
            }

            AdminUsers user = AdminUsers.GetAll().FirstOrDefault(u => u.IsEnable && u.IsVisible && u.Account == username);
            var saltPassword = MyHelp.Encrypt(password);
            if (user == null)
            {
                return SetErrorMessage("找不到此使用者!");
            }
            else if (!string.Equals(user.Password, saltPassword))
            {
                return SetErrorMessage("密碼不正確!");
            }
            else if (warehouseID != 0)
            {
                var userWarehouse = !string.IsNullOrEmpty(user.Warehouse) ? JsonConvert.DeserializeObject<List<int>>(user.Warehouse) : new List<int>();
                if (!userWarehouse.Any(w => w == warehouseID))
                {
                    return SetErrorMessage("出貨倉權限不足!");
                }
            }

            /*user.LLT = DateTime.UtcNow;
            AdminUsers.Update(user);
            AdminUsers.SaveChanges();*/

            string ApiUserName = string.IsNullOrEmpty(user.ApiUserName) ? username : user.ApiUserName;
            string ApiPassword = string.IsNullOrEmpty(user.ApiPassword) ? password : user.ApiPassword;

            SellerCloud_WebService.SC_WebService SCWS = new SellerCloud_WebService.SC_WebService(ApiUserName, ApiPassword);
            try
            {
                if (!SCWS.Is_login) return SetErrorMessage("SC帳號無法登入!");
            }
            catch (Exception)
            {
                return SetErrorMessage("SC帳號無法登入!");
            }

            return SetSessionData(false, user.Name, ApiUserName, ApiPassword, user.Id, user.GroupId, warehouseID, user.TimeZone, user.AdminGroups.Auth);
        }

        //登出
        [CheckSession]
        public ActionResult Logout()
        {
            Session.Clear();

            return RedirectToAction("Login");
        }

        /// <summary>
        /// 設置Session
        /// </summary>
        /// <param name="IsManger"></param>
        /// <param name="AdminName"></param>
        /// <param name="ApiUserName"></param>
        /// <param name="ApiPassword"></param>
        /// <param name="AdminId"></param>
        /// <param name="GroupId"></param>
        /// <param name="WarehouseId"></param>
        /// <param name="TimeZone"></param>
        /// <param name="Auth"></param>
        /// <returns></returns>
        private bool SetSessionData(bool IsManger, string AdminName, string ApiUserName, string ApiPassword, int AdminId = 0, int GroupId = 0, int WarehouseId = 0, int TimeZone = -1, string Auth = null)
        {
            Session.Add("IsLogin", true);
            Session.Add("IsManager", IsManger);
            Session.Add("AdminId", AdminId);
            Session.Add("AdminName", AdminName);
            Session.Add("GroupId", GroupId);
            Session.Add("Auth", Auth);
            Session.Add("ApiUserName", ApiUserName);
            Session.Add("ApiPassword", ApiPassword);
            Session.Add("WarehouseID", WarehouseId);
            Session.Add("TimeZone", TimeZone);

            return true;
        }

        private bool SetErrorMessage(string message)
        {
            TempData["errorMessage"] = message;

            return false;
        }
    }
}