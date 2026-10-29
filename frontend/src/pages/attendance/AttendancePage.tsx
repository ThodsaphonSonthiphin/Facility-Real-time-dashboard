import { Download } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import {
  attendanceHubKey,
  useGetAttendanceBoardQuery,
  type AttendanceBoard,
  type BoardArgs,
  type BoardSummary,
} from '../../shared/api/attendanceBoardApi';
import { livePoll } from '../../shared/api/baseApi';
import type { AttendanceEvent, Shift } from '../../shared/api/types';
import { LiveBadge } from '../../shared/components/LiveBadge';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { boxClass, buttonClass, linkClass } from '../../shared/components/styles';
import { Note } from '../../shared/components/Note';
import { Toast, type Notice } from '../../shared/components/Toast';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftLabel } from '../../shared/utils/labels';
import { exportLink } from '../../shared/utils/dateRange';
import { formatShiftDate } from '../../shared/utils/thaiTime';
import { AttendanceTable } from './AttendanceTable';
import { BoardControls } from './BoardControls';
import { CorrectionForm } from './CorrectionForm';
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
  const { data: board, error, isLoading } = useGetAttendanceBoardQuery(args, livePoll);
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState<Editing | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);
  const go = (next: BoardArgs) => setParams(toBoardParams(next));

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
    <main className="flex flex-col gap-4 text-sm text-text">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-col gap-0.5">
          {board && <span className="text-text-2">{slotLine(board)}</span>}
          <h1 className="text-xl font-bold">การเข้างาน</h1>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {board && (
            <Link
              className={buttonClass('secondary', 'desk')}
              data-tap=""
              to={exportLink({ from: board.shiftDate, to: board.shiftDate }, args.buildingId)}
            >
              <Download aria-hidden="true" className="size-4" />
              ส่งออก Excel
            </Link>
          )}
          <LiveBadge hubKey={attendanceHubKey(args)} />
        </div>
      </header>
      <BoardControls
        board={board}
        args={args}
        search={search}
        onSearch={setSearch}
        onSlot={(shiftDate, shift) => go({ ...args, shiftDate, shift })}
        onNow={() => go({ buildingId: args.buildingId })}
        onBuilding={(buildingId) => go({ ...args, buildingId })}
      />
      {notice && <Toast notice={notice} onClose={() => setNotice(null)} />}
      {isLoading && <p className="text-text-2">กำลังโหลด...</p>}
      {error && <Note tone="error">{getErrorMessage(error)}</Note>}
      {board && fill && !fillRow && !editing && (
        <Note tone="info">ไม่พบแม่บ้านคนนี้ในกะนี้ อาจย้ายพื้นที่หรือปิดใช้งานแล้ว</Note>
      )}
      {board && (
        <>
          <Tiles summary={board.summary} />
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
          <AttendanceTable
            rows={searchRows(board.rows, search)}
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

function slotLine(board: AttendanceBoard): string {
  return `${formatShiftDate(board.shiftDate)} · ${shiftLabel[board.shift]}${board.shiftEnded ? ' · จบกะแล้ว' : ''}`;
}

const tileClass = `flex flex-col ${boxClass}`;
const countClass = 'text-2xl font-bold tabular-nums';

/**
 * Wireframe D2's four counts (BoardSummaryDto). ยังไม่มีคน is framed, tinted and counted in its state's red, the same
 * colour as its chip and its rows in the table, since it is the one that needs the Admin.
 */
function Tiles({ summary }: { summary: BoardSummary }) {
  return (
    // auto-fit tiles at least 200 px wide: four across on a computer; no token describes a track list
    <ul className="m-0 grid list-none grid-cols-[repeat(auto-fit,minmax(200px,1fr))] gap-3 p-0" aria-label="สรุปกะนี้">
      <li className={tileClass}>
        <span>พื้นที่ที่ต้องมีคนกะนี้</span>
        <strong className={countClass}>{summary.required}</strong>
        <span className="text-text-2">ไม่รวมพื้นที่ที่ปิดกะนี้</span>
      </li>
      <li className={tileClass}>
        <span>มีคนแล้ว</span>
        <strong className={countClass}>{summary.present}</strong>
        <span className="text-text-2">รวมพักเบรก</span>
      </li>
      <li className="flex flex-col rounded-lg border-2 border-rework bg-rework-bg px-4 py-3.5">
        <span>ยังไม่มีคน</span>
        <strong className={`${countClass} text-rework`}>{summary.empty}</strong>
        <span className="text-text-2">ต้องหาคนทำแทน</span>
      </li>
      <li className={tileClass}>
        <span>มีคนทำแทน</span>
        <strong className={countClass}>{summary.covered}</strong>
        <span className="text-text-2">แอดมินมอบหมายเฉพาะกะนี้</span>
      </li>
    </ul>
  );
}

/** Wireframe D2's notes under the table, shut until asked (owner, 2026-10-27). */
function BoardNotes() {
  return (
    <MoreInfo summary="เกี่ยวกับหน้านี้">
      <span>หน้านี้ดูว่าแต่ละพื้นที่มีคนอยู่แล้วหรือยัง ไม่ใช่ระบบลงเวลาทำงานของบริษัท จึงไม่คิดสายหรือกลับก่อน</span>
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
