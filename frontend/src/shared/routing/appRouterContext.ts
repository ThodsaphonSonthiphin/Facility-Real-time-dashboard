import { createContext } from 'react';
import type { createBrowserRouter } from 'react-router';

export const AppRouterContext = createContext<ReturnType<typeof createBrowserRouter> | null>(null);
