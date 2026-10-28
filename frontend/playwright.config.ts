import { defineConfig } from '@playwright/test';
import { desktop, phone } from './e2e/support/devices.ts';

// facility-0094: scripts/e2e.sh starts the API on a throwaway database and passes its address here
const baseURL = process.env.E2E_BASE_URL;
if (!baseURL) {
  throw new Error('E2E_BASE_URL is not set: run `npm run e2e`, which starts the API first');
}

export default defineConfig({
  testDir: './e2e',
  // One at a time: the tests share the run's database, and a shift-in changes what the check-in page offers
  workers: 1,
  fullyParallel: false,
  forbidOnly: true,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL,
    browserName: 'chromium',
    screenshot: 'on',
    trace: 'retain-on-failure',
  },
  projects: [
    { name: 'phone', testDir: './e2e/phone', use: phone },
    { name: 'desktop', testDir: './e2e/desktop', use: desktop },
  ],
});
