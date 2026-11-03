# Handoff: current status and next steps

Updated 2026-11-03 (Asia/Bangkok). Read this first every session and update it after every task (owner's rule). This repository is public; keep secrets and real personnel data outside it.

## Current task: automatic data retention (2026-11-03, implemented locally)

Branch `codex/automatic-data-retention`, based on merged PR #146 (`577a1c6`). The owner explicitly requested retention now and selected both 90-day GPS and annual deletion/monthly summaries. Code and documentation are complete. The owner authorized **commit and push** of this retention branch on 2026-11-03; integration into master has not been requested. Confirm the exact delivery commit and remote tracking with `git log -1 --oneline` and `git status --short --branch`.

- Implementation: validated `Retention` config, scoped RetentionRunner and background worker after ApplicationStarted, immediate catch-up then 02:00 Bangkok daily, 15-minute failure retry. GPS SQL batches clear only raw coordinates/accuracy on all four operational tables, preserving derived flags and sign setup coordinates. Annual attendance/blocked scans and their typed dependent history are removed; expired tokens have a 30-day grace; old covers remain while retained sources reference them.
- Monthly archive: summary-only migration `20261103080826_PointMonthlySummaries`, 15 tables / 6 migrations total. Per-point/month anonymous counts, marker+source deletion in one serializable transaction, safe retry/rollback, recently timestamped old-shift records deferred. Whole-month semantics follow the original design: November2026 archives October2025 and earlier, preserving twelve completed months plus current month. Missing-round counts inherit current Excel schedule semantics; historical copied submitted windows remain counted.
- Independent review caught one orphan-history race: a late Admin alert-review write can land after its source is deleted. Fixed with a bounded typed orphan sweep on every run, including source-existence rechecks in DELETE. Red/green late-insert and select→delete race regressions prove the fix. Scoped re-review approved: no Critical/Important/Minor findings remain.
- **Final validation:** full backend on an isolated own MySQL instance: **708 API +357 unit passed, 0 skipped** (`.run/retention-mysql/final-backend-tests.log`). Covers real migration/model/provider/bulk updates, monthly archive and orphan cleanup. Focused red/green tests cover all four GPS tables, exact cutoffs, leap/year/month boundaries, anonymity/counts, current-data protection, idempotence, rollback and worker startup/schedule/retry/cancellation. Frontend unchanged; no browser E2E rerun for this backend maintenance feature. Documentation verification: 11 Markdown files /150 local links and anchors passed, code fences and `git diff --check` passed; 6 migrations /15 DbSets confirmed.
- Temporary MySQL used its own datadir and port33079. Tests removed their database; instance shut down, port verified closed and only this task's temporary datadirs removed. Test/server logs retained under ignored `.run/retention-mysql`. The owner's MySQL instance/data and application runtime were not touched.
- Guide: [data-retention.md](data-retention.md); [approved design](superpowers/specs/2026-11-03-data-retention-design.md); [implementation plan](superpowers/plans/2026-11-03-data-retention.md). Actual nightly behavior on the selected deployment host still needs an operating check at deploy; deterministic worker tests pass.

## Checkout and tracker

- Reconciliation baseline: `master`, `aa1963d` (PR #145 merged). Before publication, GitHub checked 2026-11-03: no open PRs; the only open issue was [decision map #1](https://github.com/ThodsaphonSonthiphin/Facility-Real-time-dashboard/issues/1).
- Documentation refresh delivery branch: `codex/reconcile-project-docs`. The owner explicitly authorized commit, push, PR and merge on 2026-11-03. Confirm the resulting checkout/merge with `git status --short --branch` and `git log -5 --oneline`; the reconciliation baseline above is not the post-merge HEAD. Before this task, only `docs/handoff.md` was modified; those notes are preserved in [handoff history](history/handoff-through-2026-11-03.md).
- ADRs run through **facility-0107**. Use the next available number when a new decision is actually made; no number is reserved for the shelved import.

## Project status

The owner counts the agreed application build as **done** (2026-11-02). Production deployment and the one-Area field pilot remain; neither is confirmed completed. The internship ends 2026-11-30.

| Scope | Current state |
|---|---|
| Cleaner/Supervisor/Admin workflows | Built and merged: attendance, submission, inspection/rework, GPS flags, scope blocks, covers, live boards, setup, accounts, summaries, Excel export and Admin history |
| Enterprise Light UI and forms | Built, merged and visually accepted by the owner (2026-10-31); Tailwind migration and earlier visual checks are complete |
| First Admin, reset-admin, safe start and deploy guide | Built and merged; actual production-host checks remain |
| Server Log through Serilog | Merged PR #135; Console only, retained by the host journal on Linux (facility-0103, 0095–0099) |
| Account deactivation, Login Lock, security headers | Merged PRs #138–#140 (facility-0104–0106) |
| Attendance date/shift race and iPhone location retry | Merged PRs #141–#142; real-iPhone retry check remains |
| Faster lost-link badge | Merged PR #144 (facility-0107): browser offline event immediately; Admin server timeout 10 s, phones 30 s, server ping 5 s |
| PDPA notice / HR discussion before production | Removed from project scope by the owner, merged PR #145; retention rules remain (facility-0055 amendment 2026-11-03) |
| Excel import | Not built; shelved by the owner 2026-11-02. Manual setup is sufficient for the pilot; facility-0056 still records account import before full rollout |
| Automatic retention deletion / monthly summaries | Implemented and verified locally on codex/automatic-data-retention (facility-0055); commit/push authorized on the feature branch; master integration and deploy-host check remain |

## Remaining work and checks

1. **Deploy:** IT supplies the server and fixed public HTTPS domain. Server OS is not yet selected. [handover.md](handover.md) covers Linux/systemd/Caddy; a Windows choice needs an equivalent operating guide. At deployment verify forwarded client IP in a `Login failed ... from <client IP>` journal line, HTTPS security headers including HSTS through the real domain, real Serilog/journal output, and `scripts/facility.test.sh` on Linux.
2. **Pilot:** one Area, one check-in sign and at least one cleaning point, its Cleaners and Supervisor (facility-0043). Admin enters buildings/Areas/points/rounds/accounts, prints from the fixed HTTPS origin, installs signs and captures each sign's location. Verify actual GPS/radius accuracy, four attendance events and submit → inspect → rework → pass at the site.
3. **Device/site checks:** the new location denied → allowed retry still needs a real iPhone. During the pilot verify real signal loss, phone camera reading the installed QR and signs printed from the fixed HTTPS origin. Earlier UI/sign visual acceptance is complete (including the owner's paper-print acceptance recorded 2026-10-27); this does not reopen those completed checks. Browser-engine E2E does not prove behavior at the pilot site.
4. **Retention delivery/operation:** code is complete and verified (see current task above); commit/push to the feature branch is authorized, with master integration still pending. At deployment apply the summary migration and verify the startup/nightly retention log. Exported Excel ownership/deletion remains undecided and outside automatic database retention.
5. **Import, only when the owner resumes it:** private spreadsheets were inspected read-only on 2026-11-02. B1's six Areas contain 19 points expressed as room-type counts; the round-time sheet is for B2. Open questions: B1 round source, split time windows across breaks, and storing square metres. The owner requested hyphenated Area codes, but current `SignCodes` accepts only A–Z/0–9 (2–20 characters); resolve this explicitly in a new ADR before code. No import, schema or code-format change is approved by this documentation task. Detailed findings remain in the history's import section.

The owner's WD presentation preparation is still advice only: no deck or agenda approved; audience and duration unknown. Rehearsal guidance must distinguish built features from unmeasured field results. See the history for the supplied rehearsal prompt context.

## Latest recorded validation (not rerun by this documentation task)

The following is the **pre-retention baseline** from facility-0107 on 2026-11-03. Current retention validation is recorded in the current-task section above:

| Check | Recorded result |
|---|---|
| Frontend | 546 tests passed; lint and TypeScript passed |
| Backend | 352 unit + 686 API tests passed, 5 skipped |
| Chromium, large, headed, 350 ms | 36 passed (`.run/e2e/20261103032049_2644e513`) |
| WebKit, large, headed, 350 ms | 34 passed, 2 skipped (`.run/e2e/20261103032521_a71d78d8`) |
| Firefox, large, headed, 350 ms | 33 passed, 3 skipped (`.run/e2e/20261103032944_0bd4136c`) |

The badge was observed immediately after emulated offline, data caught up in 0.3–1.4 s after reconnect, and server silence produced the badge in 5.4–6.8 s. These are recorded functional observations, not concurrent load or field measurements. [E2E guide](testing/e2e.md) explains coverage, skips and ownership/cleanup.

Security assessment 2026-11-02 originally reported 2 Medium, 3 Low, 2 Info, no Critical/High. F1–F4 and F7 were fixed in #138–#140; F5 (development dependency advisories) and F6 (Development-only Info) were not scheduled. The detailed security report remains outside this public repository. This is not a claim of a fresh assessment.

## Bring the app back up locally

At the earlier documentation baseline, no listeners were found on 5001, 5173 or 5101. The retention task started only its own isolated test MySQL on33079 and stopped/removed it after validation. No owner application API, tunnel or browser was started. Do not assume an old trial runtime or build still exists.

The owner clarified 2026-11-02 that **`facility_dashboard` is their own development database and may be used**. The older blanket prohibition was withdrawn. Confirm the selected database before any command that migrates or writes; never reset it as routine setup. Keep existing private connection/JWT settings and inspect only redacted targets.

For the existing local Development configuration, from the repo root:

```bash
# Terminal 1: auto-migrates the selected database and adds synthetic Development fixtures
dotnet run --project backend/src/FacilityRealtime.Api --launch-profile http --urls http://localhost:5001
# Terminal 2
npm --prefix frontend ci
npm --prefix frontend run dev -- --host localhost --port 5173 --strictPort
```

Open `http://localhost:5173/login/admin`. `http` is the launch-profile name in `Properties/launchSettings.json`; the explicit URL overrides its 5225 default to match Vite. Private user-secrets override matching Development JSON settings. Required connection string and JWT signing key stay outside git. See [README](../README.md) for initial setup and synthetic accounts.

For a shareable phone trial with synthetic data, use [handover §2.9](handover.md#29-ทดลองบนเครื่องตัวเองก่อน-ไม่มี-server): build the frontend, use a dedicated trial database and private env file with `scripts/facility.sh`, then start `cloudflared tunnel --url http://localhost:5001` in another Terminal. A Quick Tunnel gets a new URL each time; permanent pilot signs require a fixed HTTPS domain. Plain LAN HTTP cannot read phone GPS.

For automated browser verification, the runner builds and starts its own isolated API/database:

```bash
npm --prefix frontend run e2e -- --dataset large
# Optional: E2E_BROWSER=webkit or E2E_BROWSER=firefox before the command
```

Do not reuse old fixed-name E2E database instructions. Explicit cleanup must name one owned `.run/e2e/<run>` directory; see [testing/e2e.md](testing/e2e.md).

## Owner rules and documentation map

- Reply in Thai, short and direct. Use the popup tool for questions when needed.
- Obtain the owner's approval before merging PRs or another contributor's work. The owner approved commit, push, PR and merge for this documentation refresh on 2026-11-03.
- Real MySQL tests use isolated throwaway databases, and the E2E runner owns its unique databases/users. The owner's development database is not disposable.
- If root authentication is needed, have the owner enter the password locally; never record or print it in chat, tracked files or memory.
- ADR decisions and amendments take precedence over historical plans and wireframes. This task reconciles existing decisions; it makes no new product decisions.
- [Business overview](business-overview.md): Thai summary for PMs and project status.
- [Architecture](architecture.md) and [code map](code-map.md): actual connections, files and request flows.
- [ADR reading guide](adr/README.md) and [CONTEXT.md](../CONTEXT.md): current decisions and domain vocabulary.
- [Handover](handover.md): deploy and operate; [E2E guide](testing/e2e.md): run browser checks.
- [Historical handoff](history/handoff-through-2026-11-03.md): old task/validation logs, import findings and advice-only conversations. Historical pending lists are not the current backlog.

## This task: documentation reconciliation (2026-11-03)

Read the handoff, domain docs, relevant ADRs and current source; verified GitHub issue/PR state and local checkout. Updated current guides, glossary and amendment pointers; preserved the full earlier handoff as history. Validation: 24 Markdown files checked, 174 local links/anchors and code fences passed; referenced filenames and stale claims checked; archive preservation verified; `git diff --check` passed. Latest three-engine counts were cross-checked against the retained local reports. The initial documentation reconciliation did not rerun application tests or change runtime/database state. Publication uses the repository's pre-commit hook (frontend lint, TypeScript and tests); its outcome is recorded in the PR. No application source or database changes are included.
