# Small-map retest audit — October 2, 2026

Verdict: **clean AOM behavior for the observed run; the earlier blocked-vest recovery case was not reproduced.** Ocag completes five restorations, continues cooking/butchering, resumes eating and reaches bed. The maintainer reports freeing Kitchen locker space. That is consistent with avoiding capacity problems, but this run also follows a different apparel path: Ocag keeps the 51% sash, while Bowman adopts the exact vest involved in the previous failure.

This is a read-only session audit. Only audit records were updated; no source, build, deployment, original save, game settings or Workshop publication was changed.

## Evidence and scope

- Video: `C:/Users/tekmojo/Documents/ShareX_HDR/ShareX/Screenshots/2026-10/7jykyvZAFj.mp4`, 347.133 seconds, 4242×1666, 30 fps. Sparse contact sheets cover the recording; larger frames inspect saved apparel and final movement/resting states.
- Log: `C:/Users/tekmojo/.codex/attachments/98c86bc4-1462-465b-bc5b-1ce701787df0/Pasted text.txt`, 19,857 lines; SHA-256 `FC0677084FB8C615EDF75F0C625E7946B0BE55922AC01763FEEBAE119BB568E2`.
- One visible load: `AnuStart_01_ALL_MODS` at line 40. Counts below use lines 40–19857. Current-tick anchors run from 7626679 to 7670783; no performance rate is inferred.
- Installed DLL rechecked at `F:/Steam/steamapps/common/RimWorld/Mods/AutomaticOutfitManager/1.6/Assemblies/AutomaticOutfitManager.dll`: `A25B40980BBC58D419227DD1ECA4B8F855A8A8552F115E4688CA41D091FA98EE`, matching the prepared 0.4.6 runtime. The installed junction still targets the preserved upload stage. This excerpt does not independently announce its loaded version; the visible revised saved-outfit explanation also matches the prepared release.
- Local evidence: `C:/GitHub/AutomaticOutfitManager/.codex-audit/smallmap-98c86bc4/`, including summary, numbered events, contact sheets and exact frames. No new end-save or reload of this run's changes was audited.

## Outcomes

| Observation | Result |
| --- | --- |
| Completed restorations | 26: Ocag 5, Hanh 4, Lumi 4, Reba 4, Bowman 4, Jonah 3, Jono 2 |
| Apparel step endings | 176 succeeded; 0 failed |
| Weapon step endings | 18 succeeded; 0 failed |
| Preparation plans / prepared-job resumes | 29 / 29 messages; these are not a count of completed native work jobs |
| Preparation / restoration rebuilds | 0 / 0 |
| Locker-return rebuilds | 4; each followed by that pawn's completed restoration |
| Work / Non-Work buffer milestone messages | 21 / 8; sampled milestones are not total completed tasks |
| Protected-route failure / recovery query | No `protectedReach=False` and no saved-item recovery query |
| Empty restoration plan / rapid-job / failed-reservation warning | None found |

All 29 restoration-wait snapshots contain queued work, and each has a subsequent restoration completion for the same pawn. Repeated Hanh/Reba snapshots show shrinking queues and successful progress. The four idle-return rebuilds resolve: Lumi 819→1970, Reba 2506→3862 and 9875→10315, Hanh 14394→15817. These do not match the prior persistent empty-queue wait. Successful completion callbacks after snapshot clearing are trailing callbacks, not renewed restoration.

## Ocag and the exact vest

Ocag's completed restores are at lines **3529, 8253, 10773, 15014 and 17459**. Later Kitchen snapshots at **6455, 9054, 11741 and 15982**, plus the USS snapshot at **17547**, consistently retain the Poor **51%** plainleather sash. The saved-outfit viewer around **3:10** independently shows that sash, the Good 99% parka and the original personal outfit. There is no Ocag tattered-apparel replacement in this run.

