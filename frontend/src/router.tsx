import type { RouteObject } from 'react-router';
import { AccountPage } from './pages/account';
import { AdminLoginPage, LoginPage } from './pages/auth';
import { CheckInPage } from './pages/check-in';
import { InspectPage } from './pages/inspect';
import { MyBuildingPage } from './pages/my-building';
import { MyWorkPage } from './pages/my-work';
import { OwnSchedulePage, ScheduleManagementPage } from './pages/schedules';
import { ScanPage } from './pages/scan';
import { AppLayout } from './shared/components/AppLayout';
import { Splash } from './shared/components/Boxes';
import { ByRole } from './shared/components/ByRole';
import { CrashScreen } from './shared/components/CrashScreen';
import { HomeRedirect } from './shared/components/HomeRedirect';
import { ProtectedRoute } from './shared/components/ProtectedRoute';
import { StatusScreen } from './shared/components/StatusScreen';

/**
 * The whole route table as data. main.tsx feeds it to createBrowserRouter, tests to createMemoryRouter.
 * See docs/adr/facility-0026-frontend-router-shape.md.
 * No loader or action: RTK Query owns data loading (facility-0022).
 *
 * Every Admin page is lazily loaded so mobile users never download Admin bundles.
 */
export const routes: RouteObject[] = [
  {
    path: '/',
    errorElement: <CrashScreen />,
    hydrateFallbackElement: <Splash>กำลังโหลด...</Splash>,
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
              { path: 'my-schedule', element: <OwnSchedulePage /> },
              { path: 'check-in/:token', element: <CheckInPage /> },
            ],
          },
        ],
      },
      {
        element: <ProtectedRoute roles={['cleaner', 'supervisor']} />,
        children: [
          {
            element: <AppLayout wideForSupervisor />,
            children: [{ path: 'my-account', element: <AccountPage /> }],
          },
          {
            element: <AppLayout />,
            children: [
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
        children: [
          {
            element: <AppLayout wide />,
            children: [
              { path: 'my-building', element: <MyBuildingPage /> },
              { path: 'schedule-management', element: <ScheduleManagementPage /> },
            ],
          },
        ],
      },
      {
        element: <ProtectedRoute roles={['admin']} />,
        children: [
          {
            element: <AppLayout wide />,
            children: [
              { path: 'admin/overview', lazy: async () => ({ Component: (await import('./pages/overview')).OverviewPage }) },
              { path: 'dashboard', lazy: async () => ({ Component: (await import('./pages/dashboard')).DashboardPage }) },
              {
                path: 'admin/attendance',
                lazy: async () => ({ Component: (await import('./pages/attendance')).AttendancePage }),
              },
              {
                path: 'admin/schedules',
                lazy: async () => ({ Component: (await import('./pages/schedules/AdminSchedulesPage')).AdminSchedulesPage }),
              },
              { path: 'admin/alerts', lazy: async () => ({ Component: (await import('./pages/alerts')).AlertsPage }) },
              {
                path: 'admin/inspection-summary',
                lazy: async () => ({ Component: (await import('./pages/inspection-summary')).InspectionSummaryPage }),
              },
              { path: 'admin/export', lazy: async () => ({ Component: (await import('./pages/export')).ExportPage }) },
              { path: 'admin/areas/:areaId?', lazy: async () => ({ Component: (await import('./pages/areas')).AreasPage }) },
              { path: 'admin/areas/new', lazy: async () => ({ Component: (await import('./pages/areas')).AreaFormPage }) },
              {
                path: 'admin/areas/:areaId/edit',
                lazy: async () => ({ Component: (await import('./pages/areas')).AreaFormPage }),
              },
              { path: 'admin/buildings', lazy: async () => ({ Component: (await import('./pages/buildings')).BuildingsPage }) },
              { path: 'admin/print', lazy: async () => ({ Component: (await import('./pages/print-signs')).PrintSignsPage }) },
              { path: 'admin/accounts', lazy: async () => ({ Component: (await import('./pages/accounts')).AccountsPage }) },
              { path: 'admin/history', lazy: async () => ({ Component: (await import('./pages/history')).HistoryPage }) },
            ],
          },
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
        element: (
          <StatusScreen
            title="ไม่พบหน้าที่ต้องการ"
            detail="ลิงก์นี้อาจถูกย้ายหรือพิมพ์ไม่ถูกต้อง กดปุ่มด้านล่างเพื่อกลับไปหน้าหลัก"
            linkTo="/"
            linkText="กลับหน้าแรก"
          />
        ),
      },
    ],
  },
];
