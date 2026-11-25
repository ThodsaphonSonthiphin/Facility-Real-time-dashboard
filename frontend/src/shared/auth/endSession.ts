import type { Dispatch } from '@reduxjs/toolkit';
import { signedOut } from '../../store/slices/authSlice';
import { abandonSession, advanceSessionEpoch, currentSessionEpoch } from './session';

let activeLogout: AbortController | null = null;

/**
 * Called when a new login starts so a pending or in-flight logout from the prior user cannot
 * revoke or delete the newly issued refresh cookie on a shared device.
 */
export function cancelPendingLogout(): void {
  advanceSessionEpoch();
  activeLogout?.abort();
  activeLogout = null;
}

/**
 * "ออกจากระบบ": this tab forgets the session at once (the store listener also empties the API cache), then waits for
 * any refresh already running and ignores its result, and only then logs out on the server, so the logout carries
 * the newest refresh cookie and a late refresh cannot log the tab back in.
 */
export async function endSession(dispatch: Dispatch): Promise<void> {
  dispatch(signedOut());
  const pendingRefresh = abandonSession();
  const epoch = currentSessionEpoch();
  try {
    await pendingRefresh;
  } catch {
    // the outcome of the abandoned refresh does not matter
  }
  if (currentSessionEpoch() !== epoch) {
    return;
  }
  const controller = new AbortController();
  activeLogout = controller;
  try {
    await fetch('/api/auth/logout', {
      method: 'POST',
      credentials: 'same-origin',
      signal: controller.signal,
    });
  } catch {
    // offline or superseded by a new login: this tab already forgot the session (facility-0016)
  } finally {
    if (activeLogout === controller) {
      activeLogout = null;
    }
  }
}
