# Observable end-to-end tests

The suite drives the real React app, ASP.NET Core API, SignalR and MySQL. Chromium opens visibly on macOS with 350 ms between browser operations. API responses are not mocked; the existing loading/layout checks delay selected real requests and then forward them.

From the repository:

```bash
npm --prefix frontend run e2e -- --dataset large
```

For a quick visible login/dashboard check:

```bash
npm --prefix frontend run e2e -- --project=desktop --grep 'an Admin lands on D1'
```

Prerequisites: Python 3, .NET 10 SDK, Node/npm, running local MySQL and a desktop login session. `npm --prefix frontend ci` installs the already-declared Playwright dependency. The runner installs/reuses its Chromium cache. Root MySQL access is used only to create and remove this run's database/account. If needed, supply the local root password as `E2E_MYSQL_PASSWORD` in your shell; do not put it in a command committed to the repository. The API/factory receive a random database-specific account with access only to this run's database.

| Profile | Added Buildings | Added Areas | Points per added Area | Historical dates |
| --- | ---: | ---: | ---: | ---: |
| baseline (default) | 0 | 0 | 0 | 0 |
| small | 2 | 8 | 2 | 7 |
| medium | 8 | 80 | 3 | 14 |
| large | 30 | 600 | 2 | 14 |

All generated profiles also add one inactive Building and 12 unassigned accounts (six active, six inactive). Original Development fixtures remain intact. Three quarters of generated Areas work both shifts; every generated regular Building has a Supervisor for each shift and every regular Area has at most one active Cleaner per supported shift. Actual activity counts depend on completed shifts and are recorded in `dataset.json`.

The factory uses actual EF entities, existing migrations and `RoundWindowRules`. It creates synthetic attendance, normal/Issue submissions, Passed/Rework inspections, uninspected submissions, GPS flags, absences, missing Shift-Out events, historical covers and alternating setup audit snapshots. `OverviewSeed` replaces only the runner-owned initial Development target baseline once with a deterministic baseline for the fixture period and exports `overview` manifest fields (`date`, `shift`, `buildingId`, `firstDate`, `lastDate`, `emptyDate`, `preBaselineDate`, `entered`, `submitted`, `inspected`, `enteredAreas`) verified independently of the overview API; `frontend/e2e/desktop/overview.spec.ts` exercises paging, drill-through, read-only detail, filter-race preservation, retry and complete-empty vs pre-baseline states on the `medium` dataset. The regular roster's current shifts start empty for live browser workflows. Factory accounts reuse the original Day Cleaner's synthetic phone hash. Original credentials remain centralized in `frontend/e2e/support/seed.ts` and `DbInitializer.cs`.

The large profile also adds three scheduling buildings, five Areas and seven staff accounts, bringing the total to 1,134 accounts with the current Development fixtures. It enables scheduling on the owned API with a random private per-run signing key. The scheduling scenario records a synthetic lifecycle decision from before the actual slot opened and gives its own leave and transfer Cleaners an initial ShiftIn. Its timestamps relate to the actual server slot, while the optional historical factory clock still applies to regular history. The owned manifest records the selected slot and validates the two seed passes agree. [Scheduling scenarios and evidence](scheduling.md)

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

- `E2E_PORT=5102`: use another free loopback port (default 5101). Frontend and API share this origin; no Vite server is required. A run may start right after another: the port check ignores the previous run's TIME_WAIT, as Kestrel does, and still refuses a port another process listens on.
- `E2E_SLOW_MO=500`: change the visible pace. Default 350.
- `E2E_BROWSER=webkit` or `firefox`: the engine of the run (default `chromium`). One engine per run, each on its own fresh database: the mutating cases cannot run twice on one database. WebKit is Safari's engine, with an iPhone user agent on the phone project; it is not iPhone Safari itself. Firefox has no mobile emulation, so its phone project is a narrow touch window. The two layout-shift cases run on Chromium only (the API they measure with is Chromium's).
- `E2E_HEADLESS=1 E2E_SLOW_MO=0`: explicitly opt into a faster unattended run. Normal runs remain headed.
- Playwright arguments pass through: `--project=phone`, `--grep`, `--headed`, etc. Do not set `--workers` above 1: some cases share seeded state.

Retries are disabled intentionally. Retrying a mutating case without reseeding can duplicate writes and hide failures; rerun the command to get a fresh isolated environment. Direct Playwright execution requires the live runner's ownership environment and loopback origin. Never point the tests at a real-data deployment.

Safety regression checks and static checks:

```bash
python3 -m unittest discover -s scripts -p test_e2e_runner.py
npm --prefix frontend run lint
npm --prefix frontend exec -- tsc -b frontend/tsconfig.json
```

