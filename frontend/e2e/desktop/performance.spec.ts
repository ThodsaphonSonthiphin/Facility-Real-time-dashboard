import { execFile } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';
import { expect, test } from '../support/test.ts';

const execFileAsync = promisify(execFile);
const loadScriptPath = fileURLToPath(new URL('../../../scripts/load-test.mjs', import.meta.url));

test.skip(process.env.E2E_DATASET !== 'large', 'Performance/load gate requires the isolated large dataset (70+ Areas)');

test('API maintains 0% errors and meets p95 latency budgets under concurrent load on large dataset', async () => {
  let stdout = '';
  let stderr = '';
  try {
    const out = await execFileAsync(process.execPath, [loadScriptPath], {
      env: { ...process.env, LOAD_BASE_URL: process.env.E2E_BASE_URL },
    });
    stdout = out.stdout;
    stderr = out.stderr;
  } catch (err) {
    const failure = err as { message?: string; stdout?: string; stderr?: string };
    throw new Error(
      `Load test failed: ${failure.message ?? 'unknown'}\nSTDOUT:\n${failure.stdout ?? ''}\nSTDERR:\n${failure.stderr ?? ''}`,
    );
  }
  console.log(stdout);
  if (stderr) console.error(stderr);
  expect(stdout).toContain('PASS Admin Overview');
  expect(stdout).toContain('PASS Admin Service Points');
  expect(stdout).toContain('PASS Cleaner My Work');
});
