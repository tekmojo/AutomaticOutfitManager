# RimWorld 1.7 new-colony audit — October 12, 2026

**Verdict: healthy apparel-only Work/locker flow in the supplied test; no persistent AOM loop or exception found.** This is a scoped gameplay pass, not complete 1.7 compatibility certification.

## Evidence and runtime

- Video: `C:/Users/tekmojo/Documents/ShareX_HDR/ShareX/Screenshots/2026-10/80xZxkaf7L.mp4`, 175.33 seconds, 3966x1676, 30 fps. Reviewed a sparse contact sheet and individual frames at 30, 45, 65, 75, 90 and 174 seconds. This is sampled visual evidence, not frame-by-frame proof of every boundary crossing.
- Log: `C:/Users/tekmojo/.codex/attachments/eca0dac0-5c63-4016-af00-96c30139fc89/Pasted text.txt`, 4,018 lines; SHA-256 `3FFAC4415F026F63B08FB6C11795AF97735116473864A23320E37EA420F687CA`.
- Log identifies RimWorld **1.7.4976 rev1043**, Harmony Mod **3.0.0.0**, library **3.0.0.0-rc.2**, and AOM **0.4.6**, Detailed logging.
- Single logged load: `One_Seven_NO_MODS`, Harmony + Core + all five official DLCs + AOM. Line 90 reports loading one pawn outfit snapshot. This does not by itself test a newly saved transition or 1.6-to-1.7 migration.
- Current live and RC DLLs were rehashed during this audit and both remain `A25B40980BBC58D419227DD1ECA4B8F855A8A8552F115E4688CA41D091FA98EE`. These are on-disk observations; the pasted log does not independently identify the loaded DLL path/hash.
- Rule: Kitchen, ID `8d5c40a08ee54d8d990538b7a0a9929d`, area `Kitchen 1`, locker `Locker 1`, lab coats, two-task buffer. No required weapon. Pawns: Cindy, Lu, Rhea, Vikky and Birkey.

Local derived evidence: `C:/GitHub/AutomaticOutfitManager/.codex-audit/1.7-new-colony-20261012/` contains the contact sheet, individual frames and `log-summary.json`, generated with the session-audit skill's summarizer. No game/mod configuration, source or runtime file was changed.

## Results

| Observation | Result and limits |
| --- | --- |
| AOM interface | Rule controls, availability, worker activity and item inspection render in sampled frames; panel views change during the recording. Does not cover every control or confirmation key. |
| Preparation and continuation | 12 preparation plans, 12 exact prepared-job resumes and 12 completed preparations. Successful Wear returns show native tracker ownership of DoBill, HaulToCell or GotoWander. |
| Useful activity | Video shows cooking, hauling and buffer activity. The sampled food inventory rises from 173 at 65 seconds to 196 at 174 seconds, consistent with ongoing kitchen production. Do not count every logged DoBill resume as a separately proven bill completion. |
| Restoration | 11 completions: Cindy 2, Lu 2, Rhea 3, Vikky 2, Birkey 2. 28 successful Wear/RemoveApparel endings, zero failed clothing endings. |
| Task buffer | Six completed increments across three full two-task buffers: Birkey Clean + Play_Billiards; Vikky Reading + HaulToCell; Birkey HaulToCell + Clean. Each full buffer leads to restoration. |
| Contention | Other workers defer while a prepared job holds the stove or hauling target, and the holder subsequently resumes its job. No reservation-failure warning or repeated preparation rebuild appears. |
| Sleep handoff | Rhea and Cindy restore before sleep. At 90 seconds all five pawns are in bed and the panel reports no active or returning workers, with seven lab coats available. Later footage resumes activity; the final log ends with Vikky actively cooking, not all pawns idle. |
| Errors | No exception/error/warning, rapid-job warning, unresolved cross-reference or not-deep-saved warning found in this supplied excerpt. Normal informational messages include stack traces. |

Counts above cover the whole excerpt: 192 AOM messages. Explicit current-tick anchors run from 693119 (line 155) to 728096 (line 4003), a span of 34,977 ticks with no decreasing anchors. Four AOM messages precede the anchored interval; no rate or CPU/TPS performance claim is made.

## Restoration waits are resolved

