import type { AttendanceState } from '../../shared/api/types';
import { boxClass } from '../../shared/components/styles';
import { attendanceEvents, eventLabel } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';

// A recorded step has a solid frame and dark text; a step still to come is dashed and muted
const stepClass = {
  done: 'flex flex-col gap-0.5 rounded border border-border px-0.5 py-2 font-semibold text-text',
  open: 'flex flex-col gap-0.5 rounded border border-dashed border-input-border px-0.5 py-2 text-muted',
};

/**
 * The "วันนี้" row of M1 and M10: what is already recorded, with its time and a far-from-sign mark (facility-0035).
 * Every other step reads "–": the buttons below are the only thing that says what may come next (facility-0078).
 */
export function AttendanceTimeline({ attendance }: { attendance: AttendanceState }) {
  return (
    <ol className={`m-0 grid list-none grid-cols-4 gap-1.5 text-center text-md ${boxClass}`} aria-label="วันนี้">
      {attendanceEvents.map((eventType) => {
        const recorded = attendance.events.find((e) => e.eventType === eventType);
        return (
          <li key={eventType} className={recorded ? stepClass.done : stepClass.open}>
            <span>{eventLabel[eventType]}</span> <span>{recorded ? formatTime(recorded.occurredAt) : '–'}</span>
            {recorded?.withinRadius === false && (
              <>
                {' '}
                <span className="font-bold text-warn-text">ไกลป้าย</span>
              </>
            )}
          </li>
        );
      })}
    </ol>
  );
}
