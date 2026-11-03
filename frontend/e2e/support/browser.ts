/** The engine of this run (E2E_BROWSER): Chromium by default; Firefox, or WebKit (Safari's engine), on request. */
export type Engine = 'chromium' | 'firefox' | 'webkit';

const engines: readonly Engine[] = ['chromium', 'firefox', 'webkit'];
const asked = process.env.E2E_BROWSER ?? 'chromium';
if (!engines.includes(asked as Engine)) {
  throw new Error(`E2E_BROWSER must be chromium, firefox or webkit, not ${asked}`);
}
export const engine = asked as Engine;

/** The Layout Instability API that layout-shift cases measure with is Chromium's only. */
export const measuresLayoutShift = engine === 'chromium';
