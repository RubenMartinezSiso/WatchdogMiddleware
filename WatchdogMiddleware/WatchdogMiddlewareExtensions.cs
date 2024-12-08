using System;
using Microsoft.AspNetCore.Builder;
using WatchdogMiddleware;

public static class WatchdogMiddlewareExtensions
{
    public static IApplicationBuilder UseWatchdogMiddleware(this IApplicationBuilder builder, string apiName)
    {
        return builder.UseMiddleware<WatchdogMiddleware.WatchdogMiddleware>(apiName);
    }
}
