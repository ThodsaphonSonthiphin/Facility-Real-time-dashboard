import type { RouteObject } from 'react-router';
import { AdminLoginPage, LoginPage } from './pages/auth';
import { CheckInPage } from './pages/check-in';
import { InspectPage } from './pages/inspect';
import { MyBuildingPage } from './pages/my-building';
import { MyWorkPage } from './pages/my-work';
import { ScanPage } from './pages/scan';
import { AppLayout } from './shared/components/AppLayout';
import { ByRole } from './shared/components/ByRole';
import { CrashScreen } from './shared/components/CrashScreen';
import { HomeRedirect } from './shared/components/HomeRedirect';
import { ProtectedRoute } from './shared/components/ProtectedRoute';
import { StatusScreen } from './shared/components/StatusScreen';

/**
 * facility-0026: the whole route table as data. main.tsx feeds it to createBrowserRouter, tests to createMemoryRouter.
 * No loader or action: RTK Query owns data loading (facility-0022).
 *
 * facility-0062 item 3, done before the budget ran out (owner, 2026-10-19): every Admin page is its own file, loaded
 * the first time an Admin opens it (`lazy`), so a phone at a sign never downloads Admin code. Phone pages stay in the
 * main file, so a scanned sign opens without a second download.
 */
export const routes: RouteObject[] = [
  {
    path: '/',
    errorElement: <CrashScreen />,
    // While the first page's file loads. A file that cannot load (a new build replaced it) shows the crash screen
    hydrateFallbackElement: <p className="app-splash">กำลังโหลด...</p>,
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
            ],
          },
        ],
      },
      {
        element: <ProtectedRoute roles={['cleaner', 'supervisor']} />,
        children: [
          {
            element: <AppLayout />,
            children: [
              // Printed on each point's sign (facility-0026): the Cleaner submits, the Supervisor inspects (facility-0029)
              {
                path: 'scan/:token',
                element: <ByRole pages={{ cleaner: <ScanPage />, supervisor: <InspectPage /> }} />,
              },
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
        children: [
          {
            element: <AppLayout wide />,
            children: [
              { path: 'dashboard', lazy: async () => ({ Component: (await import('./pages/dashboard')).DashboardPage }) },
              // facility-0027, 0050, 0051, wireframe D2: ?date=yyyy-MM-dd&shift=Day|Night&building=<id>
              {
                path: 'admin/attendance',
                lazy: async () => ({ Component: (await import('./pages/attendance')).AttendancePage }),
              },
              // facility-0035, 0041, 0048, 0051, wireframe D3
              { path: 'admin/alerts', lazy: async () => ({ Component: (await import('./pages/alerts')).AlertsPage }) },
              // facility-0045, wireframe D4; without an id the first Area is open
              { path: 'admin/areas/:areaId?', lazy: async () => ({ Component: (await import('./pages/areas')).AreasPage }) },
              // facility-0045, wireframe D10 ("new" outranks the id above)
              { path: 'admin/areas/new', lazy: async () => ({ Component: (await import('./pages/area-form')).AreaFormPage }) },
              {
                path: 'admin/areas/:areaId/edit',
                lazy: async () => ({ Component: (await import('./pages/area-form')).AreaFormPage }),
              },
              // facility-0021 item 6, wireframe D5: ?area=<id>[&sign=<id>] or ?area=all
              { path: 'admin/print', lazy: async () => ({ Component: (await import('./pages/print-signs')).PrintSignsPage }) },
              // facility-0020, 0056, wireframe D6
              { path: 'admin/accounts', lazy: async () => ({ Component: (await import('./pages/accounts')).AccountsPage }) },
            ],
          },
          // M17: the Admin's phone page at the sign (facility-0045 item 2), in the phone column
          {
            element: <AppLayout />,
            children: [
              {
                path: 'admin/areas/:areaId/capture',
                lazy: async () => ({ Component: (await import('./pages/capture')).CapturePage }),
              },
            ],
          },
        ],
      },
      {
        path: '*',
        element: <StatusScreen title="ไม่พบหน้านี้" detail="ที่อยู่นี้ไม่มีในระบบ" linkTo="/" linkText="กลับหน้าแรก" />,
      },
    ],
  },
];
