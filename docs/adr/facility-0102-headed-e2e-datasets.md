# Headed E2E runs with owned databases and repeatable volume data

- Date:2026-10-31
- Authorization: owner's detailed E2E request in chat: visible Chromium,300–500ms slow motion, real API/database, configurable synthetic volume, retained evidence, no production data/business/API changes.
- Amends: facility-0094 points1–3 and its shared-wwwroot consequence. The real-API choice and one-worker policy remain.

Use the existing Playwright dependency and shell entry point, with a Python standard-library lifecycle runner. Each invocation publishes to its own ignored directory, creates a unique MySQL database and restricted random user, and verifies seeded readiness before browser tests. No existing database is reset. Cleanup requires the private manifest and matching database ownership marker, and removes a user only if this invocation successfully created it. Explicit cleanup names one owned run; legacy databases and stale PIDs are never adopted.

Chromium is headed by default,350ms slow motion. Retain screenshots and traces on every case, HTML/JSON reports, real response timings and generated-data validation. Automatic retries remain zero because repeated mutations need a newly seeded database.

A standalone test-only .NET tool references existing EF entities and validation without changing production startup, business logic, API contracts or migrations. Profiles small/medium/large add8/80/600 Areas and7/14/14 historical dates. The seed checks ownership and identity, uses only completed historical shifts, respects assignment and round constraints, and validates relationships, semantic distributions, future timestamps and repeat counts. Reference: `docs/testing/e2e.md`.

Existing business defects discovered by these tests are reported with evidence. Assertions are not weakened to obtain a pass; source fixes require separate owner approval. Browser volume timings are functional observations rather than concurrent-load guarantees.
