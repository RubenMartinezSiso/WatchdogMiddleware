using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Net;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using InfluxDB.Client;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Routing;
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
        private readonly bool _activateLogs;

        private readonly HttpClient _httpClient = new HttpClient();
        private readonly RequestDelegate _next;
        private readonly ILogger<WatchdogMiddleware> _logger;

        public WatchdogMiddleware(RequestDelegate next, ILogger<WatchdogMiddleware> logger, string apiName, string influxDbUrl, string influxDbToken, string influxDbOrg, string influxDbBucket, string dataTable, bool activateLogs)
        {
            _next = next;
            _logger = logger;
            _apiName = apiName;
            _influxDbUrl = influxDbUrl;
            _influxDbToken = influxDbToken;
            _influxDbOrg = influxDbOrg;
            _influxDbBucket = influxDbBucket;
            _dataTable = dataTable;
            _activateLogs = activateLogs;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var startTime = DateTime.UtcNow;
            var requestTask = Task.Run(() => LogRequest(context));

            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            try
            {
                await _next(context);

                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);

                var responseTask = Task.Run(() => LogResponse(context.Response, startTime));

                var request = await requestTask;
                var response = await responseTask;

                _ = Task.Run(() => WritePointToInfluxDB(request, response));
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }

        private async Task<InterceptedRequest> LogRequest(HttpContext context)
        {
            var clientIp = context.Request.HttpContext.Connection.RemoteIpAddress?.ToString();
            var (location, latitude, longitude) = await GetLocationFromIp(clientIp);

            RouteData routeData = context.GetRouteData();
            string action = routeData?.Values["action"]?.ToString() ?? "Unknown";
            string controller = routeData?.Values["controller"]?.ToString() ?? "Unknown";

            return new InterceptedRequest
            {
                Timestamp = DateTime.UtcNow,
                ApiName = _apiName != "" ? _apiName : "Unknown API",
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
        
        private Task<InterceptedResponse> LogResponse(HttpResponse response, DateTime startTime)
        {
            return Task.Run(() => new InterceptedResponse
            {
                Timestamp = DateTime.UtcNow,
                Duration = DateTime.UtcNow - startTime,
                StatusCode = response.StatusCode,
                Headers = response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                Body = GetResponseBodyAsync(response.Body as MemoryStream).Result
            });
        }

        private async Task<(string Location, double? Latitude, double? Longitude)> GetLocationFromIp(string ip)
        {
            try
            {
                // ip = "80.26.158.41"; // Madrid, Spain
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
            catch
            {
                return ("Unknown Location", null, null);
            }
        }

        private async Task<string> GetRequestBodyAsync(HttpRequest request)
        {
            request.EnableBuffering();
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true))
            {
                var body = await reader.ReadToEndAsync();
                request.Body.Seek(0, SeekOrigin.Begin);
                return body;
            }
        }

        private async Task<string> GetResponseBodyAsync(MemoryStream responseBody)
        {
            if (responseBody == null) return string.Empty;

            responseBody.Seek(0, SeekOrigin.Begin);
            var body = await new StreamReader(responseBody).ReadToEndAsync();
            responseBody.Seek(0, SeekOrigin.Begin);
            return body;
        }

        public async Task WritePointToInfluxDB(InterceptedRequest request, InterceptedResponse response)
        {
            try
            {
                using var client = InfluxDBClientFactory.Create(_influxDbUrl, _influxDbToken.ToCharArray());
                var writeApi = client.GetWriteApi();

                var point = PointData.Measurement(_dataTable)

                    // Timestamp
                    .Timestamp(request.Timestamp, WritePrecision.Ms)

                    // Tags
                    .Tag("req_api", request.ApiName)
                    .Tag("req_method", request.Method)
                    .Tag("req_path", request.Path)
                    .Tag("req_route_action", request.RouteAction)
                    .Tag("req_route_controller", request.RouteController)

                    // Request Fields
                    .Field("req_host", request.Host)
                    .Field("req_timestamp", request.Timestamp.ToString("o"))
                    .Field("req_query_string", request.QueryString)
                    .Field("req_client_ip", request.ClientIp)
                    .Field("req_location", request.Location)
                    .Field("req_latitude", request.Latitude)
                    .Field("req_longitude", request.Longitude)
                    .Field("req_body", request.Body)
                    .Field("req_headers", JsonConvert.SerializeObject(request.Headers))

                    // Response Fields
                    .Field("res_timestamp", response.Timestamp.ToString("o"))
                    .Field("res_status_code", response.StatusCode)
                    .Field("res_body", response.Body)
                    .Field("res_duration_ms", response.Duration.TotalMilliseconds)
                    .Field("res_headers", JsonConvert.SerializeObject(response.Headers));

                await Task.Run(() => writeApi.WritePoint(point, _influxDbBucket, _influxDbOrg));
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error writing to InfluxDB: {ex.Message}");
            }
        }
    }
}
