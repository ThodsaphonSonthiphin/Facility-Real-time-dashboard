import { Badge } from '../../../shared/components/Badge';
import { Button } from '../../../shared/components/Button';
import { Field, FieldGroup, TextArea } from '../../../shared/components/Field';
import { Note } from '../../../shared/components/Note';
import { focusRing } from '../../../shared/components/styles';
import { formatShiftDate, formatWeekday } from '../../../shared/utils/thaiTime';
import { ScheduleReview } from '../ScheduleReview/ScheduleReview';
import { weekRangeLabel } from '../scheduleRules';
import type { WeeklyOffEditorProps } from './type';
import { useWeeklyOffEditor } from './useWeeklyOffEditor';

export function WeeklyOffEditor(props: WeeklyOffEditorProps) {
  const editor = useWeeklyOffEditor(props);
  const size = props.phone ? 'phone' : 'desk';
  return (
    <ScheduleReview title="จัดวันหยุดรายสัปดาห์" editor={editor} latest={editor.latest}
      onCompare={props.onCompare} onUseLatest={editor.useLatest}
      remaining={editor.viewedRows.some((row) => row.incomplete) &&
        <Note tone="info">ยังมีแถวที่จัดไม่ครบ กรุณาตรวจในตารางรายสัปดาห์</Note>}>
      <p>สัปดาห์เป้าหมาย · {weekRangeLabel(editor.targetWeek)}</p>
      <div className="flex flex-col items-start gap-2 rounded-lg border border-border bg-surface-2 p-3">
        <p>สัปดาห์อ้างอิง · {weekRangeLabel(editor.previousWeek)}</p>
        <Button variant="secondary" size={size} disabled={!editor.copyAvailable} onClick={editor.copy}>
          คัดลอกลงช่องที่ยังไม่กำหนด
        </Button>
        {editor.sourceLoading && <p role="status">กำลังโหลดสัปดาห์อ้างอิง...</p>}
        {!editor.sourceLoading && !editor.copyAvailable && <p>ไม่มีตัวเลือกวันหยุดให้อ้างอิง</p>}
      </div>
      {editor.viewedRows.some((row) => row.incomplete) && (
        <Note tone="info">รายการที่ยังจัดไม่ครบจะแสดงตามเดิมหลังบันทึก</Note>
      )}
      {editor.viewedRows.map((row) => (
        <FieldGroup key={row.person.userId} legend={row.person.displayName}>
          {row.incomplete && <div><Badge kind="edited">ยังจัดไม่ครบ</Badge></div>}
          {row.missingSource && <p>ไม่มีวันหยุดจากสัปดาห์อ้างอิงที่ใช้กับคนนี้ได้</p>}
          {!row.editable && <p>ตารางนี้แก้วันหยุดไม่ได้ในขอบเขตปัจจุบัน</p>}
          <div className="grid grid-cols-2 gap-2">
            {row.person.days.map((day) => (
              <label key={day.date} className={`flex items-center gap-2 rounded-md border border-border-control px-2 ${props.phone ? 'min-h-12' : 'min-h-9'}`}>
                <input type="checkbox" checked={row.current?.includes(day.date) ?? false}
                  className={focusRing} disabled={!row.editable || !day.edit?.canChange}
                  onChange={(event) => row.setChoice(day.date, event.currentTarget.checked)} />
                {formatWeekday(day.date)} {formatShiftDate(day.date).slice(0, 5)}
              </label>
            ))}
          </div>
          {row.current === undefined && row.editable && row.allDatesEditable && (
            <Button variant="secondary" size={size} onClick={row.confirmNoOff}>ยืนยันว่าไม่มีวันหยุด</Button>
          )}
        </FieldGroup>
      ))}
      {editor.needsReason && (
        <Field label="เหตุผลแก้วันหยุดย้อนหลัง">
          <TextArea size={size} value={editor.reason} maxLength={500}
            onChange={(event) => editor.setReason(event.currentTarget.value)} />
        </Field>
      )}
    </ScheduleReview>
  );
}
