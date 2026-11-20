# ADR facility-0230: World-Class UX/UI Overhaul (10 Usability Heuristics, Mobile 3-Tab Shell, Admin 3-Group Drawer & M2 Schedule Controls)

- **Status:** Accepted and implemented (2026-11-20)
- **Amends:** `facility-0087`, `facility-0176`, `facility-0227`, `facility-0228`

## Context

Owner testing on real mobile and desktop screens identified scattered button placement and heuristic violations across Cleaner, Supervisor, and Admin pages:
- `LogoutButton` sat inline at the bottom of scrolling mobile content (`/my-work`, `/my-schedule`, `/my-building`, `/capture`) with no confirmation, while Desktop Supervisor and Admin placed logout in different locations.
- `/my-work` duplicated the schedule entry (`<ScheduleLink />` above the fold while `CleanerBottomNav` already had `ตารางของฉัน`), repeated shift info in both header and `ShiftBanner`, and pushed `NextJob` below the fold.
- `/my-schedule` rendered a redundant back link and stacked a 56 px primary Refresh bar on top of `CleanerBottomNav`.
- `SupervisorNav` floated mid-page on `/my-building` and jumped to the top bar on `/schedule-management`.
- `AdminNav` squeezed 12 links into a 2-column `w-80` popover on mobile and mixed live operations with scheduling/reports in a 7-item group.
- `SchedulePage` dumped 5–6 action buttons flat in the toolbar (crowding mobile viewports), lacked a `สัปดาห์นี้` shortcut, and had no close control on `ScheduleContextBar`.
- Admin pages diverged in header styling, filter placement (`InspectionSummaryPage` hid building/custom-date filters at the bottom below the table; `HistoryPage` hid search inside `<details>`), row actions (`PointTable` had two identical drawer triggers `แก้` and `จัดการ QR`), feedback (`AccountsPage` used inline `Note` instead of floating `Toast`), and jargon (`พารามิเตอร์หน้าไม่ถูกต้อง`).

## Decision

Owner reviewed interactive HTML mockups and approved the following architecture across all roles (with **no separate Help/FAQ button**, per owner instruction):

1. **Unified Mobile 3-Tab Bottom Navigation & Dedicated Account Tab (`/my-account`):**
   - Cleaner bottom nav (`เมนูหลักแม่บ้าน`): `งานของฉัน` (`/my-work`), `ตารางของฉัน` (`/my-schedule`), `บัญชี` (`/my-account`).
   - Supervisor mobile bottom nav (`เมนูหลักหัวหน้างาน`): `ตึกของฉัน` (`/my-building`), `จัดตารางงาน` (`/schedule-management`), `บัญชี` (`/my-account`); desktop (`>= 1024px`) retains top `SupervisorNav` + confirmed logout.
   - Delete inline `LogoutButton` everywhere; every sign-out across Cleaner, Supervisor, and Admin requires `LogoutConfirmDialog` (`role="alertdialog"`, `ยืนยันออกจากระบบ`).
   - **Focused Task Mode (`navMode="none"`):** `/scan/:token`, `/check-in/:token`, and `/admin/areas/:areaId/capture` hide bottom navigation so 56 px primary CTAs never double-stack.
2. **Above-the-Fold Mobile Focus & Interactive Filtering:**
   - `/my-work`: delete duplicate `<ScheduleLink />`, consolidate shift + progress into one compact card, move `อัปเดต` to top header, and surface `ConnectionBanner` at the top only when reconnecting.
   - `/my-schedule`: remove redundant back button and stacked Refresh `BottomBar`; add `สัปดาห์นี้` shortcut and top-right `อัปเดต`.
   - `/my-building`: remove mid-page `SupervisorNav`; turn the 3 summary cards (`รอตรวจ`, `รอแม่บ้านแก้`, `ตรวจผ่าน`) into interactive status toggle filters with `แสดงทุกสถานะ` reset.
   - `LocationHelp`: always offer a `กลับหน้าแรก` recovery link even on insecure HTTP or denied GPS.
3. **Admin Navigation (Option A — 3 Groups + Mobile Slide-over Drawer):**
   - Group all 12 Admin routes into `หน้างานวันนี้` (4), `ตารางและรายงาน` (3), and `ตั้งค่าระบบ` (5).
   - Replace the mobile 2-column popover with a full-height modal `<Drawer>` (`48px` single-column tap targets, Escape + focus restore, confirmed logout in footer).
4. **Schedule Toolbar (Desktop Option A + Mobile M2):**
   - Desktop (`>= 640px`): 1 primary CTA (`จัดวันหยุดรายสัปดาห์`) + 1 overflow menu (`จัดการเพิ่มเติม` for relief and emergency cover) + `สัปดาห์นี้` shortcut.
   - Mobile (`< 640px`, Option M2): full-width week strip with `กลับสัปดาห์นี้`, equal 2-column Admin building/shift selects, and 1 full-width primary button (`จัดการตารางงาน`) opening a modal Action Sheet of all management commands.
   - `ScheduleContextBar`: plain Thai label (`จัดการวันที่เลือก`) + `ปิด` button to deselect.
5. **Cross-Admin Consistency:**
   - Shared `AdminPageHeader` across all 12 Admin pages (preserving `PrintSignsPage` A4 sheet and QR geometry unchanged).
   - Move `InspectionSummaryPage` building & custom date filters up into the top `FilterToolbar`; add 1-click date presets (`วันนี้`, `7 วันล่าสุด`, `เดือนนี้`) to `ExportPage`; keep `HistoryControls` search always visible; remove duplicate `จัดการ QR` row button in `PointTable` and make mobile `AreaList` collapsible; add `ล้างตัวกรอง` inside Dashboard `FilterToolbar` and move `MoreInfo` to bottom; add `กลับกะปัจจุบัน` to Attendance `BoardControls`; switch `AccountsPage` mutation feedback to floating `Toast` and unify `แก้ไข` labels; replace `OverviewPage` jargon (`ลิงก์หน้านี้ไม่ถูกต้อง`, `ตัวกรองภาพรวม`) and add `<ChevronLeft />` to `OverviewAreaPage`.

## Consequences

- Every screen across Cleaner, Supervisor, and Admin follows one coherent shell, consistent confirmation/recovery rules, and zero duplicate navigation links in the accessibility tree.
- No backend API, database schema, auth contract, or print-sheet geometry changed.
