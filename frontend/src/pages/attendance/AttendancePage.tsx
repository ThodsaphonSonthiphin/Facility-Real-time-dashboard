import { ArrowRightLeft, Check, Coffee, MapPin, UserX } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import {
  attendanceHubKey,
  useGetAttendanceBoardQuery,
  type BoardArgs,
  type BoardSummary,
} from '../../shared/api/attendanceBoardApi';
import { livePoll } from '../../shared/api/baseApi';
import type { AttendanceEvent, Shift } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { EditedMark } from '../../shared/components/Badge';
import { LiveBadge } from '../../shared/components/LiveBadge';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { linkClass } from '../../shared/components/styles';
import { Note } from '../../shared/components/Note';
import { PageLoading } from '../../shared/components/PageState';
import { Toast, type Notice } from '../../shared/components/Toast';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { AttendanceTable } from './AttendanceTable';
import { BoardControls } from './BoardControls';
import { CorrectionForm } from './CorrectionForm';
import { CoverDrawer } from './CoverDrawer';
import type { CoverSlot } from '../../shared/utils/coverSlots';
import { readBoardArgs, readFill, searchRows, toBoardParams } from './attendanceRules';

/** The row whose form is open, the event it opens on, and the slot and Cleaner it was opened for. */
type Editing = { areaId: number; event?: AttendanceEvent; shiftDate: string; shift: Shift; userId: number };

const formKey = (form: Editing) => `${form.areaId}:${form.event ?? ''}:${form.shiftDate}:${form.shift}:${form.userId}`;

/**
 * facility-0027, 0050, wireframe D2: whether each Area that works the shift has someone in it, never late or early.
 * The shift on the clock by default; the address keeps another date, shift or building. Live on AttendanceRecorded,
 * every open, every 30 s and on focus (facility-0061). facility-0051: the Admin adds or moves a time with a reason.
 */
export function AttendancePage() {
  const [params, setParams] = useSearchParams();
  const args = readBoardArgs(params);
  const fill = readFill(params);
  const { data: board, error, isLoading, refetch } = useGetAttendanceBoardQuery(args, livePoll);
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<Editing | null>(null);
  // The board of the shift on the clock (the same query as this page's when no other shift is asked): covers go from it
  const nowBoard = useGetAttendanceBoardQuery({}, livePoll);
  const current = nowBoard.data ? { shiftDate: nowBoard.data.shiftDate, shift: nowBoard.data.shift } : undefined;
  // The cover Drawer, with the shift it opens on and the Area of the row it was opened from; it picks its own shift
  const [cover, setCover] = useState<(CoverSlot & { areaId?: number }) | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const go = (next: BoardArgs) => setParams(toBoardParams(next));
  const shown = board ? searchRows(board.rows, search) : [];
  // Another search, shift or building starts again at the first page
  const pageKey = `${search}|${toBoardParams(args).toString()}`;
  const [paging, setPaging] = useState({ key: pageKey, page: 0 });
  const lastPage = Math.max(0, Math.ceil(shown.length / PAGE_SIZE) - 1);
  const page = paging.key === pageKey ? Math.min(paging.page, lastPage) : 0;
  const setPage = (next: number) => setPaging({ key: pageKey, page: next });

  // From an alert's "เติมเวลา…" link (plan 11 decision 9): the form opens on that Cleaner once the board is in
  const fillRow = fill && board ? board.rows.find((row) => row.cleaner?.id === fill.userId) : undefined;

  // A form opened by hand belongs to the slot and Cleaner it was opened for: when the board under it changes, it closes
  const openRow = editing && board ? board.rows.find((row) => row.areaId === editing.areaId) : undefined;
  if (
    editing &&
    board &&
    (board.shiftDate !== editing.shiftDate || board.shift !== editing.shift || openRow?.cleaner?.id !== editing.userId)
  ) {
    setEditing(null);
    setNotice({ kind: 'warn', text: 'กะที่แสดงเปลี่ยนแล้ว ฟอร์มแก้เวลาถูกปิด เปิดใหม่จากแถวของแม่บ้าน' });
  }
  const open: Editing | null =
    editing ??
    (fillRow?.cleaner && board
      ? { areaId: fillRow.areaId, event: fill?.event, shiftDate: board.shiftDate, shift: board.shift, userId: fillRow.cleaner.id }
      : null);
  const formRow = open && board ? board.rows.find((row) => row.areaId === open.areaId) : undefined;
  const openKey = open ? formKey(open) : null;
  const openKeyRef = useRef(openKey);
  useEffect(() => {
    openKeyRef.current = openKey;
  }, [openKey]);

  // Closes only the form the send belongs to: the Admin may have opened another since
  const closeForm = (key: string) => {
    setEditing((current) => (current && formKey(current) === key ? null : current));
    if (fill && openKeyRef.current === key) {
      go(args); // the link's fill and event go, so the form does not open again
    }
  };

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
            onClick={() => {
              setNotice(null);
              if (board) {
                setCover({ shiftDate: board.shiftDate, shift: board.shift });
              }
            }}
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
        onSlot={(shiftDate, shift) => go({ ...args, shiftDate, shift })}
        onBuilding={(buildingId) => go({ ...args, buildingId })}
      />
      {notice && <Toast notice={notice} onClose={() => setNotice(null)} />}
      {isLoading && <PageLoading lines={3} />}
      {error && (
        <Note tone="error" onRetry={() => void refetch()}>
          {getErrorMessage(error)}
        </Note>
      )}
      {board && fill && !fillRow && !editing && (
        <Note tone="info">ไม่พบแม่บ้านคนนี้ในกะนี้ อาจย้ายพื้นที่หรือปิดใช้งานแล้ว</Note>
      )}
      {board && (
        <>
          <Tiles summary={board.summary} onBreak={board.rows.filter((row) => row.state === 'OnBreak').length} />
          {open && openKey && formRow?.cleaner && (
            <CorrectionForm
              key={openKey}
              board={board}
              row={formRow}
              cleaner={formRow.cleaner}
              initialEvent={open.event}
              onDone={(text) => {
                setNotice({ kind: 'ok', text });
                closeForm(openKey);
              }}
              onCancel={() => closeForm(openKey)}
            />
          )}
          {cover && current && (
            <CoverDrawer
              current={current}
              start={cover}
              onDone={(text) => {
                setNotice({ kind: 'ok', text });
                setCover(null);
              }}
              onCancel={() => setCover(null)}
            />
          )}
          <AttendanceTable
            onCover={(row) => {
              setNotice(null);
              setCover({ areaId: row.areaId, shiftDate: board.shiftDate, shift: board.shift });
            }}
            rows={shown.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE)}
            footer={shown.length > 0 && <TableFooter page={page} total={shown.length} onPage={setPage} />}
            onCorrect={(row) => {
              setNotice(null);
              if (board && row.cleaner) {
                setEditing({ areaId: row.areaId, shiftDate: board.shiftDate, shift: board.shift, userId: row.cleaner.id });
              }
            }}
          />
        </>
      )}
      <BoardNotes />
    </main>
  );
}

/** The design file's D2 pages: 25 Areas a page (owner, 2026-10-29), the Areas with nobody first as the server sends them. */
const PAGE_SIZE = 25;

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
