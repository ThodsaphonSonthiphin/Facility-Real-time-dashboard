# Seven Admin pages: approved design alignment

## Authority and scope

Implement facility-0100. The owner approved the seven-page alignment, Areas without its two tabs, Accounts with role tabs but without a phone column, Buildings Drawers, a single Export navigation entry, multiple export buildings with an API extension, multiple dataset checkboxes, and expandable uninspected-point lists with an API extension. These decisions replace the corresponding omissions in the earlier #126 handoff.

Use the actual approved HTML, `/Users/macbookair/Downloads/Facility Dashboard — UX UI ใหม่ (1).html`. Exact standalone pages, including their embedded rendering runtime, have been decoded to `/private/tmp/facility-approved-design/{inspection-summary,export,areas,buildings,print,accounts,history}.html`. Render at 1280 px and compare real screenshots. If temporary references are missing, decode the HTML's `__bundler/manifest` JSON: base64 decode each entry, gzip decompress entries with `compressed: true`, and select the document by its title. The repo mockups and DESIGN_SPEC provide persistent style references.

## Shared requirements

- Existing API/validation/business rules remain, except the two expressly authorized additive API extensions below.
- Preserve loading/error/empty/success/disabled states, focus restoration, native modal behavior, truthful dynamic counts and all existing management actions.
- Tailwind tokens and shared components; device fonts; 14 px Admin body, minimum 13 px; controls at least 36 px; text contrast 4.5:1; icons/control boundaries 3:1; visible labels/focus; no new dependencies.
- At 375 px, tables scroll inside their containers and never enlarge the document. At 1280 px, match the design's hierarchy, column widths, spacing, colors and control placement.
- Keep original deep links and query parameters working. Custom ranges and existing extra information can be placed under clearly labelled additional filters/details, preserving the clean default design.
- No fake data, no guessed totals, no row estimates the API cannot establish. Do not show phone numbers/hashes, secrets, QR tokens or raw GPS in report responses.
- Do not modify owner `.csproj` files or `backend/.idea/`. No commits, pushes, PRs or merges.

## Export

Match the design's large form card on the left and file summary on the right. Separate dataset, date and building sections with the prescribed borders/padding. Dataset cards remain checkboxes. Building choices show fixed-width monospace codes, truncated names with titles, a distinct all-buildings row, and an actual selected count. Include inactive buildings as before. Render the personal-data note and its acknowledgement checkbox above the single download button as shown by the design. The acknowledgement is a page-level prerequisite; it does not change the export API's authorization or legacy clients.

The summary shows actual selected dataset names, numeric date range and selected buildings. Preserve the existing filename and download/error/success flow. Do not invent a row estimate. A failed building load must show an error/retry and prevent an accidental all-building export; loading/explicitly empty selection cannot silently mean all.

Extend `ExportRequest` with optional `List<int>? BuildingIds = null`, preserving the positional constructor and JSON fields for existing requests. `buildingId` and `buildingIds` are mutually exclusive. Reject empty, non-positive and unknown explicit IDs; deduplicate valid IDs. Omitted selectors mean every building. Filter attendance, submissions, active-point missing-round generation, inspection rows and finished-round inputs consistently by selected buildings, in the database. Do not issue one complete export query per building. Keep existing query method calls working with optional extra collection parameters/overloads. Legacy requests retain their audit snapshot shape; multi-building requests record their explicit selection and accurate row counts.

Remove Export shortcuts from Dashboard, Attendance and Inspection Summary and keep the Admin menu link. The export route still understands old single-building date URLs.

## Inspection Summary

Match the design's compact header with date-range and shift selects, the always-visible no-ranking information note, and its eight-column table: Supervisor; building/shift; inspection count; submitted but not inspected; reworks; far inspections; never-inspected points; details action. Highlight only positive attention counts with their status colors. Expand the never-inspected list beneath its row as real point chips, with `aria-expanded` and `aria-controls`. Preserve old aggregate information in a row detail disclosure, without widening the default table. Additional building/custom-range filters remain accessible.

