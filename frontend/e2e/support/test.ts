import { test as base, expect, type BrowserContext } from '@playwright/test';

interface Evidence {
  errors: string[];
  responses: { method: string; path: string; status: number; milliseconds: number }[];
}
const activeEvidence = new Map<string, Evidence>();

function observe(context: BrowserContext, evidence: Evidence) {
  context.on('page', (page) => {
    page.on('pageerror', (error) => evidence.errors.push(error.message));
    // facility-0106: the app sends a strict CSP; Chromium reports anything it blocks as a console error
    page.on('console', (message) => {
      if (message.type() === 'error' && message.text().includes('Content Security Policy')) evidence.errors.push(message.text());
    });
  });
  context.on('requestfinished', (request) => {
    const url = new URL(request.url());
    if (!url.pathname.startsWith('/api/')) return;
    const timing = request.timing();
    const status = request.existingResponse()?.status() ?? 0;
    evidence.responses.push({ method: request.method(), path: url.pathname, status, milliseconds: Math.round(timing.responseEnd) });
    if (status >= 500) evidence.errors.push(`${request.method()} ${url.pathname}: HTTP ${status}`);
  });
}

/** Include the other role's window in the same runtime-error and response-duration evidence. */
export function observeAdditionalContext(context: BrowserContext) {
  const evidence = activeEvidence.get(base.info().testId);
  if (!evidence) throw new Error('Runtime evidence fixture is not active');
  observe(context, evidence);
}

/** Collect runtime failures and real loopback response timings without replacing API answers. */
export const test = base.extend<{ browserEvidence: void }>({
  browserEvidence: [async ({ context }, runTest, testInfo) => {
    const evidence: Evidence = { errors: [], responses: [] };
    activeEvidence.set(testInfo.testId, evidence);
    observe(context, evidence);
    try {
      await runTest();
    } finally {
      activeEvidence.delete(testInfo.testId);
      await testInfo.attach('browser-evidence', { body: JSON.stringify(evidence, null, 2), contentType: 'application/json' });
    }
    expect(evidence.errors, 'No uncaught page errors, CSP violations or HTTP5xx responses').toEqual([]);
  }, { auto: true }],
});
export { expect };
