using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using QDLogistics.Commons;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace QDLogistics.Hubs
{
    [HubName("apiServer")]
    public class ServerHub : Hub
    {
        private readonly ApiServerHub _apiServer;

        /// <summary>
        /// 紀錄目前已連結的 Client 識別資料
        /// </summary>
        public static Dictionary<string, ClientInfo> CurrClients = new Dictionary<string, ClientInfo>();

        public ServerHub() : this(ApiServerHub.Instance) { }

        public ServerHub(ApiServerHub apiServer)
        {
            _apiServer = apiServer;
        }

        public void BroadcastOrderChange(int OrderID, EnumData.OrderChangeStatus Status)
        {
            string message = Newtonsoft.Json.JsonConvert.SerializeObject(new { OrderID, Status });
            _apiServer.BroadcastToAll(message);
        }

        public void BroadcastProductError(int OrderID, string ProductID, EnumData.OrderChangeStatus Status)
        {
            string message = Newtonsoft.Json.JsonConvert.SerializeObject(new { OrderID, ProductID, Status });
            _apiServer.BroadcastToAll(message);
        }

        public Dictionary<string, ClientInfo> GetAllClinet()
        {
            return CurrClients;
        }
        
        /// <summary>
        /// 提供Client 端呼叫
        /// 功能:對全體 Client 發送訊息
        /// </summary>
        /// <param name="message">發送訊息內容</param>
        public void SendMsg(string message)
        {
            string connId = Context.ConnectionId;
            lock (CurrClients)
            {
                if (CurrClients.ContainsKey(connId))
                {
                    Clients.All.ReceiveMsg(CurrClients[connId].ClientName, message);//呼叫 Client 端所提供 ReceiveMsg方法(ReceiveMsg 方法由 Client 端實作)
                }
            }
        }

        /// <summary>
        /// 提供 Client 端呼叫
        /// 功能:對 Server 進行身分註冊
        /// </summary>
        /// <param name="clientName">使用者稱謂</param>
        public void Register(string clientName)
        {
            string connId = Context.ConnectionId;
            lock (CurrClients)
            {
                if (!CurrClients.ContainsKey(connId))
                {
                    CurrClients.Add(connId, new ClientInfo { ConnId = connId, ClientName = clientName });
                }
            }
        }

        /// <summary>
        /// Client 端離線時的動作
        /// </summary>
        /// <param name="stopCalled">true:為使用者正常關閉(離線); false: 使用者不正常關閉(離線)，如連線狀態逾時</param>
        /// <returns></returns>
        public override Task OnDisconnected(bool stopCalled)
        {
            string connId = Context.ConnectionId;
            lock (CurrClients)
            {
                if (CurrClients.ContainsKey(connId))
                {
                    CurrClients.Remove(connId);
                }
            }

            stopCalled = true;
            return base.OnDisconnected(stopCalled);
        }
    }

    /// <summary>
    /// 保存Client識別資料的物件
    /// </summary>
    public class ClientInfo
    {
        public string ConnId { get; set; }

        public string ClientName { get; set; }
    }
}