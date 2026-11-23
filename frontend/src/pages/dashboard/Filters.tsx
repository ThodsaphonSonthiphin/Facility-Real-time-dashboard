import { RotateCcw, Search, Wrench } from 'lucide-react';
import { useGetBuildingsQuery } from '../../shared/api/adminSetupApi';
import { buildingOptionText } from '../../shared/api/buildingNames';
import type { PointCard } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { Select, TextInput } from '../../shared/components/Field';
import { focusRing, statusIcon, statusText, toggleClass } from '../../shared/components/styles';
import { pointStatusLabel } from '../../shared/utils/labels';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import {
  buildingChosen,
  filtersCleared,
  issueOnlyToggled,
  problemFirstToggled,
  searchChanged,
  statusToggled,
} from '../../store/slices/dashboardSlice';
import { inBuilding, legendOrder, statusCounts } from './boardRules';

// The inset ring adds the second pressed stroke without changing the card's size.
const cardClass =
  'flex h-full w-full cursor-pointer flex-col items-start justify-between gap-1 rounded-lg border border-border bg-surface px-3 py-2.5 text-left ' +
  'enabled:hover:border-accent aria-pressed:border-accent aria-pressed:bg-accent-subtle ' +
  'aria-pressed:inset-ring-1 aria-pressed:inset-ring-accent ' +
  focusRing;

/** Wireframe D1's filters: the status cards with counts, then the building and "มีปัญหาที่แจ้ง" (facility-0024). */
export function Filters({ points }: { points: PointCard[] }) {
  const filters = useAppSelector((state) => state.dashboard);
  const dispatch = useAppDispatch();
  const { data: buildings } = useGetBuildingsQuery();
  const shown = inBuilding(points, filters.buildingCode);
  const counts = statusCounts(shown);
  const issues = shown.filter((point) => point.issue).length;
  const hasActiveFilters = Boolean(filters.status || filters.buildingCode || filters.issueOnly || filters.search.trim());

  return (
    <>
      {/* Keep two tracks on narrow screens. At xl, the tighter gutters allow a 130 px track without wrapping the Thai labels. */}
      <ul className="m-0 grid list-none grid-cols-2 gap-3 p-0 md:grid-cols-[repeat(auto-fit,minmax(140px,1fr))] xl:grid-cols-[repeat(auto-fit,minmax(130px,1fr))]" aria-label="สถานะ">
        {legendOrder.map((status) => {
          const Icon = statusIcon[status];
          return (
            <li key={status}>
              <button
                type="button"
                data-tap=""
                className={cardClass}
                aria-pressed={filters.status === status}
                onClick={() => dispatch(statusToggled(status))}
              >
                <span className={`flex items-center gap-1.5 text-xs font-semibold ${statusText[status]}`}>
                  <Icon aria-hidden="true" className="size-4 shrink-0" strokeWidth={2} />
                  <span>{pointStatusLabel[status]}</span>
                </span>{' '}
                <span className="block text-2xl/tight font-bold text-text tabular-nums">{counts[status]}</span>
              </button>
            </li>
          );
        })}
      </ul>
      <section
        role="region"
        aria-label="ตัวกรองจุด"
        className="grid grid-cols-1 items-center gap-3 rounded-lg border border-border bg-surface p-4 sm:flex sm:flex-wrap"
      >
        <label className="inline-flex w-full min-w-0 items-center gap-2 font-semibold sm:w-auto">
          ตึก
          <Select
            size="adaptive"
            className="min-w-0 flex-1 font-normal text-ellipsis sm:w-56 sm:flex-none"
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
        <button
          type="button"
          data-tap=""
          className={`${toggleClass} w-full sm:w-auto`}
          aria-pressed={filters.issueOnly}
          onClick={() => dispatch(issueOnlyToggled())}
        >
          <Wrench aria-hidden="true" className="size-4 text-rework" />
          มีปัญหาที่แจ้ง <span className="font-bold tabular-nums">{issues}</span>
        </button>
        <label className="relative flex w-full min-w-0 items-center sm:flex-1 sm:basis-56">
          <Search aria-hidden="true" className="pointer-events-none absolute left-2.5 size-4 text-text-2" />
          <TextInput
            size="adaptive"
            type="search"
            className="w-full pl-8.5"
            aria-label="ค้นหา"
            placeholder="ค้นหาพื้นที่ จุด หรือชื่อแม่บ้าน"
            value={filters.search}
            onChange={(event) => dispatch(searchChanged(event.target.value))}
          />
        </label>
        <label className="inline-flex min-h-11 cursor-pointer items-center gap-2 sm:min-h-9">
          <input
            type="checkbox"
            className={`size-4.5 accent-accent ${focusRing}`}
            checked={filters.problemFirst}
            onChange={() => dispatch(problemFirstToggled())}
          />
          เรียงพื้นที่ที่มีปัญหาขึ้นก่อน
        </label>
        {hasActiveFilters && (
          <Button variant="link" size="adaptive" className="justify-self-start" onClick={() => dispatch(filtersCleared())}>
            <RotateCcw aria-hidden="true" className="size-4" />
            ล้างตัวกรอง
          </Button>
        )}
      </section>
    </>
  );
}
