﻿using CarrierApi.DHL;
using CarrierApi.FedEx;
using CarrierApi.Sendle;
using CarrierApi.Winit;
using QDLogistics.FedExTrackService;
using QDLogistics.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QDLogistics.Commons
{
    public class TrackOrder
    {
        private Orders orderData;
        private Packages packageData;
        private Carriers firstMile_carrierData;
        private Carriers carrierData;

        private TimeZoneConvert time_zone = new TimeZoneConvert();

        public TrackOrder()
        {
        }

        public TrackOrder(Packages package)
        {
            SetOrder(package.Orders, package);
        }

        public TrackOrder(Orders order, Packages package)
        {
            SetOrder(order, package);
        }

        public void SetOrder(Orders order, Packages package)
        {
            orderData = order;
            packageData = package;
            carrierData = packageData.Method?.Carriers;
        }

        public TrackResult Track()
        {
            TrackResult result = new TrackResult();

            try
            {
                if (orderData.Payments.Any())
                {
                    result.PaymentDate = time_zone.InitDateTime(orderData.Payments.First().AuditDate.Value, EnumData.TimeZone.EST).Utc;
                }
                
                if (carrierData == null) throw new Exception("Not found carrier!");

                switch (carrierData.CarrierAPI.Type.Value)
                {
                    case (byte)EnumData.CarrierType.DHL:
                        result = DHL_Track(carrierData.CarrierAPI, packageData.TrackingNumber);
                        break;
                    case (byte)EnumData.CarrierType.FedEx:
                        result = FedEx_Track(carrierData.CarrierAPI, packageData.TrackingNumber);
                        break;
                    case (byte)EnumData.CarrierType.Sendle:
                        result = Sendle_Track(carrierData.CarrierAPI, packageData.TrackingNumber);
                        break;
                }
            }
            catch (Exception e)
            {
                throw new Exception(orderData.OrderID + " - " + e.Message);
            }

            return result;
        }

        public TrackResult Track(string warehouseID, string outboundNum, string trackingNum = "")
        {
            TrackResult result = new TrackResult();

            try
            {
                if (orderData.Payments.Any())
                {
                    result.PaymentDate = time_zone.InitDateTime(orderData.Payments.First().AuditDate.Value, EnumData.TimeZone.EST).Utc;
                }

                Winit_API winit = new Winit_API();
                Received track = winit.Tracking(warehouseID, outboundNum, trackingNum);

                if (track.code != "0") throw new Exception(track.msg);

                trackData[] Winit_EventList = track.data.ToObject<trackData[]>();

                if (Winit_EventList.Any())
                {
                    if (Winit_EventList.Any(e => e.status == "DIC"))
                    {
                        result.PickupDate = Winit_EventList.First(e => e.status == "DIC").scandateTime.ToUniversalTime();
                        result.DeliveryStatus = (int)OrderService.DeliveryStatusType.Intransit;
                    }

                    result.DeliveryNote = Winit_EventList.Select(e => e.scandateTime.ToString() + " " + e.trackingmess).Last();

                    if (Winit_EventList.Any(e => e.status == "DLC"))
                    {
                        result.DeliveryDate = Winit_EventList.First(e => e.status == "DLC").scandateTime.ToUniversalTime();
                        result.DeliveryStatus = (int)OrderService.DeliveryStatusType.Delivered;
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception(orderData.OrderID + " - " + e.Message);
            }

            return result;
        }

        public TrackResult Track(Box box, CarrierAPI api)
        {
            TrackResult result = new TrackResult();

            try
            {
                switch (api.Type)
                {
                    case (byte)EnumData.CarrierType.DHL:
                        result = DHL_Track(api, box.TrackingNumber);
                        break;
                    case (byte)EnumData.CarrierType.FedEx:
                        result = FedEx_Track(api, box.TrackingNumber);
                        break;
                }
            }
            catch (Exception e)
            {
                throw new Exception(box.BoxID + " - " + e.Message);
            }

            return result;
        }

        private TrackResult DHL_Track(CarrierAPI api, string trackingNumber)
        {
            TrackResult result = new TrackResult();

            DHL_API DHL = new DHL_API(api);
            TrackingResponse DHL_Result = DHL.Tracking(trackingNumber);

            TimeZoneConvert timeZone = new TimeZoneConvert();
            if (DHL_Result != null && DHL_Result.AWBInfo.Any(awb => awb.ShipmentInfo != null && !string.IsNullOrEmpty(awb.ShipmentInfo.ConsigneeName)))
            {
                List<ShipmentEvent> DHL_EventList = DHL_Result.AWBInfo.First(awb => awb.ShipmentInfo != null && !string.IsNullOrEmpty(awb.ShipmentInfo.ConsigneeName)).ShipmentInfo.Items.Skip(1).Cast<ShipmentEvent>().ToList();

                if (DHL_EventList.Any())
                {
                    if (DHL_EventList.Any(e => e.ServiceEvent.EventCode == "PU"))
                    {
                        result.PickupDate = DHL_EventList.Where(e => e.ServiceEvent.EventCode == "PU").Select(e => timeZone.InitDateTime(e.Date.Add(e.Time.TimeOfDay), EnumData.TimeZone.TST).Utc).First();
                        result.DeliveryStatus = (int)OrderService.DeliveryStatusType.Intransit;
                    }

                    result.DeliveryNote = DHL_EventList.Select(e => timeZone.InitDateTime(e.Date.Add(e.Time.TimeOfDay), EnumData.TimeZone.TST).Utc.ToString() + " " + e.ServiceEvent.Description).Last();

                    if (DHL_EventList.Any(e => e.ServiceEvent.EventCode == "OK"))
                    {
                        result.DeliveryDate = DHL_EventList.Where(e => e.ServiceEvent.EventCode == "OK").Select(e => timeZone.InitDateTime(e.Date.Add(e.Time.TimeOfDay), EnumData.TimeZone.TST).Utc).First();
                        result.DeliveryStatus = (int)OrderService.DeliveryStatusType.Delivered;
                    }
                }
            }
            else
            {
                result.DeliveryNote = DHL_Result.AWBInfo.First().Status.ActionStatus;
                //result.DeliveryStatus = (int)OrderService.DeliveryStatusType.Delivered;
            }

            return result;
        }

        private TrackResult FedEx_Track(CarrierAPI api, string trackingNumber)
        {
            TrackResult result = new TrackResult();

            FedEx_API FedEx = new FedEx_API(api);
            TrackReply FedEx_Result = FedEx.Tracking(trackingNumber);

            if(FedEx_Result.HighestSeverity != NotificationSeverityType.SUCCESS)
            {
                throw new Exception(string.Join(";", FedEx_Result.Notifications.Select(n => n.Message).ToArray()));
            }

            TrackDetail detail = FedEx_Result.CompletedTrackDetails[0].TrackDetails[0];

            if (detail.Events != null)
            {
                List<TrackEvent> FedEx_EventList = detail.Events.ToList();

                if (FedEx_EventList.Any(e => e.EventType == "PU"))
                {
                    result.PickupDate = FedEx_EventList.First(e => e.EventType == "PU").Timestamp.ToUniversalTime();
                    result.DeliveryStatus = (int)OrderService.DeliveryStatusType.Intransit;
                }

                result.DeliveryNote = FedEx_EventList.Select(e => e.Timestamp.ToString() + " " + e.EventDescription).First();

                if (FedEx_EventList.Any(e => e.EventType == "DL"))
                {
                    result.DeliveryDate = FedEx_EventList.First(e => e.EventType == "DL").Timestamp.ToUniversalTime();
                    result.DeliveryStatus = (int)OrderService.DeliveryStatusType.Delivered;
                }
            }

            return result;
        }

        private TrackResult Sendle_Track(CarrierAPI api, string trackingNumber)
        {
            TrackResult result = new TrackResult();

            Sendle_API Sendle = new Sendle_API(api);
            Sendle_API.TrackResponse Sendle_Result = Sendle.Track(trackingNumber);

            if (Sendle_Result.tracking_events.Any())
            {
                if(Sendle_Result.tracking_events.Any(e => e.event_type == "Pickup"))
                {
                    result.PickupDate = Sendle_Result.tracking_events.First(e => e.event_type == "Pickup").scan_time;
                    result.DeliveryStatus = (int)OrderService.DeliveryStatusType.Intransit;
                }

                if (Sendle_Result.tracking_events.Any(e => e.event_type == "Delivered"))
                {
                    result.DeliveryDate = Sendle_Result.tracking_events.First(e => e.event_type == "Delivered").scan_time;
                    result.DeliveryStatus = (int)OrderService.DeliveryStatusType.Delivered;
                }

                result.DeliveryNote = Sendle_Result.tracking_events.Select(e => e.scan_time.ToString() + " " + e.description).Last();
            }

            return result;
        }
    }

    public class TrackResult
    {
        public TrackResult()
        {
            DeliveryStatus = (int)OrderService.DeliveryStatusType.UnShipped;
        }

        public DateTime PaymentDate { get; set; }
        public DateTime? PickupDate { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public int DeliveryStatus { get; set; }
        public string DeliveryNote { get; set; }
        public TimeSpan DispatchTime
        {
            get
            {
                if(!PaymentDate.Equals(DateTime.MinValue) && PickupDate.HasValue)
                {
                    return PickupDate.Value - PaymentDate;
                }

                return TimeSpan.Zero;
            }
        }
        public TimeSpan TransitTime
        {
            get
            {
                if (PickupDate.HasValue && DeliveryDate.HasValue)
                {
                    return DeliveryDate.Value - PickupDate.Value;
                }

                return TimeSpan.Zero;
            }
        }
    }
}