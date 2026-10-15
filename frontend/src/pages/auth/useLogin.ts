import type { Location } from 'react-router';
import { useLocation } from 'react-router';
import type { LoginRequest } from '../../shared/api/types';
import { homeOf } from '../../shared/auth/roles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { setCredentials } from '../../store/slices/authSlice';
import { useLoginMutation } from './authApi';

/** What ProtectedRoute put in the location state: the page to return to after login (facility-0026 item 4). */
export interface LoginState {
  from?: Location;
}

/**
 * Shared by both login pages. Logging in only stores the session; the page then sees `redirectTo` and navigates,
 * so there is one way out of the login page whether the person just logged in or was already logged in.
 */
export function useLogin(wrongSecretText: string) {
  const [login, { isLoading, error }] = useLoginMutation();
  const dispatch = useAppDispatch();
  const user = useAppSelector((state) => state.auth.currentUser);
  const location = useLocation();
  const from = (location.state as LoginState | null)?.from;

  const submit = async (body: LoginRequest) => {
    try {
      dispatch(setCredentials(await login(body).unwrap()));
    } catch {
      // shown through errorText
    }
  };

  return {
    submit,
    isLoading,
    errorText: error ? loginErrorText(error, wrongSecretText) : null,
    returnsToPage: from !== undefined,
    redirectTo: user ? (from ? `${from.pathname}${from.search}` : homeOf(user.role)) : null,
  };
}

/** 401 is a wrong ID or secret, or a deactivated account; 429 is the login throttle (facility-0054). */
function loginErrorText(error: unknown, wrongSecretText: string): string {
  const status = (error as { status?: unknown }).status;
  if (status === 401) {
    return wrongSecretText;
  }
  if (status === 429) {
    return 'กรอกผิดหลายครั้ง รอสักครู่แล้วลองใหม่';
  }
  return getErrorMessage(error);
}
