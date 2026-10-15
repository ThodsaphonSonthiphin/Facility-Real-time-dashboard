import { describe, expect, it } from 'vitest';
import { otherAreaBlocked } from '../../test/cleanerFixtures';
import { blockedScanOf, isNotSent } from './scanAnswers';

describe('scanAnswers (facility-0041, 0052)', () => {
  it('isNotSent: no answer, a timeout, a foreign answer or a server fault; not a refusal', () => {
    const cases: [unknown, boolean][] = [
      [{ status: 'FETCH_ERROR' }, true],
      [{ status: 'TIMEOUT_ERROR' }, true],
      [{ status: 'PARSING_ERROR' }, true],
      [{ status: 500 }, true],
      [{ status: 503 }, true],
      [{ status: 400 }, false],
      [{ status: 403 }, false],
      [{ status: 409 }, false],
      [{ status: 429 }, false],
      [undefined, false],
    ];
    for (const [error, expected] of cases) {
      expect(isNotSent(error), JSON.stringify(error)).toBe(expected);
    }
  });

  it('blockedScanOf: only a 403 that names the sign\'s Area', () => {
    expect(blockedScanOf({ status: 403, data: otherAreaBlocked })).toEqual(otherAreaBlocked);
    expect(blockedScanOf({ status: 403, data: { message: 'x' } })).toBeNull();
  });
});
