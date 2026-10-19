import { useState } from 'react';
import { Link, useSearchParams } from 'react-router';
import {
  attendanceHubKey,
  useGetAttendanceBoardQuery,
  type AttendanceBoard,
  type BoardArgs,
  type BoardSummary,
} from '../../shared/api/attendanceBoardApi';
import { livePoll } from '../../shared/api/baseApi';
import type { AttendanceEvent } from '../../shared/api/types';
import { LiveBadge } from '../../shared/components/LiveBadge';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftLabel } from '../../shared/utils/labels';
import { formatShiftDate } from '../../shared/utils/thaiTime';
import { AttendanceTable } from './AttendanceTable';
import { BoardControls } from './BoardControls';
import { CorrectionForm } from './CorrectionForm';
import { readBoardArgs, readFill, searchRows, toBoardParams } from './attendanceRules';
import './attendance.css';

/** The row whose form is open, and the event it opens on. */
type Editing = { areaId: number; event?: AttendanceEvent };

type Notice = { kind: 'ok' | 'error'; text: string } | null;

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
  const [notice, setNotice] = useState<Notice>(null);
  const go = (next: BoardArgs) => setParams(toBoardParams(next));

  // From an alert's "เติมเวลา…" link (plan 11 decision 9): the form opens on that Cleaner once the board is in
  const fillRow = fill && board ? board.rows.find((row) => row.cleaner?.id === fill.userId) : undefined;
  const open: Editing | null = editing ?? (fillRow ? { areaId: fillRow.areaId, event: fill?.event } : null);
  const openRow = open && board ? board.rows.find((row) => row.areaId === open.areaId) : undefined;

  const closeForm = () => {
    setEditing(null);
    if (fill) {
      go(args); // the link's fill and event go, so the form does not open again
    }
  };

  return (
    <main className="attendance-page">
      <header className="attendance-heading">
        <div>
          {board && <span className="app-muted">{slotLine(board)}</span>}
          <h1>การเข้างาน</h1>
        </div>
        <LiveBadge hubKey={attendanceHubKey(args)} />
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
      {notice && (
        <p
          className={notice.kind === 'ok' ? 'app-note app-note--info' : 'app-note app-note--error'}
          role={notice.kind === 'ok' ? 'status' : 'alert'}
        >
          {notice.text}
        </p>
      )}
      {isLoading && <p className="app-muted">กำลังโหลด...</p>}
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {board && fill && !fillRow && !editing && (
        <p className="app-note app-note--info">ไม่พบแม่บ้านคนนี้ในกะนี้ อาจย้าย Area หรือปิดใช้งานแล้ว</p>
      )}
      {board && (
        <>
          <Tiles summary={board.summary} />
          {openRow?.cleaner && (
            <CorrectionForm
              key={`${openRow.areaId}:${open?.event ?? ''}`}
              board={board}
              row={openRow}
              cleaner={openRow.cleaner}
              initialEvent={open?.event}
              onDone={(text) => {
                setNotice({ kind: 'ok', text });
                closeForm();
              }}
              onCancel={closeForm}
            />
          )}
          <AttendanceTable
            rows={searchRows(board.rows, search)}
            onCorrect={(row) => {
              setNotice(null);
              setEditing({ areaId: row.areaId });
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

/** Wireframe D2's four counts (BoardSummaryDto). */
function Tiles({ summary }: { summary: BoardSummary }) {
  return (
    <ul className="attendance-tiles" aria-label="สรุปกะนี้">
      <li className="attendance-tile">
        <span>Area ที่ต้องมีคนกะนี้</span>
        <strong>{summary.required}</strong>
        <span className="app-muted">ไม่รวม Area ที่ปิดกะนี้</span>
      </li>
      <li className="attendance-tile">
        <span>มีคนแล้ว</span>
        <strong>{summary.present}</strong>
        <span className="app-muted">รวมพักเบรก</span>
      </li>
      <li className="attendance-tile attendance-tile--warn">
        <span>ยังไม่มีคน</span>
        <strong>{summary.empty}</strong>
        <span className="app-muted">ต้องหาคนทำแทน</span>
      </li>
      <li className="attendance-tile">
        <span>มีคนทำแทน</span>
        <strong>{summary.covered}</strong>
        <span className="app-muted">Admin มอบหมายเฉพาะกะนี้</span>
      </li>
    </ul>
  );
}

/** Wireframe D2's notes under the table. */
function BoardNotes() {
  return (
    <div className="attendance-notes">
      <span>หน้านี้ดูว่าแต่ละ Area มีคนอยู่แล้วหรือยัง ไม่ใช่ระบบลงเวลาทำงานของบริษัท จึงไม่คิดสายหรือกลับก่อน</span>
      <span>
        เรียง Area ที่ยังไม่มีคนขึ้นก่อน · Area ที่ทำเฉพาะกะเช้าไม่นับเป็น "ต้องมีคน" ในกะดึก · มอบหมายคนทำแทนได้ที่{' '}
        <Link to="/admin/alerts">รายการที่ขึ้นเตือน</Link>
      </span>
      <span>
        ลืมลงเวลาเลิกงาน: ช่องเลิกงานว่างไว้ หมดกะแล้วขึ้นใน <Link to="/admin/alerts">รายการที่ขึ้นเตือน</Link> ให้ Admin เติม
      </span>
    </div>
  );
}
