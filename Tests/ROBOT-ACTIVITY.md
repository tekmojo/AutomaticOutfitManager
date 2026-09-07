# Robot and animal activity classification — 2026-09-05

Status: fixed, built, and deployed from this task's verified baseline on 2026-09-05. Not yet tested in-game.

Deployment: the user explicitly approved deployment. RimWorld was closed and the installed junction targeted the live repository. Only the runtime DLL was copied using the deployment skill script. RC, live, and installed hashes all matched `E7E3292E3D20D90173B72972FBEEE14E5DE8FCFF67353F41A5D28753393AE5E3` after deployment. The game was not launched.

The user requested troubleshooting and correction of hauling/wandering robots and animals appearing in Workers/Occupants. Preserve ordinary animal eating/sleeping observation, meaningful work, human outfit transitions, and area-access rules.

## Confirmed cause

The large-map recording `Kjzmbgtbqy.mp4` shows bots hauling under Occupants and Workers. The latest local `AnuStart_ALL_MODS.rws` has `AIRobot_Hauler` pawns performing `HaulToCell` with `HaulGeneral` or `HaulCorpses`; animals also have `HaulToCell` without a WorkGiverDef. These records were read without modifying the save.

The installed, enabled `[FSF] Complex Jobs` mod (`FrozenSnowFox.ComplexJobs`, Workshop 2069684319) changes both work givers to `FSFHauling`. Its `1.6/Ludeon/Core/Patches/295_FSFHauling_FSF.xml` contains the exact replacements (HaulCorpses near line 145; HaulGeneral near line 208). This proves the vanilla-only work-type comparison was insufficient; these are not arbitrary custom robot jobs.

The RC also contained changes from the follow-up task: a broad name-based hauling fallback and an observed-roaming exclusion for robot cleaning. The latter deliberately pushed cleaning into ordinary participants. Broad hauling-name matching could incorrectly demote Warden/crafting carry work. The save contains a concrete human `HaulToCell` with `DoBillsMilitarumFabricationBench`, illustrating why the work-type distinction matters.

Autonomous robots also use ordinary `Goto` and `Wait` while returning to/waiting at their base; animals use connective waits. Previously those could fall through to Workers/Occupants despite not representing work or outfit sessions.

## Changes

- Added `Source/Detection/ActivityJobClassifier.cs`, used by the existing shared `IsHaulingJob` entry point. Recognize vanilla Hauling and the verified exact `FSFHauling` type. Retain concrete untyped hauling fallback for animals/direct jobs. Non-hauling work types remain authoritative regardless of hauling words in job/giver/driver names.
- Removed the robot-cleaning exclusion. Cleaning remains in Wanderers, governed by the existing roaming access policy.
- Only the current-path display helper additionally recognizes autonomous non-human Goto/Wait/posture waits/native idle jobs. Player-forced, drafted, and work-giver movement are excluded. Eating and sleep remain ordinary observed occupant activity. Gameplay helpers do not use this added idle display classification.
- Updated the Wanderers tooltip to describe cleaning and idle movement accurately. Removed the unused `IsAutomatedUnit` helper left by the follow-up task.
- Preserved native jobs, pathfinding, reservations, outfit ownership, and buffer completion logic. Recognition of FSFHauling consistently makes that existing hauling work follow the Hauling category in both shared checks and display.

## Validation

- `Tests/run-activity-contracts.ps1`: 26 checks passed, compiling the actual production classifier with minimal API doubles.
- `Tests/run-non-work-contracts.ps1`: 178 checks passed.
- `Tests/run-meal-handoff-contracts.ps1`: 52 checks passed.
- Repository `build.ps1 -RimWorldDir F:/Steam/steamapps/common/RimWorld`: passed.
- `git diff --check`: passed; existing line-ending configuration warnings only. Existing mixed endings were preserved rather than normalized.
- RC candidate SHA-256: `E7E3292E3D20D90173B72972FBEEE14E5DE8FCFF67353F41A5D28753393AE5E3`.
- Live DLL hash when checked after build: `27FF277740DDC2B3504B87236A0BCA9A57B0AD0CFC1A7B76B7F4D3D394DDE826`. This task did not modify it. The old handoff hash is historical; the follow-up task changed deployment state.
- Before-edit copies of the two existing source files are at `C:/GitHub/AutomaticOutfitManager/work/testing/2026-09-05/robot-activity-fix/` for comparison against this task alone. Other pending RC changes remain intact.

## Next in-game check

After an authorized deployment, use the large-map save and inspect both rule types: active robot/animal storage hauling should appear under Haulers; wandering, autonomous robot cleaning, and idle movement/waiting under Wanderers. Human preparation/return, actual work, meals, and retained buffer counters should remain in their appropriate rows. Check Work/Hauling/Wandering toggles independently, including animal hauling, a cleaning robot, and a human Warden/crafting carry task. Confirm no duplicate entry across Workers/Occupants and access-activity rows after the normal half-second refresh.

These headless checks and successful build do not establish an in-game visual or routing pass. The separate missing fridge save-component error remains outside this fix.
