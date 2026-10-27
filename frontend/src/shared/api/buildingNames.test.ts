import { describe, expect, it } from 'vitest';
import { buildingOptionText, buildingText } from './buildingNames';

describe('building labels (facility-0091 decision 1)', () => {
  it('a picker shows the code and the current name', () => {
    expect(buildingOptionText({ code: 'A', name: 'อาคารผลิต 1' })).toBe('A · อาคารผลิต 1');
  });

  it('a code becomes code · name, or stays the code while the names load or when it is unknown', () => {
    const names = new Map([['A', 'อาคารผลิต 1']]);
    expect(buildingText('A', names)).toBe('A · อาคารผลิต 1');
    expect(buildingText('Z', names)).toBe('Z');
    expect(buildingText('A')).toBe('A');
  });
});
