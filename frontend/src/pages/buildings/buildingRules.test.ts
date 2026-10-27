import { describe, expect, it } from 'vitest';
import { managedBuildings } from '../../test/adminFixtures';
import { draftProblem, statusText, stillInside } from './buildingRules';

const [withAreas, empty, off] = managedBuildings;

describe('buildingRules (facility-0090)', () => {
  it('draftProblem asks for the code of a new building, and a name always', () => {
    expect(draftProblem('', 'ตึกผลิต', true)).toBe('ใส่รหัสตึกก่อน');
    expect(draftProblem('  ', 'ตึกผลิต', true)).toBe('ใส่รหัสตึกก่อน');
    expect(draftProblem('B2', ' ', true)).toBe('ใส่ชื่อตึกก่อน');
    expect(draftProblem('', 'ตึกผลิต', false)).toBeNull();
    expect(draftProblem('B2', 'ตึกผลิต', true)).toBeNull();
  });

  it('stillInside says what keeps a building on, as the server counts it', () => {
    expect(stillInside(withAreas)).toBe('ยังมีพื้นที่ที่เปิดอยู่ 2 แห่ง และหัวหน้าประจำตึก 1 คน');
    expect(stillInside({ ...empty, activeAreaCount: 3 })).toBe('ยังมีพื้นที่ที่เปิดอยู่ 3 แห่ง');
    expect(stillInside({ ...empty, activeSupervisorCount: 2 })).toBe('ยังมีหัวหน้าประจำตึก 2 คน');
    expect(stillInside(empty)).toBeNull();
  });

  it('statusText is the status in words', () => {
    expect(statusText(empty)).toBe('ใช้งาน');
    expect(statusText(off)).toBe('ปิดใช้งาน');
  });
});
