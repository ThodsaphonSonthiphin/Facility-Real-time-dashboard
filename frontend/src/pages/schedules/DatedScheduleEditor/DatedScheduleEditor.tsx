import { Field, Select, TextArea, TextInput } from '../../../shared/components/Field';
import { Note } from '../../../shared/components/Note';
import { ScheduleHistory } from '../ScheduleHistory/ScheduleHistory';
import { ScheduleReview } from '../ScheduleReview/ScheduleReview';
import type { DatedAction, DatedScheduleEditorProps } from './type';
import { useDatedScheduleEditor } from './useDatedScheduleEditor';

export function DatedScheduleEditor(props: DatedScheduleEditorProps) {
  const editor = useDatedScheduleEditor(props);
  const size = props.phone ? 'phone' : 'desk';
  const { action } = editor.draft;
  return (
    <ScheduleReview title={editor.correction ? 'แก้ไขแผนย้อนหลัง' : 'แก้ตารางเฉพาะวันที่'}
      editor={editor} latest={editor.latest} onCompare={props.onCompare} onUseLatest={editor.useLatest}>
      <p className="font-semibold">{props.person.displayName} · {editor.dateLabel}</p>
      {editor.correction && (
        <Note tone="info">เก็บประวัติเดิมไว้ เวลาสแกนจริงไม่เปลี่ยน</Note>
      )}
      <Field label="รายการที่จะแก้">
        <Select size={size} value={action} onChange={(event) => editor.setAction(event.currentTarget.value as DatedAction)}>
          {editor.choices.map((choice) => <option key={choice.value} value={choice.value}>{choice.label}</option>)}
        </Select>
      </Field>
      {action === 'area' && (
        <Field label="พื้นที่เฉพาะวันนี้">
          <Select size={size} value={editor.draft.value} onChange={(event) => editor.setField('value', event.currentTarget.value)}>
            <option value="">ใช้พื้นที่ประจำ</option>
            {editor.areas.map((area) => <option key={area.id} value={area.id}>{area.label}</option>)}
          </Select>
        </Field>
      )}
      {action === 'breakGroup' && (
        <Field label="กลุ่มพักเฉพาะวันนี้">
          <Select size={size} value={editor.draft.value} onChange={(event) => editor.setField('value', event.currentTarget.value)}>
            <option value="">ใช้กลุ่มพักประจำ</option>
            <option value="1">กลุ่ม 1</option>
            <option value="2">กลุ่ม 2</option>
          </Select>
        </Field>
      )}
      {action === 'leave' && (
        <>
          <Field label="เริ่มลา">
            <TextInput type="datetime-local" size={size} value={editor.draft.startsAt}
              onChange={(event) => editor.setField('startsAt', event.currentTarget.value)} />
          </Field>
          <Field label="สิ้นสุดลา">
            <TextInput type="datetime-local" size={size} value={editor.draft.endsAt}
              onChange={(event) => editor.setField('endsAt', event.currentTarget.value)} />
          </Field>
        </>
      )}
      <Field label={editor.reasonRequired ? 'เหตุผลที่แก้ไขแผนย้อนหลัง' : 'เหตุผล (ถ้ามี)'}>
        <TextArea size={size} value={editor.draft.reason} maxLength={500} required={editor.reasonRequired}
          onChange={(event) => editor.setField('reason', event.currentTarget.value)} />
      </Field>
      <ScheduleHistory corrections={editor.corrections} />
    </ScheduleReview>
  );
}
