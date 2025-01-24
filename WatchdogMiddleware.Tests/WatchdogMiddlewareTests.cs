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
    [Fact(DisplayName = "Test 1: Basic InfluxDB Connection")]
    public async Task TestInfluxDBConnection()
    {
        // Arrange
        var options = new WatchdogOptions
        {
            ApiName = "TestAPI",
            InfluxDbUrl = "http://localhost:8086",
            InfluxDbToken = "your_token",
            InfluxDbOrg = "watchdogorg",
            InfluxDbBucket = "watchdogbucket",
            DataTable = "watchdog_test_table"
        };

        using var client = InfluxDBClientFactory.Create(options.InfluxDbUrl, options.InfluxDbToken.ToCharArray());

        // Act & Assert
        var health = await client.HealthAsync();
        Assert.True(health.Status == InfluxDB.Client.Api.Domain.HealthCheck.StatusEnum.Pass,
            "InfluxDB is not running or not accessible. Please ensure InfluxDB is running and accessible.");
    }

    [Fact(DisplayName = "Test 2: Write Test Data to InfluxDB")]
    public async Task TestWriteToInfluxDB()
    {
        // Arrange
        var options = new WatchdogOptions
        {
            ApiName = "TestAPI",
            InfluxDbUrl = "http://localhost:8086",
            InfluxDbToken = "your_token",
            InfluxDbOrg = "watchdogorg",
            InfluxDbBucket = "watchdogbucket",
            DataTable = "watchdog_test_table"
        };

        using var client = InfluxDBClientFactory.Create(options.InfluxDbUrl, options.InfluxDbToken.ToCharArray());
        var writeApi = client.GetWriteApi();

        // Act
        var point = PointData.Measurement("watchdog_test_table")
            .Tag("test_type", "example")
            .Tag("req_api", "TestAPI")
            .Field("req_path", "/api/test")
            .Field("res_status_code", 200)
            .Field("test_message", "This is a test entry")
            .Timestamp(DateTime.UtcNow, WritePrecision.Ms);

        // Assert
        try
        {
            writeApi.WritePoint(point, options.InfluxDbBucket, options.InfluxDbOrg);
            Assert.True(true, "Test data written successfully");
        }
        catch (Exception ex)
        {
            Assert.True(false, $"Failed to write test data: {ex.Message}\n" +
                "Please check:\n" +
                "1. InfluxDB is running\n" +
                "2. Token is valid\n" +
                "3. Organization and bucket exist\n" +
                "4. Bucket has write permissions");
        }
    }

    [Fact(DisplayName = "Test 3: Middleware Integration")]
    public async Task TestMiddlewareIntegration()
    {
        // Arrange
        var options = new WatchdogOptions
        {
            ApiName = "TestAPI",
            ActivateLogs = true
        };

        var logger = new Mock<ILogger<WatchdogMiddleware.WatchdogMiddleware>>();
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";

        var middleware = new WatchdogMiddleware.WatchdogMiddleware(
            next: (innerHttpContext) => Task.CompletedTask,
            logger: logger.Object,
            options: options
        );

        // Act & Assert
        try
        {
            await middleware.InvokeAsync(context);
            Assert.True(true, "Middleware processed the request successfully");
        }
        catch (Exception ex)
        {
            Assert.True(false, $"Middleware failed to process request: {ex.Message}\n" +
                "Please check:\n" +
                "1. Middleware configuration is correct\n" +
                "2. All required services are available\n" +
                "3. Permissions are properly set");
        }
    }

    [Fact(DisplayName = "Test 4: Sensitive Routes")]
    public async Task TestSensitiveRoutes()
    {
        // Arrange
        var options = new WatchdogOptions
        {
            ApiName = "TestAPI",
            SensitiveRoutes = new List<SensitiveRoute>
            {
                new SensitiveRoute { Path = "/api/sensitive", Method = "POST", DoNotLog = true }
            }
        };

        var logger = new Mock<ILogger<WatchdogMiddleware.WatchdogMiddleware>>();
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/sensitive";

        var middleware = new WatchdogMiddleware.WatchdogMiddleware(
            next: (innerHttpContext) => Task.CompletedTask,
            logger: logger.Object,
            options: options
        );

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        logger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)
            ),
            Times.Never,
            "Sensitive route should not be logged"
        );
    }
}
