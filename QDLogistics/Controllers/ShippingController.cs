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
        private IRepository<ShippingMethod> Method;
        private IRepository<Carriers> Carriers;
        private IRepository<CarrierAPI> CarrierAPI;

        public ShippingController()
        {
            db = new QDLogisticsEntities();
            Carriers = new GenericRepository<Carriers>(db);
            CarrierAPI = new GenericRepository<CarrierAPI>(db);
        }

        [CheckSession]
        public ActionResult ShippingMethod()
        {
            return View();
        }

        public ActionResult ShippingMethodCreate()
        {
            if (!MyHelp.CheckAuth("shipping", "shippingMethod", EnumData.AuthType.Insert)) return RedirectToAction("index", "main");

            using (Method = new GenericRepository<ShippingMethod>())
            {
                ShippingMethod newMethod = new ShippingMethod() { IsEnable = false, IsDirectLine = false, IsExport = false, IsBattery = false };
                Method.Create(newMethod);
                Method.SaveChanges();

                MyHelp.Log("ShippingMethod", newMethod.ID, "新增運輸方式");
                return RedirectToAction("shippingMethodEdit", new { id = newMethod.ID });
            }
        }

        public ActionResult ShippingMethodEdit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Method = new GenericRepository<ShippingMethod>(db);

            ShippingMethod method = Method.Get(id.Value);
            if (method == null) return HttpNotFound();

            Carriers = new GenericRepository<Carriers>(db);
            IEnumerable<Carriers> carrierList = Carriers.GetAll(true).Where(c => c.IsEnable).OrderBy(c => c.ID);

            List<object> directLineSelect = new List<object>() { new { text = "無", value = (byte)0 } };
            directLineSelect.AddRange(Enum.GetValues(typeof(EnumData.DirectLine)).Cast<EnumData.DirectLine>().Select(t => new { text = EnumData.DirectLineList()[t], value = (byte)t }).ToList());

            ViewData["carrierSelect"] = new SelectList(carrierList, "Id", "name", method.CarrierID);
            ViewData["directLineSelect"] = new SelectList(directLineSelect.AsEnumerable(), "value", "text", method.DirectLine);

            ViewBag.WCPScript = WebClientPrint.CreateScript(Url.Action("ProcessRequest", "WebClientPrintAPI", null, HttpContext.Request.Url.Scheme), Url.Action("PrintFile", "File", null, HttpContext.Request.Url.Scheme), HttpContext.Session.SessionID);
            return View(method);
        }

        [HttpPost]
        public ActionResult ShippingMethodEdit(int id)
        {
            if (!MyHelp.CheckAuth("shipping", "shippingMethod", EnumData.AuthType.Edit)) return RedirectToAction("index", "main");

            Method = new GenericRepository<ShippingMethod>(db);

            ShippingMethod method = Method.Get(id);
            if (method == null) return HttpNotFound();

            if (TryUpdateModel(method) && ModelState.IsValid)
            {
                Method.SaveChanges();

                MyHelp.Log("ShippingMethod", method.ID, "編輯運輸方式");
                return RedirectToAction("shippingMethod", "shipping");
            }

            Carriers = new GenericRepository<Carriers>(db);
            IEnumerable<Carriers> carrierList = Carriers.GetAll(true).Where(c => c.IsEnable).OrderBy(c => c.ID);

            ViewData["carrierSelect"] = new SelectList(carrierList, "Id", "name", method.CarrierID);

            ViewBag.WCPScript = WebClientPrint.CreateScript(Url.Action("ProcessRequest", "WebClientPrintAPI", null, HttpContext.Request.Url.Scheme), Url.Action("PrintFile", "File", null, HttpContext.Request.Url.Scheme), HttpContext.Session.SessionID);
            return View(method);
        }

        [CheckSession]
        public ActionResult Carrier()
        {
            return View();
        }

        public ActionResult CarrierCreate()
        {
            if (!MyHelp.CheckAuth("shipping", "carrier", EnumData.AuthType.Insert)) return RedirectToAction("index", "main");

            using (Carriers = new GenericRepository<Carriers>())
            {
                Carriers newCarrier = new Carriers() { IsEnable = false };
                Carriers.Create(newCarrier);
                Carriers.SaveChanges();

                MyHelp.Log("Carriers", newCarrier.ID, "新增運輸商");
                return RedirectToAction("carrierEdit", new { id = newCarrier.ID });
            }
        }

        public ActionResult CarrierEdit(int? id)
        {
            if (id == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Carriers = new GenericRepository<Carriers>(db);

            Carriers carrier = Carriers.Get(id.Value);
            if (carrier == null) return HttpNotFound();

            CarrierAPI = new GenericRepository<CarrierAPI>(db);
            List<CarrierAPI> apiList = new List<CarrierAPI>() { new CarrierAPI() { Id = 0, Name = "無" } };
            apiList.AddRange(CarrierAPI.GetAll(true).Where(a => a.IsEnable).OrderBy(c => c.Id));

            ViewData["apiSelect"] = new SelectList(apiList, "Id", "name", carrier.Api);

            return View(carrier);
        }

        [HttpPost]
        public ActionResult CarrierEdit(int id)
        {
            if (!MyHelp.CheckAuth("shipping", "shippingMethod", EnumData.AuthType.Edit)) return RedirectToAction("index", "main");

            Carriers = new GenericRepository<Carriers>(db);

            Carriers carrier = Carriers.Get(id);
            if (carrier == null) return HttpNotFound();

            if (TryUpdateModel(carrier) && ModelState.IsValid)
            {
                Carriers.SaveChanges();

                MyHelp.Log("Carriers", carrier.ID, "編輯運輸商");
                return RedirectToAction("carrier", "shipping");
            }

            CarrierAPI = new GenericRepository<CarrierAPI>(db);
            List<CarrierAPI> apiList = new List<CarrierAPI>() { new CarrierAPI() { Id = 0, Name = "無" } };
            apiList.AddRange(CarrierAPI.GetAll(true).Where(a => a.IsEnable).OrderBy(c => c.Id));

            ViewData["apiSelect"] = new SelectList(apiList, "Id", "name", carrier.Api);

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

        public ActionResult GetSelectOption(List<string> optionType)
        {
            AjaxResult result = new AjaxResult();

            if (optionType.Any())
            {
                var optionList = new Dictionary<string, object>();

                try
                {
                    foreach (string type in optionType)
                    {
                        switch (type)
                        {
                            case "shippingMethod":
                                Method = new GenericRepository<ShippingMethod>(db);
                                optionList.Add(type, Method.GetAll(true).Where(m => m.IsEnable).Select(m => new { text = m.Name, value = m.ID }));
                                break;
                            case "carrier":
                                Carriers = new GenericRepository<Carriers>(db);
                                optionList.Add(type, Carriers.GetAll(true).Where(c => c.IsEnable).Select(c => new
                                {
                                    text = string.Format("{0}【{1}】", c.Name, (c.CarrierAPI != null ? c.CarrierAPI.Name : "無")),
                                    value = c.ID,
                                    type = c.CarrierAPI != null ? Enum.GetName(typeof(EnumData.CarrierType), c.CarrierAPI.Type) : ""
                                }).ToList());
                                break;
                            case "methodType":
                                var FedEx_shippingMethod = Enum.GetValues(typeof(FedExShipService.ServiceType)).Cast<FedExShipService.ServiceType>().Select(b => new { text = b.ToString(), value = (int)b }).ToList();

                                Winit_API winit = new Winit_API();
                                List<deliveryWayData> deliveryWay = new List<deliveryWayData>();
                                foreach (var warehouse in winit.warehouseIDs)
                                {
                                    deliveryWay.AddRange(winit.getDeliveryWay(warehouse.Value).data.ToObject<deliveryWayData[]>());
                                }

                                var Winit_shippingMethod = deliveryWay.OrderBy(w => w.deliveryID).Select(w => new { text = w.deliveryWay, value = int.Parse(w.deliveryID) }).Distinct().ToList();

                                optionList.Add(type, new Dictionary<string, object>() { { "FedEx", FedEx_shippingMethod }, { "DHL", null }, { "Winit", Winit_shippingMethod } });
                                break;
                            case "boxType":
                                var FedEx_boxType = Enum.GetValues(typeof(FedExShipService.PackagingType)).Cast<FedExShipService.PackagingType>().Select(b => new { text = b.ToString(), value = (int)b }).ToList();

                                optionList.Add(type, new Dictionary<string, object>() { { "FedEx", FedEx_boxType } });
                                break;
                            case "carrierApi":
                                CarrierAPI = new GenericRepository<CarrierAPI>(db);
                                List<object> apiList = new List<object>() { new { text = "無", value = 0 } };
                                apiList.AddRange(CarrierAPI.GetAll(true).Where(a => a.IsEnable).Select(a => new { text = a.Name, value = a.Id }));
                                optionList.Add(type, apiList);
                                break;
                        }
                    }

                    result.data = optionList;
                }
                catch (Exception e)
                {
                    result.status = false;
                    result.message = e.InnerException != null && !string.IsNullOrEmpty(e.InnerException.Message) ? e.InnerException.Message : e.Message;
                }
            }

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