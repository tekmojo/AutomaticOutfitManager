# Turoksen follow-up session audit — 2026-09-25

**Verdict: clean for the observed AOM child-delivery and access-toggle scope; separate compatibility/save-state concerns remain.** No new AOM fix is justified by this recording. This is a read-only audit, not a deployment or release-readiness sign-off.

## Evidence and runtime

- Video: `C:\Users\tekmojo\Documents\ShareX_HDR\ShareX\Screenshots\2026-09\LSjayPrfzv.mp4` (5:26.6, 3996 × 1812, 30 fps).
- Log: `C:\Users\tekmojo\.codex\attachments\764238c2-6874-4176-9f82-e9d73de8e151\Pasted text.txt` (5,354 lines; 191 AOM messages).
- Log SHA-256: `E9F4DA44AABB0590F672A7F39F3428B45C65A492702B7BC197A18B32A91FD718`.
- Verified current RC/live/installed DLL SHA-256: `85AD264B6CF071229DEFE128B6342D460776C37E7D5A6ED6CAFC9C85BB861D04`. Version remains 0.4.3. Running game was left untouched.
- The excerpt starts mid-session, without startup/load identity. Disk identity and the existing deployment/session handoff support candidate attribution; the excerpt does not independently fingerprint its loaded DLL or prove a fresh load.
- Anomaly is visibly an access-only Work Area with no required outfit. The recording follows Turoksen and includes deliberate Allow Children changes. Adult activity, hauling and wandering permissions remain enabled in the inspected toggle frames.

## Child delivery and checkbox chronology

| Evidence | Observation | Assessment |
| --- | --- | --- |
| About 0:30 versus 0:55 | Anomaly Allow Children initially off, subsequently on | Account for intentional configuration changes |
| Log 532 | Turoksen stopped outside Anomaly while attempting HaulToCell | Consistent with the initial disabled state |
| Log 2696 / 2714 / 3872 | Loto job #75909181 proposed, accepted, then **Succeeded** | Concrete delivery completed; final wall frame contains 10 concrete. Two excess concrete remain carried, which is not itself a failure |
| Log 3071 / 3089 / 4034 | Turoksen job #75913648 proposed, accepted, then **Succeeded** | Steel delivery completed; final hidden-conduit frame at (201, 90) contains 2 steel, no carried stack remains |
| Video 3:05 and 3:30 | Turoksen visibly hauling 26 steel toward a hidden conduit | Matches the accepted construction job and shows continued movement |
| Video 3:44–3:45 | Allow Children on; children inside Anomaly | Allowed occupancy |
| Video 3:47–3:48 | Allow Children off; Turoksen exits east and Loto exits south | Expected egress after the deliberate toggle |
| Video 3:50 onward | Allow Children back on; ordinary movement resumes | No persistent exit/re-entry or Standing loop established |
| Log 4120 / 4135 | Turoksen and Loto reconsider HaulToCell from inside Anomaly | Both events occur after their construction deliveries succeeded. These are ordinary haul stops consistent with the filmed disable-and-exit sequence, not failed construction deliveries |

Video/log alignment uses matching pawn actions and event ordering, not embedded wall-clock timestamps in each log event. The checkbox is not visible throughout every second of the trip, so this audit does not claim continuous instrumented setting history.

The earlier suggestion that the remaining child cancellations established an AOM fault was too definite without the toggle timing. This recording does not reproduce a child construction cancellation. It validates this delivery path and the later deliberate access change; it does not retroactively establish the cause of every earlier interruption, or replay the exact earlier cooler job and pickup route.

## Construction results across the excerpt

