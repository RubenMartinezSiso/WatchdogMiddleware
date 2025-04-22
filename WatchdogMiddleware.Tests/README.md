# 🧪 WatchdogMiddleware Tests

This project contains a collection of integration and validation tests for the [WatchdogMiddleware](../WatchdogMiddleware) project. These tests ensure correct middleware behavior, data logging, and service integrations with tools like InfluxDB, Grafana, Prometheus, and Blackbox Exporter.

---

## ✅ Prerequisites

Before running the tests, ensure the following:

1. Docker containers for **InfluxDB**, **Grafana**, **Prometheus**, and **Blackbox Exporter** are running.
2. The `.env` configuration file in the `/Docker/` directory is correctly populated.
3. Required ports (default: `8086`, `3000`, `9090`, `9115`) are not blocked.
4. InfluxDB token has **write and delete permissions**.

---

## 📋 Test Cases Overview

| #  | Test Description                                 | Purpose                                                                 |
|----|--------------------------------------------------|-------------------------------------------------------------------------|
| 1  | InfluxDB Connection                              | Verifies that InfluxDB is up and healthy.                              |
| 2  | Grafana Connection                               | Checks that Grafana is running and reachable on the expected port.     |
| 3  | Prometheus Connection                            | Verifies availability of Prometheus locally.                           |
| 4  | Blackbox Exporter Connection                     | Confirms Blackbox Exporter is running and accessible.                  |
| 5  | Basic Data Insertion                             | Simulates a request and checks if it’s logged to InfluxDB.             |
| 6  | Error Request Insertion                          | Logs an erroneous request (e.g., 400) and confirms it is saved.        |
| 7  | Sensitive Route Exclusion                        | Ensures sensitive routes marked `DoNotLog=true` are not logged.        |
| 8  | Custom Configuration                             | Logs a request using custom API name, bucket, and datatable.           |
| 9  | Macro Insertion of Multiple Methods & APIs       | Bulk test that sends multiple requests and validates mass insertion.   |
| 10 | Mixed DataTable Insertion                        | Tests correct routing to multiple measurement names.                   |
| 11 | Checkpoint Insertion                             | Invokes the checkpoint feature and verifies a correct log entry.       |
| 12 | Delete All Data from InfluxDB Bucket             | Deletes all data across datatables in the current bucket.              |

---

## 🧪 Test Categories

### 🔗 External Services Connectivity
- **Test 1**: Checks InfluxDB health.
- **Test 2**: Validates Grafana port is open and responds.
- **Test 3**: Validates Prometheus endpoint is reachable.
- **Test 4**: Ensures Blackbox Exporter is active.

### 📦 Data Insertion & Logging
- **Test 5**: Logs a basic GET request.
- **Test 6**: Logs a failing request (status 400).
- **Test 7**: Verifies that configured sensitive routes are excluded.
- **Test 8**: Confirms logging works with custom `WatchdogOptions`.
- **Test 9**: Inserts multiple API-method combinations and checks counts.
- **Test 10**: Confirms entries are routed to two different datatables.
- **Test 11**: Validates the checkpoint system logs a custom message.

### 🧹 Data Cleanup
- **Test 12**: Deletes all data and measurements from the current InfluxDB bucket.

---

## ⚠️ Error Handling

Each test includes detailed assertions and error messages. Tests may fail if:

- Docker containers are not running.
- Ports are blocked or reassigned.
- `.env` values are missing or incorrect.
- InfluxDB token lacks permission for read/write/delete.
- The InfluxDB or Grafana services are uninitialized.

---

## ▶️ Running the Tests

Use the test runner included with the project or execute from CLI:

```bash
dotnet test
