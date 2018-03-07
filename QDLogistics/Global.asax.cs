using QDLogistics.Commons;
using StackExchange.Profiling;
using StackExchange.Profiling.EntityFramework6;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace QDLogistics
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            MiniProfilerEF6.Initialize();
            
            Application["TaskFactory"] = new TaskFactory(new LimitedConcurrencyLevelTaskScheduler(1));
            Application["ProcessScheduling"] = new List<ThreadTask>();
            Application["WebAppLogin"] = new List<Controllers.ApiController.LoginInfo>();
        }

        protected void Application_BeginRequest()
        {
            if (Request.IsLocal)
            {
                MiniProfiler.Start();
            }
        }

        protected void Application_EndRequest()
        {
            MiniProfiler.Stop();
        }
    }
}
