import { Button } from '../../../shared/components/Button';
import { Field, Select, TextArea, TextInput } from '../../../shared/components/Field';
import { Drawer } from '../../../shared/components/Modal';
import { Note } from '../../../shared/components/Note';
import { shiftShortLabel } from '../../../shared/utils/labels';
import type { TeamMissingTimeProps } from './type';
import { useTeamMissingTime } from './useTeamMissingTime';

export function TeamMissingTime(props: TeamMissingTimeProps) {
  const form = useTeamMissingTime(props);
  const size = props.phone ? 'phone' : 'desk';
  const { guard, close } = form;
  return (
    <Drawer labelledBy="missing-time-heading" onClose={close}>
      <form className={`flex flex-col gap-4 ${props.phone ? 'text-base' : 'text-sm'}`} onSubmit={form.save}>
        <header className="flex flex-wrap justify-between gap-2 border-b border-border pb-3">
          <h2 id="missing-time-heading" tabIndex={-1} className="text-xl font-bold">เพิ่มเวลาที่ขาด</h2>
          <Button size={size} variant="link" disabled={form.busy} onClick={close}>ปิด</Button>
        </header>
        <p className="font-semibold">{props.displayName} · {form.dateLabel} · {shiftShortLabel[props.slot.shift]}</p>
        {form.loading && <p role="status">กำลังโหลดข้อมูลการลงเวลา...</p>}
        {form.readError && <Note tone="error" retrySize={size} onRetry={form.refetch}>{form.readError}</Note>}
        {guard.confirming && (
          <Note tone="warn" title="มีข้อมูลที่ยังไม่ได้บันทึก">
            <div className="flex flex-wrap gap-2 pt-2">
              <Button variant="secondary" size={size} onClick={guard.stay}>อยู่หน้านี้</Button>
              <Button variant="danger" size={size} onClick={guard.discard} disabled={form.busy}>ทิ้งร่าง</Button>
            </div>
          </Note>
        )}
        {form.revisionChanged && (
          <Note tone="warn" title="ข้อมูลการลงเวลาเปลี่ยนแล้ว">
            <p>{form.selected ? 'ตรวจรายการลงเวลาล่าสุดก่อนบันทึกข้อมูลที่กรอกไว้' : 'ตรวจรายการล่าสุดและเลือกอีกครั้งหากยังมีรายการที่เพิ่มเวลาได้'}</p>
            {form.selected && (
              <Button variant="secondary" size={size} disabled={!form.canSelect} onClick={form.acknowledgeRevision}>
                ใช้ข้อมูลล่าสุด
              </Button>
            )}
          </Note>
        )}
        {form.error && <Note tone="error">{form.error}</Note>}
        {form.saved && <Note tone="success" status>บันทึกเวลาที่ขาดแล้ว</Note>}
        {!form.loading && !form.readError && !form.hasMissing && <p>ไม่มีรายการที่เพิ่มเวลาได้</p>}
        {form.choices.length > 0 && (
          <>
            <Field label="รายการลงเวลา">
              <Select size={size} value={form.selected} disabled={!form.canSelect} required
                onChange={(event) => form.chooseAction(event.currentTarget.value)}>
                <option value="">เลือกรายการที่ไม่ได้สแกน</option>
                {form.choices.map((choice) => (
                  <option key={choice.eventType} value={choice.eventType} disabled={!choice.eligible}>{choice.label}</option>
                ))}
              </Select>
            </Field>
            <Field label="เวลาที่เกิดขึ้นจริง (เวลาไทย)">
              <TextInput type="time" size={size} value={form.time} required disabled={!form.selected || form.busy}
                onChange={(event) => form.setTime(event.currentTarget.value)} />
            </Field>
            <Field label="เหตุผลที่เพิ่มเวลา">
              <TextArea size={size} value={form.reason} required maxLength={500} disabled={!form.selected || form.busy}
                onChange={(event) => form.setReason(event.currentTarget.value)} />
            </Field>
            <Button variant="primary" size={props.phone ? 'main' : 'desk'} type="submit" disabled={!form.canSave}>
              {form.busy ? 'กำลังบันทึก...' : 'บันทึกเวลาที่ขาด'}
            </Button>
          </>
        )}
      </form>
    </Drawer>
  );
}
