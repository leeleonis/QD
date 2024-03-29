﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using DirectLineApi.ShippingEasy;
using Newtonsoft.Json;

namespace ShippingEasy.Responses
{
    public class CreateOrderResponse : ApiResponse
    {
        [JsonProperty]
        public Order Order { get; private set; }
    }
}