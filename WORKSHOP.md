# Steam Workshop listing

## Item settings

- Title: `Automatic Outfit Manager`
- Initial visibility: `Hidden`
- Supported game version: `RimWorld 1.6`
- Required item: [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
- Suggested tags: select the available RimWorld 1.6, utility, interface, and gameplay tags that accurately match the item.
- Preview: `About/Preview.png`

## Short description

Area-based apparel, PPE, and exact primary-weapon rules with gear standards, lockers, successful-task buffers, access controls, and saved-gear restoration.

## Description (Steam BBCode)

```text
[h1]Automatic Outfit Manager[/h1]

Create area-based outfit rules using apparel, personal protective equipment (PPE), and optional exact primary-weapon alternatives.

Eligible undrafted humanlike pawns automatically equip the complete requirement before entry and retain it through work, hauling, wandering, personal activities, sleep, and pass-through. After leaving and successfully completing any configured follow-up buffer, they return items through an optional locker room and restore their saved apparel and exact previous primary weapon. Original apparel is restored by default; tattered personal apparel may be replaced by a valid better item after it is successfully worn.

[h1]Features[/h1]
[list]
[*]Ordinary RimWorld areas define where a rule applies
[*]Every selected apparel item stays equipped for every activity and protected route inside the area
[*]Require every selected apparel item and optionally one of several exact primary weapons
[*]Set storage-style condition and quality ranges independently for work apparel and weapons
[*]Skill-aware ranged or melee preference when both are valid
[*]Saved-apparel and exact primary-weapon restoration, including returning to an unarmed state and improving tattered personal apparel
[*]Optional locker rooms and configurable 0–20 successful-task buffers; interrupted jobs do not count
[*]Compatible nested rules with independent buffers
[*]Pause work, Recall workers, and release individual saved items
[*]Work, hauling, wandering, child-watching, and pawn-category access controls
[*]Hosted visitors restore personal gear and return assigned managed stock before natural departure
[*]Native outside jobs route around areas where that activity is disabled
[*]Managed apparel and weapon storage filters with locker restocking
[*]Live readiness, worker, hauler, wanderer, and restoration status
[*]Bounded recovery for destroyed, inaccessible, or repeatedly rejected saved items
[*]Protective managed apparel is retained across vacuum, dangerous temperatures, and toxic exposure until removal is safe
[*]Gravship Work Area and Locker room references follow their copied areas after flight
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
[*]Set the allowed condition and quality ranges for work apparel and weapons.
[*]Configure the task buffer and access permissions.
[/olist]

[quote]Automatic Outfit Manager is rule-based. It does not automatically create or activate rules for radiation, cold, fire, disease, or other hazards. Its safety guard only retains already-managed protective apparel while removal or the pawn's route would be hazardous. Empty apparel or weapon selections mean that category has no requirement.[/quote]

[h1]Compatibility[/h1]
Harmony is the only dependency. Apparel and weapons from content mods are discovered through ordinary RimWorld definitions rather than hard-coded integrations. Simple Sidearms preferences are respected without rewriting that mod's weapon memories. Native gravship area copying is supported.

A narrow RimWorld 1.6 compatibility correction keeps native rearm jobs available for player autocannon and uranium slug turrets when very low maintenance-cost settings make vanilla report an empty turret as full. RimWorld still controls the rearm job and materials; AOM does not manage ammunition.

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

[h1]License and legal notice[/h1]
Automatic Outfit Manager's original source code and documentation are licensed under the MIT License. Third-party software, RimWorld materials, trademarks, and game-derived screenshots retain their respective rights; see LICENSE and NOTICE.md in the source repository and mod package.

Portions of the materials used to create this content/mod are trademarks and/or copyrighted works of Ludeon Studios Inc. All rights reserved by Ludeon. This content/mod is not official and is not endorsed by Ludeon.
```

## Version 0.3.3 change note

```text
Adds Quiet, Basic (recommended), and Detailed logging under RimWorld's normal Automatic Outfit Manager mod settings. New installations and upgrades from earlier versions default to Basic; explicit choices persist. Routine pawn decisions no longer flood the player log: expected access and unavailable-gear states are Detailed-only, repeated managed-gear rejections are coalesced per pawn/item/session, and task buffers report only meaningful milestones. Automatic sidearm or loadout jobs no longer churn another pawn's exact saved weapon during restoration. Quiet and Basic skip detailed message construction and rate-limit bookkeeping; genuine warnings and errors remain visible.
```

## Version 0.3.2 change note

```text
Initial Steam Workshop release candidate. Includes area-based apparel/PPE rules, optional exact primary-weapon alternatives, independent condition and quality standards, skill-aware weapon choice, saved-gear restoration with tattered-apparel improvement, successful-completion task buffers, locker storage management, access controls, visitor-departure safeguards, hazard-safe gear retention, gravship area remapping, Simple Sidearms compatibility, native heavy-turret rearm compatibility, live status, Recall/Release controls, and save-safe preserved-job continuation.
```

## Screenshot plan

Use the real gameplay captures under `Screenshots/`; do not stage scenes merely to reproduce the README gameplay examples.

1. `AOM_Radiation_02.png` — full rule configuration and category access controls.
2. `AOM_Radiation_04.png` — Work Area selection tooltip with the painted map area highlighted.
3. `AOM_Radiation_05.png` — Locker Area behavior and restoration tooltip.
4. `AOM_Radiation_03.png` — apparel/weapon standards, readiness, and live activity rows.
5. `AOM_Radiation_06.png` — apparel selector showing all-of requirements and conflict feedback.
6. `AOM_Radiation_01.png` and `AOM_Radiation_07.png` — configured work and locker areas in play.
7. `AOM_Gravship_01.png` and `AOM_Gravship_02.png` — day/night gravship views showing the locker and shared work-gear layout.

These gallery sources are not part of the allowlisted runtime package.
