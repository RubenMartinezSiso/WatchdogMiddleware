using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace WatchdogMiddleware
{
    public class InterceptedRequest
    {
        public DateTime Timestamp { get; set; }
        public string ApiName { get; set; }
        public string Method { get; set; }
        public string Path { get; set; }
        public string QueryString { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string ClientIp { get; set; }
        public string Location { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string Host { get; set; }
        public string Body { get; set; }
        public string RouteAction { get; set; }
        public string RouteController { get; set; }
    }
}