Coverage includes authentication/session restoration, role restrictions, Building/Area/point/account CRUD and validation, dialogs/Drawers/typed confirmation, large lists/search/filter/default ordering, history cursor paging, attendance paging/correction/audit, covers, inspection summaries, XLSX download, multi-role rework/live update workflows, combined overview (`overview.spec.ts`), dated scheduling and 6-stage shift-aware attendance (`scheduling.spec.ts`, `schedule-attendance.spec.ts`), WCAG axe checks (`accessibility.spec.ts`), visual regression baselines (`visual-regression.spec.ts`), performance/load checks (`performance.spec.ts`), the login lock (facility-0104: five wrong logins exhaust that name/IP pair, another name on that IP can still log in while below the 20-failure IP cap) and the security headers (facility-0106). Every case fails on a CSP violation in any of its pages, as on an uncaught page error or HTTP 5xx. Account and Area lists filter in the client; they have no pagination or user-selectable sort control. Attendance pages 25 rows in the client; history uses 100-row API cursors. Small-profile attendance pagination is explicitly skipped.

Conditions (2026-11-02, updated 2026-11-23): GPS far from the sign (recorded and flagged, the Admin sees the distance) and location refused then allowed, on generated Areas 5 and 7; a submission sent without signal (the page keeps the choice, ส่งใหม่ sends it once) on Area 6; the Admin attendance page losing the network (`ขาดการเชื่อมต่อ` offline-only badge, then catch-up when back online) on Area 3; the print page (signs against the Area's API answer, QR read back with the browser's BarcodeDetector where the engine has one, A4 sheet size and nothing cut off in print media, the print call counted instead of opening the system dialog). Each case owns its generated Area (`factoryArea` in `support/seed.ts`).

Engine differences found (2026-11-02 probes, kept in the tests):

- WebKit reports CSP violations that are not the app's: a source-less `<style>` Playwright adds (a blank page under the same CSP shows it), and `style-src-attr` for its own drawing of any `<select>` (a plain HTML select shows it). Only those two are skipped, on WebKit only; an injected inline script still fails on both engines.
- WebKit keeps a refused location for the life of the page; Chromium reads it again at once. Since the retry fix, "เปิดแล้ว ลองอีกครั้ง" reloads the page there, and the WebKit case expects the reload and a second เข้างาน press.
- A visible Chromium asks with its own prompt when location is merely cleared, so the case refuses it at browser level (DevTools `Browser.setPermission`).
- Offline emulation cuts HTTP and pushes in every engine. `LiveBadge` and `ConnectionBanner` read browser `online`/`offline` events directly (`useOnlineStatus`, updated in PR #160) so `"ขาดการเชื่อมต่อ"` shows immediately on all three engines when offline (within 5 s), while transient SignalR reconnects recover silently in the background.
- "An Admin page stays live on a quiet link" (facility-0107) relays the hub's WebSocket through `page.routeWebSocket`. For 25 s, no connection may drop and open again, verifying the 5 s server ping keeps the link open without reconnecting; when server messages are later dropped, the socket detects the silent link after the 10 s `ADMIN_HUB_SERVER_TIMEOUT_MS` and reconnects silently in the background.
- A mouse click does not focus a button on WebKit, so the cover case opens its confirmation with the keyboard there.
- Playwright's Firefox (157) does not start on macOS 27 ("Could not find profile folder"), in the agent's sandbox and in a normal Terminal alike: macOS shields `~/Library/Application Support/Firefox` from it. On macOS the config gives it an empty stand-in home, `CFFIXED_USER_HOME=<run>/firefox-home`, and it starts.
- Firefox (2026-11-03 probes): with no permission set it waits on its own prompt, so a refusal cannot be made by clearing it; the `geo.prompt.testing` preference refuses, but then refuses even a granted permission, so a later allow never comes. The location-refused case is skipped on Firefox.
- The "date picked while another shift is still loading" case read the pressed shift before the first board arrived, which Firefox's slower first load showed (it picked the shift already on screen). It now waits for the first board before slowing the network.

Limits: this is a single-worker functional volume test, not a concurrent load test. Timings include trace/browser overhead and some UI timings include slow motion. iPhone Safari itself, real GPS accuracy, a phone camera reading a paper sign, a real printer, exhaustive pair/IP-throttle and token-reuse security, every account credential-change variant and every cover/alert/round boundary remain outside the executed case set. The original live-push case skips 19:00–20:00 Bangkok before its first Night round; the generated rework case skips the last two minutes before a shift change to avoid crossing its round boundary mid-workflow. Failed application assertions remain failed; production changes require the owner's approval.

Playwright configuration reference: [headed launch, slow motion and recording options](https://playwright.dev/docs/test-use-options).
