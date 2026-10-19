import { afterEach, describe, expect, it, vi } from 'vitest';
import { fakeGpsTrack } from '../../test/fakeGps';
import { accuracyVerdict, medianFix, startCapture, type CaptureProgress } from './captureLocation';

afterEach(() => vi.useRealTimers());

const reading = (latitude: number, longitude: number, accuracy: number) => ({ latitude, longitude, accuracy });

describe('captureLocation (facility-0045 item 2)', () => {
  it('takes the median of each value on its own, so one wild reading moves none of them', () => {
    expect(
      medianFix([
        { latitude: 13.7563, longitude: 100.5018, accuracyM: 20 },
        { latitude: 13.757, longitude: 100.502, accuracyM: 12 },
        { latitude: 13.7564, longitude: 100.5019, accuracyM: 16 },
      ]),
    ).toEqual({ latitude: 13.7564, longitude: 100.5019, accuracyM: 16 });
  });

  it('with an even count it takes the middle two, and rounds the accuracy up to whole metres', () => {
    expect(
      medianFix([
        { latitude: 10, longitude: 100, accuracyM: 10 },
        { latitude: 20, longitude: 110, accuracyM: 13 },
        { latitude: 30, longitude: 120, accuracyM: 20 },
        { latitude: 40, longitude: 130, accuracyM: 30 },
      ]),
    ).toEqual({ latitude: 25, longitude: 115, accuracyM: 17 });
  });

  it('reads for 30 s, reporting the seconds, the readings and the latest accuracy', async () => {
    vi.useFakeTimers();
    const gps = fakeGpsTrack([reading(13.7563, 100.5018, 20), reading(13.757, 100.502, 12), reading(13.7564, 100.5019, 16)]);
    const progress: CaptureProgress[] = [];
    const capture = startCapture((step) => progress.push(step));

    await vi.advanceTimersByTimeAsync(30_000);

    await expect(capture.done).resolves.toEqual({
      kind: 'fix',
      readings: 3,
      fix: { latitude: 13.7564, longitude: 100.5019, accuracyM: 16 },
    });
    expect(progress.find((step) => step.seconds === 18)).toEqual({ seconds: 18, readings: 3, accuracyM: 16 });
    expect(gps.cleared).toBe(1);
  });

  it('gives too-few with fewer than 3 readings in 30 s', async () => {
    vi.useFakeTimers();
    fakeGpsTrack([reading(13.7563, 100.5018, 20), reading(13.7564, 100.5019, 16)]);
    const capture = startCapture(() => undefined);

    await vi.advanceTimersByTimeAsync(30_000);

    await expect(capture.done).resolves.toEqual({ kind: 'too-few', readings: 2 });
  });

  it('stops at once when the location is refused', async () => {
    vi.useFakeTimers();
    const gps = fakeGpsTrack(['denied']);
    const capture = startCapture(() => undefined);

    await vi.advanceTimersByTimeAsync(1_000);

    await expect(capture.done).resolves.toEqual({ kind: 'denied' });
    expect(gps.cleared).toBe(1);
  });

  it('on plain http it asks for no location', async () => {
    Object.defineProperty(window, 'isSecureContext', { value: false, configurable: true });
    const gps = fakeGpsTrack([reading(13.7563, 100.5018, 20)]);

    await expect(startCapture(() => undefined).done).resolves.toEqual({ kind: 'insecure' });
    expect(gps.watches).toBe(0);
  });

  it('cancel stops the GPS and the clock', async () => {
    vi.useFakeTimers();
    const gps = fakeGpsTrack([reading(13.7563, 100.5018, 20), reading(13.757, 100.502, 12), reading(13.7564, 100.5019, 16)]);
    const progress: CaptureProgress[] = [];
    const capture = startCapture((step) => progress.push(step));
    await vi.advanceTimersByTimeAsync(2_000);

    capture.cancel();
    const reported = progress.length;
    await vi.advanceTimersByTimeAsync(30_000);

    expect(progress).toHaveLength(reported);
    expect(gps.cleared).toBe(1);
  });

  it('compares the accuracy with the sign’s radius', () => {
    expect(accuracyVerdict(18, 50)).toEqual({ ok: true, text: 'ความแม่นยำ ±18 ม. · น้อยกว่ารัศมี ใช้ได้' });
    expect(accuracyVerdict(62, 50)).toEqual({
      ok: false,
      text: 'ความแม่นยำ ±62 ม. แย่กว่ารัศมี 50 ม. ควรขยายรัศมีของป้ายนี้ หรือเก็บใหม่',
    });
  });
});
