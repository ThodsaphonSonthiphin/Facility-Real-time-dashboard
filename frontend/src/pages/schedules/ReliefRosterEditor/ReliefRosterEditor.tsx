import { Field, Select, TextInput } from '../../../shared/components/Field';
import { Note } from '../../../shared/components/Note';
import { getErrorMessage } from '../../../shared/utils/getErrorMessage';
import { formatWeekday } from '../../../shared/utils/thaiTime';
import { ScheduleReview } from '../ScheduleReview/ScheduleReview';
import type { ReliefRosterEditorProps } from './type';
import { useReliefRosterEditor } from './useReliefRosterEditor';

export function ReliefRosterEditor(props: ReliefRosterEditorProps) {
  const form = useReliefRosterEditor(props);
  const size = props.phone ? 'phone' : 'desk';
  return (
    <ScheduleReview title="จัดตารางประจำสแปร์" editor={form} reviewLabel="ตรวจทาน"
      latest={form.latest} onCompare={form.compare} onUseLatest={form.useLatest}>
      <Field label="วันเริ่มใช้" help="ตารางจะใช้ซ้ำรายสัปดาห์ตั้งแต่วันที่เลือก">
        <TextInput type="date" size={size} min={form.tomorrow} value={form.effectiveFrom}
          onChange={(event) => form.setEffectiveFrom(event.currentTarget.value)} />
      </Field>
      {form.loading && <p role="status">กำลังโหลดตารางสแปร์...</p>}
      {form.error && <Note tone="error" onRetry={form.compare}>{getErrorMessage(form.error)}</Note>}
      {!form.loading && !form.rows.length && <Note tone="info">ไม่มีแม่บ้านสแปร์ที่จัดตารางได้ในวันที่เลือก</Note>}
      {form.rows.map(({ row, areas, cells, workDays, allKnown }) => (
        <fieldset key={row.userId} className="min-w-0 rounded-lg border border-border p-3">
          <legend className="px-1 font-semibold">{row.displayName}</legend>
          <div className="mb-3 flex flex-wrap justify-between gap-2">
            <span className="text-text-2">ตึกประจำ: {row.homeBuildingCode ?? 'ยังไม่ทราบ'}</span>
            <span role="status">{!allKnown ? 'ตรวจความครบได้เฉพาะวันที่ดูได้'
              : workDays === 6 ? 'ครบ 6 วันทำงาน' : `จัดแล้ว ${workDays} วัน · เป้าหมาย 6 วัน`}</span>
          </div>
          <div className="grid min-w-0 gap-3 sm:grid-cols-2">
            {cells.map((cell) => (
              <Field key={cell.weekday} label={formatWeekday(cell.date)} help={cell.known ? undefined : 'อยู่นอกขอบเขตที่จัดได้'}>
                <Select size={size} disabled={!cell.known || form.loading} value={cell.value}
                  onChange={(event) => form.setCell(row, cell.weekday, event.currentTarget.value)}>
                  <option value="">ยังไม่กำหนด</option>
                  <option value="off">วันหยุด</option>
                  {cell.value && cell.value !== 'off' && !areas.some((area) => String(area.id) === cell.value)
                    && <option value={cell.value}>พื้นที่เดิม · {cell.value}</option>}
                  {areas.map((option) => <option key={option.id} value={option.id}>{option.label}</option>)}
                </Select>
              </Field>
            ))}
          </div>
        </fieldset>
      ))}
    </ScheduleReview>
  );
}
