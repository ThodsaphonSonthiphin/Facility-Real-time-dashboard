import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';
import { resetFakeHub } from './fakeSignalR';

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  // test/fakeGps.ts and the "plain http" tests define these on the browser objects
  Reflect.deleteProperty(navigator, 'geolocation');
  Reflect.deleteProperty(window, 'isSecureContext');
  resetFakeHub();
});
