# Saved restoration order and replacement status — 2026-09-05

The Iw9QZvmu8Z recording showed Gonzalez retrieving a distant Plainleather robe replacement before the saved Cadian helmet near the locker. The planner inserted the upgrade at the old robe's snapshot-list position; the hover independently displayed the first missing original instead of the current retrieval target.

The planner now retains the same exact jobs and orders them as follows:

1. Managed apparel removals and weapon drops, preserving their cleanup order.
2. Available exact saved apparel and weapon retrievals, with selected-locker items first, then nearer targets within each group.
3. Optional tattered replacements, also grouped by locker and distance.

Distance is squared cell distance from the planning position, not a new pathfinding scan or a claim of globally optimal routing. All exact originals precede optional upgrades, including a replacement that happens to be nearby. Unavailable originals remain eligible for existing recovery and do not block the jobs that can progress. The planner does not wear a tattered original merely to replace it moments later. Already-worn originals are considered restored and no longer generate an optional queued upgrade that could outlive snapshot completion; native personal optimization remains available afterward.

The current restoration hover now uses the actual Wear/Equip/removal/drop target. Replacement details name the new and displaced garment; the row calls an unsaved target a personal apparel change. When there is no valid active item job, the existing missing-item explanation remains available. No saved identity is changed at planning time. Successful-wear adoption, availability checks, safe-removal guards, retrieval permissions and native equipment admission remain in place.

## Verification

`run-restoration-contracts.ps1` compiles the production BuildJobs, replacement selection and BuildWeaponJobs methods verbatim, plus the production ordering and status helpers, against a deterministic test world. Only unrelated inventory/holder plumbing and native API behavior are stubbed.

33 checks cover the reported sequence; apparel and weapon cleanup; exact object/flag preservation; unavailable, reserved, destroyed, managed, inferior and tattered candidates; blocked retrieval routes; fallback after an interrupted plan; already-worn originals; saved unarmed state; stable ordering; and accurate active-item/replacement details. All 1,018 existing checks also pass (1,051 total). Production build and diff whitespace verification pass.

Native toils, live route changes, full compatibility stack behavior and the successful-Wear callback are not executed by this harness. Next game test: reproduce Gonzalez's return with local saved helmet/weapon and a distant replacement robe; confirm local restoration occurs first, accurate hover details, a single successful replacement adoption and normal state clearing. Repeat with a replacement claimed or removed during the trip to exercise existing recovery.
