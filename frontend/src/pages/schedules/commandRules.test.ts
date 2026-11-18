import { expect, it } from 'vitest';
import { resolveCommandUi } from './commandRules';

it('keeps an uncertain save uncertain until the command result is known', () => {
  expect(resolveCommandUi('sending', 'network-error')).toBe('uncertain');
  expect(resolveCommandUi('uncertain', 'lookup-failed')).toBe('uncertain');
  expect(resolveCommandUi('uncertain', 'committed')).toBe('committed');
});
