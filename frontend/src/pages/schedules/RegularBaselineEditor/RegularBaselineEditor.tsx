import { Field, Select, TextInput } from '../../../shared/components/Field';
import { Note } from '../../../shared/components/Note';
import { ScheduleReview } from '../ScheduleReview/ScheduleReview';
import type { RegularBaselineEditorProps } from './type';
import { useRegularBaselineEditor } from './useRegularBaselineEditor';

export function RegularBaselineEditor(props: RegularBaselineEditorProps) {
  const editor = useRegularBaselineEditor(props);
  const size = props.phone ? 'phone' : 'desk';
  return (
    <ScheduleReview title="แก้ตารางประจำ" editor={editor} latest={editor.latest}
      onCompare={editor.compare} onUseLatest={editor.useLatest}>
      <p className="font-semibold">{props.person.displayName}</p>
      <Note tone="info">พื้นที่และกลุ่มพักจะใช้ซ้ำทุกวันตั้งแต่วันที่เลือก วันหยุดยังจัดแยกเป็นรายสัปดาห์</Note>
      <Field label="วันเริ่มใช้">
        <TextInput type="date" size={size} value={editor.draft.effectiveFrom} min={editor.tomorrow}
          onChange={(event) => editor.setField('effectiveFrom', event.currentTarget.value)} />
      </Field>
      {editor.loading && <p role="status">กำลังโหลดตารางของวันที่เลือก...</p>}
      <Field label="พื้นที่ประจำ">
        <Select size={size} value={editor.draft.areaId}
          onChange={(event) => editor.setField('areaId', event.currentTarget.value)}>
          <option value="">เลือกพื้นที่</option>
          {editor.areas.map((area) => <option key={area.id} value={area.id}>{area.label}</option>)}
        </Select>
      </Field>
      <Field label="กลุ่มพักประจำ">
        <Select size={size} value={editor.draft.breakGroup}
          onChange={(event) => editor.setField('breakGroup', event.currentTarget.value)}>
          <option value="">เลือกกลุ่มพัก</option>
          <option value="1">กลุ่ม 1</option>
          <option value="2">กลุ่ม 2</option>
        </Select>
      </Field>
      {editor.affected.length > 0 && (
        <Note tone="warn" title="วันที่มีตารางขัดแย้ง">
          <ul className="list-disc pl-5">
            {editor.affected.map((item) => <li key={`${item.date}:${item.code}`}>{item.label}</li>)}
          </ul>
        </Note>
      )}
    </ScheduleReview>
  );
}
