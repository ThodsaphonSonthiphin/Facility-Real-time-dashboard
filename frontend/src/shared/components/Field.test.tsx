import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ErrorSummary, Field, TextInput } from './Field';

describe('Field (Enterprise Light, #122)', () => {
  it('marks a wrong answer and reads its message after the help line', () => {
    render(
      <Field label="รัศมีป้าย (เมตร)" help="ระยะจากป้าย" error="ใส่ได้ 10–200 เมตร">
        <TextInput size="desk" defaultValue="5000" />
      </Field>,
    );

    const input = screen.getByLabelText('รัศมีป้าย (เมตร)');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveAccessibleDescription('ใส่ได้ 10–200 เมตร ระยะจากป้าย');
  });

  it('leaves a right answer unmarked', () => {
    render(
      <Field label="ชื่อพื้นที่" help="แสดงบนป้าย">
        <TextInput size="desk" />
      </Field>,
    );

    const input = screen.getByLabelText('ชื่อพื้นที่');
    expect(input).not.toHaveAttribute('aria-invalid');
    expect(input).toHaveAccessibleDescription('แสดงบนป้าย');
  });

  it('links each wrong answer of a form to its control', () => {
    render(<ErrorSummary title="บันทึกไม่ได้ ตรวจ 1 ช่อง" errors={[{ fieldId: 'radius', message: 'รัศมีป้าย' }]} />);

    expect(screen.getByRole('alert')).toHaveTextContent('บันทึกไม่ได้ ตรวจ 1 ช่อง');
    expect(screen.getByRole('link', { name: 'รัศมีป้าย' })).toHaveAttribute('href', '#radius');
  });
});
