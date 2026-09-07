# Steam Workshop copy — 0.4.0

Published on the existing Workshop item on 2026-09-06. Description, change notes and all eight replacement screenshots were verified publicly after the maintainer saved them. The downloaded package matches the audited stage. See [Workshop closeout](Tests/WORKSHOP-CLOSEOUT-0.4.0.md); the matching GitHub release uses the same package. Current publication status is in the [release checklist](RELEASE-CHECKLIST.md).

- Existing item: `3792731788` — Automatic Outfit Manager. Preserve its identity and visibility.
- Supported version: RimWorld 1.6. Required item: Harmony.
- Preview: `About/Preview.png`; current branded asset retained; replacement gameplay screenshots are cataloged below.
- Full description, compact Latest update block and change note are separate publication fields.

## Short description

Work and Non-Work Area rules for automatic outfits, saved personal gear, lockers and access permissions.

## Description (Steam BBCode)

```text
[h1]Automatic Outfit Manager[/h1]

[b]Outfits for work. Personal clothes for downtime.[/b]

Set outfits and access permissions for your RimWorld map areas. Work Area Rules prepare pawns for a freezer, workshop or reactor room. Non-Work Area Rules return Work outfits before entering a dining room, lounge or bedroom.

[quote][b]RimWorld 1.6[/b] | [b]Harmony required[/b] | Vanilla and modded apparel and primary weapons[/quote]

[quote][b]Latest update — 0.4.0[/b]
Non-Work Area Rules add saved personal outfits, optional fallback outfits and a choice of which Work outfits to remove. The interface now uses map-area colors, clearer activity lists and shorter tooltips. This update also improves buffers, outfit restoration and task continuity.
[url=https://steamcommunity.com/sharedfiles/filedetails/changelog/3792731788]Read the full change notes[/url][/quote]

[h1]Choose outfits by area[/h1]
[list]
[*]Require every selected garment and, if weapons are selected, one acceptable primary weapon before entry. Requirements stay in place through work, meals, recreation, sleep and necessary transit.
[*]Return Work outfits and prefer each pawn's saved personal clothes. Choose fallback gear for pawns without a saved outfit, or turn the preference off to require a selected outfit for everyone.
[*]All Work Outfits is the default. Select individual Work rules to keep other issued gear. Shared items remain until all their sources are selected for removal.
[*]Set condition and quality ranges for selected apparel and weapons. Saved personal restoration ignores these ranges.
[*]Keep an outfit for up to 20 compatible follow-up tasks. Only successful tasks count. New work needing a different outfit can end the allowance early.
[/list]

[h1]Keep personal gear personal[/h1]
Pawns normally restore the same personal items they wore before Work gear was issued, including their previous primary weapon or unarmed state. Missing saved items do not activate a fallback outfit. Tattered personal apparel can be replaced by a suitable better garment only after it is successfully worn.

Optional locker rooms provide a changing point and preferred stock. Nearby saved items are restored before optional distant improvements. Haulers can help recover blocked saved items when suitable accessible storage exists; their work priorities and area permissions still apply.

[h1]A clearer view of your colony[/h1]
[list]
[*]Rule badges and references share their map area's color. Hover to highlight an area; click the badge to center the map on it.
[*]Custom editable areas appear above special game/mod areas in the selectors.
[*]Workers and Occupants show humanlike activity, outfit changes and completed buffer counts. Haulers and Wanderers keep animal and robot activity separate.
[*]Activities, Hauling and Wandering permissions apply separately to colonists, mechs/robots, animals, guests, slaves and prisoners. Allow Children adds a child-access control.
[*]Selected gear shows its rule names. [Retained] marks unused managed stock; muted grey explains a conflict.
[*]Edit the short description beside a badge, collapse rules, pause work or Recall one pawn. Deleting a rule asks for confirmation.
[/list]

[h1]Getting started[/h1]
[olist]
[*]Paint a Work Area and, optionally, a locker area with storage.
[*]Open Automatic Outfit Manager and select Add Work Area Rule.
[*]Choose the area, compatible apparel and acceptable primary weapons. Every selected garment is required; weapons are alternatives. Empty categories add no requirement.
[*]Set gear standards, access and the task buffer. Provide reachable stock and capable haulers.
[*]For dedicated locker storage, enable Allow managed apparel/weapons and disable the matching non-managed filters.
[*]Add a Non-Work Area Rule for a dining room or lounge. Start with Default to Saved Personal Outfit and All Work Outfits, then adjust as needed.
[/olist]

[h1]Selections and storage[/h1]
Remove and Clear change a rule's requirements while keeping those types as managed locker stock. Forget returns an unused type to ordinary storage once no rule or current outfit change needs it. Individually saved or borrowed items stay protected. Both Work and Non-Work selections count as managed stock; [Retained] does not promise a copy is available.

[h1]Overlapping areas[/h1]
Compatible Work Areas combine requirements and keep separate buffers. Contradictory outfits are blocked. A Work Area cannot be entirely inside a Non-Work Area, including identical areas. If painting creates that conflict, the Work rule is disabled and its pawns are recalled; adjust the areas and re-enable it. Partial overlaps and Non-Work rooms inside larger Work Areas still need compatible outfits.

[h1]Compatibility and limits[/h1]
[list]
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077]Harmony[/url] is the only dependency. The player chooses areas and gear; AOM does not create rules from detected hazards.
[*]Eligible humanlike pawns change outfits. Animals and robots follow access permissions without changing gear. Meals, rest and recreation use Activities; robot cleaning uses Wandering.
[*]Direct player orders and drafting keep their normal behavior. Automatic sidearm choices do not override the required primary weapon; Simple Sidearms memories are preserved.
[*]Already-managed protection stays on when removing it would expose the pawn or route to vacuum, dangerous temperatures or toxic conditions.
[*]Gravship area references follow copied destination areas. Hosted visitors return assigned managed stock before naturally leaving.
[*]Normal equipment eligibility applies. Persona weapons are excluded from automatic selection.
[*]No ammunition, inventory-sidearm, offhand or drafted weapon-switching management. No per-pawn assignment filters or manual conflict priority. English interface only.
[/list]

[h1]Support and source[/h1]
Open [b]Options → Mod settings → Automatic Outfit Manager[/b] for Quiet, Basic (recommended) or Detailed logging. Detailed keeps the job, gear and recovery evidence useful for troubleshooting without filling normal tooltips with diagnostics. Use it while reproducing an issue, then return to Basic.

[url=https://github.com/tekmojo/AutomaticOutfitManager]Player guide and source[/url] | [url=https://github.com/tekmojo/AutomaticOutfitManager/issues]Report an issue[/url]

Please include the mod version, affected pawn/rule and the log covering the problem. A short video helps with repeated outfit changes or persistent Standing.

[h1]License and legal notice[/h1]
Automatic Outfit Manager's original source and documentation use the MIT License. Third-party software, RimWorld materials, trademarks and game-derived screenshots retain their respective rights; see LICENSE and NOTICE.md.

Portions of the materials used to create this content/mod are trademarks and/or copyrighted works of Ludeon Studios Inc. All rights reserved by Ludeon. This content/mod is not official and is not endorsed by Ludeon.
```

