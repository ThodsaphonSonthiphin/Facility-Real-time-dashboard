# Facility Real-time Dashboard

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

### Frontend standards

Tailwind, look and accessibility rules that `code-review` checks. See `docs/agents/frontend-standards.md`; the look is in `design-system/facility/MASTER.md` (page overrides in `design-system/facility/pages/`).
