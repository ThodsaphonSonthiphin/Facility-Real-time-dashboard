import { describe, expect, it } from 'vitest';
import { fakeServer } from './fakeServer';

describe('fakeServer', () => {
  it('answers a faked route and records the call', async () => {
    const server = fakeServer({ 'POST /api/things': (request) => ({ status: 201, body: { echo: request.body } }) });

    const response = await fetch('/api/things?x=1', {
      method: 'POST',
      headers: { Authorization: 'Bearer t', 'Content-Type': 'application/json' },
      body: JSON.stringify({ a: 1 }),
    });

    expect(response.status).toBe(201);
    expect(await response.json()).toEqual({ echo: { a: 1 } });
    expect(server.calls).toEqual([
      { method: 'POST', path: '/api/things', search: '?x=1', body: { a: 1 }, authorization: 'Bearer t' },
    ]);
  });

  it('a 204 has no body', async () => {
    fakeServer({ 'POST /api/auth/logout': () => ({ status: 204 }) });

    const response = await fetch('/api/auth/logout', { method: 'POST' });

    expect(response.status).toBe(204);
    expect(await response.text()).toBe('');
  });

  it('answers 404 naming a route that has no fake', async () => {
    fakeServer({});

    const response = await fetch('/api/missing');

    expect(response.status).toBe(404);
    expect(await response.json()).toEqual({ message: 'no fake for GET /api/missing' });
  });
});
