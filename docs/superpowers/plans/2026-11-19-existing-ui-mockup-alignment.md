# Existing UI Mockup Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Align the existing Admin, Supervisor, and Cleaner screens with the approved UX/UI mockup.

**Architecture:** Start with Dashboard to establish the visual adjustments against the running app. Keep layout at the page that owns it, reuse the existing shared components, and keep the current router, API, and state contracts.

**Tech Stack:** React 19, React Router, Redux Toolkit, Tailwind 4, `lucide-react`, Vitest, Playwright.

**Spec:** [Approved design](../specs/2026-11-19-existing-ui-mockup-alignment-design.md).

## Global Constraints

- Compare individual scenes in [the mockup](../../design/ux-audit/mockups.html). Use the light sidebar and preserve [R-01 through R-05](2026-11-19-ux-ui-redesign-implementation.md#approved-final-decisions-r-01-through-r-05). The earlier plan's route matrix is a lookup for existing controls.
- Use actual app state and preserve API contracts, live updates, feature flag defaults, role boundaries, attendance lifecycle, QR/GPS requirements, and printed signs. The historical chart stays in the Combined Overview plan. Keep search on its current pages.
- Use [frontend standards](../../agents/frontend-standards.md) and [design system](../../../design-system/janitorial/MASTER.md) tokens. Admin minimum targets are 36 px, phone targets 48 px, phone primary actions 56 px. Use Lucide with Thai labels, named scroll regions, and existing focus treatment.
- Compare at 320 and 375 px on phones and 1024 and 1280 px on desktops as applicable. Keep synthetic evidence under ignored `.run/`, with a before and after image for each represented scene and viewport and a short note on intentional differences. Compare equivalent UI states and the same viewport. Use the [owned E2E runner](../../testing/e2e.md) for app workflows that write data.

---

### Task 1: Admin Dashboard and light shell

**Files:** `frontend/src/shared/components/AppLayout.tsx`, `frontend/src/shared/components/AdminNav.tsx`, `frontend/src/pages/dashboard/DashboardPage.tsx`, `frontend/src/pages/dashboard/Filters.tsx`, `frontend/src/pages/dashboard/BuildingSummary.tsx`.

**Interfaces:** `Filters({ points })` and `BuildingSummary({ points })` continue to consume live `PointCard[]`. Keep `AppLayout({ wide })`, `AdminNav`'s mobile disclosure and footer props, and selected nav links via `aria-current`.

- [ ] Capture the Dashboard preview (`renderDashboardContent`), existing Dashboard, and the expanded mobile menu. Note heading and section spacing, populated status grid, filter wrapping and account placement. Record the six status counts and a building's percentage as a baseline for behavior, then capture its pressed state.
- [ ] Run the existing Dashboard and menu cases as a baseline.

  ```sh
  npm --prefix frontend test -- src/pages/dashboard/DashboardPage.test.tsx src/shared/components/AdminNav.test.tsx
  ```

- [ ] Align the light sidebar's logo area, groups, active row and footer spacing while preserving the 48 px alert slot, menu disclosure, logout, and skip link. Align the Dashboard heading and live badge on one row at widths where they fit, letting them wrap at smaller widths. Use the existing shell width and gutters.
- [ ] Align the card grid, toolbar and building panel. Use the following pattern on the existing card list if the baseline comparison shows the 140 px minimum leaves a single wide card per row at 320 px. Keep the real counts, status icons, and native pressed buttons. Give pressed and resting cards the same outer dimensions.

  ```tsx
  <ul className="m-0 grid list-none grid-cols-2 gap-3 p-0 md:grid-cols-[repeat(auto-fit,minmax(140px,1fr))]" aria-label="สถานะ">
  ```

  Preserve the native list interior and restore an extra column only where the longest Thai status label fits. Align building labels, counts and progress bars within `BuildingSummary`. Keep the existing percentage calculation and building filter action.

- [ ] Re-run the same Vitest cases. Capture default and filtered Dashboard states at each size, plus empty/loading/error where applicable. Confirm clearing filters, the building action, live badge, menu keyboard behavior, and unchanged horizontal page width. Review this surface before applying shared visual changes to other pages.

### Task 2: Attendance and both roster entry points

**Files:** `frontend/src/pages/attendance/AttendancePage.tsx`, `frontend/src/pages/attendance/BoardControls.tsx`, `frontend/src/pages/attendance/CoverDrawer.tsx`, `frontend/src/pages/schedules/SchedulePage/SchedulePage.tsx`, `frontend/src/pages/schedules/SchedulePage/ScheduleContextBar.tsx`, `frontend/src/pages/schedules/SchedulePage/ScheduleSummaryCards.tsx`.

**Interfaces:** Reuse the current `KpiCard` and existing `FilterToolbar` from `SurfacePanel.tsx`. Admin and Supervisor use their existing entry points into `SchedulePage`.

- [ ] Compare `renderAttendanceContent` and `renderScheduleContent` to loaded pages. Capture legacy and numbered attendance columns, a correction/cover overlay, roster selection and a review overlay. Use the `large` runner profile for scheduling and capture normal unavailable behavior separately with the default profile as needed.
- [ ] Match title/card/toolbar rhythm and table header styling by applying spacing to the surrounding JSX. Keep the existing five attendance cards as list items, four schedule cards, week and building/shift controls, numeric columns, sticky names, and existing scroll wrapper. Adjust drawer body spacing inside the existing dialog. Preserve `ScheduleContextBar` as the action region for the selected cell.
- [ ] Run `npm --prefix frontend test -- src/pages/attendance/AttendancePage.test.tsx src/pages/schedules/SchedulePage/SchedulePage.test.tsx`. Capture at the agreed widths with populated data and a selected row. Check tabbing exposes focused cells, the roster still opens the same editor and review, both attendance actions are reachable, and closing each dialog returns focus.

### Task 3: Remaining Admin surfaces and shared treatments

**Files:** `frontend/src/pages/areas/AreasPage.tsx`, `frontend/src/pages/areas/AreaFormPage.tsx`, `frontend/src/pages/areas/AreaEditorDrawer.tsx`, `frontend/src/pages/capture/CapturePage.tsx`, `frontend/src/pages/buildings/BuildingsPage.tsx`, `frontend/src/pages/buildings/BuildingDrawer.tsx`, `frontend/src/pages/accounts/AccountsPage.tsx`, `frontend/src/pages/history/HistoryPage.tsx`, `frontend/src/pages/alerts/AlertsPage.tsx`, `frontend/src/pages/inspection-summary/InspectionSummaryPage.tsx`, `frontend/src/pages/export/ExportPage.tsx`, `frontend/src/pages/print-signs/PrintSignsPage.tsx`. Shared candidates: `frontend/src/shared/components/SurfacePanel.tsx` and `frontend/src/shared/components/KpiCard/KpiCard.tsx`.

**Interfaces:** Preserve the existing inputs and children of `SurfacePanel`, `FilterToolbar`, and `KpiCard`. Login and fallback screens are cross checks if affected by a shared edit.

- [ ] Compare Areas, Buildings, Accounts and History with `renderAdminSetupGrid`. Use the same heading and toolbar treatment on Alerts, Inspection Summary, Export and on-screen Print. Use each existing page's own controls and state. Capture an overlay on each changed page that has one, including an area editor and building/account forms.
- [ ] Align spacing and grouping around existing headings, panels, fields and table wrappers. A matching heading group can use `flex flex-wrap items-end justify-between gap-3`; apply it at the page container when the reference supports it. Keep scope badges and status copy tied to their actual semantics. Keep the export acknowledgement, review/retry states, cursor pagination, area and point controls, and current account actions. Apply print page styling to the on-screen controls only. Compare the printed sheet against the prior output using the same synthetic sign.
- [ ] Run `npm --prefix frontend test -- src/pages/areas src/pages/buildings src/pages/accounts src/pages/history src/pages/alerts src/pages/inspection-summary src/pages/export src/pages/print-signs src/pages/capture src/pages/auth src/router.test.tsx`. Capture loading/empty/error states that apply, exercise the dialogs' focus return, and compare narrow layouts and the mobile Admin menu.

### Task 4: Cleaner and Supervisor phone screens

**Files:** `frontend/src/pages/my-work/MyWorkPage.tsx`, `frontend/src/pages/my-work/NextJob.tsx`, `frontend/src/pages/schedules/SchedulePage/SchedulePage.tsx` (own week surface), `frontend/src/pages/check-in/CheckInPage/CheckInPage.tsx`, `frontend/src/pages/check-in/AttendanceTimeline.tsx`, `frontend/src/pages/scan/SubmitForm.tsx`, `frontend/src/pages/my-building/MyBuildingPage.tsx`, `frontend/src/pages/my-building/BuildingCard.tsx`, `frontend/src/pages/inspect/InspectForm.tsx`, `frontend/src/shared/components/PhoneScreen.tsx`, `frontend/src/shared/components/BottomBar.tsx`, `frontend/src/shared/components/CleanerBottomNav/CleanerBottomNav.tsx`.

**Interfaces:** Keep `PhoneScreen`, `BottomBar` and `CleanerBottomNav` composition, the role gate, and the existing route or inline-guidance behavior for each tab.

- [ ] Compare `renderPhoneMyWork`, `renderPhoneSchedule`, `renderPhoneCheckIn`, and `renderPhoneScanAndSubmit`. Apply the same phone heading/Card/ChoiceTile patterns to My Building and inspection. Capture active and off-shift states where they apply, an attendance step pending action, and submission/inspection choices with secondary fields visible.
- [ ] Align section spacing, numeric emphasis and choice padding using the existing `PhoneScreen` and tokens. For 320 px use `min-w-0` on card text wrappers and allow Thai labels to wrap. Keep the safe-area inset on the component already owning it and the existing `BottomBar`/tab stacking. Verify the last field is visible with the primary action and tabs present. Keep Cleaner actions and Supervisor inspection headings distinct and preserve existing GPS messages, retry, transfer and gap confirmation states.
- [ ] Run `npm --prefix frontend test -- src/pages/my-work src/pages/my-building src/pages/check-in src/pages/scan src/pages/inspect src/pages/schedules src/shared/components/CleanerBottomNav`. Compare before/after screenshots at 320 and 375 px, including Supervisor absence of Cleaner navigation and QR guidance off token routes.

### Task 5: Integrated review and handoff

**Files:** `docs/handoff.md`, local evidence under `.run/`. Relevant scenarios are in `frontend/e2e/desktop/{dashboard,attendance,scheduling,print}.spec.ts` and `frontend/e2e/phone/{check-in,rework-flow,schedule-attendance}.spec.ts`.

- [ ] Review all before/after pairs and run the static checks, full frontend suite, and owned browser profile below. The large profile enables scheduling only in its runner environment. Use the default profile if needed to reproduce the scheduling fallback. Record any skipped browser case with its actual reason.

  ```sh
  npm --prefix frontend run lint
  npm --prefix frontend test
  npm --prefix frontend run build
  E2E_HEADLESS=1 E2E_SLOW_MO=0 npm --prefix frontend run e2e -- --dataset large
  ```

- [ ] For each changed surface, record route, role, matching scene or inherited pattern, viewport, compared states, and evidence paths. Confirm `scrollWidth <= innerWidth` on normal pages, scrolling inside wide table regions, the keyboard flow, and accessible primary actions. Confirm Dashboard filter/building actions and live updates, attendance correction and cover, a roster action with the enabled fixture, and valid QR Cleaner and Supervisor actions. Compare print output and record any remaining device limitations.
- [ ] Update `docs/handoff.md` with the actual changes, verification and evidence locations, plus the state of open work. Use only the repository's authorized commit workflow.
