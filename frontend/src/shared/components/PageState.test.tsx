import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { Splash } from './Boxes';
import { PageDisabled, PageEmpty, PageLoading } from './PageState';

describe('page states (DESIGN_SPEC §6, #122)', () => {
  it('a page waiting for its answer says so and is marked busy', () => {
    const { rerender } = render(<PageLoading />);
    expect(screen.getByText('กำลังโหลด…').closest('[aria-busy]')).toHaveAttribute('aria-busy', 'true');

    rerender(<Splash>กำลังเปิดป้าย...</Splash>);
    expect(screen.getByText('กำลังเปิดป้าย...')).toHaveAttribute('aria-busy', 'true');
  });

  it('an empty list and a turned-off thing say what that means', () => {
    render(
      <>
        <PageEmpty title="ยังไม่มีรายการที่ขึ้นเตือน">เมื่อมีการสแกนผิดพื้นที่ จะขึ้นที่นี่</PageEmpty>
        <PageDisabled>พื้นที่นี้ปิดใช้งานแล้ว</PageDisabled>
      </>,
    );

    expect(screen.getByText('ยังไม่มีรายการที่ขึ้นเตือน')).toBeInTheDocument();
    expect(screen.getByText('เมื่อมีการสแกนผิดพื้นที่ จะขึ้นที่นี่')).toBeInTheDocument();
    expect(screen.getByText('พื้นที่นี้ปิดใช้งานแล้ว')).toBeInTheDocument();
  });
});
