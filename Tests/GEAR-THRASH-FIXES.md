# Repair staging and gear retrieval — 2026-09-05

Candidate SHA-256: `25A061DF947AF4E36BE75911A08DC090293B605C572A7F2A659E1310F5DD6ACE`.
Status: deployed with user authorization on 2026-09-05. RC/live/installed DLL hashes match; installed junction targets the live repository. Only the runtime DLL was copied. RimWorld remained closed and was not launched. Native gameplay validation remains pending.

## Evidence and changes

The [HNcINJdhz5 audit](../../testing/2026-09-05/HNcINJdhz5/audit.md) identified Jono's repeated USS/Kitchen changes for the same stove repair, plus Hanh/Jonah clothing routes that collided with another Work Area.

- A vanilla-shaped single-component `FixBrokenDownBuilding` now discovers its protected material source before the first boundary collision. The building remains the destination even when a compatibility path omits the work-giver tag. Incompatible source/destination outfits use the existing source-first, neutral-material-drop, destination-preparation handoff. Other jobs, multi-target repairs, carried materials, and disabled/paused source rules do not gain this proactive stage.
- Pending-job capture clears the boundary retry registry. The source IDs are now copied before capture, so capture cannot erase the stage just selected. Already-compliant source workers retain the lightweight stage marker too. The native tracker or pending state remains the sole owner of the exact Job.
- Apparel and weapon preparation now reject candidates whose retrieval route cannot avoid unrelated protected areas. The route customizer no longer gives a managed gear job the ordinary direct-work-target exception that the boundary guard would subsequently reject.
- Active sessions remember contributing rule IDs for exact personal restoration. After a rule handoff, a saved item can be retrieved from a remembered rule only when that rule owns its actual target. Other items and routes through that rule retain restrictions. The IDs are saved; older active saves conservatively seed the active rule. Snapshot cleanup removes item-specific access when an item stops being a personal target. Existing source egress remains available.
- Includes the requested [map-area selector colors](MAP-AREA-COLORS.md): neutral field labels, area-colored badges/selected gear, muted source colors, and standard grey apparel-layer conflicts.

No change to meal identity, removal selections, explicit weapon overrides, or gear standards is intended. New route checks may report stock unavailable when it can only be reached through an unrelated protected area. Relocating that stock to an accessible locker remains a valid remedy; historical save data cannot reconstruct a source that was never recorded.

## Validation

- Build against installed RimWorld 1.6 and Harmony: pass.
- 126 outfit/rule contracts: pass, including 21 new checks of production repair-source and gear-route policy with small game API doubles.
- 27 meal-handoff controller contracts: pass.
- Intentional test-failure reporting: handled exit and diagnostic, without an unhandled Windows crash dialog.
- Whitespace check: pass.

The policy tests do not execute the full StartJob patch, real pathfinding, reservations, Unity drawing, or in-game save/load. These results establish build and policy checks, not a gameplay pass or measured performance improvement.

## Focused manual checks — pending

1. Jono: repair the Kitchen stove with components inside USS and incompatible outfits. Expect source gear once, component retrieval, neutral material drop, destination gear once, and the same repair resuming. Repeat while initially wearing correct source gear, from neutral ground, and with no work-giver tag if a mod supplies that case.
2. Save/load during source preparation and after component pickup. The stage should survive without a duplicated Job reference. Cancel/draft, destroy or reserve the component, and remove the source rule; recovery must remain bounded.
3. Hanh/Jonah: put desired Kitchen clothing inside USS and accessible alternatives in the locker. Accessible stock should win without USS preparation. With no legal route, report unavailable stock without repeatedly equipping/cancelling for the same target.
4. Restore exact personal clothes in a remembered source area after a rule handoff. Check that a different same-definition item cannot inherit that permission and that the source cannot be used as a shortcut to an item elsewhere.
5. Replay meal return through the source locker. Preserve the meal, selected Work removals, and one Non-Work restoration; do not re-enter another Work Area on the final dining leg.
6. Run the map-area color checks and watch for long Standing periods or repeated repair/gear diagnostics. Include a longer large-map session to assess the new route preflight cost.
