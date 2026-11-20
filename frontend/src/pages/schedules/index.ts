/**
 * The phone pages' entries (facility-0176, 0177). The Admin's page has its own module (`AdminSchedulesPage.tsx`) so its
 * lazy route does not import this barrel, which the phone pages hold from the first screen.
 */
export { OwnSchedulePage, ScheduleManagementPage } from './SchedulePage/SchedulePage';
