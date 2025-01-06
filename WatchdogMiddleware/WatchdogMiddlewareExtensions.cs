using System;
using Microsoft.AspNetCore.Builder;
using WatchdogMiddleware;

public static class WatchdogMiddlewareExtensions
{
    /// <summary>
    /// Use WatchdogMiddleware to log requests and responses to InfluxDB 🐕
    /// </summary>
    /// <param name="apiName">Identifying name of the API</param>
    public static IApplicationBuilder UseWatchdogMiddleware(this IApplicationBuilder builder, string apiName)
    {
        return builder.UseMiddleware<WatchdogMiddleware.WatchdogMiddleware>(apiName);
    }
}
