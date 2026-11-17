# Midshift Area transfers: production-pattern research and proposal

17 November 2026 (Asia/Bangkok). The owner did not know the sign/attendance practice for an urgent midshift move and requested a best-practice recommendation. **Subsequently accepted in 0203–0204; no implementation authorisation.** Accepted Supervisor authority in 0199 remains; deferred cross-building lending authority in 0194 is not resolved here.

## Verified primary evidence

| Source | Supported claim | Grade/limit |
|---|---|---|
| [UKG Punch tile](https://communityfiles.ukg.com/support/kol/onlinehelp-workforcedimensions/en-us/content/Timekeeping/MyTimeStamp.htm) | Separately lists clock in/out, starting a shift, and transferring time to another job/labor category/work rule; access controls govern transfer actions. | Verified-primary, direct official page read. Vendor capability, not universal practice. |
| [UKG Transfer Employees Hours](https://coach.ukg.com/cs/coach-demo-v45/content/d_transferemployeeshours.html) | Documents midshift partial transfers and a transfer start time, with generated timecard segments. | Verified-primary, direct official job-aid read. Its derived payroll segments are not raw evidence of physical Area departure/arrival. |
| [North Central Health Care UKG job aid](https://www.norcen.org/documents/UKG%20Access%202023%20Updates/Job-Aid-Clock-In-Transfer-and-Transfer.pdf) | Has a distinct Transfer procedure selecting job/program and confirming transfer time, separately from clock-out. | Verified-primary, direct read of employer's own operating instructions. Document last updated July 2023; evidence of a deployed workflow, not proof of its current unchanged configuration. |
| [UKG Multiple Assignments](https://communityfiles.ukg.com/support/kol/onlinehelp-workforcedimensions/en-us/content/PeopleInfo/Assignments.htm) | Distinguishes job transfers from separate person-level assignments; different assignments in one day require separate shifts in that product's model. | Verified-primary, direct official page read. Counter-signal: do not claim that every kind of work change in every product is one continuous shift. |

The useful pattern is an explicit authorised transfer, rather than pretending every move is ending the working day and starting a new one. The exact destination QR flow below is our design inference: these sources do not mandate GPS/QR, prove physical arrival automatically, or specify this site's break policy. No legal/payroll research, market-adoption statistics or product-purchase recommendation. Retirement registries and market counts are immaterial to this comparison and were not researched. No secondary forums used for technical conclusions.

## Proposed project flow — not yet accepted

1. The responsible Supervisor records A → B and when the change should take effect, under 0199 and applicable scope. Keep who changed what and the original/updated plan. This command is not evidence that the person reached B.
2. Do not have the Cleaner scan final ShiftOut at A or a second ShiftIn at B merely to perform this move. Preserve the person's slot, actual initial ShiftIn and existing break progress.
3. On arrival, scan B's existing Check-In Sign and explicitly confirm “ยืนยันเข้าพื้นที่ใหม่” for the authorised pending transfer. Recommend one destination confirmation scan, without a new origin-departure scan for this flow. It is a distinct transfer-confirmation record, not a second ShiftIn or one of the normal six attendance actions. Preserve actual confirmation time, sign/GPS provenance, and transfer identity; reject unauthorised destinations.
4. Synthetic example: ShiftIn at A 07:00; Supervisor changes assignment effective 13:00; B arrival confirmed 13:05; eventual ShiftOut at the then-current assigned Area B 19:00. Arrival remains 13:05, not the planned 13:00. A's old scans stay at A. Do not claim 13:00 was a physical departure scan or infer travel time from the two plan/arrival values.
5. Break events already recorded do not reset. The person continues their remaining break actions and ends the same shift at the current authorised Area. Break-group changes, transfer during a break, missing confirmation and failed/retried/cancelled transfers still need explicit spec treatment; do not silently assign a second complete set of breaks.

This flow applies to a person already checked into that slot. A replacement who has not yet started work uses normal initial check-in at their assigned Area; do not force that person into the midshift-transfer flow. Limited essential-task help under 0195 is not automatically an Area responsibility transfer.

## Dashboard and evidence boundaries

Supervisor instruction alone must not show actual arrival at B. A new destination confirmation would be evidence of entry there and thus requires explicit integration with the overview's existing ShiftIn-based contract. This is an additional proposed effect, not a silently accepted redefinition of the three entry categories or their denominator. Past entry evidence at A remains historically true; current-person location/responsibility must not show the same transferred person working at both Areas simultaneously. Ending responsibility at A is a plan/assignment fact here, not a measured departure timestamp. Do not claim B is physically occupied before confirmation or A continuously occupied after responsibility ends; report available facts rather than filling gaps.

The prior overview delta review covered new shift actions and relief initial ShiftIn, not this new transfer-confirmation record. If accepted, review its effect on current attendance states, entry projection, provenance and drill-downs, then amend the overview documents where required. Do not fabricate a second ShiftIn solely to avoid integrating transfer evidence.

## Current code impact verified

- `AttendanceEndpoints.RecordAsync` compares the sign Area with `user.AreaId`, deduplicates an event within the slot and enforces the current event sequence. It does not implement authorised midshift destination confirmation.
- `AttendanceRules` has one ShiftIn/ShiftOut lifecycle. Reusing final ShiftOut for a move would close submission under the accepted project rule; resetting the lifecycle to reopen work is not justified.
- `AreaAttendance.StateOf` derives state from a regular Cleaner's four events plus cover. This cannot by itself represent the proposed transfer history/current responsibility.

These observations mean the proposal needs deliberate data/API/authorization/projection work, not only a new button. No source changes or application tests in this research task.

## Decision state

Owner replied “OK” to the recommendation. Recorded [0203](../adr/facility-0203-midshift-area-transfer-preserves-shift-and-break-progress.md) and [0204](../adr/facility-0204-midshift-transfer-requires-one-destination-sign-arrival-confirmation.md). The proposal sections above preserve the research rationale; core transfer mechanics are now accepted, while exception/recovery details and overview aggregation integration still need specification. Do not re-ask whether post-start changes are allowed or who decides them; 0199 already resolves those questions.
