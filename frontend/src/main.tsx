import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter } from 'react-router';
import { setupListeners } from '@reduxjs/toolkit/query';
import './index.css';
import { App } from './App';
import { routes } from './router';
import { startSession } from './shared/auth/session';
import { installTapFeedback } from './shared/utils/tapFeedback';
import { makeStore } from './store';

const store = makeStore();
// facility-0061 item 2: refetchOnFocus needs the store to hear the window's focus
setupListeners(store.dispatch);
// Outside React, so StrictMode's double effects can never send two refreshes (facility-0015)
void startSession(store.dispatch);
// Every tap on a button-styled element shows, however short (owner, 2026-10-21)
installTapFeedback(document);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App store={store} router={createBrowserRouter(routes)} />
  </StrictMode>,
);
