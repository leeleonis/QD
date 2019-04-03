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
            JsonResult result = new JsonResult();
            Dictionary<string, string> sourceList = new Dictionary<string, string>();

            try
            {
                foreach (int source in Enum.GetValues(typeof(OrderService.OrderSource)))
                {
                    sourceList.Add((source + 1).ToString(), Enum.GetName(typeof(OrderService.OrderSource), source));
                }

                string[] stateList = new string[] { "NSW", "QLD", "SA", "TAS", "VIC", "WA", "NT", "ACT" };
                result.data = new
                {
                    countryList = MyHelp.GetCountries().ToDictionary(c => c.ID, c => c.Name),
                    stateList = stateList.ToDictionary(s => s),
                    companyList = db.Companies.AsNoTracking().ToDictionary(c => c.ID.ToString(), c => c.CompanyName),
                    sourceList,
                    methodList = db.Services.AsNoTracking().Where(s => s.IsEnable.Value).ToDictionary(s => s.ServiceCode, s => s.ServiceCode),
                    warehouseList = db.Warehouses.AsNoTracking().Where(w => w.IsEnable.Value && w.IsSellable.Value).OrderByDescending(w => w.IsDefault.Value).OrderBy(w => w.ID).ToDictionary(w => w.ID.ToString(), w => w.Name),
                    productTypeList = db.ProductType.AsNoTracking().Where(t => t.IsEnable && t.CompanyID.Equals(163)).OrderBy(t => t.ProductTypeName).ToDictionary(t => t.ID.ToString(), t => t.ProductTypeName),
                    brandList = db.Manufacturers.AsNoTracking().Where(b => b.IsEnable.Value && b.CompanyID.Value.Equals(163)).OrderBy(b => b.ManufacturerName).ToDictionary(b => b.ID.ToString(), b => b.ManufacturerName),
                    battery = new Dictionary<string, string>() { { "1", "Yes" }, { "0", "No" } }
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

        public ActionResult AjaxData(int draw, List<Dictionary<string, string>> order, int start, int length)
        {
            var total = 0;
            List<object> dataList = new List<object>();

            var results = db.Preset.AsNoTracking().Where(p => p.IsEnable);
            if (order.Any())
            {
                string dir = order[0].First(o => o.Key.Equals("dir")).Value;

                switch (int.Parse(order[0].First(o => o.Key.Equals("column")).Value))
                {
                    case 3:
                        results = MyHelp.SetOrder(results, p => p.Type, dir);
                        break;
                    case 5:
                        results = MyHelp.SetOrder(results, p => p.Total, dir);
                        break;
                    case 6:
                        results = MyHelp.SetOrder(results, p => p.Country, dir);
                        break;
                    case 7:
                        results = MyHelp.SetOrder(results, p => p.State, dir);
                        break;
                    case 9:
                        results = MyHelp.SetOrder(results, p => p.CompanyID, dir);
                        break;
                    case 10:
                        results = MyHelp.SetOrder(results, p => p.SourceID, dir);
                        break;
                    case 11:
                        results = MyHelp.SetOrder(results, p => p.Amount, dir);
                        break;
                    case 12:
                        results = MyHelp.SetOrder(results, p => p.ShippingMethod, dir);
                        break;
                    case 13:
                        results = MyHelp.SetOrder(results, p => p.Sku, dir);
                        break;
                    case 14:
                        results = MyHelp.SetOrder(results, p => p.Suffix, dir);
                        break;
                    case 15:
                        results = MyHelp.SetOrder(results, p => p.ProductType, dir);
                        break;
                    case 16:
                        results = MyHelp.SetOrder(results, p => p.Brand, dir);
                        break;
                    case 17:
                        results = MyHelp.SetOrder(results, p => p.Battery, dir);
                        break;
                    case 18:
                        results = MyHelp.SetOrder(results, p => p.Weight, dir);
                        break;
                    default:
                        results = MyHelp.SetOrder(results, p => p.Id, "asc");
                        break;
                }
            }

            if (results.Any())
            {
                total = results.Count();
                foreach (Preset preset in results.Skip(start).Take(length).ToList())
                {
                    dataList.Add(new
                    {
                        visible = preset.IsVisible,
                        dispatch = preset.IsDispatch,
                        id = preset.Id,
                        type = preset.Type,
                        priority = preset.Priority,
                        value = preset.Value,
                        valueType = preset.ValueType,
                        total = preset.Total,
                        totalType = preset.TotalType,
                        country = preset.Country,
                        state = preset.State,
                        zipCodeFrom = preset.ZipCodeFrom,
                        zipCodeTo = preset.ZipCodeTo,
                        company = preset.CompanyID,
                        channel = preset.SourceID,
                        amount = preset.Amount,
                        amountType = preset.AmountType,
                        method = preset.ShippingMethod,
                        sku = preset.Sku,
                        suffix = preset.Suffix,
                        productType = preset.ProductType,
                        brand = preset.Brand,
                        battery = preset.Battery,
                        weight = preset.Weight,
                        weightType = preset.WeightType,
                        warehouse = preset.WarehouseID,
                        shippingMethod = preset.MethodID
                    });
                }
            }

            return Json(new { draw, data = dataList, recordsFiltered = total, recordsTotal = total });
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