import type { Page } from '@playwright/test';

/** facility-0094: what the tests read back from the page. Set by the init scripts below on every page load. */
declare global {
  interface Window {
    e2eLayoutShift: number;
    e2eTextShown: boolean;
  }
}

/** From the next page load on, sums layout shifts not caused by input: the CLS Lighthouse measured (2026-10-28). */
export async function recordLayoutShifts(page: Page): Promise<void> {
  await page.addInitScript(() => {
    window.e2eLayoutShift = 0;
    new PerformanceObserver((list) => {
      for (const entry of list.getEntries() as unknown as { value: number; hadRecentInput: boolean }[]) {
        if (!entry.hadRecentInput) {
          window.e2eLayoutShift += entry.value;
        }
      }
    }).observe({ type: 'layout-shift', buffered: true });
  });
}

/** The sum so far, after two frames so the last layout has been measured. */
export async function layoutShiftTotal(page: Page): Promise<number> {
  await page.evaluate(
    () => new Promise<void>((resolve) => requestAnimationFrame(() => requestAnimationFrame(() => resolve()))),
  );
  return page.evaluate(() => window.e2eLayoutShift);
}

/** From the next page load on, notes whether `text` ever appears, even for a moment. */
export async function watchForText(page: Page, text: string): Promise<void> {
  await page.addInitScript((wanted) => {
    window.e2eTextShown = false;
    new MutationObserver(() => {
      if (document.body?.textContent?.includes(wanted)) {
        window.e2eTextShown = true;
      }
    }).observe(document, { childList: true, subtree: true, characterData: true });
  }, text);
}

export async function textEverShown(page: Page): Promise<boolean> {
  return page.evaluate(() => window.e2eTextShown);
}

/** Holds every request matching the glob back by `ms`, so a loading phase lasts long enough to measure. */
export async function holdBack(page: Page, url: string, ms: number): Promise<void> {
  await page.route(url, async (route) => {
    await new Promise((resolve) => setTimeout(resolve, ms));
    await route.fallback();
  });
}

/**
 * CSS pixels the page is wider than the screen; 0 means nothing sticks out sideways. Measured against clientWidth, not
 * innerWidth: in phone emulation the layout viewport grows to fit wide content, so innerWidth would grow with it.
 */
export async function sidewaysOverflow(page: Page): Promise<number> {
  return page.evaluate(() => Math.max(0, document.documentElement.scrollWidth - document.documentElement.clientWidth));
}
