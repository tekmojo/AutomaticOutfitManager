# Unattended Standing reproduction audit — October 2, 2026

Verdict: **clean AOM behavior for the observed scope; the persistent Standing fault was not reproduced.** All seven colonists complete restorations and reach bed at the video endpoint. The full matching process log records 47 completed restorations, 317 successful apparel endings and 32 successful weapon endings, with no failed outfit steps. Temporary waits and contention recover without a manual rescue. The run is not globally error-free: existing load/compatibility errors and Research Reinvented warnings remain.

The maintainer describes two days of play without manual intervention. The independently anchored log interval is **98,537 ticks (about 1.64 in-game days)**, from 7,626,679 to 7,725,216. The evidence therefore supports an extended unattended run, not a claim of a measured full 120,000-tick window. No source, build, deployment, original save, game settings or publication was changed; only audit evidence and current validation records were written.

## Evidence and scope

- Video: `C:/Users/tekmojo/Documents/ShareX_HDR/ShareX/Screenshots/2026-10/NfEWkYQrV0.mp4`; 422.467 seconds, 3862×1738 at 30 fps, 1,170,964,007 bytes. SHA-256 `EF8F1D546964FB6FEA9DB21AACE33A9C6479F9CB73802FBE3BF2A7A70E22DF20`. Sparse 15-second contact sheets span the recording; exact frames inspect work, restoration, movement and both sleeping endpoints. Contact-sheet samples are approximate; cited exact times use separately extracted frames.
- Pasted log: `C:/Users/tekmojo/.codex/attachments/5e36d403-c17d-4af0-ada3-b4b11eedf901/Pasted text.txt`; 20,571 logical lines, SHA-256 `857B7187A2865C8D7790661D712497E089E1C65567556C94CF34474F7E50838C`. It begins during a Research Reinvented warning series and contains only the later 975 AOM messages.
- Full matching `Player.log` copied read-only into the audit directory: 780,149 bytes, 3,534 logical lines, SHA-256 `A6083DF85120B8BC16F38BF6618A4545E05832B077A3E1566FECB9E4C7B3D78C`. The pasted AOM message sequence matches its final 975 AOM messages exactly. Counts from these overlapping sources must not be added.
- Full log announces **0.4.6, Detailed logging** at line 25 and one load of **AnuStart_01_ALL_MODS** at line 1000. Session counts use full-log lines **1000–3167**; startup is audited separately. Tick anchors at lines 1272 and 3165 are monotonic. Startup and shutdown allocator statistics are not gameplay events.
- RC, live and installed DLLs all hash to `A25B40980BBC58D419227DD1ECA4B8F855A8A8552F115E4688CA41D091FA98EE`. Installed path is the junction to `C:/GitHub/AutomaticOutfitManager`, consistent with completed 0.4.6 closeout. This is local development-copy gameplay, not subscriber-only gameplay.
- Evidence directory: `C:/GitHub/AutomaticOutfitManager/.codex-audit/twoday-5e36d403/`. It contains both log copies, full/selected/pasted summaries, numbered events, contact sheets and exact frames. References below use **logical line numbers in `numbered-player.txt`**, produced by the audit summarizer's `splitlines()` convention; embedded Unicode line separators can make raw editor line numbers differ slightly.
- The video shows the same small colony, Kitchen, Dining and USS Pissant rules. Kitchen buffer is one task; USS buffer is three. The user reports no manual gameplay intervention; sampled UI interaction is inspection. Exact storage filters/capacity and a newly saved endpoint were not inspected.

## Full-session results

| Check | Result |
| --- | --- |
| Completed saved-outfit restorations | **47**: Ocag 8; Hanh, Lumi, Reba and Bowman 7 each; Jonah 6; Jono 5 |
| Apparel endings | **317 Succeeded, 0 failed** |
| Weapon endings | **32 Succeeded, 0 failed** |
| Preparation plans / exact-job resume messages | 52 / 53; the loaded state and log boundaries differ, so these are not paired task totals |
| Preparation rebuilds | 0 |
| Locker-return rebuilds | 16; every one has a later same-pawn completion |
| Restoration rebuilds | 1; Hanh successfully wears the recovered jacket and clears her snapshot |
| Restoration-wait snapshots | 63; every snapshot has 1–5 queued jobs and a later same-pawn completion |
| Saved-item diagnostic snapshots | 149; no destroyed item or `protectedReach=False` |
| Work / Non-Work buffer milestones | 42 / 12 messages; not total completed native jobs |
| Construction delivery endings | 12 Succeeded, 2 Incompletable; both latter receivers contain steel |
| Rapid-job / failed-reservation / unresolved-reference / `not deep-saved` warnings | None found |
| Empty restoration-plan messages / blocked-item recovery queries | None found |

