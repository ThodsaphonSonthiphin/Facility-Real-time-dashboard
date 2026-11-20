# Twelve-category automated verification suite

```mermaid
flowchart TD
    Q{"Which of the 12 test categories should be automated in this plan?"}
    Q -->|chosen| A["All 12 categories: run existing 7 suites plus add automated Contract, Visual Regression, Axe Accessibility, Performance/Load, and Chromium+WebKit Compatibility suites"]
    Q -->|rejected| B["Skip Visual Regression and Load Testing until UI stabilizes — leaves 2 requested categories unverified"]
```

The repository already covers 7 of the 12 verification categories (Unit, Component, Integration, API, E2E, Regression, Security). We decided to close all 5 remaining gaps with repeatable automated suites (Contract, Visual Regression, Accessibility via axe-core, Performance/Load, and Chromium + WebKit Compatibility) alongside an Overview smooth-transition regression test so all 12 categories can be verified end-to-end.
