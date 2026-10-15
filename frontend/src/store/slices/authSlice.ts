import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import type { AuthResponse, AuthUser } from '../../shared/api/types';

/** facility-0013, 0023: memory only. Nothing here is ever written to localStorage or sessionStorage. */
export interface AuthState {
  currentUser: AuthUser | null;
  accessToken: string | null;
  expiresAt: string | null;
  /** True from page load until the first refresh answers, so no page decides "not logged in" too early. */
  isBootstrapping: boolean;
  /** The person pressed "ออกจากระบบ": the next login starts at their own home, not at the page they left. */
  signedOutByUser: boolean;
}

const initialState: AuthState = {
  currentUser: null,
  accessToken: null,
  expiresAt: null,
  isBootstrapping: true,
  signedOutByUser: false,
};

const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    setCredentials(state, action: PayloadAction<AuthResponse>) {
      state.currentUser = action.payload.user;
      state.accessToken = action.payload.accessToken;
      state.expiresAt = action.payload.expiresAt;
      state.isBootstrapping = false;
      state.signedOutByUser = false;
    },
    /** The refresh cookie is gone, expired or revoked (facility-0016). */
    sessionExpired() {
      return { ...initialState, isBootstrapping: false };
    },
    signedOut() {
      return { ...initialState, isBootstrapping: false, signedOutByUser: true };
    },
    bootstrapFinished(state) {
      state.isBootstrapping = false;
    },
  },
});

export const { setCredentials, sessionExpired, signedOut, bootstrapFinished } = authSlice.actions;
export const authReducer = authSlice.reducer;
