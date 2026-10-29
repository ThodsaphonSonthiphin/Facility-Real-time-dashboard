import { RefreshCw } from 'lucide-react';
import { freshOnOpen } from '../../shared/api/baseApi';
import type { PointStatus } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { Note } from '../../shared/components/Note';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftLabel } from '../../shared/utils/labels';
import { useAppSelector } from '../../store/hooks';
import { selectHubStatus, type HubStatus } from '../../store/slices/hubSlice';
import { BuildingCard } from './BuildingCard';
import { inspectionOrder, statusFrame } from './buildingRules';
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
 * is a link. The name is not shown: the bar above already shows it.
 */
export function MyBuildingPage() {
  const { data, error, isLoading, isFetching, refetch } = useGetMyBuildingQuery(undefined, freshOnOpen);
  const hubStatus = useAppSelector(selectHubStatus(MY_BUILDING_HUB));

  return (
    <main className="flex flex-col gap-4 text-base text-text">
      <header className="flex flex-col gap-1">
        {data && <span className="text-sm tracking-wide text-text-2">{`ตอนนี้ ${shiftLabel[data.shift]}`}</span>}
        <h1 className="text-2xl font-bold">ตึกของฉัน</h1>
        {data && <p className="text-sm text-text-2">{countLine(data)}</p>}
      </header>
      {isLoading && <p className="text-sm text-text-2">กำลังโหลดจุด...</p>}
      {error && <Note tone="error">{getErrorMessage(error)}</Note>}
      {data && <BuildingList building={data} />}
      <footer className="flex flex-col gap-2.5 border-t border-border pt-3">
        <span className="text-sm text-text-2">{liveLine[hubStatus]}</span>
        <Button className="w-full" disabled={isFetching} onClick={() => void refetch()}>
          <RefreshCw aria-hidden="true" className="size-5" />
          {isFetching ? 'กำลังอัปเดต...' : 'อัปเดต'}
        </Button>
      </footer>
    </main>
  );
}

function countLine(building: MyBuilding): string {
  const points = building.buildings.flatMap((section) => section.points);
  const areas = new Set(points.map((point) => point.areaId));
  return `${areas.size} พื้นที่ · ${points.length} จุด`;
}

function BuildingList({ building }: { building: MyBuilding }) {
  if (building.buildings.length === 0) {
    return <Note tone="info">บัญชีนี้ยังไม่ได้ผูกกับตึก หรือตึกปิดใช้งาน แจ้งแอดมิน</Note>;
  }
  const points = building.buildings.flatMap((section) => section.points);
  const count = (status: PointStatus) => points.filter((point) => point.status === status).length;

  return (
    <>
      {/* รอตรวจ in its own cyan so what waits for the Supervisor stands out (#76); every count says its words */}
      <ul className="m-0 grid list-none grid-cols-3 gap-2 p-0 text-center text-sm" aria-label="สรุปการตรวจ">
        <li className={`${tileClass} ${statusFrame.PendingInspection} text-pending`}>
          <strong className={countClass}>{count('PendingInspection')}</strong> รอตรวจ
        </li>
        <li className={`${tileClass} ${statusFrame.Rework} text-rework`}>
          <strong className={countClass}>{count('Rework')}</strong> รอแม่บ้านแก้
        </li>
        <li className={`${tileClass} border border-border`}>
          <strong className={countClass}>{count('Passed')}</strong> ตรวจแล้ว
        </li>
      </ul>
      {building.buildings.map((section) => {
        const headingId = `building-${section.buildingId}`;
        return (
          <section key={section.buildingId} className="flex flex-col gap-2" aria-labelledby={headingId}>
            <h2 id={headingId} className="text-sm font-bold tracking-wide text-text-2">
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

// The base of one summary count; each tile adds its own border
const tileClass = 'flex flex-col gap-0.5 rounded-lg bg-surface px-1 py-2';
const countClass = 'text-2xl font-bold tabular-nums';
