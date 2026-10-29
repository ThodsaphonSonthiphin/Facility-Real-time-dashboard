import { ArrowRightLeft } from 'lucide-react';
import type { AttendanceBoard } from '../../shared/api/attendanceBoardApi';
import { Button } from '../../shared/components/Button';
import type { Notice } from '../../shared/components/Toast';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { shiftEndText } from '../../shared/utils/coverSlots';
import { formatDayTime, formatTime } from '../../shared/utils/thaiTime';
import { slotText } from './alertRules';
import { useCancelCoverMutation } from '../../shared/api/alertsApi';
import { countClass, countTones, panelClass, panelTitleClass } from './alertStyles';

interface CoversThisShiftProps {
  board: AttendanceBoard;
  /** A shift not started (facility-0075). */
  ahead: boolean;
  onNotice: (notice: Notice) => void;
}

/**
 * The design file's D3 "คนทำแทนกะนี้", beside the Areas with nobody: every Area cover of the shift on screen, from its
 * board: the shift on the clock, or one picked up to 7 days ahead (facility-0075, 0084). Each ends with the shift by
 * itself, or is cancelled in one press. Supervisors' building covers have no list endpoint (plan 11 decision 10).
 */
export function CoversThisShift({ board, ahead, onNotice }: CoversThisShiftProps) {
  const [cancel, { isLoading }] = useCancelCoverMutation();
  const covers = board.rows.flatMap((row) => row.covers.map((cover) => ({ row, cover })));

  const cancelCover = async (id: number, text: string) => {
    try {
      await cancel(id).unwrap();
      onNotice({ kind: 'ok', text });
    } catch (error) {
      onNotice({ kind: 'error', text: getErrorMessage(error) });
    }
  };

  return (
    <section className={`${panelClass} flex-[1_1_320px]`} aria-labelledby="alerts-covers">
      <div className="flex flex-col gap-0.5 border-b border-border p-4">
        <h2 id="alerts-covers" className={panelTitleClass}>
          <ArrowRightLeft aria-hidden="true" className="size-5 text-break" />
          {ahead ? `คนทำแทน${slotText(board)}` : 'คนทำแทนกะนี้'}
          <span className={`${countClass} ${countTones.break}`}>{covers.length}</span>
        </h2>
        <span className="text-xs text-text-2">{`หมดสิทธิ์เมื่อจบกะ ${shiftEndText[board.shift]}`}</span>
      </div>
      {covers.length === 0 && (
        <p className="px-4 py-6 text-text-2">{ahead ? `ยังไม่มีคนทำแทน${slotText(board)}` : 'ยังไม่มีคนทำแทนกะนี้'}</p>
      )}
      <ul className="m-0 flex list-none flex-col p-0">
        {covers.map(({ row, cover }) => (
          <li key={cover.id} className="flex flex-col gap-0.5 border-b border-border px-4 py-3 last:border-b-0">
            <span>
              <strong>{cover.displayName}</strong> ทำแทน <strong>{`${row.areaCode} ${row.areaName}`}</strong>
            </span>
            <span className="text-xs text-text-2">
              {`แทน ${row.cleaner ? row.cleaner.displayName : 'ไม่มีแม่บ้านประจำ'} · แอดมินมอบหมาย ${ahead ? formatDayTime(cover.assignedAt) : formatTime(cover.assignedAt)}`}
            </span>
            <Button
              variant="link"
              size="desk"
              className="self-start"
              aria-label={`ยกเลิกการทำแทนของ ${cover.displayName} ที่ ${row.areaCode}`}
              disabled={isLoading}
              onClick={() =>
                void cancelCover(cover.id, `ยกเลิกการทำแทนของ ${cover.displayName} ที่ ${row.areaCode} แล้ว`)
              }
            >
              {ahead ? 'ยกเลิก' : 'ยกเลิกก่อนหมดกะ'}
            </Button>
          </li>
        ))}
      </ul>
    </section>
  );
}
