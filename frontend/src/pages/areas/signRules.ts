/** AdminSignEndpoints.MinRadiusM / MaxRadiusM (facility-0038). */
export const MIN_RADIUS_M = 10;
export const MAX_RADIUS_M = 500;

/** A whole number of metres within the server's limits, or null. */
export function parseRadius(text: string): number | null {
  const radius = Number(text);
  return text.trim() !== '' && Number.isInteger(radius) && radius >= MIN_RADIUS_M && radius <= MAX_RADIUS_M
    ? radius
    : null;
}

/**
 * Plan 10 decision 5: the map fallback is a pair copied from a map app, "13.756300, 100.501800" (a comma or a space
 * between), within the world's bounds. Anything else is null.
 */
export function parseLatLng(text: string): { latitude: number; longitude: number } | null {
  const match = text.trim().match(/^(-?\d+(?:\.\d+)?)\s*[, ]\s*(-?\d+(?:\.\d+)?)$/);
  if (!match) {
    return null;
  }
  const latitude = Number(match[1]);
  const longitude = Number(match[2]);
  return Math.abs(latitude) <= 90 && Math.abs(longitude) <= 180 ? { latitude, longitude } : null;
}
