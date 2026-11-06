# Project File and Folder Rename Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use sp-subagent-driven-development or sp-executing-plans when applicable. This is one tightly coupled mechanical rename; execute it as a single transaction with manual implementation and an independent review. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Align project-owned source files and folders with Janitorial Management Dashboard and keep builds, tests and operational commands usable.

**Architecture:** Rename the .NET solution, seven project folders/project files and their matching namespaces together. Rename the operational scripts and design-system folder, then update project references, resources, scripts and documentation. Complete source edits and the main verification before moving the active root folder, then regenerate and verify build paths from its new location. The root move requires a separate filesystem permission boundary.

**Tech Stack:** .NET 10, EF Core, React/TypeScript/Vite, Bash, Python.

**Spec:** Owner request on 2026-11-06: “เปลี่ยนชื่อไฟล์และ Folder อื่นๆ ให้สอดคล้องกันด้วย”; canonical name in `docs/adr/facility-0113-canonical-project-name.md`.

## Global Constraints

- Preserve all pre-existing display-name edits, manual handoff notes and `Manual/` bytes.
- .NET basename/namespace: `JanitorialManagementDashboard`; script basename: `janitorial`; design-system folder: `design-system/janitorial`; root folder: `Janitorial-Management-Dashboard`.
- Keep stable ADR IDs/prefix `facility-`, map slug `facility-poc` and GitHub repository URLs, as specified by AGENTS.md. No GitHub repository rename is requested.
- Keep live database names, migration IDs/schema, user-secrets ID, JWT issuer/audience, deployment paths/service account/journal namespace and existing `FACILITY_*` configuration keys compatible. This request renames files, not deployed state or stored data.
- Do not expose private settings, reset/migrate the owner's database, commit, push or merge. Use a local feature branch for the rename.
- Root move follows the main verification; rebuild and check paths from the new location before final handoff. Do not edit Codex's internal project registry. Report the registered-workspace reopening needed.

---

### Task 1: Rename the coupled source tree and repair all references

**Files:**
- Rename `backend/FacilityRealtime.slnx` to `backend/JanitorialManagementDashboard.slnx`.
- Rename every `FacilityRealtime.*` project directory and `.csproj` under `backend/src/`, `backend/tests/`, `backend/tools/` to `JanitorialManagementDashboard.*`.
- Rename `backend/tests/FacilityRealtime.ApiTests/Infrastructure/FacilityApiFactory.cs` to `backend/tests/JanitorialManagementDashboard.ApiTests/Infrastructure/JanitorialApiFactory.cs` and its type/call sites.
- Rename `scripts/facility.sh` and `scripts/facility.test.sh` to `scripts/janitorial.sh` and `scripts/janitorial.test.sh`.
- Rename `design-system/facility/` to `design-system/janitorial/`.
- Update tracked text references in source, configuration, scripts, guides, local source links and plans. Preserve external repository identity and stable configuration values.
- Update `AGENTS.md`, `docs/agents/frontend-standards.md`, ADR 0113 and `docs/handoff.md` for the new paths and scope.
- Update frontend package/lockfile root package names to `janitorial-management-dashboard` without changing dependency versions.

**Interfaces:**
- Consumes the existing application/CLI contracts with no feature changes.
- Produces `JanitorialManagementDashboard.Api.dll`, `.E2ESeed` and the new solution/project paths; frontend `build:api`, E2E runner and shell runner must use those paths.

- [x] Inventory tracked names, preserve a before-patch and manual hashes privately, then create `codex/rename-project-files` while retaining current changes.
- [x] Apply these literal mappings to tracked text (retain BOM and line endings): `FacilityRealtime` → `JanitorialManagementDashboard`; `FacilityApiFactory` → `JanitorialApiFactory`; `facility.test.sh` → `janitorial.test.sh`; `facility.sh` → `janitorial.sh`; `design-system/facility` → `design-system/janitorial`.
- [x] Rename the actual files/folders to match the mappings, with a collision check before each move; update frontend package/lockfile package names together.
- [x] Record the naming extension in ADR 0113 and replace the earlier handoff restriction with a newer, explicitly superseding entry.
- [x] Run frontend lint/build/546-test suite; Bash syntax and all shell runner tests; Python syntax for the E2E runner; .NET solution build/full unit+API suites and E2E seed build. MySQL-only tests may be skipped when no isolated database is configured, and must be reported as skipped.
- [x] Check project references/solution entries/local links, embedded common-password resource, unchanged migration IDs and metadata compatibility, manual hashes, dependency versions and `git diff --check`; obtain an independent read-only review.
- [x] Save validation in handoff, then rename the active root folder to `Janitorial-Management-Dashboard` with the required filesystem escalation. Verify the final filesystem paths and explain how to reopen the project.

## Plan self-review

One task owns all shared paths and namespace/resource interfaces, so no intermediate state is a deliverable. The requested file/folder alignment, compatibility constraints, root-folder boundary, validation and handoff each have a concrete step. No application behavior or new regression tests are introduced by the rename; existing suites and structural checks verify it.

## Completion evidence

Completed on `codex/rename-project-files`, with no commit/push/merge. The root directory is `/Users/macbookair/Projects/Janitorial-Management-Dashboard`. Frontend lint/build/546 tests, .NET 357 unit + 702 API tests (6 MySQL skipped), E2E seed build, clean publish, shell suite and structural/integrity checks passed. Independent read-only review approved without findings. Post-move frontend rebuild, 32 focused .NET tests, shell suite and all 45 project tool symlinks passed. Removed only obsolete old-named generated files from project `bin/obj`; retained historical run artifacts. See `docs/handoff.md` for operational and reopening details.

## Publication follow-up

On 2026-11-06 the owner authorized committing, pushing and merging this rename. The follow-up review's P2 upgrade/rollback documentation issue is fixed in handover section 2.8 and independently re-reviewed; synthetic old/new-DLL upgrade/rollback checks pass. Earlier completion notes above describe the pre-publication state. See the latest handoff entry for actual publication status. The pre-existing local Word manual is excluded from this source change.
