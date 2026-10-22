import { act, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { area, cleanerSession, myWork, nightShift, notCheckedIn, outsideShift, point } from '../../test/cleanerFixtures';
import { fakeServer } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

function serveMyWork(answer: unknown, status = 200) {
  return fakeServer({ ...cleanerSession, 'GET /api/my-work': () => ({ status, body: answer }) });
}

/** Wireframe M9's four points, in the server's order (not by urgency). */
const fourPoints = [
  point({
    id: 1,
    name: 'ห้องน้ำหญิง',
    status: 'Passed',
    lastScan: { submittedAt: '2026-10-15T03:10:00Z', cleanerName: 'แม่บ้านทดสอบ', placement: 'OnTime', status: 'Issue' }, // 10:10
    issue: { tags: ['ของชำรุด'], note: 'ก๊อกอ่างที่ 2 รั่ว', reportedAt: '2026-10-15T03:10:00Z' },
  }),
  point({
    id: 2,
    name: 'ห้องน้ำชาย',
    status: 'PendingInspection',
    lastScan: { submittedAt: '2026-10-15T03:05:00Z', cleanerName: 'แม่บ้านทดสอบ', placement: 'OnTime', status: 'Normal' }, // 10:05
  }),
  point({ id: 3, name: 'โถงทางเดิน', status: 'Overdue', currentRound: { id: 31, start: '07:30:00', end: '09:00:00' } }),
  point({
    id: 4,
    name: 'จุดทิ้งขยะ',
    status: 'Rework',
    currentRound: { id: 41, start: '08:00:00', end: '10:00:00' },
    rework: { defect: 'รอบถังขยะยังมีคราบ', requestedAt: '2026-10-15T02:40:00Z' }, // 09:40
  }),
];

describe('MyWorkPage (M9, M14, facility-0052)', () => {
  it('lists the own Area most urgent first, with the summary and the shift line', async () => {
    serveMyWork(myWork(undefined, [area(fourPoints)]));
    await renderApp('/my-work');

    const ownArea = await screen.findByRole('region', { name: 'Area ทดลอง (Area ของฉัน)' });
    expect(within(ownArea).getAllByRole('heading', { level: 3 }).map((h) => h.textContent)).toEqual([
      'จุดทิ้งขยะ',
      'โถงทางเดิน',
      'ห้องน้ำชาย',
      'ห้องน้ำหญิง',
    ]);
    const summary = screen.getByRole('list', { name: 'สรุปงาน' });
    expect(within(summary).getAllByRole('listitem').map((li) => li.textContent)).toEqual([
      '1 ต้องแก้',
      '1 เลยรอบ',
      '2 ส่งแล้ว',
    ]);
    expect(screen.getByText('กะเช้า 07:00–19:00 · เข้างาน 06:52')).toBeInTheDocument();
    expect(screen.getByText('ส่งงาน 10:05 · รอบถัดไป 13:00–15:00')).toBeInTheDocument();
  });

  it('a card sent back shows the defect and says to scan the point again, with no link to the scan page (facility-0059)', async () => {
    serveMyWork(myWork(undefined, [area(fourPoints)]));
    await renderApp('/my-work');

    expect(await screen.findByText('หัวหน้าให้แก้ 09:40: “รอบถังขยะยังมีคราบ”')).toBeInTheDocument();
    expect(screen.getByText('แก้แล้วสแกน QR ที่จุดนี้เพื่อส่งงานใหม่')).toBeInTheDocument();
    expect(within(screen.getByRole('main')).queryAllByRole('link')).toHaveLength(0);
  });

  it('shows the reported issue as a tag and what an overdue point still needs (facility-0046)', async () => {
    serveMyWork(myWork(undefined, [area(fourPoints)]));
    await renderApp('/my-work');

    expect(await screen.findByText('แจ้งปัญหา: ของชำรุด · ก๊อกอ่างที่ 2 รั่ว')).toBeInTheDocument();
    expect(screen.getByText('ยังไม่ส่งงาน · เลยเวลารอบแล้ว')).toBeInTheDocument();
    expect(screen.getByText('ตรวจผ่าน · ส่งงาน 10:10 · รอบถัดไป 13:00–15:00')).toBeInTheDocument();
  });

  it('covered Areas come after the own Area, marked ทำแทน (M14)', async () => {
    const covered = area([point({ id: 9, name: 'ห้องพักพนักงาน', areaId: 12, areaName: 'Area 12', buildingCode: 'B' })], {
      areaId: 12,
      areaCode: 'AR12',
      areaName: 'Area 12',
      isCover: true,
    });
    serveMyWork(myWork(undefined, [area(), covered]));
    await renderApp('/my-work');

    expect(await screen.findByText('วันนี้ทำแทน Area 12 ด้วย · สิทธิ์หมดเองเมื่อหมดกะ')).toBeInTheDocument();
    expect(screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent)).toEqual([
      'Area ทดลอง (Area ของฉัน)',
      'Area 12 · ตึก B (ทำแทน)',
    ]);
  });

  it('before Shift-In the shift line says so', async () => {
    serveMyWork(myWork(notCheckedIn));
    await renderApp('/my-work');

    expect(await screen.findByText('กะเช้า 07:00–19:00 · ยังไม่ได้ลงเวลาเข้างาน')).toBeInTheDocument();
    expect(screen.getByText('ยังไม่ส่ง · ส่งได้ถึง 12:00')).toBeInTheDocument();
  });

  it('outside the own shift: the shift and when check-in opens, with no counts and no cards (facility-0077)', async () => {
    serveMyWork(myWork(outsideShift, [area([point({ status: 'Passed' })])], nightShift));
    await renderApp('/my-work');

    expect(await screen.findByText('ตอนนี้ไม่อยู่ในกะของคุณ')).toBeInTheDocument();
    expect(screen.getByText('กะดึก 19:00–07:00')).toBeInTheDocument();
    expect(screen.getByText('ลงเวลาเข้างานได้ตั้งแต่ 18:00')).toBeInTheDocument();
    // another shift's work is not this Cleaner's: no "ส่งแล้ว" count and no card
    expect(screen.queryByRole('list', { name: 'สรุปงาน' })).not.toBeInTheDocument();
    expect(screen.queryByText('ห้องน้ำชาย')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'อัปเดต' })).toBeEnabled();
  });

  it('asks the server again on "อัปเดต" (no push for Cleaners, facility-0052)', async () => {
    const server = serveMyWork(myWork());
    const { user } = await renderApp('/my-work');
    await screen.findByRole('region', { name: 'Area ทดลอง (Area ของฉัน)' });
    expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(1);

    await user.click(screen.getByRole('button', { name: 'อัปเดต' }));

    await waitFor(() => expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(2));
  });

  it('asks the server again each time the page opens (facility-0052)', async () => {
    const server = serveMyWork(myWork());
    const { router } = await renderApp('/my-work');
    await screen.findByRole('region', { name: 'Area ทดลอง (Area ของฉัน)' });
    expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(1);

    await act(() => router.navigate('/nowhere'));
    await act(() => router.navigate('/my-work'));

    await waitFor(() => expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(2));
  });

  it('a Cleaner without an Area is told to ask the Admin', async () => {
    serveMyWork(myWork(undefined, []));
    await renderApp('/my-work');

    expect(
      await screen.findByText('ยังไม่มีจุดที่ต้องทำ บัญชีนี้ยังไม่ได้รับ Area หรือ Area ยังไม่มีจุด แจ้ง Admin'),
    ).toBeInTheDocument();
  });

  it('a failed load keeps the heading and shows the shared error wording', async () => {
    serveMyWork(undefined, 500);
    await renderApp('/my-work');

    expect(await screen.findByRole('alert')).toHaveTextContent('เซิร์ฟเวอร์ขัดข้อง ลองใหม่อีกครั้ง');
    expect(screen.getByRole('heading', { name: 'งานของฉัน' })).toBeInTheDocument();
  });
});
