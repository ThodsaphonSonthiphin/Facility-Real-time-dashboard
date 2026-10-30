# Seven Admin pages implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring all seven Admin pages into the approved design, including the owner-approved export and inspection-summary capabilities.

**Architecture:** Reuse the existing Tailwind/shared controls and page API endpoints. Keep new page state and handlers in adjacent hooks. Add backward-compatible report contract fields and database filters; do not change schema, authentication or existing tally semantics.

**Tech Stack:** React/TypeScript, Redux Toolkit Query, Tailwind, lucide-react, .NET 10, EF Core/MySQL, Vitest, xUnit and Playwright.

**Spec:** `docs/superpowers/specs/2026-10-30-admin-design-alignment.md`; decision `docs/adr/facility-0100-admin-pages-follow-approved-design.md`.

## Global Constraints

- Read the spec's shared requirements and relevant page section. Exact rendering references: `/private/tmp/facility-approved-design/` (standalone HTML plus screenshots), original approved file in Downloads; persistent references in `docs/design/enterprise-light/mockups/`.
- No new dependencies, no schema/auth changes, no `.csproj` or `.idea` edits. No commits/pushes/PRs/merges. Preserve the existing dirty tree and all earlier four review fixes.
- Tailwind and existing components only; 14 px body, minimum 13 px, 36 px controls, numeric Gregorian dates, visible labels/focus. Do not introduce fake counts, phone data, secrets or raw GPS.
- Preserve existing business rules, deep links and all loading/error/empty/success/disabled states. Approved exceptions are only those in facility-0100.
- Owner's app runs on port 5001 with a live quick tunnel. Do not stop/rebuild it during individual tasks. Focused tests only during iteration. Root performs full suites/build and restarts after all tasks; no `npm run e2e`.
- Incremental pre-task source snapshot: `/private/tmp/facility-seven-pages-before/`. Work stays on current `feat/ui-admin-setup`; no checkout, stash or reset.
- Tests use roles/labels/text. Behavior/API changes get regression tests; CSS is validated with real browser geometry/screenshots, not class assertions. Report precise commands/results.
- Implementation agents do not dispatch helpers/reviewers. Root reviews each task. An execution report is written beside its task brief; return only status, concise test result and concerns.

### Task 1: Export selection, layout and one navigation entry

**Files:** Modify `backend/src/FacilityRealtime.Api/{DTOs/ExportDtos.cs,Endpoints/AdminExportEndpoints.cs}`, `backend/src/FacilityRealtime.Infrastructure/Persistence/{ExportQuery.cs,InspectionHistoryQuery.cs}`, export API/query tests, `frontend/src/pages/export/{ExportPage.tsx,exportApi.ts,ExportPage.test.tsx}`, dashboard/attendance/inspection-summary pages and their tests. Create `frontend/src/pages/export/useExportPage.ts` and focused building-selector UI as needed within that page directory. Amend facility-0053 with the superseding decision link. Do not edit inspection-summary tally logic.

**Interfaces:** Existing `buildingId` and date deep links work; new JSON `buildingIds: number[]` filters every export dataset and missing-round query. Page selection state is `number[] | null` (`null` = all, `[]` = none). Existing `sets: ExportSet[]` remains.

- [ ] Write API regression tests that submit two selected buildings while a third has attendance/submissions/inspections; open the actual XLSX with ClosedXML and assert only selected buildings appear, including generated missed rounds. Test empty IDs, unknown/non-positive IDs, duplicates, both selectors, and legacy request/audit behavior. Use actual database fixtures, not mocked query outputs.

```csharp
var response = await admin.PostAsJsonAsync("/api/admin/export", new {
    from = "2026-10-08", to = "2026-10-08",
    buildingIds = new[] { a, b }, sets = new[] { "Attendance", "Submissions", "Inspections" }
});
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
```

- [ ] Extend the record additively and normalize/validate selectors before any export query or audit. Keep all-building omission and legacy audit field shape; add explicit selections to multi-building audit. Filter queries using SQL membership, not one query/export per building.

```csharp
public record ExportRequest(string? From, string? To, int? BuildingId,
    List<ExportSet>? Sets, List<int>? BuildingIds = null);
```

- [ ] Implement the spec's Export card/summary layout and hook. Use actual building metadata, multiple dataset checkboxes, acknowledgement, genuine selected count, existing max-31-day check, and one download action. An unavailable building list blocks exporting and offers retry. Preserve URL-derived dates/single building.

