import { act, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { LiveBadge } from './LiveBadge';

describe('LiveBadge', () => {
  afterEach(() => {
    Object.defineProperty(navigator, 'onLine', { value: true, configurable: true });
  });

  it('shows nothing while online and shows the disconnect badge only while offline', () => {
    render(<LiveBadge />);

    expect(screen.getByRole('status')).toBeEmptyDOMElement();

    act(() => window.dispatchEvent(new Event('offline')));
    expect(screen.getByRole('status')).toHaveTextContent('ขาดการเชื่อมต่อ');

    act(() => window.dispatchEvent(new Event('online')));
    expect(screen.getByRole('status')).toBeEmptyDOMElement();
  });
});
