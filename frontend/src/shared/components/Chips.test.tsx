import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { Badge, EditedMark } from './Badge';
import { Note } from './Note';
import { StatusChip } from './StatusChip';

describe('chips, flags and notes (Enterprise Light, #122)', () => {
  it('a status chip and a flag read as their words only; their icons are decoration', () => {
    const { container } = render(
      <p>
        <StatusChip status="Rework" fixed />
        <Badge kind="late">ส่งช้า 20 นาที</Badge>
      </p>,
    );

    expect(container.firstChild).toHaveTextContent(/^ต้องแก้ไขส่งช้า 20 นาที$/);
    for (const icon of container.querySelectorAll('svg')) expect(icon).toHaveAttribute('aria-hidden', 'true');
  });

  it('the edited mark says who changed the time', () => {
    render(<EditedMark />);

    expect(screen.getByTitle('แก้โดยแอดมิน')).toHaveTextContent('แก้โดยแอดมิน');
  });

  it('an error note offers ลองอีกครั้ง only when it can be tried again', async () => {
    const retry = vi.fn();
    const { rerender } = render(<Note tone="error">โหลดไม่สำเร็จ</Note>);
    expect(screen.queryByRole('button', { name: 'ลองอีกครั้ง' })).not.toBeInTheDocument();

    rerender(
      <Note tone="error" onRetry={retry}>
        โหลดไม่สำเร็จ
      </Note>,
    );
    await userEvent.click(screen.getByRole('button', { name: 'ลองอีกครั้ง' }));

    expect(retry).toHaveBeenCalledOnce();
    expect(screen.getByRole('alert')).toHaveTextContent('โหลดไม่สำเร็จ');
  });
});
