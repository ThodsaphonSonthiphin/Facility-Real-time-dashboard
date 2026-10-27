import { screen } from '@testing-library/react';
import { expect } from 'vitest';

/**
 * Every element showing `text` sits in a closed MoreInfo whose summary is `summary` (owner, 2026-10-26: notes on how a
 * page counts or keeps things wait until asked). jsdom does not hide a closed <details>, so text queries still find it.
 */
export function expectBehindMoreInfo(text: string | RegExp, summary: string) {
  for (const element of screen.getAllByText(text)) {
    const details = element.closest('details');
    expect(details).not.toBeNull();
    expect(details).not.toHaveAttribute('open');
    expect(details?.querySelector('summary')).toHaveTextContent(summary);
  }
}

/** Every element showing `text` is on the page itself, not shut in a MoreInfo (a warning or a field's help). */
export function expectInView(text: string | RegExp) {
  for (const element of screen.getAllByText(text)) {
    expect(element.closest('details')).toBeNull();
  }
}
