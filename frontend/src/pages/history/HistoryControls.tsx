import { Button } from '../../shared/components/Button';
import { Field, Select, TextInput } from '../../shared/components/Field';
import { RangeFields } from '../../shared/components/RangeFields';
import type { RangeTexts } from '../../shared/utils/dateRange';
import type { AuditActor, HistoryArgs } from './historyApi';
import { auditCategories, categoryLabel, HISTORY_MAX_SEARCH } from './historyRules';

interface HistoryControlsProps {
  args: HistoryArgs;
  actors?: AuditActor[];
  texts: RangeTexts;
  onTexts: (next: RangeTexts) => void;
  /** Check the typed range and show it with the typed search. */
  onShow: () => void;
  /** The actor or type changed: ask at once. */
  onArgs: (next: HistoryArgs) => void;
  search: string;
  onSearch: (text: string) => void;
}

/** Wireframe D9's filters: the range (typed), ผู้ทำ, ประเภท (facility-0057 rule 2) and the search, sent with แสดง or Enter. */
export function HistoryControls({
  args,
  actors,
  texts,
  onTexts,
  onShow,
  onArgs,
  search,
  onSearch,
}: HistoryControlsProps) {
  return (
    <div className="flex flex-wrap items-end gap-2">
      <RangeFields value={texts} onChange={onTexts} onEnter={onShow} />
      <Button size="desk" onClick={onShow}>
        แสดง
      </Button>
      <Field label="ผู้ทำ">
        <Select
          size="desk"
          value={args.actorId ?? ''}
          onChange={(event) =>
            onArgs({
              ...args,
              actorId: event.target.value === '' ? undefined : Number(event.target.value),
            })
          }
        >
          <option value="">ทุกคน</option>
          {(actors ?? []).map((actor) => (
            <option key={actor.id} value={actor.id}>
              {actor.displayName}
            </option>
          ))}
        </Select>
      </Field>
      <Field label="ประเภท">
        <Select
          size="desk"
          value={args.category ?? ''}
          onChange={(event) =>
            onArgs({
              ...args,
              category: auditCategories.find((each) => each === event.target.value),
            })
          }
        >
          <option value="">ทุกประเภท</option>
          {auditCategories.map((category) => (
            <option key={category} value={category}>
              {categoryLabel[category]}
            </option>
          ))}
        </Select>
      </Field>
      <Field label="ค้นหา">
        <TextInput
          size="desk"
          className="w-60"
          type="search"
          value={search}
          maxLength={HISTORY_MAX_SEARCH}
          placeholder="ค้นหาชื่อคน จุด หรือรหัส"
          onChange={(event) => onSearch(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter') {
              onShow();
            }
          }}
        />
      </Field>
    </div>
  );
}
