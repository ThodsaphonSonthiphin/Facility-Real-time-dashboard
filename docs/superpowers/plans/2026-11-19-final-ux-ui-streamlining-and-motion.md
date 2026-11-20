# Final UX/UI Streamlining & Motion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Streamline Admin, Supervisor, and Cleaner navigation and screens to eliminate duplicated legacy UI controls, consolidate emergency cover assignment under Schedules, and add a subtle, zero-CLS CSS motion system.

**Architecture:** Frontend-only consolidation on `codex/cleaner-scheduling`. Every screen owns one clear business purpose (`facility-0224` through `facility-0228`), dead components are deleted in place (no backward compatibility), and motion uses pure CSS `transform`/`opacity` keyframes in `frontend/src/index.css` honoring `prefers-reduced-motion` (`facility-0229`).

**Tech Stack:** React 19, TypeScript, React Router, RTK Query, Tailwind CSS v4 (`@theme` tokens), `lucide-react`, Vitest, Playwright.

**Spec:** [docs/superpowers/specs/2026-11-19-final-ux-ui-streamlining-and-motion-design.md](../specs/2026-11-19-final-ux-ui-streamlining-and-motion-design.md)

## Global Constraints

- Follow `react-structure` skill: separate hooks and UI for new/refactored components (`SupervisorNav/SupervisorNav.tsx`).
- Follow `AGENTS.md` UI Iconography Standards: `lucide-react` only (one named import per icon, stroke width 2, `aria-hidden="true"`), zero emojis or Unicode pictograms in UI.
- Follow `design-system/janitorial/MASTER.md` and `docs/agents/frontend-standards.md`: Enterprise Light tokens only, phone targets `>= 48 px`, Admin targets `>= 36 px`, zero horizontal overflow at `320 px` / `375 px` / `1280 px`, main JS bundle `<= 200 kB gzip`.
- Zero CLS (`layoutShiftTotal === 0`) on monitored pages: animate only `transform`, `opacity`, `background-color`, and `border-color`, and wrap with `@media (prefers-reduced-motion: reduce)`.
- Print sheet (`PrintSignsPage` / `SignCard` / `@page`) must remain untouched and static.
- Always run Vitest and commits with `NODE_ENV=test` (shell exports `NODE_ENV=production`).
- No backend API, schema, auth, or business-rule changes.

---

### Task 1: Subtle & Snappy Motion System (`facility-0229`)

**Files:**
- Modify: `frontend/src/index.css`
- Modify: `frontend/src/shared/components/Drawer.tsx`
- Modify: `frontend/src/shared/components/Modal.tsx`
- Modify: `frontend/src/shared/components/Toast.tsx`
- Modify: `frontend/src/shared/components/Button.tsx`
- Test: `frontend/src/shared/components/Motion.test.tsx` (Create)

**Interfaces:**
- Produces CSS utility classes in `frontend/src/index.css`: `.motion-drawer`, `.motion-backdrop`, `.motion-modal`, `.motion-toast`, `.motion-view`.
- `Button` retains its exact props and adds `transition-colors duration-150 active:scale-[0.98]`.

- [ ] **Step 1: Write the failing test**

Create `frontend/src/shared/components/Motion.test.tsx`:
```tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { Button } from './Button';
import { Drawer } from './Drawer';
import { Modal } from './Modal';
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

    const { container: toastContainer } = render(
      <Toast notice={{ kind: 'ok', text: 'บันทึกแล้ว' }} onClose={vi.fn()} />,
    );
    expect(toastContainer.querySelector('.motion-toast')).not.toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `NODE_ENV=test npm --prefix frontend test -- src/shared/components/Motion.test.tsx`
Expected: FAIL (missing `active:scale-[0.98]` and `.motion-*` classes).

- [ ] **Step 3: Implement CSS keyframes and component classes**

In `frontend/src/index.css`, add pure CSS keyframes (`150–200ms`, `transform`/`opacity` only) and `@media (prefers-reduced-motion: reduce)` override:
```css
@keyframes motion-fade-in {
  from { opacity: 0; }
  to { opacity: 1; }
}
@keyframes motion-drawer-in {
  from { opacity: 0; transform: translateX(20px); }
  to { opacity: 1; transform: translateX(0); }
}
@keyframes motion-modal-in {
  from { opacity: 0; transform: scale(0.98); }
  to { opacity: 1; transform: scale(1); }
}
@keyframes motion-toast-in {
  from { opacity: 0; transform: translateY(6px); }
  to { opacity: 1; transform: translateY(0); }
}
.motion-backdrop { animation: motion-fade-in 150ms ease-out; }
.motion-drawer { animation: motion-drawer-in 200ms cubic-bezier(0.16, 1, 0.3, 1); }
.motion-modal { animation: motion-modal-in 180ms cubic-bezier(0.16, 1, 0.3, 1); }
.motion-toast { animation: motion-toast-in 180ms cubic-bezier(0.16, 1, 0.3, 1); }
.motion-view { animation: motion-fade-in 150ms ease-out; }

