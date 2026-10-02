# Full session audit — 2026-10-02

## Verdict

Normal outfit transitions pass in this recording, including Moto's ordinary pants adoption and Gonzo's original outfit return. No failed apparel/weapon endings, rapid-job warning, or sustained AOM transition loop was found. There is a confirmed misleading downed-pawn status label and a separate native autosave warning about a missing pawn relationship reference. The latter is not attributable to AOM from the available evidence. Anomaly's weapon-storage warning agrees with the saved room filters.

This was a read-only gameplay audit. No runtime source edits, build, deployment, game launch, or original-save changes were made. A copy of the latest matching autosave was preserved for inspection. Documentation changes only.

## Evidence boundary

- Video: `C:/Users/tekmojo/Documents/ShareX_HDR/ShareX/Screenshots/2026-10/9SbUorQ0Xr.mp4`, 1,099.5 seconds (18:19.5), 3830 × 1706, 30 fps. Reviewed sparse contact sheets across the recording and detailed frames around clothing, ritual, rescue, storage and status changes.
- Log: `C:/Users/tekmojo/.codex/attachments/27623682-2656-44c6-86b1-189a03a4295b/Pasted text.txt`, 12,422 lines, SHA-256 `CADEBFA0D38EBB7DFF560CDF740C1C324AB1270FD70BF6B53E1443BBFB35DCE3`.
- Gameplay interval: load of `GooseButter_ANOM_ALL_MODS` at line 1647 through line 12422. Explicit current-tick anchors span 54273480–54334470 without reversal. Counts below cover the entire selected excerpt; they are not normalized performance rates.
- Latest deployment record and current live DLL: `74C5192A1AB2B70A3F3886A82261D877BE7CA16A22E7D8869EDA70EBF8A2B6D8`. The log announces 0.4.5, not its DLL hash; runtime attribution rests on the deployment chain and observed new adoption behavior.
- Preserved `Autosave-2.rws` copy: tick 54332696, SHA-256 `72754ED83B2A96E9C07391A2D1F5D6F100AC407458D1FC2D446D39903C630608`. This precedes the final log tick. It is not an end-of-recording save or a demonstrated reload.
- Local derived evidence: `C:/GitHub/AutomaticOutfitManager/.codex-audit/full-27623682/`, including `autosave-copy.rws`, contact sheets, exact-time frames, `events.txt`, `gameplay-summary.json`, `component.xml` and `save-summary.json`.

## Confirmed normal-flow results

### Moto's pants are retained

Moto (`Human1405898`) completes an initial restoration at line 3039. At line 3088 he adopts the exact Lightleather pants `Apparel_Pants3775625`, Normal/90%, into the existing saved personal outfit. His next Radiation snapshot includes them at line 3129. The next return completes at line 3833 without a pants-removal step. The video later shows him consuming stew in Dining Room 1. The autosave has the same exact pants both worn and in his saved Non-Work outfit, with his original Hellgun equipped and no active intervention.

This validates the previously failing ordinary Wear → saved preference → subsequent work/return/dining path. It does not establish every replacement or persistence case. DJ, Roboto and Zendaya also log successful ordinary personal-garment adoptions; these are not evidence of a tattered-item replacement.

### Gonzo's saved outfit return completes

Dining triggers Gonzo's return from Radiation at lines 4125–4166. He returns the Heavy Bolter, restores his original outfit and completes at line 4602. The autosave contains all seven exact saved garments worn, his original Autogun `GW_HOI_Gun_Agripinaamkii2390927` equipped, ordinary DoBill activity, and no active intervention. Moto's and Gonzo's normal returns are verified for this session.

### Ritual, incapacitation and rescue

The Void provocation ritual completes. Native save evidence records the completion letter at tick 54326345 and Arakis's `DarkPsychicShock` added at that same tick. His collapse is the ritual's short-term coma outcome, not an outfit-removal fault.

AOM suspends Arakis's locker travel and restoration at line 7609. Jumper equips the required Anomaly clothing, then actually starts the native Rescue job (line 9156); this is more than a proposed job resume. The autosave places Arakis in the medical bed with the shock still active. At line 11789 he can move again, and his original jacket, hat and cassock are restored before completion at line 12378. The final successful Wear callback after state clearing is cleanup, not a new restoration cycle.

All five observed ritual participants return: Zendaya (9066), Lodewijk (9799), Fausto (11242), Gonzalez (11391), Arakis (12378). Interrupted StandAndStare ceremony-stage jobs do not receive buffer credit. Lodewijk and Fausto each complete their configured Sow buffer; MJ's two Train completions lead to restoration at line 12023.

Other rescuers defer while Jumper owns the target claim. His initial robe step to actual Rescue spans 1,878 game ticks. Preparation delays rescue, but this instance finishes without an unresolved claim or deadlock. Natural recovery during continued play is observed; reloading the suspended state is untested.

