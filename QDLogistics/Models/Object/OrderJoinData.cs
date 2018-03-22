using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QDLogistics.Models.Object
{
    public class OrderJoinData
    {
        public Orders order { get; set; }
        public Addresses address { get; set; }
        public Payments payment { get; set; }
        public Packages package { get; set; }
        public Items item { get; set; }
        public List<Items> items { get; set; }
        public int itemCount { get; set; }
        public ShippingMethod method { get; set; }

        public OrderJoinData()
        {
        }

        public OrderJoinData(OrderJoinData data)
        {
            order = data.order;
            address = data.address;
            payment = data.payment;
            package = data.package;
            item = data.item;
            items = data.items;
            itemCount = data.itemCount;
            method = data.method;
        }
    }
}