import { useState } from 'react';
import { useGetBuildingsQuery } from '../../shared/api/adminSetupApi';
import type { AttendanceBoard, BoardArgs } from '../../shared/api/attendanceBoardApi';
import type { Shift } from '../../shared/api/types';
import { shiftShortLabel } from '../../shared/utils/labels';
import { parseShiftDate } from '../../shared/utils/thaiTime';

interface BoardControlsProps {
  board?: AttendanceBoard;
  args: BoardArgs;
  search: string;
  onSearch: (text: string) => void;
  /** Show this shift of this date. */
  onSlot: (shiftDate: string, shift: Shift) => void;
  /** Back to the shift on the clock. */
  onNow: () => void;
  onBuilding: (buildingId: number | undefined) => void;
}

/** Wireframe D2's controls: the shift, a typed date (plan 11 decision 13), the building and the search. */
export function BoardControls({ board, args, search, onSearch, onSlot, onNow, onBuilding }: BoardControlsProps) {
  const { data: buildings } = useGetBuildingsQuery();
  const [dateText, setDateText] = useState('');
  const [dateError, setDateError] = useState<string | null>(null);

  const showDate = () => {
    const date = parseShiftDate(dateText);
    if (!date) {
      setDateError('วันที่ต้องเป็นแบบ วว/ดด/ปปปป เช่น 15/10/2026');
      return;
    }
    setDateError(null);
    onSlot(date, board?.shift ?? 'Day');
  };

  return (
    <div className="attendance-controls">
      <div className="attendance-shifts" role="group" aria-label="กะ">
        {(['Day', 'Night'] as const).map((shift) => (
          <button
            key={shift}
            type="button"
            className="attendance-shift"
            aria-pressed={board?.shift === shift}
            disabled={!board}
            onClick={() => board && onSlot(board.shiftDate, shift)}
          >
            {shiftShortLabel[shift]}
          </button>
        ))}
      </div>
      <label className="attendance-control">
        วันที่
        <input
          className="app-input"
          value={dateText}
          placeholder="วว/ดด/ปปปป"
          inputMode="numeric"
          onChange={(event) => setDateText(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              showDate();
            }
          }}
        />
      </label>
      <button type="button" className="app-button" onClick={showDate}>
        ดูวันที่นี้
      </button>
      {args.shiftDate && (
        <button type="button" className="app-button" onClick={onNow}>
          กะปัจจุบัน
        </button>
      )}
      <label className="attendance-control">
        ตึก
        <select
          className="app-input"
          value={args.buildingId ?? ''}
          onChange={(event) => onBuilding(event.target.value === '' ? undefined : Number(event.target.value))}
        >
          <option value="">ทุกตึก</option>
          {(buildings ?? []).map((building) => (
            <option key={building.id} value={building.id}>
              {building.code}
            </option>
          ))}
        </select>
      </label>
      <label className="attendance-control">
        ค้นหา
        <input
          className="app-input"
          type="search"
          value={search}
          placeholder="ชื่อพื้นที่หรือแม่บ้าน"
          onChange={(event) => onSearch(event.target.value)}
        />
      </label>
      {dateError && (
        <p className="app-note app-note--error" role="alert">
          {dateError}
        </p>
      )}
    </div>
  );
}
