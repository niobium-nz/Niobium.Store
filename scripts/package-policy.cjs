const { spawnSync } = require('node:child_process');

// Compare the closed event's pre-merge base with the actual merged tree. A rebase
// merge's first parent contains earlier PR commits, so it is not a valid baseline.
// Git trees have no REST file-list cap; scoped diff detects renames in both directions.
function contractsChanged(mergeSha, baseSha, cwd = process.cwd()) {
  if (![mergeSha, baseSha].every(sha => /^[0-9a-f]{40}$/.test(sha ?? '')))
    throw new Error('Immutable base and merged commits are required.');
  if (baseSha === mergeSha) throw new Error('The pre-merge base must precede the merged commit.');
  const ancestry = spawnSync('git', ['merge-base', '--is-ancestor', baseSha, mergeSha], { cwd, encoding: 'utf8' });
  if (ancestry.error) throw ancestry.error;
  if (ancestry.status !== 0) throw new Error('Cannot verify the pre-merge base and merged commit history.');
  const result = spawnSync('git', ['diff', '--quiet', '--no-ext-diff', '--no-textconv',
    baseSha, mergeSha, '--', 'src/Niobium.Store.Contracts/'], { cwd, encoding: 'utf8' });
  if (result.error) throw result.error;
  if (result.status === 0) return false;
  if (result.status === 1) return true;
  throw new Error('Cannot compare the merged Contracts tree with its pre-merge base.');
}

module.exports = { contractsChanged };
