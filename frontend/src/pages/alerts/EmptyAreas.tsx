import { useState, type ReactNode } from 'react';
import { Link } from 'react-router';
import type { AttendanceBoard, BoardRow } from '../../shared/api/attendanceBoardApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftShortLabel } from '../../shared/utils/labels';
import { formatShiftDate } from '../../shared/utils/thaiTime';
import { matchesSearch, sameSlot, slotKey, slotText, type CoverSlot } from './alertRules';

interface EmptyAreasProps {
  board?: AttendanceBoard;
  error?: unknown;
  /** The shifts the Admin may pick, the one on the clock first (facility-0075). */
  slots: CoverSlot[];
  /** The shift picked. */
  slot?: CoverSlot;
  /** A shift not started: every Area of it is listed with its regular Cleaner (facility-0084). */
  ahead: boolean;
  onSlot: (slot: CoverSlot) => void;
  /** The Area whose cover form is open. */
  chosen: number | null;
  onChoose: (areaId: number) => void;
  /** The cover form, shown under the list while an Area is chosen. */
  children?: ReactNode;
}

/**
 * Wireframe D3's first box. On the shift on the clock: the Areas with nobody, from the same board as the attendance
 * page (facility-0050). On a shift not started: every Area that works it, with its regular Cleaner, so the Admin can
 * cover a known absence before leaving (facility-0075, 0084). The search only narrows the rows on screen.
 */
export function EmptyAreas({ board, error, slots, slot, ahead, onSlot, chosen, onChoose, children }: EmptyAreasProps) {
  const [search, setSearch] = useState('');
  const rows = board ? board.rows.filter((row) => ahead || row.state === 'Empty') : [];
  const shown = rows.filter((row) => matchesSearch(row, search));
  const current = slots[0];

  return (
    <section className="alerts-section" aria-labelledby="alerts-empty">
      <h2 id="alerts-empty">{ahead ? `พื้นที่ของกะที่ยังไม่เริ่ม (${rows.length})` : `พื้นที่ที่ยังไม่มีคน (${rows.length})`}</h2>
      {ahead && board ? (
        <span className="app-muted">{`${slotText(board)} ยังไม่เริ่ม จึงแสดงทุกพื้นที่ที่ทำกะนี้พร้อมแม่บ้านประจำ`}</span>
      ) : (
        <span className="app-muted">
          ข้อมูลเดียวกับ <Link to="/admin/attendance">การเข้างาน</Link>
          {board ? ` · ${shiftShortLabel[board.shift]} ${formatShiftDate(board.shiftDate)}` : ''}
        </span>
      )}
      <div className="alerts-filters">
        {current && slot && (
          <label className="alerts-type">
            วันและกะ
            <select
              className="app-input"
              value={slotKey(slot)}
              onChange={(event) => onSlot(slots.find((option) => slotKey(option) === event.target.value) ?? current)}
            >
              {slots.map((option) => (
                <option key={slotKey(option)} value={slotKey(option)}>
                  {sameSlot(option, current) ? `${slotText(option)} (กะนี้)` : slotText(option)}
                </option>
              ))}
            </select>
          </label>
        )}
        <label className="alerts-type">
          ค้นหาชื่อแม่บ้านหรือพื้นที่
          <input type="search" className="app-input" value={search} onChange={(event) => setSearch(event.target.value)} />
        </label>
      </div>
      {error != null && (
        <p className="app-note app-note--error" role="alert">
          {getErrorMessage(error)}
        </p>
      )}
      {board && !ahead && rows.length === 0 && <p className="app-note app-note--info">ทุกพื้นที่มีคนแล้ว</p>}
      {board && ahead && rows.length === 0 && <p className="app-note app-note--info">ไม่มีพื้นที่ที่ทำกะนี้</p>}
      {rows.length > 0 && shown.length === 0 && <p className="app-note app-note--info">ไม่พบพื้นที่ที่ตรงกับที่ค้นหา</p>}
      <ul className="alerts-rows">
        {shown.map((row) => (
          <li key={row.areaId} className="alerts-row">
            <span>
              <strong>{`${row.areaCode} ${row.areaName}`}</strong>
              {` · ตึก ${row.buildingCode}`}
            </span>
            <span className="app-muted">{cleanerText(row, ahead)}</span>
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

/** On the shift on the clock the regular Cleaner has not checked in; ahead, nobody has yet. */
function cleanerText(row: BoardRow, ahead: boolean): string {
  if (!row.cleaner) {
    return 'ไม่มีแม่บ้านประจำ';
  }
  return ahead ? `แม่บ้านประจำ: ${row.cleaner.displayName}` : `${row.cleaner.displayName} ยังไม่ลงเวลา`;
}
