# Frontend Standards

The rules `code-review` checks on any frontend change (its Standards axis). The look itself is described in `design-system/facility/MASTER.md`; the decisions behind both are facility-0086, 0087 and 0088.

## Styling (Tailwind 4, facility-0086)

- Style with Tailwind utility classes. `frontend/src/index.css` is the only CSS file: no per-page or per-component `.css` file.
- Colours, sizes and radii come from the `@theme` tokens in `frontend/src/index.css`: write `bg-accent`, `text-text-2`, `rounded-md` and so on.
  - Never `bg-[var(--color-…)]` for a registered token.
  - Never a raw Tailwind palette class (`bg-blue-600`) or a raw hex in a component.
  - Arbitrary values (`w-[37px]`) only with a comment saying why no token fits.
- Class names are complete strings in the source. Never build one (`` `bg-${x}` ``); map a variant to a full class (`{ warn: 'bg-overdue-bg', … }`).
- Shared pieces (button, field, note, chip, table, bar) are React components in `frontend/src/shared/components` (listed in MASTER.md). No `@apply`. No UI library.
- A page sets its body text size once on its root: `text-base` on a phone page, `text-sm` on an Admin page. Components inherit it; the shell sets none.
- Two classes on one element must never set the same property (`underline` and `no-underline`, `px-0` and `px-4`): which one wins depends on Tailwind's output order.
- No `clsx` or `tailwind-merge` until a real class clash needs one.
- Print rules use the `print:` variant; only `@page` stays as plain CSS in `index.css`.
- Tailwind's preflight is on (#82): headings, lists, links, buttons and form controls start unstyled, so a heading is the size of the text around it unless it says otherwise (`text-lg font-semibold`), a list that shows bullets or numbers says so (`list-disc`, `list-decimal`) and a link uses `linkClass`.

## Look and accessibility (facility-0087)

- Text contrast at least 4.5:1; icons and control borders at least 3:1.
- Font sizes: phone body at least 16 px, Admin body at least 14 px, nothing below 13 px.
- Targets: phone at least 48 px high, primary phone actions 56 px and full width; Admin at least 36 px.
- A status is always shown with its words (`StatusChip`), never by colour alone.
- Blue means "can be pressed". Do not use the accent blue for status or decoration; status colours only for a status.
- Every control has a visible focus style and an accessible name; labels are visible, not placeholder-only.
- Loading, error, empty and disabled states are shown (see MASTER.md).
- Device font only (facility-0072); never load a web font.

## Icons (facility-0088)

- `lucide-react`, one named import per icon.
- An icon sits beside text and has `aria-hidden="true"`. No icon-only buttons (one exception, the owner's: the Toast's ✕, with `aria-label`), no emoji as icons.

## Tests and size

- Tests find elements by role, label and text, never by class or test id for styling.
- After adding a dependency, check `vite build`: main JS stays within 200 kB gzip (facility-0062). Note the CSS gzip size in the PR.
