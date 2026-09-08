# Raid claims and departure follow-up — 2026-09-07

User authorized implementing and deploying the findings from the September 7 raid audit. Source remains in the RC worktree on `codex/workshop-release-prep`, baseline commit `76821a1607128ddec2b635c15d35081380128482`, with all previously deployed post-raid changes preserved.

## Evidence

- September 6 log: `C:/Users/tekmojo/.codex/attachments/7640aa09-12e0-498b-bb7b-496a32c11a0f/pasted-text.txt`.
- September 7 log: `C:/Users/tekmojo/.codex/attachments/4398f4c5-6fea-4ccc-97ab-a3d882545e62/pasted-text.txt`.
- Audit: `C:/GitHub/AutomaticOutfitManager/work/testing/2026-09-07/4398f4c5/AUDIT.md`.
- Latest excerpt has 199 false origin-cell contention messages and three Mizo departure/restoration cycles, with targetless waits starting fresh work preparation after the first two restorations.

## Changes

- `ManagedWorkClaimRegistry` requires an actual enumerated cell target. Empty searches no longer manufacture a claim through the native default `LocalTargetInfo`, which is valid at `(0, 0, 0)`. Real origin-cell targets, hauling destinations, Thing and queued material claims retain their previous behavior.
- The candidate test fixture now represents the game's invalid sentinel and initializes Job target fields to Invalid. It executes the production StartJob claim fallback through a Wear-completion / queued-Wear / native-connective-Wait sequence. Targetless waits preserve the saved outfit queue and do not contend with real origin-cell claims.
- `NativeDepartureHandoff` bridges a completed departure restoration to the native thinker's next exit proposal. It permits only empty tracker gaps, targetless Wait/Wait_MaintainPosture, and native exit jobs, without starting another outfit session.
- The handoff lasts at most 600 ticks without a fresh native exit proposal. Waits do not extend it. New purposeful activity, directed movement, targeted waits, player orders, changed duty identity, a new AOM session, map changes, draft/downed/mental state, despawn, time rollback and load reset invalidate it. Actual exit clears it explicitly. It stores no Job or saved outfit.
- The component's occupancy/access pulse recognizes the same brief handoff. Ordinary activities and unrelated movement keep normal rule checks. Existing exit routing and final managed-item drop safeguards remain in place; AOM does not create an exit route.

## Verification and deployment

- Build succeeded against the installed RimWorld and Harmony assemblies; `git diff --check` passed.
- All **23 suites / 2,060 checks** passed. Results: `C:/GitHub/AutomaticOutfitManager/work/testing/2026-09-07/raid-claim-departure-fix/contract-results.txt`.
- `run-managed-work-candidate-contracts.ps1 -PreviousDecision` confirms the previous production registry fails both no-fabricated-claim and queued-saved-Wear preservation regressions.
- `run-native-departure-handoff-contracts.ps1 -PreviousDecision` confirms the previous production admission/state-clear code restarts preparation for the post-restoration departure wait. The current test also executes the production periodic handoff check and cancellation cases.
- RimWorld was closed. Only the runtime DLL was copied to the live repository. Candidate/live/installed junction hashes were verified identical:

  `D295BDE39728063AAB676C3269DF08DD3FF56DF87860F580300E8BF19F27E58E`

- Previous deployed hash: `8DE2FFF6F4DB67E2C108E79FEA3C928F1ECC42BB047FABDFE50422E18A82B270`.
- This is a local 0.4.0 test build, distinguished by hash. No game launch, commit, push, Workshop package or public update was performed.

## Manual replay still needed

Load the raid save and observe a departing visitor through locker return, exact personal restoration, the native wait/exit handoff and actual map exit. Confirm no fresh work outfit is issued for the connective wait and no unrelated wait logs a fabricated origin-cell claim. Also observe queued saved Wear/Equip steps completing without repeated queue rebuilds. Cancel a departure with a real new activity or player order and confirm ordinary rule checks resume. A long-running native exit failure beyond the bounded handoff is not claimed fixed.

Carry forward the original Hanh carried-meal completion and Ocag post-raid return checks; passing these fixtures does not establish an in-game pass for those earlier cases.
