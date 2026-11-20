import { ArrowRightLeft, Check, Coffee, MapPin, UserX } from 'lucide-react';
import { Link } from 'react-router';
import { attendanceHubKey, type BoardSummary } from '../../../shared/api/attendanceBoardApi';
import { AdminPageHeader } from '../../../shared/components/AdminPageHeader/AdminPageHeader';
import { Button } from '../../../shared/components/Button';
import { EditedMark } from '../../../shared/components/Badge';
import { KpiCard } from '../../../shared/components/KpiCard/KpiCard';
import { LiveBadge } from '../../../shared/components/LiveBadge';
import { linkClass } from '../../../shared/components/styles';
import { Note } from '../../../shared/components/Note';
import { PageLoading } from '../../../shared/components/PageState';
import { Toast } from '../../../shared/components/Toast';
import { getErrorMessage } from '../../../shared/utils/getErrorMessage';
import { AttendanceTable } from '../AttendanceTable';
import { BoardControls } from '../BoardControls';
import { CorrectionForm } from '../CorrectionForm';
import { PAGE_SIZE } from './type';
import { useLegacyAttendancePage } from './useLegacyAttendancePage';

/**
 * facility-0027, 0050, wireframe D2: whether each Area that works the shift has someone in it, never late or early.
 * The shift on the clock by default; the address keeps another date, shift or building. Live on AttendanceRecorded,
 * every open, every 30 s and on focus (facility-0061). facility-0051: the Admin adds or moves a time with a reason.
 * Emergency cover assignment lives under /admin/schedules (facility-0225).
 */
export function LegacyAttendancePage() {
  const {
    args, board, error, isLoading, search, setSearch, notice, shown, page, setPage,
    fillTargetMissing, open, openKey, formRow, formTarget, closeForm, openCorrection,
    canResetToCurrent, onResetToCurrent, onSlot, onBuilding, retry, clearNotice, onCorrected,
  } = useLegacyAttendancePage();

  return (
    <main className="flex min-w-0 flex-col gap-5 text-sm text-text">
      <AdminPageHeader
        title="การเข้างาน"
        actions={<LiveBadge hubKey={attendanceHubKey(args)} />}
      />
      <BoardControls
        board={board}
        args={args}
        search={search}
        canResetToCurrent={canResetToCurrent}
        onResetToCurrent={onResetToCurrent}
        onSearch={setSearch}
        onSlot={onSlot}
        onBuilding={onBuilding}
      />
      {notice && <Toast notice={notice} onClose={clearNotice} />}
      {isLoading && <PageLoading lines={3} />}
      {error && (
        <Note tone="error" onRetry={retry}>
          {getErrorMessage(error)}
        </Note>
      )}
      {fillTargetMissing && (
        <Note tone="info">ไม่พบแม่บ้านคนนี้ในกะนี้ อาจย้ายพื้นที่หรือปิดใช้งานแล้ว</Note>
      )}
      {board && (
        <>
          <Tiles summary={board.summary} onBreak={board.rows.filter((row) => row.state === 'OnBreak').length} />
          {open && openKey && formRow && formTarget && (
            <CorrectionForm
              key={openKey}
              board={board}
              row={{ ...formRow, events: formTarget.events }}
              cleaner={formTarget.cleaner}
              initialEvent={open.event}
              onDone={(text) => onCorrected(text, openKey)}
              onCancel={() => closeForm(openKey)}
            />
          )}
          <AttendanceTable
            rows={shown.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE)}
            footer={shown.length > 0 && <TableFooter page={page} total={shown.length} onPage={setPage} />}
            onCorrect={openCorrection}
          />
        </>
      )}
    </main>
  );
}

/** The bar under the table: which Areas are on screen, what [แก้] means, and the pages. */
function TableFooter({ page, total, onPage }: { page: number; total: number; onPage: (page: number) => void }) {
  const first = page * PAGE_SIZE + 1;
  const last = Math.min(total, (page + 1) * PAGE_SIZE);
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-text-2">
      <span className="inline-flex flex-wrap items-center gap-x-4 gap-y-1">
        <span>{`แสดง ${first}–${last} จาก ${total} พื้นที่`}</span>
        <span className="inline-flex items-center gap-1.5">
          <EditedMark />
          {'= แก้โดยแอดมิน · '}
          <Link className={linkClass} to="/admin/history">
            ประวัติการแก้ไข
          </Link>
        </span>
      </span>
      <span className="flex gap-2">
        <Button size="desk" disabled={page === 0} onClick={() => onPage(page - 1)}>
          ก่อนหน้า
        </Button>
        <Button size="desk" disabled={last >= total} onClick={() => onPage(page + 1)}>
          ถัดไป
        </Button>
      </span>
    </div>
  );
}

/**
 * Wireframe D2's five counts (BoardSummaryDto, and the Areas on a break from the rows): each with its icon and its state's
 * colour, as the design file's. ยังไม่มีคน is in the same orange as its chip and its rows, since it is the one that needs
 * the Admin.
 */
function Tiles({ summary, onBreak }: { summary: BoardSummary; onBreak: number }) {
  const tiles = [
    { label: 'พื้นที่ที่ต้องมีคน', count: summary.required, Icon: MapPin, tone: 'blue' as const },
    { label: 'มีคนแล้ว', count: summary.present, Icon: Check, tone: 'green' as const },
    { label: 'ยังไม่มีคน', count: summary.empty, Icon: UserX, tone: 'amber' as const },
    { label: 'มีคนทำแทน', count: summary.covered, Icon: ArrowRightLeft, tone: 'purple' as const },
    { label: 'พักเบรก', count: onBreak, Icon: Coffee, tone: 'purple' as const },
  ];
  return (
    <ul className="m-0 grid list-none grid-cols-2 gap-3 p-0 sm:grid-cols-3 lg:grid-cols-5" aria-label="สรุปกะนี้">
      {tiles.map(({ label, count, Icon, tone }) => (
        <KpiCard key={label} as="li" label={label} value={count} icon={Icon} tone={tone} />
      ))}
    </ul>
  );
}
