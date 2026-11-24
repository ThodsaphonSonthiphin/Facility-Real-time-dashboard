# Admin Pages: Overrides

Applies to all 12 Admin pages (Overview `/admin/overview` as Admin home, Dashboard `/dashboard`, Attendance, Alerts, Schedules, Inspection Summary, Export, Areas, Buildings, Print Signs, Accounts, History) plus Capture `/admin/areas/:areaId/capture`. The printed signs (D5) keep their current layout (facility-0087 rule 8). Rules here override `../MASTER.md`; everything else follows it.

## Context

An Admin at a desk with a mouse and keyboard, or reviewing on a tablet/phone when away from the desk, watching many Areas at once (70–72 Areas, about 170 Cleaners at full rollout).

## Layout

- From `1024 px` (`lg`), the menu is a persistent 240 px sidebar with 3 mental-model groups (**หน้างานวันนี้**, **ตารางและรายงาน**, **ตั้งค่าระบบ**). Below `1024 px`, a sticky `z-30` top header bar holds the `เมนูแอดมิน` Drawer trigger. Content width up to `--container-content` (1216 px) with 24 px padding on desktop; works across 375 px, 768–1024 px, and 1280 px+ without horizontal page scroll (wide tables scroll inside their own box, and two-column layouts in `AreasPage` and `ExportPage` start at `xl` / 1280 px).
- Filter toolbars use responsive grids with `size="adaptive"` controls (48 px / 16 px on mobile, 36 px / 14 px from `sm`).
- Same light theme as the phone; no dark theme.

## Type and density

- Body 14 px minimum, 13 px only for table meta; dense spacing (8–32 px).
- Tables: aligned numbers, clear header row, row height from the 36 px target.

## Interaction

- Targets at least 36 px high on desktop (`sm`+) and 48 px on mobile (`size="adaptive"`).
- Hover states allowed (mouse), but never the only way to reach an action.
- Keyboard: every action reachable with Tab, visible focus ring, with `scroll-padding-top: 72px` so sticky headers never obscure focused elements.
- Live pages (Overview, Dashboard, Attendance, Schedules) keep SignalR + 30 s polling active silently in the background. `LiveBadge` and `ConnectionBanner` read `useOnlineStatus` and show `"ขาดการเชื่อมต่อ"` only while the browser is actually offline (`!isOnline`).

## Layout chosen from the mockups (owner, 2026-10-22)

Mockups: branch `prototype/tailwind-look`, `docs/design/tailwind-look-prototype.html`.

- **Dashboard D1:** Enterprise Light keeps cards grouped by Area, building/status/Issue filters and adds search and problem-first ordering/folding as approved for #127. Values/counts come from API data. The building picker does not show an invented problem-count field.

## Overview

- Use one shift-summary surface for Entry, Submission and Inspection. Show the ratio as text with a thin progress bar when the base is known and greater than zero. Keep the metric explanations and group controls with their numbers.
- Use flat filters above the summary. Follow-up and building comparisons share a row when space allows and stack on narrower screens. Keep the round filters before the Area results in reading and keyboard order.
