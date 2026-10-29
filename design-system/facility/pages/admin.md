# Admin Pages: Overrides

Applies to the Admin computer pages (D1–D10, M17: Dashboard, attendance, alerts, Area setup, accounts, reports, Excel export, history). The printed signs (D5) keep their current layout (facility-0087 rule 8). Rules here override `../MASTER.md`; everything else follows it.

## Context

An Admin at a desk with a mouse and keyboard, on a computer screen, watching many Areas at once (70–72 Areas, about 170 Cleaners at full rollout).

## Layout

- The menu is a 240 px sidebar from 1024 px, above the page when narrower (`../MASTER.md` Layout). Content width up to `--container-content` (1216 px) with 24 px around it; must still work at 1280 px wide without horizontal page scroll (wide tables scroll inside their own box).
- Same light theme as the phone; no dark theme.

## Type and density

- Body 14 px minimum, 13 px only for table meta; dense spacing (8–32 px).
- Tables: aligned numbers, clear header row, row height from the 36 px target.

## Interaction

- Targets at least 36 px high.
- Hover states allowed (mouse), but never the only way to reach an action.
- Keyboard: every action reachable with Tab, visible focus ring.
- Live data (Dashboard) shows when it last updated and whether the connection is live (`LiveBadge`).

## Layout chosen from the mockups (owner, 2026-10-22)

Mockups: branch `prototype/tailwind-look`, `docs/design/tailwind-look-prototype.html`.

- **Dashboard D1: variant A.** Cards grouped by Area (heading "AR01 ห้องน้ำชั้น 1", "ตึก A · 5 จุด"), tiles in an auto-fill grid; the filter row has the building select, one button per status with its count, and "มีปัญหาที่แจ้ง n".
