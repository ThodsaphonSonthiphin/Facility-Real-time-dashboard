# Frontend Foundation Implementation Plan (plan 7)

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development (recommended) or sp-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the phase-1 frontend with the structure the restructure ADRs decided (store, one API layer, real router, role guard, login for all three roles), served by the API from one origin. Every role logs in and lands on its own page.

**Architecture:**
- **Clean slate.** The old screens call an API that plans 1–6 replaced, so nothing is moved. `features/`, `services/`, `types/` and `styles/` are deleted, and the tree of facility-0025 is built new.
- **Data and session (facility-0022–0024):**
  - One Redux store holds `authSlice` (user and access token, memory only).
  - RTK Query's `baseApi` is the only HTTP client. It adds the Bearer header, and on a 401 it runs one shared refresh and retries once.
  - `shared/auth/session.ts` owns the page-load refresh and the single-flight refresh. `endSession` logs out and empties the cache.
- **Routing (facility-0026, 0058):**
  - `createBrowserRouter` over one route table in `router.tsx`.
  - One `ProtectedRoute` checks the role. Each role has a home: Cleaner `/my-work`, Supervisor `/my-building`, Admin `/dashboard`.
  - The two login pages (M0, D0) return the person to the page they first opened.
  - A crash screen is the root `errorElement`.
- **Serving (facility-0067):**
  - The API serves `wwwroot` (filled by `npm run build:api`). Every page path gets `index.html`, and `/api` and `/hubs` paths never do.
  - CORS `AllowAll` is removed. The health check moves to `/api/health`.
- **Guard rails (facility-0065, 0066, 0068):**
  - eslint 9 with eslint-plugin-boundaries (no page imports another page, shared never imports pages).
  - Raw HTML is banned by lint.
  - A husky pre-commit runs frontend lint, tsc and tests.
  - The build commit is shown on the login pages.

**Tech Stack:**
- React 19.2, TypeScript 6.0, Vite 8.3.
- `@reduxjs/toolkit` 2.13.0, `react-redux` 9.3.0, `react-router` 7.18.4.
- Vitest 5.0 on jsdom 30.1.2, with `@testing-library/react` 16.3.3, `@testing-library/user-event` 14.6.8 and `@testing-library/jest-dom` 7.0.1.
- eslint 9.39.5, typescript-eslint 8.71.2 and eslint-plugin-boundaries 7.2.0. husky 9.1.7.
- `@fontsource/ibm-plex-sans-thai` 5.3.0.
- Backend: .NET 10 Minimal API and xUnit 2.9.3, as before.

**Spec:** this plan implements the frontend-restructure ADRs, with the wireframes for the screens.
- `docs/adr/facility-0022-frontend-api-layer.md`: `baseApi` plus `injectEndpoints` per page, server types in `shared/api/types.ts`, one `getErrorMessage`.
- `docs/adr/facility-0023-auth-in-base-query-and-signalr.md`:
  - `authSlice` in memory.
  - `baseQueryWithReauth` with single-flight refresh.
  - The page-load refresh before routing.
  - No `AuthContext`.
- `docs/adr/facility-0024-client-state-split.md`: server data in the RTK Query cache, shared state in slices, form drafts in `useState`.
- `docs/adr/facility-0025-frontend-target-tree.md`: the tree, `pages/` + `shared/` + `store/`.
- `docs/adr/facility-0026-frontend-router-shape.md`: the route table as data, one guard, return to the page after login, a no-permission screen.
- `docs/adr/facility-0058-dashboard-is-admin-only.md`: the Cleaner goes to My Work and the Supervisor to My Building. The Dashboard is the Admin's.
- `docs/adr/facility-0062-phone-bundle-budget-200kb-gzip.md`: at most 200 kB gzip of JavaScript, one file.
- `docs/adr/facility-0063-frontend-css-global-tokens-per-page-files.md`: tokens in `index.css`, one plain `.css` per page with a prefix, no `style={...}` in new code.
- `docs/adr/facility-0065-frontend-eslint-boundaries-and-light-pre-commit.md`: eslint 9, boundaries, husky running frontend checks only.
- `docs/adr/facility-0066-user-text-rendered-as-react-text-only.md`: no `dangerouslySetInnerHTML` (lint), no `href`/`src` from typed text.
- `docs/adr/facility-0067-api-serves-frontend-from-one-origin.md`: `wwwroot`, an `index.html` fallback that excludes `/api` and `/hubs`, no CORS `AllowAll`.
- `docs/adr/facility-0068-frontend-failure-minimum-nothing-leaves-the-phone.md`: a root crash screen with "โหลดใหม่", the build commit on the login page, errors go to the console only.
- `docs/adr/facility-0054-login-with-employee-id-and-phone.md`: Cleaners and Supervisors log in with employee ID and phone, the Admin with username and password.
- `docs/adr/facility-0013`–`0016`: refresh token in an HttpOnly cookie, access token in memory, reuse detection, sliding 30 days.
- Wireframes: `docs/design/wireframe/Login.dc.html` (M0) and `AdminLogin.dc.html` (D0). Their colours give the CSS tokens. Where a wireframe and an ADR disagree, the ADR wins.

**Where this plan sits: the frontend in five plans.** Each one ships working, tested software on its own.

| Plan | Scope | Wireframes |
|---|---|---|
| **7 (this one)** | Foundation: tree, store, API layer, session, router and guard, login pages, crash screen, served by the API | M0, D0 |
| 8 | Cleaner phone: check-in sign scan, attendance events, My Work, scan and submit, late or off-round, blocked, send failed | M1–M5, M8–M14 |
| 9 | Supervisor phone: My Building with live updates (first hub use: `getValidAccessToken`), inspect, nothing to inspect, blocked | M6, M7, M15, M16 |
| 10 | Admin setup: Areas, points, round windows, signs, sign location capture, print signs, accounts | D4, D5, D6, D10, M17 |
| 11 | Admin live board: point status with hub and 30 s poll, attendance and corrections, alerts and covers, inspection summary, export, audit log | D1, D2, D3, D7, D8, D9 |

D11 (Excel import of accounts) waits for its backend (ADR 0056).

## Global Constraints

