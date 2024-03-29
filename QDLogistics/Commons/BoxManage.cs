﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using QDLogistics.Models;
using QDLogistics.Models.Repositiry;

namespace QDLogistics.Commons
{
    public class BoxManage : IDisposable
    {
        private QDLogisticsEntities db;

        private Box boxData;

        private bool Disposed = false;
        private TimeZoneConvert TimeZoneConvert;
        private HttpSessionStateBase Session;

        public BoxManage(HttpSessionStateBase session)
        {
            db = new QDLogisticsEntities();

            TimeZoneConvert = new TimeZoneConvert();
            Session = session;
        }

        public Box GetCurrentBox(DirectLine directLine, int warehouseID, int methodID = 0)
        {
            MyHelp.Log("Box", null, string.Format("取得當前未出貨的{0} Box", directLine.Abbreviation), Session);

            boxData = db.Box.Where(b => b.IsEnable && !b.IsReserved && b.DirectLine.Equals(directLine.ID) && b.WarehouseFrom.Equals(warehouseID) && b.FirstMileMethod.Equals(methodID) && b.ShippingStatus.Equals((byte)EnumData.DirectLineStatus.未發貨))
                .OrderByDescending(b => b.Create_at).FirstOrDefault();
            if (boxData == null)
            {
                MyHelp.Log("Box", null, string.Format("開始建立【{0}】新Box", directLine.Abbreviation), Session);

                string boxID = string.Format("{0}-{1}", directLine.Abbreviation, TimeZoneConvert.Utc.ToString("yyyyMMdd"));
                int count = db.Box.AsNoTracking().Where(b => b.IsEnable && b.DirectLine.Equals(directLine.ID) && b.BoxID.Contains(boxID)).Select(b => b.MainBox).Distinct().Count() + 1;
                byte[] Byte = BitConverter.GetBytes(count);
                Byte[0] += 64;
                boxData = new Box()
                {
                    IsEnable = true,
                    BoxID = string.Format("{0}-{1}", boxID, System.Text.Encoding.ASCII.GetString(Byte.Take(1).ToArray())),
                    DirectLine = directLine.ID,
                    FirstMileMethod = methodID,
                    WarehouseFrom = warehouseID,
                    BoxType = (byte)EnumData.DirectLineBoxType.DirectLine,
                    Create_at = TimeZoneConvert.Utc
                };
                boxData.MainBox = boxData.BoxID;
                db.Entry(boxData).State = System.Data.Entity.EntityState.Added;
                db.SaveChanges();

                MyHelp.Log("Box", boxData.BoxID, string.Format("Box【{0}】建立完成", boxData.BoxID), Session);
            }

            return boxData;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed) return;

            if (disposing)
            {
            }

            db.Dispose();
            db = null;
            boxData = null;
            TimeZoneConvert = null;
            Session = null;
            Disposed = true;
        }
    }
}