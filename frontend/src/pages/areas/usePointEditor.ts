import { useState, type FormEvent } from 'react';
import type { Shift } from '../../shared/api/types';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { useCreatePointMutation, useSetPointActiveMutation, useUpdatePointMutation } from './areasApi';
import { clockOf, pointEditorTitleId, sortWindows, type WindowDraft } from './pointRules';
import type { PointEditorProps } from './PointEditor';
interface Typed { start: string; end: string }
const noTyped: Typed = { start: '', end: '' };
export function usePointEditor({ area, point, onSaved }: PointEditorProps) {
  const [name, setName] = useState(point?.name ?? '');
  const [windows, setWindows] = useState<WindowDraft[]>(() =>
    (point?.roundWindows ?? []).map(({ shift, start, end }) => ({ shift, start, end })),
  );
  // What is typed in the fields but not yet added, per shift: a save must not drop it silently
  const [pending, setPending] = useState<Record<Shift, Typed>>({ Day: noTyped, Night: noTyped });
  const [problem, setProblem] = useState<string | null>(null);
  const [createPoint, created] = useCreatePointMutation();
  const [updatePoint, updated] = useUpdatePointMutation();
  const [setPointActive, toggled] = useSetPointActiveMutation();
  const sending = created.isLoading || updated.isLoading || toggled.isLoading;
  const shifts: Shift[] = area.shiftPattern === 'DayOnly' ? ['Day'] : ['Day', 'Night'];
  const titleId = pointEditorTitleId(point?.id);

  const save = async (event: FormEvent) => {
    event.preventDefault();
    const trimmed = name.trim();
    if (trimmed === '') {
      setProblem('ใส่ชื่อจุดก่อน');
      return;
    }
    if (shifts.some((shift) => Object.values(pending[shift]).some((box) => box.trim() !== ''))) {
      setProblem('มีเวลาที่พิมพ์ไว้แต่ยังไม่ได้เพิ่ม กด "+ เพิ่มช่วงเวลา" หรือลบเวลานั้นก่อนบันทึก');
      return;
    }
    if (windows.some((w) => !clockOf(w.start.replace(/:/g, '')) || !clockOf(w.end.replace(/:/g, '')))) {
      setProblem('ใส่เวลารอบให้ครบเป็น HH:mm (ชั่วโมง 0–23 นาที 00–59)');
      return;
    }
    setProblem(null);
    // A day-only Area shows no night windows, so none are sent
    const body = { name: trimmed, roundWindows: sortWindows(windows.filter((w) => shifts.includes(w.shift))) };
    try {
      const saved = point
        ? await updatePoint({ pointId: point.id, body }).unwrap()
        : await createPoint({ areaId: area.id, body }).unwrap();
      onSaved(`บันทึกจุด ${saved.name} แล้ว`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

  const toggle = async (active: boolean) => {
    if (!point) {
      return;
    }
    setProblem(null);
    try {
      const saved = await setPointActive({ pointId: point.id, active }).unwrap();
      onSaved(`${active ? 'เปิด' : 'ปิด'}ใช้งานจุด ${saved.name} แล้ว`);
    } catch (error) {
      setProblem(getErrorMessage(error));
    }
  };

return { name, setName, windows, setWindows, pending, setPending, problem, sending, shifts, titleId, save, toggle };
}
