"""Read exception types/methods from Store test telemetry without request data."""
import json
from pathlib import Path
import re
import subprocess
from urllib.parse import urlencode


def azure(*arguments):
    result = subprocess.run(['az', *arguments, '--output', 'json', '--only-show-errors'],
                            capture_output=True, text=True, timeout=90)
    if result.returncode:
        raise RuntimeError('test_telemetry_query_failed')
    return json.loads(result.stdout)


def main():
    report = {'success': False, 'exceptions': []}
    try:
        resources = azure('resource', 'list', '--resource-group', 'rg-niobium-store-test',
                          '--resource-type', 'Microsoft.Insights/components')
        if len(resources) != 1:
            raise RuntimeError('test_telemetry_component_not_unique')
        component = azure('resource', 'show', '--ids', resources[0]['id'])
        app_id = component['properties']['AppId']
        if not re.fullmatch(r'[0-9a-fA-F-]{36}', app_id):
            raise RuntimeError('invalid_test_telemetry_component')
        query = '''exceptions
| where timestamp > ago(30m)
| mv-expand frame=details[0].parsedStack
| extend method=tostring(frame.method)
| where method startswith "Niobium."
| summarize failures=count() by type, outerType, innermostType, method
| take 40'''
        result = azure('rest', '--method', 'get', '--resource', 'https://api.applicationinsights.io',
                       '--url', f'https://api.applicationinsights.io/v1/apps/{app_id}/query?'
                       + urlencode({'query': query}))
        for table in result.get('tables', []):
            columns = [column['name'] for column in table['columns']]
            for row in table['rows']:
                source = dict(zip(columns, row))
                entry = {'failures': int(source.get('failures', 0))}
                for field in ('type', 'outerType', 'innermostType', 'method'):
                    value = str(source.get(field, ''))
                    # Only CLR type/method names, never messages, payloads or identifiers.
                    entry[field] = value if re.fullmatch(r'[A-Za-z0-9_.+<>`\[\],&: -]{0,300}', value) else 'redacted'
                report['exceptions'].append(entry)
        report['success'] = True
    except (RuntimeError, ValueError, KeyError, subprocess.TimeoutExpired):
        report['error'] = 'test_telemetry_unavailable'
    target = Path('artifacts/test-diagnostics.json')
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