- **Branch:** `feat/frontend-foundation`, created from `master` at `0611bf3` (PR #55 merged). Do not merge it.
- **Machine:** the Mac. zsh, Node v26.8.2, npm 11.19.1, .NET SDK 10.0.401.
  - Frontend commands run in `frontend/`. Backend commands run from the repo root.
  - Write files that hold Thai text with the editor tools, not shell heredocs.
- **Exact versions.** Install exactly the versions in the Tech Stack.
  - eslint stays on major 9 (facility-0065), and react-router on major 7 (facility-0026), even though newer majors exist.
  - No other new package. Base UI and the SignalR token helper arrive with the plans that first need them (8 and 9).
- **API unchanged.** No endpoint, DTO, hub message or migration changes. The one backend change is Task 5 (how `Program.cs` serves files, no CORS, the health path).
- **Bundle:** the JavaScript of `npm run build` stays ≤ 200 kB gzip in one file (facility-0062). The spike measured 127 kB.
- **Styles:**
  - No `style={...}` anywhere in `src/` (facility-0063).
  - Global tokens live in `src/index.css`. Each page has its own `.css` with a page prefix (`auth-`), and shared components use `app-`.
- **Text people type** is rendered as React text only. No `dangerouslySetInnerHTML` (lint enforces it), and no `href` or `src` built from typed text (facility-0066).
- **Session (facility-0013, 0023):**
  - The access token and user live only in the Redux store. Never write them to `localStorage`, `sessionStorage` or a cookie from JS.
  - There is one refresh at a time (`refreshSession`).
- **Server text is shown as is.** The API answers errors as `{ "message": "<Thai>" }`. `getErrorMessage` shows that message, and pages add their own text only where the context differs (facility-0022 item 4).
- **Tests:**
  - Vitest on jsdom. Only `fetch` is faked, with `src/test/fakeServer.ts`. No MSW and no module mocks of our own code (regression-net #32).
  - Find elements by role, label or text the way a person would, never by CSS class.
- **Copy:** Thai UI text comes from the wireframes. Where this plan gives a string, use it exactly, because tests match it.
- **Public repo:** made-up names only (`แม่บ้านทดสอบ`, `หัวหน้าทดสอบ`, `ผู้ดูแลทดสอบ`), employee IDs like `650001`, and phones like `0000000001`. No secret in any file.
- **MySQL** is used only in Task 6's controller smoke run.
  - Use only throwaway databases `facility_*_test`. Never touch `facility_dashboard`.
  - Ask the owner for the root password each time. Type it only into the command, never into a file or into memory.
  - Subagents skip that step.
- **Handoff after every task (owner's rule):**
  - The last step of every task adds one entry to a new section `## Task log (plan 7)` in `docs/handoff.md`, placed just above `## Pending work`. It is committed with the task.
  - The entry is in English: the date, the task, what changed, the test result, and what comes next. No secret, phone number or real name.
- **Commits** end with the attribution trailer your session gives you. From Task 1 on, the husky pre-commit runs frontend lint, tsc and tests on every commit.
  - If it fails, fix the cause. Never use `--no-verify`.
- **Baselines at `0611bf3`:**
  - Backend: 299 unit tests pass; API tests 519 pass and 5 are skipped (the MySQL ones).
  - Frontend: `src/services/authSession.test.ts` only. It is deleted in Task 1, and its behaviours are covered again in Task 2.

### Decisions made while planning (report them to the owner in the PR)

1. **Rebuild, not move.**
   - facility-0025's order (regression net #32 first, then move layer by layer) assumed working screens. After plans 1–6 the old screens call fields and logins that no longer exist (`currentStatus`, `qrToken` on cards, username-only login), so there is no behaviour to keep.
   - The old frontend is deleted in Task 1 and the facility-0025 tree is built new. Every other restructure ADR stands.
   - facility-0025 and facility-0063 get dated amendments (Task 6).
2. **The frontend is five plans (7–11, table above).** This one ships no business screen. Each role lands on a page that says "หน้านี้กำลังสร้าง" until its plan fills it.
3. **Two login routes:**
   - `/login` is M0 (employee ID + phone). `/login/admin` is D0 (username + password).
   - The wireframes draw them as two screens, linked to each other. facility-0026 named only `/login`.
   - `/admin/login` cannot be used, because the guard covers `/admin/*`.
   - A logged-out visitor to an Admin page is sent to `/login/admin`, and to `/login` otherwise.
4. **Role homes and `/` (facility-0058):** Cleaner `/my-work`, Supervisor `/my-building`, Admin `/dashboard`. `/` redirects to the logged-in role's home, or to `/login`.
5. **Logging out on purpose forgets the page.** The next login goes to that person's own home, because phones may be shared.
   - An expired session still remembers the page (facility-0026 item 4), so a cleaner who scans a sign after 30 days returns to it.
   - This is `authSlice.signedOutByUser`.
6. **Logout empties the RTK Query cache** (`resetApiState`), so the next account never sees the last one's data.
7. **A refresh that cannot reach the server keeps the session.** Only a refused refresh logs out, so a phone that drops signal for a moment stays logged in. This carries over from the old `authSession.ts`.
8. **`baseUrl` is `${window.location.origin}/api`.**
   - It is the same origin as the page (facility-0067).
   - RTK Query builds a `Request`, and a relative URL throws outside a browser. The spike showed this under Vitest.
9. **Not in this plan:**
   - `getValidAccessToken` for SignalR (facility-0023 item 3) comes with the first hub (plan 9).
   - Base UI (#30) comes with the first Dialog or Toast (plan 8 or later).
   - `refetchOnFocus`, `setupListeners` and `dashboardSlice` (facility-0024) come with the Dashboard poll and filters (plan 11).
   - `lucide-react` is removed, because nothing uses it. `@microsoft/signalr` stays for plan 9.
10. **Light theme from the wireframes:** background `#F4F5F4`, primary `#0E6B66`, IBM Plex Sans Thai.
    - The font is bundled with `@fontsource`, so the phone never calls Google Fonts and the font still loads where the factory network blocks it.
11. **eslint-plugin-boundaries 7** classifies elements by folder only.
    - `main.tsx`, `App.tsx` and `router.tsx` are classified as files of category `app` (`boundaries/files`), and the directions of facility-0065 are kept.
    - `boundaries/no-unknown-files` is an error from the start, because no old folders remain.
    - Test files and `src/test/` are outside the boundary rules.
12. **Serving (facility-0067):**
    - The health check moves from `/` to `/api/health`, because `/` is now the app.
    - `index.html` is sent with `Cache-Control: no-cache`, and the hashed `assets/` files with `immutable`. A phone picks up a new build on its next load, and the build commit on the login page shows which build it has (facility-0068).
    - `npm run build:api` builds straight into the API's `wwwroot`, which git ignores. There is no MSBuild step, because there is no CI yet (facility-0065).
13. **The fallback never matches `/api` or `/hubs`.**
    - A plain `{*path:nonfile}` fallback would answer `GET /api/<unknown>` with `index.html` (200). The spike proved it, so the route carries a regex that excludes both prefixes.
    - The audit log's 405 test still passes.

---

## File structure after this plan

```
frontend/
├── .husky/pre-commit                 frontend lint, tsc, tests (facility-0065)
├── eslint.config.js                  eslint 9 + boundaries + no raw HTML
├── index.html                        lang="th", title
├── package.json                      scripts: dev, build, build:api, lint, test, prepare
├── vite.config.ts                    build commit define, dev proxy, jsdom tests
└── src/
    ├── main.tsx                      store, page-load refresh, browser router
    ├── App.tsx                       Provider + SessionGate + RouterProvider
    ├── router.tsx                    the route table (data)
    ├── router.test.tsx               guard, homes, not-found, logout, crash
    ├── index.css                     tokens + base only
    ├── store/
    │   ├── index.ts                  makeStore, RootState, AppStore, AppDispatch
    │   ├── hooks.ts                  useAppDispatch, useAppSelector
    │   └── slices/authSlice.ts       user + token in memory
    ├── shared/
    │   ├── api/baseApi.ts (+ test)   RTK Query, Bearer, 401 → shared refresh → retry
    │   ├── api/types.ts              Role, AuthUser, AuthResponse, LoginRequest
    │   ├── auth/session.ts (+ test)  refreshSession, startSession
    │   ├── auth/endSession.ts        logout
    │   ├── auth/roles.ts             homeOf, roleLabel
    │   ├── components/               ProtectedRoute, AppLayout, SessionGate, HomeRedirect,
    │   │                             StatusScreen, CrashScreen, shared.css
    │   ├── utils/getErrorMessage.ts (+ test)
    │   └── version/buildInfo.ts      build commit
    ├── pages/
    │   ├── auth/                     LoginPage (M0), AdminLoginPage (D0), useLogin, authApi, auth.css, index.ts
    │   ├── my-work/                  MyWorkPage (Cleaner home, filled by plan 8), index.ts
    │   ├── my-building/              MyBuildingPage (Supervisor home, plan 9), index.ts
    │   └── dashboard/                DashboardPage (Admin home, plan 11), index.ts
    └── test/
        ├── setup.ts                  jest-dom, cleanup, unstub
        ├── fakeServer.ts (+ test)    fake fetch by "METHOD /path"
        └── renderApp.tsx             the whole app at a URL, like main.tsx
backend/src/FacilityRealtime.Api/Program.cs        static files, fallback, no CORS, /api/health
backend/tests/FacilityRealtime.ApiTests/FrontendHostingTests.cs
```

---

### Task 1: Clean slate and toolchain

Delete the old app, install the toolchain, and prove that lint, tests and the pre-commit work. After this task the app renders one line of text. That is expected: Tasks 2–4 build it back.

**Files:**
- Delete: `frontend/src/features/`, `frontend/src/services/`, `frontend/src/types/`, `frontend/src/styles/`, `frontend/src/assets/`, `frontend/src/App.tsx`, `frontend/public/mockups/`, `frontend/.oxlintrc.json`
- Modify: `frontend/package.json`, `frontend/package-lock.json` (by npm), `frontend/vite.config.ts`, `frontend/index.html`, `frontend/src/main.tsx`
- Create: `frontend/eslint.config.js`, `frontend/.husky/pre-commit`, `frontend/src/index.css`, `frontend/src/shared/version/buildInfo.ts`, `frontend/src/test/setup.ts`, `frontend/src/test/fakeServer.ts`, `frontend/src/test/fakeServer.test.ts`
- Modify: `docs/handoff.md`

**Interfaces:**
- Produces:
  - `fakeServer(routes: FakeRoutes): { calls: FakeRequest[] }`. Routes are keyed `"METHOD /path"`.
    - `FakeRequest = { method; path; search; body; authorization }`.
    - A handler returns `{ status: number; body?: unknown }`.
    - A route with no handler answers 404 `{ message: "no fake for METHOD /path" }`.
  - `authAnswer(role: 'cleaner' | 'supervisor' | 'admin', token = \`token-${role}\`)` returns `{ status: 200, body: AuthResponse }` for a made-up account with id 7. Its `username` is `admin` for the Admin and `650001` otherwise.
  - `buildCommit: string` from `shared/version/buildInfo.ts`.
  - The CSS tokens in `src/index.css`.

- [ ] **Step 1: Create the branch**

```bash
git checkout master && git pull --ff-only && git checkout -b feat/frontend-foundation
git log --oneline -1   # expect 0611bf3 (or a later handoff-only commit)
```

- [ ] **Step 2: Delete the old app**

From `frontend/`:

```bash
git rm -r --quiet src/features src/services src/types src/styles src/assets src/App.tsx public/mockups .oxlintrc.json
```

The three mockups also live in `docs/decision-map/facility-poc/mockups/`, and `public/` would ship them in the build.

- [ ] **Step 3: Swap the dependencies**

From `frontend/`:

```bash
npm uninstall oxlint lucide-react
npm install @reduxjs/toolkit@2.13.0 react-redux@9.3.0 react-router@7.18.4 @fontsource/ibm-plex-sans-thai@5.3.0
npm install -D eslint@9.39.5 @eslint/js@9.39.5 typescript-eslint@8.71.2 eslint-plugin-react-hooks@7.1.1 eslint-plugin-react-refresh@0.5.7 eslint-plugin-boundaries@7.2.0 globals@17.13.0 husky@9.1.7 jsdom@30.1.2 @testing-library/react@16.3.3 @testing-library/dom@10.4.2 @testing-library/user-event@14.6.8 @testing-library/jest-dom@7.0.1
```

Then set `scripts` in `package.json` to exactly:

```json
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "lint": "eslint .",
    "preview": "vite preview",
    "test": "vitest run",
    "prepare": "cd .. && husky frontend/.husky"
  },
```

- [ ] **Step 4: Write `vite.config.ts`**

```ts
/// <reference types="vitest/config" />
import { execSync } from 'node:child_process';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

/** facility-0068 item 3: the short commit shown on the login pages; "dev" outside a git checkout. */
function buildCommit(): string {
  try {
    return execSync('git rev-parse --short HEAD', { stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim();
  } catch {
    return 'dev';
  }
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  define: {
    __BUILD_COMMIT__: JSON.stringify(buildCommit()),
  },
  server: {
    host: '0.0.0.0', // Allow access from mobile devices on local WiFi
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5001',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5001',
        ws: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.{ts,tsx}'],
    setupFiles: ['src/test/setup.ts'],
  },
});
```

- [ ] **Step 5: Write `eslint.config.js`**

```js
import js from '@eslint/js';
import boundaries from 'eslint-plugin-boundaries';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import { defineConfig, globalIgnores } from 'eslint/config';
import globals from 'globals';
import tseslint from 'typescript-eslint';

// facility-0065: MenuNest's eslint 9 config plus eslint-plugin-boundaries for the folder rules of facility-0025.
export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat['recommended-latest'],
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2023,
      globals: globals.browser,
    },
    rules: {
      // facility-0066: text people type is rendered as React text only
      'no-restricted-syntax': [
        'error',
        {
          selector: "JSXAttribute[name.name='dangerouslySetInnerHTML']",
          message: 'facility-0066: render text people type as React text, never as HTML.',
        },
      ],
    },
  },
  {
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/**/*.test.{ts,tsx}', 'src/test/**'],
    plugins: { boundaries },
    settings: {
      'import/resolver': { node: { extensions: ['.ts', '.tsx'] } },
      'boundaries/elements': [
        { type: 'pages', pattern: 'src/pages/*', capture: ['page'] },
        { type: 'shared', pattern: 'src/shared' },
        { type: 'store', pattern: 'src/store' },
      ],
      // Element descriptors match folders only, so the three app files are classified as files
      'boundaries/files': [{ category: 'app', pattern: 'src/{main,App,router}.tsx' }],
    },
    rules: {
      'boundaries/no-unknown-files': 'error',
      'boundaries/dependencies': [
        'error',
        {
          default: 'disallow',
          policies: [
            { from: { file: { categories: 'app' } }, allow: { to: { element: { types: { anyOf: ['pages', 'shared', 'store'] } } } } },
            { from: { file: { categories: 'app' } }, allow: { to: { file: { categories: 'app' } } } },
            { from: { element: { type: 'pages' } }, allow: { to: { element: { types: { anyOf: ['shared', 'store'] } } } } },
            { from: { element: { type: 'store' } }, allow: { to: { element: { types: { anyOf: ['shared', 'pages'] } } } } },
            { from: { element: { type: 'shared' } }, allow: { to: { element: { type: 'store' } } } },
          ],
        },
      ],
    },
  },
]);
```

Imports inside one page folder (`pages/auth/LoginPage.tsx` → `./useLogin`) belong to the same element and are always allowed.

- [ ] **Step 6: Write `src/index.css`, `index.html`, `buildInfo.ts` and the temporary `main.tsx`**

`src/index.css`:

```css
/* facility-0063: tokens and base rules only. Each page keeps its own plain .css file with a page prefix. */
/* Colours are the wireframes' (docs/design/wireframe). */
:root {
  --color-bg: #f4f5f4;
  --color-surface: #ffffff;
  --color-text: #1d2324;
  --color-text-soft: #3b4446;
  --color-muted: #5f696b;
  --color-border: #d5dada;
  --color-input-border: #aeb6b7;
  --color-primary: #0e6b66;
  --color-primary-dark: #0a4f4b;
  --color-on-primary: #ffffff;
  --color-info-bg: #e3f1ef;
  --color-info-border: #9ccbc6;
  --color-info-text: #0a4f4b;
  --color-error-bg: #fdecea;
  --color-error-border: #e2a39b;
  --color-error-text: #8a1c12;

  --radius-sm: 8px;
  --radius-md: 10px;
  --radius-lg: 14px;
  --gap: 16px;
  --phone-width: 480px;
  --desk-width: 1216px;

  font-family: 'IBM Plex Sans Thai', system-ui, sans-serif;
  color: var(--color-text);
  background: var(--color-bg);
  -webkit-text-size-adjust: 100%;
}

*,
*::before,
*::after {
  box-sizing: border-box;
}

body {
  margin: 0;
  min-height: 100vh;
  background: var(--color-bg);
}

#root {
  min-height: 100vh;
}

h1,
h2,
h3,
p {
  margin: 0;
}

a {
  color: var(--color-primary);
}

a:hover {
  color: var(--color-primary-dark);
}

button,
input,
select,
textarea {
  font: inherit;
}
```

`index.html`: change `<html lang="en">` to `<html lang="th">` and `<title>frontend</title>` to `<title>ระบบลงเวลาและตรวจงานแม่บ้าน</title>`. Leave the rest.

`src/shared/version/buildInfo.ts`:

```ts
declare const __BUILD_COMMIT__: string;

/** facility-0068 item 3: the short commit this bundle was built from (vite.config.ts `define`), shown on the login pages. */
export const buildCommit: string = __BUILD_COMMIT__;
```

`src/main.tsx` (Task 3 replaces it):

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import '@fontsource/ibm-plex-sans-thai/400.css';
import '@fontsource/ibm-plex-sans-thai/600.css';
import '@fontsource/ibm-plex-sans-thai/700.css';
import './index.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <p>กำลังเตรียมระบบ</p>
  </StrictMode>,
);
```

- [ ] **Step 7: Write the failing test for the fake server**

`src/test/setup.ts`:

```ts
import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});
```

`src/test/fakeServer.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { fakeServer } from './fakeServer';

describe('fakeServer', () => {
  it('answers a faked route and records the call', async () => {
    const server = fakeServer({ 'POST /api/things': (request) => ({ status: 201, body: { echo: request.body } }) });

    const response = await fetch('/api/things?x=1', {
      method: 'POST',
      headers: { Authorization: 'Bearer t', 'Content-Type': 'application/json' },
      body: JSON.stringify({ a: 1 }),
    });

    expect(response.status).toBe(201);
    expect(await response.json()).toEqual({ echo: { a: 1 } });
    expect(server.calls).toEqual([
      { method: 'POST', path: '/api/things', search: '?x=1', body: { a: 1 }, authorization: 'Bearer t' },
    ]);
  });

  it('a 204 has no body', async () => {
    fakeServer({ 'POST /api/auth/logout': () => ({ status: 204 }) });

    const response = await fetch('/api/auth/logout', { method: 'POST' });

    expect(response.status).toBe(204);
    expect(await response.text()).toBe('');
  });

  it('answers 404 naming a route that has no fake', async () => {
    fakeServer({});

    const response = await fetch('/api/missing');

    expect(response.status).toBe(404);
    expect(await response.json()).toEqual({ message: 'no fake for GET /api/missing' });
  });
});
```

- [ ] **Step 8: Run it to see it fail**

Run: `npm test`
Expected: FAIL, `Failed to resolve import "./fakeServer"`.

- [ ] **Step 9: Write `src/test/fakeServer.ts`**

```ts
import { vi } from 'vitest';

export interface FakeRequest {
  method: string;
  path: string;
  search: string;
  body: unknown;
  authorization: string | null;
}

export interface FakeResponse {
  status: number;
  body?: unknown;
}

/** Key is "METHOD /path", e.g. "POST /api/auth/login". The handler may answer differently on each call. */
export type FakeRoutes = Record<string, (request: FakeRequest) => FakeResponse | Promise<FakeResponse>>;

/**
 * Replaces the global fetch for one test (the only thing faked besides SignalR, per regression-net #32).
 * Every call is recorded in `calls`. A route with no handler answers 404 with a message naming it, so a missing
 * fake shows up in the test output instead of hanging. test/setup.ts restores the real fetch after each test.
 */
export function fakeServer(routes: FakeRoutes) {
  const calls: FakeRequest[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = input instanceof Request ? input : new Request(new URL(String(input), 'http://localhost'), init);
    const url = new URL(request.url, 'http://localhost');
    const text = await request.text();
    const fake: FakeRequest = {
      method: request.method,
      path: url.pathname,
      search: url.search,
      body: text ? JSON.parse(text) : undefined,
      authorization: request.headers.get('Authorization'),
    };
    calls.push(fake);
    const handler = routes[`${fake.method} ${fake.path}`];
    const answer = handler ? await handler(fake) : { status: 404, body: { message: `no fake for ${fake.method} ${fake.path}` } };
    const hasBody = answer.body !== undefined && answer.status !== 204;
    return new Response(hasBody ? JSON.stringify(answer.body) : null, {
      status: answer.status,
      headers: hasBody ? { 'Content-Type': 'application/json' } : undefined,
    });
  });
  vi.stubGlobal('fetch', fetchMock);
  return { calls };
}

