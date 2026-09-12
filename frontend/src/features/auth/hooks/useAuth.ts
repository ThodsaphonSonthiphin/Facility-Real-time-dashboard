import { useState, useEffect, useCallback } from 'react';
import type { UserSession } from '../../../types';
import { loginApi } from '../../../services/api';

const STORAGE_KEY = 'facility_scanner_user';

export function useAuth() {
  const [currentUser, setCurrentUser] = useState<UserSession | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) {
        const parsed = JSON.parse(stored) as UserSession;
        setCurrentUser(parsed);
      }
    } catch (e) {
      console.error('Failed to load user session from storage', e);
      localStorage.removeItem(STORAGE_KEY);
    } finally {
      setIsLoading(false);
    }
  }, []);

  const login = useCallback(async (username: string, password: string): Promise<boolean> => {
    setIsLoading(true);
    setError(null);
    try {
      const session = await loginApi(username, password);
      localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
      setCurrentUser(session);
      return true;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'เข้าสู่ระบบไม่สำเร็จ';
      setError(message);
      return false;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY);
    setCurrentUser(null);
  }, []);

  return {
    currentUser,
    isLoading,
    error,
    login,
    logout,
    isAuthenticated: !!currentUser
  };
}
