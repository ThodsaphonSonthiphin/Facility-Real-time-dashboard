# Facility Design System: Master

Read this before building or moving any page. A page file in `pages/` (`phone.md`, `admin.md`) overrides it where they differ.

- Decisions: facility-0086 (Tailwind), facility-0087 (the new look), facility-0088 (icons), facility-0072 (device font).
- **The `@theme` block of `frontend/src/index.css` is the source of the token values.** The values below are the ones agreed in the grill (2026-10-22), shown so the rules can be read. If `index.css` and this file disagree, `index.css` is right: fix this file in the same change.
- Code rules (Tailwind usage, class names, tests) are in `docs/agents/frontend-standards.md`.
- `index.css` is the only CSS file, and Tailwind's preflight is on (#82): the browser's own heading sizes, list bullets, link colours and button faces are gone, so every element's look comes from its utilities.

The goal of the look: **easy to read and easy to tap at the sign.** A Cleaner stands alone at a QR sign, one hand on the phone, sometimes outdoors. Every choice below serves that before it serves looks.

## Style

Professional Enterprise Light (#120, from `docs/design/enterprise-light/DESIGN_SPEC.md`, approved by the owner 2026-10-29): a light grey page, content on white, dark text; thin borders, no shadow except on a dialog or drawer, no gradient or decoration. Light background only.

## Colour

Each colour has one meaning: **blue (accent) only for what can be pressed**; red, orange, teal, green, grey and purple only for a status.

| Role | Token (`@theme`) | Value | Meaning / use |
|---|---|---|---|
| Background | `--color-bg` | `#F4F6F8` | Page background |
| Surface | `--color-surface` | `#FFFFFF` | Cards, panels, tables, the sidebar and the phone bar |
| Surface 2 | `--color-surface-2` | `#F9FAFB` | Table heads, rows that are off |
| Skeleton | `--color-skeleton` | `#E9EDF1` | Loading boxes |
| Text | `--color-text` | `#202936` | Body text (14.5:1) |
| Text 2 | `--color-text-2` | `#667085` | Secondary text (4.8:1 on surface) |
| Text 3 | `--color-text-3` | `#344054` | Menu items not chosen |
| Border | `--color-border` | `#E0E5EB` | Dividers only |
| Control border | `--color-border-control` | `#858F9E` | Field, button and toggle outlines (3.3:1, #78's 3:1 rule) |
| Accent | `--color-accent` | `#2457C5` | Primary buttons, links, focus ring (6.5:1) |
| Accent hover | `--color-accent-hover` | `#1D4AA8` | Hover and pressed state of accent |
| Accent subtle | `--color-accent-subtle` | `#EEF3FC` | The chosen menu item, the chosen answer |
| On accent | `--color-on-accent` | `#FFFFFF` | Text on a filled accent or status colour (not in the design file, which writes white) |

### Status colours (facility-0047)

A status is **always shown with its words** (never colour alone). Each has a text colour, a light background and a border; the chip's new face (light background, border, icon) comes in #122; until then `StatusChip` is white text on the status colour.

| Point Status | Words | Tokens | Text / background / border |
|---|---|---|---|
| Rework | ต้องแก้ไข | `--color-rework`, `-bg`, `-border`, `-strong` | `#B42318` / `#FEF3F2` / `#F6C9C4` |
| Overdue | เลยเวลา | `--color-overdue`, `-bg`, `-border`, `-strong` | `#B45309` / `#FEF6EC` / `#F5D6B0` |
| PendingInspection | รอตรวจ | `--color-pending`, `-bg`, `-border` | `#0B6E8A` / `#EBF6FA` / `#B4DCE8` |
| Passed | ตรวจผ่าน | `--color-passed`, `-bg`, `-border`, `-strong` | `#16803C` / `#EDF7F0` / `#B9DFC6` |
| NotYetDone, BeforeFirstRound, OffHours | ยังไม่ทำ, ยังไม่ถึงรอบ, นอกเวลา | `--color-idle`, `-bg`, `-border` | `#5B6474` / `#F2F4F7` / `#E0E5EB` |
| Break (attendance, D2) | พัก | `--color-break`, `-bg`, `-border` | `#6941C6` / `#F4F1FB` / `#D9CFF2` |

D2's Area states reuse these chips, with their words: ยังไม่มีคน rework red, ทำงานอยู่ passed green, พักเบรก break, เลิกงานแล้ว idle grey, มีคนทำแทน pending teal. D3's alert kinds are not states, so their chips take a note's colours: refused at a sign error, far from a sign warn, a missing time plain (#78).

The three idle statuses share one grey on purpose: nothing needs doing.

### Notes (messages in a box)

A note takes the status colours (the design file's DesignSystem page): `Note` tone `error` = rework, `warn` = overdue, `info` = pending, and success = passed (the tone comes in #122). Utilities such as `bg-rework-bg`, `border-overdue-border`, `text-pending`.

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

- Font: the device's own, `system-ui, -apple-system, 'Segoe UI', Roboto, 'Noto Sans Thai', sans-serif` (facility-0072): names of fonts a device may have, never a loaded web font. `--font-mono` for codes.
- A fixed scale of seven sizes; no size outside it. Tailwind's own sizes are cleared, so only these exist:

  | Token | Utility | Size | Line height | Use |
  |---|---|---|---|---|
  | `--text-xs` | `text-xs` | 13 px | 1.5 | Secondary data, the floor everywhere |
  | `--text-sm` | `text-sm` | 14 px | 1.5 | Admin body, phone secondary lines (were 15 px `text-md`, gone in #121) |
  | `--text-base` | `text-base` | 16 px | 1.5 | Phone body |
  | `--text-lg` | `text-lg` | 18 px | 1.5 | Area group heads, phone main buttons |
  | `--text-xl` | `text-xl` | 20 px | 1.3 | Section headings |
  | `--text-2xl` | `text-2xl` | 24 px | 1.3 | Page headings, a point's name on the phone |
  | `--text-3xl` | `text-3xl` | 30 px | 1.3 | Summary numbers, the phone's result heading |

- Phone body text at least **16 px** (owner, 2026-10-29, was 17 px); Admin body text at least **14 px**; nothing anywhere below **13 px** (chips included).
- Line height 1.5 for body text (Thai marks above and below need the room), 1.3 for headings. Numbers and times `tabular-nums`.

## Size and spacing

- Spacing on the 4 px scale (`--spacing: 4px`).
- Corners: `rounded-sm` 4 px (small badges), `rounded-md` 6 px (buttons, inputs, chips), `rounded-lg` 8 px (cards, tables, dialogs); chips may be fully round (`rounded-full`). #121 moved every old `rounded` (8 px) to `rounded-lg` unchanged; controls move to `rounded-md` in #122.
- Shadow `shadow-overlay` on a dialog or drawer only.
- Widths: phone column `--container-mobile` 480 px (`max-w-mobile`), Admin page `--container-content` 1216 px (`max-w-content`), Admin sidebar `--container-sidebar` 240 px (`w-sidebar`).
- Phone targets at least **48 px** high; primary actions (ส่งงาน, ลงเวลา) **56 px**, full width.
- Admin targets at least **36 px** high. The one exception is the window pill's ✕ in D4 (24 px, owner 2026-10-23): a 36 px target grew past the pill's own words and a click beside the time deleted the window, so that ✕ is exactly the size it looks. Nothing else may be smaller.

## Layout (#121)

- **Admin, from 1024 px:** a white 240 px sidebar with a right border: the system name and "แอดมิน" on top, the menu in two groups, **ติดตามงาน** (สถานะจุด, การเข้างาน, รายการที่ขึ้นเตือน with its red count, สรุปการตรวจ, ส่งออก Excel) and **ตั้งค่า** (พื้นที่และป้าย, ตึก, พิมพ์ป้าย, บัญชีผู้ใช้, ประวัติการแก้ไข), 40 px items, the open one in accent-subtle with accent text (`aria-current="page"`), and at the bottom who is logged in and ออกจากระบบ (a button: it acts). The page beside it: at most 1216 px, centred, 24 px around it.
- **Admin, narrower:** the sidebar sits above the page and its groups wrap as rows. A wide table scrolls in its own box, never the page.
- **Phone:** one column, at most 480 px, 16 px sides: a white bar (who is logged in, ออกจากระบบ) clear of the notch (`env(safe-area-inset-top)`), then the page, which scrolls with the window and fills the rest of the screen (`min-h-svh`, not dvh: owner, 2026-10-27). `BottomBar` is the white bar for the page's one main action, held at the bottom and clear of the home bar (`env(safe-area-inset-bottom)`); the pages move into it in #123.

## Icons (facility-0088)

`lucide-react`, imported one at a time, one stroke width (2), sized with the spacing scale: `size-5` (20 px) beside phone text, `size-4` (16 px) beside Admin text, `size-7` (28 px) inside a `ChoiceTile`, as in the mockup. An icon always sits beside its words and gets `aria-hidden`. No icon-only buttons, except the Toast's ✕ (owner, #78) and the window pill's ✕ (owner, #79). No emoji as icons.

## Shared components (#71)

Build pages from these (in `frontend/src/shared/components`) instead of repeating class strings:

| Component | For |
|---|---|
| `Button` (`variant` primary / secondary / danger / link, `size` phone / main / desk) | Every button. A `link` button has no side padding, so its words line up with the text above it (owner, #78). `main` is the phone page's one 56 px full-width action; `danger` confirms what cannot be undone (red text and border on white, owner 2026-10-22) |
| `buttonClass(variant, size)` + `data-tap` | A react-router `Link` that looks like a button |
| `Note` (`tone` info / warn / error, `title`, `icon`, `alert`, `status`) | A message in a box, in the status colours (see Notes); an error is `role="alert"` unless `alert={false}` (a red box that is part of the page from the start, such as the Supervisor's defect); `status` makes it `role="status"`, for the answer to a press (the recorded time on the check-in page). `noteClass(tone)` for a box that must be a `<section>` with its own heading |
| `StatusChip` (`size` md / lg) | A Point Status, always with its words |
| `Toast` (`notice`, `onClose`; with its `Notice` type) | The answer to an Admin's action (D2, D3), floating at the top centre so nothing under it moves (owner, #78). `ok` (done): info (pending) colours, a check icon, `role="status"`, gone by itself after 5 s (held while the pointer or keyboard is on it). `warn` (something the Admin did not ask for, such as their form closing): warn colours, `role="status"`. `error`: error colours, `role="alert"`. warn and error stay until ✕ (named ปิดข้อความ), which closes any of them at once |
| `toggleClass` (`styles.ts`) + `data-tap` | An Admin on/off button with `aria-pressed` (D1 filters, D2 shift, D3 tabs): 36 px, a blue frame when on |
| `tabClass` + `tabListClass` (`styles.ts`) + `data-tap` | An Admin view switcher, a real tablist (`role="tab"`, `aria-selected`, arrows and Home/End, one keyboard stop): a 2 px line under the words, never a box, so a switch is not mistaken for a button that does something beside it (D4, owner 2026-10-23) |
| `Field` (`label`, `help`), `FieldGroup` (`legend`), `TextInput`, `Select`, `TextArea` (`size` phone / desk) | A labelled control; the help line sits outside the label and is linked with `aria-describedby`; `inputClass(size)` for a bare one. `FieldGroup` is the bordered `fieldset` whose legend names a group of answers (D10's shift pattern, D4's round windows) |
| `ChoiceTile` (radio), `ToggleChip` (checkbox) | Large answers and issue tags on the phone |
| `Table` | A table in its own scroll box; style-free `<th>` / `<td>` inside |
| `Modal` (`labelledBy`, `onClose`) | A form over the page (the browser's modal `<dialog>`), so the page behind does not move: D2's correction form, D3's cover form, D6's confirm to deactivate an account (`role="alertdialog"` with `describedBy`, owner 2026-10-26: a confirm of what cannot be undone is a popup, not a box in the page). Its heading (`tabIndex={-1}`) takes the keyboard on open; Escape asks `onClose`; closing gives the keyboard back to what opened it (owner, #78) |
| `Drawer` (`labelledBy`, `onClose`) | The same modal `<dialog>` as a full-height panel on the right edge (480 px, `w-120`, commented), scrolling on its own, the page still visible on the left: D4's point editor and its Area form (owner, 2026-10-23). Escape, the keyboard and the opener's keyboard back work as `Modal`'s |
| `MoreInfo` (`summary`, `size` desk / phone) | Notes on how a page counts or keeps things, shut until asked (owner, 2026-10-26; every page, phone ones too, 2026-10-27). A warning about what the user is about to do and a field's help stay in the page. `phone`: 48 px summary, `size-5` icon, `text-sm` |
| `Panel`, `Facts`, `Splash` | A boxed group, label–value pairs, the waiting line |
| `AppLayout`, `AdminNav`, `BottomBar`, `LiveBadge`, `StatusScreen`, `CrashScreen` | The shell (see Layout), the sidebar menu, the phone's bottom bar, the live badge, full-page messages |

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
