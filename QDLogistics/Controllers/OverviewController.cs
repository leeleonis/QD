using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using QDLogistics.Commons;
using QDLogistics.Filters;
using QDLogistics.Models;
using QDLogistics.Models.Object;
using QDLogistics.OrderService;

namespace QDLogistics.Controllers
{
    public class OverviewController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<Orders> Orders;

        public OverviewController()
        {
            db = new QDLogisticsEntities();
        }

        [CheckSession]
        public ActionResult Order()
        {
            return View();
        }

        public ActionResult GetOrderData(DataFilter filter, int page = 1, int rows = 100)
        {
            int total = 0;
            List<object> dataList = new List<object>();

            /** Order Filter **/
            var OrderFilter = db.Orders.AsNoTracking().AsQueryable();
            if (!filter.StatusCode.Equals(null)) OrderFilter = OrderFilter.Where(o => o.StatusCode.Value.Equals(filter.StatusCode.Value));
            if (!string.IsNullOrWhiteSpace(filter.OrderID)) OrderFilter = OrderFilter.Where(o => o.OrderID.ToString().Equals(filter.OrderID) || (o.OrderSource.Value.Equals(1) && o.eBaySalesRecordNumber.Equals(filter.OrderID)) || (o.OrderSource.Value.Equals(4) && o.OrderSourceOrderId.Equals(filter.OrderID)));
            if (!string.IsNullOrWhiteSpace(filter.UserID)) OrderFilter = OrderFilter.Where(o => o.eBayUserID.Contains(filter.UserID));
            if (!filter.CurrencyCode.Equals(null)) OrderFilter = OrderFilter.Where(o => o.OrderCurrencyCode.Value.Equals(filter.CurrencyCode.Value));

            /** Package Filter **/
            var PackageFilter = db.Packages.AsNoTracking().Where(p => p.IsEnable.Value);
            if (!filter.ProccessStatus.Equals(null)) PackageFilter = PackageFilter.Where(p => p.ProcessStatus.Equals(filter.ProccessStatus.Value));
            if (!filter.DeclaredTotal.Equals(0)) PackageFilter = PackageFilter.Where(p => p.DeclaredTotal.Equals(filter.DeclaredTotal));

            /** Item Filter **/
            var ItemFilter = db.Items.AsNoTracking().Where(i => i.IsEnable.Value);
            if (!string.IsNullOrWhiteSpace(filter.Sku)) ItemFilter = ItemFilter.Where(i => i.ProductID.ToLower().Contains(filter.Sku.ToLower()));
            if (!string.IsNullOrWhiteSpace(filter.ItemName)) ItemFilter = ItemFilter.Where(i => i.DisplayName.ToLower().Contains(filter.ItemName.ToLower()));

            /** Address Filter **/
            var AddressFilter = db.Addresses.AsNoTracking().Where(a => a.IsEnable.Value);
            if (!string.IsNullOrWhiteSpace(filter.CountryCode)) AddressFilter = AddressFilter.Where(a => a.CountryCode.Equals(filter.CountryCode));

            var results = OrderFilter.ToList()
                .Join(PackageFilter, o => o.OrderID, p => p.OrderID, (o, p) => new OrderJoinData() { order = o, package = p })
                .Join(ItemFilter.GroupBy(i => i.PackageID.Value), oData => oData.package.ID, i => i.Key, (oData, i) => new OrderJoinData(oData) { item = i.First(), items = i.ToList(), itemCount = i.Sum(ii => 1 + ii.KitItemCount).Value })
                .Join(AddressFilter, oData => oData.order.ShippingAddress, a => a.Id, (oData, a) => new OrderJoinData(oData) { address = a });

            /** Payment Filter **/
            var PaymentFilter = db.Payments.AsNoTracking().Where(p => p.IsEnable.Value);
            if (!filter.PaymentDate.Equals(new DateTime()))
            {
                DateTime dateFrom = new DateTime(filter.PaymentDate.Year, filter.PaymentDate.Month, filter.PaymentDate.Day, 0, 0, 0);
                dateFrom = new TimeZoneConvert(dateFrom, MyHelp.GetTimeZone((int)Session["TimeZone"])).ConvertDateTime(EnumData.TimeZone.EST);
                DateTime dateTo = new DateTime(filter.PaymentDate.Year, filter.PaymentDate.Month, filter.PaymentDate.Day + 1, 0, 0, 0);
                dateTo = new TimeZoneConvert(dateTo, MyHelp.GetTimeZone((int)Session["TimeZone"])).ConvertDateTime(EnumData.TimeZone.EST);
                PaymentFilter = PaymentFilter.Where(p => DateTime.Compare(p.AuditDate.Value, dateFrom) >= 0 && DateTime.Compare(p.AuditDate.Value, dateTo) < 0);

                results = results.Join(PaymentFilter, oData => oData.order.OrderID, p => p.OrderID, (oData, p) => new OrderJoinData(oData) { payment = p }).ToList();
            }
            else
            {
                results = results.GroupJoin(PaymentFilter, oData => oData.order.OrderID, p => p.OrderID.Value, (oData, p) => new { orderJoinData = oData, payment = p.Take(1) })
                    .SelectMany(o => o.payment.DefaultIfEmpty(), (o, p) => new OrderJoinData(o.orderJoinData) { payment = p }).ToList();
            }

            if (results.Any())
            {
                int length = rows;
                int start = (page - 1) * length;
                total = results.Count();
                results = results.OrderByDescending(data => data.order.TimeOfOrder).Skip(start).Take(length).ToList();

                TimeZoneConvert TimeZoneConvert = new TimeZoneConvert();
                EnumData.TimeZone TimeZone = MyHelp.GetTimeZone((int)Session["TimeZone"]);

                dataList.AddRange(results.Select(data => new
                {
                    PackageID = data.package.ID,
                    OrderID = data.package.OrderID.Value,
                    ParentOrderID = data.order.ParentOrderID.Value,
                    OrderSourceOrderId = data.order.OrderSourceOrderId,
                    eBayUserID = data.order.eBayUserID,
                    ItemCount = data.itemCount,
                    PaymentDate = data.payment != null ? TimeZoneConvert.InitDateTime(data.payment.AuditDate.Value, EnumData.TimeZone.EST).ConvertDateTime(TimeZone).ToString("MM/dd/yyyy<br />hh:mm tt") : "",
                    Sku = data.itemCount == 1 ? data.item.ProductID : "Multi",
                    DisplayName = data.itemCount == 1 ? data.item.DisplayName : "Multi",
                    ShippingCountry = data.address.CountryName,
                    SubTotal = data.items.Sum(i => i.Qty * i.UnitPrice).Value.ToString("N"),
                    DeclaredTotal = data.package.DeclaredTotal != 0 ? data.package.DeclaredTotal.ToString("N") : "",
                    OrderCurrencyCode = Enum.GetName(typeof(CurrencyCodeType2), data.order.OrderCurrencyCode),
                    StatusCode = Enum.GetName(typeof(OrderStatusCode), data.order.StatusCode),
                    ProccessStatus = GetOrderLink(data),
                }));
            }

            return Json(new { total, rows = dataList }, JsonRequestBehavior.AllowGet);
        }

