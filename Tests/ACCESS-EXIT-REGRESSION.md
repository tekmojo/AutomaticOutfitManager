# Access exit regression — 2026-09-05

The G1Uc1ORcQ9 session showed Kyoto repeatedly starting exit Goto jobs after Allow Children was disabled during a meal. Both native Goto and GotoWander allow opportunistic job prefixes. An inserted haul was rejected by the child filter and replaced with another exit inside the same StartJob call stack, reaching native job-loop recovery.

The shared access exit factories and redirects now register exact transient job ownership. The TryOpportunisticJob patch suppresses prefixes only for those jobs, including a final-result guard for compatibility postfixes. Identity includes the pawn, map, job reference, load ID, definition, target and bounded age. Ordinary movement and hauling remain unchanged. Ownership resets for a new or loaded game and does not add saved Job owners.

An exact exit no longer qualifies for fresh protected transit preparation in an area it is leaving. This does not exempt entry into another area, alter safe destination selection, remove hazard checks, or change recall/restoration ownership. Pending meal detours yield when destination Activities/child access is revoked or an exact access exit takes control. Cancellation leaves the actual food and saved outfit references intact.

## Automated verification

- `run-access-exit-contracts.ps1`: 20 checks. Actual Harmony intercepts a simulated native tracker. The unpatched simulation reaches 11 synchronous starts; patched direct exits finish admission in one start with no opportunistic haul. Includes compatibility insertion, ordinary hauling, job reuse, copied identity, changed target/definition, map changes, age expiry, tick rollback and reset.
- `run-meal-handoff-contracts.ps1`: 65 checks, including 13 new revocation checks across captured, returning, fallback preparation and eating stages, plus path/tick/meal-guard/dining-spot callbacks.
- Existing activity, permissions, storage, non-work, child and nesting suites: 933 checks.
- Total: 1,018 passing checks. Production build and diff whitespace checks pass.

These checks do not execute native game pathfinding, toils, or the user's complete mod stack.

## Next game test

1. With nearby haulable stock, disable Allow Children while a child is consuming a meal inside the Non-Work area. Repeat during recreation and after a save/load.
2. Confirm one direct exit, no repeated Goto/error recovery, and no new outfit preparation merely to leave. Existing managed sessions should still recall and restore safely.
3. Repeat Activities denial for a guest, slave and prisoner. Prisoners retain the existing same-room exit restriction; unreachable exits do not grant access to forbidden destinations.
4. Confirm normal permitted movement can still perform opportunistic hauling, and exits do not gain shortcuts through other protected areas.
5. Confirm interrupted meals and exit movements do not spend successful-task buffer credit; no missing or duplicated food, personal gear, or pending job ownership.
