# Half-day departure: documented production-product patterns

Research date: 17 November 2026 (Asia/Bangkok). Requested by the owner before choosing the recommended 13:00 ShiftOut behaviour. Research initially preceded approval. The owner subsequently accepted the recommendation; see the current decision state below. No implementation authorisation.

## Evidence and its limits

These are documented product capabilities, not measurements of customer adoption or a universal industry standard. No Thai labour-law, payroll, vendor licensing, purchase or integration recommendation is made. Product lifecycle/retirement registries and market counts are not material to this comparison of documented workflow patterns, so they were not researched.

| Source | Verified fact | Grade |
|---|---|---|
| [Odoo 19 Attendance logs](https://www.odoo.com/documentation/19.0/applications/hr/attendances/attendance_logs.html) | Stores check-in/check-out date and time, distinguishes open from completed attendance, and presents errors for authorised users to resolve. | Verified-primary, official indexed page text; direct open timed out |
| [Odoo 19 Request time off](https://www.odoo.com/documentation/19.0/applications/hr/time_off/request_time_off.html) | Time Off records include half-day leave where the type permits it, as a distinct workflow from attendance recording. | Verified-primary, official indexed page text; direct open timed out |
| [SAP Processing Partial-Day Absences](https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/e9aa251b81eb4842bff926f04a251916/287dde537187424de10000000a174cb4.html) | Partial-day absence represents part of planned work and can carry start/end clock times; eligibility and processing depend on configuration. | Verified-primary, official indexed page text |
| [SAP Downloading Absence/Attendance Reasons](https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/c6c3ffd90792427a9fee1a19df5b0925/2aefdd5321e8424de10000000a174cb4.html?locale=en-US&version=LATEST) | If configured, a person can attach an absence reason to clock-in P10 or clock-out P20; processing can generate a partial-day absence record. This is a counterexample to claiming every product requires a separate leave button or separate manual entry. | Verified-primary, official indexed full section; direct open had no readable lines |
| [Factorial clock in/out API](https://apidoc.factorialhr.com/reference/post_api-2026-10-01-resources-attendance-shifts-toggle-clock) | Clock in/out accepts a timestamp and notes; break configuration is a separate parameter. | Verified-primary, direct page read |
| [Odoo Attendances settings](https://www.odoo.com/documentation/19.0/applications/hr/attendances.html) | Optional automatic checkout from the work schedule exists. This is a counterexample to claiming all production products prohibit automatic checkout. | Verified-primary, official indexed page text; direct open timed out |

The research sought alternative implementations, not just confirmation of our first suggestion. SAP supports reason-assisted event entry, while Odoo offers configurable automatic checkout. Vendor docs establish available mechanisms, not what most employers choose. A SAP Learning result failed to open and is not used as evidence. Secondary Reddit results and mirrored manuals were not used for technical conclusions.

## Recommendation for this project — inference, not a vendor mandate

Keep three concepts distinct:

1. Planned shift: e.g. 07:00–19:00.
2. Recorded agreed leave: e.g. 13:00–19:00 for this synthetic half-day example, entered by the Supervisor as already accepted in 0156. Do not hard-code a half day as six hours or take Odoo's example duration as this site's policy.
3. Actual attendance: ShiftIn at 07:00 and ShiftOut at actual departure, e.g. 13:00. Retain the recorded timestamps if they differ from the planned leave start.

For someone leaving for the rest of the shift, recommend the existing “เลิกงาน” action at actual departure. Show the recorded leave alongside it, rather than replacing the checkout time with scheduled shift end or adding leave-return scans for a workflow that does not occur (0196). Ending work submission on ShiftOut is an existing project rule, not a conclusion derived from a vendor's payroll product.

No automatic checkout or guessed attendance is recommended here: the project's existing decisions preserve missing actual times and require authorised corrections with reasons/provenance. Odoo's optional auto-checkout does not override those decisions. Similarly, do not copy a vendor's leave-request/approval or payroll workflow into this project; its Supervisor records already-agreed leave and the product tracks Area coverage/work.

**Additional detail, subsequently accepted in 0201:** for an agreed partial departure, future break actions after the actual end of work should be distinguished from forgotten scans during the worked interval. Example: after an authorised 13:00 departure, do not automatically report a 16:00 break departure as forgotten. Show it as not applicable due to the recorded leave/ended work, while retaining any genuinely missing pre-departure events. Exact applicability rules, leave revision timing and historical corrections need explicit spec/acceptance cases; do not silently ship this as a global change to MissingAtShiftEnd.

Do not infer from an early departure alone that leave was agreed. Preserve mismatches for authorised review. Borrowing, the day-off scheduling sequence, and urgent Area-transfer sign/attendance remain separate questions; this research does not resolve them.

## Decision state

After reviewing the sourced recommendation, the owner said “โอเคเอาตามนี้”. Actual-time final ShiftOut with separately recorded agreed leave is accepted in [0200](../adr/facility-0200-half-day-final-departure-records-actual-shift-out.md); later break inapplicability with preserved earlier missing events is accepted in [0201](../adr/facility-0201-leave-after-final-departure-makes-later-break-actions-inapplicable.md). The research/proposal paragraphs above preserve their original reasoning; neither acceptance authorises implementation or decides other outstanding workflows. Do not reopen the hourly-errand scenario.
