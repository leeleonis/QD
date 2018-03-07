using System;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(QDLogistics.Startup))]

namespace QDLogistics
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {   
            app.MapSignalR();
        }
    }
}
