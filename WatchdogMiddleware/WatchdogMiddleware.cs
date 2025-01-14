using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WatchdogMiddleware.Models;

namespace WatchdogMiddleware
{
    public class WatchdogMiddleware
    {
        private readonly string _apiName;
        private readonly string _influxDbUrl;
        private readonly string _influxDbToken;
        private readonly string _influxDbOrg;
        private readonly string _influxDbBucket;
        private readonly string _dataTable;
        private readonly List<SensitiveRoute> _sensitiveRoutes;
        private readonly bool _activateLogs;

        private readonly HttpClient _httpClient;
        private readonly RequestDelegate _next;
        private readonly ILogger<WatchdogMiddleware> _logger;

        public WatchdogMiddleware(RequestDelegate next, ILogger<WatchdogMiddleware> logger, string apiName, string influxDbUrl, string influxDbToken, string influxDbOrg, string influxDbBucket, string dataTable, bool activateLogs, List<SensitiveRoute> sensitiveRoutes)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiName = apiName ?? "Unknown API";
            _influxDbUrl = influxDbUrl ?? throw new ArgumentNullException(nameof(influxDbUrl));
            _influxDbToken = influxDbToken ?? throw new ArgumentNullException(nameof(influxDbToken));
            _influxDbOrg = influxDbOrg ?? throw new ArgumentNullException(nameof(influxDbOrg));
            _influxDbBucket = influxDbBucket ?? throw new ArgumentNullException(nameof(influxDbBucket));
            _dataTable = dataTable ?? throw new ArgumentNullException(nameof(dataTable));
            _activateLogs = activateLogs;
            _sensitiveRoutes = sensitiveRoutes ?? new List<SensitiveRoute>();
            _httpClient = new HttpClient();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var path = context.Request.Path.Value;
            var method = context.Request.Method.ToUpper();
            var sensitiveRoute = _sensitiveRoutes.FirstOrDefault(r => r.Path == path && r.Method.ToUpper() == method);

            if (sensitiveRoute != null && sensitiveRoute.DoNotLog)
            {
                await _next(context);
                return;
            }

            if (_activateLogs)
            {
                _logger.LogInformation("WatchdogMiddleware: Starting request processing");
            }

            var startTime = DateTime.UtcNow;
            InterceptedRequest request = null;
            InterceptedResponse response = null;

            try
            {
                request = await LogRequest(context);
                var originalBodyStream = context.Response.Body;
                using var responseBody = new MemoryStream();
                context.Response.Body = responseBody;

                try
                {
                    await _next(context);
                }
                finally
                {
                    responseBody.Seek(0, SeekOrigin.Begin);
                    await responseBody.CopyToAsync(originalBodyStream);
                    response = await LogResponse(context.Response, startTime);
                    context.Response.Body = originalBodyStream;
                }

                if (sensitiveRoute != null && sensitiveRoute.Encrypt)
                {
                    request.Body = EncryptBody(request.Body);
                    response.Body = EncryptBody(response.Body);
                }

                WritePointToInfluxDB(request, response);
            }
            catch (Exception ex)
            {
                if (_activateLogs)
                {
                    _logger.LogError(ex, "WatchdogMiddleware: An error occurred during request processing");
                }
            }

            if (_activateLogs)
            {
                _logger.LogInformation("WatchdogMiddleware: Finished request processing");
            }
        }

        private string EncryptBody(string body)
        {
            try
            {
                using (var sha256 = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(body);
                    byte[] hashBytes = sha256.ComputeHash(inputBytes);
                    return Convert.ToBase64String(hashBytes);
                }
            }
            catch (Exception ex)
            {
                if (_activateLogs)
                {
                    _logger.LogError(ex, "WatchdogMiddleware: Error encrypting body");
                }
                return string.Empty;
            }
        }

        private async Task<InterceptedRequest> LogRequest(HttpContext context)
        {
            try
            {
                var clientIp = context.Request.HttpContext.Connection.RemoteIpAddress?.ToString();
                var (location, latitude, longitude) = await GetLocationFromIp(clientIp);

                RouteData routeData = context.GetRouteData();
                string action = routeData?.Values["action"]?.ToString() ?? "Unknown";
                string controller = routeData?.Values["controller"]?.ToString() ?? "Unknown";

                return new InterceptedRequest
                {
                    Timestamp = DateTime.UtcNow,
                    ApiName = _apiName,
                    Method = context.Request.Method,
                    Path = context.Request.Path,
                    QueryString = context.Request.QueryString.ToString(),
                    Headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                    ClientIp = clientIp,
                    Host = Dns.GetHostName(),
                    Body = await GetRequestBodyAsync(context.Request),
                    Location = location,
                    Latitude = latitude,
                    Longitude = longitude,
                    RouteAction = action,
                    RouteController = controller
                };
            }
            catch (Exception ex)
            {
                if (_activateLogs)
                {
                    _logger.LogError(ex, "WatchdogMiddleware: Error logging request");
                }
                return new InterceptedRequest();
            }
        }

