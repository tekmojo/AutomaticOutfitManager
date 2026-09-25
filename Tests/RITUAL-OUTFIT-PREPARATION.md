# Ritual outfit preparation: second local iteration

Requested September 24, 2026. The second-iteration details below are retained as implementation/test history. Subsequent child-checkbox integration and the deployed role-ceremony pass are recorded in [current readiness](NEXT-UPDATE-READINESS.md). Allowed children no longer need outfits or hold ceremony progress; adult permissions remain as described below. The current candidate is not published.

## Behavior

Before the opening ritual stage progresses, the gate checks the native assigned roster and owned participants. Eligible participants with available, wearable, reservable gear and allowed retrieval/ceremony routes hold progress even before an AOM outfit state exists. Existing preparation and non-work return/restoration steps remain included. Native duties and pawn jobs continue; the gate does not create jobs, reserve stock, assign participants, or bypass access.

The gate checks at most once per 30 game ticks. Ceremony/duty destinations must belong to enabled AOM rules. Activity/Haul/Wandering restrictions, child access, pause, protected transit, unavailable gear, native attendance refusal, mental/emergency control, drafting/downing, map departure, and unrelated forced orders retain precedence. Explicit recall remains distinct from a return needed to enter a non-work area. Saved personal targets and selected outfit standards use the existing AOM policies.

A clear readiness scan no longer permanently opens the gate while native gathering is still underway. The saved release flag becomes final after the ceremony actually progresses or leaves its first stage. Already-progressed rituals remain native. The report lists participants still preparing; dressing time adds neither ritual progress nor attendance credit.

A ready, active participant waiting in native gathering retains the ceremonial outfit rather than reaching the ordinary idle-return watchdog. Recall, cancellation, native control, disabled/paused areas, departure, missing required gear, or leaving the opening stage remove that exception.

Spectator claims now follow the installed native contract: target A is the spectator seat, B is the common ceremony focus, and C is optional furniture. Multi-sittable furniture receives a cell claim; single sittables retain exclusive thing claims. The native CanReserveSittableOrSpot search sees pending AOM claims and can select another seat. Native reservation failure and forced reservation overrides remain authoritative.

## Regression evidence

The Mpg9ZXxdJN.mp4 retest exposed three first-iteration defects: 65 spectator deferrals across 23 pawns against the shared focal cell, release before DJ/Bracher/Gonzalez started needed transitions, and two automatic idle returns by DJ during gathering. The old spectator fixture repeated the same mistaken target mapping. The detailed audit is in `.codex-audit/ideology-retest-Mpg9ZXxdJN/AUDIT.md` under the live repository.

The updated fixture uses distinct target-A seats and common target-B focus. Negative controls using the saved first-iteration claim/readiness production methods fail their diagnosed cases. A no-retention control reproduces the previous watchdog behavior.

## Validation

Release build passed against installed RimWorld/Harmony, and git diff --check passed.

| Suite | Checks |
| --- | ---: |
| Ritual outfit preparation | 77 |
| Installed native ritual/reservation probe | 14 |
| Prepared work candidates | 48 |
| Preparation handoff | 134 |
| Non-work outfits | 309 |
| Native rule control | 214 |
| Area permissions | 295 |
| Paused hauling/restoration | 563 |
| Session audit routing/activity | 48 |
| Saved gear recovery | 312 |
| Restoration | 50 |
| Combat buffers | 79 |

The native probe executes the real JobDriver_Spectate reservation body with distinct seat/focus cells, intercepting only the reservation sink. It also executes native Transition.CheckSignal with production readiness filters and validates patch signatures against the installed assembly. A signature ambiguity caught during development was fixed before deployment. These checks do not create a Unity map and are not an in-game pass.

Run ordinary ritual checks with `Tests/run-ritual-outfit-contracts.ps1`, and the native probe with `Tests/run-ritual-native-probe.ps1`. For old-production negative controls, pass `-PreviousClaims` or `-PreviousReadiness` and `-PreviousSourceRoot C:/GitHub/AutomaticOutfitManager/.codex-audit/ideology-second-iteration`. `-PreviousIdle` checks the previous absence of a retention exception.

## Manual retest

1. Re-enable Temple after the disabled comparison and start a fresh role-change ceremony. Use several eligible spectators, distinct seats, and enough accessible complete outfits. Let the leader arrive first.
2. Confirm spectators prepare concurrently; the leader keeps the ceremonial outfit while waiting; progress begins only after feasible participant transitions finish.
3. Include a pawn coming from work-gear return/restoration, and a participant whose outfit preparation starts later than the first spectator's. Both must be accounted for before start.
4. Test unavailable/unwearable/reserved stock, blocked protected transit, native mental/emergency unavailability, explicit recall, pause, cancellation, and an undesignated ceremony. These must not gain bypass access or leave an impossible permanent hold.
5. Test conversion, native prisoner delivery, and save/reload during gathering and after actual ceremony start. These remain focused gameplay cases, not validated by this role-change clip.

Single-stage or modded rituals whose opening duties already perform interactions also need observation. This implementation pauses ritual progress and timed effects without replacing native duties with a synthetic gathering stage.

## Runtime and rollback

Second candidate SHA-256: `93F4CE06461013E7CE68D0CF63DEC603E9FDDFABADE65166F79A87C6CC17A1E3`.

First-iteration DLL and exact gate/claim source backups are preserved in `C:/GitHub/AutomaticOutfitManager/.codex-audit/ideology-second-iteration`. First-iteration hash: `D52C5E1CD2F50E0F93FF02F1A04C5C10DCF0A602860425EFC2C963DF82B3127B`.

The pre-feature runtime remains backed up at `C:/GitHub/AutomaticOutfitManager/.codex-audit/ideology-first-iteration/AutomaticOutfitManager.before.dll`, hash `EB08621418BA99D2DF42ABCF345DF094E65D75D04927DF317BBF0B11B8C10C6C`.

Deployment copies only the runtime DLL through the established RC/live/junction path, with RimWorld closed. A requested rollback must use that same path and verify all hashes. No game launch or in-game success is implied by deployment. See the second-iteration audit directory for the deployment receipt.
