import { vi } from 'vitest';

export interface FakeRequest {
  method: string;
  path: string;
  search: string;
  body: unknown;
  authorization: string | null;
}

export interface FakeResponse {
  status: number;
  body?: unknown;
  /** A file answer (the export, D8): sent as is with these headers instead of a JSON body. */
  file?: { content: string; type: string; disposition?: string };
}

/** Key is "METHOD /path", e.g. "POST /api/auth/login". The handler may answer differently on each call. */
export type FakeRoutes = Record<string, (request: FakeRequest) => FakeResponse | Promise<FakeResponse>>;

/**
 * Replaces the global fetch for one test (the only thing faked besides SignalR, per regression-net #32).
 * Every call is recorded in `calls`. A route with no handler answers 404 with a message naming it, so a missing
 * fake shows up in the test output instead of hanging. A handler that never answers stands for a server that never
 * answers: like a browser, the request then ends only when its signal aborts (a send timeout, facility-0052).
 * test/setup.ts restores the real fetch after each test.
 */
export function fakeServer(routes: FakeRoutes) {
  const calls: FakeRequest[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = input instanceof Request ? input : new Request(new URL(String(input), 'http://localhost'), init);
    const url = new URL(request.url, 'http://localhost');
    const text = await request.text();
    const fake: FakeRequest = {
      method: request.method,
      path: url.pathname,
      search: url.search,
      body: text ? JSON.parse(text) : undefined,
      authorization: request.headers.get('Authorization'),
    };
    calls.push(fake);
    const handler = routes[`${fake.method} ${fake.path}`];
    const answer = handler
      ? await unlessAborted(request.signal, handler(fake))
      : { status: 404, body: { message: `no fake for ${fake.method} ${fake.path}` } };
    if (answer.file) {
      const headers: Record<string, string> = { 'Content-Type': answer.file.type };
      if (answer.file.disposition) {
        headers['Content-Disposition'] = answer.file.disposition;
      }
      return new Response(answer.file.content, { status: answer.status, headers });
    }
    const hasBody = answer.body !== undefined && answer.status !== 204;
    return new Response(hasBody ? JSON.stringify(answer.body) : null, {
      status: answer.status,
      headers: hasBody ? { 'Content-Type': 'application/json' } : undefined,
    });
  });
  vi.stubGlobal('fetch', fetchMock);
  return { calls };
}

/** A browser's fetch rejects with the signal's reason when the request is aborted, answer or not. */
function unlessAborted(signal: AbortSignal, answer: FakeResponse | Promise<FakeResponse>): Promise<FakeResponse> {
  return new Promise((resolve, reject) => {
    if (signal.aborted) {
      reject(signal.reason);
      return;
    }
    signal.addEventListener('abort', () => reject(signal.reason), { once: true });
    Promise.resolve(answer).then(resolve, reject);
  });
}

/** A login or refresh answer for a made-up account (public repo: no real names). */
export function authAnswer(role: 'cleaner' | 'supervisor' | 'admin', token = `token-${role}`) {
  const names = { cleaner: 'แม่บ้านทดสอบ', supervisor: 'หัวหน้าทดสอบ', admin: 'ผู้ดูแลทดสอบ' };
  return {
    status: 200,
    body: {
      accessToken: token,
      expiresAt: new Date(Date.now() + 5 * 60_000).toISOString(),
      user: { id: 7, username: role === 'admin' ? 'admin' : '650001', fullName: names[role], role },
    },
  };
}
