# Admin overview home and slim live point board

```mermaid
flowchart TD
    Q{"How should Admin Overview and Live Point Board coexist?"}
    Q -->|chosen| A["Overview as Admin home; slim Live Point Board to live cards only"]
    Q -->|rejected| B["Remove Live Point Board entirely (loses multi-area live monitor wall)"]
    Q -->|rejected| C["Keep Live Point Board as home and drop Overview (loses shift-wide summary and history)"]
```

On 19 November 2026 the owner decided to make **ภาพรวม** (`/admin/overview`) the Admin landing page (`homeOf('admin')`) while keeping **สถานะจุด** (`/dashboard`) as a slim live point-card monitor only. Duplicated building progress summaries and redundant overview widgets are removed from `/dashboard` so the two screens have clear, non-overlapping purposes: `/admin/overview` for shift-wide summaries, historical inspection, and drill-through; `/dashboard` for live real-time point cards.
