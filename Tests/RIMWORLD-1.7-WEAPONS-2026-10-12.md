# RimWorld 1.7 weapon audit — October 12, 2026

**Verdict: weapon-only equip/return and exact saved-weapon restoration are healthy. The subsequent combined test below also confirms apparel-plus-weapon preparation and five completed combined returns.** No implementation change is indicated by this evidence.

## Evidence

Maintainer attachment: `C:/Users/tekmojo/.codex/attachments/2375046f-78ff-4966-8b5a-943a5790ab57/Pasted text.txt`, 4,448 lines, SHA-256 `062008AE6E60E3CD1D7F40BE08911B6FD18E7495C7FCCF83D1D7D943502D0C61`. It contains log messages and stack traces, with no separate prose request. No video was supplied for this step.

This continues the [new-colony/reload test](RIMWORLD-1.7-NEW-COLONY-2026-10-12.md) using the same Kitchen rule ID `8d5c40a08ee54d8d990538b7a0a9929d`. It does not repeat startup versions, the mod list or loaded DLL identity. RimWorld 1.7.4976 / Harmony 3.0.0-rc.2 / AOM 0.4.6 are prior-session context, not independently established by this excerpt.

Derived summary: `C:/GitHub/AutomaticOutfitManager/.codex-audit/1.7-new-colony-20261012/weapon-summary.json`. Whole-excerpt counts: 233 AOM messages, **eight successful work-weapon equips**, **six successful saved-weapon equips**, zero failed weapon equips, 13 successful clothing endings and zero failed clothing endings. Six apparel restoration-complete messages and ten compliant-session clears are distinct event families; do not combine them into an unqualified outfit-cycle count. Explicit tick anchors span 748110–796394 (48,284 ticks), with no decreases. No performance claim follows from these counts.

## Verified behavior

- **Previous pending restoration closes.** Birkey's exact `Wear #93947` of saved parka `Apparel_Parka29995` succeeds at tick 748330 and his snapshot clears (lines 111–132). This closes the pending endpoint of the reload excerpt.
- **LMG weapon-only preparation.** Birkey, Lu and Rhea equip their selected LMGs successfully. Plans explicitly specify zero apparel items. Birkey resumes hauling, Lu resumes DoBill and Rhea resumes GotoWander. These resumes alone do not prove every native job completed.
- **Original primary weapon restoration.** Birkey returns his LMG and successfully equips his original masterwork Heavy SMG `33341` (line 944). Rhea returns her LMG and restores her original masterwork Heavy SMG `33340` (line 1702). Rhea/Vikky's hauling makes those saved weapons available without reported reservation errors.
- **Unarmed baseline.** Lu's new LMG session starts with `primary=#-1` and later returns the LMG and clears the session. Cindy's beam-repeater session likewise starts unarmed, then returns the weapon and clears. No saved-weapon Equip is needed for those unarmed baselines.
- **Beam-repeater changes.** Birkey and Vikky acquire required beam repeaters during occupied-area rechecks; both finish preparation and later restore their exact SMGs (`33341` and `33348`). Birkey's further normal/excellent beam-repeater sessions also restore `33341`, including after the weapon requirement is cleared.
- **Rule-edit recalls.** Clearing apparel/weapon requirements triggers recalls and managed-gear return. Cindy and Lu release selected shared weapons from personal snapshots and return their temporary LMGs before later sessions; these cleanup messages are not evidence of item loss.
- **Unavailable gear.** One Vikky DoBill delay reports no reachable LMG. Subsequent successful sessions elsewhere do not prove that exact deferred DoBill's completion, but there is no repeated empty-plan rebuild in this excerpt.
- **No new fault established.** No exception, error, serialization warning, missing-reference warning, reservation failure or rapid-job warning is present. Rhea's single observed native Wait_Wander occurs with AOM state/queue empty and no rejected proposals; that observation is not a persistent AOM loop. Reported outfit queues never exceed one item.

## Combined test was open after the first excerpt

