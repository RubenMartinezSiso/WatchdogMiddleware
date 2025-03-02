using System;
using System.Collections.Generic;
using WatchdogMiddleware.Models;

namespace WatchdogMiddleware.Models
{
    public class WatchdogOptions
    {
        public string ApiName { get; set; } = "Unknown API";
        public bool ActivateLogs { get; set; } = true;
        public List<SensitiveRoute> SensitiveRoutes { get; set; } = new List<SensitiveRoute>();

        public string InfluxDbHost { get; set; } = "influxdb";
        public string InfluxDbPort { get; set; } = "8086";
        public string InfluxDbToken { get; set; } = "1a4aeaa65859e8443d824ee73d82432f";
        public string InfluxDbOrg { get; set; } = "watchdogorg";
        public string InfluxDbBucket { get; set; } = "watchdogbucket";
        public string DataTable { get; set; } = "watchdogdatatable";
        public string InfluxDbUrl { get; set; } = "http://localhost:8086";
        public string CheckpointDataTable { get; set; } = "checkpointsdatatable";
    }
}
