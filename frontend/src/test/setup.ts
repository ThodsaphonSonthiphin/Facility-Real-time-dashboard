import '@testing-library/jest-dom/vitest';
import { cleanup, configure } from '@testing-library/react';
import { afterEach, vi } from 'vitest';
import { resetFakeHub } from './fakeSignalR';

// jsdom has no <dialog> methods: open and close it as a browser does, without the top layer (D2's correction form)
HTMLDialogElement.prototype.showModal ??= function showModal(this: HTMLDialogElement) {
  this.setAttribute('open', '');
};
HTMLDialogElement.prototype.close ??= function close(this: HTMLDialogElement) {
  this.removeAttribute('open');
};

// A cold import() on the first Admin render and a loaded machine outlast the default 1000 ms
configure({ asyncUtilTimeout: 3_000 });

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  // test/fakeGps.ts and the "plain http" tests define these on the browser objects
  Reflect.deleteProperty(navigator, 'geolocation');
  Reflect.deleteProperty(window, 'isSecureContext');
  resetFakeHub();
});
