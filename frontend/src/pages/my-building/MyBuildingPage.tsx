import { Check, Hourglass, RefreshCw, RotateCcw, type LucideIcon } from 'lucide-react';
import { freshOnOpen } from '../../shared/api/baseApi';
import type { PointStatus } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { LogoutButton } from '../../shared/components/LogoutButton';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { PhoneHeader, PhoneScreen } from '../../shared/components/PhoneScreen';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftLabel } from '../../shared/utils/labels';
import { useAppSelector } from '../../store/hooks';
import { selectHubStatus, type HubStatus } from '../../store/slices/hubSlice';
import { BuildingCard } from './BuildingCard';
import { inspectionOrder } from './buildingRules';
import { MY_BUILDING_HUB, useGetMyBuildingQuery, type MyBuilding } from './myBuildingApi';

/** Wireframe M15's footnote "หน้านี้อัปเดตเองเมื่อแม่บ้านส่งงาน", by the hub's state (plan 9 decision 9). */
const liveLine: Record<HubStatus, string> = {
  connecting: 'กำลังเชื่อมต่อการอัปเดตสด...',
  live: 'หน้านี้อัปเดตเองเมื่อแม่บ้านส่งงานหรือหัวหน้าตรวจ',
  reconnecting: 'การอัปเดตสดหลุด กำลังต่อใหม่ กด "อัปเดต" เพื่อดูงานล่าสุด',
};

/**
 * facility-0057 item 1, wireframe M15: every point of the Supervisor's building, and of buildings they cover this
 * shift, what waits for them first. It only shows the way: inspecting needs the point's QR (facility-0059), so no card
 * is a link. The header shows the Supervisor's own name, as the design file's SupHome.
 */
export function MyBuildingPage() {
  const { data, error, isLoading, isFetching, refetch } = useGetMyBuildingQuery(undefined, freshOnOpen);
  const hubStatus = useAppSelector(selectHubStatus(MY_BUILDING_HUB));
  const user = useAppSelector((state) => state.auth.currentUser);

  // The header holds who, the shift and the three counts (the design file's SupHome); the name is the user's own, so
  // it is there before the answer, and the lines wait as tall as their words so nothing under them moves
  const header = (
    <PhoneHeader>
      <h1 className="sr-only">ตึกของฉัน</h1>
      <span className="text-lg font-bold">{user?.fullName}</span>
      <span className="min-h-6 text-sm text-text-2">{data ? `ตอนนี้ ${shiftLabel[data.shift]}` : ''}</span>
      <span className="min-h-6 text-sm text-text-2">{data ? countLine(data) : ''}</span>
      {data && data.buildings.length > 0 && <Summary building={data} />}
    </PhoneHeader>
  );

  return (
    <PhoneScreen header={header}>
      {isLoading && <PageLoading lines={3}>กำลังโหลดจุด...</PageLoading>}
      {error && <Note tone="error">{getErrorMessage(error)}</Note>}
      {data && <BuildingList building={data} />}
      <footer className="flex flex-col gap-2.5 border-t border-border pt-3">
        <span className="text-sm text-text-2">{liveLine[hubStatus]}</span>
        <Button className="w-full" disabled={isFetching} onClick={() => void refetch()}>
          <RefreshCw aria-hidden="true" className="size-5" />
          {isFetching ? 'กำลังอัปเดต...' : 'อัปเดต'}
        </Button>
        <LogoutButton />
      </footer>
    </PhoneScreen>
  );
}

function countLine(building: MyBuilding): string {
  const points = building.buildings.flatMap((section) => section.points);
  const areas = new Set(points.map((point) => point.areaId));
  return `${areas.size} พื้นที่ · ${points.length} จุด`;
}

/** The three counts in the header (รอตรวจ in its own cyan so what waits for the Supervisor stands out, #76); every count says its words. */
function Summary({ building }: { building: MyBuilding }) {
  const points = building.buildings.flatMap((section) => section.points);
  const count = (status: PointStatus) => points.filter((point) => point.status === status).length;
  return (
    <ul className="m-0 mt-2 grid list-none grid-cols-3 gap-2 p-0 text-sm" aria-label="สรุปการตรวจ">
      <Tile
        label="รอตรวจ"
        count={count('PendingInspection')}
        icon={Hourglass}
        look="border-pending-border bg-pending-bg text-pending"
      />
      <Tile label="รอแม่บ้านแก้" count={count('Rework')} icon={RotateCcw} look="border-border bg-surface text-rework" />
      <Tile label="ตรวจแล้ว" count={count('Passed')} icon={Check} look="border-border bg-surface text-passed" />
    </ul>
  );
}

/** One count. The number comes first in the markup (read as "2 รอตรวจ") and is drawn under its label. */
function Tile({ label, count, icon: Icon, look }: { label: string; count: number; icon: LucideIcon; look: string }) {
  return (
    <li className={`flex flex-col-reverse rounded-md border px-2.5 py-2 ${look}`}>
      <strong className="text-2xl leading-tight font-bold text-text tabular-nums">{count}</strong>{' '}
      <span className="flex items-center gap-1 font-semibold">
        <Icon aria-hidden="true" className="size-4 shrink-0" />
        {label}
      </span>
    </li>
  );
}

function BuildingList({ building }: { building: MyBuilding }) {
  if (building.buildings.length === 0) {
    return <Note tone="info">บัญชีนี้ยังไม่ได้ผูกกับตึก หรือตึกปิดใช้งาน แจ้งแอดมิน</Note>;
  }
  return (
    <>
      {building.buildings.map((section) => {
        const headingId = `building-${section.buildingId}`;
        return (
          <section key={section.buildingId} className="flex flex-col gap-2" aria-labelledby={headingId}>
            <h2 id={headingId} className="text-base font-bold">
              {section.isCover ? `${section.buildingName} (ตรวจแทนกะนี้)` : section.buildingName}
            </h2>
            {!section.canInspect && <Note tone="warn">ตอนนี้ไม่ใช่กะของคุณ ดูได้ แต่สแกนตรวจไม่ได้</Note>}
            {section.points.length === 0 ? (
              <p className="text-sm text-text-2">ตึกนี้ยังไม่มีจุด</p>
            ) : (
              <ul className="m-0 flex list-none flex-col gap-2 p-0">
                {inspectionOrder(section.points).map((point) => (
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
