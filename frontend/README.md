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
src/store/                               the store, authSlice, typed hooks
src/shared/api/                          baseApi (RTK Query) and server types
src/shared/auth/                         session refresh, logout, role homes
src/shared/components/                   guard, layout, status and crash screens (class prefix app-)
src/pages/<page>/                        one folder per page: Page.tsx, <page>Api.ts, <page>.css, index.ts
src/test/                                fakeServer (fake fetch) and renderApp (the whole app at a URL)
```

- A page imports from `shared` and `store` only, never from another page. `shared` never imports a page. Lint enforces both.
- A page adds its endpoints with `baseApi.injectEndpoints` in `pages/<page>/<page>Api.ts`.
- Styles go in the page's `.css` with the page's class prefix; tokens are in `src/index.css`. No `style={...}`.
- Text people type is rendered as React text only; never build `href` or `src` from it.

## Session

The access token lives only in the Redux store. The refresh token is an HttpOnly cookie the page never sees. On page load one refresh restores the session; on a 401 every request shares one refresh and retries once. A refresh answered with 401 or 403 ends the session; any other failure (no network, a server fault, a non-JSON body) keeps it, and page-load bootstrapping always finishes. "ออกจากระบบ" logs out on the server and empties the cache.

## Tests

Tests render the whole app at a URL with `renderApp` and fake only `fetch` with `fakeServer`. Find elements the way a person would: by role, label or text.
