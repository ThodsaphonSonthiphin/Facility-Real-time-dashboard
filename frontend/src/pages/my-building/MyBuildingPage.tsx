import { Check, Hourglass, RefreshCw, RotateCcw, type LucideIcon } from 'lucide-react';
import { useState } from 'react';
import { freshOnOpen } from '../../shared/api/baseApi';
import type { PointStatus } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { ConnectionBanner } from '../../shared/components/ConnectionBanner/ConnectionBanner';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { PhoneHeader, PhoneScreen } from '../../shared/components/PhoneScreen';
import { focusRing } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftLabel } from '../../shared/utils/labels';
import { BuildingCard } from './BuildingCard';
import { inspectionOrder } from './buildingRules';
import { useGetMyBuildingQuery, type MyBuilding } from './myBuildingApi';

type BuildingStatusFilter = 'PendingInspection' | 'Rework' | 'Passed' | null;

/**
 * Supervisor home screen (`/my-building`): interactive status filter summary cards, top-right refresh, top connection
 * banner when needed, and persistent 3-tab bottom navigation (`ตึกของฉัน`, `จัดตารางงาน`, `บัญชี`) via PhoneScreen.
 */
export function MyBuildingPage() {
  const { data, error, isLoading, isFetching, refetch } = useGetMyBuildingQuery(undefined, freshOnOpen);
  const [statusFilter, setStatusFilter] = useState<BuildingStatusFilter>(null);

  const toggleFilter = (next: Exclude<BuildingStatusFilter, null>) => {
    setStatusFilter((current) => (current === next ? null : next));
  };

  const header = (
    <PhoneHeader
      action={
        <Button variant="secondary" size="phone" disabled={isFetching} onClick={() => void refetch()}>
          <RefreshCw aria-hidden="true" className="size-5" />
          {isFetching ? 'กำลังอัปเดต...' : 'อัปเดต'}
        </Button>
      }
    >
      <h1 className="text-2xl font-bold text-text">ตึกของฉัน</h1>
      <span className="min-h-5 min-w-0 text-sm wrap-break-word text-text-2 tabular-nums">
        {data ? `${shiftLabel[data.shift]} · ${countLine(data)}` : ''}
      </span>
    </PhoneHeader>
  );

  return (
    <PhoneScreen header={header}>
      <ConnectionBanner refreshing={isFetching} onRetry={() => void refetch()} />
      {isLoading && <PageLoading lines={3}>กำลังโหลดจุด...</PageLoading>}
      {error && (
        <Note tone="error" retrySize="phone" onRetry={() => void refetch()}>
          {getErrorMessage(error)}
        </Note>
      )}
      {data && data.buildings.length > 0 && (
        <Summary building={data} selected={statusFilter} onToggle={toggleFilter} onReset={() => setStatusFilter(null)} />
      )}
      {data && <BuildingList building={data} filter={statusFilter} onReset={() => setStatusFilter(null)} />}
    </PhoneScreen>
  );
}

function countLine(building: MyBuilding): string {
  const points = building.buildings.flatMap((section) => section.points);
  const areas = new Set(points.map((point) => point.areaId));
  return `${areas.size} พื้นที่ · ${points.length} จุด`;
}

