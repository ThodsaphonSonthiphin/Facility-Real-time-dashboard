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
