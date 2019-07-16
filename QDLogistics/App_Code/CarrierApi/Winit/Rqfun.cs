using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;

namespace CarrierApi.Winit_Old
{
    public class Rqfun
    {
        /// <summary>
        /// 做HTTP Request
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="targetUrl">API URL</param>
        /// <param name="token">參數</param>
        /// <returns></returns>
        public static T req<T>(string targetUrl, object token)
        {
            string json = JsonConvert.SerializeObject(token);
            byte[] postData = Encoding.UTF8.GetBytes(json);
            string result = "";

            HttpWebRequest request = HttpWebRequest.Create(targetUrl) as HttpWebRequest;
            request.Method = "POST";
            request.ContentType = "application/JSON";
            // request.Timeout = 30000;
            request.ContentLength = postData.Length;
            // 寫入 Post Body Message 資料流
            using (Stream st = request.GetRequestStream())
            {
                st.Write(postData, 0, postData.Length);
            }
            // 取得回應資料
            using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
            {
                using (StreamReader sr = new StreamReader(response.GetResponseStream()))
                {
                    result = sr.ReadToEnd();
                }
            }
            return JsonConvert.DeserializeObject<T>(result);

        }

    }
}