Add `UninspectedPoints` to each summary row, with items `{ pointId, areaCode, pointName, signCode }`. Its set is the same active beat used for `PointsTotal`, excluding points inspected by that Supervisor in their own shift during the requested period. Covers outside the beat do not remove own-beat points. Day-only Areas are excluded for Night supervisors. Sort by area code, sign code and point id. The list length equals `PointsTotal - PointsInspected`; all old tallies retain their exact semantics. Add no QR tokens or coordinates. Client shift filtering uses the existing returned shift values.

## Areas

Use the design's list rail, search and building dropdown, scrollable actual area list and truthful shown/total footer. Show all relevant buildings, including ones needed to manage inactive areas. Filtering must not silently mutate data or replace a deep-linked selected Area. Show a useful no-match state and clear filters action.

Use one detail card with name/status/actions and the cleaner/check-in-sign facts, then the point table directly beneath it. Remove the points/check-in tab switch. Keep the table's shift/radius/place columns and action placement from the design. Preserve print links, QR regeneration, location capture/map fallback, radius editing and active-state restrictions. Move the former check-in tab's management controls into the Area editing Drawer so nothing disappears. Existing point editing and sign controls stay in their Drawer. Surface real failures and retain existing confirmations and guards.

## Accounts

Use the three counted role tabs (Cleaner/Supervisor/Admin), search, role-appropriate employee ID/username and place headers, status tags and the design's compact actions. Omit phone cells entirely. Keep deactivated accounts available through an active-state filter alongside search, without a fourth role tab. Keep all creation/edit/phone/password forms in Drawers with the focus/close fix already applied. Keep the centered deactivation alertdialog and self-protection. Do not copy the mockup's sample passwords, automatic passwords or account-reassignment behavior; existing ADR rules still apply.

## Buildings and Print

Buildings follows the table typography/column spacing/status styling from the design, including actual active-area counts and the active Supervisors' names grouped by building/shift. Read names from the existing Admin accounts API; do not substitute counts for the design's names or copy its sample people. Create/rename/active-state controls use the existing Drawer; no inline edits. Keep existing active-building guards.

Print follows the design's header/summary/print-button arrangement, separate area/size toolbar and grey screen-preview frame. The preview frame contains horizontal paper scrolling at narrow widths and disappears in print media; the paper itself is unchanged. Preserve all print URLs and sizes. Offer building-group choices using existing Area metadata and detail fetching, with no invented counts. Print still refetches current tokens before printing. Do not touch `SignCard.tsx`, `QrCode.tsx`, `qr.ts`, sign tokens or sheet classes/layout. The existing 'small (6 per sheet)' name remains truthful rather than claiming a new paper dimension.

## History

Match the read-only badge/header, compact preset/actor/category filters, five-column table, action badge + summary cell, and separate red struck-through before/green after values. Generate structured change values from stored snapshots rather than parsing strings containing arrows; preserve field-specific formatting and secret suppression. Keep existing `changeLines` results compatible for callers/tests.

Use previous/next navigation over actual cached cursor pages, resetting the visible page when filters/search/range change. Fetch the next page only when needed; never show a successful next page before data arrives. Handle errors without losing the current page. Show actual displayed ranges without a fictional total. Preserve custom ranges (maximum 92 days), search, system actor entries and safe React text rendering.

## Verification

Focused behavior tests for each changed control/API contract, then frontend lint/full tests/build and backend unit/API suites appropriate to the two extensions. Browser comparisons against each rendered reference at 1280 px; 375 px overflow checks; all Drawers and empty/error/loading states. Print PDF must remain one A4 page with three seeded signs and no controls. Do not run `npm run e2e` against the owner's build because it deletes `wwwroot`; use separate read-only browser verification and report that limitation. Refresh the local test API/frontend safely after tests, retaining the database/tunnel, then update `docs/handoff.md` with actual checks and remaining owner eye checks.
