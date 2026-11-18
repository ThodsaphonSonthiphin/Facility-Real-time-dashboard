import { screen, within } from '@testing-library/react';
import { expect, it } from 'vitest';
import { reliefCandidate, reliefWeek, installReliefFixtureClock } from '../reliefEditorTestFixture';
import { commandId, editorApp } from '../scheduleEditorTestFixture';

installReliefFixtureClock();

async function assignment(conflict = false) {
  const app = await editorApp({
    week: reliefWeek,
    routes: {
      'GET /api/schedules/relief-candidates': ({ search }) => {
        const params = new URLSearchParams(search);
        return { status: 200, body: params.get('ownUserId') === '8'
          ? reliefCandidate({ userId: 8, homeBuildingId: 2, homeBuildingCode: 'B', displayName: 'สแปร์สอง' })
          : reliefCandidate() };
      },
      ...(conflict ? { 'POST /api/schedules/commands': () => ({ status: 409, body: {
        commandId, outcome: 'rejected', revisions: {}, problems: [
          { rowId: 'relief:7:2026-11-16:Day', code: 'row-invalid', message: 'คนนี้มีงานสแปร์ในกะนี้แล้ว' },
        ],
      } }) } : {}),
    },
  });
  const opener = await screen.findByRole('button', { name: 'มอบหมายงานสแปร์' });
  opener.focus();
  await app.user.keyboard('{Enter}');
  const drawer = within(await screen.findByRole('dialog', { name: 'มอบหมายงานสแปร์' }));
  return { ...app, drawer };
}

it('requires a group for standalone relief before allowing review', async () => {
  const { user, calls, drawer } = await assignment();
  await user.selectOptions(drawer.getByLabelText('พื้นที่ปลายทาง'), '4');
  await user.selectOptions(drawer.getByLabelText('แม่บ้านสแปร์'), '7');
  await user.click(drawer.getByRole('button', { name: 'ตรวจทาน' }));
  expect(drawer.getByText('เลือกกลุ่มพักครั้งที่ 1')).toBeVisible();
  expect(calls.some((call) => call.method === 'POST' && call.path === '/api/schedules/commands')).toBe(false);
});

it('inherits the replaced regular dated group and lets one Supervisor use both buildings', async () => {
  const { user, calls, drawer } = await assignment();
  await user.selectOptions(drawer.getByLabelText('พื้นที่ปลายทาง'), '4');
  await user.selectOptions(drawer.getByLabelText('แม่บ้านสแปร์'), '8');
  await drawer.findByText('ตึกประจำ: B');
  await user.selectOptions(drawer.getByLabelText('แม่บ้านประจำที่ทำแทน'), '9');
  expect(drawer.queryByLabelText(/ยืนยันว่าตกลง/)).not.toBeInTheDocument();
  await user.click(drawer.getByRole('button', { name: 'ตรวจทาน' }));
  expect(drawer.getByText(/ใช้กลุ่มพัก 1 ของคนที่แทน/)).toBeVisible();
  await user.click(drawer.getByRole('button', { name: 'บันทึก' }));
  expect(await drawer.findByText('บันทึกการเปลี่ยนแปลงแล้ว')).toBeVisible();
  const sent = calls.find((call) => call.path === '/api/schedules/commands')?.body;
  expect(sent).toMatchObject({ kind: 'assign-relief',
    expectedRevisions: { 'relief:8:2026-11-16:Day': '0' },
    payload: { userId: 8, areaId: 4, replacedUserId: 9, sourceBuildingId: 2, agreementReported: false },
  });
  expect(sent).not.toHaveProperty('payload.breakGroup');
  expect(sent).not.toHaveProperty('payload.sourceSupervisorId');
});

it('keeps chosen fields when an assignment races and the server refuses', async () => {
  const { user, drawer } = await assignment(true);
  await user.selectOptions(drawer.getByLabelText('พื้นที่ปลายทาง'), '4');
  await user.selectOptions(drawer.getByLabelText('แม่บ้านสแปร์'), '7');
  await drawer.findByText('ตึกประจำ: A');
  await user.selectOptions(drawer.getByLabelText('กลุ่มพักครั้งที่ 1'), '2');
  await user.click(drawer.getByRole('button', { name: 'ตรวจทาน' }));
  await user.click(drawer.getByRole('button', { name: 'บันทึก' }));
  expect(await drawer.findByText('คนนี้มีงานสแปร์ในกะนี้แล้ว')).toBeVisible();
  expect(drawer.getByLabelText('กลุ่มพักครั้งที่ 1')).toHaveValue('2');
  expect(drawer.getByLabelText('พื้นที่ปลายทาง')).toHaveValue('4');
  expect(drawer.getByLabelText('แม่บ้านสแปร์')).toHaveValue('7');
});
