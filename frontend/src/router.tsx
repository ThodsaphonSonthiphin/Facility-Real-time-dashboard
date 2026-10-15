import type { RouteObject } from 'react-router';
import { AdminLoginPage, LoginPage } from './pages/auth';
import { CheckInPage } from './pages/check-in';
import { DashboardPage } from './pages/dashboard';
import { MyBuildingPage } from './pages/my-building';
import { MyWorkPage } from './pages/my-work';
import { ScanPage } from './pages/scan';
import { AppLayout } from './shared/components/AppLayout';
import { CrashScreen } from './shared/components/CrashScreen';
import { HomeRedirect } from './shared/components/HomeRedirect';
import { ProtectedRoute } from './shared/components/ProtectedRoute';
import { StatusScreen } from './shared/components/StatusScreen';

/**
 * facility-0026: the whole route table as data. main.tsx feeds it to createBrowserRouter, tests to createMemoryRouter.
 * No loader or action: RTK Query owns data loading (facility-0022).
 */
export const routes: RouteObject[] = [
  {
    path: '/',
    errorElement: <CrashScreen />,
    children: [
      { index: true, element: <HomeRedirect /> },
      { path: 'login', element: <LoginPage /> },
      { path: 'login/admin', element: <AdminLoginPage /> },
      {
        element: <ProtectedRoute roles={['cleaner']} />,
        children: [
          {
            element: <AppLayout />,
            children: [
              { path: 'my-work', element: <MyWorkPage /> },
              // Printed on each Area's check-in sign (plan 8 decision 1); only the sign carries the token (facility-0059)
              { path: 'check-in/:token', element: <CheckInPage /> },
              // Printed on each point's sign (facility-0026). Cleaner only until plan 9 adds the Supervisor's inspection
              { path: 'scan/:token', element: <ScanPage /> },
            ],
          },
        ],
      },
      {
        element: <ProtectedRoute roles={['supervisor']} />,
        children: [{ element: <AppLayout />, children: [{ path: 'my-building', element: <MyBuildingPage /> }] }],
      },
      {
        element: <ProtectedRoute roles={['admin']} />,
        children: [{ element: <AppLayout wide />, children: [{ path: 'dashboard', element: <DashboardPage /> }] }],
      },
      {
        path: '*',
        element: <StatusScreen title="ไม่พบหน้านี้" detail="ที่อยู่นี้ไม่มีในระบบ" linkTo="/" linkText="กลับหน้าแรก" />,
      },
    ],
  },
];
