import { Button } from '../../../shared/components/Button';
import { Note } from '../../../shared/components/Note';
import type { ScheduleConflictProps } from './type';
import { useScheduleConflict } from './useScheduleConflict';

export function ScheduleConflict(props: ScheduleConflictProps) {
  const lines = useScheduleConflict(props);
  return (
    <Note tone="warn" title="ตารางเปลี่ยนระหว่างแก้ไข" status>
      <p>ตรวจข้อมูลล่าสุดและร่างของคุณก่อนบันทึกอีกครั้ง</p>
      {props.latest && (
        <ul className="my-2 space-y-2">
          {lines.map((line) => (
            <li key={line.id}>
              <p className="font-semibold">{line.label}</p>
              <p>ตอนเปิด · {line.before}</p>
              <p>ล่าสุด · {line.current}</p>
              <p>ร่าง · {line.after}</p>
            </li>
          ))}
        </ul>
      )}
      <div className="mt-2 flex flex-wrap gap-2">
        <Button variant="secondary" size={props.size} disabled={props.disabled} onClick={props.onCompare}>โหลดเพื่อเปรียบเทียบ</Button>
        {props.latest && (
          <Button variant="secondary" size={props.size} disabled={props.disabled} onClick={props.onUseLatest}>
            ใช้ข้อมูลล่าสุดและเก็บร่าง
          </Button>
        )}
      </div>
    </Note>
  );
}
