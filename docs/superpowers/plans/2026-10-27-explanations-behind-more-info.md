# Explanations Behind ℹ on the Remaining Pages: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On D1–D5, D10, the buildings page, M17 and the Cleaner's phone pages, the always-on explanations about how a page counts or keeps things move behind the `MoreInfo` link, as D6–D9 already do.

**Architecture:** No new behaviour and no server change. `MoreInfo` (a native `<details>`) gains a `size` prop (`desk` default, `phone` for 48 px targets). Each listed paragraph is wrapped in `MoreInfo`; warnings about what the user is about to do and a field's help stay in view. One test helper checks "behind ℹ" and "still in view".

**Tech Stack:** React 19, TypeScript, Tailwind 4, Vitest + Testing Library on jsdom.

**Spec:**
- `docs/handoff.md`, #80 entry "Explanations always on screen (owner's question, 2026-10-26)": the rule. A warning about what the user is about to do and a field's help line stay; notes about how a page counts or keeps things go behind `MoreInfo`.
- Owner, 2026-10-27: "ย้ายครับ". Use the same rule on the other Admin pages (D1–D4, D10, M17) and on the phone pages.
- Precedent commit `dc74444` (D6–D9). No test changed there.
- `design-system/facility/MASTER.md`, row `MoreInfo`.

## Global Constraints

- Screen words are Thai and follow facility-0085. Move each moved sentence **word for word**. The only reworded line is the D3 covers note (see the table).
- Phone targets are at least 48 px high; Admin targets at least 36 px (`docs/agents/frontend-standards.md`).
- Icons: `size-5` beside phone text, `size-4` beside Admin text, always `aria-hidden` (MASTER.md, facility-0088).
- No inline styles and no new colours. Use only the existing tokens (`text-muted`, `text-primary`, …).
- Pre-commit (husky) runs frontend lint, tsc and tests. Every commit must pass them.
- Never push, open a PR or merge without the owner's explicit OK. The owner tests the branch on their own machine first.

## What moves and what stays

The rule decided each row. The owner can overrule any row when reviewing this plan.

