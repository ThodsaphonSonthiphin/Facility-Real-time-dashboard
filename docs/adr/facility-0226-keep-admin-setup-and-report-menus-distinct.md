# Keep Admin setup and report menus distinct

```mermaid
flowchart TD
    Q{"Should Admin setup (5 pages) or report (2 pages) menus be merged into tabbed pages?"}
    Q -->|chosen| A["Keep all 5 setup pages and 2 report pages as distinct sidebar entries"]
    Q -->|rejected| B["Merge Buildings/Areas/Print into a single tabbed Location page (hides direct URLs and deepens clicks)"]
    Q -->|rejected| C["Merge Inspection Summary and Excel Export into one Reports page (mixes coverage view with PDPA-gated export)"]
```

On 19 November 2026 the owner decided to keep the five Admin setup entries (`พื้นที่และป้าย`, `ตึก`, `พิมพ์ป้าย`, `บัญชีผู้ใช้`, `ประวัติการแก้ไข`) and two report entries (`สรุปการตรวจ`, `ส่งออก Excel`) distinct in the sidebar, as each has a clear, non-overlapping purpose once Dashboard/Overview and Attendance/Alerts/Schedules are cleanly separated.
