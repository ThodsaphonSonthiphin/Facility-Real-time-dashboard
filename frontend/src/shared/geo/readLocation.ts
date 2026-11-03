/** What the browser's Geolocation API reported; the server stores all three (facility-0036, 0037). */
export interface GpsFix {
  latitude: number;
  longitude: number;
  accuracyM: number;
}

/**
 * denied: no permission, the one case that blocks a scan (facility-0036). unavailable: no fix in time.
 * insecure: the page is on plain http, where browsers never give a location (facility-0028, 0049).
 */
export type LocationProblem = 'denied' | 'unavailable' | 'insecure';

export type LocationReading = { kind: 'fix'; fix: GpsFix } | { kind: LocationProblem };

const PERMISSION_DENIED = 1; // GeolocationPositionError.PERMISSION_DENIED

/** A fresh, high-accuracy reading for every press: a cached fix may be from before the person walked to the sign. */
export function readLocation(): Promise<LocationReading> {
  if (window.isSecureContext === false) {
    return Promise.resolve({ kind: 'insecure' });
  }
  if (!('geolocation' in navigator)) {
    return Promise.resolve({ kind: 'unavailable' });
  }
  return new Promise((resolve) => {
    navigator.geolocation.getCurrentPosition(
      (position) =>
        resolve({
          kind: 'fix',
          fix: {
            latitude: position.coords.latitude,
            longitude: position.coords.longitude,
            accuracyM: position.coords.accuracy,
          },
        }),
      (error) => resolve({ kind: error.code === PERMISSION_DENIED ? 'denied' : 'unavailable' }),
      { enableHighAccuracy: true, timeout: 15_000, maximumAge: 0 },
    );
  });
}

/**
 * After a refusal, whether this page must reload to ask again. WebKit (iPhone Safari) keeps a refusal for the life of
 * the page even after the person turns location on; only a reload asks again (E2E probe 2026-11-02). Chromium asks
 * again at once, so it never reloads. A reload only when location is on but the page still gets a refusal: if it is
 * really still off, the help stays and nothing on the page is lost.
 */
export async function refusalNeedsReload(): Promise<boolean> {
  if (!('geolocation' in navigator)) {
    return false;
  }
  const refused = await new Promise<boolean>((resolve) => {
    setTimeout(() => resolve(false), 1_000); // a refusal answers at once; a fix on its way is not one
    navigator.geolocation.getCurrentPosition(
      () => resolve(false),
      (error) => resolve(error.code === PERMISSION_DENIED),
      { maximumAge: Infinity, timeout: 1_000 },
    );
  });
  if (!refused) {
    return false;
  }
  try {
    const status = await navigator.permissions.query({ name: 'geolocation' });
    return status.state !== 'denied';
  } catch {
    return true; // no Permissions API: a reload is the only way to ask again
  }
}
