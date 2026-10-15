import { describe, expect, it } from 'vitest';
import { makeStore } from '../../store';
import { setCredentials } from '../../store/slices/authSlice';
import { authAnswer, fakeServer } from '../../test/fakeServer';
import { baseApi } from './baseApi';

// Throwaway endpoints, only for these tests: the real ones belong to the pages.
const probeApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    probe: build.query<{ ok: boolean }, void>({ query: () => '/probe' }),
    probeTwo: build.query<{ ok: boolean }, void>({ query: () => '/probe-two' }),
    probeLogin: build.mutation<unknown, void>({ query: () => ({ url: '/auth/login', method: 'POST', body: {} }) }),
  }),
});

function loggedInStore() {
  const store = makeStore();
  store.dispatch(setCredentials(authAnswer('cleaner', 'old-token').body));
  return store;
}

describe('baseQueryWithReauth', () => {
  it('sends the access token as a Bearer header', async () => {
    const server = fakeServer({ 'GET /api/probe': () => ({ status: 200, body: { ok: true } }) });
    const store = loggedInStore();

    await store.dispatch(probeApi.endpoints.probe.initiate());

    expect(server.calls[0].authorization).toBe('Bearer old-token');
  });

  it('on 401 refreshes once, then retries with the new token', async () => {
    const server = fakeServer({
      'GET /api/probe': (request) =>
        request.authorization === 'Bearer new-token' ? { status: 200, body: { ok: true } } : { status: 401 },
      'POST /api/auth/refresh': () => authAnswer('cleaner', 'new-token'),
    });
    const store = loggedInStore();

    const result = await store.dispatch(probeApi.endpoints.probe.initiate());

    expect(result.data).toEqual({ ok: true });
    expect(server.calls.map((c) => `${c.method} ${c.path}`)).toEqual([
      'GET /api/probe',
      'POST /api/auth/refresh',
      'GET /api/probe',
    ]);
    expect(store.getState().auth.accessToken).toBe('new-token');
  });

  it('two requests failing together share one refresh (facility-0015)', async () => {
    const answer = (request: { authorization: string | null }) =>
      request.authorization === 'Bearer new-token' ? { status: 200, body: { ok: true } } : { status: 401 };
    const server = fakeServer({
      'GET /api/probe': answer,
      'GET /api/probe-two': answer,
      'POST /api/auth/refresh': () => authAnswer('cleaner', 'new-token'),
    });
    const store = loggedInStore();

    const [one, two] = await Promise.all([
      store.dispatch(probeApi.endpoints.probe.initiate()),
      store.dispatch(probeApi.endpoints.probeTwo.initiate()),
    ]);

    expect(one.data).toEqual({ ok: true });
    expect(two.data).toEqual({ ok: true });
    expect(server.calls.filter((c) => c.path === '/api/auth/refresh')).toHaveLength(1);
  });

  it('logs out when the refresh is rejected, and does not retry', async () => {
    const server = fakeServer({
      'GET /api/probe': () => ({ status: 401 }),
      'POST /api/auth/refresh': () => ({ status: 401 }),
    });
    const store = loggedInStore();

    const result = await store.dispatch(probeApi.endpoints.probe.initiate());

    expect(result.error).toMatchObject({ status: 401 });
    expect(store.getState().auth.currentUser).toBeNull();
    expect(server.calls.filter((c) => c.path === '/api/probe')).toHaveLength(1);
  });

  it('a 401 from login is a wrong secret, not an expired token: no refresh', async () => {
    const server = fakeServer({
      'POST /api/auth/login': () => ({ status: 401 }),
      'POST /api/auth/refresh': () => authAnswer('cleaner', 'new-token'),
    });
    const store = makeStore();

    await store.dispatch(probeApi.endpoints.probeLogin.initiate());

    expect(server.calls.map((c) => c.path)).toEqual(['/api/auth/login']);
  });
});
