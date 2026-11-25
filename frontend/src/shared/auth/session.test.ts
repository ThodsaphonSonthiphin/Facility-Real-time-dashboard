import { describe, expect, it, vi } from 'vitest';
import { makeStore } from '../../store';
import { setCredentials } from '../../store/slices/authSlice';
import { authAnswer, fakeServer } from '../../test/fakeServer';
import { baseApi } from '../api/baseApi';
import { cancelPendingLogout, endSession } from './endSession';
import { getValidAccessToken, refreshSession, startSession } from './session';

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

  it('requests a 15 s refresh deadline and ends bootstrapping when it expires', async () => {
    // Vitest's fake timers cannot advance Node's native AbortSignal.timeout.
    const deadline = new AbortController();
    const timeout = vi.spyOn(AbortSignal, 'timeout').mockReturnValueOnce(deadline.signal);
    const fetchRefresh = vi.fn(
      (_input: RequestInfo | URL, init?: RequestInit) =>
        new Promise<Response>((_resolve, reject) => {
          const signal = init?.signal;
          signal?.addEventListener('abort', () => reject(signal.reason));
        }),
    );
    vi.stubGlobal('fetch', fetchRefresh);
    const store = makeStore();

    const started = startSession(store.dispatch);
    expect(timeout).toHaveBeenCalledWith(15_000);
    expect(fetchRefresh).toHaveBeenCalledWith('/api/auth/refresh', expect.objectContaining({ signal: deadline.signal }));
    expect(store.getState().auth.isBootstrapping).toBe(true);

    deadline.abort(new DOMException('The operation timed out.', 'TimeoutError'));
    await started;
    expect(store.getState().auth.isBootstrapping).toBe(false);
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

  it('skips POST /api/auth/logout if a new login starts while waiting for an in-flight refresh', async () => {
    let release!: () => void;
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    const server = fakeServer({
      'POST /api/auth/refresh': async () => {
        await held;
        return authAnswer('cleaner', 'token-stale');
      },
      'POST /api/auth/logout': () => ({ status: 204 }),
    });
    const store = makeStore();
    store.dispatch(setCredentials(authAnswer('cleaner').body));

    const refreshing = refreshSession(store.dispatch);
    const ending = endSession(store.dispatch);
    cancelPendingLogout();
    release();
    await Promise.all([refreshing, ending]);

    expect(server.calls.some((c) => c.path === '/api/auth/logout')).toBe(false);
  });

  it('aborts an in-flight POST /api/auth/logout when a new login starts', async () => {
    let logoutSignal: AbortSignal | undefined;
    const fetchMock = vi.fn(
      (_input: RequestInfo | URL, init?: RequestInit) =>
        new Promise<Response>((_resolve, reject) => {
          logoutSignal = init?.signal ?? undefined;
          logoutSignal?.addEventListener('abort', () => reject(logoutSignal?.reason), { once: true });
        }),
    );
    vi.stubGlobal('fetch', fetchMock);
    const store = makeStore();
    store.dispatch(setCredentials(authAnswer('cleaner').body));

    const ending = endSession(store.dispatch);
    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledWith('/api/auth/logout', expect.any(Object)));
    expect(logoutSignal?.aborted).toBe(false);

    cancelPendingLogout();
    await ending;

    expect(logoutSignal?.aborted).toBe(true);
  });
});

describe('getValidAccessToken (facility-0023 item 3)', () => {
  function signedIn(token: string, msLeft: number) {
    const store = makeStore();
    const { body } = authAnswer('supervisor', token);
    store.dispatch(setCredentials({ ...body, expiresAt: new Date(Date.now() + msLeft).toISOString() }));
    return store;
  }

  it('a token with more than 30 s left is given as it is', async () => {
    const server = fakeServer({});
    const store = signedIn('token-live', 120_000);

    expect(await getValidAccessToken(store.getState, store.dispatch)).toBe('token-live');
    expect(server.calls).toHaveLength(0);
  });

  it('a token with 30 s or less left is refreshed first, once for every caller (facility-0015)', async () => {
    const server = fakeServer({ 'POST /api/auth/refresh': () => authAnswer('supervisor', 'token-new') });
    const store = signedIn('token-old', 20_000);

    const tokens = await Promise.all([
      getValidAccessToken(store.getState, store.dispatch),
      getValidAccessToken(store.getState, store.dispatch),
    ]);

    expect(tokens).toEqual(['token-new', 'token-new']);
    expect(server.calls.filter((c) => c.path === '/api/auth/refresh')).toHaveLength(1);
  });

  it('a refused refresh gives no token, and the session is over', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => ({ status: 401 }) });
    const store = signedIn('token-old', 1_000);

    expect(await getValidAccessToken(store.getState, store.dispatch)).toBe('');
    expect(store.getState().auth.currentUser).toBeNull();
  });
});
