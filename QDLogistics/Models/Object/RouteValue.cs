using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace QDLogistics.Models.Object
{
    public class RouteValue
    {
        public string controller { get; set; }
        public string action { get; set; }
        public int? id { get; set; }
        public int start { get; set; }
        public int length { get; set; }
        public string search { get; set; }

        public int? gId { get; set; }

        public RouteValue()
        {
            this.start = 0;
            this.length = 10;
        }

        public RouteValue(int start, int length, string search)
        {
            this.start = start;
            this.length = length;
            this.search = search;
        }

        public void setUrl(string controller, string action, int? id)
        {
            this.controller = controller;
            this.action = action;
            this.id = id;
        }
    }
}