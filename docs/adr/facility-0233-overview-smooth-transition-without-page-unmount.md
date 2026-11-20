# Overview smooth transition without full-page unmount

```mermaid
flowchart TD
    Q{"How should /admin/overview render while fetching a new group, page, or filter?"}
    Q -->|chosen| A["Keep charts and previous table visible with subtle opacity and inline updating status; clear stale rows on error"]
    Q -->|rejected| B["Keep charts visible but replace table with skeleton on every click — still causes visible table flicker"]
    Q -->|rejected| C["Unmount entire page body and show full-page PageLoading — current behavior, feels like a browser reload"]
```

On `/admin/overview`, reading `query.currentData` directly caused `response` to become `undefined` on every group, bar, page, or filter click, unmounting all charts and tables and showing full-page `PageLoading`. We decided to keep the last valid snapshot rendered with subtle dimmed opacity and an inline updating indicator on `AreaResults` while a new context is fetching, while strictly clearing stale rows and showing an error + retry control if the new context request fails (preserving the no-stale-data-on-error invariant).
