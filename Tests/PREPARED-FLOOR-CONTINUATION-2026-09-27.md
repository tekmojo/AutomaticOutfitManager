# Prepared floor continuation — 2026-09-27

Implemented and deployed under the user's explicit request following the focused floor audit. No game launch, commits, source synchronization, settings changes, or save writes.

## Evidence and cause

Attachment `8f7c2fbb-d89a-461e-a229-a21abbe511eb/Pasted text.txt`: Aoroto's prepared RemoveFloor is rejected at lines 544 and 3530; Sosossnove's at 2551. The earlier blueprint-selection error is absent. Native/source investigation proves a separate contract defect: blueprint scanners generate a cell-targeted RemoveFloor prerequisite but remain its workGiverDef. AOM used that scanner's HasJobOnCell, whose inherited JobOnCell always returns null. The log does not include original workGiverDef/blueprint IDs for all three rejections, so it cannot independently exclude genuine target changes for each event.

## Fix

- `PreparedFloorWork` captures a unique terrain blueprint's numeric identity at the pending floor job's cell, only for RemoveFloor produced by the blueprint construction scanner.
- PawnApparelState persists `pendingFloorBlueprintId` as a value, default -1. No new deep Job or Thing ownership and no reference to a potentially cancelled blueprint are added.
- CapturePendingWork retains that identity when recapturing the same job. ClearPendingWork and transfer to the native tracker clear it.
- Designation-sensitive refresh uses the exact original blueprint's native HasJobOnThing/JobOnThing. The candidate must still be the same floor job and targets, with the same designation semantics. Delivery/blocker work cannot authorize replay of the old removal job.
- Ordinary designated floor removal, Deconstruct, and Uninstall retain their existing refresh routes. Explicit orders and native reservations still govern.
- Successful refresh leaves the original pending Job intact for the existing brief-wait/queued-original handoff, preserving native job context and single ownership.

Legacy saves with an already-pending blueprint floor job have no source identity. Those continuations are conservatively rejected for normal selection rather than adopting a possibly replaced blueprint. Newly captured jobs persist their identity. Running-game save/reload validation remains pending.

## Verification

Build succeeded; git diff --check passed (existing line-ending advisories only).

- 42 native floor selection/continuation checks. The installed blueprint HasJobOnThing/JobOnThing, floor prerequisite generation, production capture/clear/transfer and refresh methods, real claim registry, and existing candidate filtering execute. Engine/world services are shimmed in a disposable process.
- Negative control for the old cell-query decision fails at the intended assertion: a valid blueprint floor continuation must survive outfit preparation. The prior selection-error negative control also still reproduces its intended failure.
- Cases include original owner and competing claims, ordinary floor jobs, forced orders, reservations, disabled work, finished floor now yielding delivery, changed blocker work, missing/moved/replaced source, ambiguous source capture, same-job recapture, cleanup, save-field metadata/default, and no Job replacement during validation.
- Save coverage verifies production schema/default and lifecycle, not a full running-game save/reload round trip.
- Preparation handoff: 134 checks; boundary admission: 47; managed gear tracking: 33; saved gear recovery: 312; previous installation repair: 24.
- Total: 592 passing checks, plus both expected negative-control failures.

## Deployment

RC branch `codex/workshop-release-prep`, HEAD `9ff91a34fa047f582e5c9a9f12f0c51c4dfab2ae` plus preserved intentional work. Only `1.6/Assemblies/AutomaticOutfitManager.dll` copied to live, with RimWorld closed. Installed junction targets `C:\GitHub\AutomaticOutfitManager`.

Previous SHA256: `8B175655323C5F608E1A2BE652F409FCFCCD9FCBA8272DDCB34FC67824E9595E`.

Verified RC/live/installed SHA256: `35E9D0097762005F37709C6EE41827CD1C09F58E56613880E91138B19E50D8D4`.

Built: yes. Deployed: yes. Tested in a running game: pending. Repeat floor replacement inside Anomaly while a builder needs to change clothes. Verify native RemoveFloor admission after preparation, actual floor progress, and eventual outfit return; include competing builders and cancellation/save-load as separate cases.
