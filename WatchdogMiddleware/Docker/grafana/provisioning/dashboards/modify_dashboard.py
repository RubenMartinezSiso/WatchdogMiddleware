import json
import os

dashboard_path = '/etc/grafana/provisioning/dashboards/template.json'
watchdog_options_path = '/etc/grafana/models/WatchdogOptions.cs'

host = os.getenv('DOCKER_INFLUXDB_INIT_HOST', 'localhost')
port = os.getenv('DOCKER_INFLUXDB_INIT_PORT', '8086')
token = os.getenv('DOCKER_INFLUXDB_INIT_ADMIN_TOKEN', 'default-token')
org = os.getenv('DOCKER_INFLUXDB_INIT_ORG', 'default-org')
bucket = os.getenv('DOCKER_INFLUXDB_INIT_BUCKET', 'default-bucket')
datatable = os.getenv('DOCKER_INFLUXDB_INIT_DT', 'watchdogdatatable')

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

        public string InfluxDbHost {{ get; set; }} = "{host}";
        public string InfluxDbPort {{ get; set; }} = "{port}";
        public string InfluxDbToken {{ get; set; }} = "{token}";
        public string InfluxDbOrg {{ get; set; }} = "{org}";
        public string InfluxDbBucket {{ get; set; }} = "{bucket}";
        public string DataTable {{ get; set; }} = "{datatable}";
        public string InfluxDbUrl {{ get; set; }} = "http://localhost:{port}";
    }}
}}
'''

with open(watchdog_options_path, 'w') as file:
    file.write(csharp_code)

with open(dashboard_path, 'r') as file:
    dashboard = json.load(file)

for var in dashboard.get('templating', {}).get('list', []):
    if var.get('name') == 'dataTable':
        var['current']['text'] = datatable
        var['current']['value'] = datatable
        var['query'] = datatable

with open(dashboard_path, 'w') as file:
    json.dump(dashboard, file, indent=2)