```ts
export interface ExportRequest {
  from: string; to: string; buildingId?: number;
  buildingIds?: number[]; sets: ExportSet[];
}
```

- [ ] Remove only the three in-page shortcuts, not the Admin menu or export route. Update the affected tests to assert no Export link inside `main`, but one menu link with `/admin/export`. Preserve QR-route restrictions and other assertions.
- [ ] Run `npm test -- src/pages/export src/pages/dashboard/DashboardPage.test.tsx src/pages/attendance/AttendancePage.test.tsx src/pages/inspection-summary/InspectionSummaryPage.test.tsx` in frontend and `dotnet test backend/tests/FacilityRealtime.ApiTests --filter 'FullyQualifiedName~Export'` at repo root. Self-review scoped diff; report results without a commit.

### Task 2: Inspection coverage details and compact summary UI

**Files:** Modify `backend/src/FacilityRealtime.Application/Inspections/InspectionSummary.cs`, summary DTO/endpoint, summary unit/API tests, `frontend/src/pages/inspection-summary/{InspectionSummaryPage.tsx,SummaryTable.tsx,summaryApi.ts,InspectionSummaryPage.test.tsx}`, `frontend/src/test/reportFixtures.ts`. Create `useInspectionSummaryPage.ts`, `useSummaryTable.ts` and a small expanded-row component within the page directory as needed.

**Interfaces:** Add row `uninspectedPoints: { pointId: number; areaCode: string; pointName: string; signCode: string }[]`. Export changes from Task 1 remain intact. Existing summary metrics and date/building deep links remain valid.

- [ ] Add tests for an uninspected active beat point, inspected same-Supervisor/same-shift exclusion, inspection by another Supervisor, cover outside the beat, Night exclusion of Day-only areas, inactive points/Areas and a zero-point beat. Assert old totals unchanged and list length equals `PointsTotal - PointsInspected`.
- [ ] Extend `SupervisorTally` with an additive initialized ID-list property so existing positional constructors remain compatible; compute IDs with the same `beatPoints` and own-shift predicate already used for PointsInspected. Project only safe point fields and sort deterministically in the endpoint.

```csharp
public IReadOnlyList<int> UninspectedPointIds { get; init; } = [];
// In Tally, use beatPoints.Except(own.Where(i => i.Slot.Shift == supervisor.Shift)
//     .Select(i => i.PointId)) for this property; old count expressions stay unchanged.
```

- [ ] Replace the wide default table with the exact mockup's eight columns and no-ranking note. Implement working preset/custom date and shift controls. Keep building/custom filters and old aggregate details in disclosures so existing information and URLs remain accessible. Use accessible expanded-row point chips and actual counts.
- [ ] Test role-safe expansion, keyboard/aria behavior, shift filtering, custom/deep-linked dates and zero/error/loading states. Do not restore an Export shortcut. Run frontend summary tests, backend summary unit/API filters, then self-review and report.

### Task 3: Areas list filters and unified detail view

**Files:** Modify `frontend/src/pages/areas/{AreasPage.tsx,AreaList.tsx,AreaDetailPanel.tsx,AreaEditorDrawer.tsx,PointTable.tsx,AreasPage.test.tsx,PointEditor.test.tsx,SignPanel.test.tsx}`. Create `useAreasPage.ts`, `useAreaDetailPanel.ts` and a check-in sign-management component in that directory. Preserve `AreaFormPage` behavior and the corrected exact-dialog assertion.

**Interfaces:** Areas metadata already contains building IDs/codes, active state and counts; existing detail APIs/mutations are unchanged. Search/building filter stays client-side, and `selectedId` still follows the existing route.

- [ ] Add role/text tests for searching code/name/building, filtering buildings, clear-filters/no-match behavior and keeping an explicitly deep-linked Area selected. Test that points and check-in facts are available together without a tablist.
- [ ] Render the actual mockup's rail search/dropdown/list/count footer and unified name/status/facts/actions card plus point table. No static mockup sample counts. Keep local filtering state/derived lists in the hook.
- [ ] Move the former check-in tab's full management controls into the Area editing Drawer, preserving capture/map/radius/QR/print functionality and guards. Keep point editing/QR management in its existing Drawer and expose table actions matching the design using the existing safe confirmation path.
- [ ] Update only assertions tied to the explicitly changed structure; retain all API payload, forbidden-action and stale-detail assertions. Run `npm test -- src/pages/areas`. Verify no changed file in `pages/print-signs` or sign tokens; self-review and report.

### Task 4: Accounts role tabs and design table