/** A login or refresh answer for a made-up account (public repo: no real names). */
export function authAnswer(role: 'cleaner' | 'supervisor' | 'admin', token = `token-${role}`) {
  const names = { cleaner: 'แม่บ้านทดสอบ', supervisor: 'หัวหน้าทดสอบ', admin: 'ผู้ดูแลทดสอบ' };
  return {
    status: 200,
    body: {
      accessToken: token,
      expiresAt: new Date(Date.now() + 5 * 60_000).toISOString(),
      user: { id: 7, username: role === 'admin' ? 'admin' : '650001', fullName: names[role], role },
    },
  };
}
```

- [ ] **Step 10: Run the tests, the typecheck, the lint and the build**

```bash
npm test          # expect: 1 file, 3 passed
npx tsc -b        # expect: no output
npm run lint      # expect: no output
npm run build     # expect: one dist/assets/index-*.js
```

- [ ] **Step 11: Prove the boundary rules bite**

Plant four violations, run lint, then delete them:

```bash
mkdir -p src/pages/one src/pages/two src/stray
printf "export const Two = 2;\n" > src/pages/two/index.ts
printf "import { Two } from '../two';\nexport const One = Two;\n" > src/pages/one/index.ts
printf "import { One } from '../../pages/one';\nexport const Bad = One;\n" > src/shared/version/bad.ts
printf "export const Stray = 1;\n" > src/stray/stray.ts
printf "export const Html = () => <div dangerouslySetInnerHTML={{ __html: 'x' }} />;\n" > src/pages/one/Html.tsx
npm run lint 2>&1 | grep -E 'error'
rm -r src/pages src/stray src/shared/version/bad.ts
```

Expected: exactly these four errors.
- `pages/one/index.ts`: "no policy allowing dependencies from elements of type "pages" ... page="one" to ... page="two"".
- `shared/version/bad.ts`: "... type "shared" to elements of type "pages"".
- `stray/stray.ts`: "File does not match any file pattern and does not belong to any known element".
- `pages/one/Html.tsx`: "facility-0066: render text people type as React text, never as HTML".

After the `rm`, `npm run lint` is clean again.

- [ ] **Step 12: Install the pre-commit hook**

Create `frontend/.husky/pre-commit`:

```sh
cd frontend
npm run lint
npx tsc -b
npm test
```

Then:

```bash
npm run prepare
git config core.hooksPath   # expect: frontend/.husky/_
```

- [ ] **Step 13: Handoff entry**

Add `## Task log (plan 7)` above `## Pending work` in `docs/handoff.md`, with this first entry (fill in the date):

```markdown
- <date> · Task 1 done: old frontend deleted (it targeted the phase-1 API); toolchain is eslint 9 + boundaries, Vitest on jsdom with a fake fetch, husky pre-commit (frontend lint, tsc, tests); wireframe CSS tokens and IBM Plex Sans Thai bundled. Frontend tests 3 passed; boundary rules shown to catch page→page, shared→pages, unknown files and raw HTML. Next: Task 2 (store, API layer and session).
```

- [ ] **Step 14: Commit**

```bash
git add -A frontend docs/handoff.md docs/superpowers/plans/2026-10-15-frontend-foundation.md
git commit -m "chore(frontend): clear the phase-1 app; eslint 9 with boundaries, Vitest on jsdom, husky pre-commit, wireframe tokens"
```

The pre-commit hook runs and must pass.

---

### Task 2: Store, API layer and session

The core every page builds on: the in-memory session, the RTK Query API with one shared refresh, logout, and the shared error wording.

**Files:**
- Create: `frontend/src/shared/api/types.ts`, `frontend/src/store/slices/authSlice.ts`, `frontend/src/store/index.ts`, `frontend/src/store/hooks.ts`, `frontend/src/shared/auth/session.ts`, `frontend/src/shared/api/baseApi.ts`, `frontend/src/shared/auth/endSession.ts`, `frontend/src/shared/utils/getErrorMessage.ts`
- Test: `frontend/src/shared/api/baseApi.test.ts`, `frontend/src/shared/auth/session.test.ts`, `frontend/src/shared/utils/getErrorMessage.test.ts`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes (Task 1): `fakeServer`, `authAnswer`.
- Produces:
  - `type Role = 'cleaner' | 'supervisor' | 'admin'`.
  - `interface AuthUser { id: number; username: string; fullName: string; role: Role }`.
  - `interface AuthResponse { accessToken: string; expiresAt: string; user: AuthUser }`.
  - `type LoginRequest = { employeeId: string; phone: string } | { username: string; password: string }`.
  - `authSlice` state `{ currentUser, accessToken, expiresAt, isBootstrapping, signedOutByUser }`, with actions `setCredentials(AuthResponse)`, `sessionExpired()`, `signedOut()` and `bootstrapFinished()`.
  - `makeStore()`, plus the types `RootState`, `AppStore` and `AppDispatch`. The store has reducers `auth` and `api`.
  - `useAppDispatch` and `useAppSelector`.
  - `baseApi`: `reducerPath: 'api'`, with no endpoints yet. Pages call `baseApi.injectEndpoints`.
  - `refreshSession(dispatch): Promise<'refreshed' | 'rejected' | 'offline'>`, `startSession(dispatch): Promise<void>` and `endSession(dispatch): Promise<void>`.
  - `getErrorMessage(error: unknown, fallback?: string): string`.

- [ ] **Step 1: Write the types, the slice and the store (no behaviour to test on their own yet)**

`src/shared/api/types.ts`:

```ts
/**
 * Shapes the server sends and accepts (facility-0022). Enums arrive as strings (JsonStringEnumConverter),
 * times as UTC ISO strings ending in "Z". Types used by one page only live in that page's folder.
 */

/** The "role" of a login or refresh answer (AuthClaims.ToClaimValue on the server). */
export type Role = 'cleaner' | 'supervisor' | 'admin';

/** AuthUserDto. username is the employee ID, or the Admin's username. */
export interface AuthUser {
  id: number;
  username: string;
  fullName: string;
  role: Role;
}

/** Answer of POST /api/auth/login and /api/auth/refresh. The refresh token is never here: it is an HttpOnly cookie (facility-0013). */
export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
}

/** facility-0054: Cleaners and Supervisors send employeeId + phone; the Admin sends username + password. */
export type LoginRequest = { employeeId: string; phone: string } | { username: string; password: string };
```

`src/store/slices/authSlice.ts`:

```ts
import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import type { AuthResponse, AuthUser } from '../../shared/api/types';

/** facility-0013, 0023: memory only. Nothing here is ever written to localStorage or sessionStorage. */
export interface AuthState {
  currentUser: AuthUser | null;
  accessToken: string | null;
  expiresAt: string | null;
  /** True from page load until the first refresh answers, so no page decides "not logged in" too early. */
  isBootstrapping: boolean;
  /** The person pressed "ออกจากระบบ": the next login starts at their own home, not at the page they left. */
  signedOutByUser: boolean;
}

const initialState: AuthState = {
  currentUser: null,
  accessToken: null,
  expiresAt: null,
  isBootstrapping: true,
  signedOutByUser: false,
};

const authSlice = createSlice({
  name: 'auth',
  initialState,
  reducers: {
    setCredentials(state, action: PayloadAction<AuthResponse>) {
      state.currentUser = action.payload.user;
      state.accessToken = action.payload.accessToken;
      state.expiresAt = action.payload.expiresAt;
      state.isBootstrapping = false;
      state.signedOutByUser = false;
    },
    /** The refresh cookie is gone, expired or revoked (facility-0016). */
    sessionExpired() {
      return { ...initialState, isBootstrapping: false };
    },
    signedOut() {
      return { ...initialState, isBootstrapping: false, signedOutByUser: true };
    },
    bootstrapFinished(state) {
      state.isBootstrapping = false;
    },
  },
});

export const { setCredentials, sessionExpired, signedOut, bootstrapFinished } = authSlice.actions;
export const authReducer = authSlice.reducer;
```

`src/store/index.ts`:

```ts
import { combineReducers, configureStore } from '@reduxjs/toolkit';
import { baseApi } from '../shared/api/baseApi';
import { authReducer } from './slices/authSlice';

const rootReducer = combineReducers({
  auth: authReducer,
  [baseApi.reducerPath]: baseApi.reducer,
});

/** One store per page load; tests make a fresh one per test. */
export function makeStore() {
  return configureStore({
    reducer: rootReducer,
    middleware: (getDefault) => getDefault().concat(baseApi.middleware),
  });
}

export type RootState = ReturnType<typeof rootReducer>;
export type AppStore = ReturnType<typeof makeStore>;
export type AppDispatch = AppStore['dispatch'];
```

`src/store/hooks.ts`:

```ts
import { useDispatch, useSelector } from 'react-redux';
import type { AppDispatch, RootState } from './index';

export const useAppDispatch = useDispatch.withTypes<AppDispatch>();
export const useAppSelector = useSelector.withTypes<RootState>();
```

- [ ] **Step 2: Write the failing tests**

`src/shared/api/baseApi.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { makeStore } from '../../store';
import { setCredentials } from '../../store/slices/authSlice';
import { authAnswer, fakeServer } from '../../test/fakeServer';
import { baseApi } from './baseApi';

// Throwaway endpoints, only for these tests: the real ones belong to the pages.
const probeApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    probe: build.query<{ ok: boolean }, void>({ query: () => '/probe' }),
    probeTwo: build.query<{ ok: boolean }, void>({ query: () => '/probe-two' }),
    probeLogin: build.mutation<unknown, void>({ query: () => ({ url: '/auth/login', method: 'POST', body: {} }) }),
  }),
});

function loggedInStore() {
  const store = makeStore();
  store.dispatch(setCredentials(authAnswer('cleaner', 'old-token').body));
  return store;
}

describe('baseQueryWithReauth', () => {
  it('sends the access token as a Bearer header', async () => {
    const server = fakeServer({ 'GET /api/probe': () => ({ status: 200, body: { ok: true } }) });
    const store = loggedInStore();

    await store.dispatch(probeApi.endpoints.probe.initiate());

    expect(server.calls[0].authorization).toBe('Bearer old-token');
  });

  it('on 401 refreshes once, then retries with the new token', async () => {
    const server = fakeServer({
      'GET /api/probe': (request) =>
        request.authorization === 'Bearer new-token' ? { status: 200, body: { ok: true } } : { status: 401 },
      'POST /api/auth/refresh': () => authAnswer('cleaner', 'new-token'),
    });
    const store = loggedInStore();

    const result = await store.dispatch(probeApi.endpoints.probe.initiate());

    expect(result.data).toEqual({ ok: true });
    expect(server.calls.map((c) => `${c.method} ${c.path}`)).toEqual([
      'GET /api/probe',
      'POST /api/auth/refresh',
      'GET /api/probe',
    ]);
    expect(store.getState().auth.accessToken).toBe('new-token');
  });

  it('two requests failing together share one refresh (facility-0015)', async () => {
    const answer = (request: { authorization: string | null }) =>
      request.authorization === 'Bearer new-token' ? { status: 200, body: { ok: true } } : { status: 401 };
    const server = fakeServer({
      'GET /api/probe': answer,
      'GET /api/probe-two': answer,
      'POST /api/auth/refresh': () => authAnswer('cleaner', 'new-token'),
    });
    const store = loggedInStore();

    const [one, two] = await Promise.all([
      store.dispatch(probeApi.endpoints.probe.initiate()),
      store.dispatch(probeApi.endpoints.probeTwo.initiate()),
    ]);

    expect(one.data).toEqual({ ok: true });
    expect(two.data).toEqual({ ok: true });
    expect(server.calls.filter((c) => c.path === '/api/auth/refresh')).toHaveLength(1);
  });

  it('logs out when the refresh is rejected, and does not retry', async () => {
    const server = fakeServer({
      'GET /api/probe': () => ({ status: 401 }),
      'POST /api/auth/refresh': () => ({ status: 401 }),
    });
    const store = loggedInStore();

    const result = await store.dispatch(probeApi.endpoints.probe.initiate());

    expect(result.error).toMatchObject({ status: 401 });
    expect(store.getState().auth.currentUser).toBeNull();
    expect(server.calls.filter((c) => c.path === '/api/probe')).toHaveLength(1);
  });

  it('a 401 from login is a wrong secret, not an expired token: no refresh', async () => {
    const server = fakeServer({
      'POST /api/auth/login': () => ({ status: 401 }),
      'POST /api/auth/refresh': () => authAnswer('cleaner', 'new-token'),
    });
    const store = makeStore();

    await store.dispatch(probeApi.endpoints.probeLogin.initiate());

    expect(server.calls.map((c) => c.path)).toEqual(['/api/auth/login']);
  });
});
```

`src/shared/auth/session.test.ts`:

```ts
import { describe, expect, it, vi } from 'vitest';
import { makeStore } from '../../store';
import { setCredentials } from '../../store/slices/authSlice';
import { authAnswer, fakeServer } from '../../test/fakeServer';
import { baseApi } from '../api/baseApi';
import { endSession } from './endSession';
import { refreshSession, startSession } from './session';

const probeApi = baseApi.injectEndpoints({
  endpoints: (build) => ({ sessionProbe: build.query<{ ok: boolean }, void>({ query: () => '/session-probe' }) }),
});

describe('startSession', () => {
  it('a live cookie restores the session before anything routes', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('admin') });
    const store = makeStore();

    await startSession(store.dispatch);

    expect(store.getState().auth).toMatchObject({ isBootstrapping: false, accessToken: 'token-admin' });
    expect(store.getState().auth.currentUser?.role).toBe('admin');
  });

  it('no cookie ends bootstrapping logged out', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => ({ status: 401 }) });
    const store = makeStore();

    await startSession(store.dispatch);

    expect(store.getState().auth).toMatchObject({ isBootstrapping: false, currentUser: null, signedOutByUser: false });
  });

  it('no network ends bootstrapping too, so the page never hangs on the splash', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('offline')));
    const store = makeStore();

    await startSession(store.dispatch);

    expect(store.getState().auth.isBootstrapping).toBe(false);
  });
});

describe('refreshSession', () => {
  it('keeps the session when the network drops for a moment', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('offline')));
    const store = makeStore();
    store.dispatch(setCredentials(authAnswer('cleaner').body));

    const outcome = await refreshSession(store.dispatch);

    expect(outcome).toBe('offline');
    expect(store.getState().auth.currentUser?.role).toBe('cleaner');
  });
});

describe('endSession', () => {
  it('logs out on the server and drops every cached answer of the account', async () => {
    const server = fakeServer({
      'GET /api/session-probe': () => ({ status: 200, body: { ok: true } }),
      'POST /api/auth/logout': () => ({ status: 204 }),
    });
    const store = makeStore();
    store.dispatch(setCredentials(authAnswer('cleaner').body));
    await store.dispatch(probeApi.endpoints.sessionProbe.initiate());
    expect(Object.keys(store.getState().api.queries)).toHaveLength(1);

    await endSession(store.dispatch);

    expect(server.calls.some((c) => c.method === 'POST' && c.path === '/api/auth/logout')).toBe(true);
    expect(store.getState().auth).toMatchObject({ currentUser: null, accessToken: null, signedOutByUser: true });
    expect(store.getState().api.queries).toEqual({});
  });

  it('still forgets the session here when the logout call cannot reach the server', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('offline')));
    const store = makeStore();
    store.dispatch(setCredentials(authAnswer('cleaner').body));

    await endSession(store.dispatch);

    expect(store.getState().auth.currentUser).toBeNull();
  });
});
```

