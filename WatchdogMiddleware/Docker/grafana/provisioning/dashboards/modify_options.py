import json
import os

watchdog_options_path = '/etc/grafana/models/WatchdogOptions.cs'

host = os.getenv('DOCKER_INFLUXDB_INIT_HOST', 'influxdb')
port = os.getenv('DOCKER_INFLUXDB_INIT_PORT', '8086')
token = os.getenv('DOCKER_INFLUXDB_INIT_ADMIN_TOKEN', '1a4aeaa65859e8443d824ee73d82432f')
org = os.getenv('DOCKER_INFLUXDB_INIT_ORG', 'watchdogorg')
bucket = os.getenv('DOCKER_INFLUXDB_INIT_BUCKET', 'watchdogbucket')
datatable = os.getenv('DOCKER_INFLUXDB_INIT_DT', 'watchdogdatatable')
ckptdatatable = os.getenv('DOCKER_INFLUXDB_INIT_CKPTDT', 'checkpointsdatatable')

csharp_code = f'''using System;
using System.Collections.Generic;
using WatchdogMiddleware.Models;

namespace WatchdogMiddleware.Models
{{
    public class WatchdogOptions
    {{
        public string ApiName {{ get; set; }} = "Unknown API";
        public bool ActivateLogs {{ get; set; }} = true;
        public List<SensitiveRoute> SensitiveRoutes {{ get; set; }} = new List<SensitiveRoute>();
        public string InfluxDbToken {{ get; set; }} = "{token}";
        public string InfluxDbOrg {{ get; set; }} = "{org}";
        public string InfluxDbBucket {{ get; set; }} = "{bucket}";
        public string DataTable {{ get; set; }} = "{datatable}";
        public string InfluxDbUrl {{ get; set; }} = "http://localhost:{port}";
        public string CheckpointDataTable {{ get; set; }} = "{ckptdatatable}";
    }}
}}
'''

with open(watchdog_options_path, 'w') as file:
    file.write(csharp_code)
