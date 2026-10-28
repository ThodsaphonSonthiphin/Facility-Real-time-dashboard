import { devices, type BrowserContextOptions } from '@playwright/test';
import { signLocation } from './seed.ts';

/** Thai, on Thai time, like every phone and desk the system serves. */
const thai: BrowserContextOptions = { locale: 'th-TH', timezoneId: 'Asia/Bangkok' };

/**
 * facility-0094: a phone the size of an iPhone 13–15, in Chromium, standing at the AR01 check-in sign. Also used for
 * the second context of the live-flow test, so everything a phone needs is here, not in the config's `use`.
 */
export const phone: BrowserContextOptions = {
  ...thai,
  viewport: { width: 390, height: 844 },
  deviceScaleFactor: 3,
  isMobile: true,
  hasTouch: true,
  userAgent: devices['Pixel 7'].userAgent,
  geolocation: signLocation,
  permissions: ['geolocation'],
};

/** An Admin's desk (D1 is a desk page, wireframe D1). */
export const desktop: BrowserContextOptions = {
  ...thai,
  viewport: { width: 1280, height: 800 },
};
