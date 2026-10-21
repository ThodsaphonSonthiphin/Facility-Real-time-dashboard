import type { KeyboardEvent } from 'react';
import { presetLabel, presetRange, rangePresets, rangeTexts, type RangeTexts } from '../utils/dateRange';
import { thaiToday } from '../utils/thaiTime';
import './shared.css';

interface RangeFieldsProps {
  value: RangeTexts;
  onChange: (next: RangeTexts) => void;
  /** Enter in either field: D7 and D9 show the range at once. */
  onEnter?: () => void;
}

/**
 * "ตั้งแต่" and "ถึง" typed as dd/mm/yyyy like D2's date (plan 11 decision 13), and wireframe D8's shortcuts
 * วันนี้ · 7 วัน · เดือนนี้, which fill both fields with Thai dates. The page checks the texts (checkRange) before asking.
 */
export function RangeFields({ value, onChange, onEnter }: RangeFieldsProps) {
  const enter = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter') {
      onEnter?.();
    }
  };
  return (
    <div className="app-range">
      <label className="app-field">
        ตั้งแต่
        <input
          className="app-input app-range__date"
          value={value.fromText}
          placeholder="วว/ดด/ปปปป"
          inputMode="numeric"
          onChange={(event) => onChange({ ...value, fromText: event.target.value })}
          onKeyDown={enter}
        />
      </label>
      <label className="app-field">
        ถึง
        <input
          className="app-input app-range__date"
          value={value.toText}
          placeholder="วว/ดด/ปปปป"
          inputMode="numeric"
          onChange={(event) => onChange({ ...value, toText: event.target.value })}
          onKeyDown={enter}
        />
      </label>
      <div className="app-range__presets" role="group" aria-label="ปุ่มลัดช่วงวัน">
        {rangePresets.map((preset) => (
          <button
            key={preset}
            type="button"
            className="app-button"
            onClick={() => onChange(rangeTexts(presetRange(preset, thaiToday())))}
          >
            {presetLabel[preset]}
          </button>
        ))}
      </div>
    </div>
  );
}