/** Interactive summary cards that filter the building point list on tap (Heuristics #6 & #7). */
function Summary({
  building,
  selected,
  onToggle,
  onReset,
}: {
  building: MyBuilding;
  selected: BuildingStatusFilter;
  onToggle: (status: Exclude<BuildingStatusFilter, null>) => void;
  onReset: () => void;
}) {
  const points = building.buildings.flatMap((section) => section.points);
  const count = (status: PointStatus) => points.filter((point) => point.status === status).length;
  return (
    <section aria-label="สรุปและกรองการตรวจ" className="flex flex-col gap-2">
      <ul className="m-0 grid list-none grid-cols-3 gap-2 p-0 text-sm" aria-label="สรุปการตรวจ">
        <FilterTile
          label="รอตรวจ"
          count={count('PendingInspection')}
          icon={Hourglass}
          pressed={selected === 'PendingInspection'}
          look="border-pending-border bg-pending-bg text-pending"
          onClick={() => onToggle('PendingInspection')}
        />
        <FilterTile
          label="รอแม่บ้านแก้"
          count={count('Rework')}
          icon={RotateCcw}
          pressed={selected === 'Rework'}
          look="border-border bg-surface text-rework"
          onClick={() => onToggle('Rework')}
        />
        <FilterTile
          label="ตรวจผ่าน"
          count={count('Passed')}
          icon={Check}
          pressed={selected === 'Passed'}
          look="border-border bg-surface text-passed"
          onClick={() => onToggle('Passed')}
        />
      </ul>
      {selected !== null && (
        <Button variant="link" size="phone" className="self-start" onClick={onReset}>
          {`แสดงทุกสถานะ (${points.length} จุด)`}
        </Button>
      )}
    </section>
  );
}

function FilterTile({
  label,
  count,
  icon: Icon,
  pressed,
  look,
  onClick,
}: {
  label: string;
  count: number;
  icon: LucideIcon;
  pressed: boolean;
  look: string;
  onClick: () => void;
}) {
  return (
    <li className="flex min-w-0">
      <button
        type="button"
        data-tap=""
        aria-pressed={pressed}
        onClick={onClick}
        className={`flex h-full min-h-16 w-full cursor-pointer flex-col-reverse items-start justify-between gap-1 rounded-lg border px-2.5 py-2 text-left transition-colors ${look} ${
          pressed ? 'ring-2 ring-accent ring-inset' : 'hover:border-accent'
        } ${focusRing}`}
      >
        <strong className="text-xl/tight font-bold text-text tabular-nums">{count}</strong>{' '}
        <span className="flex items-center gap-1 text-xs/snug font-semibold">
          <Icon aria-hidden="true" className="size-4 shrink-0" />
          <span className="min-w-0">{label}</span>
        </span>
      </button>
    </li>
  );
}

function BuildingList({
  building,
  filter,
  onReset,
}: {
  building: MyBuilding;
  filter: BuildingStatusFilter;
  onReset: () => void;
}) {
  if (building.buildings.length === 0) {
    return <Note tone="info">ยังไม่มีตึกให้ตรวจ แจ้งแอดมินให้ตรวจตึกที่คุณรับผิดชอบ</Note>;
  }
  const matchingTotal = building.buildings.reduce(
    (sum, section) => sum + section.points.filter((p) => !filter || p.status === filter).length,
    0,
  );
  if (filter && matchingTotal === 0) {
    return (
      <div className="flex flex-col items-start gap-2 rounded-lg border border-border bg-surface p-4">
        <p className="text-text-2">ไม่มีจุดในสถานะที่เลือกตอนนี้</p>
        <Button variant="secondary" size="phone" onClick={onReset}>
          แสดงทุกสถานะ
        </Button>
      </div>
    );
  }
  return (
    <>
      {building.buildings.map((section) => {
        const headingId = `building-${section.buildingId}`;
        const points = filter ? section.points.filter((point) => point.status === filter) : section.points;
        if (filter && points.length === 0) {
          return null;
        }
        return (
          <section key={section.buildingId} className="flex flex-col gap-2" aria-labelledby={headingId}>
            <h2 id={headingId} className="text-lg font-bold">
              {section.isCover ? `${section.buildingName} (ตรวจแทนกะนี้)` : section.buildingName}
            </h2>
            {!section.canInspect && <Note tone="warn">ตอนนี้ไม่ใช่กะของคุณ ดูได้ แต่สแกนตรวจไม่ได้</Note>}
            {points.length === 0 ? (
              <p className="text-text-2">ตึกนี้ยังไม่มีจุด</p>
            ) : (
              <ul className="m-0 flex list-none flex-col gap-2 p-0">
                {inspectionOrder(points).map((point) => (
                  <BuildingCard key={point.id} point={point} canInspect={section.canInspect} />
                ))}
              </ul>
            )}
          </section>
        );
      })}
    </>
  );
}
