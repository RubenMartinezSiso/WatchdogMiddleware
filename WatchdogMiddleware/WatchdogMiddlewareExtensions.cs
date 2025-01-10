using Microsoft.AspNetCore.Builder;

public static class WatchdogMiddlewareExtensions
{
    /// <summary>
    /// Use WatchdogMiddleware to log requests and responses to InfluxDB 🐕
    /// </summary>
    /// <param name="apiName">Identifying name of the API</param>
    /// <param name="influxDbUrl">URL of the InfluxDB server</param>
    /// <param name="influxDbToken">Token for InfluxDB authentication</param>
    /// <param name="influxDbOrg">Organization name in InfluxDB</param>
    /// <param name="influxDbBucket">Bucket name in InfluxDB</param>
    /// <param name="dataTable">Name of the table to write data to in InfluxDB</param>
    /// <param name="activateLogs">Activate logs to console (defatult: true)</param>
    /// <returns></returns>
    public static IApplicationBuilder UseWatchdogMiddleware(this IApplicationBuilder builder, string apiName, string influxDbUrl, string influxDbToken, string influxDbOrg, string influxDbBucket, string dataTable, bool activateLogs = true)
    {
        return builder.UseMiddleware<WatchdogMiddleware.WatchdogMiddleware>(apiName, influxDbUrl, influxDbToken, influxDbOrg, influxDbBucket, dataTable, activateLogs);
    }
}