`src/shared/utils/getErrorMessage.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { getErrorMessage } from './getErrorMessage';

describe('getErrorMessage', () => {
  it("shows the server's own Thai message as it is", () => {
    expect(getErrorMessage({ status: 409, data: { message: 'สแกนใหม่อีกครั้ง' } })).toBe('สแกนใหม่อีกครั้ง');
  });

  it('no network', () => {
    expect(getErrorMessage({ status: 'FETCH_ERROR', error: 'TypeError: Failed to fetch' })).toBe(
      'เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ตรวจสอบอินเทอร์เน็ตแล้วลองใหม่',
    );
  });

  it.each([
    [401, 'หมดเวลาการเข้าสู่ระบบ กรุณาเข้าสู่ระบบใหม่'],
    [403, 'บัญชีนี้ไม่มีสิทธิ์ทำรายการนี้'],
    [429, 'ลองหลายครั้งเกินไป รอสักครู่แล้วลองใหม่'],
    [500, 'เซิร์ฟเวอร์ขัดข้อง ลองใหม่อีกครั้ง'],
    [503, 'เซิร์ฟเวอร์ขัดข้อง ลองใหม่อีกครั้ง'],
  ])('status %i without a message', (status, text) => {
    expect(getErrorMessage({ status, data: null })).toBe(text);
  });

  it('a blank server message falls back to the status wording', () => {
    expect(getErrorMessage({ status: 403, data: { message: '  ' } })).toBe('บัญชีนี้ไม่มีสิทธิ์ทำรายการนี้');
  });

  it("anything else gets the page's fallback, or the general one", () => {
    expect(getErrorMessage({ status: 400, data: null }, 'บันทึกไม่สำเร็จ')).toBe('บันทึกไม่สำเร็จ');
    expect(getErrorMessage(new Error('x'))).toBe('ทำรายการไม่สำเร็จ ลองใหม่อีกครั้ง');
    expect(getErrorMessage(undefined)).toBe('ทำรายการไม่สำเร็จ ลองใหม่อีกครั้ง');
  });
});
```

- [ ] **Step 3: Run them to see them fail**

Run: `npm test`
Expected: FAIL. The three new files cannot resolve `./baseApi`, `./session`, `./endSession` or `./getErrorMessage`, and `store/index.ts` cannot resolve `../shared/api/baseApi`.

- [ ] **Step 4: Write the session, the API and the error wording**

`src/shared/auth/session.ts`:

```ts
import type { Dispatch } from '@reduxjs/toolkit';
import type { AuthResponse } from '../api/types';
import { bootstrapFinished, sessionExpired, setCredentials } from '../../store/slices/authSlice';

export type RefreshOutcome = 'refreshed' | 'rejected' | 'offline';

/**
 * facility-0015: one refresh at a time. Everyone who asks while a refresh is in flight shares it, because the
 * server treats a second use of the same refresh token as theft and ends the session.
 */
let inflight: Promise<RefreshOutcome> | null = null;

export function refreshSession(dispatch: Dispatch): Promise<RefreshOutcome> {
  inflight ??= runRefresh(dispatch).finally(() => {
    inflight = null;
  });
  return inflight;
}

async function runRefresh(dispatch: Dispatch): Promise<RefreshOutcome> {
  let response: Response;
  try {
    response = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'same-origin' });
  } catch {
    return 'offline'; // keep the session: a phone losing signal for a moment must not log the person out
  }
  if (!response.ok) {
    dispatch(sessionExpired());
    return 'rejected';
  }
  dispatch(setCredentials((await response.json()) as AuthResponse));
  return 'refreshed';
}

/** Page load (facility-0023 item 4): one refresh decides whether the cookie still holds a session. */
export async function startSession(dispatch: Dispatch): Promise<void> {
  await refreshSession(dispatch);
  dispatch(bootstrapFinished());
}
```

`src/shared/api/baseApi.ts`:

```ts
import {
  createApi,
  fetchBaseQuery,
  type BaseQueryFn,
  type FetchArgs,
  type FetchBaseQueryError,
} from '@reduxjs/toolkit/query/react';
import type { RootState } from '../../store';
import { refreshSession } from '../auth/session';

const rawBaseQuery = fetchBaseQuery({
  // Absolute, on this page's own origin (facility-0067): RTK Query builds a Request, and a relative URL fails outside a browser (tests)
  baseUrl: `${window.location.origin}/api`,
  credentials: 'same-origin',
  prepareHeaders: (headers, { getState }) => {
    const token = (getState() as RootState).auth.accessToken;
    if (token) {
      headers.set('Authorization', `Bearer ${token}`);
    }
    return headers;
  },
});

/** Login, refresh and logout answer 401 for a wrong secret or a dead cookie; refreshing then would be wrong. */
function isAuthCall(args: string | FetchArgs): boolean {
  return (typeof args === 'string' ? args : args.url).startsWith('/auth/');
}

/** facility-0023: on 401, refresh once (shared, facility-0015), then retry the request once. */
export const baseQueryWithReauth: BaseQueryFn<string | FetchArgs, unknown, FetchBaseQueryError> = async (
  args,
  api,
  extraOptions,
) => {
  let result = await rawBaseQuery(args, api, extraOptions);
  if (result.error?.status === 401 && !isAuthCall(args) && (await refreshSession(api.dispatch)) === 'refreshed') {
    result = await rawBaseQuery(args, api, extraOptions);
  }
  return result;
};

/** facility-0022: one API, one cache. Each page adds its endpoints with injectEndpoints in pages/<page>/<page>Api.ts. */
export const baseApi = createApi({
  reducerPath: 'api',
  baseQuery: baseQueryWithReauth,
  endpoints: () => ({}),
});
```

`src/shared/auth/endSession.ts`:

```ts
import type { Dispatch } from '@reduxjs/toolkit';
import { baseApi } from '../api/baseApi';
import { signedOut } from '../../store/slices/authSlice';

/** "ออกจากระบบ": ends the session on the server, forgets it here, and drops every cached answer of this account. */
export async function endSession(dispatch: Dispatch): Promise<void> {
  try {
    await fetch('/api/auth/logout', { method: 'POST', credentials: 'same-origin' });
  } catch {
    // offline: this tab forgets the session now; the server ends it after 30 days unused (facility-0016)
  }
  dispatch(signedOut());
  dispatch(baseApi.util.resetApiState());
}
```

`src/shared/utils/getErrorMessage.ts`:

```ts
/**
 * facility-0022 item 4: one wording for the faults every page shares. The server's own Thai message
 * ({ "message": ... }, ApiResults.Message) is shown as it is. A page passes its own text only where the context differs.
 */
export function getErrorMessage(error: unknown, fallback = 'ทำรายการไม่สำเร็จ ลองใหม่อีกครั้ง'): string {
  if (typeof error !== 'object' || error === null || !('status' in error)) {
    return fallback;
  }
  const { status, data } = error as { status: unknown; data?: unknown };
  if (status === 'FETCH_ERROR' || status === 'TIMEOUT_ERROR') {
    return 'เชื่อมต่อเซิร์ฟเวอร์ไม่ได้ ตรวจสอบอินเทอร์เน็ตแล้วลองใหม่';
  }
  const message = typeof data === 'object' && data !== null ? (data as { message?: unknown }).message : undefined;
  if (typeof message === 'string' && message.trim() !== '') {
    return message;
  }
  if (status === 401) {
    return 'หมดเวลาการเข้าสู่ระบบ กรุณาเข้าสู่ระบบใหม่';
  }
  if (status === 403) {
    return 'บัญชีนี้ไม่มีสิทธิ์ทำรายการนี้';
  }
  if (status === 429) {
    return 'ลองหลายครั้งเกินไป รอสักครู่แล้วลองใหม่';
  }
  if (typeof status === 'number' && status >= 500) {
    return 'เซิร์ฟเวอร์ขัดข้อง ลองใหม่อีกครั้ง';
  }
  return fallback;
}
```

- [ ] **Step 5: Run everything**

```bash
npm test      # expect: 4 files, 23 passed (3 + 5 + 6 + 9)
npx tsc -b
npm run lint
```

- [ ] **Step 6: Check that the key tests guard what they claim**

Make each change below, run `npm test`, confirm that exactly the named test fails, then undo it with `git checkout -- <file>`. Commit none of this.

| Change | Test that must fail |
|---|---|
| `session.ts`: `inflight ??= runRefresh` → `inflight = runRefresh` | two requests failing together share one refresh |
| `baseApi.ts`: delete `!isAuthCall(args) &&` | a 401 from login is a wrong secret … no refresh |
| `endSession.ts`: delete the `resetApiState` line | logs out on the server and drops every cached answer |
| `session.ts`: in the `catch`, add `dispatch(sessionExpired());` before `return 'offline'` | keeps the session when the network drops for a moment |

- [ ] **Step 7: Handoff entry and commit**

Handoff entry:

```markdown
- <date> · Task 2 done: authSlice (memory only), store, RTK Query baseApi with Bearer header and one shared refresh on 401 (never for /auth/ calls), page-load startSession, endSession (logout + cache reset), getErrorMessage. Frontend tests 23 passed; each guard shown to fail when its rule is removed. Next: Task 3 (router, guard and role homes).
```

```bash
git add frontend/src docs/handoff.md
git commit -m "feat(frontend): store, RTK Query API with single-flight refresh, session start and end, shared error text"
```

---

### Task 3: Router, guard and role homes

The route table, the role guard, the three role homes, the not-found, no-permission and crash screens, and the bar with "ออกจากระบบ". The login pages come in Task 4. Until then `/login` shows not-found, so the tests here check addresses, not the login screen.

**Files:**
- Create: `frontend/src/shared/auth/roles.ts`, `frontend/src/shared/components/{ProtectedRoute,StatusScreen,CrashScreen,HomeRedirect,SessionGate,AppLayout}.tsx`, `frontend/src/shared/components/shared.css`
- Create: `frontend/src/pages/my-work/{MyWorkPage.tsx,index.ts}`, `frontend/src/pages/my-building/{MyBuildingPage.tsx,index.ts}`, `frontend/src/pages/dashboard/{DashboardPage.tsx,index.ts}`
- Create: `frontend/src/router.tsx`, `frontend/src/App.tsx`, `frontend/src/test/renderApp.tsx`
- Modify: `frontend/src/main.tsx` (final form)
- Test: `frontend/src/router.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes (Task 2): `makeStore`, `AppStore`, `useAppSelector`, `useAppDispatch`, `startSession`, `endSession`, `Role`.
- Produces:
  - `homeOf(role: Role): string`. Cleaner `'/my-work'`, Supervisor `'/my-building'`, Admin `'/dashboard'`.
  - `roleLabel: Record<Role, string>`: `แม่บ้าน`, `หัวหน้างาน`, `Admin`.
  - `<ProtectedRoute roles={Role[]} />`, a layout route that renders `<Outlet />`.
    - When logged out it redirects to `/login/admin` (Admin-only routes) or `/login`, with `state: { from: location }`. The state is left out after an on-purpose logout.
    - When the role is wrong it shows "ไม่มีสิทธิ์เปิดหน้านี้".
  - `<AppLayout wide? />`: the bar with the person's name, their role and an "ออกจากระบบ" button, then `<Outlet />`.
  - `<StatusScreen title detail? linkTo? linkText? />`.
  - `routes: RouteObject[]` from `router.tsx`. `routes[0]` is the root `/` with `errorElement: <CrashScreen />`. Later plans add children.
  - `<App store router />`.
  - `renderApp(url, routes?)`, an async test helper that returns `{ store, router, user }`.
  - CSS classes `app-shell`, `app-shell--wide`, `app-bar`, `app-page`, `app-button`, `app-button--primary`, `app-status`, `app-splash`.

- [ ] **Step 1: Write the failing test**

`src/router.test.tsx`:

```tsx
import { screen } from '@testing-library/react';
import type { RouteObject } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { routes } from './router';
import { authAnswer, fakeServer } from './test/fakeServer';
import { renderApp } from './test/renderApp';

const noSession = { 'POST /api/auth/refresh': () => ({ status: 401 }) };

describe('routing (facility-0026, 0058)', () => {
  it('a live session opens the role home without showing the login page', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('supervisor') });
    const { router } = await renderApp('/');

    expect(await screen.findByRole('heading', { name: 'ตึกของฉัน' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/my-building');
  });

  it('without a session, "/" goes to the login page', async () => {
    fakeServer(noSession);
    const { router } = await renderApp('/');

    expect(router.state.location.pathname).toBe('/login');
  });

  it('a page behind login remembers itself for after the login', async () => {
    fakeServer(noSession);
    const { router } = await renderApp('/my-work?from=sign');

    expect(router.state.location.pathname).toBe('/login');
    expect(router.state.location.state).toMatchObject({ from: { pathname: '/my-work', search: '?from=sign' } });
  });

  it('an Admin page sends a logged-out visitor to the Admin login', async () => {
    fakeServer(noSession);
    const { router } = await renderApp('/dashboard');

    expect(router.state.location.pathname).toBe('/login/admin');
    expect(router.state.location.state).toMatchObject({ from: { pathname: '/dashboard' } });
  });

  it('a Cleaner opening the Dashboard sees the no-permission screen (facility-0058)', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('cleaner') });
    await renderApp('/dashboard');

    expect(screen.getByRole('heading', { name: 'ไม่มีสิทธิ์เปิดหน้านี้' })).toBeInTheDocument();
    expect(screen.getByText('หน้านี้สำหรับAdmin บัญชีนี้เป็นแม่บ้าน')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'กลับหน้าของฉัน' })).toHaveAttribute('href', '/my-work');
  });

  it('an unknown address shows not-found', async () => {
    fakeServer(noSession);
    await renderApp('/no-such-page');

    expect(screen.getByRole('heading', { name: 'ไม่พบหน้านี้' })).toBeInTheDocument();
  });

  it('logout ends the session on the server and the next login starts at the own home', async () => {
    const server = fakeServer({
      'POST /api/auth/refresh': () => authAnswer('cleaner'),
      'POST /api/auth/logout': () => ({ status: 204 }),
    });
    const { user, router, store } = await renderApp('/my-work');
    expect(screen.getByText('แม่บ้านทดสอบ')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'ออกจากระบบ' }));

    expect(router.state.location.pathname).toBe('/login');
    expect(router.state.location.state).toBeNull();
    expect(store.getState().auth.accessToken).toBeNull();
    expect(server.calls.some((c) => c.path === '/api/auth/logout')).toBe(true);
  });

  it('a page that throws shows the crash screen with a reload button (facility-0068)', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    fakeServer(noSession);
    const Broken = () => {
      throw new Error('boom');
    };
    const [root] = routes;
    const withBrokenPage: RouteObject[] = [
      { path: root.path, errorElement: root.errorElement, children: [...(root.children ?? []), { path: 'broken', element: <Broken /> }] },
    ];

    await renderApp('/broken', withBrokenPage);

    expect(screen.getByRole('heading', { name: 'เกิดข้อผิดพลาด' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'โหลดใหม่' })).toBeInTheDocument();
  });
});
```

`src/test/renderApp.tsx`:

```tsx
import { act, render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, type RouteObject } from 'react-router';
import { App } from '../App';
import { routes as appRoutes } from '../router';
import { startSession } from '../shared/auth/session';
import { makeStore } from '../store';

