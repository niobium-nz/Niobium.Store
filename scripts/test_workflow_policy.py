"""Exercise the actual workflow gates against events that must never publish production."""
import ast
from pathlib import Path
import re
import unittest

import yaml

ROOT = Path(__file__).resolve().parents[1]


def workflow(name):
    return yaml.load((ROOT / '.github/workflows' / name).read_text(), Loader=yaml.BaseLoader)


def selected(expression, event, action='', merged=False, base='main', ref='refs/heads/feature/nested'):
    values = {'github.event_name': event, 'github.event.action': action,
              'github.event.pull_request.merged': merged, 'github.event.pull_request.base.ref': base,
              'github.ref': ref}
    expression = re.sub(r'github\.[a-z_.]+', lambda match: repr(values[match.group()]), expression)
    expression = expression.replace('&&', ' and ').replace('||', ' or ')
    expression = re.sub(r'\btrue\b', 'True', expression)
    expression = re.sub(r'\bfalse\b', 'False', expression)
    tree = ast.parse(expression, mode='eval')
    allowed = (ast.Expression, ast.BoolOp, ast.And, ast.Or, ast.Compare, ast.Eq, ast.NotEq, ast.Constant)
    if any(not isinstance(node, allowed) for node in ast.walk(tree)):
        raise AssertionError('Unsupported workflow condition; extend the test explicitly.')
    return eval(compile(tree, '<workflow-condition>', 'eval'), {'__builtins__': {}}, {})


class WorkflowPolicyTests(unittest.TestCase):
    def test_production_and_packages_require_a_closed_merged_main_pr(self):
        for name, job in [('azure-deploy.yml', 'prod'), ('package-publish.yml', 'prod-release')]:
            config = workflow(name)
            self.assertEqual(config['on']['pull_request'], {'branches': ['main'], 'types': ['closed']})
            self.assertNotIn('workflow_dispatch', config['on'])
            condition = config['jobs'][job]['if']
            for event, action, merged, base, expected in [
                ('push', '', False, 'main', False),
                ('workflow_dispatch', '', False, 'main', False),
                ('pull_request', 'opened', False, 'main', False),
                ('pull_request', 'synchronize', False, 'main', False),
                ('pull_request', 'reopened', False, 'main', False),
                ('pull_request', 'closed', False, 'main', False),
                ('pull_request', 'closed', True, 'release', False),
                ('pull_request', 'closed', True, 'main', True),
            ]:
                with self.subTest(workflow=name, event=event, action=action, merged=merged, base=base):
                    self.assertEqual(selected(condition, event, action, merged, base, 'refs/heads/main'), expected)
            checkout = config['jobs'][job]['steps'][0]
            self.assertEqual(checkout['with']['ref'], '${{ github.event.pull_request.merge_commit_sha }}')

    def test_feature_branches_only_build_and_deploy_test(self):
        config = workflow('azure-deploy.yml')
        self.assertEqual(config['on']['push']['branches'], ['**', '!main'])
        for job in ['build', 'test']:
            condition = config['jobs'][job]['if']
            self.assertTrue(selected(condition, 'push'))
            self.assertFalse(selected(condition, 'push', ref='refs/heads/main'))
            self.assertFalse(selected(condition, 'pull_request', 'closed', True))
        self.assertEqual(config['jobs']['test']['needs'], 'build')
        self.assertEqual(config['permissions'], {'contents': 'read'})
        self.assertEqual(config['concurrency']['cancel-in-progress'], 'false')
        self.assertEqual(config['concurrency']['queue'], 'max')

    def test_production_has_only_health_verification_not_order_fixtures(self):
        config = workflow('azure-deploy.yml')
        prod = config['jobs']['prod']
        verify = next(step for step in prod['steps'] if step.get('name') == 'Run production read-only verification')
        self.assertIn('scripts/read-only-deployment.py', verify['run'])
        self.assertEqual(verify['env']['READONLY_EXPECTED_COMMIT'], '${{ github.event.pull_request.merge_commit_sha }}')
        rendered = str(prod)
        for forbidden in ['npm test', 'TEST_NOTIFICATION_RECIPIENT', 'fixture-config.js', 'IntegrationTests']:
            self.assertNotIn(forbidden, rendered)

    def test_packages_are_merge_only_and_unchanged_contracts_skip_publish(self):
        config = workflow('package-publish.yml')
        self.assertEqual(set(config['on']), {'pull_request'})
        self.assertEqual(set(config['jobs']), {'prod-release'})
        steps = config['jobs']['prod-release']['steps']
        self.assertEqual(steps[0]['with']['fetch-depth'], '0')
        self.assertIn('context.payload.pull_request.base.sha', steps[1]['with']['script'])
        for step in steps:
            if step.get('name') in ['Setup .NET', 'Restore Packages', 'Build Projects', 'NuGet login (OIDC)', 'NuGet Publish']:
                self.assertEqual(step['if'], "steps.changes.outputs.contracts == 'true'")


if __name__ == '__main__':
    unittest.main()