        private string GetOrderLink(OrderJoinData data)
        {
            string link = "<a href='{0}' target='_blank'>{1}</a>";
            string url = "";

            if (!string.IsNullOrEmpty(data.package.BoxID))
            {
                url = Url.Action("boxEdit", "directLine", new { id = data.package.BoxID });
            }
            else
            {
                url = Url.Action(data.package.ProcessStatus.Equals(3) ? "shipped" : (data.package.ProcessStatus.Equals(1) ? "waiting" : "index"), "order", new { data.order.OrderID });
            }
            
            return string.Format(link, url, Enum.GetName(typeof(EnumData.ProcessStatus), data.package.ProcessStatus));
        }

        public ActionResult getSelectOption()
        {
            AjaxResult result = new AjaxResult();

            var CountryCode = MyHelp.GetCountries().Select(c => new { text = c.Name, value = c.ID }).ToList();

            List<object> CurrencyCode = new List<object>();
            foreach (int code in Enum.GetValues(typeof(CurrencyCodeType2)))
            {
                CurrencyCode.Add(new { text = Enum.GetName(typeof(CurrencyCodeType2), code), value = code });
            }

            List<object> StatusCode = new List<object>();
            foreach (int code in Enum.GetValues(typeof(OrderStatusCode)))
            {
                StatusCode.Add(new { text = Enum.GetName(typeof(OrderStatusCode), code), value = code });
            }

            List<object> ProccessStatusCode = new List<object>();
            foreach (int code in Enum.GetValues(typeof(EnumData.ProcessStatus)))
            {
                ProccessStatusCode.Add(new { text = Enum.GetName(typeof(EnumData.ProcessStatus), code), value = code });
            }

            result.data = new { CountryCode, CurrencyCode, StatusCode, ProccessStatusCode };

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public class AjaxResult
        {
            public bool status { get; set; }
            public string message { get; set; }
            public object data { get; set; }

            public AjaxResult()
            {
                this.status = true;
                this.message = null;
                this.data = null;
            }
        }
    }
}