        private async Task<InterceptedResponse> LogResponse(HttpResponse response, DateTime startTime)
        {
            try
            {
                return new InterceptedResponse
                {
                    Timestamp = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime,
                    StatusCode = response.StatusCode,
                    Headers = response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                    Body = await GetResponseBodyAsync(response.Body as MemoryStream)
                };
            }
            catch (Exception ex)
            {
                if (_activateLogs)
                {
                    _logger.LogError(ex, "WatchdogMiddleware: Error logging response");
                }
                return new InterceptedResponse();
            }
        }

        private async Task<(string Location, double? Latitude, double? Longitude)> GetLocationFromIp(string ip)
        {
            try
            {
                ip = "88.4.135.170"; // Fraga, Spain
                var response = await _httpClient.GetStringAsync($"http://ip-api.com/json/{ip}");
                var json = JObject.Parse(response);

                var location = $"{json["city"]}, {json["regionName"]}, {json["country"]}";

                return (
                    location,
                    json["lat"]?.Value<double>(),
                    json["lon"]?.Value<double>()
                );
            }
            catch (Exception ex)
            {
                if (_activateLogs)
                {
                    _logger.LogError(ex, "WatchdogMiddleware: Error getting location from IP");
                }
                return ("Unknown Location", null, null);
            }
        }

        private async Task<string> GetRequestBodyAsync(HttpRequest request)
        {
            try
            {
                request.EnableBuffering();
                using (var reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true))
                {
                    var body = await reader.ReadToEndAsync();
                    request.Body.Seek(0, SeekOrigin.Begin);
                    return body;
                }
            }
            catch (Exception ex)
            {
                if (_activateLogs)
                {
                    _logger.LogError(ex, "WatchdogMiddleware: Error reading request body");
                }
                return string.Empty;
            }
        }

        private async Task<string> GetResponseBodyAsync(MemoryStream responseBody)
        {
            try
            {
                if (responseBody == null) return string.Empty;

                responseBody.Seek(0, SeekOrigin.Begin);
                var body = await new StreamReader(responseBody).ReadToEndAsync();
                responseBody.Seek(0, SeekOrigin.Begin);
                return body;
            }
            catch (Exception ex)
            {
                if (_activateLogs)
                {
                    _logger.LogError(ex, "WatchdogMiddleware: Error reading response body");
                }
                return string.Empty;
            }
        }

        private void WritePointToInfluxDB(InterceptedRequest request, InterceptedResponse response)
        {
            if (request == null || response == null)
            {
                if (_activateLogs)
                {
                    _logger.LogError("WatchdogMiddleware: Cannot write to InfluxDB, request or response is null");
                }
                return;
            }

            try
            {
                using var client = InfluxDBClientFactory.Create(_influxDbUrl, _influxDbToken.ToCharArray());
                var writeApi = client.GetWriteApi();

                var point = PointData.Measurement(_dataTable)
                    .Timestamp(request.Timestamp, WritePrecision.Ms)
                    .Tag("req_api", request.ApiName)
                    .Tag("req_method", request.Method)
                    .Tag("req_path", request.Path)
                    .Tag("req_route_action", request.RouteAction)
                    .Tag("req_route_controller", request.RouteController)
                    .Field("req_host", request.Host)
                    .Field("req_timestamp", request.Timestamp.ToString("o"))
                    .Field("req_query_string", request.QueryString)
                    .Field("req_client_ip", request.ClientIp)
                    .Field("req_location", request.Location)
                    .Field("req_latitude", request.Latitude)
                    .Field("req_longitude", request.Longitude)
                    .Field("req_body", request.Body)
                    .Field("req_headers", JsonConvert.SerializeObject(request.Headers))
                    .Field("res_timestamp", response.Timestamp.ToString("o"))
                    .Field("res_status_code", response.StatusCode)
                    .Field("res_body", response.Body)
                    .Field("res_duration_ms", response.Duration.TotalMilliseconds)
                    .Field("res_headers", JsonConvert.SerializeObject(response.Headers));

                writeApi.WritePoint(point, _influxDbBucket, _influxDbOrg);
            }
            catch (Exception ex)
            {
                if (_activateLogs)
                {
                    _logger.LogError(ex, "WatchdogMiddleware: Error writing to InfluxDB");
                }
            }
        }
    }
}
