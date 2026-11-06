# Automatic Data Retention Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Implement the owner's approved complete retention policy as an automatic server job.

**Architecture:** One scoped retention runner owns GPS clearing, transactional annual/monthly cleanup and anonymous point summaries. A BackgroundService runs it on startup and nightly using the injected clock, with cancellation/error retry. Reuse current EF, shift/round and export rules without new packages or frontend/API surfaces.

**Tech Stack:** .NET 10, EF Core 10 / Oracle MySQL, xUnit SQLite integration, existing TimeProvider.

**Spec:** docs/superpowers/specs/2026-11-03-data-retention-design.md

## Global Constraints

- Defaults: GPS 90 days, records 1 calendar year; archive only whole shift-date months older than twelve completed months; nightly 02:00 Bangkok; batch 1000; retry 15 minutes; expired refresh grace 30 days.
- No master/sign deletion, personal fields in summaries, real database writes, publication, new dependencies or APIs.
- The approved spec owns exact cutoff/math/retained-data requirements. Record technical rulings in .superpowers/sdd/2026-11-03-data-retention/progress.md.

### Task 1: Retention engine, host integration and regression suite

This is one integrated deliverable: summaries must exist atomically before annual deletion and the scheduler must run that tested engine. Keep code separated by responsibility.

**Files:**
- Create Application/Retention settings/schedule and summary calculation rules as needed.
- Create Domain/Entities/PointMonthlySummary.cs.
- Modify Infrastructure/Persistence/AppDbContext.cs; add EF migration + snapshot.
- Create Infrastructure/Persistence/Retention files for the scoped runner and helper queries.
- Create Api/Retention worker; register validated settings/scoped runner/hosted service in Program.cs and defaults in appsettings.json.
- Create backend unit and API integration retention tests, using the real SQLite relational database and a deterministic clock. Add optional MySqlFact test for real provider validation.

**Interfaces:** Expose a scoped runner RunAsync(CancellationToken) (clock/settings injected) and an aggregate result containing counts only. Worker obtains a new scope for each run. Tests invoke the real runner; no fake success return may stand in for deletion or rollback.

- [x] Write failing integration tests for clearing expired GPS on all four record types while preserving newer/boundary/setup/derived data. Run the filter and record the red result.
- [x] Implement validated settings and bounded SQL GPS clearing. Verify green.
- [x] Write failing tests for anonymous monthly counts, month/year/leap boundaries, type-aware dependent deletion, current-data protection, rollback and re-run idempotence. Supply actual seed entities with correct foreign keys and copied windows; assert source state and stored summary values.
- [x] Implement summary entity/mapping/migration and transactional archive/annual cleanup. Verify green; failure injection must prove no source deletion without committed summary.
- [x] Test startup run, next Bangkok night, cancellation and 15-minute failure retry. Implement the worker and register it only outside Testing (direct worker tests cover lifecycle).
- [x] Run backend solution tests and migration/schema/provider checks. Self-review retention boundaries and retry/transaction paths. Do not launch the owner's API or read private credentials.
- [x] Produce a concise implementation report including red/green commands, validation counts, changed files, limitations and any rulings. Leave changes uncommitted for the owner; coordinator performs independent review and docs integration.

### Task 2: Documentation and independent whole-change review

**Files:** docs/handoff.md, docs/handover.md, docs/business-overview.md, docs/architecture.md, docs/code-map.md, docs/adr/facility-0055-*.md, docs/adr/README.md, README.md where status changes.

- [x] Reconcile status from not built to implemented locally and awaiting publication; explain configuration/nightly catch-up, exact monthly archive boundary, inherited missing-round semantics, and retained Excel-file ownership question.
- [x] Dispatch independent code review using sp-requesting-code-review / scrutinize-dispatch; address data-integrity and spec issues with focused regression tests.
- [x] Verify final tests, git diff --check and affected links. Update handoff with actual results and outstanding real-host checks. No commit/push/merge without fresh authorization for this implementation.

## Completion evidence (2026-11-03)

Implemented locally without publication. Initial whole-change review found one orphan-history race; bounded typed sweeps and red/green regressions fixed it, and scoped rereview approved. Final full solution against isolated MySQL:708 API+357 unit passed,0 skips. Temporary test instance shut down and datadirs removed. Actual host schedule/log verification belongs to deployment. See docs/handoff.md and ignored .run/retention-mysql/final-backend-tests.log.
