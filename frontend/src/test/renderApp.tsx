import { act, render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, type RouteObject } from 'react-router';
import { App } from '../App';
import { routes as appRoutes } from '../router';
import { startSession } from '../shared/auth/session';
import { makeStore } from '../store';

/**
 * Renders the whole app at `url` the way main.tsx does: a fresh store, the page-load refresh, then the router
 * (regression-net #32: only fetch and SignalR are faked). Call fakeServer first; it must answer POST /api/auth/refresh.
 */
export async function renderApp(url: string, routes: RouteObject[] = appRoutes) {
  const store = makeStore();
  const router = createMemoryRouter(routes, { initialEntries: [url] });
  const user = userEvent.setup();
  render(<App store={store} router={router} />);
  await act(() => startSession(store.dispatch));
  return { store, router, user };
}
