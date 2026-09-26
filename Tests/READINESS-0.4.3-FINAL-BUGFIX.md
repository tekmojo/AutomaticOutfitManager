# Final 0.4.3 bug-fix readiness

**Published through the Mods menu; public copy and downloaded files verified.** Current release `85AD264B6CF071229DEFE128B6342D460776C37E7D5A6ED6CAFC9C85BB861D04` is built, locally deployed and gameplay-tested within the scope below. RC/live/installed hashes match. This preparation changes documentation and audit guidance only; the gameplay-tested DLL remains unchanged. Version stays **0.4.3**.

The earlier published text-only maintenance state is preserved in [archived readiness](READINESS-0.4.3-TEXT-MAINTENANCE.md) and [maintenance closeout](MAINTENANCE-CLOSEOUT-0.4.3.md). Its published DLL is `4CA32704045B015AB50A6FA20AFA236A72E717E90DDF812F785830CB1A0E9B32`; do not use that earlier hash as evidence for the current gameplay update.

## Scope

- Ordinary and buffered travel through Work Areas with no outfit requirement respects access without unnecessary protected-route avoidance.
- Adjacent construction delivery/finishing positions are recognized.
- Child boundary construction uses consistent selection/runtime approach checks, including remaining pickups and queued recipients.
- No save keys, defaults, metadata version or About/loading copy changes.

## Evidence on the current candidate

The [implementation record](CHILD-DELIVERY-ADMISSION-2026-09-25.md) records 888 passing checks, two expected-failing negative controls and native-picker execution limits. The [routing record](ACCESS-ONLY-ROUTING-2026-09-25.md) and [initial child-access record](CONSTRUCTION-CHILD-ACCESS-2026-09-25.md) preserve the earlier candidate history.

[Latest session evidence](FINAL-BUGFIX-SESSIONS-2026-09-25.md) distinguishes separate replays:

| Case | Observed result |
| --- | --- |
| LSjayPrfzv | Turoksen and Loto construction deliveries Succeeded. Allow Children off at 3:47–3:48 produces safe exits after completion. 23 construction successes; no forced construction interruption/path error. |
| RKbtGCQ9Gy | Turoksen supplies the final concrete to the wall (9 to 10), then cleans and continues other movement. Its native ending is Incompletable, not a successful buffer task. 18 other construction successes; no forced construction interruption/path error. Late toggle follows his departure. |
| Gonzo, both replays | Full buffer and exact saved gear restoration; no renewed restoration loop after snapshot clearing. |
| No-outfit Anomaly work | Adults/robots and allowed children continue ordinary construction, hauling and activity. No Anomaly-specific outfit requirement was configured. |

Earlier cancellation attribution is limited by user toggles: the initial current-candidate audit cannot establish the precise setting at every interruption. Latest evidence does not justify another speculative AOM fix. Do not count an accepted job without a matching ending as completed.

## Remaining checks and separate concerns

Held-off exterior boundary delivery, blocked/queued pickup/recipient gameplay cases, the exact earlier cooler route, and fresh transition save/load remain unverified. The latest recordings do not establish Anomaly study, psychic ritual or pit travel, complete post-ceremony restoration, or all overlap/pause edges. Earlier meal/bill, mental-state and legacy-child follow-ups stay in [published 0.4.3 readiness](READINESS-0.4.3-PUBLISHED.md) and [0.4.2 readiness](READINESS-0.4.2.md).

The two latest logs contain 54 and 51 missing faction-relation errors respectively. DBH's toilet-driver exception affects Rodion in one replay and Sullivan in the next, on different toilets. These are separate investigations without established AOM causation. Older crib/load issues are preserved in archived records and are not claimed resolved by their absence here.

## Publication and closeout

The final package was uploaded on September 25 at 18:48:28 America/Denver (September 26 at 00:48:28 UTC). Workshop item 3792731788 retains its title, creator, visibility and Harmony dependency. The streamlined description matches the public text after newline normalization, and the separate bug-fix note is verified.

Steam manifest/content handle: **4781208902276209270**. All twelve downloaded files match the stage by path, size and SHA-256 (1,061,488 bytes total). The installed junction is restored to the live repository with the tested DLL verified; source changes there remain preserved. Subscriber-only gameplay smoke and the scoped gameplay gaps above remain unclaimed.

[Final preflight](FINAL-BUGFIX-PREFLIGHT-0.4.3.md) and [upload handoff](FINAL-BUGFIX-DEPLOYMENT-0.4.3.md) preserve preparation history. [Final closeout](FINAL-BUGFIX-CLOSEOUT-0.4.3.md) records verified publication, matching GitHub asset sizes and digests, and source commit `2437ae8` published to both release branches. The original version tag and all four historical GitHub assets remain unchanged.
