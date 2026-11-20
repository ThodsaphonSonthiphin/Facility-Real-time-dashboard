import { OverviewAreaPage } from '../../shared/overview/OverviewAreaPage/OverviewAreaPage';
import { useOverviewSource } from '../../shared/overview/useOverviewSource';
import { LegacyAttendancePage } from './LegacyAttendancePage/LegacyAttendancePage';
import { ScopedAttendanceActions } from './OverviewAttendanceActions/OverviewAttendanceActions';

export function AttendancePage() {
  const fromOverview = useOverviewSource();
  return fromOverview
    ? <OverviewAreaPage view="attendance" actions={<ScopedAttendanceActions />} />
    : <LegacyAttendancePage />;
}
