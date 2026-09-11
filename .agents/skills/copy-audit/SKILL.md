---
name: copy-audit
description: 'Find every copy of a plugin or skill on this machine and prove which ones are stale. Use when the user asks to update every place / everywhere on this PC, check the plugin cache, confirm an edit actually went live, verify a skill is deployed, find drifted or vendored copies, or asks why a skill still behaves like the old version. Also use before shipping a plugin change that other repos vendor. It reports and never writes.'
effort: max
---

# Copy Audit

A plugin exists in more places than anyone tracks: the repo that publishes it,
the runtime cache, copies vendored into other repos, and per-agent skill
stores. They drift silently, and every signal that looks like proof is not one.

Run the audit, read it, and repair what it names. Never repair by guessing.

## Step 1 - run the audit

Run the checker with the plugin's name. Redirect to a file and check the bare
command's exit code; a pipe reports the last command's status, which turns a
failed run into an apparent success.

Use the form for the shell you are actually in - the exit-code idioms are not
interchangeable, and reading the wrong one makes a refusal that measured
nothing look like a clean report.

**PowerShell** - `$?` is a boolean here (`True`/`False`, never `2`), and
redirecting a native command's stderr sets it to `$false` even on exit 0, so
read `$LASTEXITCODE` and do not merge the streams:

    python ${CLAUDE_SKILL_DIR}/scripts/check_plugin_copies.py --plugin NAME > audit.txt
    Write-Output "EXIT=$LASTEXITCODE"

**bash / POSIX sh:**

    python ${CLAUDE_SKILL_DIR}/scripts/check_plugin_copies.py --plugin NAME > audit.txt 2>&1
    echo "EXIT=$?"

Exit 2 means it refused to run. That is a result, not a failure: read the
blocker it names, clear it, and run again. Exit 1 comes only with `--strict`,
and means the run produced findings.

## Step 2 - if it refuses, clear the blocker first

The audit refuses when the source is not a trustworthy baseline - uncommitted
changes under the plugin, or a branch holding commits the checked-out branch
lacks. Grading copies against a stale source reports them current when they
merely match an obsolete source, which is worse than no report.

Commit or merge what it names, then re-run. Use `--allow-dirty-source` only
when you have already accepted the baseline; the report is then stamped
ungraded and must not be quoted as proof of anything.

## Step 3 - read the verdicts

There are exactly three:

- `IN SYNC` - the CR-normalized bytes match. Nothing to do.
- `STALE` - a real copy of ours (provenance confirmed by content, not just by
  name), and behind. Repair it.
- `UNRELATED` - the same name, but a different lineage - provenance was never
  confirmed. Not ours. Leave it alone and do not report it as drift.

Each row shows the line overlap the call was decided on, so a borderline
verdict is visible rather than hidden inside a number. A verdict needs enough
lines to mean anything: below ten distinct non-blank lines on the smaller
side the copy grades `UNRELATED` whatever the overlap says, because a stub
matches `---` and its own `name:` by construction.

**What was compared:** only `skills/<name>/SKILL.md`. A copy whose
`references/*.md` or `scripts/*` drifted still reads `IN SYNC`, and in this
repo `references/` is load-bearing. If your question is about a reference
file, this audit does not answer it - diff those files directly.

## Step 4 - rows that are NOT graded, and rows that are findings

A row either carries a verdict or says why it was not graded. Every
not-graded row prints its reason, and the summary prints one count per
distinct reason - so an excluded copy is visibly excluded, never a missing
row. The two reasons today:

- **A cache version directory other than the graded one** - only one cache
  version is graded. That is the version the install manifest claims when its
  directory exists; when it does not, the highest version directory actually
  present is graded instead, because that is what loads.
- **A dated backup snapshot** under the Claude home's `backups` directory. A
  dated backup is supposed to be behind; grading it would tell you to "edit
  and commit" a directory that usually is not even a repo.

Two report lines are findings in their own right, and `--strict` exits 1 on
them even when nothing is stale:

- `FINDING: the install manifest claims version X at ... but that directory
  does NOT exist` - the headline failure this tool exists to expose.
- `FINDING: the install manifest records no entry for <plugin>@<marketplace>`
  - nothing claims the plugin is installed at all.

`N directories could not be read` is also printed when the scan hit a
directory it could not enter. A copy inside one of those is missing from the
report, not absent from the machine.

## Step 5 - repair, per role

Every `STALE` row carries its own repair. The role decides it:

- **cache** - never hand-patch it. The runtime maintains this snapshot. Edit
  the source and let the next session refresh it. A patched cache reports
  success while the real source stays old, which is the single most expensive
  mistake in this area.
- **vendored** - what to do depends on whether a `.git` exists above the copy:
  - if one does, the copy is git-tracked by that project: edit it in that
    repo and commit there. Copying a file in would leave their tree dirty.
  - if none does, there is no repo behind it to keep in sync with: edit it
    in place.
- **agent-store** - both stores an `npx skills` install writes: the central
  store under the agents home, and the per-agent copy under the Claude home's
  `skills` directory. Reinstall for the agents that read the store. A skills
  `update` short-circuits on the source hash without checking the copy, so an
  update alone never repairs a drifted copy - and editing the per-agent copy
  in place leaves the central store drifted and is clobbered by the next
  install.
- **worktree** - another branch's checkout. Merge or rebase that branch;
  do not edit its files to match.

## Step 6 - prove it landed

Re-run the audit. Do not accept any of these as proof instead:

- the install manifest naming the new version - it can name a version, an
  install path and a commit while the directory was never created
- "I restarted" - that says nothing about a directory outside the load path
- two version numbers matching - that proves the numbers match, not the bytes

The only proof is the hash, which is what re-running takes.

## What this skill will not do

It does not write to any copy. The correct action differs per role and two of
the roles must never be written to at all, so the audit reports and a person
repairs.
