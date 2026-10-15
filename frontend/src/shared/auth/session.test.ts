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
});

describe('refreshSession', () => {
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

  it('still forgets the session here when the logout call cannot reach the server', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('offline')));
    const store = makeStore();
    store.dispatch(setCredentials(authAnswer('cleaner').body));

    await endSession(store.dispatch);

    expect(store.getState().auth.currentUser).toBeNull();
  });
});