/**
 * Renders the whole app at `url` the way main.tsx does: a fresh store, the page-load refresh, then the router
 * (regression-net #32: only fetch and SignalR are faked). Call fakeServer first; it must answer POST /api/auth/refresh.
 */
export async function renderApp(url: string, routes: RouteObject[] = appRoutes) {
  const store = makeStore();
  const router = createMemoryRouter(routes, { initialEntries: [url] });
  const user = userEvent.setup();
  render(<App store={store} router={router} />);
  await act(() => startSession(store.dispatch));
  return { store, router, user };
}
```

- [ ] **Step 2: Run it to see it fail**

Run: `npm test`
Expected: FAIL, `router.test.tsx` cannot resolve `./router` and `renderApp` cannot resolve `../App`.

- [ ] **Step 3: Write the shared pieces**

`src/shared/auth/roles.ts`:

```ts
import type { Role } from '../api/types';

/** facility-0058: each role has its own first page. Only the Admin opens the Dashboard. */
export function homeOf(role: Role): string {
  switch (role) {
    case 'cleaner':
      return '/my-work';
    case 'supervisor':
      return '/my-building';
    case 'admin':
      return '/dashboard';
  }
}

export const roleLabel: Record<Role, string> = {
  cleaner: 'แม่บ้าน',
  supervisor: 'หัวหน้างาน',
  admin: 'Admin',
};
```

`src/shared/components/StatusScreen.tsx`:

```tsx
import { Link } from 'react-router';
import './shared.css';

interface StatusScreenProps {
  title: string;
  detail?: string;
  linkTo?: string;
  linkText?: string;
}

/** The full-page message of "not found", "no permission" and similar dead ends. */
export function StatusScreen({ title, detail, linkTo, linkText }: StatusScreenProps) {
  return (
    <main className="app-status">
      <h1 className="app-status__title">{title}</h1>
      {detail && <p className="app-status__detail">{detail}</p>}
      {linkTo && linkText && (
        <Link className="app-button app-button--primary" to={linkTo}>
          {linkText}
        </Link>
      )}
    </main>
  );
}
```

`src/shared/components/ProtectedRoute.tsx`:

```tsx
import { Navigate, Outlet, useLocation } from 'react-router';
import type { Role } from '../api/types';
import { useAppSelector } from '../../store/hooks';
import { homeOf, roleLabel } from '../auth/roles';
import { StatusScreen } from './StatusScreen';

/**
 * facility-0026: one guard, used as a layout route. It hides pages, it does not protect data: the server's
 * policies do that (facility-0010, 0018).
 */
export function ProtectedRoute({ roles }: { roles: readonly Role[] }) {
  const user = useAppSelector((state) => state.auth.currentUser);
  const signedOutByUser = useAppSelector((state) => state.auth.signedOutByUser);
  const location = useLocation();

  if (!user) {
    const loginPath = roles.length === 1 && roles[0] === 'admin' ? '/login/admin' : '/login';
    // Remember the page (a scanned sign's URL above all) unless the person logged out on purpose
    return <Navigate to={loginPath} state={signedOutByUser ? undefined : { from: location }} replace />;
  }
  if (!roles.includes(user.role)) {
    return (
      <StatusScreen
        title="ไม่มีสิทธิ์เปิดหน้านี้"
        detail={`หน้านี้สำหรับ${roles.map((role) => roleLabel[role]).join(' หรือ ')} บัญชีนี้เป็น${roleLabel[user.role]}`}
        linkTo={homeOf(user.role)}
        linkText="กลับหน้าของฉัน"
      />
    );
  }
  return <Outlet />;
}
```

`src/shared/components/CrashScreen.tsx`:

```tsx
import { useEffect } from 'react';
import { useRouteError } from 'react-router';
import './shared.css';

/**
 * facility-0068 item 2: the root route's errorElement, instead of a white page. Item 4: the error goes to the
 * browser console only; nothing leaves the phone.
 */
export function CrashScreen() {
  const error = useRouteError();

  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <main className="app-status">
      <h1 className="app-status__title">เกิดข้อผิดพลาด</h1>
      <p className="app-status__detail">หน้านี้แสดงผลไม่ได้ กดโหลดใหม่เพื่อลองอีกครั้ง</p>
      <button type="button" className="app-button app-button--primary" onClick={() => window.location.reload()}>
        โหลดใหม่
      </button>
    </main>
  );
}
```

`src/shared/components/HomeRedirect.tsx`:

```tsx
import { Navigate } from 'react-router';
import { useAppSelector } from '../../store/hooks';
import { homeOf } from '../auth/roles';

/** "/" goes to the logged-in account's own page (facility-0058), or to the login page. */
export function HomeRedirect() {
  const user = useAppSelector((state) => state.auth.currentUser);
  return <Navigate to={user ? homeOf(user.role) : '/login'} replace />;
}
```

`src/shared/components/SessionGate.tsx`:

```tsx
import type { ReactNode } from 'react';
import { useAppSelector } from '../../store/hooks';
import './shared.css';

/** facility-0023 item 4: nothing routes until the page-load refresh answers, so a live session never flashes the login page. */
export function SessionGate({ children }: { children: ReactNode }) {
  const isBootstrapping = useAppSelector((state) => state.auth.isBootstrapping);
  if (isBootstrapping) {
    return <p className="app-splash">กำลังตรวจสอบการเข้าสู่ระบบ...</p>;
  }
  return children;
}
```

`src/shared/components/AppLayout.tsx`:

```tsx
import { Outlet } from 'react-router';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { endSession } from '../auth/endSession';
import { roleLabel } from '../auth/roles';
import './shared.css';

/**
 * The bar above every logged-in page: who is logged in, and "ออกจากระบบ". `wide` is the Admin's desktop width
 * (wireframes D1–D11); without it the page is a phone column (M0–M17).
 */
export function AppLayout({ wide = false }: { wide?: boolean }) {
  const user = useAppSelector((state) => state.auth.currentUser);
  const dispatch = useAppDispatch();

  return (
    <div className={wide ? 'app-shell app-shell--wide' : 'app-shell'}>
      <header className="app-bar">
        <span className="app-bar__who">
          {user?.fullName}
          {user && <span className="app-bar__role"> · {roleLabel[user.role]}</span>}
        </span>
        <button type="button" className="app-button" onClick={() => void endSession(dispatch)}>
          ออกจากระบบ
        </button>
      </header>
      <Outlet />
    </div>
  );
}
```

`AppLayout` does not navigate after logout. `signedOut()` clears the user, and the `ProtectedRoute` above it redirects to `/login` without `from`.

`src/shared/components/shared.css`:

```css
/* Shared components. Class prefix: app- */

.app-shell {
  max-width: var(--phone-width);
  margin: 0 auto;
  padding: 12px 16px 24px;
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.app-shell--wide {
  max-width: var(--desk-width);
  padding: 16px 32px 32px;
}

.app-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  font-size: 14px;
  color: var(--color-muted);
}

.app-bar__who {
  font-weight: 600;
  color: var(--color-text);
}

.app-bar__role {
  font-weight: 400;
  color: var(--color-muted);
}

.app-page {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.app-page h1 {
  font-size: 26px;
  font-weight: 700;
}

.app-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 40px;
  padding: 0 14px;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-text);
  font-size: 14px;
  text-decoration: none;
  cursor: pointer;
}

.app-button--primary {
  min-height: 48px;
  border-color: var(--color-primary);
  background: var(--color-primary);
  color: var(--color-on-primary);
  font-weight: 700;
  font-size: 17px;
}

.app-button--primary:hover {
  color: var(--color-on-primary);
  background: var(--color-primary-dark);
}

.app-status {
  max-width: var(--phone-width);
  margin: 0 auto;
  padding: 48px 20px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  text-align: center;
}

.app-status__title {
  font-size: 22px;
  font-weight: 700;
}

.app-status__detail {
  color: var(--color-muted);
}

.app-splash {
  padding: 48px 20px;
  text-align: center;
  color: var(--color-muted);
}
```

- [ ] **Step 4: Write the three role homes**

Each page folder has the page and a barrel (`index.ts`). Other code imports the page only through the barrel.

`src/pages/my-work/MyWorkPage.tsx`:

```tsx
/** facility-0052, wireframe M9: the Cleaner's page. Plan 8 fills it; until then it is where a Cleaner lands after login. */
export function MyWorkPage() {
  return (
    <main className="app-page">
      <h1>งานของฉัน</h1>
      <p>หน้านี้กำลังสร้าง</p>
    </main>
  );
}
```

`src/pages/my-work/index.ts`:

```ts
export { MyWorkPage } from './MyWorkPage';
```

`src/pages/my-building/MyBuildingPage.tsx`:

```tsx
/** facility-0057, wireframe M15: the Supervisor's page. Plan 9 fills it; until then it is where a Supervisor lands after login. */
export function MyBuildingPage() {
  return (
    <main className="app-page">
      <h1>ตึกของฉัน</h1>
      <p>หน้านี้กำลังสร้าง</p>
    </main>
  );
}
```

`src/pages/my-building/index.ts`:

```ts
export { MyBuildingPage } from './MyBuildingPage';
```

`src/pages/dashboard/DashboardPage.tsx`:

```tsx
/** facility-0058, wireframe D1: the Admin's Dashboard. Plan 11 fills it; until then it is where the Admin lands after login. */
export function DashboardPage() {
  return (
    <main className="app-page">
      <h1>สถานะจุดทำความสะอาด</h1>
      <p>หน้านี้กำลังสร้าง</p>
    </main>
  );
}
```

`src/pages/dashboard/index.ts`:

```ts
export { DashboardPage } from './DashboardPage';
```

- [ ] **Step 5: Write the route table, `App` and the final `main.tsx`**

`src/router.tsx`:

```tsx
import type { RouteObject } from 'react-router';
import { DashboardPage } from './pages/dashboard';
import { MyBuildingPage } from './pages/my-building';
import { MyWorkPage } from './pages/my-work';
import { AppLayout } from './shared/components/AppLayout';
import { CrashScreen } from './shared/components/CrashScreen';
import { HomeRedirect } from './shared/components/HomeRedirect';
import { ProtectedRoute } from './shared/components/ProtectedRoute';
import { StatusScreen } from './shared/components/StatusScreen';

/**
 * facility-0026: the whole route table as data. main.tsx feeds it to createBrowserRouter, tests to createMemoryRouter.
 * No loader or action: RTK Query owns data loading (facility-0022).
 */
export const routes: RouteObject[] = [
  {
    path: '/',
    errorElement: <CrashScreen />,
    children: [
      { index: true, element: <HomeRedirect /> },
      {
        element: <ProtectedRoute roles={['cleaner']} />,
        children: [{ element: <AppLayout />, children: [{ path: 'my-work', element: <MyWorkPage /> }] }],
      },
      {
        element: <ProtectedRoute roles={['supervisor']} />,
        children: [{ element: <AppLayout />, children: [{ path: 'my-building', element: <MyBuildingPage /> }] }],
      },
      {
        element: <ProtectedRoute roles={['admin']} />,
        children: [{ element: <AppLayout wide />, children: [{ path: 'dashboard', element: <DashboardPage /> }] }],
      },
      {
        path: '*',
        element: <StatusScreen title="ไม่พบหน้านี้" detail="ที่อยู่นี้ไม่มีในระบบ" linkTo="/" linkText="กลับหน้าแรก" />,
      },
    ],
  },
];
```

`src/App.tsx`:

```tsx
import { Provider } from 'react-redux';
import { RouterProvider, type createBrowserRouter } from 'react-router';
import { SessionGate } from './shared/components/SessionGate';
import type { AppStore } from './store';

interface AppProps {
  store: AppStore;
  router: ReturnType<typeof createBrowserRouter>;
}

/** Providers only (facility-0026 item 2): the store, the session gate and the router. */
export function App({ store, router }: AppProps) {
  return (
    <Provider store={store}>
      <SessionGate>
        <RouterProvider router={router} />
      </SessionGate>
    </Provider>
  );
}
```

`src/main.tsx`:

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter } from 'react-router';
import '@fontsource/ibm-plex-sans-thai/400.css';
import '@fontsource/ibm-plex-sans-thai/600.css';
import '@fontsource/ibm-plex-sans-thai/700.css';
import './index.css';
import { App } from './App';
import { routes } from './router';
import { startSession } from './shared/auth/session';
import { makeStore } from './store';

const store = makeStore();
// Outside React, so StrictMode's double effects can never send two refreshes (facility-0015)
void startSession(store.dispatch);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App store={store} router={createBrowserRouter(routes)} />
  </StrictMode>,
);
```

- [ ] **Step 6: Run everything**

```bash
npm test      # expect: 5 files, 31 passed
npx tsc -b
npm run lint
npm run build
```

