import { Check } from 'lucide-react';
import type { AttendanceAction, AttendanceEvent, AttendanceState } from '../../shared/api/types';
import { actionStateLabel, eventLabel, lifecycleEvents } from '../../shared/utils/labels';
import { formatTime } from '../../shared/utils/thaiTime';

/** One drawn action: what it is, and the actual time only when the server says there is one. */
interface TimelineRow extends AttendanceAction {
  withinRadius?: boolean;
}

/**
 * Spec §8: the slot's own ordered lifecycle, read from the server's `actions` so a legacy slot keeps its four rows and a
 * numbered slot shows six. A kind with no scan (`skipped`, `missing`, `not-applicable`) never invents a time; the plan's
 * own `expectedAt` is shown apart from it. A page answered without `actions` (an older answer) falls back to the slot's
 * events and the allowed list, which is exactly what the four-row timeline always showed.
 */
function timelineRows(attendance: AttendanceState): TimelineRow[] {
  if (attendance.actions?.length) {
    return attendance.actions.map((action) => ({
      ...action,
      withinRadius: attendance.events.find((event) => event.eventType === action.eventType)?.withinRadius ?? undefined,
    }));
  }
  return lifecycleEvents(attendance.lifecycleVersion).map((eventType) => {
    const event = attendance.events.find((entry) => entry.eventType === eventType);
    return {
      eventType,
      state: event ? 'recorded' : 'pending',
      occurredAt: event?.occurredAt,
      source: event?.source,
      withinRadius: event?.withinRadius ?? undefined,
    };
  });
}

/**
 * The design file's CheckIn list: one row per action of the slot's lifecycle, a recorded one with a green tick and its
 * time, the one the button below records next on a pale blue ground with a blue number, and the rest grey. Every action
 * without an actual fact shows its status, including explicit skips and missing scans, and says
 * "ไม่ต้องลงเวลาเนื่องจากลา" once a recorded departure and leave make it unnecessary (spec §7).
 */
export function AttendanceTimeline({ attendance, next }: { attendance: AttendanceState; next?: AttendanceEvent }) {
  return (
    <ol
      className="m-0 flex list-none flex-col gap-px overflow-hidden rounded-lg border border-border bg-border p-0"
      aria-label="วันนี้"
    >
      {timelineRows(attendance).map((row, index) => {
        const actualAt = row.state === 'recorded' ? row.occurredAt : undefined;
        const recorded = actualAt != null;
        const onLeave = row.state === 'not-applicable';
        const isNext = !recorded && row.eventType === next && !onLeave;
        const dot = recorded
          ? 'bg-passed text-on-accent'
          : isNext
            ? 'bg-accent text-on-accent'
            : 'border border-border bg-idle-bg text-idle';
        return (
          <li
            key={row.eventType}
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
            <span className="flex min-w-0 flex-1 flex-col">
              <span className="text-lg font-semibold">{eventLabel[row.eventType]}</span>
              {recorded && row.source === 'SupervisorAdd' && <span className="text-xs text-text-2">หัวหน้าเติมเวลา</span>}
              {recorded && (row.source === 'AdminAdd' || row.source === 'AdminEdit') && (
                <span className="text-xs text-text-2">แอดมินแก้เวลา</span>
              )}
              {onLeave && <span className="text-text-2">{actionStateLabel[row.state]}</span>}
              {!recorded && !onLeave && row.expectedAt && (
                <span className="text-xs text-text-2">{`แผน ${formatTime(row.expectedAt)}`}</span>
              )}
            </span>{' '}
            {!onLeave && (
              <span
                className={`text-right tabular-nums ${
                  recorded ? 'font-semibold text-passed' : isNext ? 'font-semibold text-accent' : 'text-text-2'
                }`}
              >
                {recorded ? formatTime(actualAt) : actionStateLabel[row.state]}
              </span>
            )}
            {row.withinRadius === false && (
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
