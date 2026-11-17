# Recorded leave includes half-day and hourly absences

```mermaid
flowchart TD
    Q{"Which leave durations must the scheduling design support?"}
    Q -->|chosen| A["Full-shift and partial-shift leave<br/>Including half-day or selected hours"]
    Q -->|rejected| B["Full-shift leave only<br/>Defer partial-shift leave"]
```

On 2026-11-16 the owner selected B in the interview: this release must support half-day or hourly leave as well as full-shift leave. The Supervisor records the already-agreed leave under [facility-0156](facility-0156-supervisor-records-agreed-leave-without-request-workflow.md). Partial leave cannot be represented as absence for the entire shift without losing the distinction between working and absent periods.

The normal day/night shift hours in [facility-0150](facility-0150-cleaners-keep-full-day-and-night-shift-hours.md) remain the baseline; an agreed leave interval is an exception within that shift, not a new permanently shortened no-OT shift or a payroll rule.

How replacement work covers that interval remains unresolved. On 2026-11-16 the owner explicitly deferred this question because they do not know the field practice and will ask the Supervisor later; no whole-shift or split-period option was selected. The one-Area-for-the-whole-shift relief decision in [facility-0144](facility-0144-relief-cleaner-takes-one-area-for-the-whole-shift.md) is not silently revoked by accepting partial leave. Whether leave adds actual departure/return scans beyond the normal break lifecycle, how overnight intervals are entered, overlap rules, cancellation and post-start corrections remain to be designed.

This does not add Cleaner self-service requests or an approval workflow. Accepted design only; no application, schema, database or runtime changes have been made.
