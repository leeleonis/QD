using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace QDLogistics
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "Main",
                url: "{action}",
                defaults: new { controller = "main", action = "index", id = UrlParameter.Optional },
                constraints: new { action = "(index|login|logout)" }
            );

            routes.MapRoute(
                 name: "Admin",
                 url: "admin/{controller}/{action}/{id}",
                 defaults: new { action = "index", id = UrlParameter.Optional },
                 constraints: new { controller = "(group|user)" },
                 namespaces: new[] { "QDLogistics.Controllers.Admin" }
             );

            routes.MapRoute(
                name: "Website",
                url: "website/{controller}/{action}/{id}",
                defaults: new { action = "index", id = UrlParameter.Optional },
                constraints: new { controller = "(preset)" },
                namespaces: new[] { "QDLogistics.Controllers.Website" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Main", action = "Login", id = UrlParameter.Optional }
            );
        }
    }
}
