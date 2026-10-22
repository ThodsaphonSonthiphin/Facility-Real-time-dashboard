# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

## Before exploring, read these

- **`CONTEXT.md`** at the repo root: the project's glossary (this repo's name for `GLOSSARY.md`). Terms are written in Thai, each with an `_Avoid_:` line.
- **`docs/adr/`**: read ADRs that touch the area you're about to work in. Where an ADR conflicts with a wireframe, the ADR wins.

If any of these files don't exist, **proceed silently**. Don't flag their absence; don't suggest creating them upfront. The `/domain-modeling` skill (reached via `/grill-with-docs` and `/improve-codebase-architecture`) creates them lazily when terms or decisions actually get resolved.

## File structure

Single-context repo:

```
/
├── CONTEXT.md
├── docs/adr/
│   ├── facility-0001-scan-record-structure.md
│   └── facility-0002-scanner-authentication.md
├── backend/
└── frontend/
```

## Naming new ADRs

New ADRs go in `docs/adr/` as `facility-NNNN-<slug>.md`: the `facility-` prefix, the next number after the highest existing one, zero-padded to 4 digits (see `AGENTS.md`). Never use a bare `NNNN-<slug>.md`. When an ADR replaces or amends an older one, say so in both files.

## Use the glossary's vocabulary

When your output names a domain concept (in an issue title, a refactor proposal, a hypothesis, a test name), use the term as defined in `CONTEXT.md`. Don't drift to synonyms the glossary explicitly avoids.

If the concept you need isn't in the glossary yet, that's a signal: either you're inventing language the project doesn't use (reconsider) or there's a real gap (note it for `/domain-modeling`).

## Flag ADR conflicts

If your output contradicts an existing ADR, surface it explicitly rather than silently overriding:

> _Contradicts facility-0063 (plain CSS, per-page files), but worth reopening because…_
