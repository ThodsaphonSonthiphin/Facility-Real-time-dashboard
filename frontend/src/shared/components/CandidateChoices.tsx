import { Search } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import type { CoverCandidate } from '../api/alertsApi';
import { TextInput } from './Field';
import { choiceCardClass } from '../utils/coverCandidates';

/** A search box with the magnifier inside it (the design file's search fields). */
export function SearchBox({
  label,
  value,
  onChange,
  className = 'w-full',
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  className?: string;
}) {
  return (
    <span className={`relative flex items-center ${className}`}>
      <Search aria-hidden="true" className="pointer-events-none absolute left-2.5 size-4 text-text-2" />
      <TextInput
        size="desk"
        type="search"
        aria-label={label}
        placeholder={label}
        className="w-full pl-8.5"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </span>
  );
}

interface CandidateChoicesProps {
  /** The radio group's name for a screen reader, such as แม่บ้านที่ทำแทน. */
  label: string;
  candidates: readonly CoverCandidate[];
  /** The one chosen; the server lists the best first, so that one is chosen until the Admin picks. */
  value: number | undefined;
  onChange: (id: number) => void;
  /** The two lines under a candidate's name, and the tag on the right of it. */
  describe: (candidate: CoverCandidate) => { own: string; why?: string; tag?: ReactNode };
  disabled?: boolean;
}

/**
 * The Cleaners who can cover, as radio cards with a search by name or employee ID (the design file's cover forms). The
 * first is marked เหมาะที่สุด: the server sorts by who already scanned this Area, then who checked in first.
 */
export function CandidateChoices({
  label,
  candidates,
  value,
  onChange,
  describe,
  disabled = false,
}: CandidateChoicesProps) {
  const [search, setSearch] = useState('');
  const words = search.trim().toLowerCase();
  const shown = candidates.filter(
    (candidate) =>
      words === '' ||
      candidate.displayName.toLowerCase().includes(words) ||
      candidate.employeeId.toLowerCase().includes(words),
  );
  const best = candidates[0]?.id;

  return (
    <div className="flex flex-col gap-2">
      <SearchBox label="ค้นหาชื่อหรือรหัสพนักงาน" value={search} onChange={setSearch} />
      <div role="radiogroup" aria-label={label} className="flex flex-col gap-2">
        {shown.map((candidate) => {
          const { own, why, tag } = describe(candidate);
          return (
            <label key={candidate.id} className={choiceCardClass}>
              <input
                type="radio"
                className="sr-only"
                name={label}
                value={candidate.id}
                checked={value === candidate.id}
                disabled={disabled}
                onChange={() => onChange(candidate.id)}
              />
              <span className="flex w-full items-center justify-between gap-2">
                <strong>{candidate.displayName}</strong>
                {candidate.id === best ? (
                  <span className="inline-flex min-h-5.5 items-center rounded-sm bg-accent-subtle px-2 text-xs font-semibold text-accent-hover">
                    เหมาะที่สุด
                  </span>
                ) : (
                  tag
                )}
              </span>
              <span className="text-xs text-text-3">{own}</span>
              {why && <span className="text-xs text-text-2">{why}</span>}
            </label>
          );
        })}
      </div>
      {candidates.length > 0 && shown.length === 0 && <span className="text-text-2">ไม่พบชื่อที่ค้นหา</span>}
    </div>
  );
}
