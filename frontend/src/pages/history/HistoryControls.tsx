import { Search } from 'lucide-react';
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
  onShow: () => void;
  onArgs: (next: HistoryArgs) => void;
  search: string;
  onSearch: (text: string) => void;
  preset: string;
  onPreset: (value: string) => void;
}

/**
 * Audit history filters with always-visible search box matching Dashboard, Attendance, and Accounts (Heuristics #4, #6, #7).
 */
export function HistoryControls({ args, actors, texts, onTexts, onShow, onArgs, search, onSearch, preset, onPreset }: HistoryControlsProps) {
  return (
    <section aria-label="ตัวกรองประวัติ" className="flex min-w-0 flex-col gap-3 rounded-lg border border-border bg-surface p-4">
      <div className="grid grid-cols-1 items-end gap-3 sm:grid-cols-2 lg:flex lg:flex-wrap">
        <Field label="ช่วงวัน">
          <Select size="adaptive" className="w-full lg:w-auto" value={preset} onChange={(event) => onPreset(event.target.value)}>
            <option value="week">7 วันล่าสุด</option>
            <option value="30">30 วันล่าสุด</option>
            <option value="custom">กำหนดเอง</option>
          </Select>
        </Field>
        <Field label="ผู้ทำ">
          <Select size="adaptive" className="w-full lg:w-auto" value={args.actorId ?? ''} onChange={(event) => onArgs({ ...args, actorId: event.target.value === '' ? undefined : Number(event.target.value) })}>
            <option value="">ทุกคน</option>
            {(actors ?? []).map((actor) => <option key={actor.id} value={actor.id}>{actor.displayName}</option>)}
          </Select>
        </Field>
        <Field label="ประเภท">
          <Select size="adaptive" className="w-full min-w-0 max-w-full lg:w-auto" value={args.category ?? ''} onChange={(event) => onArgs({ ...args, category: auditCategories.find((each) => each === event.target.value) })}>
            <option value="">ทุกประเภท</option>
            {auditCategories.map((category) => <option key={category} value={category}>{categoryLabel[category]}</option>)}
          </Select>
        </Field>
        <div className="flex min-w-0 items-end gap-2 sm:col-span-2 lg:flex-1 lg:basis-60">
          <label className="relative flex min-w-0 flex-1 items-center">
            <Search aria-hidden="true" className="pointer-events-none absolute left-2.5 size-4 text-text-2" />
            <TextInput
              size="adaptive"
              className="w-full pl-8.5"
              type="search"
              aria-label="ค้นหา"
              value={search}
              maxLength={HISTORY_MAX_SEARCH}
              placeholder="ค้นหาชื่อคน จุด หรือรหัส"
              onChange={(event) => onSearch(event.target.value)}
              onKeyDown={(event) => {
                if (event.key !== 'Enter' || event.nativeEvent.isComposing || event.nativeEvent.keyCode === 229) {
                  return;
                }
                event.preventDefault();
                onShow();
              }}
            />
          </label>
          <Button size="adaptive" onClick={onShow}>ค้นหา</Button>
        </div>
      </div>
      {preset === 'custom' && (
        <div className="flex flex-wrap items-end gap-2 border-t border-border pt-3">
          <RangeFields value={texts} onChange={onTexts} onEnter={onShow} />
          <Button size="adaptive" className="w-full sm:w-auto" onClick={onShow}>แสดง</Button>
        </div>
      )}
    </section>
  );
}
