# Facility Design System: Master

Read this before building or moving any page. A page file in `pages/` (`phone.md`, `admin.md`) overrides it where they differ.

- Decisions: facility-0086 (Tailwind), facility-0087 (the new look), facility-0088 (icons), facility-0072 (device font).
- **The `@theme` block of `frontend/src/index.css` is the source of the token values.** The values below are the ones agreed in the grill (2026-10-22), shown so the rules can be read. If `index.css` and this file disagree, `index.css` is right: fix this file in the same change.
- Code rules (Tailwind usage, class names, tests) are in `docs/agents/frontend-standards.md`.
- `index.css` is the only CSS file, and Tailwind's preflight is on (#82): the browser's own heading sizes, list bullets, link colours and button faces are gone, so every element's look comes from its utilities.

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
| Input border | `--color-input-border` | `#868F91` | Field, button and toggle outlines (3.31:1 on surface, 3.03:1 on background; was `#AEB6B7`, 2.06:1, changed by the owner in #78) |

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

D2's Area states reuse these chips, with their words: ยังไม่มีคน rework red, ทำงานอยู่ passed green, พักเบรก break, เลิกงานแล้ว idle grey, มีคนทำแทน pending cyan (as in the old look). D3's alert kinds are not states, so their chips take a note's colours: refused at a sign error, far from a sign warn, a missing time plain (#78).

All pass 4.5:1 with white text. The three idle statuses share one grey on purpose: nothing needs doing.

### Notes (messages in a box)

Tokens `--color-<kind>-bg`, `--color-<kind>-border`, `--color-<kind>-text` (utilities such as `bg-info-bg`, `text-error-text`).

| Kind | Background | Border | Text |
|---|---|---|---|
| Info | `#EFF6FF` | `#BFDBFE` | `#1E40AF` (8.01:1) |
| Warn | `#FFF4E5` | `#E3B776` | `#7A4300` |
| Error | `#FDECEA` | `#E2A39B` | `#8A1C12` |

### The printed sign (D5)

The QR sign keeps the layout it had before the move, so signs already on the walls and new ones match (facility-0087 rule 8). Its tokens are for the sign and its sheet only (the sheet also shows on screen before printing), never for another page; its sizes are in points and go below the 13 px floor on purpose. Its measures (A4 sheet, 8 mm margin, 36 mm or 80 mm QR) are millimetre arbitrary values with a comment, and `@page` sits in `index.css`.

| Token (`@theme`) | Value | Use |
|---|---|---|
| `--color-sign-ink` | `#000000` | The sign's words and the QR |
| `--color-sign-soft` | `#333333` | The URL line |
| `--color-sign-cut` | `#9AA3A4` | The dashed cut line |
| `--color-sign-paper` | `#FFFFFF` | The sheet and the QR's light modules |
| `--text-sign-url` | 6.5 pt | The URL line |
| `--text-sign-meta` | 9 pt | Place, hint, sign code |
| `--text-sign-label` | 11 pt | The sign's type, "สแกนด้วยกล้องมือถือ" |
| `--text-sign-name` | 16 pt | The name on a small sign |
| `--text-sign-name-big` | 24 pt | The name on an A5 sign |

All with line height 1.25.

## Type

- Font: the device's own, `system-ui, sans-serif` (facility-0072). Never load a web font.
- A fixed scale of seven sizes; no size outside it. Tailwind's own sizes are cleared, so only these exist:

  | Token | Utility | Size | Line height | Use |
  |---|---|---|---|---|
  | `--text-xs` | `text-xs` | 13 px | 1.5 | Admin table meta, the floor everywhere |
  | `--text-sm` | `text-sm` | 14 px | 1.5 | Admin body, chips |
  | `--text-md` | `text-md` | 15 px | 1.5 | Phone secondary lines |
  | `--text-base` | `text-base` | 17 px | 1.5 | Phone body |
  | `--text-lg` | `text-lg` | 19 px | 1.4 | Phone card names, buttons |
  | `--text-xl` | `text-xl` | 22 px | 1.3 | Admin page headings |
  | `--text-2xl` | `text-2xl` | 28 px | 1.25 | Phone page headings |

- Phone body text at least **17 px**; Admin body text at least **14 px**; nothing anywhere below **13 px** (chips included).
- Line height 1.5 for body text; Thai marks above and below need the room.

## Size and spacing

- Spacing on a 4/8 px rhythm.
- Corners 8 px (`--radius`, utility `rounded`); chips may be fully round (`rounded-full`).
- Widths: phone column `--container-phone` 480 px (`max-w-phone`), Admin page `--container-desk` 1216 px (`max-w-desk`).
- Phone targets at least **48 px** high; primary actions (ส่งงาน, ลงเวลา) **56 px**, full width.
- Admin targets at least **36 px** high. The one exception is the window pill's ✕ in D4 (24 px, owner 2026-10-23): a 36 px target grew past the pill's own words and a click beside the time deleted the window, so that ✕ is exactly the size it looks. Nothing else may be smaller.

## Icons (facility-0088)

`lucide-react`, imported one at a time, one stroke width (2), sized with the spacing scale: `size-5` (20 px) beside phone text, `size-4` (16 px) beside Admin text, `size-7` (28 px) inside a `ChoiceTile`, as in the mockup. An icon always sits beside its words and gets `aria-hidden`. No icon-only buttons, except the Toast's ✕ (owner, #78) and the window pill's ✕ (owner, #79). No emoji as icons.

## Shared components (#71)

Build pages from these (in `frontend/src/shared/components`) instead of repeating class strings:

| Component | For |
|---|---|
| `Button` (`variant` primary / secondary / danger / link, `size` phone / main / desk) | Every button. A `link` button has no side padding, so its words line up with the text above it (owner, #78). `main` is the phone page's one 56 px full-width action; `danger` confirms what cannot be undone (red text and border on white, owner 2026-10-22) |
| `buttonClass(variant, size)` + `data-tap` | A react-router `Link` that looks like a button |
| `Note` (`tone` info / warn / error, `title`, `icon`, `alert`, `status`) | A message in a box; an error is `role="alert"` unless `alert={false}` (a red box that is part of the page from the start, such as the Supervisor's defect); `status` makes it `role="status"`, for the answer to a press (the recorded time on the check-in page). `noteClass(tone)` for a box that must be a `<section>` with its own heading |
| `StatusChip` (`size` md / lg) | A Point Status, always with its words |
| `Toast` (`notice`, `onClose`; with its `Notice` type) | The answer to an Admin's action (D2, D3), floating at the top centre so nothing under it moves (owner, #78). `ok` (done): info colours, a check icon, `role="status"`, gone by itself after 5 s (held while the pointer or keyboard is on it). `warn` (something the Admin did not ask for, such as their form closing): warn colours, `role="status"`. `error`: error colours, `role="alert"`. warn and error stay until ✕ (named ปิดข้อความ), which closes any of them at once |
| `toggleClass` (`styles.ts`) + `data-tap` | An Admin on/off button with `aria-pressed` (D1 filters, D2 shift, D3 tabs): 36 px, a blue frame when on |
| `tabClass` + `tabListClass` (`styles.ts`) + `data-tap` | An Admin view switcher, a real tablist (`role="tab"`, `aria-selected`, arrows and Home/End, one keyboard stop): a 2 px line under the words, never a box, so a switch is not mistaken for a button that does something beside it (D4, owner 2026-10-23) |
| `Field` (`label`, `help`), `FieldGroup` (`legend`), `TextInput`, `Select`, `TextArea` (`size` phone / desk) | A labelled control; the help line sits outside the label and is linked with `aria-describedby`; `inputClass(size)` for a bare one. `FieldGroup` is the bordered `fieldset` whose legend names a group of answers (D10's shift pattern, D4's round windows) |
| `ChoiceTile` (radio), `ToggleChip` (checkbox) | Large answers and issue tags on the phone |
| `Table` | A table in its own scroll box; style-free `<th>` / `<td>` inside |
| `Modal` (`labelledBy`, `onClose`) | A form over the page (the browser's modal `<dialog>`), so the page behind does not move: D2's correction form, D3's cover form, D6's confirm to deactivate an account (`role="alertdialog"` with `describedBy`, owner 2026-10-26: a confirm of what cannot be undone is a popup, not a box in the page). Its heading (`tabIndex={-1}`) takes the keyboard on open; Escape asks `onClose`; closing gives the keyboard back to what opened it (owner, #78) |
| `Drawer` (`labelledBy`, `onClose`) | The same modal `<dialog>` as a full-height panel on the right edge (480 px, `w-120`, commented), scrolling on its own, the page still visible on the left: D4's point editor and its Area form (owner, 2026-10-23). Escape, the keyboard and the opener's keyboard back work as `Modal`'s |
| `MoreInfo` (`summary`, `size` desk / phone) | Notes on how a page counts or keeps things, shut until asked (owner, 2026-10-26; every page, phone ones too, 2026-10-27). A warning about what the user is about to do and a field's help stay in the page. `phone`: 48 px summary, `size-5` icon, `text-md` |
| `Panel`, `Facts`, `Splash` | A boxed group, label–value pairs, the waiting line |
| `AppLayout`, `AdminNav`, `LiveBadge`, `StatusScreen`, `CrashScreen` | The shell, bar and menu, the live badge, full-page messages |

Every button-styled element gets the press feedback of `tapFeedback` (owner, 2026-10-21) through `data-tap`. Components set no body text size: each page sets `text-base` (phone) or `text-sm` (Admin) on its root, and the components inherit it.

Where an action sits follows **what it acts on**, not its verb (owner, 2026-10-24). `+ เพิ่มพื้นที่` acts on the list, so it stays at the list's top and opens a full page — creating has six fields and a warning that the code cannot be changed later. `แก้ไขพื้นที่` acts on the open Area, so it sits in that Area's header and opens in the Drawer. The two are never grouped in one row.

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
