using QDLogistics.Filters;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace QDLogistics.Controllers.Website
{
    public class PresetController : Controller
    {
        private QDLogisticsEntities db;
        private IRepository<Preset> Preset;

        public PresetController()
        {
            db = new QDLogisticsEntities();
        }

        [CheckSession]
        public ActionResult Index()
        {
            return View("~/Views/website/preset/index.cshtml");
        }

        public ActionResult Add()
        {
            JsonResult result = new JsonResult();

            using (Preset = new GenericRepository<Preset>(db))
            {
                try
                {
                    Preset.Create(new Preset() { IsEnable = true });
                    Preset.SaveChanges();
                }
                catch (Exception e)
                {
                    result.Error(e.InnerException != null ? e.InnerException.Message : e.Message);
                }
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult Edit(Preset preset)
        {
            JsonResult result = new JsonResult();

            using (Preset = new GenericRepository<Preset>(db))
            {
                try
                {
                    if (!ModelState.IsValid) throw new Exception("Valid failed!");

                    Preset.Update(preset, preset.Id);
                    Preset.SaveChanges();
                }
                catch (Exception e)
                {
                    result.Error(e.InnerException != null ? e.InnerException.Message : e.Message);
                }
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Delete(int id)
        {
            JsonResult result = new JsonResult();

            using (Preset = new GenericRepository<Preset>(db))
            {
                try
                {
                    Preset preset = Preset.Get(id);

                    if (preset == null) throw new Exception("Not found preset!");

                    preset.IsEnable = false;
                    Preset.Update(preset, preset.Id);
                    Preset.SaveChanges();
                }
                catch (Exception e)
                {
                    result.Error(e.InnerException != null ? e.InnerException.Message : e.Message);
                }
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult AjaxOption()
        {
            IRepository<Companies> Companies = new GenericRepository<Companies>(db);
            IRepository<Services> Services = new GenericRepository<Services>(db);
            IRepository<Warehouses> Warehouses = new GenericRepository<Warehouses>(db);

            JsonResult result = new JsonResult();
            Dictionary<string, string> sourceList = new Dictionary<string, string>();

            try
            {
                foreach (int source in Enum.GetValues(typeof(OrderService.OrderSource)))
                {
                    sourceList.Add((source + 1).ToString(), Enum.GetName(typeof(OrderService.OrderSource), source));
                }

                result.data = new
                {
                    countryList = MyHelp.GetCountries().ToDictionary(c => c.ID, c => c.Name),
                    companyList = Companies.GetAll(true).ToDictionary(c => c.ID.ToString(), c => c.CompanyName),
                    sourceList = sourceList,
                    methodList = Services.GetAll(true).Where(s => s.IsEnable.Equals(true)).ToDictionary(s => s.ServiceCode, s => s.ServiceCode),
                    warehouseList = Warehouses.GetAll(true).Where(w => w.IsEnable.Equals(true) && w.IsSellable.Equals(true)).OrderByDescending(w => w.IsDefault).OrderBy(w => w.ID).ToDictionary(w => w.ID.ToString(), w => w.Name)
                };
            }
            catch (Exception e)
            {
                result.Error(e.InnerException != null ? e.InnerException.Message : e.Message);
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult AjaxShippingMethod(int warehouseID)
        {
            IRepository<Warehouses> Warehouses = new GenericRepository<Warehouses>(db);
            IRepository<ShippingMethod> Method = new GenericRepository<ShippingMethod>(db);

            JsonResult result = new JsonResult();

            try
            {
                Warehouses warehouse = Warehouses.Get(warehouseID);

                if (warehouse == null) throw new Exception("Not found warehouse!");

                var methodData = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, bool>>(warehouse.CarrierData);
                result.data = Method.GetAll(true).Where(m => methodData.ContainsKey(m.ID.ToString()) && methodData[m.ID.ToString()]).ToDictionary(m => m.ID.ToString(), m => m.Name);
            }
            catch (Exception e)
            {
                result.Error(e.InnerException != null ? e.InnerException.Message : e.Message);
            }

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult AjaxData(int draw, int start, int length)
        {
            Preset = new GenericRepository<Preset>(db);

            var total = 0;
            List<object> dataList = new List<object>();

            IEnumerable<Preset> results = Preset.GetAll(true).Where(p => p.IsEnable.Equals(true));
            if (results.Any())
            {
                total = results.Count();
                foreach (Preset preset in results.OrderByDescending(p => p.Id).Skip(start).Take(length).ToList())
                {
                    dataList.Add(new
                    {
                        visible = preset.IsVisible,
                        dispatch = preset.IsDispatch,
                        id = preset.Id,
                        type = preset.Type,
                        value = preset.Value,
                        valueType = preset.ValueType,
                        total = preset.Total,
                        totalType = preset.TotalType,
                        country = preset.Country,
                        company = preset.CompanyID,
                        channel = preset.SourceID,
                        amount = preset.Amount,
                        amountType = preset.AmountType,
                        method = preset.ShippingMethod,
                        sku = preset.Sku,
                        weight = preset.Weight,
                        weightType = preset.WeightType,
                        warehouse = preset.WarehouseID,
                        shippingMethod = preset.MethodID
                    });
                }
            }

            return Json(new { draw = draw, data = dataList, recordsFiltered = total, recordsTotal = total });
        }
    }

    public class JsonResult
    {
        public bool status { get { return string.IsNullOrEmpty(message); } }
        public string message { get; private set; } = null;
        public object data { get; set; }

        public void Error(string msg)
        {
            message = msg;
        }
    }
}