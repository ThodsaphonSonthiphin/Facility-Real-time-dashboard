import { Link } from 'react-router';
import { livePoll } from '../../shared/api/baseApi';
import { LiveBadge } from '../../shared/components/LiveBadge';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { useAppSelector } from '../../store/hooks';
import { filterPoints, groupByArea, type AreaGroup } from './boardRules';
import { DASHBOARD_HUB, useGetServicePointsQuery } from './dashboardApi';
import { Filters } from './Filters';
import { PointTile } from './PointTile';
import './dashboard.css';

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
    <main className="dashboard-page">
      <header className="dashboard-heading">
        <h1>สถานะจุดทำความสะอาด</h1>
        <div className="dashboard-heading__side">
          <Link className="app-button" to="/admin/export">
            ส่งออก Excel
          </Link>
          <LiveBadge hubKey={DASHBOARD_HUB} />
        </div>
      </header>
      {data && <Filters points={data} />}
      <p className="app-muted">{RULES_NOTE}</p>
      {isLoading && <p className="app-muted">กำลังโหลดจุด...</p>}
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {data && data.length === 0 && (
        <p className="app-note app-note--info">ยังไม่มีจุดที่เปิดใช้งาน เพิ่มได้ที่หน้าจัดการพื้นที่และป้าย</p>
      )}
      {data && data.length > 0 && shown.length === 0 && <p className="app-note app-note--info">ไม่มีจุดตามตัวกรองนี้</p>}
      {groupByArea(shown).map((group) => (
        <AreaSection key={group.areaId} group={group} />
      ))}
    </main>
  );
}

function AreaSection({ group }: { group: AreaGroup }) {
  const headingId = `dashboard-area-${group.areaId}`;
  return (
    <section className="dashboard-area" aria-labelledby={headingId}>
      <div className="dashboard-area__head">
        <h2 id={headingId}>{`${group.areaCode} ${group.areaName}`}</h2>
        <span className="app-muted">{`ตึก ${group.buildingCode} · ${group.points.length} จุด`}</span>
      </div>
      <ul className="dashboard-tiles">
        {group.points.map((point) => (
          <PointTile key={point.id} point={point} />
        ))}
      </ul>
    </section>
  );
}
