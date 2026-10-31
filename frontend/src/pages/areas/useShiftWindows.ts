import { useState, type KeyboardEvent } from 'react';
import type { Shift } from '../../shared/api/types';
import { shiftShortLabel } from '../../shared/utils/labels';
import { clockOf, minutesIntoShift, sameWindow, type WindowDraft } from './pointRules';
interface Typed { start: string; end: string }
interface Options {
  shift: Shift;
  windows: WindowDraft[];
  typed: Typed;
  onTyped: (typed: Typed) => void;
  onAdd: (draft: WindowDraft) => void;
}
export function useShiftWindows({ shift, windows, typed, onTyped, onAdd }: Options) {
  const [problem, setProblem] = useState<string | null>(null);
  const label = shiftShortLabel[shift];
  // Keep insertion order while editing: changing a start must not move focus to another row.
  const mine = windows.map((window, index) => {
    const valid = clockOf(window.start.replace(/:/g, '')) && clockOf(window.end.replace(/:/g, ''));
    const start = valid ? minutesIntoShift(shift, window.start) : NaN;
    const end = valid ? minutesIntoShift(shift, window.end) : NaN;
    const length = end - start;
    const duration = start < end && end < 720 ? `${Math.floor(length / 60) ? `${Math.floor(length / 60)} ชม.` : ''}${length % 60 ? ` ${length % 60} นาที` : ''}`.trim() : 'ตรวจเวลา';
    return { window, index, start, end, duration };
  }).filter(({ window }) => window.shift === shift);
  const add = () => {
    const start = clockOf(typed.start);
    const end = clockOf(typed.end);
    if (!start || !end) {
      setProblem('ใส่เวลาเป็น 07:00 (ชั่วโมง 0–23 นาที 00–59)');
      return;
    }
    const draft = { shift, start, end };
    if (windows.some((w) => sameWindow(w, draft))) {
      setProblem('มีช่วงนี้แล้ว');
      return;
    }
    setProblem(null);
    onAdd(draft);
    onTyped({ start: '', end: '' });
  };
  const timeline = mine.flatMap(({ start, end }) => start < end && end < 720
    ? [{ left: `${start / 720 * 100}%`, width: `${(end - start) / 720 * 100}%` }] : []);
  const ticks = shift === 'Day' ? ['07:00', '10:00', '13:00', '16:00', '19:00'] : ['19:00', '22:00', '01:00', '04:00', '07:00'];
  return { problem, label, mine, add, timeline, ticks };
}

export function timeKeyDown(event: KeyboardEvent<HTMLInputElement>, value: string, onChange: (digits: string) => void, onEnter: () => void) {
  if (event.key === 'Enter') {
    event.preventDefault();
    onEnter();
  } else if (event.key === 'Backspace') {
    event.preventDefault();
    onChange(value.slice(0, -1));
  }
}
