import { ChevronDown, ChevronUp } from 'lucide-react';
import { useState } from 'react';
import { useGetBuildingsQuery } from '../../shared/api/adminSetupApi';
import { livePoll } from '../../shared/api/baseApi';
import { LiveBadge } from '../../shared/components/LiveBadge';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Note } from '../../shared/components/Note';
import { PageEmpty, PageLoading } from '../../shared/components/PageState';
import { Button } from '../../shared/components/Button';
import { pointStatusLabel } from '../../shared/utils/labels';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { useBuildingNames, buildingText } from '../../shared/api/buildingNames';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { filtersCleared } from '../../store/slices/dashboardSlice';
import { filterPoints, groupByArea, hasProblem, isUnfiltered, statusCounts, type AreaGroup } from './boardRules';
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
  const { data: points, error, isLoading: pointsLoading, refetch } = useGetServicePointsQuery(undefined, livePoll);
  // The building picker widens when the names arrive; asked here too (one shared request) so the filters, the help
  // and the cards appear together and nothing already on screen moves (layout shift, measured 2026-10-28)
  const { isLoading: buildingsLoading } = useGetBuildingsQuery();
  const isLoading = pointsLoading || buildingsLoading;
  const data = isLoading ? undefined : points;
  const filters = useAppSelector((state) => state.dashboard);
  const shown = data ? filterPoints(data, filters) : [];
  const dispatch = useAppDispatch();
  const [showAllQuiet, setShowAllQuiet] = useState(false);
  // With "เรียงพื้นที่ที่มีปัญหาขึ้นก่อน" on and nothing filtered, the Areas that need someone come first as cards and the
  // others fold into one line each; a filter or a search shows every Area it found, as cards, in the server's order
  const groups = groupByArea(shown);
  const folding = filters.problemFirst && isUnfiltered(filters);
  const needing = folding ? groups.filter(hasProblem) : groups;
  const quiet = folding ? groups.filter((group) => !hasProblem(group)) : [];
  const quietShown = showAllQuiet ? quiet : quiet.slice(0, QUIET_ROWS);

  return (
    <main className="flex flex-col gap-6 text-sm text-text">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-bold">สถานะจุดทำความสะอาด</h1>
          {/* Kept as tall as its words before the answer comes, so the cards under it do not move (layout shift) */}
          <span className="min-h-5 text-text-2">
            {data ? `${new Set(data.map((point) => point.areaId)).size} พื้นที่ · ${data.length} จุด` : ''}
          </span>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <LiveBadge hubKey={DASHBOARD_HUB} />
        </div>
      </header>
      {data && <Filters points={data} />}
      {!isLoading && <MoreInfo summary="วิธีอ่านการ์ด">{RULES_NOTE}</MoreInfo>}
      {isLoading && <PageLoading lines={3}>กำลังโหลดจุด...</PageLoading>}
      {error && (
        <Note tone="error" onRetry={() => void refetch()}>
          {getErrorMessage(error)}
        </Note>
      )}
      {data && data.length === 0 && (
        <Note tone="info">ยังไม่มีจุดที่เปิดใช้งาน เพิ่มได้ที่หน้าจัดการพื้นที่และป้าย</Note>
      )}
      {data && data.length > 0 && shown.length === 0 && (
        <div className="rounded-lg border border-border bg-surface">
          <PageEmpty
            title="ไม่มีจุดตามตัวกรองนี้"
            action={
              <Button className="mt-2" size="desk" onClick={() => dispatch(filtersCleared())}>
                ล้างตัวกรอง
              </Button>
            }
          >
            ลองเลือกตึกอื่น หรือล้างตัวกรองเพื่อดูทุกจุด
          </PageEmpty>
        </div>
      )}
      {needing.map((group) => (
        <AreaSection key={group.areaId} group={group} />
      ))}
      {quiet.length > 0 && (
        <section className="flex flex-col gap-3" aria-labelledby="dashboard-quiet">
          <div className="flex items-baseline gap-3">
            <h2 id="dashboard-quiet" className="text-lg font-semibold">
              พื้นที่ที่ไม่มีปัญหา
            </h2>
            <span className="text-text-2">{`${quiet.length} พื้นที่ · ย่อไว้ 1 บรรทัด`}</span>
          </div>
          <ul className="m-0 list-none overflow-hidden rounded-lg border border-border bg-surface p-0">
            {quietShown.map((group) => (
              <QuietArea key={group.areaId} group={group} />
            ))}
          </ul>
          {quiet.length > QUIET_ROWS && (
            <Button variant="link" className="self-start" onClick={() => setShowAllQuiet((all) => !all)}>
              {showAllQuiet ? 'แสดงน้อยลง' : `แสดงอีก ${quiet.length - QUIET_ROWS} พื้นที่`}
            </Button>
          )}
        </section>
      )}
    </main>
  );
}

