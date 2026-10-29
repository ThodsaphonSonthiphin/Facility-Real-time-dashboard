import { ArrowRightLeft, Check, UserX } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import type { AttendanceBoard, BoardRow } from '../../shared/api/attendanceBoardApi';
import { Table } from '../../shared/components/Boxes';
import { Button } from '../../shared/components/Button';
import { SearchBox } from '../../shared/components/CandidateChoices';
import { Select } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
import { tableCell } from '../../shared/components/styles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { matchesSearch, sameSlot, slotKey, slotText, type CoverSlot } from './alertRules';
import { countClass, countTones, panelClass, panelHeadClass, panelTitleClass } from './alertStyles';

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
  onChoose: (areaId: number) => void;
  /** The cover form, a popup over the page while an Area is chosen (owner, #78). */
  children?: ReactNode;
}

/**
 * The design file's D3 "พื้นที่ที่ยังไม่มีคน", a table. On the shift on the clock: the Areas with nobody, from the same
 * board as the attendance page (facility-0050). On a shift not started: every Area that works it, with its regular
 * Cleaner, so the Admin can cover a known absence before leaving (facility-0075, 0084). The search only narrows the rows
 * on screen.
 */
export function EmptyAreas({ board, error, slots, slot, ahead, onSlot, onChoose, children }: EmptyAreasProps) {
  const [search, setSearch] = useState('');
  const rows = board ? board.rows.filter((row) => ahead || row.state === 'Empty') : [];
  const shown = rows.filter((row) => matchesSearch(row, search));
  const current = slots[0];

  return (
    <section className={`${panelClass} flex-[999_1_560px]`} aria-labelledby="alerts-empty">
      <div className={panelHeadClass}>
        <div className="flex flex-col gap-0.5">
          <h2 id="alerts-empty" className={panelTitleClass}>
            <UserX aria-hidden="true" className="size-5 text-overdue" />
            {ahead ? 'พื้นที่ของกะที่ยังไม่เริ่ม' : 'พื้นที่ที่ยังไม่มีคน'}
            <span className={`${countClass} ${countTones.overdue}`}>{rows.length}</span>
          </h2>
          {ahead && board && (
            <span className="text-xs text-text-2">{`${slotText(board)} ยังไม่เริ่ม จึงแสดงทุกพื้นที่ที่ทำกะนี้พร้อมแม่บ้านประจำ`}</span>
          )}
        </div>
        <div className="flex flex-wrap gap-2">
          {current && slot && (
            <Select
              size="desk"
              aria-label="วันและกะ"
              value={slotKey(slot)}
              onChange={(event) => onSlot(slots.find((option) => slotKey(option) === event.target.value) ?? current)}
            >
              {slots.map((option) => (
                <option key={slotKey(option)} value={slotKey(option)}>
                  {sameSlot(option, current) ? `${slotText(option)} (กะนี้)` : slotText(option)}
                </option>
              ))}
            </Select>
          )}
          <SearchBox label="ค้นหาแม่บ้านหรือพื้นที่" value={search} onChange={setSearch} className="w-55" />
        </div>
      </div>
      {error != null && (
        <div className="p-4">
          <Note tone="error">{getErrorMessage(error)}</Note>
        </div>
      )}
      {board && !ahead && rows.length === 0 && (
        <div className="flex flex-col items-center gap-1 px-4 py-8 text-center">
          <Check aria-hidden="true" className="size-7 text-passed" />
          <strong>ทุกพื้นที่มีคนแล้ว</strong>
          <span className="text-text-2">พื้นที่ที่ยังไม่มีคนลงเวลาจะขึ้นที่นี่</span>
        </div>
      )}
      {board && ahead && rows.length === 0 && (
        <p className="px-4 py-8 text-center text-text-2">ไม่มีพื้นที่ที่ทำกะนี้</p>
      )}
      {rows.length > 0 && shown.length === 0 && (
        <p className="px-4 py-8 text-center text-text-2">ไม่พบพื้นที่ที่ตรงกับที่ค้นหา</p>
      )}
      {shown.length > 0 && (
        <Table framed={false} minWidth="min-w-140">
          <thead>
            <tr>
              <th scope="col">พื้นที่</th>
              <th scope="col">ตึก</th>
              <th scope="col">แม่บ้านประจำ</th>
              <th scope="col" className={tableCell.actions}>
                <span className="sr-only">การกระทำ</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {shown.map((row) => (
              <tr key={row.areaId}>
                <td>
                  <strong>{row.areaCode}</strong> {row.areaName}
                </td>
                <td className="whitespace-nowrap text-text-3">{row.buildingCode}</td>
                <td>
                  <Cleaner row={row} ahead={ahead} />
                </td>
                <td className={tableCell.actions}>
                  {row.covers.length > 0 ? (
                    // Owner, 2026-10-22: a covered Area ahead names who covers and has no assign button, as on the
                    // shift on the clock, where it leaves the list. To change who, cancel in the covers box first
                    <span>{`ทำแทนแล้ว: ${row.covers.map((cover) => cover.displayName).join(', ')}`}</span>
                  ) : (
                    <Button
                      size="desk"
                      aria-label={`มอบหมายคนทำแทน ${row.areaCode}`}
                      onClick={() => onChoose(row.areaId)}
                    >
                      <ArrowRightLeft aria-hidden="true" className="size-4" />
                      มอบหมายคนทำแทน
                    </Button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
      {children}
    </section>
  );
}

/** On the shift on the clock the regular Cleaner has not checked in; ahead, nobody has yet. */
function Cleaner({ row, ahead }: { row: BoardRow; ahead: boolean }) {
  if (!row.cleaner) {
    return (
      <span className="flex flex-col">
        <span>ไม่มีแม่บ้านประจำกะนี้</span>
        <span className="text-xs font-semibold text-overdue">ต้องมอบหมายคนทำแทน</span>
      </span>
    );
  }
  return (
    <span className="flex flex-col">
      <span>{row.cleaner.displayName}</span>
      {!ahead && <span className="text-xs font-semibold text-overdue">ยังไม่ลงเวลา</span>}
    </span>
  );
}
