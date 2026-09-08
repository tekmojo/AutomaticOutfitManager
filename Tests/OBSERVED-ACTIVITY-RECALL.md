# Individual Recall for observed activities

Automated checks: `Tests/run-pause-behavior-contracts.ps1` and its `-PreviousDecision` controls execute production observed eligibility/action, safe-exit selection and retained-buffer Refresh. Related suites cover access exits, child access, observed transitions, Non-Work rules, activity classification and permissions.

Manual gameplay checks:

1. Child floor drawing in an active Non-Work area: Recall appears; clicking it interrupts learning and exits once without an invented outfit change. Repeat adult meal and recreation activity.
2. Retained Non-Work task: Recall clears only that rule's allowance before interruption; no success credit or renewed allowance during the exit. A later real activity may start a new allowance.
3. Active owned outfit session: existing Recall returns assigned gear/restores saved gear through the normal locker path. A visited area's borrowed transition/material-source row cannot take ownership of that return.
4. No safe exit or a confined prisoner room: report the blocked exit and preserve the current activity. Never route through an unrelated protected area just to leave.
5. Drafted/downed/mental state, explicit order, rest, patient care and a carried baby: the new observed control must not interrupt these protected cases.
6. Rule remains active: a new native activity can return later. Individual Recall is not a permanent access restriction.

Latest local deployment: see `work/testing/2026-09-07/umiAhFyLhN/DEPLOYED.md` from the live repository root. Build and contract passes do not establish an in-game pass.
