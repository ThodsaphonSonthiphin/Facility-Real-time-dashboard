import { useState, type ReactNode } from 'react';
import { Link } from 'react-router';
import type { AttendanceBoard, BoardRow } from '../../shared/api/attendanceBoardApi';
import { Button } from '../../shared/components/Button';
import { Select, TextInput } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
import { linkClass } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftShortLabel } from '../../shared/utils/labels';
import { formatShiftDate } from '../../shared/utils/thaiTime';
import { matchesSearch, sameSlot, slotKey, slotText, type CoverSlot } from './alertRules';
import { listClass, rowClass, sectionClass, sectionTitleClass } from './alertStyles';

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
    <section className={sectionClass} aria-labelledby="alerts-empty">
      <h2 id="alerts-empty" className={sectionTitleClass}>
        {ahead ? `พื้นที่ของกะที่ยังไม่เริ่ม (${rows.length})` : `พื้นที่ที่ยังไม่มีคน (${rows.length})`}
      </h2>
      {ahead && board ? (
        <span className="text-muted">{`${slotText(board)} ยังไม่เริ่ม จึงแสดงทุกพื้นที่ที่ทำกะนี้พร้อมแม่บ้านประจำ`}</span>
      ) : (
        <span className="text-muted">
          ข้อมูลเดียวกับ{' '}
          <Link className={linkClass} to="/admin/attendance">
            การเข้างาน
          </Link>
          {board ? ` · ${shiftShortLabel[board.shift]} ${formatShiftDate(board.shiftDate)}` : ''}
        </span>
      )}
      <div className="flex flex-wrap items-center gap-2">
        {current && slot && (
          <label className="inline-flex items-center gap-1.5">
            วันและกะ
            <Select
              size="desk"
              value={slotKey(slot)}
              onChange={(event) => onSlot(slots.find((option) => slotKey(option) === event.target.value) ?? current)}
            >
              {slots.map((option) => (
                <option key={slotKey(option)} value={slotKey(option)}>
                  {sameSlot(option, current) ? `${slotText(option)} (กะนี้)` : slotText(option)}
                </option>
              ))}
            </Select>
          </label>
        )}
        <label className="inline-flex items-center gap-1.5">
          ค้นหาชื่อแม่บ้านหรือพื้นที่
          <TextInput size="desk" type="search" value={search} onChange={(event) => setSearch(event.target.value)} />
        </label>
      </div>
      {error != null && <Note tone="error">{getErrorMessage(error)}</Note>}
      {board && !ahead && rows.length === 0 && <Note tone="info">ทุกพื้นที่มีคนแล้ว</Note>}
      {board && ahead && rows.length === 0 && <Note tone="info">ไม่มีพื้นที่ที่ทำกะนี้</Note>}
      {rows.length > 0 && shown.length === 0 && <Note tone="info">ไม่พบพื้นที่ที่ตรงกับที่ค้นหา</Note>}
      <ul className={listClass}>
        {shown.map((row) => (
          <li key={row.areaId} className={rowClass}>
            <span>
              <strong>{`${row.areaCode} ${row.areaName}`}</strong>
              {` · ตึก ${row.buildingCode}`}
            </span>
            <span className="text-muted">{cleanerText(row, ahead)}</span>
            {row.covers.length > 0 ? (
              // Owner, 2026-10-22: a covered Area ahead names who covers and has no assign button, as on the shift on
              // the clock, where it leaves the list. To change who, cancel in the covers box first
              <span>{`ทำแทนแล้ว: ${row.covers.map((cover) => cover.displayName).join(', ')}`}</span>
            ) : (
              <Button
                size="desk"
                aria-label={`มอบหมายคนทำแทน ${row.areaCode}`}
                disabled={chosen === row.areaId}
                onClick={() => onChoose(row.areaId)}
              >
                {chosen === row.areaId ? 'กำลังมอบหมาย ›' : 'มอบหมายคนทำแทน'}
              </Button>
            )}
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
