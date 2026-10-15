import { useState, type FormEvent } from 'react';
import type { PointCard } from '../../shared/api/types';
import { formatTime } from '../../shared/utils/thaiTime';
import { PointHeading } from './PointHeading';
import { isComplete, issueTags, type ScanDraft } from './scanRules';

interface SubmitFormProps {
  point: PointCard;
  draft: ScanDraft;
  onChange: (draft: ScanDraft) => void;
  sending: boolean;
  /** The server's refusal of the last send, shown as it is (facility-0022 item 4). */
  errorText: string | null;
  onSend: () => void;
}

/** Wireframe M4: เรียบร้อย is chosen; พบปัญหา opens the tags and the note (facility-0007, 0046). */
export function SubmitForm({ point, draft, onChange, sending, errorText, onSend }: SubmitFormProps) {
  const [incomplete, setIncomplete] = useState(false);

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!isComplete(draft)) {
      setIncomplete(true);
      return;
    }
    setIncomplete(false);
    onSend();
  };

  const toggleTag = (tag: string) =>
    onChange({ ...draft, tags: draft.tags.includes(tag) ? draft.tags.filter((t) => t !== tag) : [...draft.tags, tag] });

  return (
    <main className="scan-page">
      <PointHeading point={point} />
      {point.rework && (
        <div className="app-note app-note--error">
          <strong>{`หัวหน้าให้แก้ไข · ${formatTime(point.rework.requestedAt)}`}</strong>
          <span>{`“${point.rework.defect}”`}</span>
        </div>
      )}
      <form className="scan-form" onSubmit={onSubmit}>
        <fieldset className="scan-choice">
          <legend>ผลการทำความสะอาด</legend>
          <label>
            <input
              type="radio"
              name="scan-status"
              checked={draft.status === 'Normal'}
              onChange={() => onChange({ ...draft, status: 'Normal' })}
            />{' '}
            เรียบร้อย
          </label>
          <label>
            <input
              type="radio"
              name="scan-status"
              checked={draft.status === 'Issue'}
              onChange={() => onChange({ ...draft, status: 'Issue' })}
            />{' '}
            พบปัญหา
          </label>
        </fieldset>
        {draft.status === 'Issue' && (
          <fieldset className="scan-issue">
            <legend>ปัญหาที่พบ</legend>
            <div className="scan-tags">
              {issueTags.map((tag) => (
                <label key={tag}>
                  <input type="checkbox" checked={draft.tags.includes(tag)} onChange={() => toggleTag(tag)} /> {tag}
                </label>
              ))}
            </div>
            <label htmlFor="scan-note">หมายเหตุ</label>
            <textarea
              id="scan-note"
              rows={3}
              maxLength={1000}
              value={draft.note}
              onChange={(event) => onChange({ ...draft, note: event.target.value })}
            />
          </fieldset>
        )}
        {incomplete && (
          <p className="app-note app-note--error" role="alert">
            เลือกปัญหาที่พบอย่างน้อย 1 อย่าง หรือเขียนหมายเหตุ
          </p>
        )}
        {errorText && (
          <p className="app-note app-note--error" role="alert">
            {errorText}
          </p>
        )}
        <button type="submit" className="scan-submit" disabled={sending}>
          {sending ? 'กำลังส่ง...' : 'ส่งงาน'}
        </button>
      </form>
      <p className="scan-hint">ถ้าไม่มีสัญญาณ หน้าจอจะขึ้น "ยังไม่ได้บันทึก" พร้อมปุ่มส่งใหม่ เวลาที่บันทึกคือตอนที่ส่งสำเร็จ</p>
    </main>
  );
}