The log first clears apparel and runs **zero apparel + weapon** plans. At line 2988 it clears the weapon requirement, then later plans explicitly report **one apparel item + no weapon**. No plan prepares both a garment and a weapon. These are separate weapon-only and apparel-only tests, so this excerpt does not close combined preparation/restoration.

At the end, Rhea and Lu have successfully restored, but Vikky has begun another active apparel session and Birkey/Cindy are still in preparation according to their last messages. These endpoint sessions are not counted as completed or failed.

## Combined follow-up — October 12

The next maintainer attachment explicitly exercises **one apparel item and a beam repeater together**: `C:/Users/tekmojo/.codex/attachments/bff6bcb0-0bee-4fe2-95c0-7200e7cd679a/Pasted text.txt`, 2,925 lines, SHA-256 `AE1D44C45C5173F575236749D4A3545673660EC9C808F28071F69742692FB616`. No additional prose request or video accompanies this log. Startup versions and loaded DLL identity are not repeated. Summary: `C:/GitHub/AutomaticOutfitManager/.codex-audit/1.7-new-colony-20261012/combined-summary.json`.

**Scoped combined equip/restore pass:** six combined preparation plans complete; five of those sessions restore fully within the excerpt (Rhea twice, Vikky twice, Lu once). Birkey's sixth session equips both successfully and remains active at the endpoint; its later return is not counted as passed or failed. Three earlier apparel-only returns caused by the rule edit complete before the combined cycles, making eight total restoration messages across the whole excerpt.

| Pawn/session | Preparation and activity | Completed return |
| --- | --- | --- |
| Rhea, first | Lab coat `35834` + beam repeater `48263`; GotoWander resumes | Coat removed, exact parka `29957` reworn, exact masterwork Heavy SMG `33340` equipped; snapshot clears at lines 901–922 |
| Lu | Lab coat `35836` + beam repeater `48263`; exact DoBill resumes, then one Clean buffer task completes | Beam repeater returned, coat removed, original unarmed baseline restored; snapshot clears at lines 1762–1783 |
| Vikky, first | Lab coat `35830` + beam repeater `48258`; GotoWander resumes, then one Clean buffer task completes | Coat removed and exact excellent Heavy SMG `33348` equipped; snapshot clears at lines 1962–1983 |
| Rhea, second | Lab coat `35834` + beam repeater `48259`; GotoWander resumes | Exact parka `29957` and SMG `33340` restored again; snapshot clears at lines 2229–2250 |
| Vikky, second | Lab coat `35836` + beam repeater `48263`; GotoWander resumes | Exact SMG `33348` restored after coat removal; snapshot clears at lines 2883–2904 |
| Birkey | Lab coat `35831` + beam repeater `48260`; GotoWander resumes and later LayDown route qualifies through Kitchen | Return not shown before the excerpt ends |

Whole-excerpt totals: 159 AOM messages, **19 successful clothing endings**, **10 successful weapon equips** (six work weapons, four saved weapons), no failed clothing/weapon endings, six prepared-job resumes and two successful Clean buffer increments. Tick anchors span 796500–820381 (23,881 ticks), without decreases. These totals include the initial rule-edit cleanup; they are not all new combined cycles.

No exception, error, serialization/reference warning, reservation failure or rapid-job warning appears. Brief restoration waits continue existing queues; Rhea's two-item queue correctly shrinks through parka Wear and original-weapon Equip. Four locker-return rebuild messages lead to completed restoration. Vikky's one native Wait_Wander observation occurs with no AOM state or queued job and no rejected proposals; it does not establish a persistent outfit loop.

This verifies combined equipment sequencing, exact original-gear restoration, normal unarmed return, and successful Clean tasks while managed. Most entry proposals are GotoWander; Lu's DoBill resume is not independently logged through bill completion, so do not claim six completed work jobs. The rule edit also recalls earlier preparing/active sessions successfully. Save/load during combined preparation, active use or return remains untested.

