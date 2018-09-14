using CarrierApi.DHL;
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
        private Orders _order;
        private Packages _package;
        private Carriers _carrier;
        private string _trackingNumber;

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
            _order = order;
            _package = package;
            _carrier = _package.Method != null ? _package.Method.Carriers : null;
            _trackingNumber = _package.TrackingNumber;
        }

        public TrackResult Track()
        {
            TrackResult result = new TrackResult();

            try
            {
                TimeZoneConvert timeZone = new TimeZoneConvert();

                if (_order.Payments.Any())
                {
                    result.PaymentDate = timeZone.InitDateTime(_order.Payments.First().AuditDate.Value, EnumData.TimeZone.EST).Utc;
                }

                if (_carrier == null) throw new Exception("Not found carrier!");

                switch (_carrier.Name)
                {
                    case "DHL":
                        result = DHL_Track(_carrier.CarrierAPI, _trackingNumber);
                        break;
                    case "FedEx":
                        result = FedEx_Track(_carrier.CarrierAPI, _trackingNumber);
                        break;
                    case "Sendle":
                        result = Sendle_Track(_carrier.CarrierAPI, _trackingNumber);
                        break;
                }
            }
            catch (Exception e)
            {
                throw new Exception(_order.OrderID + " - " + e.Message);
            }

            return result;
        }

        public TrackResult Track(string warehouseID, string outboundNum, string trackingNum = "")
        {
            TrackResult result = new TrackResult();

            try
            {
                TimeZoneConvert timeZone = new TimeZoneConvert();
                
                if (_order.Payments.Any())
                {
                    result.PaymentDate = timeZone.InitDateTime(_order.Payments.First().AuditDate.Value, EnumData.TimeZone.EST).Utc;
                }

                Winit_API winit = new Winit_API();
                Received track = winit.Tracking(warehouseID, outboundNum, trackingNum);

                if (track.code != "0") throw new Exception(track.msg);

                trackData[] Winit_EventList = track.data.ToObject<trackData[]>();

                if (Winit_EventList.Any())
                {
                    if (Winit_EventList.Any(e => e.status == "DIC"))
                    {
                        result.PickUpDate = Winit_EventList.First(e => e.status == "DIC").scandateTime.ToUniversalTime();
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
                throw new Exception(_order.OrderID + " - " + e.Message);
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
                        result.PickUpDate = DHL_EventList.Where(e => e.ServiceEvent.EventCode == "PU").Select(e => timeZone.InitDateTime(e.Date.Add(e.Time.TimeOfDay), EnumData.TimeZone.TST).Utc).First();
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
                result.DeliveryStatus = (int)OrderService.DeliveryStatusType.Delivered;
            }

            return result;
        }

        private TrackResult FedEx_Track(CarrierAPI api, string trackingNumber)
        {
            TrackResult result = new TrackResult();

            FedEx_API FedEx = new FedEx_API(api);
            TrackReply FedEx_Result = FedEx.Tracking(trackingNumber);
            TrackDetail detail = FedEx_Result.CompletedTrackDetails[0].TrackDetails[0];

            if (detail.Events != null)
            {
                List<TrackEvent> FedEx_EventList = detail.Events.ToList();

                if (FedEx_EventList.Any(e => e.EventType == "PU"))
                {
                    result.PickUpDate = FedEx_EventList.First(e => e.EventType == "PU").Timestamp.ToUniversalTime();
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
                    result.PickUpDate = Sendle_Result.tracking_events.First(e => e.event_type == "Pickup").scan_time;
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
        private DateTime _paymentDate;
        private DateTime _pickUpDate;
        private DateTime _deliveryDate;

        public TrackResult()
        {
            DeliveryStatus = (int)OrderService.DeliveryStatusType.UnShipped;
        }

        public DateTime PaymentDate
        {
            get { return this._paymentDate; }
            set { this._paymentDate = value; }
        }
        public DateTime PickUpDate
        {
            get { return this._pickUpDate; }
            set { this._pickUpDate = value; }
        }
        public DateTime DeliveryDate
        {
            get { return this._deliveryDate; }
            set { this._deliveryDate = value; }
        }
        public int DeliveryStatus { get; set; }
        public string DeliveryNote { get; set; }
        public TimeSpan DispatchTime
        {
            get
            {
                if(!_paymentDate.Equals(DateTime.MinValue) && !_pickUpDate.Equals(DateTime.MinValue))
                {
                    return this._pickUpDate - this._paymentDate;
                }

                return TimeSpan.Zero;
            }
        }
        public TimeSpan TransitTime
        {
            get
            {
                if (!_pickUpDate.Equals(DateTime.MinValue) && !_deliveryDate.Equals(DateTime.MinValue))
                {
                    return this._deliveryDate - this._pickUpDate;
                }

                return TimeSpan.Zero;
            }
        }
    }
}