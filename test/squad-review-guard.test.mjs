import assert from 'node:assert/strict';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { test } from 'node:test';
import {
  enforceReviewOutputs,
  reviewTarget,
  validateVerdict,
} from '../.github/workflows/shared/squad-review-guard.mjs';

const REPOSITORY = 'cedricmendelin/Agentic-Design-Patterns-MAF';
const BASE_SHA = 'a'.repeat(40);
const HEAD_SHA = 'b'.repeat(40);
const REGISTRY = {
  schema: 'squad-agent-provenance/v1',
  schema_version: 1,
  agents: {
    coder: { status: 'active', persistent_name: 'coder' },
    verifier: { status: 'active', persistent_name: 'verifier' },
  },
};

function contentFile(value) {
  const content = Buffer.from(JSON.stringify(value));
  return {
    type: 'file',
    encoding: 'base64',
    content: content.toString('base64'),
    size: content.length,
  };
}

function setup({
  pr: overrides = {},
  attribution = 'missing',
  registry = REGISTRY,
} = {}) {
  const pullRequest = {
    number: 48,
    title: 'Update demos',
    body: '',
    state: 'open',
    merged: false,
    head: {
      repo: { full_name: REPOSITORY },
      ref: 'feature/update-demos',
      sha: HEAD_SHA,
    },
    base: {
      repo: { full_name: REPOSITORY },
      ref: 'main',
      sha: BASE_SHA,
    },
    user: { type: 'User', login: 'contributor' },
    ...overrides,
  };
  const calls = [];
  const env = {
    GITHUB_REPOSITORY: REPOSITORY,
    GITHUB_EVENT_NAME: 'pull_request_target',
    SQUAD_REVIEW_PR: '48',
    SQUAD_REVIEW_HEAD: HEAD_SHA,
    SQUAD_REVIEW_WORKFLOW_SHA: BASE_SHA,
  };
  const get = async (route) => {
    calls.push(route);
    if (route === `repos/${REPOSITORY}/pulls/48`) return pullRequest;
    if (route === `repos/${REPOSITORY}`) {
      return { full_name: REPOSITORY, default_branch: 'main' };
    }
    if (route === `repos/${REPOSITORY}/contents/.squad-review.json`) {
      if (attribution === 'missing') {
        const error = new Error('not found');
        error.status = 404;
        throw error;
      }
      if (attribution instanceof Error) throw attribution;
      return contentFile(attribution);
    }
    if (route === `repos/${REPOSITORY}/contents/.squad/casting/registry.json`) {
      return contentFile(registry);
    }
    if (route === `repos/${REPOSITORY}/pulls/48/reviews`) return [];
    throw new Error(`Unexpected API request: ${route}`);
  };
  return { calls, env, get };
}

test('ordinary human PR without Squad attribution uses verifier without inventing an author', async () => {
  const { env, get } = setup();
  const target = await reviewTarget(env, get);

  assert.equal(target.author_agent, null);
  assert.equal(target.reviewer_agent, 'verifier');
  assert.equal(target.head_sha, HEAD_SHA);
});

test('valid Squad attribution remains bound to active base-committed agent identities', async () => {
  const { env, get } = setup({
    attribution: {
      schema: 'squad-review-author/v1',
      repository: REPOSITORY,
      issue: 48,
      author_agent: 'coder',
      reviewer_agent: 'verifier',
    },
  });

  const target = await reviewTarget(env, get);

  assert.equal(target.author_agent, 'coder');
  assert.equal(target.reviewer_agent, 'verifier');
});

test('safe-output stamping keeps the non-Squad author null and still requires one review', async (t) => {
  const { env, get } = setup();
  const directory = mkdtempSync(join(tmpdir(), 'squad-review-guard-'));
  t.after(() => rmSync(directory, { recursive: true, force: true }));
  const outputPath = join(directory, 'agent-output.json');
  env.GH_AW_AGENT_OUTPUT = outputPath;
  env.GITHUB_RUN_ID = '12345';
  env.GITHUB_RUN_ATTEMPT = '1';
  writeFileSync(outputPath, JSON.stringify({
    items: [{
      type: 'submit_pull_request_review',
      event: 'REQUEST_CHANGES',
      body: 'Review complete.\n\nHuman approval remains mandatory.\n\n'
        + `Squad-Review-Head: ${HEAD_SHA}`,
    }],
  }));

  await enforceReviewOutputs(env, get);

  const output = JSON.parse(readFileSync(outputPath, 'utf8'));
  assert.equal(output.items.length, 1);
  assert.equal(output.items[0].event, 'COMMENT');
  const marker = output.items[0].body.split('\n')
    .find((line) => line.startsWith('Squad-Review-Verdict: '));
  assert.ok(marker, 'safe output must contain a stamped review verdict');
  const verdict = JSON.parse(marker.slice('Squad-Review-Verdict: '.length));
  assert.equal(verdict.author_agent, null);
  assert.equal(verdict.reviewer_agent, 'verifier');
  assert.equal(verdict.head_sha, HEAD_SHA);
});

