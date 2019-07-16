using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CarrierApi.Winit_Old
{
    public class Resfun
    {
        /// <summary>
        /// 顯示結果
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>

        public static dynamic funresult(dynamic obj, string targetUrl)
        {
            Type keyt = obj.GetType();

            if (keyt == typeof(getToken))
                return Rqfun.req<Received>(targetUrl, (getToken)obj);

            if (keyt == typeof(queryOutboundOrderList))
                return Rqfun.req<Received>(targetUrl, (queryOutboundOrderList)obj);

            if (keyt == typeof(queryOutboundOrder))
                return Rqfun.req<Received>(targetUrl, (queryOutboundOrder)obj);

            if (keyt == typeof(createOutboundInfo))
                return Rqfun.req<Received>(targetUrl, (createOutboundInfo)obj);

            if (keyt == typeof(createOutboundOrder))
                return Rqfun.req<Received>(targetUrl, (createOutboundOrder)obj);

            if (keyt == typeof(confirmOutboundOrder))
                return Rqfun.req<Received>(targetUrl, (confirmOutboundOrder)obj);

            if (keyt == typeof(voidOutboundOrder))
                return Rqfun.req<Received>(targetUrl, (voidOutboundOrder)obj);

            if (keyt == typeof(queryTrack))
                return Rqfun.req<Received>(targetUrl, (queryTrack)obj);

            if (keyt == typeof(queryWarehouse))
                return Rqfun.req<Received>(targetUrl, (queryWarehouse)obj);

            if (keyt == typeof(queryDeliveryWay))
                return Rqfun.req<Received>(targetUrl, (queryDeliveryWay)obj);

            return null;
            
            /* else if (keyt == typeof(Rgpd))
            {
                var Rgpd = (Rgpd)obj;
                var result = Rqfun.req<Received>(targetUrl, Rgpd);
                return result;
            }
            else if (keyt == typeof(getProductCategoryInfo))
            {
                var getProductCategoryInfo = (getProductCategoryInfo)obj;
                var result = Rqfun.req<Received>(targetUrl, getProductCategoryInfo);
                return result;
            }
            else if (keyt == typeof(getItemInformation))
            {
                var getItemInformation = (getItemInformation)obj;
                var result = Rqfun.req<Received>(targetUrl, getItemInformation);
                return result;
            } */
        }
    }
}