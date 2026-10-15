import type { AttendanceState } from '../../shared/api/types';
import { attendanceEvents, eventLabel } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';

/** The "วันนี้" row of M1 and M10: each event's time, "ถัดไป" for what may come next, a flag mark (facility-0035). */
export function AttendanceTimeline({ attendance }: { attendance: AttendanceState }) {
  return (
    <ol className="checkin-timeline" aria-label="วันนี้">
      {attendanceEvents.map((eventType) => {
        const recorded = attendance.events.find((e) => e.eventType === eventType);
        const next = attendance.nextEvents.includes(eventType);
        const className = recorded ? 'checkin-step checkin-step--done' : next ? 'checkin-step checkin-step--next' : 'checkin-step';
        return (
          <li key={eventType} className={className}>
            <span>{eventLabel[eventType]}</span>{' '}
            <span>{recorded ? formatTime(recorded.occurredAt) : next ? 'ถัดไป' : '–'}</span>
            {recorded?.withinRadius === false && (
              <>
                {' '}
                <span className="checkin-flag">ติดธง</span>
              </>
            )}
          </li>
        );
      })}
    </ol>
  );
}
