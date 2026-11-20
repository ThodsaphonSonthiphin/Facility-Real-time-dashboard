import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { Button } from './Button';
import { Drawer, Modal } from './Modal';
import { Toast } from './Toast';

describe('Subtle & Snappy Motion classes (facility-0229)', () => {
  it('applies press/transition classes to Button', () => {
    render(<Button>บันทึก</Button>);
    const btn = screen.getByRole('button', { name: 'บันทึก' });
    expect(btn.className).toContain('active:scale-[0.98]');
  });

  it('applies motion classes to Drawer, Modal, and Toast', () => {
    const { container: drawerContainer } = render(
      <Drawer labelledBy="d-title" onClose={vi.fn()}>
        <h2 id="d-title">Drawer</h2>
      </Drawer>,
    );
    expect(drawerContainer.querySelector('.motion-drawer')).not.toBeNull();
    expect(drawerContainer.querySelector('.motion-backdrop')).not.toBeNull();

    const { container: modalContainer } = render(
      <Modal labelledBy="m-title" onClose={vi.fn()}>
        <h2 id="m-title">Modal</h2>
      </Modal>,
    );
    expect(modalContainer.querySelector('.motion-modal')).not.toBeNull();
    expect(modalContainer.querySelector('.motion-backdrop')).not.toBeNull();

    const { container: toastContainer } = render(
      <Toast notice={{ kind: 'ok', text: 'บันทึกแล้ว' }} onClose={vi.fn()} />,
    );
    expect(toastContainer.querySelector('.motion-toast')).not.toBeNull();
  });
});
