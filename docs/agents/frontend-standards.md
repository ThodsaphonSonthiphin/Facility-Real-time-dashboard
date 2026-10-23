# Frontend Standards

The rules `code-review` checks on any frontend change (its Standards axis). The look itself is described in `design-system/facility/MASTER.md`; the decisions behind both are facility-0086, 0087 and 0088.

## Styling (Tailwind 4, facility-0086)

- Style with Tailwind utility classes. New per-page `.css` files are not allowed; an existing one shrinks or is deleted when its page moves.
- Colours, sizes and radii come from the `@theme` tokens in `frontend/src/index.css`: write `bg-primary`, `text-muted`, `rounded` and so on.
  - Never `bg-[var(--color-…)]` for a registered token.
  - Never a raw Tailwind palette class (`bg-blue-600`) or a raw hex in a component.
  - Arbitrary values (`w-[37px]`) only with a comment saying why no token fits.
- Class names are complete strings in the source. Never build one (`` `bg-${x}` ``); map a variant to a full class (`{ warn: 'bg-warn-bg', … }`).
- Shared pieces (button, field, note, chip, table, bar) are React components in `frontend/src/shared/components` (listed in MASTER.md). No `@apply`. No UI library.
- A moved page sets its body text size once on its root: `text-base` on a phone page, `text-sm` on an Admin page. Components inherit it; the shell sets none, so pages not moved yet keep theirs.
- Two classes on one element must never set the same property (`underline` and `no-underline`, `px-0` and `px-4`): which one wins depends on Tailwind's output order.
- No `clsx` or `tailwind-merge` until a real class clash needs one.
- Print rules use the `print:` variant; only `@page` stays as plain CSS in `index.css`.
- Until the last page moves (#82), Tailwind's preflight stays off, and the old look's variables are named `--old-*`. A moved page uses only the `@theme` tokens; never use an `--old-*` variable in new code.
- The old plain CSS is not in a cascade layer, so it beats any utility. A moved page must drop its old classes entirely: mixing an old class and utilities on one element lets the old rule win.

## Look and accessibility (facility-0087)

- Text contrast at least 4.5:1; icons and control borders at least 3:1.
- Font sizes: phone body at least 17 px, Admin body at least 14 px, nothing below 13 px.
- Targets: phone at least 48 px high, primary phone actions 56 px and full width; Admin at least 36 px.
- A status is always shown with its words (`StatusChip`), never by colour alone.
- Blue means "can be pressed". Do not use primary blue for status or decoration.
- Every control has a visible focus style and an accessible name; labels are visible, not placeholder-only.
- Loading, error, empty and disabled states are shown (see MASTER.md).
- Device font only (facility-0072); never load a web font.

## Icons (facility-0088)

- `lucide-react`, one named import per icon.
- An icon sits beside text and has `aria-hidden="true"`. No icon-only buttons (one exception, the owner's: the Toast's ✕, with `aria-label`), no emoji as icons.

## Tests and size

- Tests find elements by role, label and text, never by class or test id for styling.
- After adding a dependency, check `vite build`: main JS stays within 200 kB gzip (facility-0062). Note the CSS gzip size in the PR.
