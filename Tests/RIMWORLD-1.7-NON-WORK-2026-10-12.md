# RimWorld 1.7 saved-personal Non-Work audit

October 12, 2026. **Partial pass:** saved-personal restoration and subsequent Kitchen preparation succeed. Actual Dining boundary crossing, meal completion and the route-conflict cases remain unverified. No runtime changes were made.

**Follow-up status:** the later video below verifies ordinary Dining use, including visible eating followed by further activity, billiards and restored personal gear. Precise equip-before-boundary ordering and the earlier route-conflict cause remain open. The original excerpt assessment above is preserved as dated evidence.

## Evidence and provenance

- Maintainer attachment: `C:/Users/tekmojo/.codex/attachments/a6754349-e07d-43d4-940d-3c7176833798/Pasted text.txt`.
- SHA-256: `A29AD0E84C896107A835D190EF79694CAD88741DA50356D4D0373E9DD1E6B1C8`.
- 8,515 lines and 445 AOM messages. Explicit current-tick anchors run from 856710 to 876300, a span of 19,590 ticks.
- Earlier session startup established RimWorld 1.7.4976 rev1043, Harmony Mod 3.0.0.0 / library 3.0.0.0-rc.2 and AOM 0.4.6. This excerpt does not independently repeat startup versions, loaded DLL hash or mod list.
- No video accompanies this excerpt. The previously requested saved-personal / All Work Outfits configuration is context; the log identifies saved-personal Dining transitions but does not visually establish every rule setting or painted cell.

## Observed results

Across the entire excerpt, there are 17 completed restorations, 42 successful clothing endings and 28 successful weapon endings, with no failed clothing or weapon endings. There are 17 preparation plans and 17 prepared-job resumes. These totals include ordinary Kitchen cycles; they are not 17 completed Non-Work visits. No exception or error headline was found.

| Case | Evidence | Limit |
| --- | --- | --- |
| Rhea meal handoff | Lines 3941–4334: carries the meal toward the source locker, removes lab coat, restores original SMG `33340` and parka `29957`, clears the snapshot, then has native `Ingest #111265` targeting meal `54867` with no queued outfit steps | The same-meal resume message and admitted Ingest job do not establish arrival or consumption |
| Lu meal handoff | Lines 4717–4880: returns the beam repeater, removes lab coat, clears the snapshot and resumes native `Ingest #111400` targeting meal `54877` | No completed Ingest result or visual boundary evidence |
| Vikky returns to Kitchen | Lines 4402–4696: Non-Work buffer ends, followed by combined Kitchen preparation, successful work-weapon equip, exact DoBill resume and active Kitchen state | The resumed bill is not separately proven complete |
| Lu recreation proposal | Lines 6114–6249: Dining restoration is triggered by `Play_Billiards #112016`; beam repeater is returned and lab-coat removal succeeds | Endpoint is native Wait_MaintainPosture; billiards participation/completion is not established |

Six return decisions explicitly identify changing for a Non-Work area. Rhea also starts a compliant Dining session without claiming personal gear. Neither generic session wording nor zero Non-Work buffer-completion messages establishes a defect.

## Follow-up findings

Five `HaulToCell` proposals are delayed because Non-Work outfit requirements conflict with another area required by the job: Birkey at lines 128 and 4227, Cindy at 2033, and Vikky at 3066 and 5431. Each is preceded by a Dining qualification because every viable route crosses the protected area.

The current source explicitly blocks mutually exclusive requirements for overlap or multi-area jobs (`Source/Patches/PawnJobTracker_StartJob_Patch.cs`, around lines 3954–3995). This supports interpreting the messages as deliberate conflict handling. It does **not** establish which painted cells or route caused them, that the areas overlap, or that the behavior is a new 1.7 regression. Inspect the actual Kitchen/Dining layout before changing either rule.

Birkey has one recent-transition diagnostic at line 4554: a stationary Wait observed for 300 ticks while carrying meal `54870`, still in an active Kitchen session. Later Non-Work restoration and Kitchen activity demonstrate subsequent progress, but the fate of that particular meal remains unverified. The excerpt does not establish whether this wait was caused by one of the hauling conflicts.

Source inspection of `Source/Patches/NonWorkMealHandoff.cs` confirms that the same-meal resume message is issued when assigning the meal job, before consumption is established. Treating that message as a completed dining visit would overstate the evidence.

