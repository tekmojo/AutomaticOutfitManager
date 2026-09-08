# Post-raid recovery: Ocag and Hanh

Local fix candidate after released 0.4.0. Baseline: `76821a1607128ddec2b635c15d35081380128482`.

## Evidence

User capture `Ds16unKBfX.mp4` and log attachment `bbd9696f-97ef-4954-ae49-b0e54f928f3c/pasted-text.txt` show two failures:

- Ocag has a completed Kitchen buffer, a requested outfit return and a newly encountered USS Pissant Main requirement. The component's boundary handoff can prepare during the requested recall. Direct StartJob promotion lacks the helper's scoped re-entry protection; the same hauling job replaces native error-recovery Wait, producing ten rapid-job errors in the supplied log.
- Hanh's saved outfit restoration completes, then carried-meal routes toward a dining cell inside USS Pissant Main fail under AOM's avoidance customizer. Native waits and repeated meal attempts follow. The log contains nineteen no-node path warnings and a stationary observation reaching 1,530 ticks.

The complete local audit and extracted evidence are in `C:/GitHub/AutomaticOutfitManager/work/testing/2026-09-06/Ds16unKBfX/`.

## Changes

- A shared boundary admission scope covers both Prefix promotion and component recovery, including native retries of the same root identity. Native recovery waits survive nested admission. Harmony finalization releases the scope on success or exception.
- The retry registry retains a detached snapshot. The tracker or preparation state receives a separate working job; an already detached native retry retains its own object. Acceptance requires actual tracker/queue/pending-state ownership and the expected definition/load ID, so a pooled/reused job is not mistaken for success.
- Failed admissions defer retry for 60 ticks. Successful admission retires only observations predating that attempt, preserving a newer boundary callback. Existing return/restoration takes precedence over renewed preparation; buffer counts are not reset.
- When the primary locker is on another map, the return planner can use a safe, reachable locker belonging to a currently tracked rule on the pawn's map after the normal cross-map return opportunity. A rule without a locker keeps its existing behavior. Unrelated lockers, unsafe cells and unavailable destinations are not admitted.
- A native carried Ingest job may obtain a route to its permitted dining destination while missing gear. The unchanged actual-cell guard still requires the outfit before protected entry. This aligns meals with the existing reading/direct-target behavior and keeps unrelated areas out of the route.

## Validation and deployment

- Build succeeded against the installed RimWorld 1.6 assemblies; `git diff --check` passed.
- All 22 contract suites passed: **1,996 checks**, including 42 new boundary-admission checks and 48 route/activity checks.
- `run-boundary-admission-contracts.ps1 -PreviousDecision` fails the intended nested native-recovery assertion using the previous production promotion/helper. The current production methods pass under real Harmony with a native-shaped tracker body exercising queueing, nested recovery, cleanup/pooling, exceptions and admission ownership.
- `run-session-audit-fix-contracts.ps1 -PreviousDecision` fails the unprepared carried-meal route assertion using the previous production routing policy. The current policy passes; denied Activities, unrelated-area transit, custom drivers, food pickup and incorrect carried items remain excluded.
- Fixture tests are not a running RimWorld gameplay pass. The new locker selection is tested with controlled reachability/safety dependencies; a manual post-raid replay remains necessary.
- RimWorld was closed. Only `1.6/Assemblies/AutomaticOutfitManager.dll` was deployed from the RC to the live repository, through the installed junction. Candidate/live/installed hashes were verified identical:
  `8DE2FFF6F4DB67E2C108E79FEA3C928F1ECC42BB047FABDFE50422E18A82B270`.
- No game launch, commit, GitHub push, Workshop packaging or public update was performed. Assembly version remains 0.4.0 for this local test candidate; distinguish it by hash.

## Next manual check

Replay the same post-raid save. Confirm Ocag can complete the safe return and resume useful work without repeated preparation or same-tick job errors. Confirm Hanh completes Ingest, then resumes another activity instead of repeatedly targeting the blocked dining cell. Observe buffer counts and safe clothing retention during drafting/downing and cross-map return.

The separate native Refuel target/no-job warning in this capture remains unattributed; it is not claimed fixed by this candidate.