@media (prefers-reduced-motion: reduce) {
  .motion-backdrop, .motion-drawer, .motion-modal, .motion-toast, .motion-view {
    animation-duration: 0.01ms !important;
  }
}
```
Add `.motion-backdrop` / `.motion-drawer` to `Drawer.tsx`, `.motion-backdrop` / `.motion-modal` to `Modal.tsx`, `.motion-toast` to `Toast.tsx`, and `transition-transform duration-150 active:scale-[0.98]` to `buttonClass` in `Button.tsx`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `NODE_ENV=test npm --prefix frontend test -- src/shared/components/Motion.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/index.css frontend/src/shared/components/Drawer.tsx frontend/src/shared/components/Modal.tsx frontend/src/shared/components/Toast.tsx frontend/src/shared/components/Button.tsx frontend/src/shared/components/Motion.test.tsx
NODE_ENV=test git commit -m "feat(ui): add subtle and snappy CSS motion system (facility-0229) (#1)"
```

---

### Task 2: Admin Overview Home & Slim Live Point Board (`facility-0224`)

**Files:**
- Modify: `frontend/src/shared/auth/roles.ts`
- Modify: `frontend/src/pages/dashboard/LegacyDashboardPage/LegacyDashboardPage.tsx`
- Delete: `frontend/src/pages/dashboard/BuildingSummary.tsx`
- Modify: `frontend/src/pages/dashboard/DashboardPage.test.tsx`
- Modify: `frontend/src/router.test.tsx` (if asserting Admin home route)
- Modify: `frontend/e2e/desktop/login.spec.ts` and `frontend/e2e/desktop/dashboard.spec.ts` (update Admin landing expectation to `/admin/overview` where appropriate; keep `/dashboard` explicit `page.goto('/dashboard')` checks intact)

**Interfaces:**
- `homeOf('admin')` returns `'/admin/overview'`.
- `LegacyDashboardPage` renders live point cards, filters, and `MoreInfo` without `<BuildingSummary />`.

- [ ] **Step 1: Write/update failing tests**

Update `frontend/src/pages/dashboard/DashboardPage.test.tsx` to assert `BuildingSummary` is no longer rendered on `/dashboard`, and add a test asserting `homeOf('admin') === '/admin/overview'`.

- [ ] **Step 2: Run tests to verify failure**

Run: `NODE_ENV=test npm --prefix frontend test -- src/pages/dashboard/DashboardPage.test.tsx src/router.test.tsx`
Expected: FAIL.

- [ ] **Step 3: Implement minimal changes**

In `frontend/src/shared/auth/roles.ts`:
```ts
    case 'admin':
      return '/admin/overview';
