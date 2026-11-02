import { defineConfig } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { engine } from './e2e/support/browser.ts';
import { desktop, phone } from './e2e/support/devices.ts';

// facility-0094: scripts/e2e.sh starts the API on a throwaway database and passes its address here
const baseURL = process.env.E2E_BASE_URL;
if (!baseURL) {
  throw new Error('E2E_BASE_URL is not set: run `npm run e2e`, which starts the API first');
}
const address = new URL(baseURL);
const runDirectory = process.env.E2E_RUN_DIR;
if (address.protocol !== 'http:' || address.hostname !== '127.0.0.1' || !runDirectory) {
  throw new Error('E2E writes require the runner-owned loopback environment. Run npm run e2e.');
}
const ownership = JSON.parse(readFileSync(`${runDirectory}/ownership.json`, 'utf8')) as { cleaned: boolean; token: string };
if (ownership.cleaned || ownership.token !== process.env.E2E_SEED_TOKEN) {
  throw new Error('Missing or expired E2E database ownership');
}

export default defineConfig({
  testDir: './e2e',
  // One at a time: the tests share the run's database, and a shift-in changes what the check-in page offers
  workers: 1,
  fullyParallel: false,
  forbidOnly: true,
  retries: 0, // Mutating tests need a fresh database for a genuine retry.
  timeout: 90_000,
  expect: { timeout: 10_000 },
  outputDir: process.env.E2E_RUN_DIR ? `${process.env.E2E_RUN_DIR}/test-results` : './test-results',
  reporter: [
    ['list'],
    ['html', { open: 'never', outputFolder: process.env.E2E_RUN_DIR ? `${process.env.E2E_RUN_DIR}/html` : './playwright-report' }],
    ['json', { outputFile: process.env.E2E_RUN_DIR ? `${process.env.E2E_RUN_DIR}/results.json` : './test-results/results.json' }],
  ],
  use: {
    baseURL,
    browserName: engine,
    headless: process.env.E2E_HEADLESS === '1',
    launchOptions: { slowMo: Number(process.env.E2E_SLOW_MO ?? 350) },
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
    screenshot: 'on',
    trace: 'on',
  },
  projects: [
    { name: 'phone', testDir: './e2e/phone', use: phone },
    { name: 'desktop', testDir: './e2e/desktop', use: desktop },
  ],
});
