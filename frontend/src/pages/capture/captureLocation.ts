import type { GpsFix, LocationProblem } from '../../shared/geo/readLocation';

/** facility-0045 item 2: read for 30 s standing still at the sign. */
export const CAPTURE_MS = 30_000;

/** Fewer readings than this in 30 s give no median worth keeping. */
export const MIN_READINGS = 3;

export interface CaptureProgress {
  seconds: number;
  readings: number;
  /** The latest reading's accuracy, null before the first. */
  accuracyM: number | null;
}

export type CaptureResult =
  | { kind: 'fix'; fix: GpsFix; readings: number }
  | { kind: 'too-few'; readings: number }
  | { kind: LocationProblem };

const PERMISSION_DENIED = 1; // GeolocationPositionError.PERMISSION_DENIED

function median(values: number[]): number {
  const sorted = [...values].sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
}

/** The median latitude, longitude and accuracy, each on its own; the accuracy rounded up to whole metres. */
export function medianFix(fixes: readonly GpsFix[]): GpsFix {
  return {
    latitude: median(fixes.map((fix) => fix.latitude)),
    longitude: median(fixes.map((fix) => fix.longitude)),
    accuracyM: Math.ceil(median(fixes.map((fix) => fix.accuracyM))),
  };
}

/** facility-0045 item 2: an accuracy worse than the radius is worth a warning, not a refusal. */
export function accuracyVerdict(accuracyM: number, radiusM: number): { ok: boolean; text: string } {
  return accuracyM <= radiusM
    ? { ok: true, text: `ความแม่นยำ ±${accuracyM} ม. · น้อยกว่ารัศมี ใช้ได้` }
    : { ok: false, text: `ความแม่นยำ ±${accuracyM} ม. แย่กว่ารัศมี ${radiusM} ม. ควรขยายรัศมีของป้ายนี้ หรือเก็บใหม่` };
}

/**
 * Watches the GPS (high accuracy, no cached fix) for 30 s, reporting once a second and on every reading, then gives
 * the median. A refused permission ends it at once; other GPS errors only mean fewer readings. `cancel` stops the
 * watch and the clock, and the result then never comes.
 */
export function startCapture(onProgress: (progress: CaptureProgress) => void): {
  done: Promise<CaptureResult>;
  cancel: () => void;
} {
  let cancel: () => void = () => undefined;
  const done = new Promise<CaptureResult>((resolve) => {
    if (window.isSecureContext === false) {
      resolve({ kind: 'insecure' });
      return;
    }
    if (!('geolocation' in navigator)) {
      resolve({ kind: 'unavailable' });
      return;
    }
    const fixes: GpsFix[] = [];
    let seconds = 0;
    const report = () =>
      onProgress({ seconds, readings: fixes.length, accuracyM: fixes.length > 0 ? fixes[fixes.length - 1].accuracyM : null });

    const watchId = navigator.geolocation.watchPosition(
      (position) => {
        fixes.push({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          accuracyM: position.coords.accuracy,
        });
        report();
      },
      (error) => {
        if (error.code === PERMISSION_DENIED) {
          stop();
          resolve({ kind: 'denied' });
        }
      },
      { enableHighAccuracy: true, maximumAge: 0 },
    );
    const timer = setInterval(() => {
      seconds += 1;
      report();
      if (seconds * 1000 >= CAPTURE_MS) {
        stop();
        resolve(
          fixes.length < MIN_READINGS
            ? { kind: 'too-few', readings: fixes.length }
            : { kind: 'fix', fix: medianFix(fixes), readings: fixes.length },
        );
      }
    }, 1000);

    function stop() {
      clearInterval(timer);
      navigator.geolocation.clearWatch(watchId);
    }
    cancel = stop;
  });
  return { done, cancel: () => cancel() };
}
