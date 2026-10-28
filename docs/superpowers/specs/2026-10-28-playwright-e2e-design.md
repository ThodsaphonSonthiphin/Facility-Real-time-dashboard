# Playwright end-to-end tests on a phone-sized browser (design)

Date: 2026-10-28. Status: design approved by the owner in chat (popups), spec waiting for the owner's review. ADR to write with the plan: `facility-0094`.

## Why

The 463 Vitest tests run on jsdom: no real layout, scrolling, geolocation or SignalR socket. So the bugs the owner found by eye in the last weeks (check-in page scroll with `100dvh`, layout shift on My Work and D1, the badge flashing on first connect) were only found by the owner on a phone. The owner wants an AI to run those checks instead of them. Chosen by the owner by popup on 2026-10-28: permanent Playwright tests, covering both the work flows and the phone look.

Ruled out: Argent (software-mansion/argent). It drives native, React Native and Electron apps; this project is a web app. Not a fit, nothing to install.

## Goal and non-goals

Goal: one command, `npm run e2e` (in `frontend/`), that builds the app, runs it on a throwaway database and checks the main flows and the phone layout in a real browser, then cleans up after itself, pass or fail.

Not in this change (YAGNI):
- screenshot diff against stored baselines (flaky, costly to keep; screenshots are saved as artefacts for a person or an AI to look at);
- a CI pipeline (the repo has no `.github`; tests run on the owner's Mac or where an agent works);
- a pre-commit hook (too slow);
- real iPhone Safari, real GPS and a real paper print (D5). These stay with the owner. Chromium emulation is not Safari: a Safari-only layout bug can pass here.

## Layout

- `@playwright/test` as a devDependency of `frontend/`; tests in `frontend/e2e/`; config in `frontend/playwright.config.ts`.
- Playwright files end in `.spec.ts`; Vitest only picks `src/**/*.test.{ts,tsx}`, so the two runners never meet without any change. ESLint and `tsc -b` (a new `tsconfig.e2e.json`) cover `e2e/` too, so the pre-commit hook checks them.
- `scripts/e2e.sh` does the whole run, written in the style of `scripts/facility.sh`; `npm run e2e` calls it. A second script `npm run e2e:clean` removes what a killed run left behind.

## How a run goes (`scripts/e2e.sh`)

1. `npm run build:api` (the build goes to the API's git-ignored `wwwroot`).
2. Make the database `facility_e2e_test` fresh (drop if it exists, create). The script refuses any other database name and never touches `facility_dashboard` (the 2026-10-27 trial run changed the developer database by mistake; see handoff).
3. `dotnet build -c Release`, then start the built `FacilityRealtime.Api.dll` from the project folder (so `wwwroot` is found) in Development (so the seed runs), on `E2E_PORT` (default 5101, not the 5001 the owner uses), with the connection string and a random `Jwt__SigningKey` set only for this process. No `user-secrets` step is needed. Running the dll, not `dotnet run`, leaves one process whose id is kept in `.run/e2e-api.pid`, so stopping it is sure. The script stops first if something already answers on that port. MySQL is reached as `root`; a root password, if the machine has one, comes from `E2E_MYSQL_PASSWORD`.
4. Wait until `/api/health` answers, then run Playwright.
5. A `trap` on exit stops the API and drops the database even when a test fails or the run is interrupted. `e2e:clean` does the same by hand if the shell was killed.
6. Exit code is Playwright's, so an agent can read pass or fail.

MySQL is the real one on the machine (Homebrew, no root password on the owner's Mac, as in earlier real runs), the same engine as production.

## Browser setup

Chromium only. One phone project: 390 x 844, touch, mobile user agent, geolocation granted and set to the AR01-IN sign in the seed (13.756300, 100.501800). One desktop project at 1280 px for D1. Traces and screenshots are kept on failure, under `frontend/test-results/` (git-ignored).

Seed data used (made-up, public, in `DbInitializer.cs`): Cleaner E1001, Supervisor S2001, Admin `admin`, the check-in sign `token-checkin-ar01`. The tests read the values from one helper file, not from copies spread over the tests.

## First tests

| Area | Checks |
|---|---|
| Login | Cleaner, Supervisor and Admin each reach their own home page; a wrong phone number is refused with the page's message. |
| Check-in page | The page scrolls to the bottom; the buttons end up inside the viewport; no horizontal overflow (`scrollWidth <= innerWidth`). |
| My Work | Layout shift (`layout-shift` entries from a `PerformanceObserver`, no recent input) stays under 0.01 from load until the list is shown, with the `/api/my-work` answer held back 800 ms so the loading phase is long enough to see. |
| Live flow | Two browser contexts: Admin on D1 (desktop), Cleaner on a phone context. The Cleaner shifts in and submits a point; D1 shows the Cleaner's name on the point within 10 s without a reload (the poll is every 30 s, so a pass means the push worked). |
| Live badge (facility-0064 amended) | The orange badge text never appears while the first connection is made (the hub's `negotiate` held back 1.5 s). The hub-dropped case stays covered by `LiveBadge.test.tsx`: cutting an open WebSocket from Playwright is not reliable. |
| D1 layout shift | With `/api/admin/buildings` held back 800 ms, D1 does not move when the names arrive; CLS under 0.01. |

Found while planning (2026-10-28), so the checks above are what they can honestly be:
- **The `100dvh` scroll bug cannot be caught here.** Chromium's phone emulation has no toolbar that hides while scrolling, so `dvh` and `svh` are the same height. The check-in test guards the page's layout (fits, buttons reachable), not that bug; a real phone is still the only check for it.
- **CLS under 0.01, not exactly 0**, so a sub-pixel shift from font loading does not fail a run; the old bugs measured 0.10 and 0.04.
- **Who works now:** the seed has a Day Cleaner E1001 (07:00–19:00) and a Night Cleaner E1002 (19:00–07:00), both in AR01; the tests pick the one whose shift the Thai clock is in, so a run works at any hour. Between 19:00 and 20:00 the Night shift's first round (20:00) has not started and D1 shows no submission, so the live-flow test is skipped then with a message saying why.

Tests run one at a time (`workers: 1`): they share the run's database, and the live flow's shift-in changes what the check-in page shows. Each test logs in itself and copes with a shift-in already made (it presses "เข้างาน" only when offered).

## Docs and rules to update with the build

- ADR `facility-0094`: why Playwright on the real stack, why not Argent or mocks, what it cannot catch.
- `docs/handover.md`: how to run, how to clean up, what to do when a run is killed (the next maintainer is unknown, so every feature needs a documented way to run and recover it).
- `docs/code-map.md`, `docs/handoff.md` (as the owner's rule, after every task), and `docs/agents/frontend-standards.md` if it lists the test commands.

## Risks

- Flaky live-update checks: use Playwright's auto-waiting assertions with an explicit timeout, never fixed sleeps.
- First run downloads a Chromium (about 150 MB): the handover says so, and the script prints a clear message when the browser is missing.
- Run time: build plus API start is the cost. Measured and written down in the handoff after the first real run.
- MySQL not running or root needs a password: the script stops with a plain message before building.

## Done means

`npm run e2e` passes on a clean checkout of the branch on the owner's Mac; each new test fails when its bug is put back (proved once per test, like the existing "fails on the old code" rule); `npm run e2e:clean` leaves no database, process or `wwwroot`; unit tests, `tsc` and ESLint stay clean; docs above updated.
