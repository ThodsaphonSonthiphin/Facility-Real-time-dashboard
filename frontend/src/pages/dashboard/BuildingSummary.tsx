import { ChevronRight } from 'lucide-react';
import { buildingText, useBuildingNames } from '../../shared/api/buildingNames';
import type { PointCard } from '../../shared/api/types';
import { Table } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { SurfacePanel } from '../../shared/components/SurfacePanel';
import { focusRing, tableCell } from '../../shared/components/styles';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { buildingChosen } from '../../store/slices/dashboardSlice';
import { statusCounts } from './boardRules';

interface BuildingRow {
  buildingCode: string;
  total: number;
  passed: number;
  problem: number;
  progress: number;
}

function summarizeBuildings(points: readonly PointCard[]): BuildingRow[] {
  const byBuilding = new Map<string, PointCard[]>();
  for (const point of points) {
    const list = byBuilding.get(point.buildingCode);
    if (list) {
      list.push(point);
    } else {
      byBuilding.set(point.buildingCode, [point]);
    }
  }
  return Array.from(byBuilding.entries()).map(([buildingCode, buildingPoints]) => {
    const counts = statusCounts(buildingPoints);
    const total = buildingPoints.length;
    const passed = counts.Passed;
    const problem = counts.Rework + counts.Overdue;
    const progress = total > 0 ? Math.round((passed / total) * 100) : 0;
    return { buildingCode, total, passed, problem, progress };
  });
}

/**
 * Per-building progress summary derived solely from live `PointCard[]` and building names.
 * Uses `<button type="button">` (never `<a>`) to filter the dashboard by building.
 */
export function BuildingSummary({ points }: { points: readonly PointCard[] }) {
  const names = useBuildingNames();
  const selectedBuilding = useAppSelector((state) => state.dashboard.buildingCode);
  const dispatch = useAppDispatch();
  const rows = summarizeBuildings(points);

  if (rows.length === 0) {
    return null;
  }

  return (
    <SurfacePanel
      title="สถานะพื้นที่ (ตามตึก)"
      className="shadow-xs"
      actions={
        selectedBuilding !== '' ? (
          <Button variant="link" size="desk" onClick={() => dispatch(buildingChosen(''))}>
            แสดงทุกตึก
          </Button>
        ) : undefined
      }
    >
      <Table label="สถานะพื้นที่ (ตามตึก)" framed={false} minWidth="min-w-160">
        <thead>
          <tr>
            <th scope="col" className="min-w-44 pl-4">
              ตึก
            </th>
            <th scope="col" className={tableCell.num}>
              จุดทั้งหมด
            </th>
            <th scope="col" className={tableCell.num}>
              ตรวจผ่าน
            </th>
            <th scope="col" className={tableCell.num}>
              มีปัญหา
            </th>
            <th scope="col" className="w-60 min-w-48">
              ความคืบหน้า
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => {
            const isSelected = selectedBuilding === row.buildingCode;
            return (
              <tr key={row.buildingCode} className={isSelected ? '[&>*]:bg-accent-subtle/50' : undefined}>
                <th scope="row" className="py-1.5">
                  <button
                    type="button"
                    data-tap=""
                    aria-pressed={isSelected}
                    onClick={() => dispatch(buildingChosen(isSelected ? '' : row.buildingCode))}
                    className={`inline-flex min-h-9 cursor-pointer items-center gap-1.5 rounded-md px-1 py-1 text-left font-semibold text-accent hover:bg-accent-subtle aria-pressed:bg-accent-subtle aria-pressed:ring-1 aria-pressed:ring-accent ${focusRing}`}
                  >
                    <span>{buildingText(row.buildingCode, names)}</span>
                    <ChevronRight aria-hidden="true" className="size-4 shrink-0" />
                  </button>
                </th>
                <td className={tableCell.num}>{row.total}</td>
                <td className={`${tableCell.num} font-semibold text-passed`}>{row.passed}</td>
                <td className={`${tableCell.num} ${row.problem > 0 ? 'font-semibold text-rework' : 'text-text-2'}`}>
                  {row.problem}
                </td>
                <td>
                  <div className="flex items-center gap-2">
                    <span aria-hidden="true" className="w-10 shrink-0 text-right text-xs font-bold text-text tabular-nums">
                      {row.progress}%
                    </span>
                    <div
                      role="progressbar"
                      aria-label={`ความคืบหน้าตึก ${row.buildingCode}`}
                      aria-valuemin={0}
                      aria-valuemax={100}
                      aria-valuenow={row.progress}
                      className="h-2 flex-1 overflow-hidden rounded-full bg-skeleton"
                    >
                      <div className="h-full rounded-full bg-accent" style={{ width: `${row.progress}%` }} />
                    </div>
                  </div>
                </td>
              </tr>
            );
          })}
        </tbody>
      </Table>
    </SurfacePanel>
  );
}