## Findings and remaining uncertainties

### 1. Missing pawn relationship reference in the autosave

Line 11557 warns: `Thing_Human3840719` is referenced by `otherPawn` but not deep-saved, which may cause loading errors. Inspection of the copied XML confirms no definition for that pawn and two dangling native social references:

| Existing pawn | Save location | Relationship to missing pawn |
| --- | --- | --- |
| Human171539, Grigos “Philonus” Philonus | worldPawns/pawnsAlive | Parent |
| Human1815399, Lisphoteos Phonek | worldPawns/pawnsMothballed | Spouse |

Both relation records start at tick 54293000. No missing `Thing_` references were found in the AOM component against saved thing definitions. That is a structural check, not successful deserialization. The stack is native autosave serialization and does not establish the responsible mod. Do not label the whole save corrupt or blame Hospitality/AOM without a load result and source evidence.

Next test: manually load a copy of the preserved autosave, inspect the new load log and relationship behavior, and follow Arakis's suspended restoration and MJ's in-progress return. Preserve the original save. This recording contains no such reload.

### 2. Downed restoration status is misleading

At approximately 14:40, Arakis is downed while his row says **Waiting for saved outfit item**. The log correctly reports downed suspension. `Source/UI/PawnAutomaticOutfitStatus.cs`, `TransitionLabel`, handles drafting, LayDown and forced activity before the generic restoring/idle label, but lacks a downed/suspended check. The managed row therefore implies missing equipment while he is incapacitated.

Recommended narrow correction: display **Restoration paused — downed** before the generic idle-item wait. This is a status-text fault; subsequent rescue and restoration succeed. Not changed in this audit.

### 3. Anomaly storage warning matches its filters

At approximately 16:40 the warning names Autogun U90 (Excellent/99%) and Autogun Agripinaa MK II (Excellent/99%). The copied save's `Locker Anomaly 1` area has apparel lockers and other filtered storage, but none accepts these weapon definitions. The warning assesses relevant saved originals, including items that may still be held; it does not prove both guns lie on the floor or that their owners are blocked.

The two personal-apparel lockers accept 506 apparel definitions with full condition/quality ranges; the two work-only lockers accept the robe and hood with Normal-or-better/60–100% limits. The other in-room shelves/crate also exclude the two rifles. If local storage of these guns is desired, suitable storage inside the intended locker must allow them. No filters were altered.

Successful local saved-clothing retrieval is observed, including Arakis's items around the Anomaly locker. No confirmed cross-locker export regression was found, but this recording does not prove all storage-capacity, floor fallback, hauling and protected-recovery branches. The differently graded guns visible in the outside container near the end are not sufficient evidence that the two warning items were exported.

### 4. Previous guest continuation case remains open

This is a different guest group. Lefrich is observed claiming a bed; guest Orthospar has real Dining Room DoBill activity in the autosave. No matching Anomaly preparation for this group's arrival appears in the selected log. This does not reproduce or close the previously documented Hospitality arrival-Goto continuation issue involving the older group.

### 5. Other coverage limits

MJ successfully recovers Radiation protection while already in the occupied area, later completes the two-task buffer and restores. The evidence does not establish how the initial unprotected entry occurred, so equip-before-entry is not graded as passed or as a confirmed new defect. Aoroto and Bracher remain in legitimate active work in the autosave; ongoing work is not failed restoration.

Two startup AudioClip load exceptions and missing Wildebeest/Zebra angry clips precede the selected gameplay interval. They are separate asset issues, not evidence of an AOM clothing fault. No CPU/TPS conclusion follows from diagnostic counts.

## Log totals and next priorities

The selected interval contains 483 AOM messages, nine preparation plans, nine logged prepared-job resumes, three locker-return rebuilds, ten completed restorations, 65 successful apparel endings and seven successful weapon endings. There are no failed apparel/weapon endings. The three locker returns that rebuild later complete. Five work-buffer completion messages are milestones, not a count of every native job. No sustained Standing/Wait loop or `10 jobs in one tick` warning was found.

The summary script's concern heuristics did not catch the missing-reference warning; the finding above comes from manual log/XML inspection. Do not treat its empty gameplay concern list as a clean-save verdict.

Priorities: (1) test loading the preserved autosave copy, (2) correct the downed status label when authorized, (3) supply accepting Anomaly weapon storage if desired, and (4) retain the focused Hospitality arrival-interruption test. Current tests close the specific Moto pants regression and the observed normal outfit returns, not broader save/load or compatibility gates.

## Subsequent authorized release preparation

The [0.4.6 preparation](UPLOAD-PREP-0.4.6.md) corrects the downed status label and its detail/Non-Work overrides. The release build is deployed for the maintainer's upload; visual confirmation of that new label remains pending. This does not alter the read-only scope or recorded outcomes of the audit above. Save/reload and guest findings remain open.
