import { Provider } from 'react-redux';
import { RouterProvider, type createBrowserRouter } from 'react-router';
import { SessionGate } from './shared/components/SessionGate';
import type { AppStore } from './store';

interface AppProps {
  store: AppStore;
  router: ReturnType<typeof createBrowserRouter>;
}

/** Store, session gate and router providers. See docs/adr/facility-0026-frontend-router-shape.md, item 2. */
export function App({ store, router }: AppProps) {
  return (
    <Provider store={store}>
      <SessionGate>
        <RouterProvider router={router} />
      </SessionGate>
    </Provider>
  );
}
