import { Provider } from 'react-redux';
import { RouterProvider, type createBrowserRouter } from 'react-router';
import { SessionGate } from './shared/components/SessionGate';
import type { AppStore } from './store';

interface AppProps {
  store: AppStore;
  router: ReturnType<typeof createBrowserRouter>;
}

/** Providers only (facility-0026 item 2): the store, the session gate and the router. */
export function App({ store, router }: AppProps) {
  return (
    <Provider store={store}>
      <SessionGate>
        <RouterProvider router={router} />
      </SessionGate>
    </Provider>
  );
}
