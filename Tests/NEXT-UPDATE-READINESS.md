# 0.4.1 validation record — post-0.4.0

Updated 2026-09-08. Workshop publication and downloaded-package verification are complete; the existing screenshots are retained. Unexercised gameplay cases below remain unverified follow-up coverage, not passed tests. Current closeout status is in the [release checklist](../RELEASE-CHECKLIST.md). The previous current-status document is preserved as [0.4.0 readiness](READINESS-0.4.0.md); its broader chronology remains in [historical readiness](NEXT-UPDATE-HISTORY-2026-09-06.md).

## Build and evidence boundary

The recent small/large-map tests used gameplay DLL SHA-256 **09BB39D2D970EB399D46D247050A74F8CFE2AE455A19630C59FD8EEC7ED94905**. The 0.4.1 preparation changes version metadata and player text, with no gameplay-decision edits. The rebuilt candidate and static results are recorded in [copy preflight](RELEASE-COPY-AUDIT-0.4.1.md). A built candidate is not a deployed or gameplay-tested candidate.

## Recent findings

| Test/evidence | Result and limit |
| --- | --- |
| Small-map `8BPi8HIXiC`, local `native-control-small-retest/audit.md` | Reba's mental eating continued with AOM suspended and the pawn omitted from area rows. After a debug-ended break, normal outfit preparation and the exact rest job resumed. This does not test natural recovery timing or save/load during suspension. |
| Large-map `lANsRuWWWA`, local `large-map-robe-storage/audit.md` | The Camelhide robe was good quality at 48% condition; locker storage was set to 60–100%. Robe being checked did not bypass that limit. The maintainer corrected the storage setting and confirmed the warning disappeared. Resolved configuration issue; rule standards were not responsible. |
| Same large-map test | Oto completed restoration without an observed AOM error. A saved weapon was hauled for Schmurda; the excerpt did not prove Schmurda later equipped it. |
| Small-map `RYeN58Usmj`, local `RYeN58Usmj/audit.md` | Normal preparation, restoration, buffers and protected material hauling progressed. Two cooler frames received steel and components. The final view showed all seven pawns in bed; logged rest admissions retained the required outfit. No new fix was indicated by this test. |
| Same small-map test: Bowman | The same saved flak vest restored after it was at an accessible location. This confirms normal restoration, not recovery from the earlier protected cell. The earlier recovery query had no accepting storage group. |
| Same small-map test: battery delivery | Steel reached the battery frame even though the native haul ended Incompletable after a stack merge. Components arrived later. No demonstrated material loss or loop; an Incompletable job must still receive no successful-task buffer credit. |
| Maintainer's preceding PPE haul confirmation | Another required outfit item was hauled out of the Work Area with PPE. Useful confirmation of the permitted hauling path, not proof of every receiver, overlap or paused-state variant. |

The local audit paths above are under `C:/GitHub/AutomaticOutfitManager/work/testing/2026-09-07/`. User attachments and those audit files are local evidence, not runtime package inputs.

## What remains

The maintainer proceeded with release. Retain the four focused checks in the release checklist as unverified follow-up coverage: exact protected saved-item recovery, mental-state save/load, inactive shared-snapshot cleanup, and the rebuilt candidate's UI/load smoke. Preserve older successful normal-flow evidence. Do not label a different map, moved item, debug-ended break or successful build as proof of an unexercised boundary case.

Small and large maps do not need another unrestricted audit merely because copy changed. Any repeated outfit plan without progress, missing exact ownership, failed safe return, or renewed same-tick loop should instead become a focused reproduction with Detailed logging.
