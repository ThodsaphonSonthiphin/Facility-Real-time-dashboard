import { TriangleAlert } from 'lucide-react';
import { useGetBuildingsQuery } from '../../shared/api/adminSetupApi';
import { buildingOptionText } from '../../shared/api/buildingNames';
import type { PointCard } from '../../shared/api/types';
import { Select } from '../../shared/components/Field';
import { statusBg, toggleClass } from '../../shared/components/styles';
import { pointStatusLabel } from '../../shared/utils/labels';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { buildingChosen, issueOnlyToggled, statusToggled } from '../../store/slices/dashboardSlice';
import { inBuilding, legendOrder, statusCounts } from './boardRules';

const countClass = 'font-bold tabular-nums';

/** Wireframe D1's filter row: the building, the legend as status buttons with counts, and "มีปัญหาที่แจ้ง" (facility-0024). */
export function Filters({ points }: { points: PointCard[] }) {
  const filters = useAppSelector((state) => state.dashboard);
  const dispatch = useAppDispatch();
  const { data: buildings } = useGetBuildingsQuery();
  const shown = inBuilding(points, filters.buildingCode);
  const counts = statusCounts(shown);
  const issues = shown.filter((point) => point.issue).length;

  return (
    <div className="flex flex-wrap items-center gap-2">
      <label className="inline-flex items-center gap-1.5">
        ตึก
        <Select
          size="desk"
          value={filters.buildingCode}
          onChange={(event) => dispatch(buildingChosen(event.target.value))}
        >
          <option value="">ทุกตึก</option>
          {(buildings ?? []).map((building) => (
            <option key={building.id} value={building.code}>
              {buildingOptionText(building)}
            </option>
          ))}
        </Select>
      </label>
      <ul className="m-0 flex list-none flex-wrap gap-2 p-0" aria-label="สถานะ">
        {legendOrder.map((status) => (
          <li key={status}>
            <button
              type="button"
              data-tap=""
              className={toggleClass}
              aria-pressed={filters.status === status}
              onClick={() => dispatch(statusToggled(status))}
            >
              <span aria-hidden="true" className={`size-2.5 shrink-0 rounded-full ${statusBg[status]}`} />
              {pointStatusLabel[status]} <span className={countClass}>{counts[status]}</span>
            </button>
          </li>
        ))}
      </ul>
      <button
        type="button"
        data-tap=""
        className={toggleClass}
        aria-pressed={filters.issueOnly}
        onClick={() => dispatch(issueOnlyToggled())}
      >
        <TriangleAlert aria-hidden="true" className="size-4" />
        มีปัญหาที่แจ้ง <span className={countClass}>{issues}</span>
      </button>
    </div>
  );
}
