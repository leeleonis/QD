using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QDLogistics.Hubs
{
    public class ApiServerHub
    {
        private readonly static Lazy<ApiServerHub> _instance = new Lazy<ApiServerHub>(() => new ApiServerHub(GlobalHost.ConnectionManager.GetHubContext<ServerHub>().Clients));

        public static ApiServerHub Instance { get { return _instance.Value; } }

        private IHubConnectionContext<dynamic> Clients { get; set; }

        public ApiServerHub(IHubConnectionContext<dynamic> clients)
        {
            Clients = clients;
        }

        public void BroadcastOrderChange(string Message)
        {
            Clients.All.refreshOrderPickUp(Message);
        }
    }
}