import json
import os

dashboard_path = '/etc/grafana/provisioning/dashboards/template.json'

datatable = os.getenv('DOCKER_INFLUXDB_INIT_DT', 'watchdogdatatable')

with open(dashboard_path, 'r') as file:
    dashboard = json.load(file)

for var in dashboard.get('templating', {}).get('list', []):
    if var.get('name') == 'dataTable':
        var['current']['text'] = datatable
        var['current']['value'] = datatable
        var['options'][0]['text'] = datatable
        var['options'][0]['value'] = datatable
        var['query'] = datatable

with open(dashboard_path, 'w') as file:
    json.dump(dashboard, file, indent=2)