The pasted excerpt alone has 24 restorations, 167 successful apparel endings and 19 successful weapon endings. Those preliminary totals are included in the full-session totals above.

## Standing, contention and recovery

The waits differ from the earlier persistent blocked-vest case. Queues progress and clear, rather than repeatedly waiting with no executable missing-item step. All sixteen locker-return rebuilds resolve, including Ocag at **2684→2700**, Reba at **2736→2836**, and Bowman's final return at **3095→3134**. A matching later completion was checked for every wait and rebuild, not inferred solely from aggregate counts.

Hanh provides a useful real contention test:

1. At **2929**, exact saved jacket `Apparel_FlakJacket1658877` is reachable but reserved by Bowman (`canReserve=False`, `protectedReach=True`). It is omitted from Hanh's initial five-job restore queue.
2. Bowman yields the haul so Hanh can restore (**2933**), then finds another protected haul during automatic return and continues work. This does not leave the competing hauler stranded.
3. Hanh completes the queued items. At tick **7716060**, the remaining jacket is immediately admitted as Wear `#2358775` (**2985**); the following diagnostic calls this a restoration rebuild (**2986**).
4. The jacket succeeds at tick **7716268** (**2991**), with snapshot clearing at **2990**, state `none` and an empty queue. The final step took 208 ticks; there is no repeated empty-plan retry. The callback after clearing is the successful ending of this same job.

Earlier, Reba temporarily carries Ocag's exact veil (**1339**). Ocag subsequently completes restoration (**1424**). These are useful accessible-item contention outcomes, but neither exercises recovery from an unrelated forbidden retrieval route with no accepting destination.

There are 36 native work-selection deferrals (13 construction deliveries, 13 meals, five hauls and five bills) and 56 rejected automatic Simple Sidearms managed-weapon pickup proposals. They coexist with successful work, meals, restoration and rest. No rapid same-tick admission loop, persistent queue growth or demonstrated starvation results. Counts alone are not a CPU/TPS measurement.

## Ocag and the previously blocked vest

Ocag completes eight restorations at **1424, 1676, 1745, 2066, 2435, 2510, 2700 and 2828**. She retains exact sash `Apparel_Sash1459688` through her Kitchen snapshots. It changes from displayed **51%** to **49%** by the USS snapshot at **2997**. There is no tattered replacement/adoption event for Ocag, and no blocked saved-item route in this run.

This time **Lumi** adopts and restores the former problem vest, **`Apparel_FlakVest1659117`**. Its successful restores are identifiable at **1801, 2014, 2459, 2646 and 3015**, on accessible routes. Bowman adopts the other vest, `Apparel_FlakVest1659110`. This differs from both the original failed run and the earlier healthy retest; exact item identity is necessary when comparing them.

Consequently, the old case remains open: Ocag did not own that vest, lose access to it in the unrelated protected area, and require an eligible hauler to deliver it to owner-accessible accepting storage. Her 49% sash at the end does not establish a replacement test; the later return from that final USS activity is outside the recording.

## Meals, work, buffers and video

