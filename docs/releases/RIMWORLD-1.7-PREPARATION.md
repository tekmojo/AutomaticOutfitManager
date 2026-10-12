# RimWorld 1.7 compatibility preparation

Started October 12, 2026. **Partial 1.7 validation: the new-colony apparel/locker test is healthy; broader compatibility remains unverified.** Published AOM remains 0.4.6. Its completed [release checklist](history/RELEASE-HISTORY-0.4.6.md) and [closeout](../../Tests/RELEASE-CLOSEOUT-0.4.6.md) retain their original meaning. No next AOM version has been assigned.

## Announcement and working approach

The [Steam announcement supplied by the maintainer](https://store.steampowered.com/news/app/294100/view/672882362547178633) could not be retrieved directly. Ludeon's [official announcement by TiaPixel](https://www.reddit.com/r/RimWorld/comments/1x47v0u/announcing_the_waylith_engine_and_update_17/) links to that same Steam post. It says 1.7 moves from Unity to Waylith, is available on Steam's unstable branch, and is expected to run most existing mods unchanged. Ludeon asks authors to test before declaring 1.7 support. It also describes saves as compatible in both directions; that is upstream guidance, not an AOM save-migration test result.

Start by testing the published 0.4.6 DLL unchanged. Change source or target framework only if actual 1.7 evidence requires it. Engine replacement alone does not establish that Unity-named types or assemblies have disappeared. Measure any AOM performance claim independently.

## Verified local baseline

- Authoritative release source: `work/radiation-any-weapon`, branch `codex/workshop-release-prep`, HEAD `b618146` at inspection. Existing uncommitted October 2 audit documentation is preserved.
- Installed game: `F:/Steam/steamapps/common/RimWorld`, Version.txt **1.6.4871 rev590**, Steam build **23969874**. No 1.7 runtime was found in this inspected installation.
- RC and live DLLs both match published 0.4.6 SHA-256 **A25B40980BBC58D419227DD1ECA4B8F855A8A8552F115E4688CA41D091FA98EE**.
- Installed AOM directory is a junction to `C:/GitHub/AutomaticOutfitManager`. The live checkout has older, separately modified source; use the RC worktree for future development.
- About declares only 1.6. Assembly resides under `1.6/Assemblies`; there is no `LoadFolders.xml` in the RC root. Verify actual 1.7 folder selection before choosing a shared or version-specific layout.
- Project targets `net472`. Build and packaging scripts use the 1.6 output directory and legacy managed-assembly paths. These are preparation constraints, not demonstrated runtime faults.
- [Machine-readable baseline](../../Tests/RIMWORLD-1.7-BASELINE-2026-10-12.json) records hashes of the game references, Harmony, AOM and About. It does not preserve the reference binaries or substitute for a restorable 1.6 installation.

Refresh the inventory before and after a deliberate game update, saving the later output under a new filename:

```powershell
./Tests/Get-CompatibilityBaseline.ps1 -RimWorldDir 'F:/Steam/steamapps/common/RimWorld'
```

The helper only reads files and emits JSON. If 1.7 moves references, inspect its installed layout and pass `-ManagedDir` and `-HarmonyDll` explicitly. Missing legacy paths are inconclusive. Record the actual game version shown at startup as well as file metadata.

## Source areas to verify first

| Area | Existing dependency or assumption | Required evidence |
| --- | --- | --- |
| Patch startup | `Source/Core/AutomaticOutfitManagerMod.cs` calls Harmony `PatchAll`; many patches use private fields and reflected methods | Correct Harmony loaded; all expected targets resolve, including dynamically selected scanner targets; no skipped required behavior or patch exceptions |
| Protected routing | `ProtectedPathAvoidance.cs` uses `PathRequest.IPathGridCustomizer`, persistent `Unity.Collections.NativeArray<ushort>` and path requests; path follower patches access `pawn` and `nextCell` | Entry prevention, safe egress, detours, cancellation and map cleanup work without allocation/disposal errors |
| Method-body patches | `ConstructionChildAccess.cs`, `ConstructionFloorClaims_Patch.cs`, `RepairComponentClaims.cs` contain transpilers | Each intended instruction match still occurs; execute child construction, floor removal and claimed-component repair. Successful startup alone is insufficient |
| UI and assets | `Source/UI` uses Unity-named GUI, textures, input and text APIs; project references CoreModule, IMGUI and TextRendering | Tab, scroll, selection, range controls, tooltips, overlays and confirmation keys render and behave correctly |
| State and ownership | Game component, pawn state, saved gear/locker references and pending job continuation | Exact items and rules survive save/load and normal interruption without duplicate claims, lost gear or recurring Standing |
| Optional integrations | Hospitality and Simple Sidearms reflection; Ideology rituals and Odyssey map/gravship hooks | Test separately after the minimal stack, with mod/DLC versions recorded |
| Build/test tooling | Existing native probe runners use Windows C# executables and hard-coded 1.6 reference paths | Inspect the 1.7 runtime and references before adapting/running probes; a 1.6 runner pass cannot establish 1.7 compatibility |

## Ordered validation checklist

The broad gates below remain open; scoped results are recorded separately so a partial pass does not close an entire gate. Record date, exact game/build, AOM DLL hash, Harmony version/hash, active mods/DLC, save name and log path for every result.

1. [ ] Preserve restorable 1.6 game/reference inputs, current mod configuration and separate copies of test saves before changing the test installation. Identify a 1.7 test environment and record its actual binary layout. The announcement's Steam route is Properties > Betas > unstable, then Play 1.7 Unstable if prompted. Game launch remains manual.
2. [ ] Start with Core + compatible Harmony + unchanged AOM 0.4.6, adding DLC only for their specific cases. Verify AOM's DLL is actually loaded from the expected path and loaded exactly once. Inspect startup and patch errors; open the AOM tab and verify assets and controls. A supported-version warning alone is not a gameplay failure.
3. [ ] Exercise apparel-only, weapon-only and combined Work rules through preparation, useful native job completion, buffer expiry and exact saved-outfit restoration. Check a selected-outfit Non-Work rule and saved-personal preference separately.
4. [ ] Exercise protected entry/exit, unavailable gear, nested/paused areas and two pawns contending for the same item. Confirm useful alternate work or safe waiting, no repeated job loop, no unsafe entry and no claim leakage. Exercise all three method-body patch scenarios above.
5. [ ] Load a copy of a 1.6 save in 1.7. Make fresh saves during preparation, active work and return; reload each and follow native completion. Verify exact item ownership, locker associations and rules. If dual-version support is intended, test a separate copy of a 1.7-written save back on 1.6.
6. [ ] Add the relevant integrations: draft/downed recovery, Hospitality arrival/departure, Simple Sidearms restoration, Ideology gathering, Odyssey vacuum routes and gravship map transfer. Preserve separate results for each tested configuration.
7. [ ] Re-run the 1.6 startup and core workflow with the final candidate. If source changes become necessary, run affected contract/native probes against identified reference sets and repeat the affected gameplay cases.
8. [ ] Only after validation, prepare About support metadata and verified folder selection for both game versions. Update the packager's allowlist/paths if needed, then inspect the installed package for missing or duplicate assemblies. Keep AOM's mod version distinct from RimWorld's game version. Choose the AOM release version and refresh the current release checklist at candidate preparation.

Packaging, deployment, Workshop upload and publication remain later release operations. No metadata support claim, game branch switch, launch, build, deployment or publication was performed during this preparation.

## Existing issues and evidence boundaries

Carry [0.4.6 deferred follow-up](../../Tests/PATCH-FOLLOWUP-0.4.6.md) forward: blocked exact saved gear/full storage, persistence gaps, Hospitality arrival continuation and separately observed mod-load errors are not newly discovered 1.7 regressions. Reproduce on matched 1.6/1.7 conditions before attribution. Keep the successful October 2 normal-flow evidence, but do not count it as a 1.7 pass.

For a failed case, record the first exception or failed patch target, affected method/signature, exact scenario, and whether the same input fails on 1.6. For a passed case, record completion and restored item identities, not merely a resume request or absence of an exception.

## First unstable session — October 12

The maintainer reports creating a new colony. The supplied startup excerpt identifies **RimWorld 1.7.4976 rev1043**, **Harmony Mod 3.0.0.0 / library 3.0.0.0-rc.2**, and **AOM 0.4.6 (Detailed logging)**. It records loading `One_Seven_NO_MODS` with Harmony, Core, Royalty, Ideology, Biotech, Anomaly, Odyssey and AOM only. AOM retains the expected incompatible-version metadata marker.

No AOM or Harmony exception appears in this excerpt. The sole warning says a missing hidden `Funeral (no corpse)` ritual precept is being added, with a native `RimWorld.Ideo.ExposeData` stack; this is not evidence of an AOM fault. The informational messages also include stack traces and are not exceptions.

Evidence: maintainer attachment `C:/Users/tekmojo/.codex/attachments/f52a8d43-8ff3-4fa7-82a8-5f4e22a84fbc/Pasted text.txt`. This establishes reported version and startup/load messages, not complete gameplay success, every patch's effectiveness, UI rendering, or a fresh changed-state reload. The running DLL path/hash is not present in the excerpt, so the earlier frozen hash remains a baseline rather than independently verified session provenance. Checklist item 2 remains open for its remaining checks.

## New-colony apparel test — October 12

The [video/log audit](../../Tests/RIMWORLD-1.7-NEW-COLONY-2026-10-12.md) finds healthy Kitchen lab-coat preparation, work, two-task buffers, locker returns and exact personal-parka restoration. There are 11 completed restorations, 28 successful clothing endings and no failed clothing endings or exception headlines. Sampled video confirms the interface is usable and all five pawns reach bed with no active or returning workers before the next activity period. Brief queued-parka waits resolve. This covers the apparel-only portion of gate 3, with ordinary contention evidence toward gate 4; weapon, Non-Work and other scenarios remain open.

The [save/reload follow-up](../../Tests/RIMWORLD-1.7-NEW-COLONY-2026-10-12.md#savereload-follow-up--october-12) loads `One_Seven_NO_MODS_02` and two outfit snapshots. Cindy and Vikky restore their loaded sessions successfully; the full excerpt has five completed restorations and 12 successful clothing endings with no failed steps or errors. This is a scoped active-outfit persistence pass toward gate 5, not migration or every transition phase. Birkey's last parka Wear is still underway when the paste ends, so its completion is pending.

The [weapon-only follow-up](../../Tests/RIMWORLD-1.7-WEAPONS-2026-10-12.md) closes Birkey's pending parka restoration and verifies eight successful work-weapon equips and six saved-weapon equips, including exact original SMGs. There are no failed weapon steps or error headlines. Rule-edit recalls and returns also work in the observed cases. Later plans revert to apparel-only after the weapon requirement is cleared; combined apparel/weapon behavior remains pending.

The subsequent [combined follow-up](../../Tests/RIMWORLD-1.7-WEAPONS-2026-10-12.md#combined-follow-up--october-12) verifies six combined lab-coat/beam-repeater preparations and five complete returns (Rhea twice, Vikky twice, Lu once). Exact original SMGs and Rhea's parka restore successfully; Birkey's last combined session remains active at the excerpt boundary. No clothing/weapon step fails. Successful Clean tasks are logged; the resumed cooking bill is not separately proven complete. Combined equipment sequencing now passes within this scope, while Non-Work and other gate-3 requirements remain open.

The [combined reload follow-up](../../Tests/RIMWORLD-1.7-WEAPONS-2026-10-12.md#combined-savereload-follow-up--october-12) loads three snapshots and verifies Rhea, Birkey and Vikky restore their loaded combined outfits and exact original SMGs; both saved parkas also restore. The full excerpt has 12 completed restorations, 27 successful clothing steps and 17 successful weapon equips without failed steps or errors. This passes active combined-state persistence within the observed scope; preparation/return saves, pre-save nonzero buffer continuity and 1.6 migration remain pending.

The [saved-personal Non-Work follow-up](../../Tests/RIMWORLD-1.7-NON-WORK-2026-10-12.md) verifies Rhea and Lu restore gear before native meal-job resumption, and Vikky subsequently prepares for Kitchen work again. The excerpt has 17 completed restorations and no failed clothing/weapon steps, but those totals include ordinary Kitchen cycles. Five hauling proposals encounter incompatible requirements along protected routes; Birkey also has an isolated wait while carrying a meal and later progresses. Actual meal completion, boundary crossing and the cause of the route conflicts remain unverified. This is a partial Non-Work pass, not closure of gate 3 or 4.

The subsequent [Non-Work video follow-up](../../Tests/RIMWORLD-1.7-NON-WORK-2026-10-12.md#video-follow-up--october-12) shows Lu eating, Birkey eating then cleaning and playing billiards, and Vikky in Dining with her personal SMG and pants rather than Kitchen gear. The supplied log adds six completed restorations, 17 successful clothing endings and nine successful weapon endings with no failed steps or conflict-delay messages. Ordinary saved-personal Dining/recreation is healthy in this scope. Painted boundary ordering, the earlier exact meal's outcome and the earlier hauling-conflict cause remain unverified; a different later meal does not close that exact continuation case.

**Next actionable step:** preserve the working configuration, then test an explicitly selected Dining outfit with saved-personal preference disabled and available apparel different from Kitchen's lab coat. Follow actual Dining activity and subsequent Kitchen preparation. Show painted Kitchen/Dining boundaries in the capture to resolve the outstanding layout/entry question. Do not attribute the earlier conflicts to 1.7 without further evidence.

## Maintainer decision — October 12

The maintainer requested proceeding with the 1.7 compatibility patch and explicitly accepted the gameplay coverage as sufficient. Remaining checks above are deferred, not passed, and no further gameplay testing is a prerequisite for this maintenance preparation. The candidate preserves the tested 0.4.6 DLL, adds 1.7 support metadata and maps both game versions explicitly to the shared assembly. See the [current maintenance checklist](CHECKLIST.md) and [readiness](../../Tests/NEXT-UPDATE-READINESS.md). The historical baseline and earlier next-test recommendations above remain dated evidence, not an active testing hold.
