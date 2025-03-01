using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WatchdogMiddleware.Models;

namespace WatchdogMiddleware
{
    public static class CheckpointExtensions
    {
        /// <summary>
        /// Extension for facilitating the logging of "checkpoints" in the API.
        /// 📍 Checkpoints are used to record significant events (e.g., user login, critical operations, etc.)
        /// and store them in InfluxDB, enabling later analysis and visualization in Grafana.
        /// 
        /// Usage:
        /// - Call this method from any controller or middleware using the HttpContext, passing a custom message
        ///   and optionally a dictionary containing additional data.
        /// 
        /// Example:
        /// HttpContext.LogCheckpoint("User logged in", new Dictionary<string, object> { { "username", userData.username } });
        /// </summary>
        public static void LogCheckpoint(this HttpContext context, string message, Dictionary<string, object> additionalData = null)
        {
            var options = new WatchdogOptions();
            var loggerFactory = context.RequestServices.GetService<ILoggerFactory>();
            var checkpointLogger = new CheckpointLogger(options, loggerFactory.CreateLogger<CheckpointLogger>());
            checkpointLogger.LogCheckpoint(context, message, additionalData);
        }
    }
}
