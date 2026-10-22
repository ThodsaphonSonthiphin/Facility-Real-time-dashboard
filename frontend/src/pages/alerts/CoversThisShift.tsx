import type { AttendanceBoard } from '../../shared/api/attendanceBoardApi';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { formatTime } from '../../shared/utils/thaiTime';
import type { Notice } from './AlertItem';
import { slotText } from './alertRules';
import { useCancelCoverMutation } from './alertsApi';

interface CoversThisShiftProps {
  board: AttendanceBoard;
  /** A shift not started (facility-0075). */
  ahead: boolean;
  onNotice: (notice: Notice) => void;
}

/**
 * Wireframe D3 "คนทำแทนกะนี้": every Area cover of the shift on screen, from its board: the shift on the clock, or one
 * picked up to 7 days ahead (facility-0075, 0084). Each ends with the shift by itself, or is cancelled in one press.
 * Supervisors' building covers have no list endpoint (plan 11 decision 10).
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
    <section className="alerts-section" aria-labelledby="alerts-covers">
      <h2 id="alerts-covers">{`${ahead ? `คนทำแทน${slotText(board)}` : 'คนทำแทนกะนี้'} (${covers.length})`}</h2>
      {covers.length === 0 && <p className="app-muted">ยังไม่มีคนทำแทนกะนี้</p>}
      <ul className="alerts-rows">
        {covers.map(({ row, cover }) => (
          <li key={cover.id} className="alerts-row">
            <span>
              <strong>{cover.displayName}</strong>
              {` ทำแทน ${row.areaCode}`}
              <span className="app-muted">
                {` · แทน ${row.cleaner ? row.cleaner.displayName : 'ไม่มีแม่บ้านประจำ'} · แอดมินมอบหมาย ${formatTime(cover.assignedAt)}`}
              </span>
            </span>
            <span className="app-muted">หมดสิทธิ์เมื่อจบกะ</span>
            <button
              type="button"
              className="app-link-button"
              aria-label={`ยกเลิกการทำแทนของ ${cover.displayName} ที่ ${row.areaCode}`}
              disabled={isLoading}
              onClick={() => void cancelCover(cover.id, `ยกเลิกการทำแทนของ ${cover.displayName} ที่ ${row.areaCode} แล้ว`)}
            >
              {ahead ? 'ยกเลิก' : 'ยกเลิกก่อนหมดกะ'}
            </button>
          </li>
        ))}
      </ul>
      <p className="app-muted">การตรวจแทนตึกของหัวหน้ายังไม่แสดงในหน้านี้</p>
    </section>
  );
}
