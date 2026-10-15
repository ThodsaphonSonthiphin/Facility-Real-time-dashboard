import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter } from 'react-router';
import '@fontsource/ibm-plex-sans-thai/400.css';
import '@fontsource/ibm-plex-sans-thai/600.css';
import '@fontsource/ibm-plex-sans-thai/700.css';
import './index.css';
import { App } from './App';
import { routes } from './router';
import { startSession } from './shared/auth/session';
import { makeStore } from './store';

const store = makeStore();
// Outside React, so StrictMode's double effects can never send two refreshes (facility-0015)
void startSession(store.dispatch);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App store={store} router={createBrowserRouter(routes)} />
  </StrictMode>,
);
