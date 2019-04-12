using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QDLogistics.Commons
{
    public static class EnumData
    {
        public enum YesNo { No, Yes };
        public enum ProcessStatus { 訂單管理, 待出貨, 包貨, 已出貨, 鎖定中 };
        public static Dictionary<ProcessStatus, string> ProcessStatusList()
        {
            return new Dictionary<ProcessStatus, string>()
            {
                { ProcessStatus.訂單管理, "訂單管理 Unmanaged" },
                { ProcessStatus.待出貨, "待出貨 Awaiting Dispatch" },
                { ProcessStatus.鎖定中, "鎖定中 Locked" },
                { ProcessStatus.已出貨, "已出貨 Fulfilled" }
            };
        }
        public enum OrderChangeStatus { 提交至待出貨區, 取消出貨, 已完成出貨, 狀態異常, 產品異常, 包裹回收 }

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

        public enum CarrierType { Other, DHL, FedEx, UPS, USPS, Winit, IDS, Sendle };

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

        public enum DirectLineStatus { 未發貨, 運輸中, 已到貨, 延誤中, 延誤後抵達, 取消 }
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

        public enum LabelStatus { 正常, 鎖定中, 完成, 作廢, 回收 }

        public enum CaseEventType { CancelShipment, UpdateTracking, UpdateShipment, ChangeShippingMethod, ResendShipment, ReturnPackage }
        public static Dictionary<CaseEventType, string> CaseEventTypeList()
        {
            return new Dictionary<CaseEventType, string>()
            {
                { CaseEventType.CancelShipment, "Cancel Shipment" },
                { CaseEventType.UpdateTracking, "Update Tracking" },
                { CaseEventType.UpdateShipment, "Update Shipment" },
                { CaseEventType.ChangeShippingMethod, "Change Shipping Method" },
                { CaseEventType.ResendShipment, "Resend Shipment" },
                { CaseEventType.ReturnPackage, "Return Package" }
            };
        }
        public enum CaseEventRequest { None, Successful, Investigating, InTransit, Lost, Failed }
        public enum CaseEventStatus { Open, Locked, Close, Error }

        public static string StateAbbreviationExpand(string abbr)
        {
            Dictionary<string, string> states = new Dictionary<string, string>
            {
                { "AL", "Alabama" },
                { "AK", "Alaska" },
                { "AZ", "Arizona" },
                { "AR", "Arkansas" },
                { "CA", "California" },
                { "CO", "Colorado" },
                { "CT", "Connecticut" },
                { "DE", "Delaware" },
                { "DC", "District of Columbia" },
                { "FL", "Florida" },
                { "GA", "Georgia" },
                { "HI", "Hawaii" },
                { "ID", "Idaho" },
                { "IL", "Illinois" },
                { "IN", "Indiana" },
                { "IA", "Iowa" },
                { "KS", "Kansas" },
                { "KY", "Kentucky" },
                { "LA", "Louisiana" },
                { "ME", "Maine" },
                { "MD", "Maryland" },
                { "MA", "Massachusetts" },
                { "MI", "Michigan" },
                { "MN", "Minnesota" },
                { "MS", "Mississippi" },
                { "MO", "Missouri" },
                { "MT", "Montana" },
                { "NE", "Nebraska" },
                { "NV", "Nevada" },
                { "NH", "New Hampshire" },
                { "NJ", "New Jersey" },
                { "NM", "New Mexico" },
                { "NY", "New York" },
                { "NC", "North Carolina" },
                { "ND", "North Dakota" },
                { "OH", "Ohio" },
                { "OK", "Oklahoma" },
                { "OR", "Oregon" },
                { "PA", "Pennsylvania" },
                { "RI", "Rhode Island" },
                { "SC", "South Carolina" },
                { "SD", "South Dakota" },
                { "TN", "Tennessee" },
                { "TX", "Texas" },
                { "UT", "Utah" },
                { "VT", "Vermont" },
                { "VA", "Virginia" },
                { "WA", "Washington" },
                { "WV", "West Virginia" },
                { "WI", "Wisconsin" },
                { "WY", "Wyoming" }
            };

            if (states.ContainsKey(abbr))
                return (states[abbr]);

            if (states.Any(a => a.Value.ToLower().Equals(abbr.ToLower())))
                return states.First(a => a.Value.ToLower().Equals(abbr.ToLower())).Key;

            return abbr;
        }
    }
}