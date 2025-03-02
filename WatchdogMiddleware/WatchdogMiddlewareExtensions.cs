using Microsoft.AspNetCore.Builder;
using WatchdogMiddleware.Models;

public static class WatchdogMiddlewareExtensions
{
    /// <summary>
    /// Enables the WatchdogMiddleware in the application pipeline.
    /// 🐕 Logs incoming requests and outgoing responses to InfluxDB for monitoring.
    /// 
    /// Usage:
    /// - Call this method in the `Configure` method of Startup.cs.
    /// - Optionally, pass a lambda to configure WatchdogOptions (e.g., API name, logging settings, etc.).
    /// 
    /// Example:
    /// app.UseWatchdogMiddleware(options => {
    ///     options.ApiName = "MyAPI";
    ///     options.ActivateLogs = true;
    /// });
    /// </summary>
    public static IApplicationBuilder UseWatchdogMiddleware(this IApplicationBuilder builder, Action<WatchdogOptions> configureOptions = null)
    {
        var options = new WatchdogOptions();
        configureOptions?.Invoke(options);
        WatchdogOptionsHolder.Options = options;
        return builder.UseMiddleware<WatchdogMiddleware.WatchdogMiddleware>(options);
    }
}