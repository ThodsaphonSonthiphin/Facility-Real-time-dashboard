import { screen, within } from '@testing-library/react';
import { expect, it } from 'vitest';
import { reliefCandidate, reliefWeek, installReliefFixtureClock } from '../reliefEditorTestFixture';
import { commandId, committed, editorApp } from '../scheduleEditorTestFixture';

installReliefFixtureClock();

it('finds an exact source and employee, requires attestation, and records the named dated source', async () => {
  const { user, calls } = await editorApp({
    week: reliefWeek,
    routes: { 'GET /api/schedules/relief-candidates': () => ({ status: 200, body: reliefCandidate({
      userId: 14, employeeId: 'E014', homeBuildingId: 3, homeBuildingCode: 'C', displayName: 'สแปร์จากตึก C',
      agreementRequired: true, sourceSupervisorId: 22, sourceSupervisorName: 'หัวหน้าตึก C',
    }) }) },
  });
  await user.click(await screen.findByRole('button', { name: 'บันทึกการยืมสแปร์' }));
  const drawer = within(await screen.findByRole('dialog', { name: 'บันทึกการยืมสแปร์' }));
  expect(drawer.getByText('ตกลงกับซุปตึกต้นทางก่อน แล้วจึงบันทึกงานที่นี่')).toBeVisible();
  await user.selectOptions(drawer.getByLabelText('พื้นที่ปลายทาง'), '4');
  await user.selectOptions(drawer.getByLabelText('ตึกต้นทาง'), '3');
  await user.type(drawer.getByLabelText('รหัสพนักงานสแปร์'), 'E014');
  await user.click(drawer.getByRole('button', { name: 'ค้นหาสแปร์' }));
  expect(await drawer.findByText('ตึกประจำ: C')).toBeVisible();
  await user.selectOptions(drawer.getByLabelText('กลุ่มพักครั้งที่ 1'), '2');
  expect(drawer.getByRole('button', { name: 'ตรวจทาน' })).toBeDisabled();
  expect(calls.filter((call) => call.method === 'POST' && call.path.startsWith('/api/schedules'))).toEqual([]);
  const query = new URLSearchParams(calls.find((call) => call.path.endsWith('/relief-candidates'))?.search);
  expect(Object.fromEntries(query)).toEqual({ employeeId: 'E014', sourceBuildingId: '3', destinationAreaId: '4', date: '2026-11-16', shift: 'Day' });
  await user.selectOptions(drawer.getByLabelText('ยืนยันว่าตกลงกับตึกต้นทางนอกเว็บแล้ว'), 'yes');
  await user.click(drawer.getByRole('button', { name: 'ตรวจทาน' }));
  expect(drawer.getByText(/ผู้บันทึกยืนยันว่าตกลงกับตึกต้นทางนอกเว็บแล้ว/)).toBeVisible();
  await user.click(drawer.getByRole('button', { name: 'บันทึก' }));
  expect(await drawer.findByText('บันทึกการเปลี่ยนแปลงแล้ว')).toBeVisible();
  expect(calls.find((call) => call.path === '/api/schedules/commands')?.body).toMatchObject({
    payload: { userId: 14, areaId: 4, sourceBuildingId: 3, sourceSupervisorId: 22, breakGroup: 2, agreementReported: true },
  });
});

it('keeps the loan search and choice after a stale result and asks again only if the source changed', async () => {
  let changed = false;
  const { user, calls } = await editorApp({
    week: reliefWeek,
    routes: {
      'GET /api/schedules/relief-candidates': () => ({ status: 200, body: reliefCandidate({
        userId: 14, employeeId: 'E014', homeBuildingId: 3, homeBuildingCode: 'C', displayName: 'สแปร์จากตึก C',
        agreementRequired: true, sourceSupervisorId: changed ? 24 : 22, sourceSupervisorName: changed ? 'หัวหน้าคนใหม่' : 'หัวหน้าคนเดิม',
        revision: changed ? '3' : '0',
      }) }),
      'POST /api/schedules/commands': () => {
        if (changed) return committed();
        changed = true;
        return { status: 409, body: { commandId, outcome: 'rejected', revisions: {}, problems: [
          { rowId: 'relief:14:2026-11-16:Day', code: 'stale-revision', message: 'รายการเปลี่ยนแล้ว' },
        ] } };
      },
    },
  });
  await user.click(await screen.findByRole('button', { name: 'บันทึกการยืมสแปร์' }));
  const drawer = within(await screen.findByRole('dialog'));
  await user.selectOptions(drawer.getByLabelText('พื้นที่ปลายทาง'), '4');
  await user.selectOptions(drawer.getByLabelText('ตึกต้นทาง'), '3');
  await user.type(drawer.getByLabelText('รหัสพนักงานสแปร์'), 'E014');
  await user.click(drawer.getByRole('button', { name: 'ค้นหาสแปร์' }));
  await user.selectOptions(await drawer.findByLabelText('ยืนยันว่าตกลงกับตึกต้นทางนอกเว็บแล้ว'), 'yes');
  await user.selectOptions(drawer.getByLabelText('กลุ่มพักครั้งที่ 1'), '2');
  await user.click(drawer.getByRole('button', { name: 'ตรวจทาน' }));
  await user.click(drawer.getByRole('button', { name: 'บันทึก' }));
  await user.click(await drawer.findByRole('button', { name: 'โหลดเพื่อเปรียบเทียบ' }));
  expect(await drawer.findByText(/ตอนเปิด.*หัวหน้าคนเดิม/)).toBeVisible();
  expect(drawer.getByText(/ล่าสุด.*หัวหน้าคนใหม่/)).toBeVisible();
  await user.click(drawer.getByRole('button', { name: 'ใช้ข้อมูลล่าสุดและเก็บร่าง' }));
  expect(drawer.getByLabelText('รหัสพนักงานสแปร์')).toHaveValue('E014');
  expect(drawer.getByLabelText('ตึกต้นทาง')).toHaveValue('3');
  expect(drawer.getByLabelText('กลุ่มพักครั้งที่ 1')).toHaveValue('2');
  expect(drawer.getByRole('button', { name: 'ตรวจทาน' })).toBeDisabled();
  await user.selectOptions(drawer.getByLabelText('ยืนยันว่าตกลงกับตึกต้นทางนอกเว็บแล้ว'), 'yes');
  await user.click(drawer.getByRole('button', { name: 'ตรวจทาน' }));
  await user.click(drawer.getByRole('button', { name: 'บันทึก' }));
  expect(await drawer.findByText('บันทึกการเปลี่ยนแปลงแล้ว')).toBeVisible();
  expect(calls.filter((call) => call.path === '/api/schedules/commands').at(-1)?.body).toMatchObject({
    expectedRevisions: { 'relief:14:2026-11-16:Day': '3' },
    payload: { userId: 14, sourceSupervisorId: 24, agreementReported: true, breakGroup: 2 },
  });
});
