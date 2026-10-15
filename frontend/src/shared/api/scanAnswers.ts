import type { BlockedScan } from './types';

/** An RTK Query error's HTTP status, or its kind ('FETCH_ERROR', 'TIMEOUT_ERROR', 'PARSING_ERROR'). */
export function statusOf(error: unknown): unknown {
  return typeof error === 'object' && error !== null && 'status' in error ? error.status : undefined;
}

/** facility-0041, 0048: a 403 that names the scanned sign's Area is a Blocked Scan, not a missing permission. */
export function blockedScanOf(error: unknown): BlockedScan | null {
  if (statusOf(error) !== 403) {
    return null;
  }
  const data = (error as { data?: unknown }).data;
  return typeof data === 'object' && data !== null && 'reason' in data && 'areaCode' in data ? (data as BlockedScan) : null;
}

/**
 * facility-0052: no answer, no answer in time, an answer that is not ours, or a server fault. As far as the phone can
 * tell nothing was saved, so the page says "ยังไม่ได้บันทึก" and the person sends again.
 */
export function isNotSent(error: unknown): boolean {
  const status = statusOf(error);
  return (
    status === 'FETCH_ERROR' ||
    status === 'TIMEOUT_ERROR' ||
    status === 'PARSING_ERROR' ||
    (typeof status === 'number' && status >= 500)
  );
}