- Ocag's Kitchen→Dining changes reach actual native `Ingest` ownership after restoration at **1747** and **2512**, with state cleared and empty queues. At **3027**, her later USS preparation also hands over to actual current `Ingest`. These are stronger outcomes than a logged resume request.
- Lumi exercises the one-displaced-haul meal path twice. The first haul earns no buffer credit (**1779**), but the retried meal is claimed by Jonah (**1781**); Lumi returns and restores (**1803**). Do not count that specific attempt as a completed meal. The second earns no haul credit (**1994**), confirms actual current `Ingest` (**1996**), then completes Dining restoration and resumes native `Ingest` (**2026–2028**). No repeated meal retry loop is demonstrated.
- Four later source-locker meal handoffs in the pasted excerpt reach native `Ingest` with cleared state: Hanh, Ocag, Jonah and Jono (pasted lines **5031, 7544, 17064 and 18925**).
- Native construction endings for Jono `#2340425` (**1856**) and Hanh `#2352797` (**2706**) are **Incompletable**, with exhausted/destroyed carried-stack references and steel present in the receiver. This is consistent with transfer/merge progress; it is not proof of lost material or a successful ending. Hanh's recipient wall reaches five steel, then a separate successful `FinishFrame` earns **1/3** credit (**2707**). These two deliveries are not counted as Succeeded.
- Bowman's `BuildRoof` buffer candidate explicitly ends Incompletable and is **not counted** (**2865**). Production completion handling requires Succeeded, and emits only the first/final milestone, explaining jumps such as 1/3→3/3. Expanded UI shows intermediate counts (for example Reba 2/3 at 5:20). Compact-hover parity was not independently checked.
- Around **1:00**, Ocag's native job UI shows reading; she later cooks/butchers, restores, eats and moves normally. At **4:30**, all seven are in bed with **Active: Resting**. Subsequent samples show waking, hauling, construction, television, meals and changing. At **6:50**, several are finishing restoration/preparation; by **6:58**, all seven again show **Active: Resting** in their beds. No pawn is left in a persistent Standing state at the endpoint.
- Visible Kitchen and USS work preparation and task-buffer returns behave coherently. This is not a test of guest arrival, gravship relocation, vacuum traversal, downed suspension, mental-state recovery or unsafe gear removal.

## Separate compatibility and startup findings

The full log has additional material omitted from the pasted excerpt:

| Finding | Evidence and interpretation |
| --- | --- |
| VFE Security terrain component | **78**: `Buildings_Trenches.xml` cannot resolve `VFESecurity.CompProperties_TerrainSetter`. Definition/load compatibility issue; AOM cause not established. |
| Missing saved map component | **1193–1198**: missing `Fortified.MapComponent_ModificationIndex`, followed by failure to instantiate abstract `Verse.MapComponent`. Existing saved-mod compatibility problem. |
| Modded armor post-load failures | **12** `PostLoadInit` null exceptions at **1200–1258**. Full stacks enter `LayeredApparel.CompLayeredApparel.UpdateLinkedStates` from GW4K/Core40k/Genes40k apparel color/exposure callbacks. These do not establish an AOM cause. |
| Research Reinvented unlock warnings | **43** `ComplexClothing`/`VFEFactory_Autoloom_*` unsupported-def warnings at **2149–2191**; the pasted log contains the final 24, with Research Reinvented opportunity-generation stacks during quest reward/book generation. A compatibility warning series, not an AOM restoration error. |
| Translation report | **768** reports five English translation errors, without the detailed report. Their ownership cannot be determined from this line. |
| Other startup diagnostics | 664 texture-dimension/compression warnings; two messages for the same Z binding conflict (Numbers/map search); nine Mono fallback-library messages; FunnelBit reports managed fallback because external Burst is absent; Camera+ reports a missing fallback shader. No demonstrated link to Standing. |

No new gameplay exception headline, missing AOM reference, failed reservation warning or rapid-job warning appears after load in the audited session. The generic summary's concern matcher does not recognize all these load-error formats, so this conclusion uses raw-log inspection as well as automated counts. Shutdown allocator statistics are retained as evidence but do not support an AOM memory/performance diagnosis.

## Validation disposition

Record **an extended normal-flow and accessible-item contention pass on released 0.4.6**, with no reproduced Standing fault. Preserve the earlier blocked-vest issue as deferred/unresolved. No corrective build is justified solely by the waits in this run.

The shortest remaining Standing test is still the exact blocked owner/item/route combination: preserve that state, offer one free accepting owner-reachable storage destination, and confirm recovery haul → exact owner's Wear → snapshot clearing. A full-storage variant remains separate. Save/reload of newly changed state, subscriber-only gameplay and the previously deferred guest/compatibility cases remain unverified. This audit does not reopen or redo publication closeout.
