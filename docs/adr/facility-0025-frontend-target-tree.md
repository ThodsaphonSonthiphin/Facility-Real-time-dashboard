# Frontend Target Tree — pages/shared/store แบบ MenuNest และลำดับย้ายไฟล์ทีละชั้นแล้วทีละหน้า

```mermaid
flowchart TD
    Q{"ย้าย frontend ไปโครงแบบ MenuNest — ไฟล์ไหนอยู่โฟลเดอร์ไหน<br/>และย้ายลำดับใดให้ทุก commit ยัง build ผ่านและ test ผ่าน"} -->|chosen| A["A: src/{main,App,router,index.css} + store/ + pages/{auth,dashboard,scan,accounts,points} + shared/{api,auth,components,hooks,utils}<br/>scanHub อยู่ shared/api, authSession อยู่ shared/auth, accounts กับ points เป็นสองหน้าพี่น้อง<br/>ย้ายทีละชั้น: deps+store -> shared (คง re-export ชั่วคราว) -> Dashboard -> scan -> login -> router -> ลบของเก่า"]
    Q -->|rejected| B["B: ย้ายทีเดียวจบใน commit เดียว:<br/>เขียนเร็วที่สุด แต่แอปพังระหว่างทาง, diff ก้อนเดียวรีวิวไม่ไหว และถ้าพังไม่รู้ว่าการย้ายไหนทำ"]
    Q -->|rejected| C["C: ย้ายหน้าสแกนก่อน Dashboard:<br/>หน้าเล็กกว่า ผิดแล้วถูกกว่า แต่ pattern ที่ยากจริง (hub patch cache + poll + tag) ไปโผล่ทีหลัง ตอนที่หน้าอื่นลอกรูปแบบไปแล้ว"]
    Q -->|rejected| D["D: เก็บ scanHub ไว้ใน pages/dashboard:<br/>วันนี้มีหน้าเดียวที่ใช้จริง แต่ #26 ให้เปิด hub จาก API slice ของหน้านั้น การให้หน้า import จาก shared รักษาทิศ shared -> pages ไว้เมื่อมีหน้าที่สองมาใช้"]
    Q -->|rejected| E["E: รวมเป็น pages/admin โฟลเดอร์เดียวสำหรับ 2B และ 2C:<br/>แผนทั้งสองอธิบายสองหน้าจอที่ใช้ layout ร่วมกัน ซึ่ง layout เป็นเรื่องของ shared/components ไม่ใช่เหตุผลให้ยุบสองหน้าเป็นหนึ่ง"]
```

