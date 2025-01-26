# WatchdogMiddleware Tests

Integration tests for WatchdogMiddleware that verify the middleware's functionality with InfluxDB.

## Prerequisites

Before running tests:
1. InfluxDB must be running on localhost:8086
2. Default bucket and organization must exist
3. Token must have write permissions

## Test Cases

### 1. InfluxDB Connection
Verifies basic connectivity to InfluxDB service.
- Checks if InfluxDB is running and healthy
- Uses default connection settings

### 2. Basic Data Insertion
Tests basic middleware functionality with default settings.
- Simulates HTTP request/response
- Verifies data is actually written to InfluxDB
- Uses default WatchdogOptions

### 3. Sensitive Routes
Validates sensitive route handling.
- Configures a sensitive route
- Verifies sensitive data is not logged
- Checks InfluxDB for absence of sensitive data

### 4. Custom Configuration
Tests middleware with custom settings.
- Uses custom API name, bucket, and table
- Verifies custom configuration is respected
- Confirms data is written with custom parameters

## Error Handling

Tests will fail if:
- InfluxDB is not accessible
- Data writing fails
- Configuration is invalid
- Permissions are insufficient

Each test includes detailed error messages to help identify and fix issues.

## Running Tests

