# Admin pages follow the approved Enterprise Light design

- Date: 2026-10-30
- Source: the owner's decisions in the UI review chat.
- Reference: `Facility Dashboard — UX UI ใหม่ (1).html`; `docs/design/enterprise-light/` contains the corresponding original page mockups.

## Decision

Amended by facility-0101 (2026-10-31): the owner explicitly requested aligning the nested forms and confirmation windows as well as the routes covered here.

Align `/admin/inspection-summary`, `/admin/export`, `/admin/areas`, `/admin/buildings`, `/admin/print`, `/admin/accounts` and `/admin/history` with the approved design's structure, controls, typography, spacing and states, rather than changing only their headings.

1. Areas follows the design: one detail view instead of the points/check-in tabs, and a searchable list with a building filter. Keep all sign-management actions available through the area and point Drawers, and preserve existing deep links.
2. Buildings keeps Drawers for creating, renaming and enabling/disabling; this is the owner's explicit exception to the inline-edit mockup.
3. Accounts follows the role tabs and table layout. Omit the phone column: phone hashes cannot be turned back into phone numbers and no phone data is exposed by the API. Keep all existing account actions, password rules and self-deactivation protection.
4. Export has one navigation entry, in the Admin menu, and one download action on its page. Remove the shortcuts from Dashboard, Attendance and Inspection Summary. This supersedes facility-0053's 2026-10-21 shortcut decision; existing export URLs with dates and a single building still work.
5. Export supports selecting multiple buildings, including inactive buildings, in one workbook; the owner explicitly authorized the API extension. Keep multiple dataset checkboxes and one worksheet per chosen dataset; this is the owner's explicit exception to the mockup's radio buttons. Keep the 31-day limit, existing report semantics and safe export contents.
6. Inspection Summary includes the expandable list of points the individual Supervisor did not inspect in their own building and shift in the chosen period. The owner explicitly authorized returning these point details from the API. Preserve every existing tally's meaning; this is coverage information, not a score or ranking.
7. Audit history may use previous/next controls over its existing cursor pages. Do not invent a total row count; the API does not return one. Preserve custom date ranges and search, even where the design presents simpler presets.
8. Only the print page's screen layout changes. Printed sign cards, QR generation, sign tokens, dimensions, text and paper layout remain unchanged.

## Compatibility and limits

- Export accepts the existing `buildingId` and adds `buildingIds`. Supplying both is rejected. No building selector means all buildings; an explicitly empty list is rejected rather than exporting everything.
- Existing passwords, roles, authorization, audit confidentiality, validation, record retention and scan rules remain as specified in the earlier ADRs.
- Dates use Arabic digits and Gregorian numeric dates (`29/10/2026`), never English month names.
- Counts, names and options come from actual API data, never the mockup's illustrative values. Do not invent row estimates, phone numbers or supervisor names.
- New UI state and handlers live in page hooks beside their UI files. Use the existing Tailwind and shared components; no new dependencies or unrelated restructuring.
- Work remains uncommitted until the owner asks for a commit/push/PR/merge.
