import { describe, expect, it } from 'vitest';
import { actionText, readHistoryArgs, toHistoryParams } from './historyRules';

describe('historyRules (D9)', () => {
  it('actionText names each action in Thai, and shows an unknown one as stored', () => {
    expect(actionText('ATTENDANCE_EDIT')).toBe('แก้เวลาลงงาน');
    expect(actionText('ACCOUNT_PHONE')).toBe('แก้เบอร์โทร');
    expect(actionText('SIGN_LOCATION')).toBe('เก็บพิกัดป้าย');
    expect(actionText('SOMETHING_NEW')).toBe('SOMETHING_NEW');
    expect(actionText('constructor')).toBe('constructor');
  });

  it('readHistoryArgs keeps a range given in full, a real actor id and a known type', () => {
    expect(readHistoryArgs(new URLSearchParams('from=2026-10-01&to=2026-10-07&actor=8&category=Cover'))).toEqual({
      from: '2026-10-01',
      to: '2026-10-07',
      actorId: 8,
      category: 'Cover',
    });
    expect(readHistoryArgs(new URLSearchParams('from=2026-10-01&actor=x&category=Nope'))).toEqual({});
  });

  it('toHistoryParams writes what readHistoryArgs reads, leaving out what is not chosen', () => {
    expect(toHistoryParams({ from: '2026-10-01', to: '2026-10-07', actorId: 8, category: 'Cover' }).toString()).toBe(
      'from=2026-10-01&to=2026-10-07&actor=8&category=Cover',
    );
    expect(toHistoryParams({ category: 'Export' }).toString()).toBe('category=Export');
    expect(toHistoryParams({}).toString()).toBe('');
  });
});
