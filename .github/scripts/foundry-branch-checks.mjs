import { execFileSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

const shaPattern = /^[0-9a-f]{40}$/;

export async function resolveTestRef(ref, api) {
  if (!ref || ref !== ref.trim() || /[\x00-\x20\x7f]/.test(ref) || ref.length > 255) {
    throw new Error('test_ref must be a branch name or a full commit SHA');
  }
  const isSha = shaPattern.test(ref);
  const result = await api(isSha ? `commits/${ref}` : `branches/${encodeURIComponent(ref)}`);
  const sha = isSha ? result.sha : result.commit?.sha;
  if (!shaPattern.test(sha ?? '') || (isSha && sha !== ref)) {
    throw new Error('GitHub did not resolve test_ref to the requested commit');
  }
  return sha;
}

export function validateEnvironment(environment, policies, defaultBranch) {
  const hasReviewers = environment.protection_rules?.some(
    rule => rule.type === 'required_reviewers' && rule.reviewers?.length > 0,
  );
  const branchPolicy = environment.deployment_branch_policy;
  const entries = policies.branch_policies;
  if (!hasReviewers || !branchPolicy?.custom_branch_policies || branchPolicy.protected_branches ||
      policies.total_count !== 1 || entries?.length !== 1 ||
      entries[0].name !== defaultBranch || entries[0].type !== 'branch') {
    throw new Error('foundry-branch-integration must require reviewers and allow only the default branch');
  }
}

async function main() {
  const repository = process.env.GITHUB_REPOSITORY;
  if (!/^[\w.-]+\/[\w.-]+$/.test(repository ?? '')) throw new Error('Invalid repository');
  const api = async path => JSON.parse(execFileSync('gh', [
    'api', `repos/${repository}/${path}`,
  ], { encoding: 'utf8' }));

  if (process.argv[2] === 'resolve') {
    console.log(await resolveTestRef(process.env.TEST_REF, api));
  } else if (process.argv[2] === 'environment') {
    const environment = await api('environments/foundry-branch-integration');
    const policies = await api('environments/foundry-branch-integration/deployment-branch-policies?per_page=100');
    validateEnvironment(environment, policies, process.env.DEFAULT_BRANCH);
  } else {
    throw new Error('Expected resolve or environment');
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch(error => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
