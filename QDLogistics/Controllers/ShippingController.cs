using CarrierApi.Winit;
using Neodynamic.SDK.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Postmen_sdk_NET;
using QDLogistics.Commons;
using QDLogistics.Filters;
using QDLogistics.Models;
using QDLogistics.Models.Object;
using QDLogistics.Models.Repositiry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace QDLogistics.Controllers
{
    public class ShippingController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<Carriers> Carriers;
        private IRepository<CarrierAPI> CarrierAPI;

        public ShippingController()
        {
            db = new QDLogisticsEntities();
            Carriers = new GenericRepository<Carriers>(db);
            CarrierAPI = new GenericRepository<CarrierAPI>(db);
        }

        [CheckSession]
        public ActionResult Carrier()
        {
            return View();
        }
        
        public ActionResult Carriercreate()
        {
            if (!MyHelp.CheckAuth("shipping", "carrier", EnumData.AuthType.Insert)) return RedirectToAction("index", "main");

            Carriers newCarrier = new Carriers() { IsEnable = false, IsExport = false, IsBattery = false };
            Carriers.Create(newCarrier);
            Carriers.SaveChanges();

            MyHelp.Log("Carriers", null, "新增運輸商");
            return RedirectToAction("Carrieredit", new { id = newCarrier.ID });
        }
        
        public ActionResult Carrieredit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Carriers carrier = Carriers.Get(id.Value);
            if (carrier == null) return HttpNotFound();

            var methodList = Enum.GetValues(typeof(EnumData.ShippingMethod)).Cast<EnumData.ShippingMethod>().Select(s => new { text = s.ToString(), value = (int)s, group = "TWN" }).ToList();

            Winit_API winit = new Winit_API();
            List<object> deliveryWay = new List<object>();
            foreach (var warehouse in winit.warehouseIDs)
            {
                deliveryWayData[] way = winit.getDeliveryWay(warehouse.Value).data.ToObject<deliveryWayData[]>();
                methodList.AddRange(way.Select(w => new { text = w.deliveryWay, value = int.Parse(w.deliveryID), group = "Winit" }));
            }
            ViewData["methodList"] = new SelectList(methodList, "value", "text", "group", carrier.ShippingMethod);

            var boxTypeList = Enum.GetValues(typeof(EnumData.BoxType)).Cast<EnumData.BoxType>().Select(b => new { text = b.ToString(), value = (int)b }).ToList();
            ViewData["boxTypeList"] = new SelectList(boxTypeList, "value", "text", carrier.BoxType);

            ViewBag.WCPScript = WebClientPrint.CreateScript(Url.Action("ProcessRequest", "WebClientPrintAPI", null, HttpContext.Request.Url.Scheme), Url.Action("PrintFile", "File", null, HttpContext.Request.Url.Scheme), HttpContext.Session.SessionID);
            IEnumerable<CarrierAPI> apiList = CarrierAPI.GetAll(true).Where(a => a.IsEnable == true).OrderBy(a => a.Id);
            ViewData["apiList"] = new SelectList(apiList, "Id", "name", carrier.Api);
            return View(carrier);
        }

        [HttpPost]
        public ActionResult Carrieredit(int id)
        {
            if (!MyHelp.CheckAuth("shipping", "carrier", EnumData.AuthType.Edit)) return RedirectToAction("index", "main");

            Carriers carrier = Carriers.Get(id);
            if (carrier == null) return HttpNotFound();

            if (TryUpdateModel(carrier) && ModelState.IsValid)
            {
                Carriers.SaveChanges();

                MyHelp.Log("Carriers", carrier.ID, "編輯運輸商");
                return RedirectToAction("Carrier", "shipping");
            }

            ViewBag.WCPScript = WebClientPrint.CreateScript(Url.Action("ProcessRequest", "WebClientPrintAPI", null, HttpContext.Request.Url.Scheme), Url.Action("PrintFile", "File", null, HttpContext.Request.Url.Scheme), HttpContext.Session.SessionID);
            IEnumerable<CarrierAPI> apiList = CarrierAPI.GetAll().Where(a => a.IsEnable == true).OrderBy(a => a.Id);
            ViewData["apiList"] = new SelectList(apiList, "Id", "name", carrier.Api);
            return View(carrier);
        }

        [CheckSession]
        public ActionResult Service()
        {
            return View();
        }

        [CheckSession]
        public ActionResult Country()
        {
            ViewBag.countries = MyHelp.GetCountries();
            return View();
        }

        [CheckSession]
        public ActionResult Api([Bind(Include = "start,length,search")] RouteValue routeValue)
        {
            ViewBag.routeValue = routeValue;
            return View();
        }

        public ActionResult Apicreate()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Apicreate(CarrierAPI newApi)
        {
            Postmen postmen = new Postmen(newApi.IsTest ? "sandbox" : "production");

            switch (newApi.Type)
            {
                case (byte)EnumData.CarrierType.DHL:
                case (byte)EnumData.CarrierType.FedEx:
                case (byte)EnumData.CarrierType.UPS:
                case (byte)EnumData.CarrierType.USPS:
                    JObject obj = setAccountData(newApi);
                    string type = Enum.GetName(typeof(EnumData.CarrierType), newApi.Type);

                    obj.Add("slug", type.ToLower());
                    obj.Add("description", type + " Shipper Account");
                    obj.Add("address", new JObject() {
                        { "country", "TWN" }, { "contact_name", "Huai Wei Ho" },  { "phone", "0423718118" },  { "fax", null },   { "email", null },  { "company_name", "Zhi You Wan LTD" },
                        { "street1", "No.51, Sec.3 Jianguo N. Rd.," }, { "street2", "South Dist.," }, { "street3", null }, { "city", "Taichung City" }, { "state", null },
                        { "postal_code", "403" }, { "type", "business" }
                    });
                    obj.Add("timezone", "Asia/Taipei");

                    try
                    {
                        var result = postmen.create("shipper-accounts", obj);
                        newApi.AccountID = result["data"]["id"].ToString();
                    }
                    catch (Postmen.PostmenException e)
                    {
                        ViewBag.Error = e.Details;
                        return View();
                    }
                    break;
                default:
                    break;
            }

            CarrierAPI.Create(newApi);
            CarrierAPI.SaveChanges();

            MyHelp.Log("CarrierAPI", null, "新增連線API");
            return RedirectToAction("api", "shipping");
        }
        
        public ActionResult Apiedit(int? id, [Bind(Include = "start,length,search")] RouteValue routeValue)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            CarrierAPI api = CarrierAPI.Get(id.Value);
            if (api == null) return HttpNotFound();

            ViewBag.routeValue = routeValue;
            return View(api);
        }

        [HttpPost]
        public ActionResult Apiedit(int id, [Bind(Include = "start,length,search")] RouteValue routeValue)
        {
            CarrierAPI api = CarrierAPI.Get(id);
            if (api == null) return HttpNotFound();

            if (TryUpdateModel(api) && ModelState.IsValid)
            {
                CarrierAPI.SaveChanges();

                MyHelp.Log("CarrierAPI", api.Id, "編輯連線API");
                return RedirectToAction("api", "shipping", routeValue);
            }

            ViewBag.routeValue = routeValue;
            return View(api);
        }

        [CheckSession]
        public ActionResult Apidelete(int id, [Bind(Include = "start,length,search")] RouteValue routeValue)
        {
            CarrierAPI api = CarrierAPI.Get(id);
            if (api == null) return HttpNotFound();

            api.IsEnable = false;
            CarrierAPI.Update(api);
            CarrierAPI.SaveChanges();

            MyHelp.Log("CarrierAPI", api.Id, "刪除連線API");
            return RedirectToAction("api", "shipping", routeValue);
        }

        private JObject setAccountData(CarrierAPI api)
        {
            JObject obj = new JObject();

            switch (api.Type)
            {
                case (byte)EnumData.CarrierType.DHL:
                    obj.Add("credentials", new JObject() { { "account_number", api.ApiAccount }, { "password", api.ApiPassword }, { "site_id", api.ApiKey } });
                    break;
                case (byte)EnumData.CarrierType.FedEx:
                    obj.Add("credentials", new JObject() { { "account_number", api.ApiAccount }, { "key", api.ApiKey }, { "password", api.ApiPassword }, { "meter_number", api.ApiMeter } });
                    break;
                case (byte)EnumData.CarrierType.UPS:
                    obj.Add("credentials", new JObject() { { "account_number", api.ApiAccount }, { "access_key", api.ApiKey }, { "password", api.ApiPassword }, { "user_identifier", api.ApiMeter } });
                    break;
                case (byte)EnumData.CarrierType.USPS:
                    obj.Add("credentials", new JObject() { { "account_id", api.ApiAccount }, { "passphrase", api.ApiPassword } });
                    break;
            }

            return obj;
        }
    }
}