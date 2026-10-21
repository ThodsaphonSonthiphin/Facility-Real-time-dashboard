import { describe, expect, it } from 'vitest';
import {
  addDays,
  checkRange,
  exportLink,
  lastSevenDays,
  matchesPreset,
  presetRange,
  rangePresets,
  rangeDays,
  rangeTexts,
  readBuildingId,
  readRange,
  toRangeParams,
} from './dateRange';

describe('dateRange', () => {
  it('addDays moves across months and years', () => {
    expect(addDays('2026-10-15', -6)).toBe('2026-10-09');
    expect(addDays('2026-10-01', -1)).toBe('2026-09-30');
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
  });

  it('rangeDays counts both ends', () => {
    expect(rangeDays({ from: '2026-10-15', to: '2026-10-15' })).toBe(1);
    expect(rangeDays({ from: '2026-09-01', to: '2026-10-01' })).toBe(31);
  });

  it('the shortcuts: today, the 7 dates ending today, and this month so far', () => {
    expect(presetRange('today', '2026-10-15')).toEqual({ from: '2026-10-15', to: '2026-10-15' });
    expect(presetRange('week', '2026-10-15')).toEqual({ from: '2026-10-09', to: '2026-10-15' });
    expect(presetRange('month', '2026-10-15')).toEqual({ from: '2026-10-01', to: '2026-10-15' });
    expect(lastSevenDays('2026-10-03')).toEqual({ from: '2026-09-27', to: '2026-10-03' });
  });

  it('rangeTexts writes a range the way the fields show it', () => {
    expect(rangeTexts({ from: '2026-10-01', to: '2026-10-07' })).toEqual({ fromText: '01/10/2026', toText: '07/10/2026' });
  });

  it('checkRange gives the range, or the message to show before asking', () => {
    expect(checkRange({ fromText: '01/10/2026', toText: ' 07/10/2026 ' }, 31)).toEqual({ from: '2026-10-01', to: '2026-10-07' });
    expect(checkRange({ fromText: '1/10/2026', toText: '07/10/2026' }, 31)).toBe('วันที่ต้องเป็นแบบ วว/ดด/ปปปป เช่น 15/10/2026');
    expect(checkRange({ fromText: '01/10/2026', toText: '31/02/2026' }, 31)).toBe('วันที่ต้องเป็นแบบ วว/ดด/ปปปป เช่น 15/10/2026');
    expect(checkRange({ fromText: '08/10/2026', toText: '07/10/2026' }, 31)).toBe('วันสุดท้ายต้องไม่ก่อนวันเริ่ม');
    expect(checkRange({ fromText: '01/09/2026', toText: '01/10/2026' }, 31)).toEqual({ from: '2026-09-01', to: '2026-10-01' });
    expect(checkRange({ fromText: '01/09/2026', toText: '02/10/2026' }, 31)).toBe('เลือกได้ไม่เกิน 31 วัน');
  });

  it('readRange takes both dates from the address or neither, because the server refuses one alone', () => {
    expect(readRange(new URLSearchParams('from=2026-10-01&to=2026-10-07'))).toEqual({ from: '2026-10-01', to: '2026-10-07' });
    expect(readRange(new URLSearchParams('from=2026-10-01'))).toBeUndefined();
    expect(readRange(new URLSearchParams('from=01/10/2026&to=2026-10-07'))).toBeUndefined();
    expect(readRange(new URLSearchParams(''))).toBeUndefined();
  });

  it('readBuildingId takes a positive whole number only', () => {
    expect(readBuildingId(new URLSearchParams('building=2'))).toBe(2);
    expect(readBuildingId(new URLSearchParams('building=0'))).toBeUndefined();
    expect(readBuildingId(new URLSearchParams('building=x'))).toBeUndefined();
    expect(readBuildingId(new URLSearchParams(''))).toBeUndefined();
  });

  it('toRangeParams and exportLink leave out what is not chosen', () => {
    expect(toRangeParams({ from: '2026-10-01', to: '2026-10-07' }, 2).toString()).toBe('from=2026-10-01&to=2026-10-07&building=2');
    expect(toRangeParams(undefined, undefined).toString()).toBe('');
    expect(exportLink({ from: '2026-10-15', to: '2026-10-15' }, undefined)).toBe('/admin/export?from=2026-10-15&to=2026-10-15');
    expect(exportLink(undefined, 1)).toBe('/admin/export?building=1');
    expect(exportLink(undefined, undefined)).toBe('/admin/export');
  });

  it('matchesPreset marks every shortcut whose range the typed fields hold exactly', () => {
    const marked = (fromText: string, toText: string, today: string) =>
      rangePresets.filter((preset) => matchesPreset({ fromText, toText }, preset, today));
    expect(marked('09/10/2026', '15/10/2026', '2026-10-15')).toEqual(['week']);
    expect(marked(' 01/10/2026 ', '15/10/2026', '2026-10-15')).toEqual(['month']);
    expect(marked('15/10/2026', '15/10/2026', '2026-10-15')).toEqual(['today']);
    expect(marked('02/10/2026', '15/10/2026', '2026-10-15')).toEqual([]);
    expect(marked('01/10/2026', '07/10/2026', '2026-10-07')).toEqual(['week', 'month']);
  });
});
