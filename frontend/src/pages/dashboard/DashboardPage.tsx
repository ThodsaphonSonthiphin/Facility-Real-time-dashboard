import { Download } from 'lucide-react';
import { Link } from 'react-router';
import { livePoll } from '../../shared/api/baseApi';
import { LiveBadge } from '../../shared/components/LiveBadge';
import { Note } from '../../shared/components/Note';
import { buttonClass } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { useBuildingNames, buildingText } from '../../shared/api/buildingNames';
import { useAppSelector } from '../../store/hooks';
import { filterPoints, groupByArea, type AreaGroup } from './boardRules';
import { DASHBOARD_HUB, useGetServicePointsQuery } from './dashboardApi';
import { Filters } from './Filters';
import { PointTile } from './PointTile';

/** Wireframe D1's grey line, word for word. */
const RULES_NOTE =
  'รอบเป็นช่วงเวลา พ้นเวลาสิ้นสุดแล้วยังไม่ส่ง = เลยเวลา · ส่งหลังช่วงจบนับเป็นรอบนั้นแต่ติดว่าส่งช้า · พอรอบใหม่เริ่ม การ์ดเริ่มใหม่ งานเก่าที่ไม่ได้ตรวจหรือไม่ได้แก้เก็บในประวัติ · ป้ายแดง "แจ้งปัญหา" คือของชำรุดที่แม่บ้านแจ้ง ไม่ใช่สถานะของการ์ด';

/**
 * facility-0058, wireframe D1: every point of every active Area, by Area, as the clock's shift sees it. Pushed cards
 * change at once (#26); every open, the 30 s poll and a window focus catch what nobody pushes (facility-0061). A failed
 * poll keeps the last cards on screen under the error.
 */
export function DashboardPage() {
  const { data, error, isLoading } = useGetServicePointsQuery(undefined, livePoll);
  const filters = useAppSelector((state) => state.dashboard);
  const shown = data ? filterPoints(data, filters) : [];

  return (
    <main className="flex flex-col gap-4 text-sm text-text">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-bold">สถานะจุดทำความสะอาด</h1>
        <div className="flex flex-wrap items-center gap-2">
          <Link className={buttonClass('secondary', 'desk')} data-tap="" to="/admin/export">
            <Download aria-hidden="true" className="size-4" />
            ส่งออก Excel
          </Link>
          <LiveBadge hubKey={DASHBOARD_HUB} />
        </div>
      </header>
      {data && <Filters points={data} />}
      <p className="text-muted">{RULES_NOTE}</p>
      {isLoading && <p className="text-muted">กำลังโหลดจุด...</p>}
      {error && <Note tone="error">{getErrorMessage(error)}</Note>}
      {data && data.length === 0 && (
        <Note tone="info">ยังไม่มีจุดที่เปิดใช้งาน เพิ่มได้ที่หน้าจัดการพื้นที่และป้าย</Note>
      )}
      {data && data.length > 0 && shown.length === 0 && <Note tone="info">ไม่มีจุดตามตัวกรองนี้</Note>}
      {groupByArea(shown).map((group) => (
        <AreaSection key={group.areaId} group={group} />
      ))}
    </main>
  );
}

function AreaSection({ group }: { group: AreaGroup }) {
  const names = useBuildingNames();
  const headingId = `dashboard-area-${group.areaId}`;
  return (
    <section className="flex flex-col gap-2" aria-labelledby={headingId}>
      <div className="flex flex-wrap items-baseline gap-2.5">
        <h2 id={headingId} className="text-base font-bold">{`${group.areaCode} ${group.areaName}`}</h2>
        <span className="text-muted">{`ตึก ${buildingText(group.buildingCode, names)} · ${group.points.length} จุด`}</span>
      </div>
      {/* auto-fill tiles at least 230 px wide (the mockup's): four across at 1280 px; no token describes a track list */}
      <ul className="m-0 grid list-none grid-cols-[repeat(auto-fill,minmax(230px,1fr))] gap-2 p-0">
        {group.points.map((point) => (
          <PointTile key={point.id} point={point} />
        ))}
      </ul>
    </section>
  );
}