## Combined save/reload follow-up — October 12

**Combined active-outfit reload passes for the observed loaded sessions.** The next excerpt loads `One_Seven_NO_MODS_02` with Harmony, Core, all five official DLCs and AOM, and reports **three loaded outfit snapshots**. Rhea, Birkey and Vikky each return combined work gear and restore their original items before any new snapshot capture for that pawn appears. This is direct post-load continuation evidence rather than counting only new interventions created after reload.

Evidence: `C:/Users/tekmojo/.codex/attachments/d9a0bf5c-ee66-482c-8b08-a20aa09923ed/Pasted text.txt`, 5,355 lines, SHA-256 `DAF1FB691BC76D7F6483E7F87B0D2BB43A6737A390205850E8AC8D9EDFD9AEC7`. No new video or separate prose request accompanies the log. Startup versions and loaded DLL hash are not repeated; retain the earlier session provenance limits. Summary: `C:/GitHub/AutomaticOutfitManager/.codex-audit/1.7-new-colony-20261012/combined-reload-summary.json`.

- **Rhea:** two buffer tasks complete after loading; work beam repeater and lab coat return, exact parka `29957` is reworn at tick 841760, exact masterwork SMG `33340` equips at 841925, and her snapshot clears (lines 233–517).
- **Birkey:** returns the combined outfit through the locker; exact masterwork SMG `33341` equips at tick 844567 and exact parka `29995` Wear succeeds at 844834, clearing the session (lines 1202–1598). His next snapshot starts later at line 1726. This is the loaded session in this save, not proof that the precise earlier recording-end session persisted unchanged across the unrecorded interval.
- **Vikky:** Reading and HaulToCell complete the two-task buffer; beam repeater and lab coat return, exact excellent SMG `33348` equips at tick 846024, and the snapshot clears (lines 1812–2069). A new capture occurs only later at line 3418.
- **Continued play:** whole-excerpt totals are 12 restorations (Rhea 2, Birkey 2, Vikky 2, Lu 2, Cindy 4), 27 successful clothing endings, 17 successful weapon equips (11 work, six saved), 11 preparations/resumes and 13 completed buffer increments. No equipment step fails. Four two-task buffers complete; remaining increments are partial and include an unstamped tail event. Do not infer a preserved pre-save nonzero count without the actual pre-save state.
- **No new error:** no exception, serialization/reference warning, reservation failure or rapid-job warning appears. Queues never exceed the two expected remaining gear actions and progress to completion. Ten locker-return rebuild diagnostics accompany successful returns, without preparation/restoration rebuild loops. Cindy's native-wait observation at tick 850740 has no active outfit state and is followed by new preparation at 850754.
- **Endpoint:** Vikky has restored; Cindy and Birkey have subsequent active work. Do not describe all pawns as idle or claim all started sessions complete.

Explicit current-tick anchors run from 840629 to 855185 (14,556 ticks), with no decreases. Counts above cover the whole excerpt; no rate or CPU/TPS claim is made. This verifies active combined-state persistence, original gear identity and post-load buffer operation. Save/load during preparation/return, preserved nonzero buffer progress and 1.6 migration remain separate untested cases.

## Next test (updated after combined reload)

Keep the combined Kitchen rule and minimal mod list. Add a separate Non-Work area covering the dining/recreation room, without overlapping Kitchen. Leave **Default to Saved Personal Outfit** checked and **Remove Work Outfits** set to **All Work Outfits**. Observe a pawn wearing both Kitchen items naturally choose eating/recreation there: work gear must return and the personal outfit restore before Non-Work entry. Then observe a new Kitchen task re-equip both items. Prefer video plus log for the exact entry boundary; ordinary idle return alone does not establish this behavior. Selected-outfit Non-Work mode is a separate later check.

No source edits, build, deployment or game changes were performed for this audit. See the [1.7 preparation checklist](../docs/releases/RIMWORLD-1.7-PREPARATION.md) for broader open cases.
