# Simplify Cleaner bottom navigation to two real tabs

```mermaid
flowchart TD
    Q{"Which tabs should appear in CleanerBottomNav?"}
    Q -->|chosen| A["Two real destination tabs: งานของฉัน (/my-work) and ตารางของฉัน (/my-schedule)"]
    Q -->|rejected| B["Keep 4 tabs including non-camera สแกน QR guidance and duplicate บัญชี tab"]
    Q -->|rejected| C["Remove bottom navigation entirely and use top segmented tabs"]
```

On 19 November 2026 the owner decided to simplify `CleanerBottomNav` from four tabs to two real navigation tabs: **งานของฉัน** (`/my-work`) and **ตารางของฉัน** (`/my-schedule`). The **สแกน QR** tab (which could not open a camera and only showed inline guidance) and the **บัญชี** tab (which duplicated the top header's user identity and sign-out action) are removed so every control performs a real navigation action.