/** How many folded Areas show before "แสดงอีก n พื้นที่" (the design file's five). */
const QUIET_ROWS = 5;

/** One Area with nothing wrong, folded into a line: its words, its points' statuses in short, and "ดูจุด" to open its cards. */
function QuietArea({ group }: { group: AreaGroup }) {
  const names = useBuildingNames();
  const [open, setOpen] = useState(false);
  const counts = statusCounts(group.points);
  const summary = (Object.keys(counts) as (keyof typeof counts)[])
    .filter((status) => counts[status] > 0)
    .map((status) => `${pointStatusLabel[status]} ${counts[status]}`)
    .join(' · ');
  const regionId = `dashboard-quiet-${group.areaId}`;
  return (
    <li className="border-b border-border last:border-b-0">
      <div className="flex min-h-12 flex-wrap items-center gap-x-4 gap-y-1 px-4 py-2.5">
        <span className="min-w-55 font-semibold">{`${group.areaCode} ${group.areaName}`}</span>
        <span className="min-w-60 text-text-2">{`ตึก ${buildingText(group.buildingCode, names)} · ${group.points.length} จุด`}</span>
        <span className="flex-1 text-text-3">{summary}</span>
        <Button
          variant="link"
          size="desk"
          aria-expanded={open}
          aria-controls={regionId}
          onClick={() => setOpen((on) => !on)}
        >
          {open ? 'ซ่อนจุด' : 'ดูจุด'}
          {open ? (
            <ChevronUp aria-hidden="true" className="size-4" />
          ) : (
            <ChevronDown aria-hidden="true" className="size-4" />
          )}
        </Button>
      </div>
      {open && (
        <ul
          id={regionId}
          className="m-0 grid list-none grid-cols-[repeat(auto-fill,minmax(230px,1fr))] gap-3 p-4 pt-1"
          aria-label={`จุดของ ${group.areaCode}`}
        >
          {group.points.map((point) => (
            <PointTile key={point.id} point={point} />
          ))}
        </ul>
      )}
    </li>
  );
}

function AreaSection({ group }: { group: AreaGroup }) {
  const names = useBuildingNames();
  const headingId = `dashboard-area-${group.areaId}`;
  return (
    <section className="flex flex-col gap-3" aria-labelledby={headingId}>
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <h2 id={headingId} className="text-lg font-semibold">{`${group.areaCode} ${group.areaName}`}</h2>
        <span className="text-text-2">{`ตึก ${buildingText(group.buildingCode, names)} · ${group.points.length} จุด`}</span>
      </div>
      {/* auto-fill tiles at least 230 px wide (the mockup's): four across at 1280 px; no token describes a track list */}
      <ul className="m-0 grid list-none grid-cols-[repeat(auto-fill,minmax(230px,1fr))] gap-3 p-0">
        {group.points.map((point) => (
          <PointTile key={point.id} point={point} />
        ))}
      </ul>
    </section>
  );
}
