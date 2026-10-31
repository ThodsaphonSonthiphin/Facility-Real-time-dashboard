# Admin forms and confirmations follow the approved design

- Date: 2026-10-31
- Authority: owner requested the missing forms/confirmation windows and explicitly authorized implementation in chat.
- Reference: `Facility Dashboard — UX UI ใหม่ (1).html`, six additional pages: correction, cover, Area, point/rounds, account forms and confirmation windows.

## Decision

Extend facility-0100's alignment to forms and confirmations, not just parent routes. Use the reference hierarchy, dimensions, scrollable bodies, visible action footers, impact explanations and actual data. Preserve existing API, validation and business semantics where the reference illustrates unsupported behavior.

1. Point rounds become editable numbered rows with actual durations and a timeline, retaining numeric HH:mm and invalid-draft safeguards. Add round creates an empty editable row; incomplete rows block save. Area/account forms group identity, assignment and sign information and retain existing actions.
2. Successful changed-name Area save opens the existing print route for that Area; failed save retains draft and route. Sign settings remain independent saves; the UI must not imply an atomic Area/sign update.
3. Actions that stop use or invalidate a sign require contextual confirmation before mutation. Area/Building deactivation requires typing the actual entity code; existing eligibility guards remain. Cover cancellation uses neutral confirmation. QR regeneration offers the checked print-followup option and navigates only after success.
4. These confirmation windows use a solid red button for stopping use or invalidating signs, per the approved reference. This amends facility-0087's 2026-10-22 outline-only confirmation-button decision in this context; the shared outline danger style remains available elsewhere.
5. Counts/consequences follow available data and existing guards. No cascading deactivation, automatic account reassignment, cover replacement, generated passwords/forced first-login change or new session-reset capability. The standalone session-reset scenario has no existing endpoint and is not implemented by this presentation task.
6. Nested Escape closes only the top confirmation and restores its opener. Pending mutation blocks close/cancel; failure stays visible for retry. Verify all six groups at1280/375px with synthetic data and no real mutations.

## Consequences

Track each reference group explicitly rather than inferring completion from route coverage. Existing DayOnly/DayAndNight, credential limits, independent sign saves and cover rules produce documented differences from the illustrative mockup. Additional backend behavior is separate product work.

## Owner visual feedback amendment (2026-10-31)

Owner approved bringing phone/password changes into the account edit Drawer, each with its own form and save action; remove the separate table actions. Current credentials remain unreadable. Credential success clears only its own input and retains unsaved profile edits. Correct the point rows to the approved link-delete/dashed-add layout and provide focus-outline clearance inside account scrolling bodies.
