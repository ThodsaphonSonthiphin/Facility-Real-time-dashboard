import { useGetBuildingsQuery } from '../../shared/api/adminSetupApi';
import type { PointCard } from '../../shared/api/types';
import { pointStatusLabel } from '../../shared/utils/labels';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { buildingChosen, issueOnlyToggled, statusToggled } from '../../store/slices/dashboardSlice';
import { inBuilding, legendOrder, statusCounts } from './boardRules';

/** Wireframe D1's filter row: the building, the legend as status buttons with counts, and "มีปัญหาที่แจ้ง" (facility-0024). */
export function Filters({ points }: { points: PointCard[] }) {
  const filters = useAppSelector((state) => state.dashboard);
  const dispatch = useAppDispatch();
  const { data: buildings } = useGetBuildingsQuery();
  const shown = inBuilding(points, filters.buildingCode);
  const counts = statusCounts(shown);
  const issues = shown.filter((point) => point.issue).length;

  return (
    <div className="dashboard-filters">
      <label className="dashboard-filter">
        ตึก
        <select
          className="app-input"
          value={filters.buildingCode}
          onChange={(event) => dispatch(buildingChosen(event.target.value))}
        >
          <option value="">ทุกตึก</option>
          {(buildings ?? []).map((building) => (
            <option key={building.id} value={building.code}>
              {building.code}
            </option>
          ))}
        </select>
      </label>
      <ul className="dashboard-legend" aria-label="สถานะ">
        {legendOrder.map((status) => (
          <li key={status}>
            <button
              type="button"
              className="dashboard-legend__item"
              aria-pressed={filters.status === status}
              onClick={() => dispatch(statusToggled(status))}
            >
              <span className={`dashboard-dot dashboard-dot--${status}`} />
              {`${pointStatusLabel[status]} ${counts[status]}`}
            </button>
          </li>
        ))}
      </ul>
      <button
        type="button"
        className="dashboard-issue"
        aria-pressed={filters.issueOnly}
        onClick={() => dispatch(issueOnlyToggled())}
      >
        {`มีปัญหาที่แจ้ง ${issues}`}
      </button>
    </div>
  );
}