Rhea's three `unresolved restoration item` messages concern the same accessible saved parka, `Apparel_Parka29957`. Each is emitted during a zero-tick `Wait_MaintainPosture` handoff with exactly one queued Wear. Each resumes that existing queue and completes successfully:

- Lines 1388–1528: Wear starts at tick 696600; completion at 696814.
- Lines 2258–2389: Wear starts at tick 701130; completion at 701374.
- Lines 3633–3710: Wear starts at tick 726630; completion at 726921.

Seven `locker return became idle` messages occur across returns. They are followed by successful restoration, not sustained same-job churn. The maximum reported queue count is one. No preparation or restoration rebuild family appears. These messages do not establish a new 1.7 fault.

Birkey's exact saved parka `Apparel_Parka29995` is also successfully reworn twice (lines 2154 and 3377). This supports exact-item restoration in these accessible-item cases; it does not close the earlier blocked-item/full-storage issue.

## Save/reload follow-up — October 12

**Basic active-outfit reload passes for the observed Cindy/Vikky returns.** The maintainer reports completing the requested save/reload. The new excerpt explicitly loads `One_Seven_NO_MODS_02` with the same minimal mod list and reports two loaded pawn outfit snapshots (line 14). Before any new snapshot capture, Cindy and Vikky each return through the locker, successfully remove their work coat and clear their restored state (lines 23–247). The Kitchen rule ID and two-task buffer remain present. This demonstrates loaded-session restoration; the excerpt does not establish persistence of a nonzero buffer count or every exact pre-save job/phase.

Evidence: `C:/Users/tekmojo/.codex/attachments/e08b8d8f-9fde-408f-9a43-5e0039858b5f/Pasted text.txt`, 1,656 lines; SHA-256 `4A0579C2E1342DACB491E71D5040EEA425253FE5762D7420F9033995FE6F8D99`. The excerpt begins at save loading and does not repeat the game/AOM/Harmony startup versions or loaded DLL hash; those remain session context from the previous evidence. Summary: `C:/GitHub/AutomaticOutfitManager/.codex-audit/1.7-new-colony-20261012/reload-summary.json`.

- Whole excerpt: 92 AOM messages, five completed restorations (Cindy 1, Vikky 2, Lu 1, Rhea 1), 12 successful clothing endings, zero failed clothing endings, five preparation plans and five exact-job resumes. No exception, serialization warning, unresolved cross-reference or rapid-job warning appears.
- Explicit tick anchors: 729270–748020, spanning 18,750 ticks. No decreasing anchors; counts above are whole-excerpt totals, not normalized rates.
- Rhea's same saved parka `Apparel_Parka29957` successfully restores at tick 745821 after a queued handoff. The six locker-return rebuild messages do not show sustained same-job churn; the last return is still in progress at the excerpt boundary.
- Additional protected-entry evidence: Birkey is stopped outside Kitchen at `(144, 0, 141)` before crossing to `(144, 0, 142)` during Ingest (line 1006), equips the lab coat, and the native tracker owns the exact prepared `Ingest #93503` after successful Wear (line 1294). Other pawns defer access to his claimed meal. The excerpt does not explicitly log successful ingestion, so this supports boundary interception and job admission, not a completed-meal pass.
- Birkey's final parka restoration is **unfinished in the supplied excerpt**: RemoveApparel succeeds, Wear of `Apparel_Parka29995` starts at tick 748020, and the brief wait ends, but the paste stops before Wear success/state clearing. Do not count that sixth restoration as passed or failed. Rhea has also begun another active cooking cycle at the endpoint.

No code, build, deployment or game-state change follows from this audit. Save/reload during preparation/return, weapon state, nonzero buffer progress, Non-Work and 1.6 save migration remain untested.

## Next focused test (updated after reload)

Keep the minimal mod list. Let Birkey finish the already-started parka restoration and retain its completion in the next log. Then test a weapon-only rule with an available weapon different from the pawn's current primary weapon, following native work and return to the original weapon state. After that passes, add the lab-coat requirement for a combined apparel/weapon cycle.

Non-Work entry, unavailable gear, interruption, method-body patch scenarios, full-mod-list interactions and 1.6 save migration remain separate pending cases in the [1.7 preparation checklist](../docs/releases/RIMWORLD-1.7-PREPARATION.md). Earlier [0.4.6 deferred issues](PATCH-FOLLOWUP-0.4.6.md) remain open.
