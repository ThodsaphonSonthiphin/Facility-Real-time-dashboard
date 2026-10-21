# Frontend

React 19 + TypeScript on Vite 8. The ADRs `docs/adr/facility-0022` to `0026` and `0061` to `0068` are the rules; this file is the short version.

## Commands

| Command | What it does |
|---|---|
| `npm run dev` | Vite on :5173, proxying `/api` and `/hubs` to the API on :5001 |
| `npm test` | Vitest on jsdom |
| `npm run lint` | eslint 9, folder boundaries, no raw HTML |
| `npm run build` | typecheck, then build into `dist/` |
| `npm run build:api` | build into the API's `wwwroot`, so the API serves the app (facility-0067) |

The pre-commit hook (husky) runs lint, `tsc -b` and the tests.

## Where things go (facility-0025)

```
src/main.tsx · App.tsx · router.tsx     app: store, page-load refresh, route table
src/store/                               the store, authSlice, hubSlice (each hub connection live or not), dashboardSlice (Dashboard filters), typed hooks
src/shared/api/                          baseApi (RTK Query, livePoll), server types, myWorkApi, scanHub (SignalR into the cache), adminSetupApi (Admin reads),
                                         attendanceBoardApi (D2 and D3), alertListApi (alerts and the menu count)
src/shared/auth/                         session refresh, logout, role homes
src/shared/geo/                          readLocation: one fresh GPS reading per press
src/shared/components/                   guard, layout, Admin menu, status and crash screens (class prefix app-)
src/pages/<page>/                        one folder per page: Page.tsx, <page>Api.ts, <page>.css, index.ts
src/test/                                fakeServer (fake fetch), fakeGps, fakeSignalR, fixtures, renderApp
```

- A page imports from `shared` and `store` only, never from another page. `shared` never imports a page. Lint enforces both.
- A page adds its endpoints with `baseApi.injectEndpoints` in `pages/<page>/<page>Api.ts`.
- Admin setup and accounts data provide the cache tag `AdminSetup`, and every save that changes Areas, signs, Cleaners or accounts invalidates it (a phone change or password reset changes nothing shown, so it does not): Areas, signs, Cleaners and accounts depend on each other.
- A new Admin page goes under the Admin `ProtectedRoute` in `router.tsx` as a lazy route (`lazy: async () => ({ Component: (await import('./pages/<page>')).<Page> })`), so phones never download it (facility-0062), and into the lazy-route list in `router.test.tsx`. It goes into `shared/components/AdminNav.tsx` if it belongs in the menu. Phone pages stay in the main file.
- The Admin's live board uses the tags `ServicePoints`, `AttendanceBoard` (one per shift, `boardTag`) and `Alerts`; corrections and covers invalidate the last two.
- Styles go in the page's `.css` with the page's class prefix; tokens are in `src/index.css`. No `style={...}`.
- Text people type is rendered as React text only; never build `href` or `src` from it.

## Session

The access token lives only in the Redux store. The refresh token is an HttpOnly cookie the page never sees. On page load one refresh restores the session; on a 401 every request shares one refresh and retries once. A refresh answered with 401 or 403 ends the session; any other failure (no network, a server fault, a non-JSON body) keeps it, and page-load bootstrapping always finishes. "ออกจากระบบ" logs out on the server and empties the cache.

SignalR gets its token from `getValidAccessToken`, which refreshes first when 30 s or less is left, on every connect and reconnect. A page that wants live updates opens the hub with `followScanHub` inside its query's `onCacheEntryAdded`. There is one connection per cache entry, and each has its own key in `hubSlice`. See `pages/my-building/myBuildingApi.ts`, `pages/dashboard/dashboardApi.ts` and `shared/api/attendanceBoardApi.ts`. The Admin's live pages also use `livePoll` (every open, every 30 s, on focus; facility-0061) and show `LiveBadge` with the connection's key.

## Tests

Tests render the whole app at a URL with `renderApp` and fake only `fetch` with `fakeServer`. Find elements the way a person would: by role, label or text.

The Cleaner pages also need a location: `fakeGps(nearSign)` from `src/test/fakeGps.ts` gives jsdom a `navigator.geolocation` (`'denied'` and `'unavailable'` answers too). Made-up My Work, attendance and point answers are in `src/test/cleanerFixtures.ts`.

The `@microsoft/signalr` package is replaced in every test by `src/test/fakeSignalR.ts` (`test.alias` in `vite.config.ts`). Use `hub.latest()` to see the connection the app built and to play the server: `push('ScanRecorded', card)`, `reconnecting()`, `reconnected()`, `close()`; wrap pushes in `act(...)`. Made-up Supervisor answers are in `src/test/supervisorFixtures.ts`. The server leaves out null fields, so fixtures leave them out too.

Made-up Admin answers (Areas, signs, points, buildings, Cleaners, accounts) are in `src/test/adminFixtures.ts`; the logged-in Admin is account 7. The capture page reads the GPS for 30 s with `watchPosition`: `fakeGpsTrack([...])` sends one reading a second, and those tests use `vi.useFakeTimers({ shouldAdvanceTime: true })`, `fireEvent` for clicks, and restore real timers in `afterEach`. The print page's "พิมพ์" is checked with `vi.spyOn(window, 'print')`.

Made-up live board answers (Dashboard points, the attendance board, alerts, cover candidates) are in `src/test/liveFixtures.ts`, with `apiIdle(store)`, which waits until no request is on its way. The 30 s poll is tested with `vi.useFakeTimers({ shouldAdvanceTime: true })` started before `renderApp` and restored in `finally`, and a window focus with `setupListeners(store.dispatch)` in the test. `renderApp` waits until the router has loaded a lazy page, so the page is there when it returns.
