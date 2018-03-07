using Newtonsoft.Json;
using QDLogistics.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace CarrierApi.Winit
{
    public class Winit_API
    {
        private string api_url = "http://api.winit.com.cn/ADInterface/api";
        private string api_key = "peter0626@hotmail.com";
        private string api_userName = "peter0626@hotmail.com";
        private string api_password = "gubu67qaP5e$ra*t";
        private string api_token;

        private QDLogisticsEntities db = new QDLogisticsEntities();
        private IRepository<Warehouses> Warehouses;
        private IRepository<CarrierAPI> CarrierAPI;
        public Dictionary<int, string> warehouseIDs
        {
            get
            {
                return Warehouses.GetAll(true).Where(w => w.IsEnable == true & w.IsSellable == true && !w.WinitWarehouseID.Equals("0")).ToDictionary(w => w.ID, w => w.WinitWarehouseID);
            }
        }

        public Winit_API() : this(null)
        {
        }

        public Winit_API(CarrierAPI Api)
        {
            Warehouses = new QDLogistics.Models.Repositiry.GenericRepository<Warehouses>(db);

            if (Api == null)
            {
                CarrierAPI = new QDLogistics.Models.Repositiry.GenericRepository<CarrierAPI>(db);

                Api = CarrierAPI.Get(17);

                if (Api == null) throw new Exception("Carrier Api not found!");
            }

            api_url = string.Format("http://{0}.winit.com.cn/ADInterface/api", Api.IsTest ? "erp.sandbox" : "api");
            api_key = Api.ApiKey;
            api_userName = Api.ApiAccount;
            api_password = Api.ApiPassword;
            api_token = _Get_Token();
        }

        public Received SearchOrder(string pageNum = "1", string pageSize = "100", int days = 7)
        {
            TimeZoneConvert timeZone = new TimeZoneConvert();
            DateTime endDate = timeZone.ConvertDateTime(QDLogistics.Commons.EnumData.TimeZone.TST);
            DateTime startDate = endDate.AddDays(-days);

            queryOutboundOrderList_data data = new queryOutboundOrderList_data()
            {
                dateOrderedStartDate = startDate.ToString("yyyy-MM-dd"),
                dateOrderedEndDate = endDate.ToString("yyyy-MM-dd"),
                status = "valid",
                pageSize = pageSize,
                pageNum = pageNum
            };

            queryOutboundOrderList request = _RequestInit<queryOutboundOrderList>("queryOutboundOrderList", JsonConvert.SerializeObject(data));
            request.data = data;

            Received result = Resfun.funresult(request, api_url);

            return result;
        }

        public Received Order(string outboundOrderNum)
        {
            queryOutboundOrder_data data = new queryOutboundOrder_data() { outboundOrderNum = outboundOrderNum };

            queryOutboundOrder request = _RequestInit<queryOutboundOrder>("queryOutboundOrder", JsonConvert.SerializeObject(data));
            request.data = data;

            Received result = Resfun.funresult(request, api_url);

            return result;
        }

        public Received Tracking(string warehouseID, string outboundNum, string trackingNum = "")
        {
            queryTrack_data data = new queryTrack_data()
            {
                warehouseID = warehouseID,
                outboundNum = outboundNum,
                trackingNum = trackingNum
            };

            queryTrack request = _RequestInit<queryTrack>("queryTrack", JsonConvert.SerializeObject(data));
            request.data = data;

            Received result = Resfun.funresult(request, api_url);

            return result;
        }

        public Received CreateInfo(createOutboundInfo_data data)
        {
            createOutboundInfo request = _RequestInit<createOutboundInfo>("createOutboundInfo", JsonConvert.SerializeObject(data));
            request.data = data;

            Received result = Resfun.funresult(request, api_url);

            return result;
        }

        public Received Create(createOutboundOrder_data data)
        {
            createOutboundOrder request = _RequestInit<createOutboundOrder>("createOutboundOrder", JsonConvert.SerializeObject(data));
            request.data = data;

            Received result = Resfun.funresult(request, api_url);

            return result;
        }

        public Received Confirm(string outboundOrderNum)
        {
            confirmOutboundOrder_data data = new confirmOutboundOrder_data() { outboundOrderNum = outboundOrderNum };

            confirmOutboundOrder request = _RequestInit<confirmOutboundOrder>("confirmOutboundOrder", JsonConvert.SerializeObject(data));
            request.data = data;

            return Resfun.funresult(request, api_url);
        }


        public Received Void(string outboundOrderNum)
        {
            voidOutboundOrder_data data = new voidOutboundOrder_data() { outboundOrderNum = outboundOrderNum };

            voidOutboundOrder request = _RequestInit<voidOutboundOrder>("voidOutboundOrder", JsonConvert.SerializeObject(data));
            request.data = data;

            return Resfun.funresult(request, api_url);
        }

        public Received getWarehouses()
        {
            queryWarehouse request = _RequestInit<queryWarehouse>("queryWarehouse", JsonConvert.SerializeObject(new { }));
            request.data = new { };

            Received result = Resfun.funresult(request, api_url);

            return result;
        }

        public Received getDeliveryWay(string warehouseID)
        {
            object data = new { warehouseID = warehouseID };

            queryDeliveryWay request = _RequestInit<queryDeliveryWay>("queryDeliveryWay", JsonConvert.SerializeObject(data));
            request.data = data;

            Received result = Resfun.funresult(request, api_url);

            return result;
        }

        private T _RequestInit<T>(string action, string data) where T : Token2, new()
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");

            T request = new T()
            {
                action = action,
                app_key = api_key,
                format = "json",
                language = "zh_CN",
                platform = "SELLERERP",
                sign = _Get_Sign(action, data, timestamp),
                sign_method = "md5",
                timestamp = timestamp,
                version = "1.0"
            };

            return request;
        }

        private string _Get_Sign(string action, string data, string timestamp)
        {
            string sign = "";
            MD5 md5 = MD5.Create();

            string format = "json", platform = "SELLERERP", sign_method = "md5", version = "1.0";
            var combine = api_token + "action" + action + "app_key" + api_key + "data" + data + "format" + format + "platform" + platform + "sign_method" + sign_method + "timestamp" + timestamp + "version" + version + api_token;
            byte[] Original = Encoding.ASCII.GetBytes(combine); //將字串來源轉為Byte[] 
            byte[] Change = md5.ComputeHash(Original);
            String a = Convert.ToBase64String(Change);

            for (int i = 0; i < Change.Length; i++)
            {
                sign = sign + Change[i].ToString("X2");
            }

            return sign;
        }

        private string _Get_Token()
        {
            getToken request = new getToken()
            {
                action = "getToken",
                data = new getTokendata()
                {
                    userName = api_userName,
                    passWord = api_password
                }
            };

            Received result = Resfun.funresult(request, api_url);

            if (!result.code.Equals("0")) throw (new Exception(result.msg));

            return result.data;
        }
    }
}