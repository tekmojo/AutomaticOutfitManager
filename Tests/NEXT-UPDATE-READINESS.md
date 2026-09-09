# 0.4.2 validation record

Updated 2026-09-08. Workshop publication and downloaded-package verification are complete; see [0.4.2 closeout](WORKSHOP-CLOSEOUT-0.4.2.md). The [release checklist](../RELEASE-CHECKLIST.md) retains the unverified gameplay follow-ups and pending GitHub closeout. Publication does not convert those cases into passes. [0.4.1 readiness](READINESS-0.4.1.md) and [closeout](../RELEASE-HISTORY-0.4.1.md) remain history.

## Build and evidence boundary

Latest gameplay-tested DLL: **F634D86039F215FC98FD883C667701DFB66F8E990A2221AD60292CFD5ACF2FD5**. The 0.4.2 preparation changes version metadata, About and one option tooltip, not gameplay decisions. The new build/hash and validation are recorded in [copy preflight](RELEASE-COPY-AUDIT-0.4.2.md). Built, deployed and tested are separate states.

## Recent test outcomes

| Evidence | Result and limit |
| --- | --- |
| Latest boundary correction; eN89V3kPdn investigation | A rejected route could continue moving while current job was null. The path now stops before job cleanup. Native movement ordering and unchecked Non-Work exits have positive and previous-decision negative controls in [the focused record](NON-WORK-UNAVAILABLE-GEAR.md). |
| Large map, 9IY04v5LHt | Juroksen stopped outside, equipped selected armor and resumed his retained DoBill. No unmatched entry/exit detour. Zendaya's outside boundary also held, but her original bill continuation cancelled; later hauling/eating and restoration progressed. Roboto did not make a clearly recorded boundary attempt. |
| Same large map | Nine restoration completions; 37 successful apparel endings and four saved-weapon equips, no failures. Loto's previous repeated exit stall did not recur. Only about 5.2 game hours are explicitly anchored. |
| Small map, CHVEpIAO5m | Loaded AnuStart_01_ALL_MODS, 250×250 with 188 mod/DLC entries. Seven snapshots and one pending work claim recovered. All 66 inspected AOM references resolved, no duplicate inspected job IDs or saved personal owners. About 15.94 game hours; no new save created. |
| Same small map: meals | Bowman, Jono, Lumi and Hanh each reached actual Ingest after Kitchen-to-Dining restoration. Lumi's one displaced haul completed without buffer credit before the fresh Ingest retry. Ocagobrei used packaged food elsewhere; his earlier simple-meal loop stays open. |
| Same small map: protected sash | Sash #1459688 had native reach but blocked protected reach. Jonah hauled the exact sash out; Ocagobrei wore it and cleared his snapshot. The roughly one-game-hour wait ended with actual recovery, not repeated empty-plan rebuilding. |
| Same small map: progress | 27 restorations, 179 successful apparel endings, 18 successful weapon equips (16 work, two saved), nine successful construction deliveries. No new confirmed AOM fault. |
| Earlier small-map QOiUhixqU2 on released B8C1A1D… | Jono's protected flak vest recovered through Bowman and restored exactly. Separately, Ocagobrei repeated 17 Kitchen/Dining meal cycles with no confirmed Ingest admission. That finding remains unresolved; no blanket meal-fix claim. |

Full local audits are under `C:/GitHub/AutomaticOutfitManager/.codex-audit/`: `entry-followup-2026-09-08`, `full-session-2026-09-08-2118`, `small-map-2026-09-08-2123`, and `small-map-2026-09-08`. They are local evidence, not package inputs. Each report records its source, hash and timing limits.

## Evidence still needed

Use the ordered cases in the release checklist: Ocagobrei's actual simple-meal route, Roboto/Zendaya ingredient-to-bill completion, alternate work after an unwearable child denial, and a newly created transition save/reload. Preserve the 0.4.1 follow-ups for mental-state save/load and inactive shared-snapshot cleanup.

Do not repeat broad endurance play solely because a tooltip/version changed. A UI/load smoke covers that copy-only difference; unresolved gameplay cases need the matching pawn, job and conditions. A fresh session restarted from an old save can have an earlier game tick than a physically older autosave, so match file timestamps, hashes and load boundaries before assigning save evidence.
