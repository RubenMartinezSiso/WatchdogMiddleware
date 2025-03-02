using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text;
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
        private readonly WatchdogOptions _options;
        private readonly RequestDelegate _next;
        private readonly ILogger<WatchdogMiddleware> _logger;

        private static readonly MemoryCache _locationCache = new(new MemoryCacheOptions { SizeLimit = 1000 });
        private static readonly HttpClient _httpClient;
        private static DateTime _lastErrorTime = DateTime.MinValue;
        private static int _consecutiveErrors = 0;
        private const int ERROR_THRESHOLD = 10;
        private const int ERROR_TIMEOUT_SECONDS = 15;
        private const int LIMIT_TIMEOUT_MILISECONDS = 500;

        static WatchdogMiddleware()
        {
            var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = 20,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(LIMIT_TIMEOUT_MILISECONDS)
            };
        }

        public WatchdogMiddleware(RequestDelegate next, ILogger<WatchdogMiddleware> logger, WatchdogOptions options)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? new WatchdogOptions();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var path = context.Request.Path.Value;
            var method = context.Request.Method.ToUpper();
            var sensitiveRoute = _options.SensitiveRoutes.FirstOrDefault(r => r.Path == path && r.Method.ToUpper() == method);

            if (sensitiveRoute != null && sensitiveRoute.DoNotLog)
            {
                await _next(context);
                return;
            }

            if (_options.ActivateLogs)
                _logger.LogInformation("WatchdogMiddleware: Starting request processing");

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
                if (_options.ActivateLogs)
                    _logger.LogError(ex, "WatchdogMiddleware: An error occurred during request processing");
            }

            if (_options.ActivateLogs)
                _logger.LogInformation("WatchdogMiddleware: Finished request processing");
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
                if (_options.ActivateLogs)
                    _logger.LogError(ex, "WatchdogMiddleware: Error encrypting body");

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
                    ApiName = _options.ApiName != "Unknown API" ? _options.ApiName : ExtractApiName(),
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
                if (_options.ActivateLogs)
                    _logger.LogError(ex, "WatchdogMiddleware: Error logging request");
                
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
                if (_options.ActivateLogs)
                    _logger.LogError(ex, "WatchdogMiddleware: Error logging response");
                
                return new InterceptedResponse();
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
                    _logger.LogError(ex, "WatchdogMiddleware: Error extracting API name from assembly");
            }

            return _options.ApiName;
        }

        private async Task<(string Location, double? Latitude, double? Longitude)> GetLocationFromIp(string ip)
        {
            ip = "88.4.132.1"; // For testing purposes
            if (string.IsNullOrEmpty(ip))
                return ("Unknown Location", null, null);

            if (_consecutiveErrors >= ERROR_THRESHOLD || (DateTime.UtcNow - _lastErrorTime).TotalSeconds < ERROR_TIMEOUT_SECONDS)
                return ("Unknown Location", null, null);

            if (_locationCache.TryGetValue(ip, out (string, double?, double?) cachedLocation))
                return cachedLocation;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(LIMIT_TIMEOUT_MILISECONDS));
                var response = await _httpClient.GetStringAsync($"http://ip-api.com/json/{ip}", cts.Token);

                var json = JObject.Parse(response);
                var location = ($"{json["city"]}, {json["regionName"]}, {json["country"]}", json["lat"]?.Value<double>(), json["lon"]?.Value<double>()
                );

                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetSize(1)
                    .SetAbsoluteExpiration(TimeSpan.FromHours(24));
                _locationCache.Set(ip, location, cacheEntryOptions);

                _consecutiveErrors = 0;
                _lastErrorTime = DateTime.MinValue;

                return location;
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                _lastErrorTime = DateTime.UtcNow;

                if (_options.ActivateLogs)
                    _logger.LogError(ex, "WatchdogMiddleware: Error getting location from IP");
                
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
                if (_options.ActivateLogs)
                    _logger.LogError(ex, "WatchdogMiddleware: Error reading request body");

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
                if (_options.ActivateLogs)
                    _logger.LogError(ex, "WatchdogMiddleware: Error reading response body");

                return string.Empty;
            }
        }

        private void WritePointToInfluxDB(InterceptedRequest request, InterceptedResponse response)
        {
            if (request == null || response == null)
            {
                if (_options.ActivateLogs)
                    _logger.LogError("WatchdogMiddleware: Cannot write to InfluxDB, request or response is null");
                
                return;
            }

            try
            {
                using var client = InfluxDBClientFactory.Create(_options.InfluxDbUrl, _options.InfluxDbToken.ToCharArray());
                var writeApi = client.GetWriteApi();

                var point = PointData.Measurement(_options.DataTable)
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

                writeApi.WritePoint(point, _options.InfluxDbBucket, _options.InfluxDbOrg);

                if (_options.ActivateLogs)
                    _logger.LogInformation("WatchdogMiddleware: Process completed");
            }
            catch (Exception ex)
            {
                if (_options.ActivateLogs)
                    _logger.LogError(ex, "WatchdogMiddleware: Error writing to InfluxDB");
            }
        }
    }
}
