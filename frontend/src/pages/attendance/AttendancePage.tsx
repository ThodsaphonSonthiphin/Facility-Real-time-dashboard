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
import { LiveBadge } from '../../shared/components/LiveBadge';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftLabel } from '../../shared/utils/labels';
import { formatShiftDate } from '../../shared/utils/thaiTime';
import { AttendanceTable } from './AttendanceTable';
import { BoardControls } from './BoardControls';
import { readBoardArgs, searchRows, toBoardParams } from './attendanceRules';
import './attendance.css';

/**
 * facility-0027, 0050, wireframe D2: whether each Area that works the shift has someone in it, never late or early.
 * The shift on the clock by default; the address keeps another date, shift or building. Live on AttendanceRecorded,
 * every open, every 30 s and on focus (facility-0061).
 */
export function AttendancePage() {
  const [params, setParams] = useSearchParams();
  const args = readBoardArgs(params);
  const { data: board, error, isLoading } = useGetAttendanceBoardQuery(args, livePoll);
  const [search, setSearch] = useState('');
  const go = (next: BoardArgs) => setParams(toBoardParams(next));

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
      {isLoading && <p className="app-muted">กำลังโหลด...</p>}
      {error && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {board && (
        <>
          <Tiles summary={board.summary} />
          <AttendanceTable rows={searchRows(board.rows, search)} />
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
