import type { AuthResponse, AuthUser } from '../types';

type SessionListener = (user: AuthUser | null) => void;

/** Refresh when less than this is left, so a request never leaves with a token about to expire. */
const REFRESH_MARGIN_MS = 30_000;

// ADR facility-0013: the access token lives only in page memory. The refresh token is an HttpOnly cookie JS never sees.
let session: AuthResponse | null = null;
let inflightRefresh: Promise<AuthResponse | null> | null = null;
const listeners = new Set<SessionListener>();

function setSession(next: AuthResponse | null): void {
  session = next;
  const user = next?.user ?? null;
  listeners.forEach((listener) => listener(user));
}

export function onSessionChange(listener: SessionListener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function getCurrentUser(): AuthUser | null {
  return session?.user ?? null;
}

export async function login(username: string, password: string): Promise<AuthUser> {
  const res = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'same-origin',
    body: JSON.stringify({ username, password }),
  });
  if (!res.ok) {
    throw new Error('ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง');
  }
  const body = (await res.json()) as AuthResponse;
  setSession(body);
  return body.user;
}

/** ADR facility-0015: one refresh at a time. Concurrent callers share the same request. */
export function refreshSession(): Promise<AuthResponse | null> {
  if (!inflightRefresh) {
    inflightRefresh = runRefresh().finally(() => {
      inflightRefresh = null;
    });
  }
  return inflightRefresh;
}

async function runRefresh(): Promise<AuthResponse | null> {
  let res: Response;
  try {
    res = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'same-origin' });
  } catch {
    return session; // network error: keep the current session rather than logging the user out
  }
  const next = res.ok ? ((await res.json()) as AuthResponse) : null;
  setSession(next);
  return next;
}

export async function logout(): Promise<void> {
  try {
    await fetch('/api/auth/logout', { method: 'POST', credentials: 'same-origin' });
  } catch {
    // offline: this tab forgets the session now; the server revokes it on the next successful logout or after 30 days
  } finally {
    setSession(null);
  }
}

export async function getValidAccessToken(now: number = Date.now()): Promise<string | null> {
  if (session && Date.parse(session.expiresAt) - now > REFRESH_MARGIN_MS) {
    return session.accessToken;
  }
  const refreshed = await refreshSession();
  return refreshed?.accessToken ?? null;
}

/** fetch with the access token attached. On 401 it refreshes once and retries once. */
export async function apiFetch(input: string, init: RequestInit = {}): Promise<Response> {
  const send = (token: string | null): Promise<Response> => {
    const headers = new Headers(init.headers);
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }
    return fetch(input, { ...init, headers, credentials: 'same-origin' });
  };

  const token = await getValidAccessToken();
  const first = await send(token);
  if (first.status !== 401 || token === null) {
    return first;
  }
  const refreshed = await refreshSession();
  return refreshed ? send(refreshed.accessToken) : first;
}

export function resetSessionForTests(): void {
  session = null;
  inflightRefresh = null;
  listeners.clear();
}
