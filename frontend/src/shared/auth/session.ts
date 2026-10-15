import type { Dispatch } from '@reduxjs/toolkit';
import type { AuthResponse } from '../api/types';
import { bootstrapFinished, sessionExpired, setCredentials } from '../../store/slices/authSlice';

export type RefreshOutcome = 'refreshed' | 'rejected' | 'offline';

/**
 * facility-0015: one refresh at a time. Everyone who asks while a refresh is in flight shares it, because the
 * server treats a second use of the same refresh token as theft and ends the session.
 */
let inflight: Promise<RefreshOutcome> | null = null;

export function refreshSession(dispatch: Dispatch): Promise<RefreshOutcome> {
  inflight ??= runRefresh(dispatch).finally(() => {
    inflight = null;
  });
  return inflight;
}

async function runRefresh(dispatch: Dispatch): Promise<RefreshOutcome> {
  let response: Response;
  try {
    response = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'same-origin' });
  } catch {
    return 'offline'; // keep the session: a phone losing signal for a moment must not log the person out
  }
  if (!response.ok) {
    dispatch(sessionExpired());
    return 'rejected';
  }
  dispatch(setCredentials((await response.json()) as AuthResponse));
  return 'refreshed';
}

/** Page load (facility-0023 item 4): one refresh decides whether the cookie still holds a session. */
export async function startSession(dispatch: Dispatch): Promise<void> {
  await refreshSession(dispatch);
  dispatch(bootstrapFinished());
}
