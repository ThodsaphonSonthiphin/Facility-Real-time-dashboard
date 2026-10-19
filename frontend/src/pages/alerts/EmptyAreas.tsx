import type { ReactNode } from 'react';
import { Link } from 'react-router';
import type { AttendanceBoard } from '../../shared/api/attendanceBoardApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftShortLabel } from '../../shared/utils/labels';
import { formatShiftDate } from '../../shared/utils/thaiTime';

interface EmptyAreasProps {
  board?: AttendanceBoard;
  error?: unknown;
  /** The Area whose cover form is open. */
  chosen: number | null;
  onChoose: (areaId: number) => void;
  /** The cover form, shown under the list while an Area is chosen. */
  children?: ReactNode;
}

/** Wireframe D3's first box: the Areas with nobody this shift, from the same board as the attendance page (facility-0050). */
export function EmptyAreas({ board, error, chosen, onChoose, children }: EmptyAreasProps) {
  const empty = board ? board.rows.filter((row) => row.state === 'Empty') : [];
  return (
    <section className="alerts-section" aria-labelledby="alerts-empty">
      <h2 id="alerts-empty">{`Area ที่ยังไม่มีคน (${empty.length})`}</h2>
      <span className="app-muted">
        ข้อมูลเดียวกับ <Link to="/admin/attendance">การเข้างาน</Link>
        {board ? ` · ${shiftShortLabel[board.shift]} ${formatShiftDate(board.shiftDate)}` : ''}
      </span>
      {error != null && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {board && empty.length === 0 && <p className="app-note app-note--info">ทุก Area มีคนแล้ว</p>}
      <ul className="alerts-rows">
        {empty.map((row) => (
          <li key={row.areaId} className="alerts-row">
            <span>
              <strong>{`${row.areaCode} ${row.areaName}`}</strong>
              {` · ตึก ${row.buildingCode}`}
            </span>
            <span className="app-muted">{row.cleaner ? `${row.cleaner.displayName} ยังไม่ลงเวลา` : 'ไม่มีแม่บ้านประจำ'}</span>
            <button
              type="button"
              className="app-button"
              aria-label={`มอบหมายคนทำแทน ${row.areaCode}`}
              disabled={chosen === row.areaId}
              onClick={() => onChoose(row.areaId)}
            >
              {chosen === row.areaId ? 'กำลังมอบหมาย ›' : 'มอบหมายคนทำแทน'}
            </button>
          </li>
        ))}
      </ul>
      {children}
    </section>
  );
}
