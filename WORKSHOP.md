# Steam Workshop listing

## Item settings

- Title: `Automatic Outfit Manager`
- Initial visibility: `Hidden`
- Supported game version: `RimWorld 1.6`
- Required item: [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
- Suggested tags: select the available RimWorld 1.6, utility, interface, and gameplay tags that accurately match the item.
- Preview: `About/Preview.png`

## Short description

Area rules that automatically equip apparel, PPE, and optional exact primary weapons, then return managed items and restore each pawn's saved gear.

## Description (Steam BBCode)

```text
[h1]Automatic Outfit Manager[/h1]

[b]Area rules. Automatic gear. Exact restoration.[/b]

Create a RimWorld area, choose required apparel and optional exact primary-weapon alternatives, and let eligible undrafted humanlike pawns manage the rest. They equip before entry, keep the complete requirement through work, hauling, wandering, personal activities, sleep, and protected transit, then return managed items and restore their saved gear after leaving and completing any configured buffer.

[quote][b]RimWorld 1.6[/b] | [b]Harmony required[/b] | Vanilla and modded gear | Basic logging by default[/quote]

[quote][b]Latest update — 0.3.6[/b]
Outside-target jobs now prefer routes around managed areas instead of using them as shortcuts, while pawns and animals already inside retain a clear exit. Recreation obeys Wandering access, late-bound jobs keep ownership across temporary retries, and only explicit player weapon orders can override a required work weapon.
[url=https://steamcommunity.com/sharedfiles/filedetails/changelog/3792731788]Read the full change notes[/url][/quote]

[h1]How it works[/h1]
[olist]
[*]Define the area. Use an ordinary RimWorld area for the workspace, route, room, or defensive position that needs managed gear.
[*]Set the requirement. Select every required apparel item and, optionally, one or more acceptable exact primary-weapon alternatives.
[*]Let AOM manage the transition. Eligible pawns prepare before entry and retain the complete requirement throughout relevant activity and protected transit.
[*]Restore saved gear. After leaving and completing any successful-task buffer, pawns return managed items through the optional locker room and restore their saved apparel and exact previous primary weapon.
[/olist]

[h1]Core features[/h1]
[list]
[*]All-of apparel requirements: every selected apparel item must be worn simultaneously
[*]One-of weapon alternatives: equip one acceptable selected primary weapon, with skill-aware ranged or melee preference when both are valid
[*]Independent gear standards: storage-style condition and quality ranges filter shared work apparel and weapons
[*]Exact restoration: restore saved apparel and the exact previous primary weapon, including returning a pawn to an unarmed state
[*]Locker workflow: optional changing rooms, managed storage filters, and low-priority restocking
[*]Successful-task buffers: configure 0–20 completed follow-up jobs; interrupted or failed jobs do not count
[*]Nested rules: compatible overlapping areas combine requirements and track independent buffers
[*]Access controls: configure work, hauling, wandering, child-watching, and pawn-category permissions
[*]Live visibility: see readiness, availability, workers, haulers, wanderers, and restoration progress
[*]Player controls: pause a rule, Recall a worker, or release an individual saved item
[/list]

[h1]Gear restoration and safety[/h1]
[list]
[*]Original saved apparel is restored by default. Personal apparel below 50% hit points may transfer to a valid better replacement only after the replacement is successfully worn.
[*]Condition and quality ranges apply to shared work gear and rule availability; they do not replace exact saved-personal-gear restoration.
[*]Destroyed saved-item references are skipped; temporarily unavailable or repeatedly rejected items use bounded recovery instead of creating permanent Standing or retry loops.
[*]Already-managed protective apparel is retained across vacuum, dangerous temperatures, and toxic exposure until removal and the pawn's route are safe.
[*]Hosted visitors bypass follow-up buffers when naturally departing, restore personal gear when possible, and return assigned managed stock before leaving the map.
[/list]

[h1]Getting started[/h1]
[olist]
[*]Create an area over the workspace that needs managed gear.
[*]Open the Automatic Outfit Manager tab and add a rule.
[*]Choose the work area and, optionally, a nearby locker room.
[*]Select the required apparel and any acceptable primary-weapon alternatives.
[*]Set gear standards, task buffer, and access permissions.
[/olist]

[h1]Compatibility[/h1]
[list]
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077]Harmony[/url] is the only dependency.
[*]Vanilla and modded apparel and weapons are discovered through ordinary RimWorld definitions rather than hard-coded content-mod integrations.
[*]Simple Sidearms preferences are respected without rewriting that mod's weapon memories.
[*]Gravship Work Area and Locker room references follow their copied destination-map areas after flight.
[*]Drafting and explicit player-forced behavior take priority over automatic management.
[*]Normal RimWorld equipment eligibility still applies, including ideology and biocoding restrictions; persona weapons are excluded from automatic selection.
[/list]

[h1]Common questions[/h1]

[b]Does AOM detect radiation, temperature, disease, or other hazards automatically?[/b]
No. Rules are created and configured by the player. The safety guard only retains already-managed protective apparel while removing it or following the pawn's route would be hazardous.

[b]What happens when a category is empty?[/b]
Empty apparel or weapon selections add no requirement. A pawn may keep any primary weapon or remain unarmed when no weapon alternatives are selected.

[b]Does AOM manage ammunition, sidearms, offhands, shields, or drafted weapon switching?[/b]
No. AOM's weapon scope is the actively equipped primary weapon. A narrow RimWorld 1.6 compatibility correction preserves native rearm jobs for player autocannon and uranium slug turrets at very low maintenance-cost settings; RimWorld still controls the job and materials.

[b]Where are the logging controls?[/b]
Open [b]Options → Mod settings → Automatic Outfit Manager[/b] and choose Quiet, Basic (recommended), or Detailed. Basic is the default; use Detailed temporarily while reproducing an issue.

[h1]Current boundaries[/h1]
[list]
[*]No automatic hazard, temperature, health-condition, job, or work-type triggers
[*]No manual priority for genuinely conflicting overlapping rules
[*]No per-pawn assignment filters
[*]No ammunition, inventory-sidearm, offhand, shield, or drafted-weapon management
[*]English interface text only
[/list]

[h1]Support and source[/h1]
[list]
[*][url=https://github.com/tekmojo/AutomaticOutfitManager]Documentation and source on GitHub[/url]
[*][url=https://github.com/tekmojo/AutomaticOutfitManager/issues]Report a bug or request a feature[/url]
[*][url=https://steamcommunity.com/sharedfiles/filedetails/changelog/3792731788]Workshop change notes[/url]
[/list]

If Automatic Outfit Manager helps your colony, a rating or favorite helps other players find it.

[h1]License and legal notice[/h1]
Automatic Outfit Manager's original source code and documentation are licensed under the MIT License. Third-party software, RimWorld materials, trademarks, and game-derived screenshots retain their respective rights; see LICENSE and NOTICE.md in the source repository and mod package.

Portions of the materials used to create this content/mod are trademarks and/or copyrighted works of Ludeon Studios Inc. All rights reserved by Ludeon. This content/mod is not official and is not endorsed by Ludeon.
```

## Version 0.3.6 change note

```text
Fixes protected-area routing and work-gear enforcement. Jobs with targets outside a managed area now prefer routes around it, including otherwise permitted work, hauling, wandering, and recreation, while pawns and animals already inside retain a clear way out. Autonomous recreation now obeys Wandering access. Late-bound boundary retries preserve the first interrupted job and wait through temporary reservation conflicts instead of handing control to unrelated activities. Only an explicit player-forced weapon choice may override a rule's required primary weapon; automatic sidearm or loadout selections no longer do so. No rule setup changes are required.
```

## Version 0.3.5 change note

```text
Fixes late-bound protected-boundary handoffs. When a running native job chooses or recalculates a route that reaches a managed area only after the job has started, Automatic Outfit Manager now retains that exact interrupted job long enough to prepare the required gear and resume it when the target remains valid. The pawn stays outside the first protected cell during reconsideration instead of falling into generic occupied-area recovery. Detailed boundary diagnostics now report the current and next path cells and whether the pawn is still outside. No save-format or rule-setting changes.
```

## Version 0.3.4 change note

```text
Improves Automatic Outfit Manager's management-window responsiveness on large colonies. Display-only hauling, wandering, and protected-transit rows now reuse each pawn's current RimWorld path instead of launching fresh pathfinder requests from the UI. Short-lived readiness and pawn-status caches are checked before rebuilding display data, and rules outside the visible scroll area are no longer drawn. Full gameplay boundary, access, and gear enforcement remains unchanged. A large modded-save test with Detailed logging enabled found no measurable process-level CPU penalty from opening the window or panning the map, with no AOM errors or UI log spam. Save data and rule behavior are unchanged.
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
