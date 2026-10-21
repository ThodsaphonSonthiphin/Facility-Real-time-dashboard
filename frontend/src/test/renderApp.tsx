import { act, render, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, type RouteObject } from 'react-router';
import { App } from '../App';
import { routes as appRoutes } from '../router';
import { startSession } from '../shared/auth/session';
import { makeStore } from '../store';

/**
 * Renders the whole app at `url` the way main.tsx does: a fresh store, the page-load refresh, then the router
 * (regression-net #32: only fetch and SignalR are faked). Call fakeServer first; it must answer POST /api/auth/refresh.
 * Admin pages are lazy routes (facility-0062 item 3), so it also waits until the router has loaded the first page and React has committed it.
 */
export async function renderApp(url: string, routes: RouteObject[] = appRoutes) {
  const store = makeStore();
  const router = createMemoryRouter(routes, { initialEntries: [url] });
  const user = userEvent.setup();
  render(<App store={store} router={router} />);
  await act(() => startSession(store.dispatch));
  await waitFor(() => {
    if (!router.state.initialized) {
      throw new Error('the router is still loading the first page');
    }
  });
  // RouterProvider applies the loaded page inside React.startTransition; flush it so the page is committed
  await act(async () => {});
  return { store, router, user };
}
