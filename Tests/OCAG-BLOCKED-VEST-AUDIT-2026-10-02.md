# Ocag's blocked saved vest — 2026-10-02

## Verdict

Confirmed persistent saved-item restoration wait. Ocagobrei finishes the available outfit steps but cannot retrieve her exact saved Masterwork flak vest from an unrelated protected area. Eligible haulers attempt recovery and find no usable accepting storage destination. No failed Wear/Equip step, empty-queue rebuild storm or recurrence of the older meal-admission loop is demonstrated.

This exposes the current recovery limitation: same-locker ground fallback applies during Preparing/Active, while blocked-item recovery during Restoring requires accepting slot storage. How the vest reached the other area is not established. Keep further release closeout on hold pending a targeted recovery test and, if necessary, a transfer reproduction. The maintainer reports uploading 0.4.6; public/subscriber verification has not been performed in this investigation.

Read-only investigation: no source, game settings, original saves, deployment, junction or publication changes. The installed junction still targets the preserved 0.4.6 stage.

## Evidence

- Video `FGJWSL2YkL.mp4`: 150.567 seconds, 4058×1820 at 30 fps. Contact sheets sampled the full recording at 5-second intervals, with full-size inspection around the later return and final stationary state. The camera shows Ocag remaining in the lower Kitchen locker/storage room while other pawns continue activity. Storage filters and the exact vest transfer are not inspected on screen.
- Pasted log `873234b3-0837-4d96-935c-155967ca2970/Pasted text.txt`: 19,263 lines; SHA-256 `E94E9669B01E30882095CB5FC8C6BBC48BB27269B76361777E17A3D84D7CC493`. This begins mid-session, not at startup.
- Independently copied `Player.log` matches the final Ocag diagnostics, announces **0.4.6** and loads `AnuStart_01_ALL_MODS`. The installed DLL remains `A25B40980BBC58D419227DD1ECA4B8F855A8A8552F115E4688CA41D091FA98EE`.
- Original `AnuStart_01_ALL_MODS.rws` baseline tick **7626656**, SHA-256 `716F613C67617BDF0F583398A68AF75CAB93B0E7E80668825517E3C83DF324F7`. It predates this run; no matching new end save was available. Current autosaves belong to the other colony and are not used to infer this map's endpoint.
- Local copies, frame sheets, events, summary and baseline storage extraction: `C:/GitHub/AutomaticOutfitManager/.codex-audit/ocag-873234b3/`.

## Ocag timeline

1. Ocag completes the first observed Kitchen return at line **2712**, including her Hellcat rifle and saved clothing.
2. At **9920**, the old saved Poor sash has reached **49%** condition. AOM chooses the Masterwork flak vest as a better replacement. It adopts exact item `Apparel_FlakVest1659117` only after successful Wear (**10464**), clears the displaced sash and completes restoration (**10484**). This is a valid tattered-apparel replacement, not unexplained loss of good clothing.
3. The vest is subsequently recorded at `(154,0,112)` with `protectedReach=True` (**11034**), restored successfully (**11198**), and the outfit completes (**11344**). The meal handoff then reaches an actual native **Ingest** job (**11368–11392**). This specific meal path passes.
4. The next Kitchen snapshot includes the vest (**11587**). Preparation completes and native DoBill becomes current (**12303**). After the one-task buffer, the return decision occurs at tick **7653328** (**14163**).
5. Before that restoration queue begins, the vest is already at `(142,0,121)`, `nativeReach=True`, `protectedReach=False` (**14406**). It is present, unreserved and not forbidden. The queue contains only removal of the royal vest/robe and Wear of the original parka; it cannot include the blocked flak vest. The parka succeeds at tick **7654378** (**14990**).
6. Recovery queries by Jonah and Bowman (**15498, 15744, 16110, 16492, 16633**) fail. The first reaches two destination checks, both rejected before the owner-route test. Later searches reach no eligible accepting group. Those diagnostics do not identify the exact native rejection reason.
7. Ocag remains in Restoring at `(148,0,111)` with an empty queue. There are **28 wait snapshots after the final parka step** and **29 blocked-vest diagnostics** overall, ending unresolved at line **19251**. Later messages report the age of the last hauling query; they are not evidence of new recovery attempts every time. The native Wait IDs change while position and missing item remain unchanged.

