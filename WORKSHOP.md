# Steam Workshop listing

## Item settings

- Title: `Automatic Outfit Manager`
- Initial visibility: `Hidden`
- Supported game version: `RimWorld 1.6`
- Required item: [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
- Suggested tags: select the available RimWorld 1.6, utility, interface, and gameplay tags that accurately match the item.
- Preview: `About/Preview.png`

## Short description

Area-based apparel, PPE, and exact primary-weapon rules with lockers, task buffers, access controls, and exact gear restoration.

## Description (Steam BBCode)

```text
[h1]Automatic Outfit Manager[/h1]

Create area-based outfit rules using apparel, personal protective equipment (PPE), and optional exact primary-weapon alternatives.

Eligible undrafted humanlike pawns automatically equip the complete requirement before entry and retain it through work, hauling, wandering, personal activities, sleep, and pass-through. After leaving and completing any configured follow-up buffer, they return items through an optional locker room and restore the exact apparel and primary weapon they had beforehand.

[h1]Features[/h1]
[list]
[*]Ordinary RimWorld areas define where a rule applies
[*]Every selected apparel item stays equipped for every activity and protected route inside the area
[*]Require every selected apparel item and optionally one of several exact primary weapons
[*]Skill-aware ranged or melee preference when both are valid
[*]Exact apparel and primary-weapon restoration, including returning to an unarmed state
[*]Optional locker rooms and configurable 0–20 task buffers
[*]Compatible nested rules with independent buffers
[*]Pause work, Recall workers, and release individual saved items
[*]Work, hauling, wandering, child-watching, and pawn-category access controls
[*]Native outside jobs route around areas where that activity is disabled
[*]Managed apparel and weapon storage filters with locker restocking
[*]Live readiness, worker, hauler, wanderer, and restoration status
[*]Generic support for vanilla and modded apparel and weapons
[/list]

[h1]Requirements[/h1]
[list]
[*]RimWorld 1.6
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077]Harmony[/url]
[/list]

[h1]Getting started[/h1]
[olist]
[*]Create an area over the workspace that needs managed gear.
[*]Open the Automatic Outfit Manager tab and add a rule.
[*]Choose the work area and, optionally, a nearby locker room.
[*]Select every required apparel item and any acceptable primary-weapon alternatives.
[*]Configure the task buffer and access permissions.
[/olist]

[quote]Automatic Outfit Manager is rule-based. It does not automatically detect radiation, cold, fire, disease, or other hazards. Empty apparel or weapon selections mean that category has no requirement.[/quote]

[h1]Compatibility[/h1]
Harmony is the only dependency. Apparel and weapons from content mods are discovered through ordinary RimWorld definitions rather than hard-coded integrations. Simple Sidearms preferences are respected without rewriting that mod's weapon memories.

Drafted and explicit player-forced behavior is respected. Weapon selection uses normal RimWorld equipment eligibility, including ideology and biocoding restrictions; persona weapons are excluded from automatic selection.

[h1]Current boundaries[/h1]
[list]
[*]No automatic hazard, temperature, health-condition, job, or work-type triggers
[*]No manual priority for genuinely conflicting overlapping rules
[*]No per-pawn assignment filters
[*]No ammunition, inventory-sidearm, offhand, shield, or drafted-weapon management
[*]English interface text only
[/list]

[h1]Support and source[/h1]
[url=https://github.com/tekmojo/AutomaticOutfitManager]Documentation, source, and issue reporting on GitHub[/url]
```

## Version 0.3.2 change note

```text
Initial Steam Workshop release candidate. Includes area-based apparel/PPE rules, optional exact primary-weapon alternatives, skill-aware weapon choice, exact apparel and weapon restoration, nested rules and task buffers, locker storage management, access controls, live status, Recall/Release controls, and save-safe preserved-job continuation.
```

## Screenshot plan

Use real gameplay screenshots rather than recreating README scenarios.

1. Main rules screen showing one neutral apparel-only rule and **Any weapon**.
2. Weapon selector showing several exact alternatives and the retained-stock color legend.
3. Active worker status with saved gear, current transition, and task buffer visible.
4. Locker storage filters for managed/non-managed apparel and weapons.
5. Nested neutral rules on the map with compatible combined requirements.