The excerpt ends with Lu starting an idle return decision. That unfinished endpoint is not a failed restoration. The five-pawn session does not end in a verified all-idle state.

## Next focused test

Keep the current configuration for the first capture. Show the painted Kitchen and Dining areas, their access routes and lockers, then follow a geared pawn through the locker change, Dining entry and actual eating. Include Birkey if the meal-carrying pause recurs. Capture the matching log. This will distinguish an area-layout conflict from failed continuation and verify the boundary ordering that the present log cannot show.

Selected-outfit Non-Work rules, unavailable-gear recovery and other [1.7 preparation gates](../docs/releases/RIMWORLD-1.7-PREPARATION.md) remain separate tests.

## Video follow-up — October 12

Evidence: `C:/Users/tekmojo/Documents/ShareX_HDR/ShareX/Screenshots/2026-10/QiVnh9fUf8.mp4` (118.100 seconds, 4162×1712, 30 fps) and `C:/Users/tekmojo/.codex/attachments/572215d7-2957-4771-899b-f06e7f59d1a7/Pasted text.txt`. Log SHA-256: `A2FF4E46275F3F2FFF61EDAD69904EF01F57AC4595CBCC1E787588D494EDE749`. These are supplied together; exact video-to-game-tick synchronization is not independently established. Runtime provenance remains the earlier session context.

**Verdict: healthy ordinary saved-personal Dining/recreation behavior in the observed scope.** Sparse contact sheets were followed by exact frames at 39, 60, 61, 63, 65, 73 and 75 seconds. Contact-sheet labels are sampling-bin labels; the timestamps below come from direct frame extraction.

- The Dining settings visibly show Default to Saved Personal Outfit enabled, All Work Outfits removal, and an Immediate task buffer.
- At 39 seconds Lu is at the Dining table with the occupant status `Consuming simple meal`.
- At 60–61 seconds Birkey is seated with a meal and the same consumption status. At 63 seconds the meal is no longer visible and his task is cleaning dirt; at 65 seconds he is at the billiards table with `Playing billiards`. This supports normal meal completion and continued activity visually. It does not identify this as the earlier unresolved meal `54870`, or provide a logged Ingest Succeeded result.
- At 73–75 seconds Vikky is wandering in the dining room. Her selected gear panel shows an excellent Heavy SMG, cloth pants and an inventory meal; no lab coat or beam repeater remains. Keeping her personal weapon is consistent with saved-personal mode. An inventory meal while wandering is not itself a stuck meal-carrying job.
- Later sampled frames show television and reading activity. Pawns move between rooms and continue activities; no persistent colony-wide Standing condition is established.

The new log has 3,150 lines and 170 AOM messages. It records six completed restorations, 17 successful clothing endings and nine successful weapon endings, with zero failed steps. Seven preparation plans and six exact prepared-job resumes are logged. Six plans concern GotoWander and one concerns Clean; they should not all be described as completed useful Kitchen work. Birkey's Clean does earn one logged buffer completion. Explicit current-tick anchors span 877110–911668 (34,558 ticks); no performance claim is made from this span.

No conflict-delay messages, meal-handoff messages, exception headlines, serialization warnings or rapid-job warnings occur in the new excerpt. Protected-route qualification messages still occur, including for hauling and bed access. Thus the absence of conflict-delay messages does not resolve the earlier layout question.

Lu and Birkey each have one 300-tick recent-transition Wait_Wander observation after their AOM state clears: no carried item, no outfit queue and no sampled rejected proposal. Both later progress. These are distinct from Birkey's earlier active-session wait with a carried meal. Seven locker-return rebuild messages are present, but successful subsequent steps provide progress evidence; they do not alone demonstrate an endless rebuild loop.

The final excerpt cuts off with Lu's new work-weapon equip admitted and Birkey's saved SMG equip admitted after successful parka restoration. Their final equip completions remain outside the paste. Do not count these as failures or completed returns.

The video shows the physical rooms, but the inspected frames do not expose painted Kitchen/Dining boundaries. Precise equip-before-entry ordering and the prior multi-area conflict remain open. No source edit, build or deployment is justified by this follow-up alone.

**Next distinct test:** save this working configuration, then test Dining with saved-personal preference disabled and one explicitly selected, available apparel type different from Kitchen's lab coat. Follow a pawn through dressing, actual Dining activity and subsequent Kitchen preparation; show painted boundaries in the capture to also close the outstanding entry/layout evidence gap. Keep missing-gear testing separate.
