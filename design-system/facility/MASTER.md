# Facility Design System: Master

Read this before building or moving any page. A page file in `pages/` (`phone.md`, `admin.md`) overrides it where they differ.

- Decisions: facility-0086 (Tailwind), facility-0087 (the new look), facility-0088 (icons), facility-0072 (device font).
- **The `@theme` block of `frontend/src/index.css` is the source of the token values.** The values below are the ones agreed in the grill (2026-10-22), shown so the rules can be read. If `index.css` and this file disagree, `index.css` is right: fix this file in the same change.
- Code rules (Tailwind usage, class names, tests) are in `docs/agents/frontend-standards.md`.

The goal of the look: **easy to read and easy to tap at the sign.** A Cleaner stands alone at a QR sign, one hand on the phone, sometimes outdoors. Every choice below serves that before it serves looks.

## Style

Swiss-style minimalism, light background only (no dark mode this round). Clear type hierarchy, thin borders, 8 px corners, no heavy shadows, no decoration that carries no meaning.

## Colour

Each colour has one meaning.

| Role | Token (`@theme`) | Value | Meaning / use |
|---|---|---|---|
| Primary | `--color-primary` | `#2563EB` | Anything you can press: primary buttons, links, focus ring |
| Primary pressed | `--color-primary-strong` | `#1D4ED8` | Hover and pressed state of primary |
| On primary | `--color-on-primary` | `#FFFFFF` | Text on primary (5.17:1) |
| Background | `--color-bg` | `#F4F5F4` | Page background |
| Surface | `--color-surface` | `#FFFFFF` | Cards, panels, tables |
| Text | `--color-text` | `#1D2324` | Body text |
| Muted text | `--color-muted` | `#5F696B` | Secondary text (5.17:1 on background) |
| Border | `--color-border` | `#D5DADA` | Card and table borders |
| Input border | `--color-input-border` | `#AEB6B7` | Field outlines |

**No separate accent colour.** Orange, red and green are only ever status.

### Status colours (facility-0047)

A status chip is white text on the status colour, **always with its words** (never colour alone).

| Point Status | Words | Token | Value |
|---|---|---|---|
| Rework | ต้องแก้ไข | `--color-status-rework` | `#B42318` |
| Overdue | เลยเวลา | `--color-status-overdue` | `#A85A00` |
| PendingInspection | รอตรวจ | `--color-status-pending` | `#0E7490` |
| Passed | ตรวจผ่าน | `--color-status-passed` | `#2E6E4A` |
| NotYetDone, BeforeFirstRound, OffHours | ยังไม่ทำ, ยังไม่ถึงรอบ, นอกเวลา | `--color-status-idle` | `#5F696B` |
| Break (attendance, D2) | พัก | `--color-status-break` | `#6B5B95` |

All pass 4.5:1 with white text. The three idle statuses share one grey on purpose: nothing needs doing.

### Notes (messages in a box)

| Kind | Background | Border | Text |
|---|---|---|---|
| Info | `#EFF6FF` | `#BFDBFE` | `#1E40AF` (8.01:1) |
| Warn | `#FFF4E5` | `#E3B776` | `#7A4300` |
| Error | `#FDECEA` | `#E2A39B` | `#8A1C12` |

## Type

- Font: the device's own, `system-ui, sans-serif` (facility-0072). Never load a web font.
- A fixed scale of about six sizes; no size outside it.
- Phone body text at least **17 px**; Admin body text at least **14 px**; nothing anywhere below **13 px** (chips included).
- Line height 1.5 for body text; Thai marks above and below need the room.

## Size and spacing

- Spacing on a 4/8 px rhythm.
- Corners 8 px (`--radius`); chips may be fully round.
- Phone targets at least **48 px** high; primary actions (ส่งงาน, ลงเวลา) **56 px**, full width.
- Admin targets at least **36 px** high.

## Icons (facility-0088)

`lucide-react`, imported one at a time, one stroke width, sizes from a token. An icon always sits beside its words and gets `aria-hidden`. No icon-only buttons. No emoji as icons.

## States every page shows

- Loading: the words "กำลังโหลด..." (or a skeleton where the layout is known), never a frozen screen.
- Error: what went wrong and what to do, near the thing that failed; field errors under the field, linked with `aria-describedby`.
- Empty: say there is nothing, and why if known.
- Disabled: real `disabled`, visibly weaker, no action.

## Not used (rejected in the grill)

Web fonts (Kalam, Patrick Hand, Fira), landing-page layouts (Hero + CTA), GSAP or scroll animation, dark Admin theme, an orange accent, pill-shaped primary buttons, hover-only affordances on the phone.

## Pre-delivery check (each page, before calling it done)

- [ ] Text contrast at least 4.5:1; icons and control borders at least 3:1
- [ ] Type sizes meet the minimums above; nothing below 13 px
- [ ] Targets meet the minimums above, with at least 8 px between them
- [ ] Every status shows its words, not only a colour
- [ ] Visible focus ring on every control (keyboard)
- [ ] Loading, error, empty and disabled states as above
- [ ] Icons beside words, `aria-hidden`, no emoji
- [ ] No horizontal scroll at 375 px; Admin pages checked at 1280 px
- [ ] `prefers-reduced-motion` respected if anything moves
- [ ] Checked by eye on a real iPhone and a real Android phone (device fonts differ), and on a computer; for D5, a real print
