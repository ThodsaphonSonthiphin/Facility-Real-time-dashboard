import { describe, expect, it } from 'vitest';
import { boardRows, coveredRow, correctedRow, emptyRow, workingRow } from '../../test/liveFixtures';
import { coverLine, flagText, readBoardArgs, searchRows, toBoardParams } from './attendanceRules';

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
});
