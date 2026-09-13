import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import * as authSession from './authSession';

const user = { id: 1, username: 'somchai', fullName: 'สมชาย ใจดี', role: 'cleaner' };

function authBody(accessToken: string, expiresInMs = 5 * 60_000) {
  return { accessToken, expiresAt: new Date(Date.now() + expiresInMs).toISOString(), user };
}

function jsonResponse(status: number, body?: unknown): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

function authHeader(init: RequestInit | undefined): string | null {
  return new Headers(init?.headers).get('Authorization');
}

const fetchMock = vi.fn<(url: string, init?: RequestInit) => Promise<Response>>();
const callsTo = (url: string) => fetchMock.mock.calls.filter(([calledUrl]) => calledUrl === url).length;

beforeEach(() => {
  authSession.resetSessionForTests();
  fetchMock.mockReset();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('authSession', () => {
  it('keeps the access token in memory and attaches it to API calls', async () => {
    fetchMock.mockImplementation(async (url) =>
      url === '/api/auth/login' ? jsonResponse(200, authBody('A')) : jsonResponse(200, []),
    );

    await authSession.login('somchai', 'password123');
    await authSession.apiFetch('/api/service-points');

    const [, init] = fetchMock.mock.calls[1];
    expect(authHeader(init)).toBe('Bearer A');
    expect(authSession.getCurrentUser()).toEqual(user);
  });

  it('rejects a failed login with a Thai message and stores nothing', async () => {
    fetchMock.mockResolvedValue(jsonResponse(401));

    await expect(authSession.login('somchai', 'wrong')).rejects.toThrow('ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง');
    expect(authSession.getCurrentUser()).toBeNull();
  });

  it('restores a session from the refresh cookie on page load', async () => {
    fetchMock.mockResolvedValue(jsonResponse(200, authBody('R')));

    const restored = await authSession.refreshSession();

    expect(restored?.accessToken).toBe('R');
    expect(authSession.getCurrentUser()?.username).toBe('somchai');
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/refresh', { method: 'POST', credentials: 'same-origin' });
  });

  it('shares one refresh between concurrent 401s, then retries each call with the new token', async () => {
    fetchMock.mockImplementation(async (url, init) => {
      if (url === '/api/auth/login') return jsonResponse(200, authBody('A'));
      if (url === '/api/auth/refresh') {
        await new Promise((resolve) => setTimeout(resolve, 5));
        return jsonResponse(200, authBody('B'));
      }
      return authHeader(init) === 'Bearer B' ? jsonResponse(200, []) : jsonResponse(401);
    });
    await authSession.login('somchai', 'password123');

    const [first, second] = await Promise.all([authSession.apiFetch('/api/a'), authSession.apiFetch('/api/b')]);

    expect(first.status).toBe(200);
    expect(second.status).toBe(200);
    expect(callsTo('/api/auth/refresh')).toBe(1);
  });

  it('clears the session, tells listeners and returns the 401 when refresh fails', async () => {
    fetchMock.mockImplementation(async (url) =>
      url === '/api/auth/login' ? jsonResponse(200, authBody('A')) : jsonResponse(401),
    );
    await authSession.login('somchai', 'password123');
    const listener = vi.fn();
    authSession.onSessionChange(listener);

    const response = await authSession.apiFetch('/api/service-points');

    expect(response.status).toBe(401);
    expect(authSession.getCurrentUser()).toBeNull();
    expect(listener).toHaveBeenLastCalledWith(null);
  });

  it('refreshes before sending when the token has 30 seconds or less left', async () => {
    fetchMock.mockImplementation(async (url) =>
      url === '/api/auth/login' ? jsonResponse(200, authBody('A', 20_000)) : jsonResponse(200, authBody('B')),
    );
    await authSession.login('somchai', 'password123');

    expect(await authSession.getValidAccessToken()).toBe('B');
    expect(await authSession.getValidAccessToken()).toBe('B');
    expect(callsTo('/api/auth/refresh')).toBe(1);
  });

  it('sends no Authorization header when nobody is logged in', async () => {
    fetchMock.mockResolvedValue(jsonResponse(401));

    const response = await authSession.apiFetch('/api/service-points');

    const apiCall = fetchMock.mock.calls.find(([url]) => url === '/api/service-points');
    expect(response.status).toBe(401);
    expect(authHeader(apiCall?.[1])).toBeNull();
    expect(callsTo('/api/auth/refresh')).toBe(1);
  });

  it('forgets the session on logout even when the request fails', async () => {
    fetchMock.mockImplementation(async (url) => {
      if (url === '/api/auth/login') return jsonResponse(200, authBody('A'));
      throw new TypeError('Failed to fetch');
    });
    await authSession.login('somchai', 'password123');

    await authSession.logout();

    expect(authSession.getCurrentUser()).toBeNull();
  });
});