- [ ] **Step 7: Check that the guard tests guard**

Make each change below, run `npm test`, confirm the named test fails (others may fail with it), then undo it with `git checkout -- <file>`.

| Change | Test that must fail |
|---|---|
| `ProtectedRoute.tsx`: delete `state={signedOutByUser ? undefined : { from: location }}` | a page behind login remembers itself for after the login |
| `ProtectedRoute.tsx`: `signedOutByUser ? undefined :` → `false ? undefined :` | logout ends the session … starts at the own home |
| `ProtectedRoute.tsx`: `roles.length === 1 && roles[0] === 'admin'` → `false` | an Admin page sends a logged-out visitor to the Admin login |

- [ ] **Step 8: Handoff entry and commit**

Handoff entry:

```markdown
- <date> · Task 3 done: route table (createBrowserRouter), ProtectedRoute by role, role homes /my-work, /my-building, /dashboard (placeholders), not-found, no-permission, crash screen, session gate, bar with logout. Frontend tests 31 passed. Next: Task 4 (login pages M0 and D0).
```

```bash
git add frontend/src docs/handoff.md
git commit -m "feat(frontend): router with role guard, role homes, not-found, no-permission and crash screens, logout bar"
```

---

### Task 4: Login pages (M0 and D0)

The Cleaner and Supervisor log in with employee ID and phone at `/login`. The Admin logs in with username and password at `/login/admin`. Both pages return to the page that was opened before the login.

**Files:**
- Create: `frontend/src/pages/auth/{authApi.ts,useLogin.ts,LoginPage.tsx,AdminLoginPage.tsx,auth.css,index.ts}`
- Modify: `frontend/src/router.tsx` (two routes)
- Test: `frontend/src/pages/auth/LoginPage.test.tsx`
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes:
  - From Task 2: `baseApi`, `LoginRequest`, `AuthResponse`, `setCredentials`, `getErrorMessage`, `useAppDispatch` and `useAppSelector`.
  - From Task 3: `homeOf`, `routes` and `renderApp`.
  - From Task 1: `buildCommit`.
- Produces:
  - `useLoginMutation`, from `pages/auth/authApi.ts`. It is used only inside `pages/auth`.
  - `LoginPage` and `AdminLoginPage`, exported from the barrel `pages/auth/index.ts`.
  - Routes `/login` and `/login/admin`.
  - The login state shape `{ from?: Location }`.

- [ ] **Step 1: Write the failing test**

`src/pages/auth/LoginPage.test.tsx`:

```tsx
import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { authAnswer, fakeServer } from '../../test/fakeServer';
import { renderApp } from '../../test/renderApp';

const noSession = { 'POST /api/auth/refresh': () => ({ status: 401 }) };

describe('LoginPage (M0)', () => {
  it('a Cleaner logs in with employee ID and phone and lands on My Work', async () => {
    const server = fakeServer({ ...noSession, 'POST /api/auth/login': () => authAnswer('cleaner') });
    const { user, router } = await renderApp('/login');

    await user.type(screen.getByLabelText('รหัสพนักงาน'), ' 650001 ');
    await user.type(screen.getByLabelText('เบอร์โทรศัพท์'), '000-000-0001');
    await user.click(screen.getByRole('button', { name: 'เข้าสู่ระบบ' }));

    expect(await screen.findByRole('heading', { name: 'งานของฉัน' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/my-work');
    expect(server.calls.find((c) => c.path === '/api/auth/login')?.body).toEqual({
      employeeId: '650001',
      phone: '000-000-0001',
    });
  });

  it('a Supervisor lands on My Building', async () => {
    fakeServer({ ...noSession, 'POST /api/auth/login': () => authAnswer('supervisor') });
    const { user } = await renderApp('/login');

    await user.type(screen.getByLabelText('รหัสพนักงาน'), '650002');
    await user.type(screen.getByLabelText('เบอร์โทรศัพท์'), '0000000002');
    await user.click(screen.getByRole('button', { name: 'เข้าสู่ระบบ' }));

    expect(await screen.findByRole('heading', { name: 'ตึกของฉัน' })).toBeInTheDocument();
  });

  it('a wrong ID or phone shows the M0 message and stays on the page', async () => {
    fakeServer({ ...noSession, 'POST /api/auth/login': () => ({ status: 401 }) });
    const { user, router } = await renderApp('/login');

    await user.type(screen.getByLabelText('รหัสพนักงาน'), '650001');
    await user.type(screen.getByLabelText('เบอร์โทรศัพท์'), '0999999999');
    await user.click(screen.getByRole('button', { name: 'เข้าสู่ระบบ' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('รหัสพนักงานหรือเบอร์โทรไม่ถูกต้อง');
    expect(router.state.location.pathname).toBe('/login');
  });

  it('the login throttle (429) asks the person to wait', async () => {
    fakeServer({ ...noSession, 'POST /api/auth/login': () => ({ status: 429 }) });
    const { user } = await renderApp('/login');

    await user.type(screen.getByLabelText('รหัสพนักงาน'), '650001');
    await user.type(screen.getByLabelText('เบอร์โทรศัพท์'), '0999999999');
    await user.click(screen.getByRole('button', { name: 'เข้าสู่ระบบ' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('กรอกผิดหลายครั้ง รอสักครู่แล้วลองใหม่');
  });

  it('shows the build commit (facility-0068)', async () => {
    fakeServer(noSession);
    await renderApp('/login');

    expect(screen.getByText(/^เวอร์ชัน /)).toBeInTheDocument();
  });

  it('after login, returns to the page that was opened before it (facility-0026)', async () => {
    fakeServer({ ...noSession, 'POST /api/auth/login': () => authAnswer('cleaner') });
    const { user, router } = await renderApp('/my-work?from=sign');

    expect(screen.getByText('เข้าสู่ระบบก่อน แล้วระบบจะพากลับไปหน้าที่เปิดไว้ทันที')).toBeInTheDocument();
    await user.type(screen.getByLabelText('รหัสพนักงาน'), '650001');
    await user.type(screen.getByLabelText('เบอร์โทรศัพท์'), '0000000001');
    await user.click(screen.getByRole('button', { name: 'เข้าสู่ระบบ' }));

    await screen.findByRole('heading', { name: 'งานของฉัน' });
    expect(router.state.location.pathname + router.state.location.search).toBe('/my-work?from=sign');
  });

  it('after an on-purpose logout the login page is the plain one', async () => {
    fakeServer({ 'POST /api/auth/refresh': () => authAnswer('cleaner'), 'POST /api/auth/logout': () => ({ status: 204 }) });
    const { user } = await renderApp('/my-work');

    await user.click(screen.getByRole('button', { name: 'ออกจากระบบ' }));

    expect(await screen.findByRole('heading', { name: 'เข้าสู่ระบบ' })).toBeInTheDocument();
    expect(screen.queryByText('เข้าสู่ระบบก่อน แล้วระบบจะพากลับไปหน้าที่เปิดไว้ทันที')).not.toBeInTheDocument();
  });
});

describe('AdminLoginPage (D0)', () => {
  it('the Admin logs in with username and password and lands on the Dashboard', async () => {
    const server = fakeServer({ ...noSession, 'POST /api/auth/login': () => authAnswer('admin') });
    const { user, router } = await renderApp('/login/admin');

    await user.type(screen.getByLabelText('ชื่อผู้ใช้'), 'admin');
    await user.type(screen.getByLabelText('รหัสผ่าน'), 'not-a-real-password');
    await user.click(screen.getByRole('button', { name: 'เข้าสู่ระบบ' }));

    expect(await screen.findByRole('heading', { name: 'สถานะจุดทำความสะอาด' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/dashboard');
    expect(server.calls.find((c) => c.path === '/api/auth/login')?.body).toEqual({
      username: 'admin',
      password: 'not-a-real-password',
    });
  });

  it('a wrong password shows the D0 message', async () => {
    fakeServer({ ...noSession, 'POST /api/auth/login': () => ({ status: 401 }) });
    const { user } = await renderApp('/login/admin');

    await user.type(screen.getByLabelText('ชื่อผู้ใช้'), 'admin');
    await user.type(screen.getByLabelText('รหัสผ่าน'), 'wrong-password');
    await user.click(screen.getByRole('button', { name: 'เข้าสู่ระบบ' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง');
  });

  it('the link between the two login pages keeps the page to return to', async () => {
    fakeServer({ ...noSession, 'POST /api/auth/login': () => authAnswer('admin') });
    const { user, router } = await renderApp('/my-work');

    await user.click(screen.getByRole('link', { name: 'หน้า Admin' }));

    expect(router.state.location.pathname).toBe('/login/admin');
    expect(router.state.location.state).toMatchObject({ from: { pathname: '/my-work' } });
  });
});
```

- [ ] **Step 2: Run it to see it fail**

Run: `npm test`
Expected: FAIL. The new tests cannot find the labels "รหัสพนักงาน" and "ชื่อผู้ใช้", because `/login` and `/login/admin` still show not-found.

- [ ] **Step 3: Write the API endpoint and the shared login hook**

`src/pages/auth/authApi.ts`:

```ts
import { baseApi } from '../../shared/api/baseApi';
import type { AuthResponse, LoginRequest } from '../../shared/api/types';

const authApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    login: build.mutation<AuthResponse, LoginRequest>({
      query: (body) => ({ url: '/auth/login', method: 'POST', body }),
    }),
  }),
});

export const { useLoginMutation } = authApi;
```

`src/pages/auth/useLogin.ts`:

```ts
import type { Location } from 'react-router';
import { useLocation } from 'react-router';
import type { LoginRequest } from '../../shared/api/types';
import { homeOf } from '../../shared/auth/roles';
import { getErrorMessage } from '../../shared/utils/getErrorMessage';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { setCredentials } from '../../store/slices/authSlice';
import { useLoginMutation } from './authApi';

/** What ProtectedRoute put in the location state: the page to return to after login (facility-0026 item 4). */
export interface LoginState {
  from?: Location;
}

/**
 * Shared by both login pages. Logging in only stores the session; the page then sees `redirectTo` and navigates,
 * so there is one way out of the login page whether the person just logged in or was already logged in.
 */
export function useLogin(wrongSecretText: string) {
  const [login, { isLoading, error }] = useLoginMutation();
  const dispatch = useAppDispatch();
  const user = useAppSelector((state) => state.auth.currentUser);
  const location = useLocation();
  const from = (location.state as LoginState | null)?.from;

  const submit = async (body: LoginRequest) => {
    try {
      dispatch(setCredentials(await login(body).unwrap()));
    } catch {
      // shown through errorText
    }
  };

  return {
    submit,
    isLoading,
    errorText: error ? loginErrorText(error, wrongSecretText) : null,
    returnsToPage: from !== undefined,
    redirectTo: user ? (from ? `${from.pathname}${from.search}` : homeOf(user.role)) : null,
  };
}

/** 401 is a wrong ID or secret, or a deactivated account; 429 is the login throttle (facility-0054). */
function loginErrorText(error: unknown, wrongSecretText: string): string {
  const status = (error as { status?: unknown }).status;
  if (status === 401) {
    return wrongSecretText;
  }
  if (status === 429) {
    return 'กรอกผิดหลายครั้ง รอสักครู่แล้วลองใหม่';
  }
  return getErrorMessage(error);
}
```

- [ ] **Step 4: Write the two pages, their CSS and the barrel**

`src/pages/auth/LoginPage.tsx`:

```tsx
import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation } from 'react-router';
import { buildCommit } from '../../shared/version/buildInfo';
import { useLogin } from './useLogin';
import './auth.css';

/** Wireframe M0: Cleaners and Supervisors log in with employee ID + phone on their phone (facility-0054). */
export function LoginPage() {
  const [employeeId, setEmployeeId] = useState('');
  const [phone, setPhone] = useState('');
  const { submit, isLoading, errorText, returnsToPage, redirectTo } = useLogin('รหัสพนักงานหรือเบอร์โทรไม่ถูกต้อง');
  const location = useLocation();

  if (redirectTo) {
    return <Navigate to={redirectTo} replace />;
  }

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    void submit({ employeeId: employeeId.trim(), phone });
  };

  return (
    <main className="auth-page">
      <header className="auth-heading">
        <span className="auth-kicker">ระบบลงเวลาและตรวจงานแม่บ้าน</span>
        <h1>เข้าสู่ระบบ</h1>
      </header>
      {returnsToPage && (
        <p className="auth-note auth-note--info">เข้าสู่ระบบก่อน แล้วระบบจะพากลับไปหน้าที่เปิดไว้ทันที</p>
      )}
      <form className="auth-form" onSubmit={onSubmit}>
        <div className="auth-fields">
          <label htmlFor="login-employee-id">รหัสพนักงาน</label>
          <input
            id="login-employee-id"
            type="text"
            inputMode="numeric"
            autoComplete="username"
            required
            value={employeeId}
            onChange={(event) => setEmployeeId(event.target.value)}
          />
          <label htmlFor="login-phone">เบอร์โทรศัพท์</label>
          <input
            id="login-phone"
            type="tel"
            inputMode="tel"
            autoComplete="current-password"
            required
            value={phone}
            onChange={(event) => setPhone(event.target.value)}
          />
          <span className="auth-hint">เบอร์ที่แจ้ง Admin ไว้ตอนสร้างบัญชี</span>
        </div>
        {errorText && (
          <p className="auth-note auth-note--error" role="alert">
            {errorText}
          </p>
        )}
        <button type="submit" className="auth-submit" disabled={isLoading}>
          {isLoading ? 'กำลังเข้าสู่ระบบ...' : 'เข้าสู่ระบบ'}
        </button>
      </form>
      <footer className="auth-footer">
        <span>เปลี่ยนเบอร์โทร: แจ้ง Admin ให้แก้ · ไม่มีการสมัครเอง</span>
        <span>
          Admin เข้าด้วยชื่อผู้ใช้และรหัสผ่าน ·{' '}
          <Link to="/login/admin" state={location.state}>
            หน้า Admin
          </Link>
        </span>
        <span className="auth-build">เวอร์ชัน {buildCommit}</span>
      </footer>
    </main>
  );
}
```

The phone is sent as typed. The server keeps only its digits (`PhoneNumber.Normalize`), so `000-000-0001` works.

`src/pages/auth/AdminLoginPage.tsx`:

