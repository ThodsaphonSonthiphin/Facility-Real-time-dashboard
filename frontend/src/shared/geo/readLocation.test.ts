import { describe, expect, it } from 'vitest';
import { fakeGps, nearSign } from '../../test/fakeGps';
import { readLocation } from './readLocation';

describe('readLocation (facility-0036, 0037)', () => {
  it('a fix carries the phone accuracy as accuracyM', async () => {
    fakeGps(nearSign);

    await expect(readLocation()).resolves.toEqual({
      kind: 'fix',
      fix: { latitude: 13.7563, longitude: 100.5018, accuracyM: 12 },
    });
  });

  it('a refused permission is "denied"; any other failure is "unavailable"', async () => {
    fakeGps('denied', 'unavailable');

    await expect(readLocation()).resolves.toEqual({ kind: 'denied' });
    await expect(readLocation()).resolves.toEqual({ kind: 'unavailable' });
  });

  it('on plain http it does not ask the phone, because browsers never answer there', async () => {
    const gps = fakeGps(nearSign);
    Object.defineProperty(window, 'isSecureContext', { value: false, configurable: true });

    await expect(readLocation()).resolves.toEqual({ kind: 'insecure' });
    expect(gps.calls).toBe(0);
  });
});
