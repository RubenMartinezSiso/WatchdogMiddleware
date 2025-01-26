using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatchdogMiddleware.Models
{
    internal class InterceptedResponse
    {
        public DateTime Timestamp { get; set; }
        public TimeSpan Duration { get; set; }
        public int StatusCode { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string Body { get; set; }
    }
}