```tsx
import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation } from 'react-router';
import { buildCommit } from '../../shared/version/buildInfo';
import { useLogin } from './useLogin';
import './auth.css';

/** Wireframe D0: the Admin logs in with username + password (facility-0054). */
export function AdminLoginPage() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const { submit, isLoading, errorText, returnsToPage, redirectTo } = useLogin('ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง');
  const location = useLocation();

  if (redirectTo) {
    return <Navigate to={redirectTo} replace />;
  }

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    void submit({ username: username.trim(), password });
  };

  return (
    <main className="auth-page auth-page--desk">
      <form className="auth-form auth-card" onSubmit={onSubmit}>
        <header className="auth-heading">
          <span className="auth-kicker">ระบบลงเวลาและตรวจงานแม่บ้าน</span>
          <h1>Admin เข้าสู่ระบบ</h1>
        </header>
        {returnsToPage && (
          <p className="auth-note auth-note--info">เข้าสู่ระบบก่อน แล้วระบบจะพากลับไปหน้าที่เปิดไว้ทันที</p>
        )}
        <label htmlFor="admin-username">ชื่อผู้ใช้</label>
        <input
          id="admin-username"
          type="text"
          autoComplete="username"
          required
          value={username}
          onChange={(event) => setUsername(event.target.value)}
        />
        <label htmlFor="admin-password">รหัสผ่าน</label>
        <input
          id="admin-password"
          type="password"
          autoComplete="current-password"
          required
          value={password}
          onChange={(event) => setPassword(event.target.value)}
        />
        {errorText && (
          <p className="auth-note auth-note--error" role="alert">
            {errorText}
          </p>
        )}
        <button type="submit" className="auth-submit" disabled={isLoading}>
          {isLoading ? 'กำลังเข้าสู่ระบบ...' : 'เข้าสู่ระบบ'}
        </button>
        <footer className="auth-footer">
          <span>
            แม่บ้านและหัวหน้าเข้าด้วยรหัสพนักงาน + เบอร์โทรบนมือถือ ·{' '}
            <Link to="/login" state={location.state}>
              หน้าเข้าสู่ระบบของพนักงาน
            </Link>
          </span>
          <span>ลืมรหัสผ่าน: ให้ Admin อีกคนรีเซ็ต</span>
          <span className="auth-build">เวอร์ชัน {buildCommit}</span>
        </footer>
      </form>
    </main>
  );
}
```

`src/pages/auth/auth.css`:

```css
/* Login pages, wireframes M0 and D0. Class prefix: auth- */

.auth-page {
  max-width: var(--phone-width);
  min-height: 100vh;
  margin: 0 auto;
  padding: 24px 20px;
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.auth-page--desk {
  max-width: none;
  align-items: center;
  justify-content: center;
}

.auth-heading {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.auth-heading h1 {
  font-size: 26px;
  font-weight: 700;
}

.auth-kicker {
  font-size: 13px;
  letter-spacing: 0.04em;
  color: var(--color-muted);
}

.auth-form {
  flex-grow: 1;
  display: flex;
  flex-direction: column;
  gap: var(--gap);
}

.auth-card {
  flex-grow: 0;
  width: 100%;
  max-width: 400px;
  gap: 12px;
  padding: 28px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
}

.auth-fields {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 16px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
}

.auth-form label {
  font-size: 15px;
  font-weight: 600;
}

.auth-form input {
  min-height: 48px;
  padding: 0 12px;
  font-size: 17px;
  border: 1px solid var(--color-input-border);
  border-radius: var(--radius-md);
  background: var(--color-surface);
}

.auth-hint {
  font-size: 12px;
  color: var(--color-muted);
}

.auth-note {
  padding: 10px 14px;
  border-radius: var(--radius-md);
  font-size: 14px;
}

.auth-note--info {
  background: var(--color-info-bg);
  border: 1px solid var(--color-info-border);
  color: var(--color-info-text);
}

.auth-note--error {
  background: var(--color-error-bg);
  border: 1px solid var(--color-error-border);
  color: var(--color-error-text);
  font-weight: 600;
}

.auth-submit {
  margin-top: auto;
  min-height: 72px;
  border: 0;
  border-radius: var(--radius-lg);
  background: var(--color-primary);
  color: var(--color-on-primary);
  font-size: 22px;
  font-weight: 700;
  cursor: pointer;
}

.auth-card .auth-submit {
  margin-top: 4px;
  min-height: 48px;
  font-size: 17px;
}

.auth-submit:disabled {
  opacity: 0.6;
  cursor: wait;
}

.auth-footer {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 13px;
  color: var(--color-muted);
  text-align: center;
}

.auth-build {
  font-size: 11px;
}
```

`src/pages/auth/index.ts`:

```ts
export { LoginPage } from './LoginPage';
export { AdminLoginPage } from './AdminLoginPage';
```

- [ ] **Step 5: Add the two routes**

In `src/router.tsx`, add the import below the other page imports:

```tsx
import { AdminLoginPage, LoginPage } from './pages/auth';
```

Then add the two routes right after `{ index: true, element: <HomeRedirect /> },`:

```tsx
      { path: 'login', element: <LoginPage /> },
      { path: 'login/admin', element: <AdminLoginPage /> },
```

- [ ] **Step 6: Run everything and measure the bundle**

```bash
npm test        # expect: 6 files, 41 passed
npx tsc -b
npm run lint
npm run build   # note the gzip size of dist/assets/index-*.js: must be ≤ 200 kB (the spike measured 127.15 kB)
```

- [ ] **Step 7: Check that the return-to-page test guards**

In `useLogin.ts`, change `(from ? \`${from.pathname}${from.search}\` : homeOf(user.role))` to `homeOf(user.role)`. Run `npm test` and confirm that "after login, returns to the page that was opened before it" fails. Then run `git checkout -- src/pages/auth/useLogin.ts`.

- [ ] **Step 8: Handoff entry and commit**

Handoff entry (put in the measured size):

```markdown
- <date> · Task 4 done: login pages /login (employee ID + phone, M0) and /login/admin (username + password, D0); wrong secret, throttle and server messages; return to the page opened before login; build commit shown. Frontend tests 41 passed; JS bundle <N> kB gzip. Next: Task 5 (the API serves the built frontend).
```

```bash
git add frontend/src docs/handoff.md
git commit -m "feat(frontend): login pages for staff (employee ID + phone) and Admin, returning to the page opened before login"
```

---

### Task 5: The API serves the built frontend (facility-0067)

**Files:**
- Modify: `backend/src/FacilityRealtime.Api/Program.cs`
- Modify: `backend/tests/FacilityRealtime.ApiTests/Infrastructure/FacilityApiFactory.cs`
- Modify: `backend/tests/FacilityRealtime.ApiTests/HarnessTests.cs:14`
- Create: `backend/tests/FacilityRealtime.ApiTests/FrontendHostingTests.cs`
- Modify: `frontend/package.json` (script `build:api`), `.gitignore` (repo root)
- Modify: `docs/handoff.md`

**Interfaces:**
- Consumes: the `npm run build` output from Tasks 1–4.
- Produces:
  - `GET /api/health` replaces `GET /`.
  - Page paths answer with `index.html` from `wwwroot`.
  - `FacilityApiFactory.WebRoot`, a `string?` init property for tests.
  - `npm run build:api`.

- [ ] **Step 1: Write the failing tests**

In `FacilityApiFactory.cs`, add the property after `ConfigureTestServices`:

```csharp
    /// <summary>Set to serve a built frontend from this folder instead of the project's wwwroot (facility-0067 tests).</summary>
    public string? WebRoot { get; init; }
```

Then, in `ConfigureWebHost`, add this right after `builder.UseSetting("Jwt:SigningKey", signingKey);`:

```csharp
        if (WebRoot is not null)
        {
            builder.UseWebRoot(WebRoot);
        }
```

In `HarnessTests.cs`, change `GetAsync("/")` to `GetAsync("/api/health")`.

Create `FrontendHostingTests.cs`:

```csharp
using System.Net;
using FacilityRealtime.ApiTests.Infrastructure;

namespace FacilityRealtime.ApiTests;

/// <summary>facility-0067: the API serves the built frontend from its own origin, and never in place of an API answer.</summary>
public sealed class FrontendHostingTests : IDisposable
{
    private const string IndexMarker = "<!-- facility frontend index -->";

    private readonly string _webRoot = Directory.CreateTempSubdirectory("facility-wwwroot-").FullName;

    public FrontendHostingTests()
    {
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), $"<!doctype html>{IndexMarker}<div id=\"root\"></div>");
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "assets", "index-abc123.js"), "console.log('app');");
    }

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    [Theory]
    [InlineData("/")]
    [InlineData("/login")]
    [InlineData("/scan/0b6f3c3e-2f7d-4c55-9d7e-1a2b3c4d5e6f")]
    [InlineData("/admin/accounts")]
    public async Task Page_paths_get_the_app(string path)
    {
        using var factory = new FacilityApiFactory { WebRoot = _webRoot };

        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(IndexMarker, await response.Content.ReadAsStringAsync());
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Built_assets_are_served_and_cached_for_good()
    {
        using var factory = new FacilityApiFactory { WebRoot = _webRoot };

        var response = await factory.CreateClient().GetAsync("/assets/index-abc123.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", response.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("/api/no-such-endpoint")]
    [InlineData("/api")]
    [InlineData("/hubs/no-such-hub")]
    [InlineData("/assets/missing.js")]
    public async Task Api_paths_and_missing_files_are_404_not_the_app(string path)
    {
        using var factory = new FacilityApiFactory { WebRoot = _webRoot };

        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(IndexMarker, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_wrong_method_on_an_api_route_is_still_405()
    {
        using var factory = new FacilityApiFactory { WebRoot = _webRoot };

        var response = await factory.CreateClient().DeleteAsync("/api/admin/audit-log");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task A_page_path_that_starts_with_api_letters_still_gets_the_app()
    {
        using var factory = new FacilityApiFactory { WebRoot = _webRoot };

        var response = await factory.CreateClient().GetAsync("/apiary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(IndexMarker, await response.Content.ReadAsStringAsync());
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~FrontendHostingTests|FullyQualifiedName~HarnessTests"`

Expected: FAIL.
- `Page_paths_get_the_app` gets 404.
- `Built_assets_are_served_and_cached_for_good` gets 404.
- `/apiary` gets 404.
- `Api_boots_on_sqlite_and_answers_the_healthcheck` gets 404 on `/api/health`.

- [ ] **Step 3: Change `Program.cs`**

1. Delete the whole CORS block, from the line `// 3. CORS (Allow local network devices & dev servers)` through the closing `});` of `builder.Services.AddCors(...)`, and the blank line after it. Renumber the comments that follow: `// 4. JSON Serialization ...` becomes `// 3. ...`, and `// 5. OpenAPI document ...` becomes `// 4. ...`.

2. Replace

```csharp
app.UseCors("AllowAll");
app.UseAuthentication();
```

with

```csharp
// facility-0067: the built frontend (frontend/dist, copied into wwwroot by `npm run build:api`) is served from this origin.
// index.html is revalidated on every load so a phone picks up a new build at once; the hashed assets/ files never change.
var appFiles = new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = context.File.Name == "index.html"
            ? "no-cache"
            : "public, max-age=31536000, immutable";
    },
};
app.UseDefaultFiles();
app.UseStaticFiles(appFiles);
app.UseAuthentication();
```

3. Replace

```csharp
app.MapGet("/", () => Results.Ok(new { status = "healthy", service = "Facility Real-time Dashboard API" }));
```

with

```csharp
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", service = "Facility Real-time Dashboard API" }));
```

4. After `app.MapAdminInspectionSummaryEndpoints();` add:

```csharp

// 9. Every other page path (/login, /scan/<token>, ...) gets the app, so a printed QR Sign opens it (facility-0026, 0067).
// Paths under /api and /hubs never do: they keep their own 404 and 405. A path with a file extension is a missing file, 404.
app.MapFallbackToFile("{*path:regex(^(?!(api|hubs)(/|$)).*$):nonfile}", "index.html", appFiles);
```

- [ ] **Step 4: Run the new tests, then the whole backend suite**

```bash
dotnet test backend/tests/FacilityRealtime.ApiTests --filter "FullyQualifiedName~FrontendHostingTests|FullyQualifiedName~HarnessTests|FullyQualifiedName~CorsTests|FullyQualifiedName~AuditLogEndpointTests"
dotnet test backend/FacilityRealtime.slnx
```

Expected: everything passes. Units 299. API 530 pass (519 + 11 new) and 5 are skipped.

`CorsTests` still passes: with no CORS at all, a cross-origin preflight never gets `Access-Control-Allow-Credentials`.

- [ ] **Step 5: Check that the fallback guard bites**

In `Program.cs`, temporarily change the route to `"{*path:nonfile}"` and rerun `FrontendHostingTests`. Expect `/api/no-such-endpoint`, `/api` and `/hubs/no-such-hub` to fail with 200 and the app's HTML. Then restore the regex.

- [ ] **Step 6: Add `build:api` and ignore its output**

In `frontend/package.json` `scripts`, add after `"build"`:

```json
    "build:api": "tsc -b && vite build --outDir ../backend/src/FacilityRealtime.Api/wwwroot --emptyOutDir",
```

Append to the root `.gitignore`:

```
# facility-0067: the built frontend, made by `npm --prefix frontend run build:api`
backend/src/FacilityRealtime.Api/wwwroot/
```

Then check it:

```bash
npm --prefix frontend run build:api
ls backend/src/FacilityRealtime.Api/wwwroot            # expect: index.html, assets/, favicon.svg, icons.svg
git status --short backend/src/FacilityRealtime.Api    # expect: only Program.cs
dotnet test backend/FacilityRealtime.slnx              # still green with a real wwwroot present
rm -r backend/src/FacilityRealtime.Api/wwwroot
```

- [ ] **Step 7: Handoff entry and commit**

Handoff entry:

```markdown
- <date> · Task 5 done: the API serves the built frontend from wwwroot (npm run build:api), index.html for every page path but never for /api or /hubs (they keep 404/405), index.html no-cache and hashed assets immutable; CORS AllowAll removed; health check moved to /api/health. Unit 299, API 530 + 5 skipped. Next: Task 6 (docs, ADR amendments, final checks).
```

