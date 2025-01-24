# WatchdogMiddleware Tests

This project contains integration tests for the WatchdogMiddleware. These tests verify the middleware's functionality and help identify common issues.

## Prerequisites

Before running the tests, ensure:

1. InfluxDB is running and accessible
2. The following environment is properly configured:
   - InfluxDB URL (default: http://localhost:8086)
   - InfluxDB Token
   - Organization name
   - Bucket name

## Running the Tests

1. Open the solution in Visual Studio
2. Right-click on the WatchdogMiddleware.Tests project
3. Select "Run Tests"

## Test Cases

### Test 1: Basic InfluxDB Connection
Verifies that InfluxDB is accessible and properly configured.

### Test 2: Write Test Data
Attempts to write test data to InfluxDB to verify write permissions and configuration.

### Test 3: Middleware Integration
Tests the basic middleware functionality with a mock HTTP context.

### Test 4: Sensitive Routes
Verifies that sensitive routes are properly handled and not logged.

## Troubleshooting

If tests fail, check:

1. InfluxDB Connection:
   - Is InfluxDB running?
   - Is the URL correct?
   - Is the token valid?

2. Permissions:
   - Does the token have write permissions?
   - Does the bucket exist?
   - Is the organization name correct?

3. Configuration:
   - Are all environment variables set?
   - Is the middleware properly configured?

## Test Data

All test data is clearly marked with:
- Measurement name: "watchdog_test_table"
- Tag "test_type": "example"
- Descriptive test messages

This ensures test data can be easily identified and separated from production data.