In the [previous audit](OCAG-BLOCKED-VEST-AUDIT-2026-10-02.md), the sash reached **49%**, Ocag successfully adopted `Apparel_FlakVest1659117`, and a later return stalled because that exact vest was inside an unrelated protected area with no usable recovery destination.

This time Bowman adopts a Masterwork flak vest after successful ordinary Wear (**5375**); subsequent exact-item jobs identify it as **`Apparel_FlakVest1659117`**. Bowman successfully restores it at **8913, 13454 and 17281**. At **16950**, it is at `(133,0,127)` with `protectedReach=True`. This is accessible retrieval for Bowman, not proof of hauling Ocag's inaccessible item out of a protected area. Lumi separately adopts vest `Apparel_FlakVest1659110` (**5355**, later exact references). Neither adoption indicates unexplained loss of Ocag's saved outfit.

The capacity change is maintainer-reported; exact deleted items, shelf capacity at each decision and unchanged filters are not established by the recording. Freeing accepting space can improve storage availability. It cannot by itself explain this run's success because the owner, garment and retrieval route also differ. No recovery query exercises the earlier no-destination condition here.

## Work, meals, transit and endpoint

- Kitchen work resumes after preparation, including butchering and cooking visible in the recording. Buffer completion leads to returns; temporary native target-claim deferrals do not become a stuck preparation.
- Ocag's Kitchen-to-Dining meal handoff at **10196–10821** restores saved gear, then the final native callback reports actual current **Ingest** of `MealSimple2156065`, with state cleared and an empty queue (**10821**, tick 7644200). This is stronger evidence than a resume proposal alone.
- Ocag's later incompatible Kitchen→USS transition restores the old outfit first (**17084–17459**), then prepares the USS outfit and equips Shocking Laughter successfully (**17941**) before resuming the bed job (**17966**).
- Hanh's later simple-meal sequence progresses through restoration (**19238**); the video around **5:10** shows actual consuming, followed by preparation for rest. The last logged weapon step succeeds (**19786**) and bed preparation completes (**19835**).
- At **5:30**, Ocag is moving for a packaged survival meal and Hanh is still preparing. By **5:45**, all seven listed pawns, including Ocag and Hanh, show **Active: Resting** and are visibly at their beds. There is no stranded endpoint pawn. The USS rule remains active during rest; a later return from that final activity lies outside this recording.
- Route-qualified transit proposals occur, but this is not the earlier Hospitality guest-arrival case and does not validate that compatibility issue. This run also does not exercise blocked-item recovery, downed suspension or a reload of newly changed outfit state.
- Revised saved-outfit explanatory text and locker tooltip are visible. The About card and downed status wording are not verified here.

## Separate load/compatibility findings

The log is not globally error-free. It contains a missing **`Fortified.MapComponent_ModificationIndex`** class (**267**) followed by failure to load the fallback abstract **`Verse.MapComponent`** (**291**). There are also **12 PostLoadInit null exceptions** on modded armor items (**337–560**). The first full stack enters `LayeredApparel.CompLayeredApparel.UpdateLinkedStates` during a GW4K armor color/ExposeData callback. These are load/compatibility concerns; the available stack and later healthy AOM transitions do not establish an AOM cause or connect them to the prior vanilla flak-vest stall.

No missing-pawn serialization, `not deep-saved`, rapid-job or failed-reservation warning was found in this excerpt. The separate large-colony autosave warning remains separate evidence, not a finding on this small-map run.

## Focused follow-up

Record this as a normal-flow pass with no reproduced Standing fault. Preserve the earlier blocked-item case as open, rather than claiming a code fix or capacity-only diagnosis. If that blocked state is still available, the shortest decisive check is to provide one free accepting Kitchen storage destination and observe the exact vest being recovered, worn by its saved owner, and restoration clearing. If only this restarted baseline remains, another ordinary successful cycle cannot close that recovery case; an exact blocked-item reproduction is needed.

Further release closeout and public/subscriber verification have not been performed. No additional long audit is required merely to reconfirm the normal paths that passed here.
