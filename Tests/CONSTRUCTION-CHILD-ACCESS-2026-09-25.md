# Restricted-child construction delivery follow-up

Version 0.4.3. Historical first child-delivery candidate, locally deployed and retested. Entry/exit and removal of path errors passed; post-pickup cancellations remained. Superseded by the [admission/runtime follow-up](CHILD-DELIVERY-ADMISSION-2026-09-25.md); unpublished. No save keys, rule defaults, interface text, or version identifiers change.

## Evidence and cause

The follow-up `UcGhfx3Tc1.mp4` (561.2 seconds) and attached 8,804-line log show the previous no-outfit routing fix working. Remaining construction path failures affect Turoksen and Loto, with Allow Children off and every captured failed destination reporting activity denied / missingOutfit false. Four jobs end ErroredPather; one further Loto attempt is interrupted before its empty-path warning arrives. Full audit: `C:/GitHub/AutomaticOutfitManager/.codex-audit/routing-followup-2026-09-25/AUDIT.md`.

The native scanner can accept a boundary frame/blueprint while its eventual GotoBuild standing cell lies inside the denied area. Installed game IL confirms GotoBuild uses RCellFinder.TryFindGoodAdjacentSpotToTouch, which has a preferred-cell pass, an adjacent fallback pass, and a target-footprint fallback. Checking only the building or rejecting only the first chosen cell misses either the fault or a legal alternative.

## Change

- Filter both native adjacent-cell enumerations for ordinary managed children at a denied construction boundary. Retain native cell ranking, good-destination checks, corner-touch checks and fallback behavior.
- Check cell access and an actual route avoiding all child-denied areas. Allow initial egress but do not grant forbidden re-entry. Reuse the existing protected-path policy; do not invent a parallel pathfinder.
- Validate the complete generated delivery's current and queued recipients at ResourceDeliverJobFor, shared by HasJobOnThing and JobOnThing. No material pickup, speculative duplicate native job generation, saved Job, or persistent rejection cache is added.
- Scope prospective selection synchronously and restore its context in finally. Actual delivery movement uses the current job, so the same cell filter can select a permitted approach from a different side. Current FinishFrame destination selection also benefits.
- Preserve native/player control, allowed children, adults, robots, animals, unrelated touch searches, map identity, and disabled rules. Limit extra path probes to child construction sites touching a denied area.

## Verification

| Check | Result |
| --- | --- |
| New production decision and Harmony callback fixture | 68 passed |
| Installed native API/IL and execution probe | 5 passed |
| Existing routing/construction/presentation contracts | 73 passed |
| Existing paused hauling/restoration contracts | 600 passed |
| Existing child-access contracts | 54 passed |
| Existing prepared-work candidate contracts | 48 passed |
| Total | 848 passed |

Three negative controls pass: the unfiltered native scanner reproduces acceptance of an impossible child delivery; the previous Work-area routing decision reproduces the ordinary no-outfit failure; removing the previous construction-destination exemption reproduces the adjacent PPE-routing failure. Expected assertion failures are caught and return a failing exit code without crash dialogs.

New cases include all approaches denied; farther legal approach; fallback enumeration; blocked/unreachable approaches; legal endpoint behind a denied route; overlapping rules; queued recipients; permission changes; native/forced control; unrelated jobs; frame finishing; native footprint fallback; map validity; and context cleanup after a path exception.

The native probe binds both production patches against the installed game assembly and verifies insertion in both real picker passes. It executes the real native picker with an empty map-dependent enumeration and confirms both passes and footprint fallback. Rich map/path/access cases use deterministic native-type fixtures; this is not a substitute for in-game pathfinding and hauling observation.

Build succeeds using build.ps1 and installed RimWorld assemblies. git diff --check passes.

New RC DLL SHA-256: `5027B8AECF741427081B47BA8F33001D895B76B774566E6C1A36608407F96163`.

Previous DLL backed up locally: `F30038F990615E73C70E58D64A902AD7394096C6B2B4841CAD8FD9D7DE8F0566`.

Only the runtime DLL was deployed. RC, live and installed hashes match the new candidate above; deployment.json records verification. The live source checkout and Workshop publication were not changed. RimWorld was not launched. New source and tests remain uncommitted alongside the earlier routing changes. Local evidence and pre-change DLL: `C:/GitHub/AutomaticOutfitManager/.codex-audit/child-delivery-fix-2026-09-25/`.

## Next in-game check

Launch RimWorld manually through Steam and load the same save. With Allow Children off, observe a boundary doorway: children should skip deliveries with no legal approach, while adults/robots continue. Give a second boundary worksite a reachable permitted side and confirm the child uses it. Toggle Allow Children on and confirm native delivery resumes without outfit intervention. Keep a real outfit-required boundary check separate. Workshop upload remains through the in-game Mods menu when ready.
