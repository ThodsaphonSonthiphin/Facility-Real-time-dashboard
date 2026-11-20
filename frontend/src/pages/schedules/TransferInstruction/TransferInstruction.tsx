import { Field, Select, TextArea, TextInput } from '../../../shared/components/Field';
import { Note } from '../../../shared/components/Note';
import { shiftShortLabel } from '../../../shared/utils/labels';
import { ScheduleReview } from '../ScheduleReview/ScheduleReview';
import type { TransferInstructionProps } from './type';
import { useTransferInstruction } from './useTransferInstruction';

export function TransferInstruction(props: TransferInstructionProps) {
  const editor = useTransferInstruction(props);
  const size = props.phone ? 'phone' : 'desk';
  const { currentInstruction, confirmHistory } = editor;
  return (
    <ScheduleReview title="ย้ายพื้นที่ระหว่างกะ" editor={editor} latest={editor.latest} onCompare={editor.onCompare}
      onUseLatest={editor.useLatest} remaining={editor.ui === 'reviewing' || editor.ui === 'sending'
        ? <p>เหตุผล · {editor.reason.trim()}</p> : undefined}>
      <p className="font-semibold">{props.displayName} · {editor.trimDate} · {shiftShortLabel[props.slot.shift]}</p>
      {editor.loading && <p role="status">กำลังโหลดข้อมูลการลงเวลา...</p>}
      {editor.readError && <Note tone="error" retrySize={size} onRetry={editor.onCompare}>{editor.readError}</Note>}
      {currentInstruction && (
        <Note tone={editor.cancelling ? 'info' : 'warn'} title={currentInstruction.status}>
          <p>{currentInstruction.route}</p>
          <p>เวลาที่มีผล · {currentInstruction.effective}</p>
        </Note>
      )}
      {confirmHistory.length > 0 && (
        <section className="space-y-2">
          <h3 className="font-semibold">ประวัติการย้ายพื้นที่ที่ยืนยันแล้ว</h3>
          <ul className="space-y-2">
            {confirmHistory.map((entry) => (
              <li key={entry.id} className="rounded-lg border border-border p-3">
                <p className="font-semibold">{entry.status} · {entry.route}</p>
                <p>เวลาที่มีผล · {entry.effective}</p>
                {entry.arrival && <p>เวลาที่ถึงจริง · {entry.arrival}</p>}
              </li>
            ))}
          </ul>
        </section>
      )}
      {editor.hasState && !editor.cancelling && (
        <p>พื้นที่ปัจจุบัน · {editor.origin}</p>
      )}
      {editor.hasState && !editor.cancelling && editor.cannotInstruct && (
        <Note tone="info">{editor.cannotInstruct}</Note>
      )}
      {editor.hasState && !editor.cancelling && !editor.cannotInstruct && (
        <>
          <Field label="พื้นที่ปลายทาง">
            <Select size={size} value={editor.destination} required onChange={(event) => editor.setDestination(event.currentTarget.value)}>
              <option value="">เลือกพื้นที่ปลายทาง</option>
              {editor.destinations.map(({ id, label }) => <option key={id} value={id}>{label}</option>)}
            </Select>
          </Field>
          <Field label="เวลาที่มีผล">
            <TextInput size={size} type="datetime-local" value={editor.effectiveTime} required
              onChange={(event) => editor.setEffectiveTime(event.currentTarget.value)} />
          </Field>
        </>
      )}
      {editor.hasState && (editor.cancelling || !editor.cannotInstruct) && (
        <Field label={editor.cancelling ? 'เหตุผลที่ยกเลิกคำสั่ง' : 'เหตุผลที่ย้ายพื้นที่'}>
          <TextArea size={size} value={editor.reason} required maxLength={500}
            onChange={(event) => editor.setReason(event.currentTarget.value)} />
        </Field>
      )}
    </ScheduleReview>
  );
}
