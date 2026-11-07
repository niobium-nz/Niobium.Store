const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const { contractsChanged } = require('./package-policy.cjs');

test('Contracts release detection covers additions, modifications, removal and renames', t => {
  const cwd = fs.mkdtempSync(path.join(os.tmpdir(), 'store-package-'));
  t.after(() => fs.rmSync(cwd, { recursive: true, force: true }));
  const git = (...args) => execFileSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  const write = (file, value = 'fixture') => {
    fs.mkdirSync(path.dirname(path.join(cwd, file)), { recursive: true });
    fs.writeFileSync(path.join(cwd, file), value);
  };
  const commit = () => { git('add', '.'); git('-c', 'core.hooksPath=/dev/null', 'commit', '--quiet', '--no-gpg-sign', '-m', 'Fixture'); return git('rev-parse', 'HEAD'); };
  git('init', '--quiet'); git('config', 'user.name', 'Fixture'); git('config', 'user.email', 'fixture@example.invalid');
  write('README.md'); const initial = commit();
  assert.throws(() => contractsChanged(initial, cwd), /Cannot compare/);
  write('docs/change.md'); assert.equal(contractsChanged(commit(), cwd), false);
  const contract = 'src/Niobium.Store.Contracts/Model.cs';
  write(contract); assert.equal(contractsChanged(commit(), cwd), true);
  write(contract, 'changed'); assert.equal(contractsChanged(commit(), cwd), true);
  git('mv', contract, 'Model.cs'); assert.equal(contractsChanged(commit(), cwd), true);
  git('mv', 'Model.cs', contract); assert.equal(contractsChanged(commit(), cwd), true);
  git('rm', contract); assert.equal(contractsChanged(commit(), cwd), true);
  write('src/Niobium.Store.Contracts.Tests/Test.cs'); assert.equal(contractsChanged(commit(), cwd), false);
  for (const value of [undefined, 'main', '--quiet']) assert.throws(() => contractsChanged(value, cwd), /immutable merged commit/);
  assert.throws(() => contractsChanged('a'.repeat(40), cwd), /Cannot compare/);
});
