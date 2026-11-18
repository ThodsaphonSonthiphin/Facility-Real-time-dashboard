import { AdminSchedulesPage as AdminWeek } from './SchedulePage/SchedulePage';

/**
 * The Admin's timetable (facility-0178), behind its own module so its route can load it lazily (facility-0062 item 3) and
 * a phone never downloads Admin page code. Tasks 14–15 add the Admin's editors here, not to the read-only table the phone
 * pages share.
 */
export function AdminSchedulesPage() {
  return <AdminWeek />;
}
