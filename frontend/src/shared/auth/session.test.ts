import { describe, expect, it, vi } from 'vitest';
import { makeStore } from '../../store';
import { setCredentials } from '../../store/slices/authSlice';
import { authAnswer, fakeServer } from '../../test/fakeServer';
import { baseApi } from '../api/baseApi';
import { endSession } from './endSession';
import { refreshSession, startSession } from './session';

const probeApi = baseApi.injectEndpoints({
  endpoints: (build) => ({ sessionProbe: build.query<{ ok: boolean }, void>({ query: () => '/session-probe' }) }),
});

describe('startSession', () => {
  it('a live cookie restores the session before anything routes', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('admin') });
    const store = makeStore();

    await startSession(store.dispatch);

    expect(store.getState().auth).toMatchObject({ isBootstrapping: false, accessToken: 'token-admin' });
    expect(store.getState().auth.currentUser?.role).toBe('admin');
  });

  it('no cookie ends bootstrapping logged out', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => ({ status: 401 }) });
    const store = makeStore();

    await startSession(store.dispatch);

    expect(store.getState().auth).toMatchObject({ isBootstrapping: false, currentUser: null, signedOutByUser: false });
  });

  it('no network ends bootstrapping too, so the page never hangs on the splash', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('offline')));
    const store = makeStore();

    await startSession(store.dispatch);

    expect(store.getState().auth.isBootstrapping).toBe(false);
  });

  it('a refresh answer that is not JSON still ends bootstrapping', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValue(
          new Response('<html>portal</html>', { status: 200, headers: { 'Content-Type': 'text/html' } }),
        ),
    );
    const store = makeStore();

    await startSession(store.dispatch);

    expect(store.getState().auth.isBootstrapping).toBe(false);
  });

  it('a refresh that never answers gives up after 15 s and ends bootstrapping', async () => {
    vi.useFakeTimers();
    try {
      vi.stubGlobal(
        'fetch',
        vi.fn(
          (_input: RequestInfo | URL, init?: RequestInit) =>
            new Promise<Response>((_resolve, reject) => {
              init?.signal?.addEventListener('abort', () => reject(new DOMException('aborted', 'AbortError')));
            }),
        ),
      );
      const store = makeStore();

      const started = startSession(store.dispatch);
      await vi.advanceTimersByTimeAsync(15_000);
      await started;

      expect(store.getState().auth.isBootstrapping).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });
});

describe('refreshSession', () => {
  it('a refused refresh also drops every cached answer of the account', async () => {
    fakeServer({
      'GET /api/session-probe': () => ({ status: 200, body: { ok: true } }),
      'POST /api/auth/refresh': () => ({ status: 401 }),
    });
    const store = makeStore();
    store.dispatch(setCredentials(authAnswer('cleaner').body));
    await store.dispatch(probeApi.endpoints.sessionProbe.initiate());
    expect(Object.keys(store.getState().api.queries)).toHaveLength(1);

    await refreshSession(store.dispatch);

    expect(store.getState().api.queries).toEqual({});
    expect(store.getState().auth.currentUser).toBeNull();
  });

  it('a server fault (503) keeps the session', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => ({ status: 503 }) });
    const store = makeStore();
    store.dispatch(setCredentials(authAnswer('cleaner').body));

    const outcome = await refreshSession(store.dispatch);

    expect(outcome).toBe('offline');
    expect(store.getState().auth.currentUser?.role).toBe('cleaner');
  });

  it('403 ends the session like 401', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => ({ status: 403 }) });
    const store = makeStore();
    store.dispatch(setCredentials(authAnswer('cleaner').body));

    const outcome = await refreshSession(store.dispatch);

    expect(outcome).toBe('rejected');
    expect(store.getState().auth.currentUser).toBeNull();
  });

  it('keeps the session when the network drops for a moment', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('offline')));
    const store = makeStore();
    store.dispatch(setCredentials(authAnswer('cleaner').body));

    const outcome = await refreshSession(store.dispatch);

    expect(outcome).toBe('offline');
    expect(store.getState().auth.currentUser?.role).toBe('cleaner');
  });
});

describe('endSession', () => {
  it('logs out on the server and drops every cached answer of the account', async () => {
    const server = fakeServer({
      'GET /api/session-probe': () => ({ status: 200, body: { ok: true } }),
      'POST /api/auth/logout': () => ({ status: 204 }),
    });
    const store = makeStore();
    store.dispatch(setCredentials(authAnswer('cleaner').body));
    await store.dispatch(probeApi.endpoints.sessionProbe.initiate());
    expect(Object.keys(store.getState().api.queries)).toHaveLength(1);

    await endSession(store.dispatch);

    expect(server.calls.some((c) => c.method === 'POST' && c.path === '/api/auth/logout')).toBe(true);
    expect(store.getState().auth).toMatchObject({ currentUser: null, accessToken: null, signedOutByUser: true });
    expect(store.getState().api.queries).toEqual({});
  });

  it('a refresh that finishes after logout does not log the tab back in', async () => {
    let release!: () => void;
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    const server = fakeServer({
      'POST /api/auth/refresh': async () => {
        await held;
        return authAnswer('cleaner', 'token-late');
      },
      'POST /api/auth/logout': () => ({ status: 204 }),
    });
    const store = makeStore();
    store.dispatch(setCredentials(authAnswer('cleaner').body));

    const refreshing = refreshSession(store.dispatch);
    const ending = endSession(store.dispatch);
    expect(store.getState().auth.currentUser).toBeNull();
    release();
    await Promise.all([refreshing, ending]);

    expect(store.getState().auth).toMatchObject({ currentUser: null, accessToken: null, signedOutByUser: true });
    const paths = server.calls.map((c) => c.path);
    expect(paths.indexOf('/api/auth/logout')).toBeGreaterThan(paths.indexOf('/api/auth/refresh'));
  });

  it('still forgets the session here when the logout call cannot reach the server', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('offline')));
    const store = makeStore();
    store.dispatch(setCredentials(authAnswer('cleaner').body));

    await endSession(store.dispatch);

    expect(store.getState().auth.currentUser).toBeNull();
  });
});
