import { Button } from '../../../shared/components/Button';
import { Field, Select, TextInput } from '../../../shared/components/Field';
import { Note } from '../../../shared/components/Note';
import { formatThaiDate, formatWeekday } from '../../../shared/utils/thaiTime';
import { shiftShortLabel } from '../../../shared/utils/labels';
import { getErrorMessage } from '../../../shared/utils/getErrorMessage';
import { ScheduleReview } from '../ScheduleReview/ScheduleReview';
import type { ReliefAssignmentEditorProps } from './type';
import { useReliefAssignmentEditor } from './useReliefAssignmentEditor';

export function ReliefAssignmentEditor(props: ReliefAssignmentEditorProps & { mode?: 'own' | 'loan' }) {
  const mode = props.mode ?? 'own';
  const form = useReliefAssignmentEditor(props, mode);
  const size = props.phone ? 'phone' : 'desk';
  return (
    <ScheduleReview title={mode === 'loan' ? 'บันทึกการยืมสแปร์' : 'มอบหมายงานสแปร์'} editor={form}
      reviewLabel="ตรวจทาน" latest={form.latest} onCompare={form.compare} onUseLatest={form.useLatest}>
      {mode === 'loan' && <Note tone="info">ตกลงกับซุปตึกต้นทางก่อน แล้วจึงบันทึกงานที่นี่</Note>}
      <div className="grid min-w-0 gap-3 sm:grid-cols-2">
        <Field label="วันที่ทำงาน">
          <Select size={size} value={form.date} onChange={(event) => form.chooseDate(event.currentTarget.value)}>
            {form.dates.map((date) => <option key={date} value={date}>{formatWeekday(date)} {formatThaiDate(date)}</option>)}
          </Select>
        </Field>
        <Field label="กะปลายทาง">
          <Select size={size} value={form.shift} onChange={(event) => form.chooseShift(event.currentTarget.value)}>
            {[...new Set(form.slots.map((slot) => slot.shift))].map((shift) => (
              <option key={shift} value={shift}>{shiftShortLabel[shift]}</option>
            ))}
          </Select>
        </Field>
      </div>
      <Field label="พื้นที่ปลายทาง">
        <Select size={size} value={form.area} onChange={(event) => form.chooseArea(event.currentTarget.value)}>
          <option value="">เลือกพื้นที่</option>
          {form.choices.map((slot) => (
            <optgroup key={slot.buildingId} label={`ตึก ${slot.buildingCode}`}>
              {slot.areas.map((option) => <option key={option.id} value={option.id}>{option.label}</option>)}
            </optgroup>
          ))}
        </Select>
      </Field>
      {mode === 'own' ? (
        <Field label="แม่บ้านสแปร์">
          <Select size={size} value={form.personId} onChange={(event) => form.choosePerson(event.currentTarget.value)}>
            <option value="">เลือกแม่บ้านสแปร์</option>
            {form.ownPeople.map((person) => (
              <option key={person.userId} value={person.userId}>{person.displayName} · ตึก {person.homeBuildingCode}</option>
            ))}
          </Select>
        </Field>
      ) : (
        <div className="grid min-w-0 gap-3">
          <Field label="ตึกต้นทาง">
            <Select size={size} value={form.loan.fields.sourceBuildingId}
              onChange={(event) => form.changeLoan({ sourceBuildingId: event.currentTarget.value })}>
              <option value="">เลือกตึกต้นทาง</option>
              {props.week.sourceBuildings?.map((building) => <option key={building.id} value={building.id}>{building.code}</option>)}
            </Select>
          </Field>
          <Field label="รหัสพนักงานสแปร์">
            <TextInput size={size} value={form.loan.fields.employeeId} maxLength={50}
              onChange={(event) => form.changeLoan({ employeeId: event.currentTarget.value })} />
          </Field>
          <Button size={size} variant="secondary" onClick={form.loan.search}
            disabled={!form.area || !form.loan.fields.employeeId.trim() || !form.loan.fields.sourceBuildingId || form.candidateQuery.isFetching}>
            ค้นหาสแปร์
          </Button>
        </div>
      )}
      {form.candidateQuery.isFetching && <p role="status">กำลังตรวจสอบแม่บ้านสแปร์...</p>}
      {form.candidateQuery.error && <Note tone="error" onRetry={() => void form.candidateQuery.refetch()}>{getErrorMessage(form.candidateQuery.error)}</Note>}
      {form.candidate && (
        <section className="space-y-1 rounded-lg border border-border bg-surface-2 p-3" aria-label="ผลการค้นหาแม่บ้านสแปร์">
          <p className="font-semibold">{form.candidate.displayName}</p>
          <p className="text-text-2">ตึกประจำ: {form.candidate.homeBuildingCode}</p>
          {form.candidate.sourceSupervisorName && <p>ซุปตึกต้นทาง · {form.candidate.sourceSupervisorName}</p>}
          {form.candidate.eligible && <p>จัดงานในกะที่เลือกได้</p>}
        </section>
      )}
      <Field label="แม่บ้านประจำที่ทำแทน">
        <Select size={size} value={form.replacementId} onChange={(event) => form.setReplacement(event.currentTarget.value)}>
          <option value="">งานสแปร์ไม่ได้แทนใคร</option>
          {form.replaced.map((person) => <option key={person.userId} value={person.userId}>{person.displayName}</option>)}
        </Select>
      </Field>
      <Field label="กลุ่มพักครั้งที่ 1">
        <Select size={size} value={form.group} onChange={(event) => form.setGroup(event.currentTarget.value)}>
          <option value="">{form.inherited ? `ใช้กลุ่ม ${form.inherited} ของคนที่แทน` : 'เลือกกลุ่มพัก'}</option>
          <option value="1">เลือกกลุ่ม 1</option>
          <option value="2">เลือกกลุ่ม 2</option>
        </Select>
      </Field>
      {form.requiresAgreement && (
        <Field label="ยืนยันว่าตกลงกับตึกต้นทางนอกเว็บแล้ว" help="บันทึกการยืนยันของผู้จัดงานปลายทาง">
          <Select size={size} value={form.attested ? 'yes' : ''} onChange={(event) => form.setAttested(event.currentTarget.value === 'yes')}>
            <option value="">ยังไม่ยืนยัน</option>
            <option value="yes">ตกลงกับตึกต้นทางนอกเว็บแล้ว</option>
          </Select>
        </Field>
      )}
      {form.date < form.today && (
        <Field label="เหตุผลการแก้แผนย้อนหลัง">
          <TextInput size={size} maxLength={500} value={form.reason} onChange={(event) => form.setReason(event.currentTarget.value)} />
        </Field>
      )}
    </ScheduleReview>
  );
}
