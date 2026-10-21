import { RangeFields } from '../../shared/components/RangeFields';
import type { RangeTexts } from '../../shared/utils/dateRange';
import type { AuditActor, HistoryArgs } from './historyApi';
import { auditCategories, categoryLabel } from './historyRules';

interface HistoryControlsProps {
  args: HistoryArgs;
  actors?: AuditActor[];
  texts: RangeTexts;
  onTexts: (next: RangeTexts) => void;
  /** Check the typed range and show it. */
  onShow: () => void;
  /** The actor or type changed: ask at once. */
  onArgs: (next: HistoryArgs) => void;
  search: string;
  onSearch: (text: string) => void;
}

/** Wireframe D9's filters: the range (typed), ผู้ทำ and ประเภท (facility-0057 rule 2). */
export function HistoryControls({ args, actors, texts, onTexts, onShow, onArgs, search, onSearch }: HistoryControlsProps) {
  return (
    <div className="history-controls">
      <RangeFields value={texts} onChange={onTexts} onEnter={onShow} />
      <button type="button" className="app-button" onClick={onShow}>
        แสดง
      </button>
      <label className="app-field">
        ผู้ทำ
        <select
          className="app-input"
          value={args.actorId ?? ''}
          onChange={(event) =>
            onArgs({ ...args, actorId: event.target.value === '' ? undefined : Number(event.target.value) })
          }
        >
          <option value="">ทุกคน</option>
          {(actors ?? []).map((actor) => (
            <option key={actor.id} value={actor.id}>
              {actor.displayName}
            </option>
          ))}
        </select>
      </label>
      <label className="app-field">
        ประเภท
        <select
          className="app-input"
          value={args.category ?? ''}
          onChange={(event) => onArgs({ ...args, category: auditCategories.find((each) => each === event.target.value) })}
        >
          <option value="">ทุกประเภท</option>
          {auditCategories.map((category) => (
            <option key={category} value={category}>
              {categoryLabel[category]}
            </option>
          ))}
        </select>
      </label>
      <div className="history-search-field">
        <label className="app-field">
          ค้นหา
          <input
            className="app-input history-search"
            type="search"
            value={search}
            placeholder="ค้นหาชื่อคน จุด หรือรหัส"
            onChange={(event) => onSearch(event.target.value)}
          />
        </label>
        <span className="app-field__help">ค้นหาเฉพาะรายการที่โหลดแล้ว</span>
      </div>
    </div>
  );
}
