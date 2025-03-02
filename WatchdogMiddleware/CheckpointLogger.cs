using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using WatchdogMiddleware.Models;

namespace WatchdogMiddleware
{
    public class CheckpointLogger
    {
        private readonly WatchdogOptions _options;
        private readonly ILogger<CheckpointLogger> _logger;

        public CheckpointLogger(WatchdogOptions options, ILogger<CheckpointLogger> logger)
        {
            _options = options;
            _logger = logger;
        }

        public void LogCheckpoint(HttpContext context, string message, Dictionary<string, object> additionalData = null)
        {
            try
            {
                if (_options.ActivateLogs)
                    _logger.LogInformation("CheckpointLogger: Starting checkpoint processing");

                string apiName = _options.ApiName != "Unknown API" ? _options.ApiName : ExtractApiName();

                RouteData routeData = context.GetRouteData();
                string routeController = routeData?.Values["controller"]?.ToString() ?? "Unknown";
                string routeAction = routeData?.Values["action"]?.ToString() ?? "Unknown";

                string ip = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                using var client = InfluxDBClientFactory.Create(_options.InfluxDbUrl, _options.InfluxDbToken.ToCharArray());
                var writeApi = client.GetWriteApi();

                var point = PointData.Measurement(_options.CheckpointDataTable)
                    .Timestamp(DateTime.UtcNow, WritePrecision.Ms)
                    .Tag("ckpt_api", apiName)
                    .Tag("ckpt_path", context.Request.Path)
                    .Tag("ckpt_controller", routeController)
                    .Tag("ckpt_action", routeAction)
                    .Tag("ckpt_method", context.Request.Method)
                    .Tag("ckpt_ip", ip)
                    .Field("ckpt_timestamp", DateTime.UtcNow.ToString("o"))
                    .Field("ckpt_message", message)
                    .Field("ckpt_data", additionalData != null ? JsonConvert.SerializeObject(additionalData) : "{}");

                writeApi.WritePoint(point, _options.InfluxDbBucket, _options.InfluxDbOrg);

                if (_options.ActivateLogs)
                    _logger.LogInformation("CheckpointLogger: Process completed");
            }
            catch (Exception ex)
            {
                if (_options.ActivateLogs)
                    _logger.LogError(ex, "CheckpointLogger: Error while sending checkpoint to InfluxDB");
            }
        }

        private string ExtractApiName()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetEntryAssembly();
                if (assembly != null)
                {
                    string assemblyName = assembly.GetName().Name;
                    return !string.IsNullOrEmpty(assemblyName) ? assemblyName : _options.ApiName;
                }
            }
            catch (Exception ex)
            {
                if (_options.ActivateLogs)
                    _logger.LogError(ex, "CheckpointLogger: Error extracting API name from assembly");
            }
            return _options.ApiName;
        }
    }
}
