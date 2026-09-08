# Non-Work pause regression — 2026-09-07

Evidence: `work/testing/2026-09-07/eXBxQXYbnN/AUDIT.md` in the live repository. Dining Room 1 retained eating/social occupants after pause. Four no-node path warnings involved native carried meals targeting the closed dining location. A tracked-only return lost its state during synchronous recall interruption.

Changes:

- `DeniedActivityRule` now includes pause for purposeful activities even without a work giver or outfit session. Its existing native overrides and essential-rest protections remain. Hauling and wandering retain their independent permissions.
- Native chair selection and late carried-meal paths recheck destination access. A denied dining destination is replaced with a permitted reachable spot, avoiding unrelated protected areas and missing required gear. This does not end a job from inside its toil. If no legal spot is available, the path yields in place and scheduled runtime access enforcement handles recall/exit.
- A tracked-only recall no longer clears its state if ending the old job has started a return/restoration or replaced the state. The return keeps its existing ownership and completion path.
- Denied untracked activities use an owned exit when possible. The exact exit may cross its currently occupied area, but cannot re-enter it or bypass another area. Exit selection excludes areas needing unavailable outfit preparation, respects prisoner rooms, and accepts a narrow safe corridor when the preferred boundary clearance is unavailable.
- Non-Work controls read Pause activities / Resume activities / Activities paused. Both layouts reserve room for the longer button. Help explains that permitted hauling/wandering continue.

Verification: `run-pause-behavior-contracts.ps1` executes extracted production activity/recall/exit methods and the production meal destination helper. Its recall fixture simulates synchronous native EndCurrentJob starting a return/restoration or replacing state. The previous activity, recall and absent dining-redirection decisions each fail their corresponding negative control. Existing access-exit fixtures cover exact job identity/expiry and actual Harmony optional-job interception.

Manual verification still required: pause during eating at a table, carrying food toward it, social recreation, Non-Work buffering, and locker return. Confirm food remains available, no repeated failed chair path or doorway wait occurs, interrupted tasks earn no buffer credit, saved gear restores, allowed haulers continue, and Resume permits fresh activity. Include guests/slaves/prisoners, a narrow exit, a confined room, and an unrelated protected area on the route. No source-log verbosity was added.

This change does not implement the separate pending Reba automatic-idle-return cancellation recommendation from the earlier small-map audit.
