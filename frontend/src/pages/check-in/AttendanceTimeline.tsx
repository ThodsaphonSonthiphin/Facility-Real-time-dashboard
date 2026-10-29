import { Check } from 'lucide-react';
import type { AttendanceEvent, AttendanceState } from '../../shared/api/types';
import { attendanceEvents, eventLabel } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';

/**
 * The design file's CheckIn list: four rows, a recorded one with a green tick and its time, the one the button below
 * records next on a pale blue ground with a blue number, the others grey. Every step not yet recorded reads "–": the
 * buttons are the only thing that says what may come next (facility-0078), and `next` only colours the first of them.
 */
export function AttendanceTimeline({ attendance, next }: { attendance: AttendanceState; next?: AttendanceEvent }) {
  return (
    <ol
      className="m-0 flex list-none flex-col gap-px overflow-hidden rounded-lg border border-border bg-border p-0"
      aria-label="วันนี้"
    >
      {attendanceEvents.map((eventType, index) => {
        const recorded = attendance.events.find((e) => e.eventType === eventType);
        const isNext = !recorded && eventType === next;
        const dot = recorded
          ? 'bg-passed text-on-accent'
          : isNext
            ? 'bg-accent text-on-accent'
            : 'border border-border bg-idle-bg text-idle';
        return (
          <li
            key={eventType}
            className={`flex min-h-18 items-center gap-3 px-4 py-3 ${isNext ? 'bg-accent-subtle' : 'bg-surface'}`}
          >
            <span
              aria-hidden="true"
              // The number is drawn from data-n, so it is not part of the row's text
              data-n={index + 1}
              className={`flex size-8 shrink-0 items-center justify-center rounded-full text-[15px] font-bold ${recorded ? '' : 'before:content-[attr(data-n)]'} ${dot}`}
            >
              {recorded && <Check className="size-4" />}
            </span>
            <span className="flex-1 text-lg font-semibold">{eventLabel[eventType]}</span>{' '}
            <span
              className={`tabular-nums ${recorded ? 'font-semibold text-passed' : isNext ? 'font-semibold text-accent' : 'text-text-2'}`}
            >
              {recorded ? formatTime(recorded.occurredAt) : '–'}
            </span>
            {recorded?.withinRadius === false && (
              <>
                {' '}
                <span className="font-bold text-overdue">ไกลป้าย</span>
              </>
            )}
          </li>
        );
      })}
    </ol>
  );
}
