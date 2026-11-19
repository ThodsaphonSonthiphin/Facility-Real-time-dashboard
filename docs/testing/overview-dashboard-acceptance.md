# Combined Overview Dashboard Acceptance Report (Task 11)

- **Date:** 2026-11-19 (Asia/Bangkok)
- **Branch / Worktree:** `codex/combined-overview-dashboard` (`Janitorial-Management-Dashboard-overview-dashboard`, off `431bf95`)
- **Database isolation:** MySQL tests used disposable `facility_overview_test` via `FACILITY_MYSQL_TEST` (deleted in `finally`). E2E runs used unique `facility_e2e_<timestamp>_<suffix>` databases and users cleaned on exit. The owner's `facility_dashboard` database was untouched.

## Commands & Results

| Check | Command | Outcome | Notes |
|---|---|---|---|
| Targeted MySQL Overview & Read-Race | `FACILITY_MYSQL_TEST=... dotnet test backend/tests/JanitorialManagementDashboard.ApiTests --filter 'FullyQualifiedName~MySqlOverviewTests\|FullyQualifiedName~OverviewReadRaceTests'` | **7/7 passed** (0 failed, 0 skipped, `11.82s`) | Covers `20261119013534_OverviewTargetHistory` Up/Down/Up, `round_origin_id` preservation, `FOR UPDATE` serialization, intermediate rollback, same-timestamp ID sort, `RepeatableRead` snapshot consistency, retention anchor race, zero-event vs partial annual deletion, midnight Night shift, policy history, and Issue state. |
| Full Backend Suite (with MySQL) | `FACILITY_MYSQL_TEST=... dotnet test backend/JanitorialManagementDashboard.slnx --no-restore` | `UnitTests`: **605/605 passed**; `ApiTests`: **1052 passed, 19 failed, 0 skipped** (`3m 28s`) | All Overview, Retention, and MySQL-gated cases passed. The 19 `ApiTests` failures are pre-existing date-sensitive `ReliefLoanTests`/`ReliefAssignmentTests` cases (`ReliefApi.Wednesday` `2026-11-18` vs `2026-11-19` UTC `CleanerProfilePeriod` start) on this branch. |
| Frontend Unit Tests | `NODE_ENV=test npm --prefix frontend test` | **77/77 files, 753/753 passed** | Re-run cleanly after initial CPU contention timeout on `ScheduleConflict.test.tsx`. |
| Frontend Lint & Build | `npm --prefix frontend run lint && npm --prefix frontend run build` | **0 lint errors**; build passed (`306ms`) | `dist/assets/index-Dta6Mj1R.js` **103.70 kB gzip** (<= 200 kB budget), `dist/assets/index-CX2lt8BI.css` **9.28 kB gzip**. |
| E2E Runner Safety Tests | `python3 -m unittest discover -s scripts -p test_e2e_runner.py` | **12/12 passed** (`0.014s`) | Verifies DB/user ownership checks and cleanup guards. |
| E2E Overview (Medium Desktop) | `E2E_HEADLESS=1 E2E_SLOW_MO=0 npm --prefix frontend run e2e -- --dataset medium --project=desktop --grep overview` | **5/5 passed** (`11.9s`) | Evidence: `.run/e2e/20261119155657_105ca749`. Verified 5 page-2 Areas through drill-through & reload at `1280×900` and `375×812` (`0` horizontal overflow), read-only historical rounds, aborted read retry, empty vs pre-baseline distinction, and filter-race preservation. |
| E2E Full Desktop (Medium) | `E2E_HEADLESS=1 E2E_SLOW_MO=0 npm --prefix frontend run e2e -- --dataset medium --project=desktop` | **25 passed, 4 skipped, 1 failed** (`1.7m`) | Evidence: `.run/e2e/20261119155913_4bbbf9dd`. 4 skips are large-dataset scheduling tests. 1 failure is pre-existing `dashboard.spec.ts:9` (`375px` mobile nav collapse selector, already updated on `codex/cleaner-scheduling`). |
| E2E Full Phone (Baseline) | `E2E_HEADLESS=1 E2E_SLOW_MO=0 E2E_PORT=5102 npm --prefix frontend run e2e -- --dataset baseline --project=phone` | **11 passed, 6 skipped, 0 failed** (`19.1s`) | Evidence: `.run/e2e/20261119155925_5de230fc`. 6 skips require `--dataset` / scheduling fixtures. Proves ordinary phone auth/check-in/My Work/My Building paths stay unaffected. |

## Visual & Pending Owner Checks

- **Viewport & layout checks:** Captured `1280×900` and `375×812` screenshots in `.run/e2e/20261119155657_105ca749` with `sidewaysOverflow === 0`.
- **Pending real-device checks (not replaceable by headless Chromium):** Physical iPhone Safari / Android GPS field verification, real camera scanning printed QR signs, and manual screen-reader navigation.
