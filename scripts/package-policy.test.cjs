const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { execFileSync } = require('node:child_process');
const { contractsChanged } = require('./package-policy.cjs');

function repository(t) {
  const cwd = fs.mkdtempSync(path.join(os.tmpdir(), 'store-package-'));
  t.after(() => fs.rmSync(cwd, { recursive: true, force: true }));
  const git = (...args) => execFileSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  const write = (file, value = 'fixture') => {
    fs.mkdirSync(path.dirname(path.join(cwd, file)), { recursive: true });
    fs.writeFileSync(path.join(cwd, file), value);
  };
  const commit = () => { git('add', '.'); git('commit', '--quiet', '-m', 'Fixture'); return git('rev-parse', 'HEAD'); };
  git('init', '--quiet', '-b', 'main');
  git('config', 'user.name', 'Fixture'); git('config', 'user.email', 'fixture@example.invalid');
  git('config', 'commit.gpgsign', 'false'); git('config', 'core.hooksPath', '/dev/null');
  write('README.md'); const initial = commit();
  return { cwd, git, write, commit, initial };
}

test('Contracts release detection covers additions, modifications, removal and renames', t => {
  const { cwd, git, write, commit, initial } = repository(t);
  const commitChanged = () => {
    const base = git('rev-parse', 'HEAD');
    return contractsChanged(commit(), base, cwd);
  };
  write('docs/change.md'); assert.equal(commitChanged(), false);
  const contract = 'src/Niobium.Store.Contracts/Model.cs';
  write(contract); assert.equal(commitChanged(), true);
  write(contract, 'changed'); assert.equal(commitChanged(), true);
  git('mv', contract, 'Model.cs'); assert.equal(commitChanged(), true);
  git('mv', 'Model.cs', contract); assert.equal(commitChanged(), true);
  git('rm', contract); assert.equal(commitChanged(), true);
  write('src/Niobium.Store.Contracts.Tests/Test.cs'); assert.equal(commitChanged(), false);
  for (const value of [undefined, 'main', '--quiet']) {
    assert.throws(() => contractsChanged(value, initial, cwd), /Immutable base and merged/);
    assert.throws(() => contractsChanged(initial, value, cwd), /Immutable base and merged/);
  }
  assert.throws(() => contractsChanged(initial, initial, cwd), /must precede/);
  assert.throws(() => contractsChanged('a'.repeat(40), initial, cwd), /Cannot verify/);
  assert.throws(() => contractsChanged(initial, git('rev-parse', 'HEAD'), cwd), /Cannot verify/);
});

for (const method of ['merge', 'squash', 'rebase']) {
  test(`Contracts in an earlier PR commit are detected after ${method} merge`, t => {
    const { cwd, git, write, commit } = repository(t);
    git('checkout', '-q', '-b', 'feature');
    write('src/Niobium.Store.Contracts/Model.cs'); commit();
    write('docs/change.md'); commit();
    git('checkout', '-q', 'main');
    write('main-only.md'); const base = commit();
    if (method === 'merge') git('merge', '--no-ff', '--no-edit', 'feature');
    if (method === 'squash') { git('merge', '--squash', 'feature'); commit(); }
    if (method === 'rebase') {
      git('checkout', '-q', 'feature'); git('rebase', 'main');
      git('checkout', '-q', 'main'); git('merge', '--ff-only', 'feature');
    }
    const merged = git('rev-parse', 'HEAD');
    assert.equal(contractsChanged(merged, base, cwd), true);
    if (method === 'rebase') assert.equal(contractsChanged(merged, git('rev-parse', 'HEAD^'), cwd), false);
  });
}

test('a Contracts change reverted within a multi-commit PR does not publish', t => {
  const { cwd, git, write, commit, initial } = repository(t);
  const contract = 'src/Niobium.Store.Contracts/Model.cs';
  write(contract); commit();
  git('rm', contract); const merged = commit();
  assert.equal(contractsChanged(merged, initial, cwd), false);
});
