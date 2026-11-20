import { ChevronDown, ChevronUp } from 'lucide-react';
import { AdminPageHeader } from '../../../shared/components/AdminPageHeader/AdminPageHeader';
import { LiveBadge } from '../../../shared/components/LiveBadge';
import { MoreInfo } from '../../../shared/components/MoreInfo';
import { Note } from '../../../shared/components/Note';
import { PageEmpty, PageLoading } from '../../../shared/components/PageState';
import { Button } from '../../../shared/components/Button';
import { getErrorMessage } from '../../../shared/utils/getErrorMessage';
import { useBuildingNames, buildingText } from '../../../shared/api/buildingNames';
import { DASHBOARD_HUB } from '../dashboardApi';
import { Filters } from '../Filters';
import { PointTile } from '../PointTile';
import { QUIET_ROWS, type AreaSectionProps } from './type';
import { useLegacyDashboardPage, useQuietArea } from './useLegacyDashboardPage';

/** Wireframe D1's grey line, word for word. */
const RULES_NOTE =
  'ถ้าจบรอบแล้วยังไม่มีงาน จุดจะแสดงว่า “เลยเวลา” ถ้าส่งงานก่อนรอบถัดไปของกะนั้นเริ่ม ระบบจะนับให้รอบเดิมและระบุว่าส่งช้า เมื่อเริ่มรอบใหม่ หน้าจะแสดงสถานะของรอบใหม่ ส่วน “แจ้งปัญหา” คือสิ่งที่แม่บ้านรายงานไว้ เช่น น้ำยาหมด';

/**
 * facility-0058, wireframe D1: every point of every active Area, by Area, as the clock's shift sees it. Pushed cards
 * change at once (#26); every open, the 30 s poll and a window focus catch what nobody pushes (facility-0061). A failed
 * poll keeps the last cards on screen under the error.
 */
export function LegacyDashboardPage() {
  const { data, isLoading, error, shown, needing, quiet, quietShown, showAllQuiet, clearFilters, toggleAllQuiet, retry } =
    useLegacyDashboardPage();

  return (
    <main className="flex flex-col gap-5 text-sm text-text">
      <AdminPageHeader
        title="สถานะจุดทำความสะอาด"
        subtitle={data ? `${new Set(data.map((point) => point.areaId)).size} พื้นที่ · ${data.length} จุด` : ''}
        actions={<LiveBadge hubKey={DASHBOARD_HUB} />}
      />
      {data && <Filters points={data} />}
      {isLoading && <PageLoading lines={3}>กำลังโหลดจุด...</PageLoading>}
      {error && (
        <Note tone="error" onRetry={retry}>
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
              <Button className="mt-2" size="desk" onClick={clearFilters}>
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
            <Button variant="link" className="self-start" onClick={toggleAllQuiet}>
              {showAllQuiet ? 'แสดงน้อยลง' : `แสดงอีก ${quiet.length - QUIET_ROWS} พื้นที่`}
            </Button>
          )}
        </section>
      )}
      {!isLoading && <MoreInfo summary="วิธีอ่านการ์ด">{RULES_NOTE}</MoreInfo>}
    </main>
  );
}

/** One Area with nothing wrong, folded into a line: its words, its points' statuses in short, and "ดูจุด" to open its cards. */
function QuietArea({ group }: AreaSectionProps) {
  const { names, open, summary, toggle } = useQuietArea({ group });
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
          onClick={toggle}
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

function AreaSection({ group }: AreaSectionProps) {
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
