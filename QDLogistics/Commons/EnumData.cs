using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QDLogistics.Commons
{
    public static class EnumData
    {
        public enum ProcessStatus { 訂單管理, 待出貨, 包貨, 已出貨, 鎖定中 };
        public enum OrderChangeStatus { 提交至待出貨區, 取消出貨, 已完成出貨 }

        public enum Export { 正式, 簡易 };
        public enum ExportMethod { 外貨復出口, 國貨出口 };
        public static string GetExportMethod(int code)
        {
            string name = null;

            switch (code)
            {
                case 0:
                    name = "G3-81 (外貨復出口)";
                    break;
                case 1:
                    name = "G5-02 (國貨出口)";
                    break;
            }

            return name;
        }

        public enum CarrierType { Other, DHL, FedEx, UPS, USPS, Winit };
        public enum ShippingMethod
        {
            dhl_b2c, dhl_domestic_economy_select, dhl_domestic_express, dhl_domestic_express_0900, dhl_domestic_express_1030, dhl_domestic_express_1200, dhl_economy_select, dhl_europack, dhl_express_0900, dhl_express_1030, dhl_express_1200, dhl_express_easy, dhl_express_envelope, dhl_express_worldwide, dhl_express_worldwide_eu, dhl_globalmail_business, dhl_jetline, dhl_jumbo_box, dhl_medical_express, dhl_same_day, dhl_sprintline,
            fedex_2_day, fedex_2_day_am, fedex_2_day_am_one_rate, fedex_2_day_one_rate, fedex_distance_deferred, fedex_europe_first_international_priority, fedex_express_saver, fedex_express_saver_one_rate, fedex_first_overnight, fedex_first_overnight_one_rate, fedex_ground, fedex_ground_home_delivery, fedex_international_economy, fedex_international_first, fedex_international_priority, fedex_next_day_afternoon, fedex_next_day_early_morning, fedex_next_day_end_of_day, fedex_next_day_mid_morning, fedex_priority_overnight, fedex_priority_overnight_one_rate, fedex_same_day, fedex_same_day_city, fedex_standard_overnight, fedex_standard_overnight_one_rate
        };
        public enum BoxType
        {
            custom,
            dhl_document, dhl_domestic, dhl_express_document, dhl_express_envelope, dhl_flyer, dhl_jumbo_box, dhl_jumbo_document, dhl_jumbo_junior_document, dhl_jumbo_junior_parcel, dhl_jumbo_parcel, dhl_junior_jumbo_box, dhl_other_dhl_packaging, dhl_parcel,
            fedex_10kg_box, fedex_25kg_box, fedex_box, fedex_envelope, fedex_extra_large_box, fedex_large_box, fedex_medium_box, fedex_pak, fedex_small_box, fedex_tube
        };

        public enum AuthType { View, Edit, Insert, Delete };

        public enum TimeZone { UTC, EST, TST, PST, GMT, AEST, JST };
        public static Dictionary<TimeZone, string> TimeZoneList()
        {
            return new Dictionary<TimeZone, string>() { { TimeZone.UTC, "UTC" },
                { TimeZone.EST, "Eastern Standard Time" }, { TimeZone.TST, "Taipei Standard Time" }, { TimeZone.PST, "Pacific Standard Time" },
                { TimeZone.GMT, "Greenwich Mean Time" }, { TimeZone.AEST, "AUS Eastern Standard Time" }, { TimeZone.JST, "Tokyo Standard Time" }
            };
        }
        public static string GetTimeZnoe(TimeZone key)
        {
            Dictionary<TimeZone, string> list = TimeZoneList();

            if (!list.ContainsKey(key)) throw new Exception("TimeZone not found!");

            return list[key];
        }

        public enum TaskStatus { 未執行, 執行中, 執行完, 執行失敗 };

        public static Dictionary<int, string> Get_RMA_Reason_List()
        {
            return new Dictionary<int, string>()
            {
                { 1, "defective" },
                { 22, "no longer needed" },
                { 70, "website description is inaccurate" },
                { 4, "exchange" },
                { 16, "return to shipper" },
                { 19, "warranty" },
                { 3, "other" }
            };
        }
    }
}