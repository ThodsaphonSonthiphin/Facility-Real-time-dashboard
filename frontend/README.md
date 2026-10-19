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
src/store/                               the store, authSlice, hubSlice (scan hub live or not), typed hooks
src/shared/api/                          baseApi (RTK Query), server types, myWorkApi, scanHub (SignalR into the cache), adminSetupApi (Admin reads)
src/shared/auth/                         session refresh, logout, role homes
src/shared/geo/                          readLocation: one fresh GPS reading per press
src/shared/components/                   guard, layout, Admin menu, status and crash screens (class prefix app-)
src/pages/<page>/                        one folder per page: Page.tsx, <page>Api.ts, <page>.css, index.ts
src/test/                                fakeServer (fake fetch), fakeGps, fakeSignalR, fixtures, renderApp
```

- A page imports from `shared` and `store` only, never from another page. `shared` never imports a page. Lint enforces both.
- A page adds its endpoints with `baseApi.injectEndpoints` in `pages/<page>/<page>Api.ts`.
- Admin setup and accounts data provide the cache tag `AdminSetup`, and every Admin save invalidates it: Areas, signs, Cleaners and accounts depend on each other.
- A new Admin page goes under the Admin `ProtectedRoute` in `router.tsx`, and into `shared/components/AdminNav.tsx` if it belongs in the menu.
- Styles go in the page's `.css` with the page's class prefix; tokens are in `src/index.css`. No `style={...}`.
- Text people type is rendered as React text only; never build `href` or `src` from it.

## Session

The access token lives only in the Redux store. The refresh token is an HttpOnly cookie the page never sees. On page load one refresh restores the session; on a 401 every request shares one refresh and retries once. A refresh answered with 401 or 403 ends the session; any other failure (no network, a server fault, a non-JSON body) keeps it, and page-load bootstrapping always finishes. "ออกจากระบบ" logs out on the server and empties the cache.

SignalR gets its token from `getValidAccessToken`, which refreshes first when 30 s or less is left, on every connect and reconnect. A page that wants live updates opens the hub with `followScanHub` inside its query's `onCacheEntryAdded` (one connection per cache entry; see `pages/my-building/myBuildingApi.ts`).

## Tests

Tests render the whole app at a URL with `renderApp` and fake only `fetch` with `fakeServer`. Find elements the way a person would: by role, label or text.

The Cleaner pages also need a location: `fakeGps(nearSign)` from `src/test/fakeGps.ts` gives jsdom a `navigator.geolocation` (`'denied'` and `'unavailable'` answers too). Made-up My Work, attendance and point answers are in `src/test/cleanerFixtures.ts`.

The `@microsoft/signalr` package is replaced in every test by `src/test/fakeSignalR.ts` (`test.alias` in `vite.config.ts`). Use `hub.latest()` to see the connection the app built and to play the server: `push('ScanRecorded', card)`, `reconnecting()`, `reconnected()`, `close()`; wrap pushes in `act(...)`. Made-up Supervisor answers are in `src/test/supervisorFixtures.ts`. The server leaves out null fields, so fixtures leave them out too.

Made-up Admin answers (Areas, signs, points, buildings, Cleaners, accounts) are in `src/test/adminFixtures.ts`; the logged-in Admin is account 7. The capture page reads the GPS for 30 s with `watchPosition`: `fakeGpsTrack([...])` sends one reading a second, and those tests use `vi.useFakeTimers({ shouldAdvanceTime: true })`, `fireEvent` for clicks, and restore real timers in `afterEach`. The print page's "พิมพ์" is checked with `vi.spyOn(window, 'print')`.
