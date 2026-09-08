# Native rule suspension

Regression: small-map session mvGMPuo8My / log attachment 210a86ce-855e-4c97-92ea-f48976689ca4. Ocag's mental-state meal was admitted by StartJob but interrupted repeatedly at the Kitchen next-cell boundary. Fourteen throttled messages repeated one internal cell transition, retaining an obsolete GotoWander root.

The current change aligns next-cell checks, path costs, periodic recall/recovery and activity projection with native control. Mental states (including a still-running SocialFight) and emergency jobs suspend civilian outfit intervention. Downed/drafted/custody exceptions remain in their existing lifecycle paths and also bypass the movement guard/activity projection. Ordinary hunger, fatigue, injuries, sleep and rest do not create an exception.

Mental/emergency suspension drops obsolete boundary and prepared-meal retries, Non-Work detours, pending civilian work and pending buffer candidates. It removes only assigned queued outfit steps, retaining unrelated native jobs and the current native job. Snapshot and item ownership survive. The persisted nativeControlSuspended flag defaults false for older saves. After recovery, an active session reevaluates the new activity; interrupted preparation or restoration re-enters the normal safe locker-return boundary. No immediate gear removal or unconditional current-cell restoration is introduced.

Automated checks:

- Tests/run-native-rule-control-contracts.ps1 compiles the actual full next-cell Prefix, native admission blocks, suspension method and shared policy with a native-shaped tracker/path body and controlled dependencies. Exercises repeated mental Ingest, stale GotoWander, no active session, SocialFight, incapacitation, emergency control, queued outfit jobs, reference preservation, recovery from each outfit phase, normal PPE and access rejection.
- Pass -PreviousPathSource with the archived pre-change PawnPathFollower_ProtectedArea_Patch.cs to reproduce the exact previous boundary failure. Core methods are not simulated wholesale; periodic wiring and projection/path-cost guard checks are separately identified as structural checks.
- Existing combat buffer, boundary admission, restoration, meal, Non-Work snapshot/migration and activity suites cover adjacent contracts. The pause suite executes the full recall loop for mental, downed, SocialFight and emergency cases.

Manual replay remains necessary: load the affected save, verify Ocag's mental meal/wandering proceeds without boundary retries or locker orders, then verify normal activity and PPE enforcement resume when mental control ends. Test a mental break during a gear transition and save/reload while suspended. Worn gear may still change through native mental behavior; AOM does not initiate removal while suspended.