test('Copilot PR without Squad attribution uses the same explicit external-review policy', async () => {
  const { env, get } = setup({
    pr: {
      head: {
        repo: { full_name: REPOSITORY },
        ref: 'copilot/update-demos',
        sha: HEAD_SHA,
      },
      user: { type: 'Bot', login: 'copilot-swe-agent[bot]' },
    },
  });

  const target = await reviewTarget(env, get);

  assert.equal(target.author_agent, null);
  assert.equal(target.reviewer_agent, 'verifier');
});

test('Squad namespace PR without committed attribution is refused, not treated as external', async () => {
  const { env, get } = setup({
    pr: {
      head: {
        repo: { full_name: REPOSITORY },
        ref: 'squad/implement-48-update-demos',
        sha: HEAD_SHA,
      },
    },
  });

  await assert.rejects(
    reviewTarget(env, get),
    /missing committed attribution for Squad-authored pull request/,
  );
});

test('bootstrap PR without committed attribution remains on the bootstrap provenance path', async () => {
  const { env, get } = setup({
    pr: {
      head: {
        repo: { full_name: REPOSITORY },
        ref: 'squad/bootstrap-cast',
        sha: HEAD_SHA,
      },
      user: { type: 'Bot', login: 'github-actions[bot]' },
    },
  });

  await assert.rejects(reviewTarget(env, get), /missing or duplicate base-controlled bootstrap PR provenance/);
});

test('missing verifier or non-404 attribution errors do not fall back to external review', async () => {
  const { env, get } = setup({
    registry: {
      ...REGISTRY,
      agents: {
        coder: REGISTRY.agents.coder,
        verifier: { status: 'inactive', persistent_name: 'verifier' },
      },
    },
  });
  await assert.rejects(reviewTarget(env, get), /missing active verifier agent/);

  const forbidden = new Error('forbidden');
  forbidden.status = 403;
  const failing = setup({ attribution: forbidden });
  await assert.rejects(
    reviewTarget(failing.env, failing.get),
    /forbidden/,
  );
});

test('malformed committed attribution is not treated as missing attribution', async () => {
  const { env, get } = setup({
    attribution: { schema: 'squad-review-author/v1' },
  });

  await assert.rejects(
    reviewTarget(env, get),
    /missing or malformed committed \.squad-review\.json/,
  );
});

test('malformed Squad marker is refused before external attribution is considered', async () => {
  const { env, get } = setup({ pr: { body: 'squad:implement with invalid fields' } });

  await assert.rejects(
    reviewTarget(env, get),
    /malformed or duplicate implementation provenance/,
  );
});

test('verdict validation accepts only the expected null external author identity', () => {
  const timestamp = new Date().toISOString().replace(/\.\d{3}Z$/, 'Z');
  const expected = {
    repository: REPOSITORY,
    pull_request: 48,
    base_sha: BASE_SHA,
    head_sha: HEAD_SHA,
    author_agent: null,
    reviewer_agent: 'verifier',
    workflow_sha: BASE_SHA,
  };
  const review = {
    id: 55,
    user: { login: 'github-actions[bot]', id: 41898282, type: 'Bot' },
    commit_id: HEAD_SHA,
    state: 'COMMENTED',
    submitted_at: timestamp,
  };
  const verdict = {
    schema: 'squad-review-verdict/v1',
    ...expected,
    result: 'COMMENT',
    timestamp,
    run_id: 12345,
    run_attempt: 1,
    event: 'pull_request_target',
    workflow_path: '.github/workflows/squad-review.lock.yml',
  };

  assert.equal(validateVerdict(verdict, expected, review).author_agent, null);
  assert.throws(
    () => validateVerdict({ ...verdict, author_agent: 'coder' }, expected, review),
    /verdict author_agent mismatch/,
  );
});