## Version 0.4.0 change note — published

```text
[b]0.4.0 — Non-Work Areas and clearer outfit management[/b]

Adds Non-Work Area Rules for dining rooms, lounges and other personal-outfit spaces. Prefer automatically saved personal outfits, choose fallback apparel and weapons, or require a selected outfit for everyone. All Work Outfits is the default removal setting; individual Work sources can be kept, with shared-item protection and conflict checks in both selectors.

The interface now follows map-area colors, groups custom areas first, names selecting rules, marks retained stock, and uses muted grey for conflicts. Rule descriptions are editable, badges center the map, and Delete asks for confirmation. Workers/Occupants and compact hovers show consistent completed buffer counts; animals and robots stay under Haulers/Wanderers. Meals, rest and recreation use Activities permissions, with broader Allow Children controls.

Improves task and meal continuation, buffer credit for successfully completed tasks, safe childcare handoffs, saved-gear restoration order and locker restocking. Eligible haulers can recover blocked saved items when suitable accessible storage exists. Pawns waiting for unavailable saved gear avoid repeatedly restarting the same return sequence.

Player tooltips and item inspection text are shorter and clearer, while Detailed logging remains available for troubleshooting.

Existing Work rules remain Work rules. Non-Work rules are optional additions.
```

## Screenshots

All eight [0.4.0 gallery images](Screenshots/README.md) are live on Workshop in the listed order, replacing the older gameplay gallery. Their public Steam image identifiers match the local originals; the branded cover remains separate. Every original is under 1,000,000 bytes. The same eight images form this release's [GitHub gallery](Screenshots/README.md).

## Historical change notes

The following describe their original releases, not current behavior where 0.4.0 supersedes it.

## Version 0.3.8 change note

```text
Fixes the tested cross-area task-continuity failures and their gear-change loops. Ordinary managed work is now classified by its actual worksite rather than an auxiliary material target. For vanilla-style breakdown repair with one component target, a source-equipped pawn can carry the component to the first neutral exterior cell, change for the destination area, and resume the exact repair without oscillating between incompatible outfits. Protected boundary retries keep detached job data instead of retaining a pooled live job, and compatibility-rewritten job starts cannot recursively re-admit the same retained haul. Prepared meal jobs receive one bounded recovery when opportunistic hauling tries to replace them, and the displaced haul cannot consume task-buffer progress. Essential personal jobs also avoid restore/re-equip cycling during a confirmed temporary gear shortage. No rule setup changes are required.
```

## Version 0.3.7 change note

```text
Improves transition stability after managed work. Genuine idle gaps during preparation, incomplete task buffers, locker return, and saved-outfit restoration now recover promptly instead of appearing as long Standing jobs. Existing queued Wear and Equip jobs survive compatibility-inserted waits, and newly available protected work can retain a complete managed outfit during an automatic idle return instead of causing restore/re-equip churn. Safe targetless waits with missing work gear leave the protected area rather than cycling gear merely to remain idle. AOM-owned apparel, weapon, and locker-return jobs may enter only the active rule that owns their exact target, preventing unrelated managed areas from becoming shortcuts. Worker rows and compact hovers now report the same completed buffer count; the current job counts only after successful completion. No rule setup changes are required.
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
