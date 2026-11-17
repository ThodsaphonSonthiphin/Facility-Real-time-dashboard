# Do not impose a synthetic week limit on schedule reading

```mermaid
flowchart TD
    Q["Scheduling design closeout"]
    Q -->|chosen| A["Allow calendar week selection and report real data/history availability"]
    Q -->|rejected| B["Treat mockup date bounds as a business horizon"]
```

On 2026-11-17 the owner confirmed the consolidated recap and requested the scheduling spec (“ยืนยันชุดนี้ เขียน spec ต่อ”). This records the corresponding accepted choice in [the closeout review](../requirements-gathering/2026-11-17-scheduling-design-closeout.md).

Read selection is not permission to mutate any chosen date. Missing history is unavailable, not reconstructed. Query one week at a time and apply existing retention and scoped authority.

Accepted design only. This does not approve application implementation, schema/database/runtime changes, deployment or publication. The scheduling spec will define the contracts and acceptance cases before an implementation plan is approved.