| Page | File:line | Text (start) | Decision | `MoreInfo` summary |
|---|---|---|---|---|
| D1 | `dashboard/DashboardPage.tsx:42` | รอบเป็นช่วงเวลา … ไม่ใช่สถานะของการ์ด | **move** (how cards count) | วิธีอ่านการ์ด |
| D2 | `attendance/AttendancePage.tsx:166-181` | tile lines (ไม่รวมพื้นที่ที่ปิดกะนี้ …) | stay (they label the number beside them) | — |
| D2 | `attendance/AttendancePage.tsx:188-206` `BoardNotes` | หน้านี้ดูว่าแต่ละพื้นที่… / เรียงพื้นที่… / ลืมลงเวลาเลิกงาน… | **move** | เกี่ยวกับหน้านี้ |
| D3 | `alerts/AlertsPage.tsx:151` | ไม่มีเตือนจากเวลาที่สแกน · … เก็บลงประวัติ… | **move** | เกี่ยวกับหน้านี้ |
| D3 | `alerts/CoversThisShift.tsx:64` | การตรวจแทนตึกของหัวหน้ายังไม่แสดงในหน้านี้ | **move** into D3's page `MoreInfo`, reworded "รายการคนทำแทนยังไม่แสดงการตรวจแทนตึกของหัวหน้า" (it leaves its section, so it names the list) | เกี่ยวกับหน้านี้ |
| D3 | `alerts/CoverForm.tsx:77-82` | มีผลเฉพาะ… หมดสิทธิ์เองเมื่อจบกะ / แม่บ้านที่ทำแทนลงเวลา… | stay (what the Admin is about to do) | — |
| D3 | `alerts/EmptyAreas.tsx:50-58` | section sub-line | stay (context of the list) | — |
| D4 | `areas/AreasPage.tsx:35` | แม่บ้านสแกนได้เฉพาะป้ายของพื้นที่ตัวเอง · … | **move** | เกี่ยวกับหน้านี้ |
| D4 | `areas/SignPanel.tsx:87, 107, 159` | M17 how-to, map-pick help, radius help | stay (help for the control beside it) | — |
| D4 | `areas/PointEditor.tsx:130, 145` | window rules, deactivate result | stay (field help; warning) | — |
| D10 | `areas/AreaFormPage.tsx:39`, `areas/AreaEditorDrawer.tsx:53` | การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน | **move** | เกี่ยวกับการบันทึก |
| D10 | `areas/AreaForm.tsx:80, 138`, `areas/AreaActiveSection.tsx:46` | code fixed, Cleaner pickers help, deactivate result | stay | — |
| ตึก | `buildings/BuildingsPage.tsx:44` | ไม่มีปุ่มลบ ใช้ปิดใช้งานแทน … | **move** (same kind as D6's, which moved) | เกี่ยวกับหน้านี้ |
| ตึก | `buildings/BuildingDrawer.tsx:131` | การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน | **move** | เกี่ยวกับการบันทึก |
| ตึก | `buildings/BuildingDrawer.tsx:120` | ตึกหายจากฟอร์ม… (deactivate result) | stay | — |
| D5 | `print-signs/PrintSignsPage.tsx:117` | ระบบสร้าง QR ในเครื่องนี้เอง ไม่ส่งรหัสออกไปเว็บภายนอก | **move** (split from the line) | เกี่ยวกับ QR |
| D5 | same line | ติดเสร็จให้สแกนทดสอบ 1 ครั้ง หน้าจอต้องขึ้นชื่อจุดตรงกับป้าย | stay (what to do after printing) | — |
| D5 | `print-signs/PrintSignsPage.tsx:96` | ที่อยู่บนป้าย: … | stay (data the https warning refers to) | — |
| M17 | `capture/CapturePage.tsx:83` | ต้องเปิดผ่าน HTTPS · การบันทึกเก็บลงประวัติ… | **move**, phone size (the page already errors on plain http) | เกี่ยวกับหน้านี้ |
| M1 check-in | `check-in/CheckInPage.tsx:138` | ระบบแสดงปุ่มถัดไปให้เองตามลำดับ · กดซ้ำภายใน 5 นาทีไม่นับ · ไม่คิดสายหรือกลับก่อน | **move**, phone size | เกี่ยวกับการลงเวลา |
| M4 scan | `scan/SubmitForm.tsx:106` | ถ้าไม่มีสัญญาณ หน้าจอจะขึ้น "ยังไม่ได้บันทึก" … | **move**, phone size | เกี่ยวกับการส่งงาน |
| M12 | `scan/SendFailed.tsx:41, 44`, `inspect/InspectSendFailed.tsx:41` | resend timing; nothing is kept on the phone | stay (warns what happens if they do not resend now) | — |
| M6 | `inspect/InspectForm.tsx:93` | ไม่สะอาด = แม่บ้านเห็นสิ่งที่ต้องแก้ทันที… | stay (what the choice does) | — |
| M16 | `inspect/NothingToInspect.tsx:44` | ไม่มีปุ่มตรวจจนกว่า… | stay (why no button) | — |
| M13 | `scan/OffDuty.tsx:27` | ถ้าลงเวลาไม่ได้… แจ้งแอดมิน | stay (what to do instead) | — |

My Work and My Building have no line of this kind. D6–D9 are already done.

## File Structure

- Create `frontend/src/test/moreInfo.ts`: the two test helpers `expectBehindMoreInfo` and `expectInView`.
- Modify `frontend/src/shared/components/MoreInfo.tsx`: the `size` prop.
- Modify the page files in the table, and their page tests (one new `it` per page test file).
- Modify `design-system/facility/MASTER.md` (the `MoreInfo` row) and `docs/handoff.md`.

---

### Task 0: Branch

- [ ] **Step 1:** From the repo root, on `master`: `git checkout -b feat/more-info-rest`. The uncommitted `docs/handoff.md`, `.gitignore`, `docs/adr/facility-0092-…md` and root `package-lock.json` come along untouched. **Do not stage or commit them** (they are the owner's to decide). In every commit below, `git add` only the files named.

### Task 1: Test helpers + the live board pages (D1, D2, D3)

**Files:**
- Create: `frontend/src/test/moreInfo.ts`
- Modify: `frontend/src/pages/dashboard/DashboardPage.tsx:16-17,42`
- Modify: `frontend/src/pages/attendance/AttendancePage.tsx:188-206`
- Modify: `frontend/src/pages/alerts/AlertsPage.tsx:151-153`, `frontend/src/pages/alerts/CoversThisShift.tsx:64`
- Test: `frontend/src/pages/dashboard/DashboardPage.test.tsx`, `frontend/src/pages/attendance/AttendancePage.test.tsx`, `frontend/src/pages/alerts/AlertsPage.test.tsx`

**Interfaces:**
- Produces: `expectBehindMoreInfo(text: string | RegExp, summary: string): void` and `expectInView(text: string | RegExp): void` from `frontend/src/test/moreInfo.ts`. Tasks 2 and 3 import them.

- [ ] **Step 1: Write the helpers**

`frontend/src/test/moreInfo.ts`:

```ts
import { screen } from '@testing-library/react';
import { expect } from 'vitest';

/**
 * Every element showing `text` sits in a closed MoreInfo whose summary is `summary` (owner, 2026-10-26: notes on how a
 * page counts or keeps things wait until asked). jsdom does not hide a closed <details>, so text queries still find it.
 */
export function expectBehindMoreInfo(text: string | RegExp, summary: string) {
  for (const element of screen.getAllByText(text)) {
    const details = element.closest('details');
    expect(details).not.toBeNull();
    expect(details).not.toHaveAttribute('open');
    expect(details?.querySelector('summary')).toHaveTextContent(summary);
  }
}

/** Every element showing `text` is on the page itself, not shut in a MoreInfo (a warning or a field's help). */
export function expectInView(text: string | RegExp) {
  for (const element of screen.getAllByText(text)) {
    expect(element.closest('details')).toBeNull();
  }
}
```

Note: Testing Library's text match reads only an element's own text nodes, so a `<span>` holding a `<Link>` still matches on its own words. A regex anchored with `^` matches the span, not its ancestors.

- [ ] **Step 2: Write the failing tests**

In `DashboardPage.test.tsx`, add the import `import { expectBehindMoreInfo } from '../../test/moreInfo';`, and add inside the `describe`:

```ts
  it('the notes on reading a card wait behind ℹ (owner, 2026-10-27)', async () => {
    serve();
    await renderApp('/dashboard');

    await screen.findByRole('region', { name: 'AR01 Area ทดลอง' });
    expectBehindMoreInfo(/^รอบเป็นช่วงเวลา/, 'วิธีอ่านการ์ด');
  });
```

In `AttendancePage.test.tsx`, add the import `import { expectBehindMoreInfo, expectInView } from '../../test/moreInfo';`, and add inside the `describe`:

```ts
  it('the page notes wait behind ℹ; the summary tiles keep their lines (owner, 2026-10-27)', async () => {
    serve();
    await renderApp('/admin/attendance');

    await screen.findByText('15/10/2026 · กะเช้า 07:00–19:00');
    expectBehindMoreInfo(
      'หน้านี้ดูว่าแต่ละพื้นที่มีคนอยู่แล้วหรือยัง ไม่ใช่ระบบลงเวลาทำงานของบริษัท จึงไม่คิดสายหรือกลับก่อน',
      'เกี่ยวกับหน้านี้',
    );
    expectBehindMoreInfo(/^เรียงพื้นที่ที่ยังไม่มีคนขึ้นก่อน/, 'เกี่ยวกับหน้านี้');
    expectBehindMoreInfo(/^ลืมลงเวลาเลิกงาน/, 'เกี่ยวกับหน้านี้');
    expectInView('ไม่รวมพื้นที่ที่ปิดกะนี้');
  });
```

In `AlertsPage.test.tsx`, add the import `import { expectBehindMoreInfo } from '../../test/moreInfo';`, and add inside the `describe`:

```ts
  it('the page notes wait behind ℹ (owner, 2026-10-27)', async () => {
    serve();
    await renderApp('/admin/alerts');

    await screen.findAllByRole('article');
    expectBehindMoreInfo(
      'ไม่มีเตือนจากเวลาที่สแกน · ไม่คิดสายหรือกลับก่อน · ทุกการกดในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน',
      'เกี่ยวกับหน้านี้',
    );
    expectBehindMoreInfo('รายการคนทำแทนยังไม่แสดงการตรวจแทนตึกของหัวหน้า', 'เกี่ยวกับหน้านี้');
  });
```

- [ ] **Step 3: Run them to see them fail**

Run: `cd frontend && npx vitest run src/pages/dashboard/DashboardPage.test.tsx src/pages/attendance/AttendancePage.test.tsx src/pages/alerts/AlertsPage.test.tsx -t "ℹ"`
Expected: 3 FAIL. D1 and D2 fail on `expected null not to be null` (no `<details>`). D3 fails on "Unable to find an element with the text: รายการคนทำแทน…".

- [ ] **Step 4: Move the notes**

`DashboardPage.tsx`: add `import { MoreInfo } from '../../shared/components/MoreInfo';` and replace line 42:

```tsx
      <p className="text-muted">{RULES_NOTE}</p>
```
with
```tsx
      <MoreInfo summary="วิธีอ่านการ์ด">{RULES_NOTE}</MoreInfo>
```

`AttendancePage.tsx`: add the same import, and in `BoardNotes` replace the wrapper `<div className="flex flex-col gap-1 text-muted">` … `</div>` with `<MoreInfo summary="เกี่ยวกับหน้านี้">` … `</MoreInfo>`. Keep the three `<span>`s and their `Link`s exactly as they are. Change the doc comment to `/** Wireframe D2's notes under the table, shut until asked (owner, 2026-10-27). */`.

`AlertsPage.tsx`: add the import, and replace lines 151-153:

```tsx
        <p className="text-muted">
          ไม่มีเตือนจากเวลาที่สแกน · ไม่คิดสายหรือกลับก่อน · ทุกการกดในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน
        </p>
```
with
```tsx
        <MoreInfo summary="เกี่ยวกับหน้านี้">
          <span>ไม่มีเตือนจากเวลาที่สแกน · ไม่คิดสายหรือกลับก่อน · ทุกการกดในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน</span>
          <span>รายการคนทำแทนยังไม่แสดงการตรวจแทนตึกของหัวหน้า</span>
        </MoreInfo>
```

`CoversThisShift.tsx`: delete line 64, `<p className="text-muted">การตรวจแทนตึกของหัวหน้ายังไม่แสดงในหน้านี้</p>`.

- [ ] **Step 5: Run the three files**

Run: `cd frontend && npx vitest run src/pages/dashboard src/pages/attendance src/pages/alerts`
Expected: all PASS. If an older test now fails because a role query skips the closed `<details>` (for example, a count of `link`s on D2), check whether it should count links shut in ℹ. Adjust only that assertion and say so in the report.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/test/moreInfo.ts frontend/src/pages/dashboard frontend/src/pages/attendance frontend/src/pages/alerts
git commit -m "feat(frontend): D1-D3 explanations sit behind an info link (owner, 2026-10-27)"
```

### Task 2: The setup pages (D4, D10, ตึก, D5)

**Files:**
- Modify: `frontend/src/pages/areas/AreasPage.tsx:35-38`, `frontend/src/pages/areas/AreaFormPage.tsx:39`, `frontend/src/pages/areas/AreaEditorDrawer.tsx:53`
- Modify: `frontend/src/pages/buildings/BuildingsPage.tsx:44-47`, `frontend/src/pages/buildings/BuildingDrawer.tsx:131`
- Modify: `frontend/src/pages/print-signs/PrintSignsPage.tsx:117-119`
- Test: `frontend/src/pages/areas/AreasPage.test.tsx`, `frontend/src/pages/areas/AreaFormPage.test.tsx`, `frontend/src/pages/buildings/BuildingsPage.test.tsx`, `frontend/src/pages/print-signs/PrintSignsPage.test.tsx`

**Interfaces:**
- Consumes: `expectBehindMoreInfo`, `expectInView` from `frontend/src/test/moreInfo.ts` (Task 1).

- [ ] **Step 1: Write the failing tests**

In each of the four test files, add `import { expectBehindMoreInfo, expectInView } from '../../test/moreInfo';` (import only the names the file uses, or lint fails).

`AreasPage.test.tsx` (uses both):

```ts
  it('the page note and the save note wait behind ℹ; the sign help stays (owner, 2026-10-27)', async () => {
    serve();
    const { user } = await renderApp('/admin/areas/1');

    await screen.findByRole('heading', { level: 2, name: 'AR01 Area ทดลอง' });
    expectBehindMoreInfo(
      'แม่บ้านสแกนได้เฉพาะป้ายของพื้นที่ตัวเอง · ป้ายลงเวลา 1 ป้ายต่อพื้นที่ ระบบสร้างให้เองเมื่อเพิ่มพื้นที่ · ทุกการแก้ในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน',
      'เกี่ยวกับหน้านี้',
    );
    expectInView(/^เปิดหน้านี้บนมือถือ ยืนตรงที่ติดป้าย/);

    await user.click(screen.getByRole('button', { name: 'แก้ไขพื้นที่' }));
    const editor = await screen.findByRole('dialog', { name: 'แก้ไขพื้นที่ · AR01 Area ทดลอง' });
    await within(editor).findByLabelText('ชื่อพื้นที่');
    expectBehindMoreInfo('การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน', 'เกี่ยวกับการบันทึก');
  });
```

`AreaFormPage.test.tsx` (uses `expectBehindMoreInfo` only):

```ts
  it('the save note waits behind ℹ (owner, 2026-10-27)', async () => {
    serve();
    await renderApp('/admin/areas/1/edit');

    await screen.findByLabelText('ชื่อพื้นที่');
    expectBehindMoreInfo('การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน', 'เกี่ยวกับการบันทึก');
  });
```

`BuildingsPage.test.tsx` (uses `expectBehindMoreInfo` only; `openEditor` and `a` already exist in the file):

```ts
  it('the page note and the save note wait behind ℹ (owner, 2026-10-27)', async () => {
    serve();
    const { user } = await renderApp('/admin/buildings');

    await screen.findByRole('heading', { level: 2, name: 'ตึกทั้งหมด (3)' });
    expectBehindMoreInfo(/^ไม่มีปุ่มลบ ใช้ปิดใช้งานแทน เพื่อให้ประวัติยังอยู่/, 'เกี่ยวกับหน้านี้');

    await openEditor(user, a);
    expectBehindMoreInfo('การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน', 'เกี่ยวกับการบันทึก');
  });
```

`PrintSignsPage.test.tsx` (uses both):

```ts
  it('how the QR is made waits behind ℹ; the test scan after hanging stays (owner, 2026-10-27)', async () => {
    serve();
    await renderApp('/admin/print?area=1');

    await screen.findByRole('heading', { level: 1, name: 'พิมพ์ป้าย QR · AR01 Area ทดลอง' });
    expectBehindMoreInfo('ระบบสร้าง QR ในเครื่องนี้เอง ไม่ส่งรหัสออกไปเว็บภายนอก', 'เกี่ยวกับ QR');
    expectInView('ติดเสร็จให้สแกนทดสอบ 1 ครั้ง หน้าจอต้องขึ้นชื่อจุดตรงกับป้าย');
  });
```

- [ ] **Step 2: Run them to see them fail**

Run: `cd frontend && npx vitest run src/pages/areas src/pages/buildings src/pages/print-signs -t "ℹ"`
Expected: 4 FAIL. The first three fail on `expected null not to be null`. The print page fails on "Unable to find an element with the text: ระบบสร้าง QR…", because the line is still one sentence.

- [ ] **Step 3: Move the notes**

Add `import { MoreInfo } from '../../shared/components/MoreInfo';` to each page file you change.

`AreasPage.tsx`, replace lines 35-38:
```tsx
      <p className="text-muted">
        แม่บ้านสแกนได้เฉพาะป้ายของพื้นที่ตัวเอง · ป้ายลงเวลา 1 ป้ายต่อพื้นที่ ระบบสร้างให้เองเมื่อเพิ่มพื้นที่ ·
        ทุกการแก้ในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน
      </p>
```
with
```tsx
      <MoreInfo summary="เกี่ยวกับหน้านี้">
        <span>
          แม่บ้านสแกนได้เฉพาะป้ายของพื้นที่ตัวเอง · ป้ายลงเวลา 1 ป้ายต่อพื้นที่ ระบบสร้างให้เองเมื่อเพิ่มพื้นที่ ·
          ทุกการแก้ในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน
        </span>
      </MoreInfo>
```

`AreaFormPage.tsx:39`, `AreaEditorDrawer.tsx:53` and `BuildingDrawer.tsx:131`: in each, replace
```tsx
<p className="text-muted">การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน</p>
```
with
```tsx
<MoreInfo summary="เกี่ยวกับการบันทึก">การบันทึกทุกครั้งเก็บลงประวัติการแก้ไขของแอดมิน</MoreInfo>
```
Keep each file's indentation.

`BuildingsPage.tsx`, replace lines 44-47:
```tsx
      <p className="text-muted">
        ไม่มีปุ่มลบ ใช้ปิดใช้งานแทน เพื่อให้ประวัติยังอยู่ · ปิดตึกได้เมื่อไม่มีพื้นที่ที่เปิดอยู่และไม่มีหัวหน้าที่เปิดใช้งานประจำตึก ·
        ทุกการแก้ในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน
      </p>
```
with
```tsx
      <MoreInfo summary="เกี่ยวกับหน้านี้">
        <span>
          ไม่มีปุ่มลบ ใช้ปิดใช้งานแทน เพื่อให้ประวัติยังอยู่ · ปิดตึกได้เมื่อไม่มีพื้นที่ที่เปิดอยู่และไม่มีหัวหน้าที่เปิดใช้งานประจำตึก ·
          ทุกการแก้ในหน้านี้เก็บลงประวัติการแก้ไขของแอดมิน
        </span>
      </MoreInfo>
```

`PrintSignsPage.tsx`, replace lines 117-119:
```tsx
      <p className="text-muted print:hidden">
        ระบบสร้าง QR ในเครื่องนี้เอง ไม่ส่งรหัสออกไปเว็บภายนอก · ติดเสร็จให้สแกนทดสอบ 1 ครั้ง หน้าจอต้องขึ้นชื่อจุดตรงกับป้าย
      </p>
```
with
```tsx
      <div className="flex flex-col gap-1.5 print:hidden">
        <p className="text-muted">ติดเสร็จให้สแกนทดสอบ 1 ครั้ง หน้าจอต้องขึ้นชื่อจุดตรงกับป้าย</p>
        <MoreInfo summary="เกี่ยวกับ QR">ระบบสร้าง QR ในเครื่องนี้เอง ไม่ส่งรหัสออกไปเว็บภายนอก</MoreInfo>
      </div>
```
(`print:hidden` keeps both off the paper, as before.)

- [ ] **Step 4: Run the files**

Run: `cd frontend && npx vitest run src/pages/areas src/pages/buildings src/pages/print-signs`
Expected: all PASS (same caveat as Task 1, Step 5).

- [ ] **Step 5: Commit**

```bash
git add frontend/src/pages/areas frontend/src/pages/buildings frontend/src/pages/print-signs
git commit -m "feat(frontend): D4, D5, D10 and building explanations sit behind an info link (owner, 2026-10-27)"
```

### Task 3: Phone size, M17 and the Cleaner's phone pages

**Files:**
- Modify: `frontend/src/shared/components/MoreInfo.tsx`
- Modify: `frontend/src/pages/capture/CapturePage.tsx:83`, `frontend/src/pages/check-in/CheckInPage.tsx:138-140`, `frontend/src/pages/scan/SubmitForm.tsx:106-108`
- Modify: `design-system/facility/MASTER.md:127`
- Test: `frontend/src/pages/capture/CapturePage.test.tsx`, `frontend/src/pages/check-in/CheckInPage.test.tsx`, `frontend/src/pages/scan/ScanPage.test.tsx`

**Interfaces:**
- Consumes: `expectBehindMoreInfo` (Task 1).
- Produces: `MoreInfo({ summary, size = 'desk', children }: { summary: string; size?: 'desk' | 'phone'; children: ReactNode })`.

- [ ] **Step 1: Write the failing tests**

Add `import { expectBehindMoreInfo } from '../../test/moreInfo';` to the three test files.

`CapturePage.test.tsx` (`serve` and `openAtSign` exist in the file):

```ts
  it('the page note waits behind a phone-size ℹ (owner, 2026-10-27)', async () => {
    serve();
    await openAtSign('/admin/areas/1/capture?sign=102', 'AR01-02 ห้องน้ำหญิง');

    expectBehindMoreInfo('ต้องเปิดผ่าน HTTPS · การบันทึกเก็บลงประวัติการแก้ไขของแอดมิน', 'เกี่ยวกับหน้านี้');
    expect(screen.getByText('เกี่ยวกับหน้านี้').closest('summary')).toHaveClass('min-h-12');
  });
```

`CheckInPage.test.tsx`:

```ts
  it('how the buttons work waits behind a phone-size ℹ (owner, 2026-10-27)', async () => {
    serve({});
    await renderApp(SIGN);

    await screen.findByRole('button', { name: 'เข้างาน' });
    expectBehindMoreInfo('ระบบแสดงปุ่มถัดไปให้เองตามลำดับ · กดซ้ำภายใน 5 นาทีไม่นับ · ไม่คิดสายหรือกลับก่อน', 'เกี่ยวกับการลงเวลา');
    expect(screen.getByText('เกี่ยวกับการลงเวลา').closest('summary')).toHaveClass('min-h-12');
  });
```

`ScanPage.test.tsx` (`fakeGps` and `nearSign` are already imported there):

```ts
  it('what happens with no signal waits behind a phone-size ℹ (owner, 2026-10-27)', async () => {
    fakeGps(nearSign);
    serve();
    await renderApp(SIGN);

    await screen.findByRole('heading', { name: 'ห้องน้ำชาย' });
    expectBehindMoreInfo(
      'ถ้าไม่มีสัญญาณ หน้าจอจะขึ้น "ยังไม่ได้บันทึก" พร้อมปุ่มส่งใหม่ เวลาที่บันทึกคือตอนที่ส่งสำเร็จ',
      'เกี่ยวกับการส่งงาน',
    );
    expect(screen.getByText('เกี่ยวกับการส่งงาน').closest('summary')).toHaveClass('min-h-12');
  });
```

- [ ] **Step 2: Run them to see them fail**

Run: `cd frontend && npx vitest run src/pages/capture src/pages/check-in src/pages/scan -t "ℹ"`
Expected: 3 FAIL on `expected null not to be null`.

- [ ] **Step 3: Add the phone size to `MoreInfo`**

Replace the body of `frontend/src/shared/components/MoreInfo.tsx` after the imports with:

```tsx
/** desk: the Admin pages, 36 px; phone: 48 px and the phone's icon and text sizes (MASTER.md). */
const sizeClass = {
  desk: { summary: 'min-h-9', icon: 'size-4', body: '' },
  phone: { summary: 'min-h-12 text-md', icon: 'size-5', body: 'text-md' },
} as const;

/**
 * Notes about how a page counts or keeps things, shut until asked (owner, 2026-10-26: a page should not show the
 * wireframe's explanations all the time; 2026-10-27: on every page, the phone ones too). A warning about what the user
 * is about to do, and a field's help, stay in the page; this is for the rest. The native <details>: it opens with the
 * keyboard and is read out as expandable.
 */
export function MoreInfo({
  summary,
  size = 'desk',
  children,
}: {
  summary: string;
  size?: 'desk' | 'phone';
  children: ReactNode;
}) {
  const classes = sizeClass[size];
  return (
    <details className="group max-w-[80ch]">
      <summary
        className={`inline-flex ${classes.summary} cursor-pointer list-none items-center gap-1.5 rounded text-primary hover:text-primary-strong ${focusRing}`}
      >
        <Info aria-hidden="true" className={classes.icon} />
        <span className="underline">{summary}</span>
      </summary>
      <div className={`flex flex-col gap-1.5 pb-1 text-muted ${classes.body}`}>{children}</div>
    </details>
  );
}
```

D6–D9 keep the default `desk` size, so they render the same classes as before (the empty `body` adds one trailing space only).

- [ ] **Step 4: Move the notes**

Add `import { MoreInfo } from '../../shared/components/MoreInfo';` to the three page files.

`CapturePage.tsx:83`, replace
```tsx
      <p className="text-md text-muted">ต้องเปิดผ่าน HTTPS · การบันทึกเก็บลงประวัติการแก้ไขของแอดมิน</p>
```
with
```tsx
      <MoreInfo size="phone" summary="เกี่ยวกับหน้านี้">
        ต้องเปิดผ่าน HTTPS · การบันทึกเก็บลงประวัติการแก้ไขของแอดมิน
      </MoreInfo>
```

`CheckInPage.tsx:138-140`, replace
```tsx
      <p className="text-center text-md text-muted">
        ระบบแสดงปุ่มถัดไปให้เองตามลำดับ · กดซ้ำภายใน 5 นาทีไม่นับ · ไม่คิดสายหรือกลับก่อน
      </p>
```
with
```tsx
      <MoreInfo size="phone" summary="เกี่ยวกับการลงเวลา">
        ระบบแสดงปุ่มถัดไปให้เองตามลำดับ · กดซ้ำภายใน 5 นาทีไม่นับ · ไม่คิดสายหรือกลับก่อน
      </MoreInfo>
```

`SubmitForm.tsx:106-108`, replace
```tsx
      <p className="text-center text-md text-muted">
        ถ้าไม่มีสัญญาณ หน้าจอจะขึ้น "ยังไม่ได้บันทึก" พร้อมปุ่มส่งใหม่ เวลาที่บันทึกคือตอนที่ส่งสำเร็จ
      </p>
```
with
```tsx
      <MoreInfo size="phone" summary="เกี่ยวกับการส่งงาน">
        ถ้าไม่มีสัญญาณ หน้าจอจะขึ้น "ยังไม่ได้บันทึก" พร้อมปุ่มส่งใหม่ เวลาที่บันทึกคือตอนที่ส่งสำเร็จ
      </MoreInfo>
```

- [ ] **Step 5: Update MASTER.md**

In `design-system/facility/MASTER.md`, replace the `MoreInfo` row (line 127) with:

```markdown
| `MoreInfo` (`summary`, `size` desk / phone) | Notes on how a page counts or keeps things, shut until asked (owner, 2026-10-26; every page, phone ones too, 2026-10-27). A warning about what the user is about to do and a field's help stay in the page. `phone`: 48 px summary, `size-5` icon, `text-md` |
```

- [ ] **Step 6: Run everything**

Run: `cd frontend && npx vitest run && npx tsc -b && npx eslint .`
Expected: all tests PASS (the count before this branch plus 10 new ones), tsc and eslint clean.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/shared/components/MoreInfo.tsx frontend/src/pages/capture frontend/src/pages/check-in frontend/src/pages/scan design-system/facility/MASTER.md
git commit -m "feat(frontend): phone-size info link; M17, check-in and scan notes sit behind it (owner, 2026-10-27)"
```

### Task 4: Handoff and the owner's test build

**Files:**
- Modify: `docs/handoff.md`

- [ ] **Step 1: Build for the API**

Run: `cd frontend && npm run build:api`
Expected: the build succeeds. Note the startup JS gzip size it prints, to compare with master's 162.99 kB. The change should be near zero.

- [ ] **Step 2: Update the handoff**

In `docs/handoff.md`:
- **Last updated** line: say the branch `feat/more-info-rest` is done, not pushed, and the owner's test on their own machine is owed.
- The #80 "Explanations always on screen" bullet: replace "**Owner, 2026-10-27: move them behind `MoreInfo` too, by the same rule.**" with "Done on branch `feat/more-info-rest` (plan `docs/superpowers/plans/2026-10-27-explanations-behind-more-info.md`; the plan's table lists what moved and what stayed)."
- In `## Pending work`, item (1) of the order-of-work bullet: mark it done on the branch, owner's test owed.

Then commit only that file:

```bash
git add docs/handoff.md
git commit -m "docs: handoff — explanations behind ℹ done on feat/more-info-rest; owner's test owed"
```

Caution: `docs/handoff.md` already holds uncommitted lines from before this branch (facility-0092 and order of work, 2026-10-27). They go into this commit too. Tell the owner this in the report.

- [ ] **Step 3: Bring up the owner's test build (the controller, not a subagent)**

The controller runs this. MySQL steps never go to a subagent.

1. Create the throwaway database `facility_moreinfo_test`. On this Mac, MySQL root has no password.
2. Start the API on it as in `## Sharing a test build`, step 3: Development, port 5001, `--no-launch-profile`, connection string set in the shell. The seed gives admin / admin1234, Supervisor S2001, and building A with AR01 and AR02.
3. The owner runs `cloudflared tunnel --url http://localhost:5001` in their own terminal if they want to use a phone.

What the owner checks (desktop 1280 px, and a phone for M1 / M4 / M17):
- D1, D2, D3, D4, D5, D10, ตึก: each shows the ℹ link named in the plan's table, closed. Click it: the old text appears. The kept lines (tile lines, cover form, sign help, "ติดเสร็จให้สแกนทดสอบ…") still show.
- Check-in sign page, a point's scan page, M17 on a phone: the ℹ link is large enough to tap and opens.

- [ ] **Step 4: After the owner's check**

Stop the API, drop `facility_moreinfo_test` and delete `wwwroot`. Ask the owner by popup whether to push, open the PR and merge. Update the handoff.
