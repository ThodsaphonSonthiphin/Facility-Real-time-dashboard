import { useGetBuildingsQuery } from '../../shared/api/adminSetupApi';
import type { AttendanceBoard, BoardArgs } from '../../shared/api/attendanceBoardApi';
import { buildingOptionText } from '../../shared/api/buildingNames';
import type { Shift } from '../../shared/api/types';
import { SearchBox } from '../../shared/components/CandidateChoices';
import { Select, TextInput } from '../../shared/components/Field';
import { Segmented } from '../../shared/components/Segmented';
import { shiftLabel } from '../../shared/utils/labels';

const shiftOptions = (['Day', 'Night'] as const).map((shift) => ({ value: shift, label: shiftLabel[shift] }));

interface BoardControlsProps {
  board?: AttendanceBoard;
  args: BoardArgs;
  search: string;
  onSearch: (text: string) => void;
  /** Show this shift of this date. */
  onSlot: (shiftDate: string, shift: Shift) => void;
  onBuilding: (buildingId: number | undefined) => void;
}

/**
 * The design file's D2 filter row: the shift, the date (the browser's calendar, owner 2026-10-29), the building, each
 * with its label beside it, then the search (kept on the owner's word, at the end of the row). A date picked shows that
 * date at once; the shift on the clock is its date and shift.
 */
export function BoardControls({ board, args, search, onSearch, onSlot, onBuilding }: BoardControlsProps) {
  const { data: buildings } = useGetBuildingsQuery();
  const date = board?.shiftDate ?? args.shiftDate ?? '';

  return (
    <section aria-label="ตัวกรอง" className="flex flex-wrap items-center gap-3">
      <Segmented
        label="กะ"
        options={shiftOptions}
        value={board?.shift}
        disabled={!board}
        onChange={(shift) => board && onSlot(board.shiftDate, shift)}
      />
      <label className="inline-flex items-center gap-2">
        วันที่
        <TextInput
          size="desk"
          type="date"
          value={date}
          disabled={!board}
          onChange={(event) => {
            // A date box gives "" while a part of the date is cleared; only a whole date is shown
            if (/^\d{4}-\d{2}-\d{2}$/.test(event.target.value)) {
              onSlot(event.target.value, board?.shift ?? 'Day');
            }
          }}
        />
      </label>
      <label className="inline-flex items-center gap-2">
        ตึก
        <Select
          size="desk"
          value={args.buildingId ?? ''}
          onChange={(event) => onBuilding(event.target.value === '' ? undefined : Number(event.target.value))}
        >
          <option value="">ทุกตึก</option>
          {(buildings ?? []).map((building) => (
            <option key={building.id} value={building.id}>
              {buildingOptionText(building)}
            </option>
          ))}
        </Select>
      </label>
      <SearchBox label="ค้นหาพื้นที่หรือแม่บ้าน" value={search} onChange={onSearch} className="ml-auto w-60" />
    </section>
  );
}