**Files:** Modify `frontend/src/pages/accounts/{AccountsPage.tsx,AccountTable.tsx,accountRules.ts,AccountsPage.test.tsx,AccountActions.test.tsx}`; create `useAccountsPage.ts` and local props types as needed. Preserve the existing four form headers/Drawers and secret-handling forms.

**Interfaces:** Use existing `AccountRole` and account API. Tabs are `Cleaner | Supervisor | Admin`, default Cleaner. Active filter is `all | active | inactive`, independent of role/search; counts are based on actual data. Phone fields never appear in the table.

- [ ] Add tests for each counted role tab, keyboard tab switching, search within the role, deactivated accounts via active-state filtering, correct employee ID/username/place headers, no phone column and all existing actions/self-protection.
- [ ] Use shared `Tabs` with a corresponding labelled `tabpanel`, design status tags/actions/footer and visible search label. Put hook/state/handlers outside the JSX page; do not alter API validation, reassignment, generated-password rules or any form payload.
- [ ] Adapt existing helpers to explicitly select the role needed by each test; do not weaken assertions to make the new default pass. Keep the four Drawer focus/close tests and strengthen them if helper changes require it. Run `npm test -- src/pages/accounts`; self-review and report.

### Task 5: Buildings table and Print screen frames

**Files:** Modify `frontend/src/pages/buildings/{BuildingsPage.tsx,BuildingTable.tsx,BuildingsPage.test.tsx}`, `frontend/src/pages/print-signs/{PrintSignsPage.tsx,printSignsApi.ts,PrintSignsPage.test.tsx}`. Use adjacent page hooks for substantial state/handler extraction; preserve BuildingDrawer's CRUD/guard behavior. Check actual print-test filename before editing if named differently.

**Interfaces:** `ManagedBuilding.activeAreaCount` supplies actual area counts; the existing Admin accounts API supplies active Supervisor names per building/shift. Building groups for printing use existing Area metadata/detail queries; existing area/sign URLs remain valid. No backend changes.

- [ ] Match Buildings' table spacing, building names, area counts, active Supervisor names, inactive rows and actions; keep create/rename/active-state operations in the existing Drawer, as the owner's exception. Preserve disabled-state tests. Distinguish loading/error of the names query from an actually empty assignment.
- [ ] Match Print header/summary/right-side print action, separate toolbar and grey screen preview frame. Contain paper scrolling in that preview at narrow widths; remove the frame in print media. Add real building-group selection with existing detail fetching and preserve `?area=...&sign=...`, all/single printing and stale-token refetch. Reuse shared size controls with correct accessible semantics.
- [ ] Never change `SignCard.tsx`, `QrCode.tsx`, `qr.ts`, sign tokens, `sheetClass`, `sheetGrid` or paper layout. Test building selection excludes other buildings and inactive signs, existing deep links, sizes and refetch-before-print.
- [ ] Run `npm test -- src/pages/buildings src/pages/print-signs`; report changed files, checks and exact print invariants retained.

### Task 6: Audit history filters, structured changes and cursor pages

**Files:** Modify `frontend/src/pages/history/{HistoryPage.tsx,HistoryControls.tsx,HistoryTable.tsx,historyRules.ts,HistoryPage.test.tsx}` and existing history-rule tests. Create `useHistoryPage.ts` and a typed change-value component in that directory. Existing API query/authorization stays unchanged.

**Interfaces:** New structured helper returns `{ label?: string; before: string; after: string }[]`; keep `changeLines` backward-compatible as a formatter over the same structured data. Cursor pages are the existing RTK infinite-query data, with a separate visible page index reset on query identity change.

- [ ] Add tests for raw names containing arrows, secret-change actions with no secret values, null/primitive snapshots, field-specific dates/enums and same-value suppression. Confirm old `changeLines` fixtures still pass.
- [ ] Match mockup header/read-only badge, compact 7/30/custom range/actor/category controls, five-column table and action badges. Keep custom date/search accessible. Render before/after as safe React text in red struck-through/green spans; never parse display strings or inject HTML.
- [ ] Implement previous/next cursor navigation with truthful ranges, filter reset, retained current page on a failed next fetch, no guessed total and no lost loading/empty/footer states. Test two loaded pages, next fetch failure/retry, Back/filter reset and zero results.
- [ ] Run `npm test -- src/pages/history`; self-review and report. Root then runs frontend full suite/lint/build, backend required suites, seven-page screenshot comparison at 1280, 375 overflow checks, API smoke checks and unchanged print-PDF validation, followed by independent final review and handoff update.
