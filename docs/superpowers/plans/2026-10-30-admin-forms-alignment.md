# Admin forms and confirmations alignment

Owner authorized implementation in chat on 2026-10-30 after the six-item gap audit. Base: cf197f2. Reference: approved Downloads HTML, decoded templates in /private/tmp/facility-form-references/. Traceability: #120 Enterprise Light.

## Global Constraints

- Match the approved six form/dialog references, preserving existing business rules and API contracts. No backend changes or new dependencies. Never edit owner .csproj or backend/.idea, unrelated Export comments, or previously merged page layouts.
- Tailwind tokens/shared controls; Admin body14px, minimum13px; targets>=36px; visible labels/focus; proper error/loading/disabled states, focus trap/Escape/opener restoration; no document overflow at375px. Extract modified form state/handlers into neighboring hooks, following current repo layout, not MUI.
- Preserve existing exceptions: no random passwords/forced password change; no automatic reassignment; no cover replacement; no invented counts/phone values or timing promises. Keep current validation limits and mutation semantics. Do not implement unsupported session-reset endpoint from the mockup; document this limitation explicitly.
- Only root runs full suite at integration; workers run focused tests/typecheck. Commit only scoped files on current feat/ui-admin-setup, reference #120; do not push/merge. Preserve existing dirty files. Root updates handoff and performs browser checks with mocked API and no real mutations.

### Task 1: Area and point forms

Align AreaForm/AreaEditorDrawer and PointEditor to area.html and point.html. Group area identity, shifts/cleaners and check-in sign sections, show actual sign count/reprint advice for name change; after a successful changed-name area save, navigate to the existing /admin/print?area=<id> route with the reference's save-and-print button (never navigate on failure). Preserve sign tools and active restrictions. Align point round editor to numbered editable rows with shift header/toggle when supported by existing DayOnly/DayAndNight model. No per-point shift toggle if API cannot represent it; empty round list keeps existing semantics. Support editing existing window times without silently losing drafts; retain numeric HH:mm and overlap/server validations. Place sign radius visibly per reference while retaining existing sign save semantics (do not pretend atomic combined save). Existing location/QR tools remain available in labelled details. Add behavior tests at existing renderApp seam for modified rows/drafts and preserved payload; first failing test before new round-edit behavior. Typecheck/focused tests. Commit scoped changes and report.

### Task 2: Account forms

Align create/edit/phone/password forms with account.html visual hierarchy, role choice cards/descriptions, grouped identity and role-dependent assignment sections, clear error states and footer. Fixed role/employee ID stay fixed on edit. Keep existing no-phone-exposure and password rules, typed password rather than mockup-generated credentials. Preserve all actions and meaningful explanatory content under labelled details when appropriate. Extract state into hooks, use existing Drawer shell. Add focused tests for existing constraints/payload and changed interactions at renderApp seam, then typecheck/focused tests. Commit scoped files/report.

### Task 3: Confirmations

Implement shared accessible confirmation modal styled to confirmation.html; neutral/accent vs destructive solid-red button, contextual icon, title/body/impact list, optional typed code and print-followup checkbox. State in hook. Integrate existing actions: account deactivation, QR regeneration, Area/Building deactivation, cover cancellation (both existing cover UIs), and point deactivation if present. Preserve existing guards and error/retry/sending behavior. High-impact Area/Building deactivation requires actual entity code; impact counts must be actual available data or omit unavailable counts. Do not add unsupported login-reset API/action. Avoid unsafe nested dialog handling: verify Escape and opener restoration. Tests: no mutation until confirm, cancel/Escape no request, code guard, error retry, QR print followup, cover cancellation path. First red test before implementation. Typecheck/focused tests and commit/report.

### Task 4: Integration and verification

Recheck CorrectionForm and CoverDrawer against correction.html/cover.html with previously approved exceptions. Fix concrete gaps only. Independently review each task for spec and quality before next implementation; one broad final review plus two-axis code-review. Full frontend tests/lint/typecheck/build once at end (hooks may repeat required checks), browser 1280/375 for all six groups and accessible confirmation states using fake API, save screenshots/results. Owner visual acceptance stays pending. Record exact omissions/exceptions and actual results in handoff, commit. Do not claim finished if reference groups remain unchecked.
