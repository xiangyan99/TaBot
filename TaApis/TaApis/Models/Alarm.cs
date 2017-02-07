using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TaApis.Models
{
    public class Alarm
    {
        public string id;
        public string name;
        public string fromId;
        public string fromName;
        public string serviceUrl;
        public string conversationId;
        public double temperature;
    }
}