- Decision map: [#22 target-tree](https://github.com/ThodsaphonSonthiphin/Facility-Real-time-dashboard/issues/22) บนแผนที่ [#21](https://github.com/ThodsaphonSonthiphin/Facility-Real-time-dashboard/issues/21)
- วันที่: 2026-09-16
- เกี่ยวข้อง: facility-0022 (API slice ต่อหน้า — ไฟล์ที่ tree นี้วางตำแหน่งให้), facility-0004 (ส่วน frontend ยังผิด), #26 hub-cache-bridge, #23 router-shape (เนื้อใน router.tsx), #31 css-organisation (การแตก index.css), #32 regression-net (ตาข่ายที่ต้องมีก่อนย้าย)

## Context & Decision

แผนที่ #21 ตัดสินแล้วว่าใช้โครงแบบ MenuNest (`pages/` + `shared/` + `store/` + barrel ต่อหน้า) ตั๋วนี้ระบุ tree จริงของแอปนี้ และลำดับการย้าย

**1. Tree**

```
src/
├── main.tsx · App.tsx · router.tsx · index.css
├── store/index.ts                  store เดียว, slice ของแต่ละหน้ามาลงทะเบียนที่นี่
├── pages/
│   ├── auth/                       LoginPage.tsx · index.ts
│   ├── dashboard/                  DashboardPage.tsx · ServicePointCard.tsx · KpiSummaryBar.tsx
│   │                               useDashboard.ts · dashboardApi.ts · dashboardSlice.ts · index.ts
│   ├── scan/                       ScanRecordPage.tsx · IssueTagSelector.tsx
│   │                               useScanRecord.ts · scanApi.ts · scanSlice.ts · index.ts
│   ├── accounts/                   แผน 2B
│   └── points/                     แผน 2C
└── shared/
    ├── api/                        baseApi.ts · types.ts · scanHub.ts
    ├── auth/                       authSession.ts (+ test)
    ├── components/                 AppLayout.tsx · ProtectedRoute.tsx · NavBar.tsx
    ├── hooks/                      useCurrentUser.ts
    └── utils/                      getErrorMessage.ts
```

`services/`, `types/index.ts` และ `features/` หายไปทั้งหมด

**2. ลำดับย้าย — ทีละชั้น แล้วทีละหน้า**

| ขั้น | ทำอะไร | สภาพหลังขั้นนี้ |
|---|---|---|
| 0 | `regression-net` (#32) เขียน component test ของ Dashboard และหน้าสแกนก่อน | มีตาข่าย |
| 1 | เพิ่ม Redux Toolkit และ `@base-ui/react`, สร้าง `store/index.ts` กับ `shared/api/baseApi.ts` | ยังไม่มีใครใช้ แอปเดิมทำงานปกติ |
| 2 | ย้าย `authSession.ts`, `signalr.ts`, type ของ server ไปที่ใหม่ โดยคง re-export ที่ path เดิมชั่วคราว | import เดิมยังใช้ได้ |
| 3 | ย้ายหน้า Dashboard | pattern ทั้งชุด (page folder, API slice, tag, hub patch) พิสูจน์บนหน้าที่ยากที่สุด |
| 4 | ย้ายหน้าสแกน แล้วหน้า login | ทำซ้ำ pattern เดิม |
| 5 | เปลี่ยน if-chain ใน `App.tsx` เป็น `router.tsx` | เส้นทางจริงตาม #23 |
| 6 | ลบ `services/`, `types/index.ts`, `features/` และ re-export ชั่วคราว | เหลือ tree เดียว |

## Consequences

- ขั้น 2 ทำให้มี import path สองทางอยู่ชั่วคราว จนกว่าขั้น 6 จะลบทิ้ง — เป็นราคาที่จ่ายเพื่อไม่ให้ build พังกลางทาง
- ทุก commit ต้อง build ผ่านและ test ผ่าน ไม่มี commit ที่ "ย้ายครึ่งทาง"
- Dashboard ย้ายก่อน แปลว่าถ้ารูปแบบผิด จะเจอตั้งแต่หน้าแรก ไม่ใช่หลังจากหน้าอื่นลอกไปแล้ว
- `router.tsx` และการแตก `index.css` ยังเป็นของตั๋ว #23 และ #31 — tree นี้แค่จองที่ให้

> **แก้ไข 2026-10-15 (แผน 7):** ไม่ได้ย้ายตามลำดับในข้อ 2 เพราะหลังแผน backend 1–6 หน้าจอเดิมเรียก API ที่ไม่มีแล้ว (เช่น `currentStatus` และการ login ด้วย username อย่างเดียว) จึงไม่มีพฤติกรรมให้ตาข่าย #32 รักษา แผน 7 ลบ `features/`, `services/`, `types/`, `styles/` แล้วสร้าง tree ตามข้อ 1 ใหม่ทั้งหมด ส่วนที่เพิ่มจากข้อ 1: `store/slices/authSlice.ts` กับ `store/hooks.ts` (facility-0023), `shared/version/buildInfo.ts` (facility-0068), `shared/auth/roles.ts`, `src/test/` (ตัวช่วย test ที่ fake แค่ `fetch`) และหน้า `my-work`, `my-building` ตาม facility-0052 และ 0057 หน้า `scan`, `accounts` และ `points` มากับแผน 8 และ 10

> **แก้ไข 2026-10-15 (แผน 8):** เพิ่มหน้า `pages/check-in/` และ `pages/scan/` ของที่หลายหน้าใช้ร่วมกันอยู่ใน `shared/` เพราะหน้าห้าม import หน้าอื่น: `shared/api/myWorkApi.ts` (GET /api/my-work ใช้ทั้งหน้างานของฉัน หน้าลงเวลา และหน้าสแกน), `shared/api/scanAnswers.ts` (อ่านคำตอบ 403 ที่เป็น Blocked Scan และการส่งที่ไม่ถึงเซิร์ฟเวอร์), `shared/geo/readLocation.ts` (อ่าน GPS), `shared/utils/labels.ts` กับ `thaiTime.ts` และ component `StatusChip`, `LocationHelp` (M2), `BlockedScanScreen` (M8) ฝั่ง test เพิ่ม `src/test/fakeGps.ts` (fake `navigator.geolocation` แบบเดียวกับที่ fake `fetch`) และ `src/test/cleanerFixtures.ts`

> **แก้ไข 2026-10-16 (แผน 9):** เพิ่มหน้า `pages/my-building/` (M15) และ `pages/inspect/` (M6, M7, M16) ของที่ใช้ร่วมกันเพิ่มใน `shared/`: `shared/api/scanHub.ts` (`followScanHub` เปิด SignalR หนึ่ง connection ต่อ cache entry ใน `onCacheEntryAdded` ตาม #26 ใช้ทั้งหน้าตึกของฉันและ Dashboard ของแผน 11) และ `shared/components/ByRole.tsx` (ที่อยู่เดียว หน้าตามบทบาท) state ของการเชื่อมต่อ hub อยู่ที่ `store/slices/hubSlice.ts` ฝั่ง test เพิ่ม `src/test/fakeSignalR.ts` ที่แทน package `@microsoft/signalr` ทุก test ผ่าน `test.alias` ใน `vite.config.ts` (ตาข่าย #32 fake แค่ fetch กับ SignalR) และ `src/test/supervisorFixtures.ts`
