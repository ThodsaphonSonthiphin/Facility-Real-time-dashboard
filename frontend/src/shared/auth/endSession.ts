import type { Dispatch } from '@reduxjs/toolkit';
import { signedOut } from '../../store/slices/authSlice';
import { abandonSession } from './session';

/**
 * "ออกจากระบบ": this tab forgets the session at once (the store listener also empties the API cache), then waits for
 * any refresh already running and ignores its result, and only then logs out on the server, so the logout carries
 * the newest refresh cookie and a late refresh cannot log the tab back in.
 */
export async function endSession(dispatch: Dispatch): Promise<void> {
  dispatch(signedOut());
  try {
    await abandonSession();
  } catch {
    // the outcome of the abandoned refresh does not matter
  }
  try {
    await fetch('/api/auth/logout', { method: 'POST', credentials: 'same-origin' });
  } catch {
    // offline: this tab already forgot the session; the server ends it after 30 days unused (facility-0016)
  }
}
