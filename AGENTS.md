### Frontend standards

Tailwind, visual design, and accessibility rules that `code-review` checks.

Sources of truth:

- `docs/agents/frontend-standards.md` — frontend implementation and accessibility standards.
- `design-system/janitorial/MASTER.md` — authoritative visual design system.
- `design-system/janitorial/pages/` — page-specific design decisions.
- `frontend/src/index.css` — implemented semantic design tokens.

### AI-generated UI quality

Use `.agents/skills/unslop-ui/SKILL.md` when designing, redesigning, or auditing frontend UI.

Treat `unslop-ui` as an additional quality guardrail, not as a replacement for the established design system.

Rules:

- Avoid generic AI-generated aesthetics and unmodified framework defaults.
- Never choose colors, typography, gradients, shadows, or layouts merely because they are common AI defaults.
- Do not replace one generic AI aesthetic with another.
- Base visual decisions on user workflows, information hierarchy, accessibility, and the established design system.
- Preserve intentional brand colors and existing design decisions unless the task explicitly requires changing them.
- Use semantic design tokens and reusable components instead of arbitrary page-level styling.
- Do not introduce decorative gradients, glow effects, animations, or unnecessary cards without a functional or design-system justification.
- Do not ban Tailwind, shadcn, rounded corners, gradients, or specific colors solely because they appear in AI-generated interfaces.
- Prioritize clarity, consistency, data density, and efficient task completion over decoration.

### UI audit and verification

For UI redesigns or visual quality audits:

1. Inspect the existing design system, relevant components, and rendered pages before editing.
2. Review the applicable `unslop-ui` guidelines.
3. Run the `unslop-ui` scanner when applicable.
4. Treat scanner findings as review signals, not automatic defects.
5. Inspect actual rendered UI at desktop (1280px) and mobile (375px) widths.
6. Verify visual consistency, readable typography, contrast, spacing, responsive behavior, and accessibility.
7. Resolve justified issues without changing unrelated business logic.
8. Report the visual changes, rationale, and verification results.

When intentionally changing the visual direction:

- Update `design-system/janitorial/MASTER.md` first.
- Update relevant page-specific design documentation.
- Apply the approved direction consistently through shared tokens and components.
- Do not independently invent a new visual style for each page.

## Stage: development

Nothing here is released and nobody depends on any of it: every part is an experiment. Every decision, on every topic, follows from that:

- **No backward compatibility.** When anything changes (code, data, content, routes, config, dependencies), change it in place: update everything that uses it in the same change and delete what nothing uses. Each thing has one current way, never an old way kept beside it.
- **Build the complete version.** Solve the whole real problem, configure each tool the standard, complete way, and choose concrete values. Revisiting a choice later is cheap; a partial or temporary version has to be redone.

# Janitorial Management Dashboard

## Project Standards & Decisions

- ADR Directory: `docs/adr/`
- ADR Sequence Prefix: `facility-` (e.g. `facility-0001-<slug>.md`, zero-padded to 4 digits)
- Map slug: `facility-poc` (GitHub Issues tracker: `ThodsaphonSonthiphin/Facility-Real-time-dashboard#1`)

## Agent skills

### Session start

Read `docs/handoff.md` first, every session: it carries the current branch and PR, what is done, what the owner still owes a check on, and how to bring the test build back up (the throwaway API, database and tunnel do not survive a reboot). It is updated after every task (owner's rule).

### Issue tracker

GitHub Issues of `ThodsaphonSonthiphin/Facility-Real-time-dashboard`, via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

The five default labels: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: the glossary is `CONTEXT.md`, and ADRs are `docs/adr/facility-NNNN-<slug>.md`. See `docs/agents/domain.md`.

## UI Iconography Standards

- NEVER use emojis in application UI.
- Use `lucide-react` exclusively for interface icons (one named import per icon).
- Do not use emoji characters, Unicode pictograms, or decorative symbols as substitutes for SVG icons.
- Do not introduce additional icon libraries.
- Maintain consistent icon sizes, alignment, and stroke widths, preferring existing conventions (`size-4` beside Admin text, `size-5` beside phone text, `size-7` in `ChoiceTile`, stroke width `2`, `aria-hidden="true"`).
- Use semantic color tokens from the established design system (`frontend/src/index.css` `@theme` tokens).
- Always pair important action icons with clear, meaningful Thai text labels (no icon-only buttons, except the owner-approved `Toast` close button with `aria-label` and the D4 window pill close control).
- Use icons only when they improve usability and recognition; do not introduce unnecessary decorative icons.
- Apply these standards to all newly created or modified UI components, pages, dialogs, navigation elements, and HTML/CSS mockups.
- Preserve legitimate user-generated content that may contain emojis.

## AI Communication Standards

- Do not use emojis in responses, explanations, progress reports, code reviews, documentation, or implementation summaries.
- Use professional, concise, plain-text or Markdown formatting.
- Avoid decorative Unicode symbols in headings and lists.
