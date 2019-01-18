using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using Newtonsoft.Json;
using QDLogistics.Models;

namespace QDLogistics.Commons
{
    public class StockKeepingUnit : IDisposable
    {
        protected QDLogisticsEntities db = new QDLogisticsEntities();

        protected Items itemData;

        #region IDisposable Support
        private bool disposedValue = false; // 偵測多餘的呼叫

        public StockKeepingUnit()
        {
        }

        public StockKeepingUnit(int itemID)
        {
            SetItemData(itemID);
        }

        public void SetItemData(int itemID)
        {
            itemData = db.Items.Find(itemID);
        }

        public bool CheckSkuSerial()
        {
            bool checkResult = true;

            try
            {
                if (itemData == null) throw new Exception("找不到資料!");

                var itemReceive = db.PurchaseItemReceive.AsNoTracking().FirstOrDefault(p => p.ProductID.Equals(itemData.ProductID));

                if (itemReceive != null && itemReceive.IsRequireSerialScan && !itemData.Qty.Equals(itemData.SerialNumbers.Count())) throw new Exception("Serial Number 數量不合!");
            }
            catch (Exception e)
            {
                MyHelp.Log("Items", itemData.ID, string.Format("訂單【{0}】- {1} 發現異常 - {2}", itemData.OrderID, itemData.ProductID, e.Message));
                checkResult = false;
            }

            return checkResult;
        }

        public void RecordOrderSkuStatement(int OrderID, string State)
        {
            Orders order = db.Orders.Find(OrderID);
            DateTime Date = State.Equals("New") ? order.Payments.FirstOrDefault()?.AuditDate?.ToUniversalTime() ?? order.TimeOfOrder.Value.ToUniversalTime() : DateTime.UtcNow;
            var data = order.Items.Where(i => i.IsEnable.Value).Select(i => new
            {
                OrderID,
                SCID = i.ShipFromWarehouseID,
                SkuNo = i.ProductID,
                i.Qty,
                State,
                Date
            }).ToList();

            Response response = Request("Ajax/OrderLogList", data);
            if (!response.status) throw new Exception(response.message);
        }

        private Response Request(string url, dynamic data, string method = "post")
        {
            Response response = new Response();
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://internal.qd.com.tw:8080/" + url);
            request.ContentType = "application/json";
            request.Method = "post";
            //request.ProtocolVersion = HttpVersion.Version10;

            if (data.Any())
            {
                using (StreamWriter streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(JsonConvert.SerializeObject(data));
                    streamWriter.Flush();
                }

                HttpWebResponse httpResponse = (HttpWebResponse)request.GetResponse();
                using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    response = JsonConvert.DeserializeObject<Response>(streamReader.ReadToEnd());
                }
            }

            return response;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 處置受控狀態 (受控物件)。
                }

                // TODO: 釋放非受控資源 (非受控物件) 並覆寫下方的完成項。
                // TODO: 將大型欄位設為 null。

                db = null;
                itemData = null;

                disposedValue = true;
            }
        }

        // TODO: 僅當上方的 Dispose(bool disposing) 具有會釋放非受控資源的程式碼時，才覆寫完成項。
        // ~StockKeepingUnit() {
        //   // 請勿變更這個程式碼。請將清除程式碼放入上方的 Dispose(bool disposing) 中。
        //   Dispose(false);
        // }

        // 加入這個程式碼的目的在正確實作可處置的模式。
        public void Dispose()
        {
            // 請勿變更這個程式碼。請將清除程式碼放入上方的 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果上方的完成項已被覆寫，即取消下行的註解狀態。
            // GC.SuppressFinalize(this);
        }
        #endregion

        public class Response
        {
            public bool status { get; set; }
            public string message { get; set; }

            public Response()
            {
                this.status = true;
                this.message = null;
            }
        }
    }
}