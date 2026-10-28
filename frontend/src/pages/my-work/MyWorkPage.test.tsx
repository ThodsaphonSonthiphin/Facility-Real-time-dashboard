import { act, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { area, cleanerSession, myWork, nightShift, notCheckedIn, outsideShift, point } from '../../test/cleanerFixtures';
import { fakeServer } from '../../test/fakeServer';
import { hub } from '../../test/fakeSignalR';
import { renderApp } from '../../test/renderApp';

function serveMyWork(answer: unknown, status = 200) {
  return fakeServer({ ...cleanerSession, 'GET /api/my-work': () => ({ status, body: answer }) });
}

/** The hub's first start asks again (facility-0064): wait for that catch-up answer before counting what a press asks. */
async function settled(server: ReturnType<typeof serveMyWork>) {
  await waitFor(() => expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(2));
  await waitFor(() => expect(screen.getByRole('button', { name: 'อัปเดต' })).toBeEnabled());
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
  it('shows the live line and "อัปเดต" only with the list, so the list never pushes them down (layout shift)', async () => {
    let answer: () => void = () => {};
    fakeServer({
      ...cleanerSession,
      'GET /api/my-work': () =>
        new Promise((resolve) => {
          answer = () => resolve({ status: 200, body: myWork(undefined, [area(fourPoints)]) });
        }),
    });
    await renderApp('/my-work');

    await screen.findByText('กำลังโหลดงาน...');
    expect(screen.queryByRole('button', { name: /อัปเดต/ })).not.toBeInTheDocument();

    act(() => answer());
    await screen.findByRole('region', { name: 'ทำอันนี้ก่อน' });
    expect(screen.getByRole('button', { name: /อัปเดต/ })).toBeInTheDocument();
  });

  it('shows every point most urgent first, whatever order the server sends, under the shift line', async () => {
    serveMyWork(myWork(undefined, [area(fourPoints)]));
    await renderApp('/my-work');

    await screen.findByRole('region', { name: 'ทำอันนี้ก่อน' });
    expect(screen.getAllByRole('heading', { level: 3 }).map((h) => h.textContent)).toEqual([
      'จุดทิ้งขยะ',
      'โถงทางเดิน',
      'ห้องน้ำชาย',
      'ห้องน้ำหญิง',
    ]);
    expect(screen.getByText('กะเช้า 07:00–19:00 · เข้างาน 06:52')).toBeInTheDocument();
  });

  it('puts the most urgent point first in "ทำอันนี้ก่อน", with its defect and what to do (variant B, #73)', async () => {
    serveMyWork(myWork(undefined, [area(fourPoints)]));
    await renderApp('/my-work');

    const next = await screen.findByRole('region', { name: 'ทำอันนี้ก่อน' });
    expect(within(next).getByRole('heading', { level: 3 })).toHaveTextContent('จุดทิ้งขยะ');
    expect(within(next).getByText('ต้องแก้ไข')).toBeInTheDocument();
    expect(within(next).getByText('Area ทดลอง · รอบ 08:00–10:00')).toBeInTheDocument();
    expect(within(next).getByText('หัวหน้าให้แก้ 09:40: “รอบถังขยะยังมีคราบ”')).toBeInTheDocument();
    expect(within(next).getByText('แก้แล้วสแกน QR ที่จุดนี้เพื่อส่งงานใหม่')).toBeInTheDocument();
  });

  it('picks the next job across the own and the covered Areas, with the words for its status (#73)', async () => {
    const covered = area(
      [
        point({
          id: 9,
          name: 'ห้องพักพนักงาน',
          areaId: 12,
          areaName: 'Area 12',
          buildingCode: 'B',
          status: 'Overdue',
          currentRound: { id: 91, start: '08:00:00', end: '09:30:00' },
        }),
      ],
      { areaId: 12, areaCode: 'AR12', areaName: 'Area 12', isCover: true },
    );
    serveMyWork(myWork(undefined, [area([point({ status: 'NotYetDone' })]), covered]));
    await renderApp('/my-work');

    const next = await screen.findByRole('region', { name: 'ทำอันนี้ก่อน' });
    expect(within(next).getByRole('heading', { level: 3 })).toHaveTextContent('ห้องพักพนักงาน');
    expect(within(next).getByText('Area 12 · ตึก B (ทำแทน) · รอบ 08:00–09:30')).toBeInTheDocument();
    expect(screen.getByText('0 จาก 2 จุด ส่งแล้ว')).toBeInTheDocument();
    expect(within(next).getByText('ยังไม่ส่งงาน · เลยเวลารอบแล้ว')).toBeInTheDocument();
    expect(within(next).getByText('สแกน QR ที่จุดนี้เพื่อส่งงาน')).toBeInTheDocument();
  });

  it('lists the rest in ต้องทำต่อ / ส่งแล้ว / ยังไม่ต้องทำ, with their chips and any reported issue (#73)', async () => {
    const later = [
      point({ id: 5, name: 'ห้องเก็บของ', status: 'BeforeFirstRound', currentRound: undefined }),
      point({ id: 6, name: 'ห้องประชุม', status: 'OffHours', currentRound: undefined, nextRound: undefined }),
    ];
    serveMyWork(myWork(undefined, [area([...later, ...fourPoints])]));
    await renderApp('/my-work');

    const names = (group: string) =>
      within(screen.getByRole('region', { name: group }))
        .getAllByRole('heading', { level: 3 })
        .map((h) => h.textContent);
    await screen.findByRole('region', { name: 'ทำอันนี้ก่อน' });
    expect(screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent)).toEqual([
      'ทำอันนี้ก่อน',
      'ต้องทำต่อ',
      'ส่งแล้ว',
      'ยังไม่ต้องทำ',
    ]);
    expect(names('ต้องทำต่อ')).toEqual(['โถงทางเดิน']);
    expect(names('ส่งแล้ว')).toEqual(['ห้องน้ำชาย', 'ห้องน้ำหญิง']);
    expect(names('ยังไม่ต้องทำ')).toEqual(['ห้องเก็บของ', 'ห้องประชุม']);
    const notYet = screen.getByRole('region', { name: 'ยังไม่ต้องทำ' });
    expect(within(notYet).getByText('ยังไม่ถึงรอบ')).toBeInTheDocument();
    expect(within(notYet).getByText('นอกเวลา')).toBeInTheDocument();
    const sent = screen.getByRole('region', { name: 'ส่งแล้ว' });
    expect(within(sent).getByText('แจ้งปัญหา: ของชำรุด · ก๊อกอ่างที่ 2 รั่ว')).toBeInTheDocument();
    expect(within(sent).getAllByText('Area ทดลอง · รอบ 10:00–12:00')).toHaveLength(2);
  });

  it('a second ต้องแก้ไข point shows its defect on its row too (owner, 2026-10-22)', async () => {
    const rework = (id: number, name: string, defect: string) =>
      point({ id, name, status: 'Rework', rework: { defect, requestedAt: '2026-10-15T02:40:00Z' } }); // 09:40
    serveMyWork(myWork(undefined, [area([rework(1, 'ห้องน้ำหญิง', 'พื้นเปียก'), rework(2, 'ห้องน้ำชาย', 'กระจกมีคราบ')])]));
    await renderApp('/my-work');

    const later = await screen.findByRole('region', { name: 'ต้องทำต่อ' });
    expect(within(later).getByText('หัวหน้าให้แก้ 09:40: “กระจกมีคราบ”')).toBeInTheDocument();
  });

  it('hides a group with nothing in it (#73)', async () => {
    serveMyWork(myWork(undefined, [area([point({ status: 'Rework', rework: { defect: 'พื้นเปียก', requestedAt: '2026-10-15T02:40:00Z' } })])]));
    await renderApp('/my-work');

    await screen.findByRole('region', { name: 'ทำอันนี้ก่อน' });
    for (const group of ['ต้องทำต่อ', 'ส่งแล้ว', 'ยังไม่ต้องทำ']) {
      expect(screen.queryByRole('region', { name: group })).not.toBeInTheDocument();
    }
    expect(screen.getByText('0 จาก 1 จุด ส่งแล้ว')).toBeInTheDocument();
  });

  it('counts the sent points out of all of them, รอตรวจ and ตรวจผ่าน both counting as sent (#73)', async () => {
    serveMyWork(myWork(undefined, [area(fourPoints)]));
    await renderApp('/my-work');

    expect(await screen.findByText('2 จาก 4 จุด ส่งแล้ว')).toBeInTheDocument();
  });

  it('with nothing left to do now, a note takes the large card\'s place (#73)', async () => {
    serveMyWork(myWork(undefined, [area([point({ status: 'PendingInspection' }), point({ id: 12, status: 'BeforeFirstRound' })])]));
    await renderApp('/my-work');

    expect(await screen.findByText('ส่งครบทุกจุดที่ต้องทำตอนนี้แล้ว')).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'ทำอันนี้ก่อน' })).not.toBeInTheDocument();
  });

  it('a card sent back shows the defect and says to scan the point again, with no link to the scan page (facility-0059)', async () => {
    serveMyWork(myWork(undefined, [area(fourPoints)]));
    await renderApp('/my-work');

    expect(await screen.findByText('หัวหน้าให้แก้ 09:40: “รอบถังขยะยังมีคราบ”')).toBeInTheDocument();
    expect(screen.getByText('แก้แล้วสแกน QR ที่จุดนี้เพื่อส่งงานใหม่')).toBeInTheDocument();
    expect(within(screen.getByRole('main')).queryAllByRole('link')).toHaveLength(0);
  });

  it('a covered Area\'s points join the same groups, marked ทำแทน, under the cover line (M14)', async () => {
    const covered = area([point({ id: 9, name: 'ห้องพักพนักงาน', areaId: 12, areaName: 'Area 12', buildingCode: 'B' })], {
      areaId: 12,
      areaCode: 'AR12',
      areaName: 'Area 12',
      isCover: true,
    });
    serveMyWork(myWork(undefined, [area(), covered]));
    await renderApp('/my-work');

    expect(await screen.findByText('วันนี้ทำแทน Area 12 ด้วย · สิทธิ์หมดเองเมื่อหมดกะ')).toBeInTheDocument();
    // both points are ยังไม่ทำ: the own Area's comes first (the server's order), the covered one is the next row
    const next = screen.getByRole('region', { name: 'ทำอันนี้ก่อน' });
    expect(within(next).getByText('Area ทดลอง · รอบ 10:00–12:00')).toBeInTheDocument();
    const later = screen.getByRole('region', { name: 'ต้องทำต่อ' });
    expect(within(later).getByText('Area 12 · ตึก B (ทำแทน) · รอบ 10:00–12:00')).toBeInTheDocument();
  });

  it('before Shift-In the shift line says so', async () => {
    serveMyWork(myWork(notCheckedIn));
    await renderApp('/my-work');

    expect(await screen.findByText('กะเช้า 07:00–19:00 · ยังไม่ได้ลงเวลาเข้างาน')).toBeInTheDocument();
    const next = screen.getByRole('region', { name: 'ทำอันนี้ก่อน' });
    expect(within(next).getByText('ยังไม่ส่ง · ส่งได้ถึง 12:00')).toBeInTheDocument();
    expect(within(next).getByText('สแกน QR ที่จุดนี้เพื่อส่งงาน')).toBeInTheDocument();
  });

  it('outside the own shift: the shift and when check-in opens, with no counts and no cards (facility-0077)', async () => {
    serveMyWork(myWork(outsideShift, [area([point({ status: 'Passed' })])], nightShift));
    await renderApp('/my-work');

    expect(await screen.findByText('ตอนนี้ไม่อยู่ในกะของคุณ')).toBeInTheDocument();
    expect(screen.getByText('กะดึก 19:00–07:00')).toBeInTheDocument();
    expect(screen.getByText('ลงเวลาเข้างานได้ตั้งแต่ 18:00')).toBeInTheDocument();
    // another shift's work is not this Cleaner's: no progress line, no large card and no rows
    expect(screen.queryByText(/จุด ส่งแล้ว$/)).not.toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'ทำอันนี้ก่อน' })).not.toBeInTheDocument();
    expect(screen.queryByText('ห้องน้ำชาย')).not.toBeInTheDocument();
    // the hub's first start asks again, and the button is busy while that answer is on its way
    await waitFor(() => expect(screen.getByRole('button', { name: 'อัปเดต' })).toBeEnabled());
  });

  it('asks the server again on "อัปเดต"', async () => {
    const server = serveMyWork(myWork());
    const { user } = await renderApp('/my-work');
    await screen.findByRole('region', { name: 'ทำอันนี้ก่อน' });
    await settled(server);

    await user.click(screen.getByRole('button', { name: 'อัปเดต' }));

    await waitFor(() => expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(3));
  });

  it('asks the server again each time the page opens (facility-0052)', async () => {
    const server = serveMyWork(myWork());
    const { router } = await renderApp('/my-work');
    await screen.findByRole('region', { name: 'ทำอันนี้ก่อน' });
    await settled(server);

    await act(() => router.navigate('/nowhere'));
    await act(() => router.navigate('/my-work'));

    await waitFor(() => expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(3));
  });

  it('a Cleaner without an Area is told to ask the Admin', async () => {
    serveMyWork(myWork(undefined, []));
    await renderApp('/my-work');

    expect(
      await screen.findByText('ยังไม่มีจุดที่ต้องทำ บัญชีนี้ยังไม่ได้รับพื้นที่ หรือพื้นที่ยังไม่มีจุด แจ้งแอดมิน'),
    ).toBeInTheDocument();
  });

  it('a failed load keeps the heading and shows the shared error wording', async () => {
    serveMyWork(undefined, 500);
    await renderApp('/my-work');

    expect(await screen.findByRole('alert')).toHaveTextContent('เซิร์ฟเวอร์ขัดข้อง ลองใหม่อีกครั้ง');
    expect(screen.getByRole('heading', { name: 'งานของฉัน' })).toBeInTheDocument();
  });

  it("opens a live connection with the Cleaner's token and says the page updates by itself", async () => {
    serveMyWork(myWork(undefined, [area(fourPoints)]));
    await renderApp('/my-work');

    expect(await screen.findByText('หน้านี้อัปเดตเองเมื่อหัวหน้าตรวจงาน')).toBeInTheDocument();
    expect(hub.latest().tokens).toEqual(['token-cleaner']);
  });

  it('asks again when the hub says a point changed, so an inspection shows without a press', async () => {
    let answer = myWork(undefined, [area(fourPoints)]);
    const server = fakeServer({ ...cleanerSession, 'GET /api/my-work': () => ({ status: 200, body: answer }) });
    await renderApp('/my-work');
    await screen.findByRole('region', { name: 'ทำอันนี้ก่อน' });
    await settled(server);
    expect(screen.queryByText(/หัวหน้าให้แก้ 10:30/)).not.toBeInTheDocument();

    // the Supervisor marked ห้องน้ำชาย ไม่สะอาด: the server now answers with it as Rework
    answer = myWork(undefined, [
      area([
        ...fourPoints.slice(0, 1),
        point({ id: 2, name: 'ห้องน้ำชาย', status: 'Rework', rework: { defect: 'พื้นยังเปียก', requestedAt: '2026-10-15T03:30:00Z' } }),
        ...fourPoints.slice(2),
      ]),
    ]);
    act(() => hub.latest().push('InspectionRecorded', { ...fourPoints[1], status: 'Rework' }));

    expect(await screen.findByText('หัวหน้าให้แก้ 10:30: “พื้นยังเปียก”')).toBeInTheDocument();
    expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(3);
  });

  it('a dropped hub says so, and coming back asks the server again (facility-0064)', async () => {
    const server = serveMyWork(myWork(undefined, [area(fourPoints)]));
    await renderApp('/my-work');
    await screen.findByText('หน้านี้อัปเดตเองเมื่อหัวหน้าตรวจงาน');
    await settled(server);

    act(() => hub.latest().reconnecting());
    expect(screen.getByText('การอัปเดตสดหลุด กำลังต่อใหม่ กด "อัปเดต" เพื่อดูงานล่าสุด')).toBeInTheDocument();

    act(() => hub.latest().reconnected());

    await waitFor(() => expect(server.calls.filter((c) => c.path === '/api/my-work')).toHaveLength(3));
    expect(screen.getByText('หน้านี้อัปเดตเองเมื่อหัวหน้าตรวจงาน')).toBeInTheDocument();
  });
});
