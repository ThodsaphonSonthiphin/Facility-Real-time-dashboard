import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';
import { expect } from './test.ts';

/**
 * Wait until the screen has settled before an audit: the page's own reads have finished and any CSS transition they
 * started (a button leaving its disabled grey, a view fading in) has finished. An audit taken mid-transition reads an
 * in-between colour that no user ever rests on.
 */
export async function settlePage(page: Page): Promise<void> {
  await page.waitForLoadState('networkidle');
  await page.evaluate(() => Promise.race([
    Promise.all(document.getAnimations().map((animation) => animation.finished.catch(() => undefined))),
    new Promise((resolve) => setTimeout(resolve, 1_000)),
  ]));
}

/** WCAG 2.2 A/AA with no critical or serious violation allowed on the settled screen. */
export async function expectNoSeriousViolations(page: Page): Promise<void> {
  await settlePage(page);
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22a', 'wcag22aa'])
    .analyze();
  const blocking = results.violations
    .filter((violation) => violation.impact === 'critical' || violation.impact === 'serious')
    .map(({ id, impact, nodes }) => ({
      id, impact,
      elements: nodes.map(({ target, failureSummary }) => ({ target, failureSummary })),
    }));
  expect(blocking, `WCAG violations on ${page.url()}`).toEqual([]);
}