```
In `frontend/src/pages/dashboard/LegacyDashboardPage/LegacyDashboardPage.tsx`, remove `import { BuildingSummary } from '../BuildingSummary';` and `{data && data.length > 0 && <BuildingSummary points={data} />}`. Delete `frontend/src/pages/dashboard/BuildingSummary.tsx`. Update `e2e/desktop/login.spec.ts` / `dashboard.spec.ts` for `/admin/overview` landing.

- [ ] **Step 4: Run tests to verify pass**

Run: `NODE_ENV=test npm --prefix frontend test -- src/pages/dashboard/DashboardPage.test.tsx src/router.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A frontend/src/shared/auth/roles.ts frontend/src/pages/dashboard/ frontend/src/router.test.tsx frontend/e2e/desktop/
NODE_ENV=test git commit -m "feat(admin): make overview the Admin home and slim live point board (facility-0224) (#1)"
```

---

### Task 3: Single-Purpose Operations Screens — Alerts, Attendance & Schedules Cover Consolidation (`facility-0225`)

**Files:**
- Modify: `frontend/src/pages/alerts/AlertsPage.tsx`
- Delete: `frontend/src/pages/alerts/EmptyAreas.tsx`
- Delete: `frontend/src/pages/alerts/CoversThisShift.tsx`
- Move: `frontend/src/pages/attendance/CoverDrawer.tsx` -> `frontend/src/pages/schedules/CoverDrawer.tsx`
- Modify: `frontend/src/pages/attendance/LegacyAttendancePage/LegacyAttendancePage.tsx`
- Modify: `frontend/src/pages/attendance/LegacyAttendancePage/useLegacyAttendancePage.ts`
- Modify: `frontend/src/pages/attendance/AttendanceTable.tsx`
- Modify: `frontend/src/pages/attendance/OverviewAttendanceActions/OverviewAttendanceActions.tsx`
- Modify: `frontend/src/pages/attendance/OverviewAttendanceActions/useOverviewAttendanceActions.ts`
- Modify: `frontend/src/pages/schedules/SchedulePage/SchedulePage.tsx` (add `มอบหมายทำแทนฉุกเฉิน` button opening `CoverDrawer` for Admin)
- Modify: `frontend/src/pages/alerts/AlertsPage.test.tsx`, `frontend/src/pages/alerts/AlertCovers.test.tsx` (re-home cover tests to `frontend/src/pages/schedules/ScheduleCover.test.tsx`), `frontend/src/pages/attendance/AttendancePage.test.tsx`, `frontend/src/pages/overview/OverviewPage.test.tsx`, `frontend/e2e/desktop/attendance.spec.ts`

**Interfaces:**
- `AlertsPage` renders only the exception review queue (no `EmptyAreas`, no `CoversThisShift`).
- `LegacyAttendancePage`, `AttendanceTable`, and `OverviewAttendanceActions` render no `มอบหมายทำแทน` button or `CoverDrawer`; empty rows render `<Link to="/admin/schedules">ไปจัดคนแทนที่ตารางงาน</Link>`.
- `SchedulePage` (Admin mode) exposes `มอบหมายทำแทนฉุกเฉิน` opening `CoverDrawer` with current slot defaults.

- [ ] **Step 1: Write/update failing tests**

Update `AlertsPage.test.tsx` (assert no `EmptyAreas` / `CoversThisShift`), `AttendancePage.test.tsx` & `OverviewPage.test.tsx` (assert no cover button, assert link to `/admin/schedules` on empty row), and add `ScheduleCover.test.tsx` (assert `มอบหมายทำแทนฉุกเฉิน` opens `CoverDrawer` in `/admin/schedules`).

- [ ] **Step 2: Run tests to verify failure**

Run: `NODE_ENV=test npm --prefix frontend test -- src/pages/alerts/ src/pages/attendance/ src/pages/schedules/ScheduleCover.test.tsx`
Expected: FAIL.

- [ ] **Step 3: Implement consolidation**

1. Clean `AlertsPage.tsx` to exception queue only and delete `EmptyAreas.tsx` and `CoversThisShift.tsx`.
2. Remove `cover` state, header cover button, row `onCover`, and `<CoverDrawer />` from `LegacyAttendancePage`, `useLegacyAttendancePage`, `AttendanceTable`, `OverviewAttendanceActions`, and `useOverviewAttendanceActions`. Add `/admin/schedules` link on empty rows.
3. Move `CoverDrawer.tsx` to `frontend/src/pages/schedules/CoverDrawer.tsx` and wire the `มอบหมายทำแทนฉุกเฉิน` button in `SchedulePage.tsx` (Admin view) using the active shift slot.
4. Update `frontend/e2e/desktop/attendance.spec.ts` so the cover assignment E2E opens from `/admin/schedules` (or verifies the `/admin/schedules` link from `/admin/attendance`).

- [ ] **Step 4: Run tests to verify pass**

Run: `NODE_ENV=test npm --prefix frontend test -- src/pages/alerts/ src/pages/attendance/ src/pages/schedules/ src/pages/overview/`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A frontend/src/pages/alerts/ frontend/src/pages/attendance/ frontend/src/pages/schedules/ frontend/src/pages/overview/ frontend/e2e/desktop/attendance.spec.ts
NODE_ENV=test git commit -m "feat(admin): consolidate cover assignment under Schedules and simplify Alerts/Attendance (facility-0225) (#1)"
```

---

### Task 4: Cleaner 2-Tab Bottom Nav & Supervisor 2-Page Switcher (`facility-0227`, `facility-0228`)

**Files:**
- Modify: `frontend/src/shared/components/CleanerBottomNav/CleanerBottomNav.tsx`
- Modify: `frontend/src/shared/components/CleanerBottomNav/useCleanerBottomNav.ts`
- Modify: `frontend/src/shared/components/CleanerBottomNav/CleanerBottomNav.test.tsx`
- Create: `frontend/src/shared/components/SupervisorNav/SupervisorNav.tsx`
- Create: `frontend/src/shared/components/SupervisorNav/SupervisorNav.test.tsx`
- Modify: `frontend/src/pages/my-building/MyBuildingPage.tsx`
- Modify: `frontend/src/pages/schedules/SchedulePage/SchedulePage.tsx`
- Modify: `frontend/src/pages/my-building/MyBuildingPage.test.tsx`
- Modify: `frontend/src/pages/schedules/SchedulePage/SchedulePage.test.tsx`