## Areas, filters and source explanation

Decoded baseline area grids place `(148,111)` and the earlier `(154,112)` vest cell inside **Locker**, the Kitchen/Dining changing area. The final `(142,121)` vest cell belongs to **USS Pissant**, not Locker or USS Pissant Locker. `GearRetrievalRoute.RestrictedRules` excludes unrelated protected rules from saved-item retrieval; restoration does not grant blanket entry just because an exact saved item is there. The runtime `protectedReach=False` confirms this block independently of the baseline.

The baseline ship rule disables slave permissions, but `everEnslaved` and `JoinAsSlave` history do not establish Ocag's current slave status. Do not attribute this retrieval failure to slave access. The unrelated-rule retrieval guard is the established cause.

The baseline **Stockpile zone 1** covers 126 Kitchen-locker cells and rejects `AutomaticOutfitManager_AllowManaged` (automatic outfit apparel). It allows the Flak vest definition and full condition/quality ranges, but those do not override its special-filter rejection. Ten local shelves permit the vest type and managed apparel at full ranges; their baseline storage cells are heavily occupied. The recording does not establish their exact capacity at the failed query. Therefore the log proves unavailable accepting destinations, while the baseline explains an important floor-storage exclusion; it does not prove a particular shelf remained full throughout the run.

`SavedGearLockerPolicy.TryGetHome` limits locality to Preparing/Active. `GroundHasSpace` requires an unzoned floor cell with no item, so it does not force a saved garment into a rejecting stockpile. `SavedGearRecovery.TryMakeRecoveryJob` instead searches native accepting slot storage, then checks the owner's retrieval route and the hauler's permissions. It has no same-locker non-storage floor fallback. This preserves filters but leaves a blocked owner waiting when no qualifying storage cell is available.

The locality restriction is already disabled during Restoring, so the failed queries are not evidence that the new locality filter incorrectly rejected a valid restoration destination. Similarly, repeated rejected Simple Sidearms pickup proposals coexist with the wait, but the missing vest independently prevents restoration completion. Clearing sidearm memory would not resolve that missing garment.

## Next action and implementation boundary

Shortest recovery test: provide a free storage cell in the Kitchen locker that accepts **Flak vest** and **Automatic outfit apparel**, with suitable native quality/condition filters, then let an eligible colony hauler recover the exact vest. A small dedicated stockpile is preferable to broadly changing the general stockpile. Confirm delivery, Ocag's successful Wear and snapshot clearing. Alternatively an explicit haul of the exact item to an accessible local cell can establish that the item itself is healthy. No in-game changes were made here.

If improving automatic handling, extend blocked-item recovery with a narrowly scoped safe local-ground destination when accepting storage is unavailable. It must preserve exact ownership, eligible haulers, owner reachability, claims and storage filters: use actual unzoned floor, not a stockpile that rejects the item. This would address the recovery gap but does not explain or fix the unobserved original transfer. Reproduce that transfer with a saved local association before calling it a locality regression; record source/destination, item, phase and actual hauler or clothing-drop job.

## Broader small-map result

Subsequent evidence: the [restarted small-map retest](SMALL-MAP-RETEST-AUDIT-2026-10-02.md) has no persistent stall after the maintainer freed locker space. Ocag keeps the 51% sash and Bowman adopts this exact vest, restoring it successfully on accessible routes. That run does not reproduce the owner/garment/protected-route combination documented here and does not close blocked-item recovery.

The pasted gameplay interval contains **25 completed restorations, 161 successful apparel endings and 15 successful weapon endings**, with zero failed gear endings. Ocag completes three earlier restores before the final blocked one. No rapid-job warning or exception appears in this pasted interval. Aggregate successes do not close her unresolved restoration.

The matching startup log separately contains a missing VFESecurity terrain-setter type, an abstract MapComponent load failure and twelve post-load null exceptions on other modded armor items. These are additional compatibility findings, not evidence explaining this exact vanilla flak vest's protected-route/storage wait. No performance claim or full save/reload pass is made.
