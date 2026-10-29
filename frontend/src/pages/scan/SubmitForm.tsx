import { CircleCheck, Send, Wrench } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import type { PointCard } from '../../shared/api/types';
import { Button } from '../../shared/components/Button';
import { ChoiceTile, Field, TextArea, ToggleChip } from '../../shared/components/Field';
import { MoreInfo } from '../../shared/components/MoreInfo';
import { Note } from '../../shared/components/Note';
import { PhoneHeader, PhoneScreen } from '../../shared/components/PhoneScreen';
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
    <PhoneScreen
      header={
        <PhoneHeader back={{ to: '/my-work', label: 'งานของฉัน' }}>
          <PointHeading point={point} />
        </PhoneHeader>
      }
      bottom={
        // The send button is the page's one main action, so it sits in the bar; `form` ties it to the form above it
        <Button type="submit" form="scan-form" variant="primary" size="main" disabled={sending}>
          <Send aria-hidden="true" className="size-6" />
          {sending ? 'กำลังส่ง...' : 'ส่งงาน'}
        </Button>
      }
    >
      {point.rework && (
        <Note tone="error" alert={false} title={`หัวหน้าให้แก้ไข · ${formatTime(point.rework.requestedAt)}`}>
          <span>{`“${point.rework.defect}”`}</span>
        </Note>
      )}
      <form id="scan-form" className="flex flex-col gap-5" onSubmit={onSubmit}>
        <fieldset className="m-0 border-0 p-0">
          <legend className="mb-3 p-0 text-lg font-bold">ผลการทำความสะอาด</legend>
          <div className="grid grid-cols-2 gap-3">
            <ChoiceTile
              name="scan-status"
              disabled={sending}
              checked={draft.status === 'Normal'}
              onChange={() => onChange({ ...draft, status: 'Normal' })}
            >
              <CircleCheck aria-hidden="true" className="size-9" />
              เรียบร้อย
            </ChoiceTile>
            <ChoiceTile
              name="scan-status"
              disabled={sending}
              checked={draft.status === 'Issue'}
              onChange={() => onChange({ ...draft, status: 'Issue' })}
            >
              <Wrench aria-hidden="true" className="size-9" />
              พบปัญหา
            </ChoiceTile>
          </div>
        </fieldset>
        {draft.status === 'Issue' && (
          <fieldset className="m-0 border-0 p-0">
            <legend className="mb-2 p-0 font-bold">
              ปัญหาที่พบ <span className="font-normal text-text-2">เลือกได้หลายข้อ</span>
            </legend>
            <div className="flex flex-wrap gap-2">
              {issueTags.map((tag) => (
                <ToggleChip
                  key={tag}
                  disabled={sending}
                  checked={draft.tags.includes(tag)}
                  onChange={() => toggleTag(tag)}
                >
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
      </form>
      <MoreInfo size="phone" summary="เกี่ยวกับการส่งงาน">
        ถ้าไม่มีสัญญาณ หน้าจอจะขึ้น "ยังไม่ได้บันทึก" พร้อมปุ่มส่งใหม่ เวลาที่บันทึกคือตอนที่ส่งสำเร็จ
      </MoreInfo>
    </PhoneScreen>
  );
}
