import { devices, type BrowserContextOptions } from '@playwright/test';
import { engine } from './browser.ts';
import { signLocation } from './seed.ts';

/** Thai, on Thai time, like every phone and desk the system serves. */
const thai: BrowserContextOptions = { locale: 'th-TH', timezoneId: 'Asia/Bangkok' };

/**
 * facility-0094: a phone the size of an iPhone 13–15, standing at the AR01 check-in sign. Also used for the second
 * context of the live-flow test, so everything a phone needs is here, not in the config's `use`. WebKit says it is an
 * iPhone, the others an Android phone; Firefox has no mobile emulation (`isMobile`), so there it is a narrow touch window.
 */
export const phone: BrowserContextOptions = {
  ...thai,
  viewport: { width: 390, height: 844 },
  deviceScaleFactor: 3,
  ...(engine === 'firefox' ? {} : { isMobile: true }),
  hasTouch: true,
  userAgent: devices[engine === 'webkit' ? 'iPhone 13' : 'Pixel 7'].userAgent,
  geolocation: signLocation,
  permissions: ['geolocation'],
};

/** An Admin's desk (D1 is a desk page, wireframe D1). */
export const desktop: BrowserContextOptions = {
  ...thai,
  viewport: { width: 1280, height: 800 },
};
