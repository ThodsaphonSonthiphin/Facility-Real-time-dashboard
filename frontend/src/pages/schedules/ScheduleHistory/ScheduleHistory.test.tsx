import { render, screen } from '@testing-library/react';
import { expect, it } from 'vitest';
import { ScheduleHistory } from './ScheduleHistory';

it('labels the recorded original and corrected values with the reason and actor', () => {
  render(<ScheduleHistory corrections={[{
    field: 'breakGroup', before: 1, after: 2, reason: 'แก้กลุ่มพักที่บันทึกผิด',
    at: '2026-11-17T02:00:00Z', actor: 'หัวหน้าทดสอบ',
  }]} />);
  expect(screen.getByText('กลุ่มพักในแผนเดิม · 1')).toBeVisible();
  expect(screen.getByText('กลุ่มพักในแผนที่แก้ · 2')).toBeVisible();
  expect(screen.getByText('เหตุผล · แก้กลุ่มพักที่บันทึกผิด')).toBeVisible();
  expect(screen.getByText(/09:00.*หัวหน้าทดสอบ/)).toBeVisible();
});
