import { ArrowRightLeft, Check, Coffee, MapPin, UserX } from 'lucide-react';
import { Link } from 'react-router';
import { attendanceHubKey, type BoardSummary } from '../../../shared/api/attendanceBoardApi';
import { Button } from '../../../shared/components/Button';
import { EditedMark } from '../../../shared/components/Badge';
import { LiveBadge } from '../../../shared/components/LiveBadge';
import { MoreInfo } from '../../../shared/components/MoreInfo';
import { linkClass } from '../../../shared/components/styles';
import { Note } from '../../../shared/components/Note';
import { PageLoading } from '../../../shared/components/PageState';
import { Toast } from '../../../shared/components/Toast';
import { getErrorMessage } from '../../../shared/utils/getErrorMessage';
import { AttendanceTable } from '../AttendanceTable';
import { BoardControls } from '../BoardControls';
import { CorrectionForm } from '../CorrectionForm';
import { CoverDrawer } from '../CoverDrawer';
import { PAGE_SIZE } from './type';
import { useLegacyAttendancePage } from './useLegacyAttendancePage';

/**
 * facility-0027, 0050, wireframe D2: whether each Area that works the shift has someone in it, never late or early.
 * The shift on the clock by default; the address keeps another date, shift or building. Live on AttendanceRecorded,
 * every open, every 30 s and on focus (facility-0061). facility-0051: the Admin adds or moves a time with a reason.
 */
export function LegacyAttendancePage() {
  const {
    args, board, error, isLoading, current, search, setSearch, notice, shown, page, setPage, cover,
    fillTargetMissing, open, openKey, formRow, formTarget, closeForm, openCover, openCorrection,
    onSlot, onBuilding, retry, clearNotice, closeCover, onCovered, onCorrected,
  } = useLegacyAttendancePage();

  return (
    <main className="flex min-w-0 flex-col gap-6 text-sm text-text">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="text-2xl font-bold">การเข้างาน</h1>
          <span className="text-text-2">ดูว่าพื้นที่ไหนมีแม่บ้านอยู่ แก้เวลาที่ลงผิด และมอบหมายคนทำแทน</span>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <LiveBadge hubKey={attendanceHubKey(args)} />
          {/* The design file's main action: the cover Drawer without picking a row first; the Area is chosen inside it */}
          <Button
            variant="primary"
            size="desk"
            disabled={!board || !current}
            onClick={() => openCover()}
          >
            <ArrowRightLeft aria-hidden="true" className="size-4" />
            มอบหมายทำแทน
          </Button>
        </div>
      </header>
      <Note tone="info">
        <span>
          หน้านี้ใช้ดูว่ามีคนดูแลพื้นที่หรือไม่ <strong>ไม่ใช่ระบบลงเวลาทำงาน และไม่คิดสาย</strong>
        </span>
      </Note>
      <BoardControls
        board={board}
        args={args}
        search={search}
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
          {cover && current && (
            <CoverDrawer
              current={current}
              start={cover}
              onDone={onCovered}
              onCancel={closeCover}
            />
          )}
          <AttendanceTable
            lifecycleVersion={board.lifecycleVersion}
            onCover={openCover}
            rows={shown.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE)}
            footer={shown.length > 0 && <TableFooter page={page} total={shown.length} onPage={setPage} />}
            onCorrect={openCorrection}
          />
        </>
      )}
      <BoardNotes />
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
          {'= แก้โดยแอดมิน ดูเหตุผลใน '}
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
    { label: 'พื้นที่ที่ต้องมีคน', count: summary.required, Icon: MapPin, tone: 'text-text-3' },
    { label: 'มีคนแล้ว', count: summary.present, Icon: Check, tone: 'text-passed' },
    { label: 'ยังไม่มีคน', count: summary.empty, Icon: UserX, tone: 'text-overdue' },
    { label: 'มีคนทำแทน', count: summary.covered, Icon: ArrowRightLeft, tone: 'text-break' },
    { label: 'พักเบรก', count: onBreak, Icon: Coffee, tone: 'text-break' },
  ];
  return (
    // auto-fit tiles at least 160 px wide: five across on a computer; no token describes a track list
    <ul className="m-0 grid list-none grid-cols-[repeat(auto-fit,minmax(160px,1fr))] gap-3 p-0" aria-label="สรุปกะนี้">
      {tiles.map(({ label, count, Icon, tone }) => (
        <li key={label} className="flex flex-col gap-0.5 rounded-lg border border-border bg-surface px-4 py-3">
          <span className={`flex items-center gap-1.5 text-sm font-semibold ${tone}`}>
            <Icon aria-hidden="true" className="size-4" />
            {label}
          </span>
          <strong className="text-3xl leading-tight font-bold tabular-nums">{count}</strong>
        </li>
      ))}
    </ul>
  );
}

/** Wireframe D2's notes under the table, shut until asked (owner, 2026-10-27). */
function BoardNotes() {
  return (
    <MoreInfo summary="เกี่ยวกับหน้านี้">
      <span>
        เรียงพื้นที่ที่ยังไม่มีคนขึ้นก่อน · พื้นที่ที่ทำเฉพาะกะเช้าไม่นับเป็น "ต้องมีคน" ในกะดึก · มอบหมายคนทำแทนได้ที่{' '}
        <Link className={linkClass} to="/admin/alerts">
          รายการที่ขึ้นเตือน
        </Link>
      </span>
      <span>
        ลืมลงเวลาเลิกงาน: ช่องเลิกงานว่างไว้ หมดกะแล้วขึ้นใน{' '}
        <Link className={linkClass} to="/admin/alerts">
          รายการที่ขึ้นเตือน
        </Link>{' '}
        ให้แอดมินเติม
      </span>
    </MoreInfo>
  );
}
