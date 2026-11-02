import { test as base, expect, type BrowserContext } from '@playwright/test';
import { engine } from './browser.ts';

interface Evidence {
  errors: string[];
  responses: { method: string; path: string; status: number; milliseconds: number }[];
}
const activeEvidence = new Map<string, Evidence>();

const cspMark = 'E2E CSP violation:';

async function observe(context: BrowserContext, evidence: Evidence) {
  // facility-0106: the app sends a strict CSP. Every engine fires securitypolicyviolation on what it blocks; the page
  // passes it on as a console line (an init script is not itself under the page's CSP).
  await context.addInitScript(({ mark, engine }) => {
    document.addEventListener('securitypolicyviolation', (event) => {
      // Playwright's WebKit puts in a <style> of its own that has no source; a blank page under the app's CSP shows it
      // too (2026-11-02). Only that is skipped: an inline script with no source still counts (checked with a probe).
      if (engine === 'webkit' && event.effectiveDirective === 'style-src-elem' && !event.sourceFile) return;
      // WebKit also reports style-src-attr for its own drawing of any <select>, even one in plain HTML with no script
      // (probe 2026-11-02): the element has no style attribute, or is already off the page (React's createElement of a
      // select). Chromium reports neither, so the Chromium run still checks every style attribute.
      const target = event.target;
      if (event.effectiveDirective === 'style-src-attr' && engine === 'webkit' &&
        (!(target instanceof Element) || (target instanceof HTMLSelectElement && !target.hasAttribute('style')))) return;
      const element = event.target instanceof Element ? event.target.outerHTML.slice(0, 160) : '';
      console.error(`${mark} ${event.effectiveDirective} blocked ${event.blockedURI || 'inline'} ` +
        `from ${event.sourceFile || '?'}:${event.lineNumber}:${event.columnNumber} ${element}`);
    });
  }, { mark: cspMark, engine });
  context.on('page', (page) => {
    page.on('pageerror', (error) => evidence.errors.push(error.message));
    page.on('console', (message) => {
      if (message.text().startsWith(cspMark)) evidence.errors.push(message.text());
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
export async function observeAdditionalContext(context: BrowserContext) {
  const evidence = activeEvidence.get(base.info().testId);
  if (!evidence) throw new Error('Runtime evidence fixture is not active');
  await observe(context, evidence);
}

/** Collect runtime failures and real loopback response timings without replacing API answers. */
export const test = base.extend<{ browserEvidence: void }>({
  browserEvidence: [async ({ context }, runTest, testInfo) => {
    const evidence: Evidence = { errors: [], responses: [] };
    activeEvidence.set(testInfo.testId, evidence);
    await observe(context, evidence);
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
