import { CircleCheck, Send, TriangleAlert } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import type { PointCard } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { ChoiceTile, Field, TextArea, ToggleChip } from '../../shared/components/Field';
import { Note } from '../../shared/components/Note';
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

/**
 * Wireframe M4, drawn as variant A of the mockups (#74): one form, เรียบร้อย / พบปัญหา as two large tiles, the tags as
 * toggle chips, ส่งงาน as the 56 px main action. เรียบร้อย is chosen; พบปัญหา opens the tags and the note
 * (facility-0007, 0046). The tiles and chips stay a real radio group and real checkboxes.
 */
export function SubmitForm({ point, draft, onChange, sending, errorText, onSend }: SubmitFormProps) {
  // facility-0076: the warning shows once ส่งงาน was pressed, and only while พบปัญหา still has no tag and no note
  const [tried, setTried] = useState(false);
  const incomplete = tried && !isComplete(draft);

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!isComplete(draft)) {
      setTried(true);
      return;
    }
    onSend();
  };

  const toggleTag = (tag: string) =>
    onChange({ ...draft, tags: draft.tags.includes(tag) ? draft.tags.filter((t) => t !== tag) : [...draft.tags, tag] });

  return (
    <main className="flex flex-col gap-4 text-base text-text">
      <PointHeading point={point} />
      {point.rework && (
        <Note tone="error" alert={false} title={`หัวหน้าให้แก้ไข · ${formatTime(point.rework.requestedAt)}`}>
          <span>{`“${point.rework.defect}”`}</span>
        </Note>
      )}
      <form className="flex flex-col gap-4" onSubmit={onSubmit}>
        <fieldset className="m-0 border-0 p-0">
          <legend className="mb-2 p-0 font-bold">ผลการทำความสะอาด</legend>
          <div className="grid grid-cols-2 gap-2.5">
            <ChoiceTile
              name="scan-status"
              disabled={sending}
              checked={draft.status === 'Normal'}
              onChange={() => onChange({ ...draft, status: 'Normal' })}
            >
              <CircleCheck aria-hidden="true" className="size-7" />
              เรียบร้อย
            </ChoiceTile>
            <ChoiceTile
              name="scan-status"
              disabled={sending}
              checked={draft.status === 'Issue'}
              onChange={() => onChange({ ...draft, status: 'Issue' })}
            >
              <TriangleAlert aria-hidden="true" className="size-7" />
              พบปัญหา
            </ChoiceTile>
          </div>
        </fieldset>
        {draft.status === 'Issue' && (
            <fieldset className="m-0 border-0 p-0">
              <legend className="mb-2 p-0 font-bold">ปัญหาที่พบ</legend>
              <div className="flex flex-wrap gap-2">
                {issueTags.map((tag) => (
                  <ToggleChip key={tag} disabled={sending} checked={draft.tags.includes(tag)} onChange={() => toggleTag(tag)}>
                    {tag}
                  </ToggleChip>
                ))}
              </div>
              {/* The note stays inside "ปัญหาที่พบ", as before (#74 keeps the form's structure) */}
              <div className="mt-3">
                <Field label="หมายเหตุ">
                  <TextArea
                    rows={3}
                    maxLength={1000}
                    disabled={sending}
                    value={draft.note}
                    onChange={(event) => onChange({ ...draft, note: event.target.value })}
                  />
                </Field>
              </div>
            </fieldset>
        )}
        {incomplete && <Note tone="error">เลือกปัญหาที่พบอย่างน้อย 1 อย่าง หรือเขียนหมายเหตุ</Note>}
        {errorText && <Note tone="error">{errorText}</Note>}
        <Button type="submit" variant="primary" size="main" disabled={sending}>
          <Send aria-hidden="true" className="size-5" />
          {sending ? 'กำลังส่ง...' : 'ส่งงาน'}
        </Button>
      </form>
      <p className="text-center text-md text-muted">
        ถ้าไม่มีสัญญาณ หน้าจอจะขึ้น "ยังไม่ได้บันทึก" พร้อมปุ่มส่งใหม่ เวลาที่บันทึกคือตอนที่ส่งสำเร็จ
      </p>
    </main>
  );
}
