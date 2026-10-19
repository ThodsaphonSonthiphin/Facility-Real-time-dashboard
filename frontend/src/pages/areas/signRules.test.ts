import { describe, expect, it } from 'vitest';
import { parseLatLng, parseRadius } from './signRules';

describe('sign rules (facility-0038, 0045)', () => {
  it('accepts a whole radius from 10 to 500 m, the server’s limits', () => {
    expect(parseRadius('50')).toBe(50);
    expect(parseRadius(' 10 ')).toBe(10);
    expect(parseRadius('500')).toBe(500);
    expect(parseRadius('9')).toBeNull();
    expect(parseRadius('501')).toBeNull();
    expect(parseRadius('50.5')).toBeNull();
    expect(parseRadius('')).toBeNull();
  });

  it('reads coordinates pasted from a map app', () => {
    expect(parseLatLng('13.756300, 100.501800')).toEqual({ latitude: 13.7563, longitude: 100.5018 });
    expect(parseLatLng('13.7563 100.5018')).toEqual({ latitude: 13.7563, longitude: 100.5018 });
    expect(parseLatLng(' -33.8688,151.2093 ')).toEqual({ latitude: -33.8688, longitude: 151.2093 });
    expect(parseLatLng('13.7563')).toBeNull();
    expect(parseLatLng('91, 100')).toBeNull();
    expect(parseLatLng('13.7, 181')).toBeNull();
    expect(parseLatLng('ตึก A')).toBeNull();
  });
});
