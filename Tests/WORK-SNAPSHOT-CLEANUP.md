# Work-only gear snapshot cleanup — 2026-09-05

Built candidate: `9ABDF109EC93617166F65A0A6BFCE1DDC3C01C360FDB584612089EDDA53F96FB`.

User-authorized deployment completed on 2026-09-05 with RimWorld closed. Candidate, live, and installed SHA-256 all match `9ABDF109EC93617166F65A0A6BFCE1DDC3C01C360FDB584612089EDDA53F96FB`. Only the runtime DLL was deployed; no game launch or save edit. Gameplay verification remains pending. The preceding meal candidate was `C11E1A0697B92FE8BA1CBE731B8B95EA413B6F1493285C94338217DC3535514E`. This build also includes the earlier Work-blue/Non-Work-teal rule palette.

[Evidence and session audit](../../testing/2026-09-05/lumi-jsDywtVvwb/audit.md).

## Contract

- Exclude exact saved apparel/primary items when an enabled Work Area rule on the pawn's map selects that gear and the exact item meets both condition and quality limits. A paused Work rule still designates its gear. Disabled, unconfigured, other-map, Non-Work, and retained-catalogue-only selections do not create new exclusions. Weapon matching supports exact alternatives and legacy categories.
- Capture-time cleanup prevents new contamination. Existing stored snapshots and Work state baselines are cleaned on the first 30-tick maintenance pulse after load, on requirements edits, and every 300 ticks. The periodic check also catches changing item condition or map context. The snapshot viewer does not capture or mutate snapshots.
- Remove personal ownership records and record stable matching source IDs. Worn and already-assigned Work items retain managed ownership for normal safe return. Loose saved items become shared stock without a new retrieval assignment. Legacy compliant workers gain a return ledger and preserve remaining personal items.
- Preserve separate temporary Non-Work targets. Selective removal still keeps unselected/shared gear; saved personal items fill around retained gear. An in-flight personal restoration is recalculated using its destination selection. Obsolete assigned snapshot Wear and automatic Equip restores are removed individually; other queued steps remain. Current-job retirement occurs on the maintenance pulse only if that exact job is still current and native/player overrides do not apply.
- Never remove a physical item merely to clean a snapshot. Existing locker, hazard, native-order, and explicit-primary rules still control gear changes. Cleanup does not restore deleted snapshot membership merely because a source rule is later disabled; a future eligible pre-work capture can record new personal gear.

Implementation: `Source/Detection/WorkGearSnapshotPolicy.cs`, capture/edit/tick/transition hooks in `Source/Core/AutomaticOutfitManagerGameComponent.cs`, snapshot help in `Source/UI/MainRulesWindow.cs` and README.

## Verification

`Tests/run-non-work-contracts.ps1 -VerifyFailureReporting`: **105 passed**, including 34 new snapshot-policy checks and the separate handled-failure check. The runner compiles the production rule, evaluator, outfit, fallback, and cleanup policy files against small API doubles.

`Tests/run-meal-handoff-contracts.ps1`: **27 passed**, compiling the production controller and save record with deterministic doubles. `build.ps1` succeeds against the installed RimWorld 1.6 assemblies. `git diff --check` passes. The new policy checks also recognize AOM-owned forced Wear restores, preserve unrelated restore steps, preserve retained partial targets, and exclude explicit Equip orders. These checks do not exercise the full game component in a running Unity process, real reservations, native pathfinding, or actual save round trips.

## Manual checks — pending

| Scenario | Expected |
| --- | --- |
| Load Lumi's old snapshot containing the legendary 69% formal vest; enabled Work rule accepts it | After a short unpause, View snapshots omits the vest but retains unrelated exact items. No immediate stripping. |
| Change a Work rule while paused to select an existing personal garment/primary | The snapshot updates automatically. Existing safe return handles worn gear after play resumes. No red conflict palette is reintroduced. |
| Personal vest outside HP or quality range, disabled rule, unconfigured area, or only a rule on another map | No new exclusion. A paused enabled rule still excludes eligible gear. |
| Already-compliant worker on an old save | Matching worn gear returns as Work gear. Remaining personal clothing keeps its ownership and restores normally. |
| Stored vest reused for an incoming Work requirement, before its Wear job begins | The assigned Wear remains authorized after reclassification; no blocked preparation loop. |
| Other pawn wants a loose formerly personal vest | The old personal snapshot no longer reserves it. A currently assigned/worn item remains protected until safe return. |
| Two Work sources share an item; one selected for Non-Work removal | Item remains as retained Work gear. Selecting both returns it. It never reappears as personal. |
| All selected, saved preference off, no fallback | Work gear returns; empty slots remain allowed. |
| Cleanup during queued personal Wear/Equip restoration, including a connective wait | Only newly obsolete automatic restore steps retire. Valid outfit steps and native continuations progress without queue growth. |
| Explicit primary selection or queued player order | Cleanup does not override the player's current action or repeatedly interrupt it. |
| Meal handoff during cleanup/load | Same meal and count survive; source locker remains first; no second changing loop or new Work Area transit. |
| Partial Non-Work target or fallback baseline contains deliberately retained Work gear | Temporary target is preserved; cleanup of personal history does not silently remove retained gear. |
| Save/load during return and after cleanup | No missing references, duplicate Job owners, returned vest restoration, or repeated cleanup messages. |
| Vacuum/temperature protection and an inaccessible locker | No immediate unsafe removal. Existing safe return/egress or bounded meal fallback remains authoritative. |

Track the unrelated recovered Jono vacsuit path warning separately; this cleanup does not claim to fix it.
