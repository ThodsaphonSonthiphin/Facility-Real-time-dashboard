import { render, screen } from '@testing-library/react';
import { CheckCircle2, MapPin, Users } from 'lucide-react';
import { describe, expect, it } from 'vitest';
import { AvatarBadge } from '../AvatarBadge/AvatarBadge';
import { KpiCard } from './KpiCard';

describe('KpiCard', () => {
  it('renders label, formatted value, and unit in div mode', () => {
    render(<KpiCard label="จุดที่เรียบร้อย" value={1246} unit="จุด" icon={CheckCircle2} tone="green" />);

    expect(screen.getByText('จุดที่เรียบร้อย')).toBeInTheDocument();
    expect(screen.getByText(/1,246/)).toBeInTheDocument();
    expect(screen.getByText('จุด')).toBeInTheDocument();
  });

  it('preserves AttendancePage list item textContent contract when as="li" without unit or progress', () => {
    render(
      <ul>
        <KpiCard as="li" label="พื้นที่ที่ต้องมีคน" value={6} icon={MapPin} tone="neutral" />
      </ul>,
    );

    const item = screen.getByRole('listitem');
    expect(item.textContent).toBe('พื้นที่ที่ต้องมีคน6');
  });

  it('renders an accessible progressbar when progress is provided', () => {
    render(
      <KpiCard
        label="กำลังปฏิบัติงาน"
        value={168}
        unit="/ 170 คน"
        icon={Users}
        tone="blue"
        progress={99}
        progressLabel="อัตราการเข้างาน"
      />,
    );

    const bar = screen.getByRole('progressbar', { name: 'อัตราการเข้างาน' });
    expect(bar).toHaveAttribute('aria-valuemin', '0');
    expect(bar).toHaveAttribute('aria-valuemax', '100');
    expect(bar).toHaveAttribute('aria-valuenow', '99');
    expect(screen.getByText('99%')).toBeInTheDocument();
  });

  it('clamps progress values outside 0–100', () => {
    render(<KpiCard label="ความคืบหน้า" value={100} icon={CheckCircle2} progress={120} />);

    const bar = screen.getByRole('progressbar', { name: 'ความคืบหน้า' });
    expect(bar).toHaveAttribute('aria-valuenow', '100');
  });
});

describe('AvatarBadge', () => {
  it('extracts Thai consonant initial and hides badge from assistive tech', () => {
    const { container } = render(<AvatarBadge name="สมชาย ใจดี" tone="blue" />);

    const badge = container.firstElementChild;
    expect(badge).toHaveAttribute('aria-hidden', 'true');
    expect(badge?.textContent).toBe('ส');
  });

  it('skips Thai leading vowel to extract the first consonant', () => {
    const { container } = render(<AvatarBadge name="เกรียงไกร" tone="teal" />);
    expect(container.firstElementChild?.textContent).toBe('ก');
  });

  it('extracts uppercased Latin initial and falls back to question mark on empty input', () => {
    const { container, rerender } = render(<AvatarBadge name="  alice smith" tone="amber" />);
    expect(container.firstElementChild?.textContent).toBe('A');

    rerender(<AvatarBadge name="   " tone="purple" />);
    expect(container.firstElementChild?.textContent).toBe('?');
  });
});