**Interfaces:**
- `CleanerBottomNav` renders exactly 2 `<NavLink>` tabs (`งานของฉัน` -> `/my-work`, `ตารางของฉัน` -> `/my-schedule`) in a 2-column grid with `min-h-12` targets.
- `SupervisorNav()` renders a `<nav aria-label="เมนูหัวหน้างาน">` with 2 `<NavLink>` controls (`ตึกของฉัน` -> `/my-building`, `จัดตารางงาน` -> `/schedule-management`) with `aria-current="page"`.

- [ ] **Step 1: Write/update failing tests**

Update `CleanerBottomNav.test.tsx` to assert exactly 2 tabs (`งานของฉัน`, `ตารางของฉัน`) and absence of `สแกน QR` and `บัญชี`. Create `SupervisorNav.test.tsx` asserting both links, active `aria-current="page"`, and rendering on both `/my-building` and `/schedule-management`.

- [ ] **Step 2: Run tests to verify failure**

Run: `NODE_ENV=test npm --prefix frontend test -- src/shared/components/CleanerBottomNav/ src/shared/components/SupervisorNav/ src/pages/my-building/MyBuildingPage.test.tsx`
Expected: FAIL.

- [ ] **Step 3: Implement simplified navs**

1. Reduce `CleanerBottomNav.tsx` and `useCleanerBottomNav.ts` to the 2 real `<NavLink>` tabs (`grid-cols-2`), deleting QR guidance popover and duplicate account tab state.
2. Create `SupervisorNav.tsx` using `lucide-react` (`Building2`, `CalendarDays`, `aria-hidden="true"`) and Enterprise Light active styling (`bg-accent-subtle text-accent font-bold`).
3. Render `<SupervisorNav />` in `MyBuildingPage.tsx` and Supervisor mode of `SchedulePage.tsx`, replacing the ad-hoc one-way link/back-link pair.

- [ ] **Step 4: Run tests to verify pass**

Run: `NODE_ENV=test npm --prefix frontend test -- src/shared/components/CleanerBottomNav/ src/shared/components/SupervisorNav/ src/pages/my-building/ src/pages/schedules/`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/shared/components/CleanerBottomNav/ frontend/src/shared/components/SupervisorNav/ frontend/src/pages/my-building/ frontend/src/pages/schedules/
NODE_ENV=test git commit -m "feat(nav): simplify Cleaner nav to 2 tabs and add consistent Supervisor switcher (facility-0227, 0228) (#1)"
```

---

### Task 5: Integrated Verification, E2E Acceptance & Docs Update

**Files:**
- Modify: `docs/handoff.md`
- Modify: `docs/code-map.md`
- Modify: `docs/adr/README.md` (if present, register `facility-0224`..`facility-0229`)

- [ ] **Step 1: Run full frontend checks and build**

Run:
```bash
NODE_ENV=test npm --prefix frontend test
npm --prefix frontend run lint
npm --prefix frontend run build
```
Expected: All unit/integration tests pass, 0 lint errors, main JS gzip `<= 200 kB`.

- [ ] **Step 2: Run full Chromium E2E verification**

Run:
```bash
E2E_HEADLESS=1 E2E_SLOW_MO=0 npm --prefix frontend run e2e -- --dataset medium --project=desktop
E2E_HEADLESS=1 E2E_SLOW_MO=0 E2E_PORT=5102 npm --prefix frontend run e2e -- --dataset large
```
Expected: 0 failures across both profiles, zero CLS, zero horizontal overflow.

- [ ] **Step 3: Update docs and commit**

Update `docs/handoff.md`, `docs/code-map.md`, and `docs/adr/README.md` with verified counts, gzip sizes, and E2E evidence paths.

```bash
git add docs/handoff.md docs/code-map.md docs/adr/
NODE_ENV=test git commit -m "docs: record final UX/UI streamlining and motion verification (#1)"
```

---

## Plan Self-Review Checklist

- **Spec coverage:**
  - `facility-0224` (Overview home + slim `/dashboard`) -> Task 2
  - `facility-0225` (Cover consolidated in Schedules, Alerts exception-only, Attendance + Overview drill-through time-only) -> Task 3
  - `facility-0226` (5 setup + 2 report menus distinct) -> preserved across Tasks 2–3
  - `facility-0227` (Cleaner 2-tab bottom nav) -> Task 4
  - `facility-0228` (Supervisor 2-page switcher) -> Task 4
  - `facility-0229` (Subtle & Snappy CSS motion, zero CLS, reduced-motion) -> Task 1
  - Verification & Docs -> Task 5
- **Placeholder scan:** No TBD/TODO placeholders; every task lists exact files, interfaces, tests, commands, and commit steps.
- **Type consistency:** `homeOf('admin')`, `SupervisorNav`, `CoverDrawer`, and `.motion-*` classes match across all tasks.
