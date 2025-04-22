using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using WatchdogMiddleware.Models;
using WatchdogMiddleware;

public class WatchdogMiddlewareTests
{
    private readonly ILogger<WatchdogMiddleware.WatchdogMiddleware> _logger;

    public WatchdogMiddlewareTests()
    {
        _logger = Mock.Of<ILogger<WatchdogMiddleware.WatchdogMiddleware>>();
    }

    private static string GetEnvValue(string key)
    {
        // Ajusta esto si usas una ruta relativa en el proyecto
        var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WatchdogMiddleware", "Docker"));
        var envPath = Path.Combine(basePath, ".env");

        if (!System.IO.File.Exists(envPath))
            throw new FileNotFoundException(".env file not found at: " + envPath);

        var lines = System.IO.File.ReadAllLines(envPath);
        var match = lines
            .FirstOrDefault(line => line.StartsWith(key) && line.Contains("="));

        if (match == null)
            throw new Exception($"Key {key} not found in .env");

        var port = match.Split('=')[1].Trim().Split('#')[0].Trim();
        return port;
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
            "1. InfluxDB is running on " + options.InfluxDbUrl + "\n" +
            "2. InfluxDB service is healthy");
    }

    [Fact(DisplayName = "Test 2: Local Grafana Connection")]
    public async Task Should_Connect_To_Grafana()
    {
        var grafanaPort = GetEnvValue("GRAFANA_PORT");
        var url = $"http://localhost:{grafanaPort}/";
        using var client = new HttpClient();

        try
        {
            var response = await client.GetAsync(url);
            Assert.True(response.IsSuccessStatusCode, $"Grafana did not respond correctly on {url}");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Grafana connection failed: {ex.Message}");
        }
    }

    [Fact(DisplayName = "Test 3: Local Prometheus Connection")]
    public async Task Should_Connect_To_Prometheus()
    {
        var prometheusPort = GetEnvValue("PROMETHEUS_PORT");
        var url = $"http://localhost:{prometheusPort}/";
        using var client = new HttpClient();

        try
        {
            var response = await client.GetAsync(url);
            Assert.True(response.IsSuccessStatusCode, $"Prometheus did not respond correctly on {url}");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Prometheus connection failed: {ex.Message}");
        }
    }

    [Fact(DisplayName = "Test 4: Local Blackbox Exporter Connection")]
    public async Task Should_Connect_To_BlackboxExporter()
    {
        var blackboxPort = GetEnvValue("BLACKBOX_PORT");
        var url = $"http://localhost:{blackboxPort}/";
        using var client = new HttpClient();

        try
        {
            var response = await client.GetAsync(url);
            Assert.True(response.IsSuccessStatusCode, $"Blackbox Exporter did not respond correctly on {url}");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Blackbox Exporter connection failed: {ex.Message}");
        }
    }

    [Fact(DisplayName = "Test 5: Basic Data Insertion")]
    public async Task Should_Insert_Basic_Data()
    {
        // Arrange
        var options = new WatchdogOptions();
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test5";
        context.Response.Body = new MemoryStream();
        context.Response.StatusCode = 200;

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
                       $"|> filter(fn: (r) => r[\"req_path\"] == \"/api/test5\")";

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

    [Fact(DisplayName = "Test 6: Error Request Insertion")]
    public async Task Should_Insert_Error_Data()
    {
        // Arrange
        var options = new WatchdogOptions();
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test6";
        context.Response.Body = new MemoryStream();
        context.Response.StatusCode = 400;

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
                       $"|> filter(fn: (r) => r[\"req_path\"] == \"/api/test6\")";

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

    [Fact(DisplayName = "Test 7: Sensitive Routes")]
    public async Task Should_Not_Log_Sensitive_Routes()
    {
        // Arrange
        var options = new WatchdogOptions
        {
            SensitiveRoutes = new List<SensitiveRoute>
            {
                new SensitiveRoute { Path = "/api/test7", Method = "POST", DoNotLog = true }
            }
        };

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/test7";
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
                   $"|> filter(fn: (r) => r[\"req_path\"] == \"/api/test7\")";

        var tables = await client.GetQueryApi().QueryAsync(query, options.InfluxDbOrg);
        Assert.True(tables.Count == 0, "Sensitive route was logged when it should not have been");
    }

    // WARNING: Test is going to fail if custom configuration is not modified or valid
    [Fact(DisplayName = "Test 8: Custom Configuration")]
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
        context.Request.Path = "/api/test8";
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
                       $"|> filter(fn: (r) => r[\"req_path\"] == \"/api/test8\")";

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

    [Fact(DisplayName = "Test 9: Macro Insertion of Requests with Multiple Methods and APIs")]
    public async Task Should_Insert_Multiple_Requests_For_Multiple_Methods_And_APIs()
    {
        // Arrange
        string[] methods = new string[] { "GET", "POST", "PUT", "DELETE" };
        List<string> apiNames = new List<string>
        {
            "API_A", "API_B",
            "API_A", "API_B",
            "API_A", "API_B",
            "API_A", "API_B"
        };

        int requestCounter = 0;
        // Insert 2 requests for each HTTP method using different API names
        foreach (var method in methods)
        {
            for (int i = 0; i < 2; i++)
            {
                var options = new WatchdogOptions
                {
                    ApiName = apiNames[requestCounter],
                    DataTable = GetEnvValue("DOCKER_INFLUXDB_INIT_DT")
                };
                requestCounter++;

                var context = new DefaultHttpContext();
                context.Request.Method = method;
                context.Request.Path = "/api/macro";
                context.Response.Body = new MemoryStream();

                var middleware = new WatchdogMiddleware.WatchdogMiddleware(
                    next: (ctx) => Task.CompletedTask,
                    _logger,
                    options
                );

                await middleware.InvokeAsync(context);
            }
        }

        // Act & Assert: Query InfluxDB for records with req_path "/api/macro"
        using var client = InfluxDBClientFactory.Create("http://localhost:8086", "1a4aeaa65859e8443d824ee73d82432f".ToCharArray());
        var query = $"from(bucket: \"watchdogbucket\") " +
                    $"|> range(start: -1m) " +
                    $"|> filter(fn: (r) => r[\"req_path\"] == \"/api/macro\")";
        var tables = await client.GetQueryApi().QueryAsync(query, "watchdogorg");
        int totalCount = 0;
        foreach (var table in tables)
        {
            totalCount += table.Records.Count;
        }

        Assert.True(totalCount >= 8, $"Expected at least 8 records for macro requests, found {totalCount}");
    }

    [Fact(DisplayName = "Test 10: Mixed DataTable Insertion")]
    public async Task Should_Insert_Requests_To_Multiple_DataTables()
    {
        // Arrange for first DataTable ("first_table")
        var options1 = new WatchdogOptions
        {
            DataTable = "first_table",
            ApiName = "API_First"
        };
        var context1 = new DefaultHttpContext();
        context1.Request.Method = "GET";
        context1.Request.Path = "/api/mixed1";
        context1.Response.Body = new MemoryStream();
        var middleware1 = new WatchdogMiddleware.WatchdogMiddleware(
             next: (ctx) => Task.CompletedTask,
             _logger,
             options1
        );
        await middleware1.InvokeAsync(context1);

        var context2 = new DefaultHttpContext();
        context2.Request.Method = "POST";
        context2.Request.Path = "/api/mixed1";
        context2.Response.Body = new MemoryStream();
        await middleware1.InvokeAsync(context2);

        // Arrange for second DataTable ("second_table")
        var options2 = new WatchdogOptions
        {
            DataTable = "second_table",
            ApiName = "API_Second"
        };
        var context3 = new DefaultHttpContext();
        context3.Request.Method = "GET";
        context3.Request.Path = "/api/mixed2";
        context3.Response.Body = new MemoryStream();
        var middleware2 = new WatchdogMiddleware.WatchdogMiddleware(
             next: (ctx) => Task.CompletedTask,
             _logger,
             options2
        );
        await middleware2.InvokeAsync(context3);

        var context4 = new DefaultHttpContext();
        context4.Request.Method = "POST";
        context4.Request.Path = "/api/mixed2";
        context4.Response.Body = new MemoryStream();
        await middleware2.InvokeAsync(context4);

        // Act & Assert: Query InfluxDB for records in each DataTable
        using var client = InfluxDBClientFactory.Create("http://localhost:8086", "1a4aeaa65859e8443d824ee73d82432f".ToCharArray());

        var query1 = $"from(bucket: \"watchdogbucket\") " +
                     $"|> range(start: -1m) " +
                     $"|> filter(fn: (r) => r[\"_measurement\"] == \"first_table\")";
        var tables1 = await client.GetQueryApi().QueryAsync(query1, "watchdogorg");
        int count1 = 0;
        foreach (var table in tables1)
        {
            count1 += table.Records.Count;
        }

        var query2 = $"from(bucket: \"watchdogbucket\") " +
                     $"|> range(start: -1m) " +
                     $"|> filter(fn: (r) => r[\"_measurement\"] == \"second_table\")";
        var tables2 = await client.GetQueryApi().QueryAsync(query2, "watchdogorg");
        int count2 = 0;
        foreach (var table in tables2)
        {
            count2 += table.Records.Count;
        }

        Assert.True(count1 >= 2, $"Expected at least 2 records in first_table, found {count1}");
        Assert.True(count2 >= 2, $"Expected at least 2 records in second_table, found {count2}");
    }

    [Fact(DisplayName = "Test 11: Checkpoint Insertion")]
    public async Task Should_Insert_Checkpoint()
    {
        // Arrange
        var options = new WatchdogOptions
        {
            ApiName = "CheckpointTestAPI",
            CheckpointDataTable = "checkpoint_test_table"
        };

        // Save the options globally so the checkpoint extension can access them
        WatchdogOptionsHolder.Options = options;

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/checkpoint";
        context.Response.Body = new MemoryStream();

        // Act: Call the checkpoint extension
        context.LogCheckpoint("Test checkpoint", new Dictionary<string, object> { { "detail", "value" } });

        // Act & Assert: Query InfluxDB for the checkpoint record
        using var client = InfluxDBClientFactory.Create(options.InfluxDbUrl, options.InfluxDbToken.ToCharArray());
        var query = $"from(bucket: \"{options.InfluxDbBucket}\") " +
                    $"|> range(start: -1m) " +
                    $"|> filter(fn: (r) => r[\"_measurement\"] == \"checkpoint_test_table\") ";
        var tables = await client.GetQueryApi().QueryAsync(query, options.InfluxDbOrg);
        int totalCount = 0;
        foreach (var table in tables)
        {
            totalCount += table.Records.Count;
        }

        Assert.True(totalCount > 0, $"Expected at least one checkpoint record, found {totalCount}");
    }

    [Fact(DisplayName = "Test 12: Delete All Data From Bucket")]
    public async Task Should_Delete_All_Data_From_Bucket()
    {
        // Arrange
        var options = new WatchdogOptions();
        var influxDbUrl = options.InfluxDbUrl;
        var token = options.InfluxDbToken;
        var bucket = options.InfluxDbBucket;
        var org = options.InfluxDbOrg;

        var start = DateTime.Parse("1677-12-12T00:12:43.145224Z").ToUniversalTime();
        var stop = DateTime.UtcNow.AddMinutes(1);

        using var client = InfluxDBClientFactory.Create(influxDbUrl, token.ToCharArray());
        var deleteApi = client.GetDeleteApi();

        try
        {
            // Act
            await deleteApi.Delete(start, stop, "", bucket, org); // "" => delete filter matches all

            // Assert
            var query = $"from(bucket: \"{bucket}\") |> range(start: -1d)";
            var tables = await client.GetQueryApi().QueryAsync(query, org);

            int total = tables.Sum(t => t.Records.Count);
            Assert.True(total == 0, $"Expected all data to be deleted, but {total} records remain.");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Data deletion failed: {ex.Message}");
        }
    }
}
