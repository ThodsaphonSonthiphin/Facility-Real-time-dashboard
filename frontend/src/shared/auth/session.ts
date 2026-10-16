import type { Dispatch } from '@reduxjs/toolkit';
import type { RootState } from '../../store';
import type { AuthResponse } from '../api/types';
import { bootstrapFinished, sessionExpired, setCredentials } from '../../store/slices/authSlice';

export type RefreshOutcome = 'refreshed' | 'rejected' | 'offline';

/**
 * facility-0015: one refresh at a time. Everyone who asks while a refresh is in flight shares it, because the
 * server treats a second use of the same refresh token as theft and ends the session.
 */
let inflight: Promise<RefreshOutcome> | null = null;

/** Counts sessions ended on purpose; a refresh that started before the bump must not log the tab back in. */
let sessionEpoch = 0;

/** A stalled connection must not keep the page-load splash up forever (ADR 0068). */
const REFRESH_TIMEOUT_MS = 15_000;

/**
 * Called by endSession so a refresh already running cannot log the tab back in (its result is ignored), and so the
 * logout request can wait for that refresh's cookie. Resolves when no refresh is in flight.
 */
export function abandonSession(): Promise<unknown> {
  sessionEpoch += 1;
  return inflight ?? Promise.resolve();
}

export function refreshSession(dispatch: Dispatch): Promise<RefreshOutcome> {
  inflight ??= runRefresh(dispatch).finally(() => {
    inflight = null;
  });
  return inflight;
}

async function runRefresh(dispatch: Dispatch): Promise<RefreshOutcome> {
  const epoch = sessionEpoch;
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), REFRESH_TIMEOUT_MS);
  let response: Response;
  let answer: AuthResponse | null = null;
  try {
    response = await fetch('/api/auth/refresh', {
      method: 'POST',
      credentials: 'same-origin',
      signal: controller.signal,
    });
    if (response.ok) {
      try {
        answer = (await response.json()) as AuthResponse;
      } catch {
        return 'offline'; // a 200 that is not our JSON (captive portal, cut body): keep the session
      }
    }
  } catch {
    return 'offline'; // no network, a timeout or a server fault: keep the session, a phone losing signal must not log the person out
  } finally {
    clearTimeout(timer);
  }
  if (epoch !== sessionEpoch) {
    return 'rejected'; // the session was ended on purpose while this refresh ran: do not touch the store
  }
  if (response.status === 401 || response.status === 403) {
    dispatch(sessionExpired());
    return 'rejected';
  }
  if (!response.ok) {
    return 'offline'; // no network or a server fault (5xx, proxy during a deploy): keep the session
  }
  if (!answer) {
    return 'offline';
  }
  dispatch(setCredentials(answer));
  return 'refreshed';
}

/** Page load (facility-0023 item 4): one refresh decides whether the cookie still holds a session. */
export async function startSession(dispatch: Dispatch): Promise<void> {
  try {
    await refreshSession(dispatch);
  } finally {
    dispatch(bootstrapFinished());
  }
}

/** facility-0023 item 3: a token with this little left is refreshed before SignalR uses it. */
const REFRESH_MARGIN_MS = 30_000;

/**
 * SignalR's accessTokenFactory, asked on every connect and reconnect. It shares the one refresh in flight
 * (facility-0015). A refused refresh ends the session: the answer is '' and the store listener empties the cache, which
 * stops the hub.
 */
export async function getValidAccessToken(getState: () => RootState, dispatch: Dispatch): Promise<string> {
  const { accessToken, expiresAt } = getState().auth;
  if (accessToken && expiresAt && Date.parse(expiresAt) - Date.now() > REFRESH_MARGIN_MS) {
    return accessToken;
  }
  await refreshSession(dispatch);
  return getState().auth.accessToken ?? '';
}
