import { describe, expect, it } from 'vitest';
import { boardRows, breakRow, coveredRow, emptyRow, workingRow } from '../../test/liveFixtures';
import {
  changedEvents,
  correctionReason,
  draftProblems,
  reasonProblem,
  coverDetail,
  firstEvent,
  readBoardArgs,
  readFill,
  searchRows,
  toBoardParams,
} from './attendanceRules';

describe('attendance rules (wireframe D2)', () => {
  it('reads the shift from the URL only when the date and the shift come together, as the server wants', () => {
    expect(readBoardArgs(new URLSearchParams('date=2026-10-14&shift=Night&building=2'))).toEqual({
      shiftDate: '2026-10-14',
      shift: 'Night',
      buildingId: 2,
    });
    expect(readBoardArgs(new URLSearchParams('date=2026-10-14'))).toEqual({});
    expect(readBoardArgs(new URLSearchParams('date=14/10/2026&shift=Day'))).toEqual({});
    expect(readBoardArgs(new URLSearchParams('shift=Evening&date=2026-10-14&building=x'))).toEqual({});
    expect(readBoardArgs(new URLSearchParams(''))).toEqual({});
  });

  it('writes the URL back in the same words', () => {
    expect(toBoardParams({ shiftDate: '2026-10-14', shift: 'Night', buildingId: 2 }).toString()).toBe(
      'date=2026-10-14&shift=Night&building=2',
    );
    expect(toBoardParams({ buildingId: 1 }).toString()).toBe('building=1');
    expect(toBoardParams({}).toString()).toBe('');
  });

  it('search keeps the rows whose Area, Cleaner or cover matches, in any letter case', () => {
    expect(searchRows(boardRows, 'ar12').map((row) => row.areaCode)).toEqual(['AR12']);
    expect(searchRows(boardRows, 'ที่อื่น').map((row) => row.areaCode)).toEqual(['AR02']);
    expect(searchRows(boardRows, '  ')).toHaveLength(6);
  });

  it('describes each cover with its own Area and check-in', () => {
    expect(coverDetail(coveredRow.covers[0])).toBe('พื้นที่ของตัวเอง AR03 · แอดมินมอบหมาย 07:10 · เข้างาน 06:50');
    expect(coverDetail({ ...coveredRow.covers[0], ownAreaCode: undefined, shiftInAt: undefined })).toBe(
      'แอดมินมอบหมาย 07:10 · ยังไม่ลงเวลา',
    );
  });

  it('checks a correction before sending: each changed time "HH:mm" and in order, and a reason (facility-0051)', () => {
    const times = (values: Partial<Record<'ShiftIn' | 'BreakOut' | 'BreakIn' | 'ShiftOut', [string, string?]>>) => ({
      ShiftIn: { old: values.ShiftIn?.[0] ?? '', value: values.ShiftIn?.[1] ?? values.ShiftIn?.[0] ?? '' },
      BreakOut: { old: values.BreakOut?.[0] ?? '', value: values.BreakOut?.[1] ?? values.BreakOut?.[0] ?? '' },
      BreakIn: { old: values.BreakIn?.[0] ?? '', value: values.BreakIn?.[1] ?? values.BreakIn?.[0] ?? '' },
      ShiftOut: { old: values.ShiftOut?.[0] ?? '', value: values.ShiftOut?.[1] ?? values.ShiftOut?.[0] ?? '' },
    });
    const ok = times({ ShiftIn: ['06:58'], BreakOut: ['09:45', '09:30'], BreakIn: ['', ' 10:00 '] });
    expect(changedEvents(ok)).toEqual(['BreakOut', 'BreakIn']);
    expect(draftProblems(ok, 'Day')).toEqual({});
    expect(draftProblems(times({ BreakOut: ['', '7:05'] }), 'Day')).toEqual({ BreakOut: 'เวลาต้องเป็นแบบ ชม.:นาที เช่น 07:05' });
    expect(draftProblems(times({ ShiftIn: ['06:58', ''] }), 'Day')).toEqual({ ShiftIn: 'ลบเวลาไม่ได้ ใส่เวลาใหม่ หรือกดคืนค่า' });
    expect(draftProblems(times({ BreakOut: ['09:30'], BreakIn: ['', '09:20'] }), 'Day')).toEqual({
      BreakIn: 'ต้องหลังเวลาออกพัก (09:30)',
    });
    expect(draftProblems(times({ BreakOut: ['', '10:30'], BreakIn: ['10:00'] }), 'Day')).toEqual({
      BreakOut: 'ต้องก่อนเวลากลับจากพัก (10:00)',
    });
    // A Night shift runs past midnight: 06:55 comes after 18:55
    expect(draftProblems(times({ ShiftIn: ['18:55'], ShiftOut: ['', '06:55'] }), 'Night')).toEqual({});
    expect(draftProblems(times({ ShiftIn: ['18:55'], ShiftOut: ['', '18:50'] }), 'Night')).toEqual({
      ShiftOut: 'ต้องหลังเวลาเข้างาน (18:55)',
    });

    expect(reasonProblem(null, '')).toBe('เลือกเหตุผลอย่างน้อย 1 ข้อ');
    expect(reasonProblem('อื่น ๆ', '  ')).toBe('เลือก "อื่น ๆ" ต้องเขียนรายละเอียด');
    expect(reasonProblem('มือถือแบตหมด', '')).toBeNull();
    expect(reasonProblem('อื่น ๆ', 'ก'.repeat(495))).toBe('เหตุผลยาวได้ไม่เกิน 500 ตัวอักษร');
    expect(correctionReason('มือถือแบตหมด', ' หัวหน้ายืนยัน ')).toBe('มือถือแบตหมด · หัวหน้ายืนยัน');
    expect(correctionReason('ลืมลงเวลา', '')).toBe('ลืมลงเวลา');
  });

  it('the form opens on the event asked for, else the first one not scanned', () => {
    expect(firstEvent(emptyRow)).toBe('ShiftIn');
    expect(firstEvent(workingRow)).toBe('BreakOut');
    expect(firstEvent(breakRow)).toBe('BreakIn');
    expect(firstEvent(workingRow, 'ShiftIn')).toBe('ShiftIn');
  });

  it('reads the Cleaner and event an alert link asks to fill', () => {
    expect(readFill(new URLSearchParams('fill=22&event=ShiftOut'))).toEqual({ userId: 22, event: 'ShiftOut' });
    expect(readFill(new URLSearchParams('fill=22&event=Lunch'))).toEqual({ userId: 22, event: undefined });
    expect(readFill(new URLSearchParams('fill=x'))).toBeUndefined();
    expect(readFill(new URLSearchParams(''))).toBeUndefined();
  });
});
