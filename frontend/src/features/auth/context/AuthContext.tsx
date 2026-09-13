import React, { createContext, useState, useEffect, useCallback } from 'react';
import type { AuthUser } from '../../../types';
import * as authSession from '../../../services/authSession';

/** Phase 1 kept the whole session here. ADR facility-0013 rejected localStorage, so a leftover copy is removed on load. */
const LEGACY_STORAGE_KEY = 'facility_scanner_user';

export interface AuthContextType {
  currentUser: AuthUser | null;
  /** True until the page-load refresh settles, so a logged-in user never sees the login page flash. */
  isLoading: boolean;
  /** True while a login request is running. */
  isSubmitting: boolean;
  error: string | null;
  login: (username: string, password: string) => Promise<boolean>;
  logout: () => Promise<void>;
  isAuthenticated: boolean;
  isAdmin: boolean;
}

export const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [currentUser, setCurrentUser] = useState<AuthUser | null>(() => authSession.getCurrentUser());
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    localStorage.removeItem(LEGACY_STORAGE_KEY);
    const unsubscribe = authSession.onSessionChange(setCurrentUser);
    // ADR facility-0013: the access token is never persisted, so a reload asks the refresh cookie for a new one
    authSession.refreshSession().finally(() => setIsLoading(false));
    return unsubscribe;
  }, []);

  const login = useCallback(async (username: string, password: string): Promise<boolean> => {
    setIsSubmitting(true);
    setError(null);
    try {
      await authSession.login(username, password);
      return true;
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'เข้าสู่ระบบไม่สำเร็จ');
      return false;
    } finally {
      setIsSubmitting(false);
    }
  }, []);

  const logout = useCallback(() => authSession.logout(), []);

  return (
    <AuthContext.Provider
      value={{
        currentUser,
        isLoading,
        isSubmitting,
        error,
        login,
        logout,
        isAuthenticated: !!currentUser,
        isAdmin: currentUser?.role === 'admin'
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};
