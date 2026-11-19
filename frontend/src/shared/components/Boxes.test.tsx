import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { HorizontalScrollArea } from './Boxes';

function table() {
  return (
    <HorizontalScrollArea label="ตารางงานรายสัปดาห์">
      <table aria-label="ตารางงานรายสัปดาห์">
        <tbody><tr><th scope="row">แม่บ้านทดสอบ</th><td>ทำงาน</td></tr></tbody>
      </table>
    </HorizontalScrollArea>
  );
}

function stickyTable() {
  return (
    <HorizontalScrollArea label="ตารางงานรายสัปดาห์" leadingOverlayWidth={140}>
      <table aria-label="ตารางงานรายสัปดาห์">
        <tbody><tr><th scope="row">แม่บ้านทดสอบ</th><td><button type="button">แก้ไขวันสุดท้าย</button></td></tr></tbody>
      </table>
    </HorizontalScrollArea>
  );
}

describe('HorizontalScrollArea', () => {
  it('adds an accessible keyboard scroll region only while its content overflows', async () => {
    render(table());
    const scroller = screen.getByRole('table', { name: 'ตารางงานรายสัปดาห์' }).parentElement!;

    expect(scroller).not.toHaveAttribute('tabindex');
    expect(screen.queryByRole('region', { name: 'ตารางงานรายสัปดาห์' })).not.toBeInTheDocument();

    Object.defineProperty(scroller, 'clientWidth', { configurable: true, value: 340 });
    Object.defineProperty(scroller, 'scrollWidth', { configurable: true, value: 720 });
    fireEvent(window, new Event('resize'));

    const region = await screen.findByRole('region', { name: 'ตารางงานรายสัปดาห์' });
    expect(region).toHaveAttribute('tabindex', '0');
    expect(region).toHaveAttribute('aria-describedby');
    expect(screen.getByText(/เลื่อนตารางไปด้านข้าง/)).toBeInTheDocument();
    fireEvent.keyDown(region, { key: 'ArrowRight' });
    expect(region.scrollLeft).toBe(272);
    fireEvent.keyDown(region, { key: 'ArrowLeft' });
    expect(region.scrollLeft).toBe(0);

    Object.defineProperty(scroller, 'scrollWidth', { configurable: true, value: 320 });
    fireEvent(window, new Event('resize'));
    await waitFor(() => expect(screen.queryByRole('region', { name: 'ตารางงานรายสัปดาห์' })).not.toBeInTheDocument());
    expect(scroller).not.toHaveAttribute('tabindex');
  });

  it('uses the unobscured width for keyboard steps and ignores nested controls and fitting tables', async () => {
    render(stickyTable());
    const scroller = screen.getByRole('table', { name: 'ตารางงานรายสัปดาห์' }).parentElement!;
    Object.defineProperty(scroller, 'clientWidth', { configurable: true, value: 340 });
    Object.defineProperty(scroller, 'scrollWidth', { configurable: true, value: 720 });
    fireEvent(window, new Event('resize'));

    const region = await screen.findByRole('region', { name: 'ตารางงานรายสัปดาห์' });
    expect(region).toHaveStyle({ scrollPaddingInlineStart: '140px' });
    fireEvent.keyDown(region, { key: 'ArrowRight' });
    expect(region.scrollLeft).toBe(160);

    region.scrollLeft = 0;
    fireEvent.keyDown(screen.getByRole('button', { name: 'แก้ไขวันสุดท้าย' }), { key: 'End' });
    expect(region.scrollLeft).toBe(0);

    Object.defineProperty(scroller, 'scrollWidth', { configurable: true, value: 320 });
    fireEvent(window, new Event('resize'));
    await waitFor(() => expect(screen.queryByRole('region', { name: 'ตารางงานรายสัปดาห์' })).not.toBeInTheDocument());
    scroller.scrollLeft = 12;
    fireEvent.keyDown(scroller, { key: 'End' });
    expect(scroller.scrollLeft).toBe(12);
  });
});
