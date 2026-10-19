import { describe, expect, it } from 'vitest';
import { boardRows, breakRow, coveredRow, correctedRow, emptyRow, workingRow } from '../../test/liveFixtures';
import {
  correctionProblem,
  coverLine,
  firstEvent,
  flagText,
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

  it('names the scans outside the radius, and each cover with its own Area and check-in', () => {
    expect(flagText(workingRow)).toBe('เข้างาน ห่างป้าย 85 ม.');
    expect(flagText(emptyRow)).toBe('');
    expect(flagText(correctedRow)).toBe(''); // no GPS verdict is not a flag
    expect(coverLine(coveredRow.covers[0])).toBe('ทำแทน: แม่บ้านที่อื่น (AR03) · Admin มอบหมาย 07:10 · เข้างาน 06:50');
    expect(coverLine({ ...coveredRow.covers[0], ownAreaCode: undefined, shiftInAt: undefined })).toBe(
      'ทำแทน: แม่บ้านที่อื่น · Admin มอบหมาย 07:10 · ยังไม่ลงเวลา',
    );
  });

  it('checks a correction before sending: a time "HH:mm" and a reason (facility-0051)', () => {
    expect(correctionProblem({ time: '07:05', reason: 'มือถือแบตหมด' })).toBeNull();
    expect(correctionProblem({ time: ' 23:59 ', reason: 'ลืม' })).toBeNull();
    expect(correctionProblem({ time: '7:05', reason: 'ลืม' })).toBe('เวลาต้องเป็นแบบ ชม.:นาที เช่น 07:05');
    expect(correctionProblem({ time: '24:00', reason: 'ลืม' })).toBe('เวลาต้องเป็นแบบ ชม.:นาที เช่น 07:05');
    expect(correctionProblem({ time: '07:05', reason: '   ' })).toBe('ต้องกรอกเหตุผล');
    expect(correctionProblem({ time: '07:05', reason: 'ก'.repeat(501) })).toBe('เหตุผลยาวได้ไม่เกิน 500 ตัวอักษร');
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
