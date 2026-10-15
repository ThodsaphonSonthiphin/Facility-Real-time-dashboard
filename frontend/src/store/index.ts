import { combineReducers, configureStore } from '@reduxjs/toolkit';
import { baseApi } from '../shared/api/baseApi';
import { authReducer } from './slices/authSlice';

const rootReducer = combineReducers({
  auth: authReducer,
  [baseApi.reducerPath]: baseApi.reducer,
});

/** One store per page load; tests make a fresh one per test. */
export function makeStore() {
  return configureStore({
    reducer: rootReducer,
    middleware: (getDefault) => getDefault().concat(baseApi.middleware),
  });
}

export type RootState = ReturnType<typeof rootReducer>;
export type AppStore = ReturnType<typeof makeStore>;
export type AppDispatch = AppStore['dispatch'];
