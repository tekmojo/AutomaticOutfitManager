# Non-Work meal handoff candidate — 2026-09-05

Implemented for native nutritional `Ingest` jobs that have picked up food during an active Work Area session and then choose a Non-Work dining cell. Drug use, custom ingestion job definitions, manual orders, and general hauling/ingredient logistics are outside this handoff.

## Behavior

- Capture the actual dining destination after pickup, including the exact carried meal and count. Defer job mutation until the next component pulse.
- Preflight the source locker, personal/fallback items, and the outgoing route. The source session's locker is authoritative. All selected work gear returns through that one source locker; the Non-Work locker is not a second mandatory waypoint.
- Move the exact meal into inventory without merging it, then use the existing restoration to apply personal snapshot/removal subset/shared-item semantics. Never deep-save a native Job in the meal record.
- With fallback selected, prepare the required items after returning source gear. Prefer source-locker stock, then reachable map stock; normal weapon preferences and gear standards remain. Preserve the saved personal snapshot and normal temporary fallback ownership.
- Exclude other managed areas during changing. After changing, admit the intended Non-Work area and forbid every new Work Area entry. Check actual path cells as well as the planned path. Personal items stored behind a Work boundary are not a reason to start a second work outfit.
- Resume native eating from the same inventory meal and restore the recorded dining spot. Follow a native stack split and prevent autonomous hauling from displacing that active meal.
- Unavailable outfits, genuine destination overlaps, unsafe post-removal routes, blocked source lockers, changed dining routes/reservations, or a 5,000-tick changing timeout end the detour. The native meal eats at the current location with the gear currently worn. This avoids entering the restricted destination or repeating outfit swaps. It can produce RimWorld's normal ate-without-table result.
- Draft/downed/mental state, emergency, custody escape, explicit orders, map changes, or lost food retire the meal continuation. Existing safe outfit restoration remains responsible for any gear already being returned.
- Save the meal reference, count, map, source/destination IDs, destination/locker cells, stage, and outfit references. No game job has a second saved owner.

## Automated verification

- Build against installed RimWorld 1.6 and Harmony: passed.
- `run-non-work-contracts.ps1 -VerifyFailureReporting`: 71 existing production outfit/rule checks and the caught-failure check passed.
- `run-meal-handoff-contracts.ps1`: 27 production-controller checks passed against a deterministic test world. Covers deferred capture, exact stow/count, source return, final restriction, automatic vs forced haul, changed path, pickup fallback, hazard preflight, draft, lost/claimed gear/food, disabled destination, timeout, map change, selected/empty fallback, save-record identity and absence of duplicated Job fields.
- Whitespace check: passed.

These checks do not execute RimWorld's pathfinder, live reservations, Harmony patch ordering, animations, or actual save loading. Manual gameplay results remain pending.

## Focused game test — pending

1. Repeat Reba's Simple meal trip: pickup in Kitchen → Kitchen source locker → selected work gear removed → personal outfit restored → original meal eaten in the Non-Work dining area. Confirm food increases and the repeated Standing/path-error/idle-return-cancellation sequence stops.
2. Repeat with no fallback and the checkbox off; then selected fallback in the source locker and at another neutral stock location. Confirm no mandatory second locker visit.
3. Repeat with partial/shared removal, saved unarmed state, and an explicit primary choice. An unsatisfied destination must recover without overriding the player or alternating outfits.
4. Put saved gear behind a Work boundary; block the only exit, overlap the dining cell with another rule, repaint an area during travel, and occupy the chair. Confirm recovery never starts another Work outfit for the meal.
5. Interrupt with drafting, forced hauling, emergency, food loss, and map travel. Confirm normal control and no duplicated/lost inventory item.
6. Save/reload during captured pickup, source return, personal restoration, fallback preparation, dining travel, and chewing. Confirm the same meal and stage resume once.
7. Check vac-suit and other environmental protection: safe locker removal, hazardous route rejected before removal, and native emergencies remain authoritative.

Candidate SHA-256: `C11E1A0697B92FE8BA1CBE731B8B95EA413B6F1493285C94338217DC3535514E`.
Deployment: completed with user authorization on 2026-09-05. RimWorld was closed; the installed junction targeted the live repository; RC, live and installed DLL SHA-256 hashes all matched the candidate above after copying the DLL only. RimWorld has not been launched by this task.
