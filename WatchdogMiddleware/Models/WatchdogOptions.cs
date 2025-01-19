using System;
using System.Collections.Generic;
using WatchdogMiddleware.Models;

namespace WatchdogMiddleware.Models
{
    /// <summary>
    /// Configuration options for WatchdogMiddleware.
    /// 🛠️ Allows customization of logging behavior and InfluxDB connection settings.
    ///
    /// Properties:
    /// - **ApiName**: Name of the API being monitored (default: "Unknown API").
    /// - **ActivateLogs**: Toggles logging functionality (default: true).
    /// - **SensitiveRoutes**: A list of sensitive API routes to exclude from logging.
    /// - **InfluxDbHost/Port**: Address and port of the InfluxDB instance.
    /// - **InfluxDbToken**: Authentication token for InfluxDB.
    /// - **InfluxDbOrg/Bucket**: Organization and bucket used for storing logs.
    /// - **DataTable**: Name of the data table in InfluxDB.
    /// - **InfluxDbUrl**: Complete URL for InfluxDB (auto-generated based on host/port).
    ///
    /// Example:
    /// var options = new WatchdogOptions {
    ///     ApiName = "MyAPI",
    ///     InfluxDbBucket = "customBucket",
    ///     ActivateLogs = false
    /// };
    /// </summary>
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
    }
}
