import type { AttendanceState } from '../../shared/api/types';
import { attendanceEvents, eventLabel } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';

/**
 * The "วันนี้" row of M1 and M10: what is already recorded, with its time and a far-from-sign mark (facility-0035).
 * Every other step reads "–": the buttons below are the only thing that says what may come next (facility-0078).
 */
export function AttendanceTimeline({ attendance }: { attendance: AttendanceState }) {
  return (
    <ol className="checkin-timeline" aria-label="วันนี้">
      {attendanceEvents.map((eventType) => {
        const recorded = attendance.events.find((e) => e.eventType === eventType);
        return (
          <li key={eventType} className={recorded ? 'checkin-step checkin-step--done' : 'checkin-step'}>
            <span>{eventLabel[eventType]}</span> <span>{recorded ? formatTime(recorded.occurredAt) : '–'}</span>
            {recorded?.withinRadius === false && (
              <>
                {' '}
                <span className="checkin-flag">ไกลป้าย</span>
              </>
            )}
          </li>
        );
      })}
    </ol>
  );
}
