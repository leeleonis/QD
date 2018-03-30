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

        public enum CarrierType { Other, DHL, FedEx, UPS, USPS, Winit, IDS };

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

        public enum DirectLine { IDS = 1 }
        public static Dictionary<DirectLine, string> DirectLineList()
        {
            return new Dictionary<DirectLine, string>()
            {
                {DirectLine.IDS, "IDS Contionental" }
            };
        }
        public enum DirectLineStatus { 未發貨, 運輸中, 已到貨 }
        public enum DirectLineBoxType { DirectLine, InventoryTransfer, FBA }
        public static Dictionary<DirectLineBoxType, string> BoxTypeList()
        {
            return new Dictionary<DirectLineBoxType, string>()
            {
                {DirectLineBoxType.DirectLine, "Direct Line" },
                {DirectLineBoxType.InventoryTransfer, "Inventory Transfer" },
                {DirectLineBoxType.FBA, "FBA" }
            };
        }
    }
}