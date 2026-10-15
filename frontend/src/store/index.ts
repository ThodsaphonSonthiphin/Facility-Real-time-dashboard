import { combineReducers, configureStore, createListenerMiddleware, isAnyOf } from '@reduxjs/toolkit';
import { baseApi } from '../shared/api/baseApi';
import { authReducer, sessionExpired, signedOut } from './slices/authSlice';

const rootReducer = combineReducers({
  auth: authReducer,
  [baseApi.reducerPath]: baseApi.reducer,
});

/** One store per page load; tests make a fresh one per test. */
export function makeStore() {
  // facility-0023 item 2 / plan decision 6: whenever a session ends, the next account never sees the last one's answers.
  const sessionEnd = createListenerMiddleware();
  sessionEnd.startListening({
    matcher: isAnyOf(sessionExpired, signedOut),
    effect: (_action, api) => {
      api.dispatch(baseApi.util.resetApiState());
    },
  });
  return configureStore({
    reducer: rootReducer,
    middleware: (getDefault) => getDefault().prepend(sessionEnd.middleware).concat(baseApi.middleware),
  });
}

export type RootState = ReturnType<typeof rootReducer>;
export type AppStore = ReturnType<typeof makeStore>;
export type AppDispatch = AppStore['dispatch'];
