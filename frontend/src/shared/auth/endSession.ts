import type { Dispatch } from '@reduxjs/toolkit';
import { baseApi } from '../api/baseApi';
import { signedOut } from '../../store/slices/authSlice';

/** "ออกจากระบบ": ends the session on the server, forgets it here, and drops every cached answer of this account. */
export async function endSession(dispatch: Dispatch): Promise<void> {
  try {
    await fetch('/api/auth/logout', { method: 'POST', credentials: 'same-origin' });
  } catch {
    // offline: this tab forgets the session now; the server ends it after 30 days unused (facility-0016)
  }
  dispatch(signedOut());
  dispatch(baseApi.util.resetApiState());
}
