import { freshOnOpen } from '../../shared/api/myWorkApi';
import type { PointStatus } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftLabel } from '../../shared/utils/labels';
import { useAppSelector } from '../../store/hooks';
import type { HubStatus } from '../../store/slices/hubSlice';
import { BuildingCard } from './BuildingCard';
import { inspectionOrder } from './buildingRules';
import { useGetMyBuildingQuery, type MyBuilding } from './myBuildingApi';
import './myBuilding.css';

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
  const hubStatus = useAppSelector((state) => state.hub.status);

  return (
    <main className="building-page">
      <header className="building-heading">
        {data && <span className="building-kicker">{`ตอนนี้ ${shiftLabel[data.shift]}`}</span>}
        <h1>ตึกของฉัน</h1>
        {data && <p className="building-sub">{countLine(data)}</p>}
      </header>
      {isLoading && <p className="building-muted">กำลังโหลดจุด...</p>}
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {data && <BuildingList building={data} />}
      <footer className="building-footer">
        <span>{liveLine[hubStatus]}</span>
        <button type="button" className="app-button" disabled={isFetching} onClick={() => void refetch()}>
          {isFetching ? 'กำลังอัปเดต...' : 'อัปเดต'}
        </button>
      </footer>
    </main>
  );
}

function countLine(building: MyBuilding): string {
  const points = building.buildings.flatMap((section) => section.points);
  const areas = new Set(points.map((point) => point.areaId));
  return `${areas.size} Area · ${points.length} จุด`;
}

function BuildingList({ building }: { building: MyBuilding }) {
  if (building.buildings.length === 0) {
    return <p className="app-note app-note--info">บัญชีนี้ยังไม่ได้ผูกกับตึก หรือตึกปิดใช้งาน แจ้ง Admin</p>;
  }
  const points = building.buildings.flatMap((section) => section.points);
  const count = (status: PointStatus) => points.filter((point) => point.status === status).length;

  return (
    <>
      <ul className="building-summary" aria-label="สรุปการตรวจ">
        <li className="building-summary__pending">
          <strong>{count('PendingInspection')}</strong> รอตรวจ
        </li>
        <li className="building-summary__rework">
          <strong>{count('Rework')}</strong> รอแม่บ้านแก้
        </li>
        <li>
          <strong>{count('Passed')}</strong> ตรวจแล้ว
        </li>
      </ul>
      {building.buildings.map((section) => {
        const headingId = `building-${section.buildingId}`;
        return (
          <section key={section.buildingId} className="building-section" aria-labelledby={headingId}>
            <h2 id={headingId}>{section.isCover ? `${section.buildingName} (ตรวจแทนกะนี้)` : section.buildingName}</h2>
            {!section.canInspect && (
              <p className="app-note app-note--warn">ตอนนี้ไม่ใช่กะของคุณ ดูได้ แต่สแกนตรวจไม่ได้</p>
            )}
            {section.points.length === 0 ? (
              <p className="building-muted">ตึกนี้ยังไม่มีจุด</p>
            ) : (
              <ul className="building-cards">
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
