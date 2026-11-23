import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ConnectionBanner } from './ConnectionBanner';

describe('ConnectionBanner', () => {
  afterEach(() => {
    Object.defineProperty(navigator, 'onLine', { value: true, configurable: true });
  });

  it('renders nothing while online and shows disconnect alert with retry while offline', async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(<ConnectionBanner onRetry={onRetry} />);

    expect(screen.queryByRole('status')).not.toBeInTheDocument();

    act(() => window.dispatchEvent(new Event('offline')));
    expect(screen.getByRole('status')).toHaveTextContent('ขาดการเชื่อมต่อ');

    await user.click(screen.getByRole('button', { name: 'ลองใหม่' }));
    expect(onRetry).toHaveBeenCalledTimes(1);

    act(() => window.dispatchEvent(new Event('online')));
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });
});