```bash
git add backend .gitignore frontend/package.json docs/handoff.md
git commit -m "feat(backend): serve the built frontend from one origin with an index.html fallback outside /api and /hubs; remove CORS AllowAll; health at /api/health"
```

---

### Task 6: Docs, ADR amendments and final checks

**Files:**
- Modify (amendment appended at the end of each): `docs/adr/facility-0004-app-architecture.md`, `facility-0025-frontend-target-tree.md`, `facility-0026-frontend-router-shape.md`, `facility-0062-phone-bundle-budget-200kb-gzip.md`, `facility-0063-frontend-css-global-tokens-per-page-files.md`, `facility-0065-frontend-eslint-boundaries-and-light-pre-commit.md`
- Modify (banner under the title): `docs/superpowers/plans/2026-09-13-phase2b-accounts-page.md`, `docs/superpowers/plans/2026-09-13-phase2c-points-admin-page.md`
- Modify: `README.md`, `frontend/README.md` (rewrite), `docs/architecture.md`, `docs/handoff.md`

**Interfaces:** none. Docs only, plus a full check.

- [ ] **Step 1: ADR amendments**

Append each block to the end of its ADR, after a blank line. The ADRs are written in Thai, so the amendments are too. Use the editor tools to write them.

`facility-0004-app-architecture.md`:

```markdown
> **แก้ไข 2026-10-15 (แผน 7, #37):** ข้อ 3 ไม่ตรงกับของจริง frontend ไม่ใช้ MUI และไม่ใช้ skill `react-structure` โครงจริงเป็นแบบ MenuNest: `pages/` + `shared/` + `store/` (facility-0025), Redux Toolkit + RTK Query (facility-0022 ถึง 0024), React Router 7 (facility-0026), CSS ธรรมดาต่อหน้า (facility-0063) และ Base UI เมื่อหน้าแรกต้องใช้ Dialog หรือ Toast ข้อ 5 (Vite proxy) ยังใช้ตอนพัฒนา ส่วนระบบจริง API เสิร์ฟ frontend เองจาก origin เดียว (facility-0067)
```

`facility-0025-frontend-target-tree.md`:

```markdown
> **แก้ไข 2026-10-15 (แผน 7):** ไม่ได้ย้ายตามลำดับในข้อ 2 เพราะหลังแผน backend 1–6 หน้าจอเดิมเรียก API ที่ไม่มีแล้ว (เช่น `currentStatus` และการ login ด้วย username อย่างเดียว) จึงไม่มีพฤติกรรมให้ตาข่าย #32 รักษา แผน 7 ลบ `features/`, `services/`, `types/`, `styles/` แล้วสร้าง tree ตามข้อ 1 ใหม่ทั้งหมด ส่วนที่เพิ่มจากข้อ 1: `store/slices/authSlice.ts` กับ `store/hooks.ts` (facility-0023), `shared/version/buildInfo.ts` (facility-0068), `shared/auth/roles.ts`, `src/test/` (ตัวช่วย test ที่ fake แค่ `fetch`) และหน้า `my-work`, `my-building` ตาม facility-0052 และ 0057 หน้า `scan`, `accounts` และ `points` มากับแผน 8 และ 10
```

`facility-0026-frontend-router-shape.md`:

```markdown
> **แก้ไข 2026-10-15 (แผน 7):** ตาราง route ตอนนี้: `/` พาไปหน้าแรกของบทบาท (แม่บ้าน `/my-work`, หัวหน้า `/my-building`, Admin `/dashboard` ตาม facility-0058) หรือไป `/login` ถ้ายังไม่ login; `/login` คือ M0 (รหัสพนักงาน + เบอร์โทร) และ `/login/admin` คือ D0 (ชื่อผู้ใช้ + รหัสผ่าน) แยกสองหน้าตาม wireframe และอยู่นอก `/admin/*` เพราะ guard คุม `/admin/*` ทั้งหมด; คนที่ยังไม่ login เปิดหน้า Admin ถูกพาไป `/login/admin`; ถ้ากด "ออกจากระบบ" เอง login ครั้งถัดไปไปหน้าแรกของบทบาท ไม่กลับหน้าเดิม (มือถืออาจใช้ร่วมกัน) แต่ถ้า session หมดอายุยังจำหน้าเดิมตามข้อ 4; `/scan/:token` และ route ของหน้า Admin มากับแผน 8–11
```

`facility-0062-phone-bundle-budget-200kb-gzip.md` (put in the size you measured in Task 4):

```markdown
> **วัด 2026-10-15 (แผน 7):** JavaScript ไฟล์เดียว <N> kB gzip หลังเพิ่ม Redux Toolkit, React Router 7 และหน้า login (ยังไม่มี Base UI และยังไม่ได้ import SignalR) ยังอยู่ในงบ 200 kB
```

`facility-0063-frontend-css-global-tokens-per-page-files.md`:

```markdown
> **แก้ไข 2026-10-15 (แผน 7):** ไม่มี component ไหนถูกย้าย (ดู facility-0025 แก้ไข 2026-10-15) จึงไม่มีช่วงสไตล์สองแบบตามข้อ 5 ทุกหน้าเขียนสไตล์ใน CSS file ตั้งแต่แรก token ใน `index.css` มาจาก wireframe (พื้นสว่าง สีหลัก `#0E6B66` ฟอนต์ IBM Plex Sans Thai จาก `@fontsource` บน origin เดียวกัน ไม่โหลดจาก Google Fonts) class ของ component ใน `shared/components` ขึ้นต้นด้วย `app-`
```

`facility-0065-frontend-eslint-boundaries-and-light-pre-commit.md`:

```markdown
> **แก้ไข 2026-10-15 (แผน 7):** eslint-plugin-boundaries 7 ประกาศ element ได้แค่โฟลเดอร์ ไฟล์ `main.tsx`, `App.tsx`, `router.tsx` จึงจัดเป็นไฟล์กลุ่ม `app` ผ่าน `boundaries/files` แทน element ทิศที่อนุญาตเหมือนข้อ 2 ทุกข้อ `boundaries/no-unknown-files` เป็น error ตั้งแต่แรกเพราะไม่มีโฟลเดอร์เก่าเหลือ ไฟล์ test และ `src/test/` อยู่นอกกติกานี้
```

- [ ] **Step 2: Mark plans 2B and 2C superseded**

Insert this block right under the `# ...` title line of both `2026-09-13-phase2b-accounts-page.md` and `2026-09-13-phase2c-points-admin-page.md`:

```markdown
> **Superseded (2026-10-15, plan 7):** written for the phase-2 frontend and API, both since replaced. The accounts and points pages are rebuilt in frontend plan 10 against the facility-0025 tree and the plan-3 and plan-5 endpoints. Kept as history only; do not execute it.
```

- [ ] **Step 3: `README.md`**

1. Delete the blockquote that starts `> **หมายเหตุสำหรับ branch นี้ (\`feat/pilot-data-model\`):**`, which is now false.
2. Replace the `- **Frontend**: ...` bullet with:

```markdown
- **Frontend**: Vite + React 19 + TypeScript, Redux Toolkit + RTK Query, React Router 7, CSS ธรรมดาต่อหน้า (`frontend/`, โครงตาม facility-0025) ระบบจริง API เสิร์ฟหน้าเว็บเองจาก origin เดียว (facility-0067)
```

3. Under `### 4. รัน Frontend (Vite React)`, after the existing code block, add:

````markdown
ครั้งแรกที่ `npm install` จะติดตั้ง pre-commit hook (husky) ซึ่งรัน `npm run lint`, `npx tsc -b` และ `npm test` ของ frontend ทุกครั้งที่ commit

ให้ API เสิร์ฟหน้าเว็บเอง (แบบระบบจริง ไม่ต้องรัน Vite):
```bash
npm --prefix frontend run build:api   # build ลง backend/src/FacilityRealtime.Api/wwwroot (git ไม่เก็บ)
dotnet run --project backend/src/FacilityRealtime.Api --urls "http://0.0.0.0:5001"
# เปิด http://<IP ของเครื่อง>:5001/login  ตรวจว่า API ทำงานที่ /api/health
```
````

- [ ] **Step 4: Rewrite `frontend/README.md`**

Replace the whole file with:

````markdown
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

The access token lives only in the Redux store. The refresh token is an HttpOnly cookie the page never sees. On page load one refresh restores the session; on a 401 every request shares one refresh and retries once. "ออกจากระบบ" logs out on the server and empties the cache.

## Tests

Tests render the whole app at a URL with `renderApp` and fake only `fetch` with `fakeServer`. Find elements the way a person would: by role, label or text.
````

- [ ] **Step 5: `docs/architecture.md`, the component diagram**

In section 2, replace the whole `subgraph FE[...] ... end` block with:

```
    subgraph FE["Frontend — frontend/src (React 19 + TypeScript, tree of facility-0025)"]
        AppF["main.tsx · App.tsx · router.tsx<br/>store, page-load refresh, createBrowserRouter"]
        Pages["pages/*<br/>auth (M0, D0) · my-work · my-building · dashboard<br/>each: Page.tsx + pageApi.ts (injectEndpoints) + page.css"]
        Store["store/<br/>authSlice: user + access token in memory"]
        BaseApi["shared/api/baseApi.ts<br/>RTK Query, Bearer header,<br/>401 → one shared refresh → retry"]
        Session["shared/auth<br/>session.ts (refresh, single-flight)<br/>endSession.ts (logout)"]
        Guard["shared/components<br/>ProtectedRoute (role), AppLayout,<br/>StatusScreen, CrashScreen"]
    end
```

Then replace the lines from `App --> Dash & Scan & Login` through `SR -->|"WebSocket /hubs/scan?access_token=…"| Hub` with:

```
    AppF --> Pages & Guard & Store
    Pages --> BaseApi
    Pages --> Store
    Guard --> Store
    BaseApi --> Session
    Session --> Store
    Session -->|"POST /api/auth/refresh | logout<br/>(cookie facility_refresh)"| AuthEp
    BaseApi -->|"/api/auth/login and /api/*<br/>Authorization: Bearer"| Ep
```

Under the paragraph "Why the proxy matters ..." in section 1, add:

```markdown
In production the Vite server is not used: `npm run build:api` puts the built app in the API's `wwwroot`, and the API serves it from the same origin, with `index.html` for every page path outside `/api` and `/hubs` ([facility-0067](adr/facility-0067-api-serves-frontend-from-one-origin.md)). No page opens the SignalR hub yet; the Supervisor and Admin plans (9, 11) add it.
```

- [ ] **Step 6: `docs/handoff.md`**

1. Add a row to the Status table:

```markdown
| 7 | Frontend foundation: tree, store, API layer, session, router and guard, login pages, served by the API | done (branch `feat/frontend-foundation`): `docs/superpowers/plans/2026-10-15-frontend-foundation.md` |
```

2. Under `## Pending work`, replace the line that starts `- Frontend: plan 7 (foundation) is written` with:

```markdown
- Frontend plans 8–11 (scopes in the plan-7 file): 8 Cleaner phone, 9 Supervisor phone, 10 Admin setup, 11 Admin live board.
```

3. In `## Notes for the frontend plan`, add at the top:

```markdown
- Plan 7 built the base: add a page as `src/pages/<page>/` with `<page>Api.ts` (`baseApi.injectEndpoints`), a `.css` with the page prefix and an `index.ts`; add its route in `router.tsx` under the right `ProtectedRoute`; test with `renderApp` + `fakeServer`.
- The QR Sign URLs are not decided yet: plan 8 decides the check-in sign's path (`by-token` answers 404 for a check-in sign) and plan 10 prints whatever plan 8 chose.
```

- [ ] **Step 7: Final full check**

```bash
cd frontend && npm test && npx tsc -b && npm run lint && npm run build && cd ..
dotnet test backend/FacilityRealtime.slnx
grep -rn "style={" frontend/src || echo "no inline styles"
grep -rn "localStorage\|sessionStorage" frontend/src || echo "no web storage"
```

Expected:
- Frontend: 41 tests pass, typecheck and lint are clean, and the JS bundle is ≤ 200 kB gzip.
- Backend: 299 unit tests pass; API 530 pass and 5 are skipped.
- Both greps print the "no ..." line.

- [ ] **Step 8: Handoff entry and commit**

```markdown
- <date> · Task 6 done: ADR amendments (0004 frontend section #37, 0025 rebuild not move, 0026 routes, 0062 measured size, 0063 tokens, 0065 boundaries 7), plans 2B/2C marked superseded, README, frontend README, architecture diagram. Frontend 41 tests; unit 299; API 530 + 5 skipped. Next: the controller's real run, then the whole-branch review.
```

```bash
git add docs README.md frontend/README.md
git commit -m "docs: frontend foundation in README, architecture and handoff; ADR amendments for the rebuild, routes, tokens and boundaries"
```

- [ ] **Step 9 (controller only, not a subagent): real run on a throwaway database**

Ask the owner for the MySQL root password. Use it only in the commands below, and never write it to a file.

```bash
mysql -u root -p -e "CREATE DATABASE facility_frontend_test CHARACTER SET utf8mb4;"
npm --prefix frontend run build:api
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=facility_frontend_test;Uid=root;Pwd=<typed by the owner>;CharSet=utf8mb4;" \
dotnet run --project backend/src/FacilityRealtime.Api --urls "http://0.0.0.0:5001"
```

In a second terminal:

```bash
curl -s http://localhost:5001/api/health                                # {"status":"healthy",...}
curl -s http://localhost:5001/login | grep -c '<div id="root">'         # 1
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5001/api/nope # 404
```

Ask the owner to open `http://<Mac IP>:5001/login` on a phone and check three logins with the seed accounts in `README.md`:
- A Cleaner lands on "งานของฉัน".
- A Supervisor lands on "ตึกของฉัน".
- The Admin, through "หน้า Admin", lands on "สถานะจุดทำความสะอาด".

Then check that "ออกจากระบบ" returns to the plain login page.

When done, stop the API, delete the build output and drop the database:

```bash
rm -r backend/src/FacilityRealtime.Api/wwwroot
mysql -u root -p -e "DROP DATABASE facility_frontend_test;"
```

Record the result in the handoff (Task log), commit it, then run the whole-branch review.
