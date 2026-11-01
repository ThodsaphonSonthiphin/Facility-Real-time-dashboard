# Observable end-to-end tests

The suite drives the real React app, ASP.NET Core API, SignalR and MySQL. Chromium opens visibly on macOS with350ms between browser operations. API responses are not mocked; the existing loading/layout checks delay selected real requests and then forward them.

From the repository:

```bash
npm --prefix frontend run e2e -- --dataset large
```

For a quick visible login/dashboard check:

```bash
npm --prefix frontend run e2e -- --project=desktop --grep 'an Admin lands on D1'
```

Prerequisites: Python3, .NET10 SDK, Node/npm, running local MySQL and a desktop login session. `npm --prefix frontend ci` installs the already-declared Playwright dependency. The runner installs/reuses its Chromium cache. Root MySQL access is used only to create and remove this run's database/account. If needed, supply the local root password as `E2E_MYSQL_PASSWORD` in your shell; do not put it in a command committed to the repository. The API/factory receive a random database-specific account with access only to this run's database.

| Profile | Added Buildings | Added Areas | Points per added Area | Historical dates |
| --- | ---: | ---: | ---: | ---: |
| baseline (default) |0|0|0|0|
| small |2|8|2|7|
| medium |8|80|3|14|
| large |30|600|2|14|

All generated profiles also add one inactive Building and12 unassigned accounts (six active, six inactive). Original Development fixtures remain intact. Three quarters of generated Areas work both shifts; every Building has a Supervisor for each shift and every Area has at most one active Cleaner per supported shift. The large profile has1,127 accounts with the current Development fixtures. Actual activity counts depend on completed shifts and are recorded in `dataset.json`.

The factory uses actual EF entities, existing migrations and `RoundWindowRules`. It creates synthetic attendance, normal/Issue submissions, Passed/Rework inspections, uninspected submissions, GPS flags, absences, missing Shift-Out events, historical covers and alternating setup audit snapshots. It keeps current shifts empty for live browser workflows. No real identity or phone is introduced. Factory accounts reuse the original Day Cleaner's synthetic phone hash; original credentials remain centralized in `frontend/e2e/support/seed.ts` and `DbInitializer.cs`.

Reproducibility: codes, assignments, distributions and offsets are deterministic for a profile/date. IDs are generated on a fresh database and need not be byte-identical across MySQL versions. `E2E_SEED_DATE=YYYY-MM-DD` fixes the anchor date (not future). `E2E_SEED_NOW=<UTC ISO timestamp>` is a test-only factory clock for overnight-boundary checks; it never changes the API clock. It must not be in the future. The runner invokes the factory twice, requires an unchanged ownership/profile/date identity, and compares actual counts. Report tests select the historical fixture range and compare inspection totals to independently queried database counts. Summary tests use the manifest's `summaryFromDate`/`summaryToDate`, ending before the active shift date, including before07:00 Bangkok when yesterday's Night shift remains active. History/export retain the complete historical date range.

Every run creates `facility_e2e_<UTC timestamp>_<random suffix>` with CREATE, without adopting an existing name. A private manifest and matching database marker prove ownership before cleanup. The MySQL user is marked owned only after successful CREATE USER. An existing user collision cannot cause it to be removed. The run stops only the API process it launched, then removes its synthetic database and owned user, whether the tests pass or fail. The separate published app and screenshots remain under `.run/e2e/<run>/`. The owner's shared `wwwroot`, local review API and other databases are preserved. This replaces the old fixed-database lifecycle in facility-0094.

An interrupted run normally cleans up on Ctrl+C/SIGTERM. SIGKILL/reboot cannot run cleanup. Once any surviving run process has stopped, explicitly clean the exact owned directory:

Cleanup validates the marker, removes the successfully created user first, then removes the database. If either DROP fails before database deletion, the marker remains available for retry; boundary tests cover both failures.

```bash
npm --prefix frontend run e2e:clean -- /absolute/path/to/repo/.run/e2e/20261031155209_5743fe0c
```

Use the directory printed by your run, not the example. Cleanup refuses a missing/mismatched marker, external directory or legacy `facility_e2e_test`. It does not kill a process from an old PID file. If ownership validation fails, investigate rather than deleting another database.

Each run retains:

- `report.md`: counts, case outcomes, measured API durations, workbook row counts and artifact links.
- `html/index.html` and `results.json`: Playwright's detailed and machine-readable reports.
- `test-results/`: screenshots for passes/failures, failure context, traces, and downloaded workbooks.
- `dataset.json`, `dataset-repeat.json`: actual counts, semantic distributions, time/relationship checks and repeat-seed evidence.
- `api.log`, `steps.log`, `run.json`, `ownership.json`: environment/build/cleanup evidence (`steps.log` is available for runs made with the final runner).

To open an artifact without rerunning writes:

```bash
npm --prefix frontend exec -- playwright show-report /absolute/path/to/run/html
npm --prefix frontend exec -- playwright show-trace /absolute/path/to/run/test-results/case/trace.zip
```

The report and traces contain synthetic credentials/session tokens from browser traffic; `.run/` is ignored. Review artifacts before sharing them.

Options:

- `E2E_PORT=5102`: use another free loopback port (default5101). Frontend and API share this origin; no Vite server is required.
- `E2E_SLOW_MO=500`: change the visible pace. Default350.
- `E2E_HEADLESS=1 E2E_SLOW_MO=0`: explicitly opt into a faster unattended run. Normal runs remain headed.
- Playwright arguments pass through: `--project=phone`, `--grep`, `--headed`, etc. Do not set `--workers` above1: some cases share seeded state.

Retries are disabled intentionally. Retrying a mutating case without reseeding can duplicate writes and hide failures; rerun the command to get a fresh isolated environment. Direct Playwright execution requires the live runner's ownership environment and loopback origin. Never point the tests at a real-data deployment.

Safety regression checks and static checks:

```bash
python3 -m unittest discover -s scripts -p test_e2e_runner.py
npm --prefix frontend run lint
npm --prefix frontend exec -- tsc -b frontend/tsconfig.json
```

Coverage includes authentication/session restoration, role restrictions, Building/Area/point/account CRUD and validation, dialogs/Drawers/typed confirmation, large lists/search/filter/default ordering, history cursor paging, attendance paging/correction/audit, covers, inspection summaries, XLSX download and multi-role rework/live update workflows. Account and Area lists filter in the client; they have no pagination or user-selectable sort control. Attendance pages25 rows in the client; history uses100-row API cursors. Small-profile attendance pagination is explicitly skipped.

Limits: this is a single-worker functional volume test, not a concurrent load test. Timings include trace/browser overhead and some UI timings include slow motion. Safari, real GPS accuracy, physical QR scanning/printing, offline recovery, throttling/token-reuse security, every account credential-change variant and every cover/alert/round boundary remain outside the executed case set. The original live-push case skips19:00–20:00 Bangkok before its first Night round; the generated rework case skips the last two minutes before a shift change to avoid crossing its round boundary mid-workflow. Failed application assertions remain failed; production changes require the owner's approval.

Playwright configuration reference: [headed launch, slow motion and recording options](https://playwright.dev/docs/test-use-options).
