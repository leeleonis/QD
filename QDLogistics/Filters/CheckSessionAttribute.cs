using Newtonsoft.Json;
using QDLogistics.Commons;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.WebPages;

namespace QDLogistics.Filters
{
    public class CheckSessionAttribute : ActionFilterAttribute
    {
        private QDLogisticsEntities db;
        private IRepository<Menu> Menu;
        private IRepository<AdminUsers> AdminUsers;

        public CheckSessionAttribute()
        {
            db = new QDLogisticsEntities();
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            bool isLogin = (context.HttpContext.Session.Contents["IsLogin"] == null) ? false : (bool)context.HttpContext.Session.Contents["IsLogin"];

            if (!isLogin) ToLoginPage(context, "login");

            if (isLogin && !(bool)context.HttpContext.Session.Contents["IsManager"])
            {
                Menu = new GenericRepository<Menu>(db);
                string controllerName = HttpContext.Current.Request.RequestContext.RouteData.Values["controller"].ToString().ToLower();
                string actionName = HttpContext.Current.Request.RequestContext.RouteData.Values["action"].ToString().ToLower();

                if (!new string[] { "main", "ajax", "file" }.Contains(controllerName))
                {
                    Menu menu = Menu.GetAll().First(m => m.IsEnable && m.Controller.ToLower().Equals(controllerName) && m.Action.ToLower().Equals(actionName));
                    if (menu == null) ToLoginPage(context);

                    string auth = (string)context.HttpContext.Session.Contents["Auth"];
                    if (auth.IsEmpty()) ToLoginPage(context);

                    Dictionary<int, List<bool>> managerAuth = JsonConvert.DeserializeObject<Dictionary<int, List<bool>>>(auth);
                    if (menu.ParentMenu != null && !managerAuth[menu.ParentMenu.MenuId][(int)EnumData.AuthType.View]) ToLoginPage(context);
                    if (!managerAuth[menu.MenuId][(int)EnumData.AuthType.View]) ToLoginPage(context);
                }
            }

            context.HttpContext.Session.Add("IsLogin", isLogin);
            base.OnActionExecuting(context);
        }

        public override void OnResultExecuted(ResultExecutedContext context)
        {
            bool isLogin = (context.HttpContext.Session.Contents["IsLogin"] == null) ? false : (bool)context.HttpContext.Session.Contents["IsLogin"];

            if (isLogin && !(bool)context.HttpContext.Session.Contents["IsManager"])
            {
                /*AdminUsers = new GenericRepository<AdminUsers>(db);
                int userID = (int)context.HttpContext.Session.Contents["AdminId"];

                AdminUsers user = AdminUsers.Get(userID);
                user.LLT = DateTime.Now;
                AdminUsers.Update(user);
                AdminUsers.SaveChanges();*/
            }

            context.HttpContext.Session.Add("IsLogin", isLogin);
            base.OnResultExecuted(context);
        }

        private void ToLoginPage(ActionExecutingContext context, string action = "")
        {
            RouteValueDictionary redirectTargetDictionary = new RouteValueDictionary
            {
                { "action", action }
            };

            context.Result = new RedirectToRouteResult(redirectTargetDictionary);
            return;
        }
    }
}