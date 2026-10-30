import type { AuditEntry } from './historyApi';
import { changeValues } from './historyRules';

/** Snapshot text is rendered as React text, including literal arrows and HTML-like names. */
export function HistoryChangeValue({ entry }: { entry: AuditEntry }) {
  const changes = changeValues(entry);
  if (changes.length === 0) return <>–</>;
  return (
    <ul className="flex flex-col gap-1.5">
      {changes.map((change, index) => (
        <li key={index} className="flex flex-wrap items-center gap-1.5">
          {change.label && <span className="text-xs text-text-2">{`${change.label}: `}</span>}
          <span className="rounded-sm bg-rework-bg px-1.5 py-0.5 text-rework line-through">{change.before}</span>
          <span className="text-text-2">{' → '}</span>
          <span className="rounded-sm bg-passed-bg px-1.5 py-0.5 font-semibold text-passed-strong">{change.after}</span>
        </li>
      ))}
    </ul>
  );
}
