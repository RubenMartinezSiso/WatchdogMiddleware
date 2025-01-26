using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using WatchdogMiddleware.Models;

public class WatchdogMiddlewareTests
{
    private readonly ILogger<WatchdogMiddleware.WatchdogMiddleware> _logger;

    public WatchdogMiddlewareTests()
    {
        _logger = Mock.Of<ILogger<WatchdogMiddleware.WatchdogMiddleware>>();
    }

    [Fact(DisplayName = "Test 1: InfluxDB Connection")]
    public async Task Should_Connect_To_InfluxDB()
    {
        // Arrange
        var options = new WatchdogOptions();
        string InflusDbUrl = options.InfluxDbUrl;
        using var client = InfluxDBClientFactory.Create(InflusDbUrl, options.InfluxDbToken.ToCharArray());

        // Act
        var health = await client.HealthAsync();

        // Assert
        Assert.True(health.Status == HealthCheck.StatusEnum.Pass,
            "InfluxDB connection failed. Please check:\n" +
            "1. InfluxDB is running on localhost:8086\n" +
            "2. InfluxDB service is healthy");
    }

    [Fact(DisplayName = "Test 2: Basic Data Insertion")]
    public async Task Should_Insert_Basic_Data()
    {
        // Arrange
        var options = new WatchdogOptions();
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test2";
        context.Response.Body = new MemoryStream();

        var middleware = new WatchdogMiddleware.WatchdogMiddleware(
            next: (innerHttpContext) => Task.CompletedTask,
            _logger,
            options
        );

        // Act & Assert
        try
        {
            await middleware.InvokeAsync(context);

            // Verify data was written
            using var client = InfluxDBClientFactory.Create(options.InfluxDbUrl, options.InfluxDbToken.ToCharArray());
            var query = $"from(bucket: \"{options.InfluxDbBucket}\") " +
                       $"|> range(start: -1m) " +
                       $"|> filter(fn: (r) => r[\"req_path\"] == \"/api/test2\")";

            var tables = await client.GetQueryApi().QueryAsync(query, options.InfluxDbOrg);
            Assert.True(tables.Count > 0, "No data was written to InfluxDB");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Test failed: {ex.Message}\n" +
                "Please check:\n" +
                "1. InfluxDB connection settings\n" +
                "2. Write permissions\n" +
                "3. Bucket exists and is accessible");
        }
    }

    [Fact(DisplayName = "Test 3: Sensitive Routes")]
    public async Task Should_Not_Log_Sensitive_Routes()
    {
        // Arrange
        var options = new WatchdogOptions
        {
            SensitiveRoutes = new List<SensitiveRoute>
            {
                new SensitiveRoute { Path = "/api/test3", Method = "POST", DoNotLog = true }
            }
        };

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/test3";
        context.Response.Body = new MemoryStream();

        var middleware = new WatchdogMiddleware.WatchdogMiddleware(
            next: (innerHttpContext) => Task.CompletedTask,
            _logger,
            options
        );

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        using var client = InfluxDBClientFactory.Create(options.InfluxDbUrl, options.InfluxDbToken.ToCharArray());
        var query = $"from(bucket: \"{options.InfluxDbBucket}\") " +
                   $"|> range(start: -1m) " +
                   $"|> filter(fn: (r) => r[\"req_path\"] == \"/api/test3\")";

        var tables = await client.GetQueryApi().QueryAsync(query, options.InfluxDbOrg);
        Assert.True(tables.Count == 0, "Sensitive route was logged when it should not have been");
    }

    [Fact(DisplayName = "Test 4: Custom Configuration")]
    public async Task Should_Use_Custom_Configuration()
    {
        // Arrange
        var options = new WatchdogOptions
        {
            // Add more custom configurations here (test is going to fail if it is not valid)
            ApiName = "CustomTestAPI",
            InfluxDbBucket = "custom_bucket",
            DataTable = "custom_table",
            ActivateLogs = true
        };

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test4";
        context.Response.Body = new MemoryStream();

        var middleware = new WatchdogMiddleware.WatchdogMiddleware(
            next: (innerHttpContext) => Task.CompletedTask,
            _logger,
            options
        );

        // Act & Assert
        try
        {
            await middleware.InvokeAsync(context);

            using var client = InfluxDBClientFactory.Create(options.InfluxDbUrl, options.InfluxDbToken.ToCharArray());
            var query = $"from(bucket: \"{options.InfluxDbBucket}\") " +
                       $"|> range(start: -1m) " +
                       $"|> filter(fn: (r) => r[\"req_path\"] == \"/api/test4\")";

            var tables = await client.GetQueryApi().QueryAsync(query, options.InfluxDbOrg);
            Assert.True(tables.Count > 0, "Custom configuration data was not written to InfluxDB");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Test failed: {ex.Message}\n" +
                "Please check:\n" +
                "1. Custom bucket exists\n" +
                "2. Write permissions for custom bucket\n" +
                "3. Custom configuration is valid");
        }
    }
}