- **23 Succeeded**, **12 Incompletable**, **0 InterruptForced**, **0 ErroredPather** construction endings.
- Nine Incompletable endings have a consumed/destroyed source and a live recipient frame containing materials (lines 732, 1218, 1300, 1698, 2200, 2756, 2998, 4292, 4636). This is consistent with material consumption/merge bookkeeping; it is not evidence of lost materials or denied access. These remain Incompletable in the counts and are not credited as successful buffer tasks.
- Three have a destroyed blueprint target (584, 1507, 1634), consistent with a stale construction target while other builders progress. The excerpt does not prove every native target replacement, so no stronger causal claim is made.
- Neither child has a failed construction ending in this excerpt.
- No rapid-job warning, reservation-failure family, or unresolved-load-reference message was found in this excerpt. This does not clear startup warnings absent from the attachment.

## Outfit/buffer and ownership checks

Gonzo completes a full return/restoration sequence:

1. Buffer credit advances from 1/2 (2806) to 2/2 (3421), then return begins (3446).
2. Temporary bolter returns (3637); suit removal starts (3656). One idle locker-return recovery message follows at 3675, with subsequent progress rather than a repeating empty-plan loop.
3. Suit and mask removal succeed (3765, 3982); saved helmet wear succeeds (4224).
4. Exact saved autogun `GW_HOI_Gun_Agripinaamkii2390927` equips successfully (4246).
5. Saved jacket restores, snapshot clears (4429), and its final successful callback reports state None (4453).
6. No later Gonzo restoration restart appears in the remaining excerpt.

There are four successful apparel endings, one successful weapon ending, and no failed outfit-step endings. Intermediate saved-item diagnostics report reachable/reservable items and are followed by actual restoration; they do not establish inaccessible-gear churn. The one locker-return recovery is retained in the result rather than hidden by the overall pass.

Min and Sullivan's automatic bills are prevented from consuming Gonzo's saved weapon (817, 1788); Gonzo later restores it. Zendaya's safe-placement completion (3194) does not grant extra buffer credit. Foto begins a new two-garment/weapon preparation near the tail (5295–5314); the excerpt ends before completion, so that preparation is ungraded.

## Separate concerns

**Repeated faction-relation errors:** 54 instances from line 834 through 5332 report that GooseButter has a null relation with The Adeptus Astartes. Sample stacks enter `Faction.RelationWith` through hostility/opinion/shared-bed mood evaluation. This is ongoing error spam, not a clean overall game log. The attachment does not identify which mod or save operation created the missing relation; it provides no AOM causal frame. Investigate faction/save initialization separately before attributing it to a particular mod. Counts alone do not quantify its performance cost.

**Dubs Bad Hygiene toilet exception:** Rodion's `UseToilet #75901387`, toilet `ToiletAdvStuff3203121`, fails at toil 3 (1889–1910). The NullReferenceException originates in `DubsBadHygiene.JobDriver_UseToilet.<MakeNewToils>b__1_1`, source line 73. This repeats the prior failure family on a different job/toilet. The throwing frame is DBH; the log does not prove the underlying cause or rule out an interaction. It is a separate investigation, not evidence that the successful AOM child delivery failed.

Three additional bed-assignment messages name Medic, Pyrrha and Caine as already having beds. These alone do not demonstrate a rest or outfit loop.

## Scope and next useful check

No code, build, deployment, version, or save change was made. Raw audit artifacts are stored in `C:/GitHub/AutomaticOutfitManager/.codex-audit/turoksen-follow-2026-09-25/`.

This pass covers allowed child construction delivery followed by disabling access while children are inside. It does not prove the exact older steel-to-cooler route, every child-denied exterior delivery path, a blocked saved-item recovery, save/reload continuity, or all DLC interactions. If further AOM confirmation is needed, use one exterior-accessible perimeter delivery with Allow Children left off from pickup through completion. Keep it separate from a subsequent toggle test. The present evidence does not call for another speculative child-access fix.

The explicit current-tick anchors span 51501561–51508497, but only 61 AOM messages fall within the anchored window. Whole-excerpt counts above are not normalized against that smaller window, and no TPS/CPU claim is made.
