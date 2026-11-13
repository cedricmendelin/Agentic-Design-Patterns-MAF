import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { resolveTestRef, validateEnvironment } from './foundry-branch-checks.mjs';

const sha = 'a'.repeat(40);

test('resolves branch names and exact commit SHAs using distinct same-repository API paths', async () => {
  const paths = [];
  const api = async path => {
    paths.push(path);
    return path.startsWith('branches/') ? { commit: { sha } } : { sha };
  };
  assert.equal(await resolveTestRef('feature/demo', api), sha);
  assert.equal(await resolveTestRef(sha, api), sha);
  assert.deepEqual(paths, ['branches/feature%2Fdemo', `commits/${sha}`]);
});

test('rejects invalid input and mismatched or malformed commit responses', async () => {
  for (const ref of ['', ' branch', 'branch\nother']) {
    await assert.rejects(resolveTestRef(ref, async () => ({})));
  }
  await assert.rejects(resolveTestRef(sha, async () => ({ sha: 'b'.repeat(40) })));
  await assert.rejects(resolveTestRef('feature', async () => ({ commit: { sha: 'abc' } })));
});

test('requires a reviewer and only the exact default branch policy', () => {
  const environment = {
    protection_rules: [{ type: 'required_reviewers', reviewers: [{ type: 'User' }] }],
    deployment_branch_policy: { custom_branch_policies: true, protected_branches: false },
  };
  const policy = { total_count: 1, branch_policies: [{ name: 'main', type: 'branch' }] };
  assert.doesNotThrow(() => validateEnvironment(environment, policy, 'main'));
  assert.throws(() => validateEnvironment({ ...environment, protection_rules: [] }, policy, 'main'));
  assert.throws(() => validateEnvironment({ ...environment, deployment_branch_policy: null }, policy, 'main'));
  assert.throws(() => validateEnvironment(environment, {
    total_count: 2, branch_policies: [...policy.branch_policies, { name: '*', type: 'branch' }],
  }, 'main'));
  assert.throws(() => validateEnvironment(environment, {
    total_count: 1, branch_policies: [{ name: 'main', type: 'tag' }],
  }, 'main'));
});
