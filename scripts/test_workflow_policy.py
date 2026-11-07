"""Exercise the actual workflow gates against events that must never publish production."""
import ast
import copy
import json
from pathlib import Path
import re
import subprocess
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
            self.assertEqual(config['on']['pull_request_target'], {'branches': ['main'], 'types': ['closed']})
            self.assertNotIn('pull_request', config['on'])
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
                ('pull_request', 'closed', True, 'main', False),
                ('pull_request_target', 'opened', False, 'main', False),
                ('pull_request_target', 'synchronize', False, 'main', False),
                ('pull_request_target', 'closed', False, 'main', False),
                ('pull_request_target', 'closed', True, 'release', False),
                ('pull_request_target', 'closed', True, 'main', True),
            ]:
                with self.subTest(workflow=name, event=event, action=action, merged=merged, base=base):
                    self.assertEqual(selected(condition, event, action, merged, base, 'refs/heads/main'), expected)
            checkout = next(step for step in config['jobs'][job]['steps'] if step.get('name') == 'Checkout')
            self.assertEqual(checkout['with']['ref'], '${{ github.event.pull_request.merge_commit_sha }}')

    def test_feature_branches_only_build_and_deploy_test(self):
        config = workflow('azure-deploy.yml')
        self.assertEqual(config['on']['push']['branches'], ['**', '!main'])
        for job in ['build', 'test']:
            condition = config['jobs'][job]['if']
            self.assertTrue(selected(condition, 'push'))
            self.assertFalse(selected(condition, 'push', ref='refs/heads/main'))
            self.assertFalse(selected(condition, 'pull_request_target', 'closed', True))
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
        self.assertEqual(set(config['on']), {'pull_request_target'})
        self.assertEqual(set(config['jobs']), {'prod-release'})
        steps = config['jobs']['prod-release']['steps']
        self.assertEqual(steps[0]['with']['fetch-depth'], '0')
        self.assertIn('context.payload.pull_request.base.sha', steps[1]['with']['script'])
        for step in steps:
            if step.get('name') in ['Setup .NET', 'Restore Packages', 'Build Projects', 'NuGet login (OIDC)', 'NuGet Publish']:
                self.assertEqual(step['if'], "steps.changes.outputs.contracts == 'true'")

    def test_production_gate_rejects_a_stale_merge_after_a_newer_run(self):
        prod = workflow('azure-deploy.yml')['jobs']['prod']
        gate = prod['steps'][0]
        self.assertEqual(gate['id'], 'merge')
        for step in prod['steps'][1:]:
            if not step.get('uses', '').startswith('actions/upload-artifact@'):
                self.assertEqual(step['if'], "steps.merge.outputs.current == 'true'")

        old, new = 'a' * 40, 'b' * 40
        context = {'eventName': 'pull_request_target', 'repo': {'owner': 'niobium-nz', 'repo': 'Niobium.Store'},
                   'payload': {'action': 'closed', 'pull_request': {'merged': True, 'merge_commit_sha': new,
                       'base': {'ref': 'main', 'repo': {'full_name': 'niobium-nz/Niobium.Store'}}}}}
        script = gate['with']['script']
        # A newer event acquires the queue first. The older event must then skip,
        # including when the PR originated from a fork or Dependabot.
        for actor in ['maintainer', 'fork-contributor', 'dependabot[bot]']:
            current_context = copy.deepcopy(context)
            current_context['actor'] = actor
            current_context['payload']['pull_request']['head'] = {'repo': {'full_name': f'{actor}/fork'}}
            allowed = run_merge_gate(script, current_context, new)
            self.assertEqual(allowed['outputs'], {'current': 'true'})
            self.assertEqual(allowed['calls'], [{'owner': 'niobium-nz', 'repo': 'Niobium.Store', 'branch': 'main'}])
            current_context['payload']['pull_request']['merge_commit_sha'] = old
            stale = run_merge_gate(script, current_context, new)
            self.assertEqual(stale['outputs'], {'current': 'false'})
            self.assertIn('superseded', stale['summary'])

        for invalid in ['event', 'action', 'merged', 'branch', 'repository', 'sha']:
            rejected = copy.deepcopy(context)
            pr = rejected['payload']['pull_request']
            if invalid == 'event': rejected['eventName'] = 'pull_request'
            if invalid == 'action': rejected['payload']['action'] = 'opened'
            if invalid == 'merged': pr['merged'] = False
            if invalid == 'branch': pr['base']['ref'] = 'feature'
            if invalid == 'repository': pr['base']['repo']['full_name'] = 'other/repo'
            if invalid == 'sha': pr['merge_commit_sha'] = 'main'
            result = run_merge_gate(script, rejected, new)
            self.assertTrue(result['failure'])
            self.assertEqual(result['outputs'], {})
            self.assertEqual(result['calls'], [])
        failed_lookup = run_merge_gate(script, context, None)
        self.assertTrue(failed_lookup['failure'])
        self.assertEqual(failed_lookup['outputs'], {})


def run_merge_gate(script, context, tip):
    runner = r'''
const fs = require('node:fs');
const {script, context, tip} = JSON.parse(fs.readFileSync(0, 'utf8'));
const result = {outputs: {}, calls: [], failure: '', summary: ''};
const core = {
  setOutput: (name, value) => result.outputs[name] = value,
  setFailed: value => result.failure = value,
  notice: () => {},
  summary: {addRaw(value) { result.summary = value; return this; }, async write() {}},
};
const github = {rest: {repos: {getBranch: async args => {
  result.calls.push(args);
  if (tip === null) throw new Error('branch_lookup_failed');
  return {data: {commit: {sha: tip}}};
}}}};
(async () => {
  const AsyncFunction = Object.getPrototypeOf(async function() {}).constructor;
  try { await new AsyncFunction('context', 'github', 'core', script)(context, github, core); }
  catch (error) { result.failure = error.message; }
  process.stdout.write(JSON.stringify(result));
})();
'''
    result = subprocess.run(['node', '-e', runner],
                            input=json.dumps({'script': script, 'context': context, 'tip': tip}),
                            capture_output=True, text=True, check=True)
    return json.loads(result.stdout)


if __name__ == '__main__':
    unittest.main()
