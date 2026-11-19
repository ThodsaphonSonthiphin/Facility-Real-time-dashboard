import { OverviewAreaPage } from '../../shared/overview/OverviewAreaPage/OverviewAreaPage';
import { useOverviewSource } from '../../shared/overview/useOverviewSource';
import { LegacyDashboardPage } from './LegacyDashboardPage/LegacyDashboardPage';

export function DashboardPage() {
  const fromOverview = useOverviewSource();
  return fromOverview ? <OverviewAreaPage view="cleaning" /> : <LegacyDashboardPage />;
}
