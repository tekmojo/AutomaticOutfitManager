# Steam Workshop copy — 0.4.1

Published on the existing Workshop item on 2026-09-08. The public description and full change note match the prepared text, and the downloaded package matches the audited stage. The current eight-image gallery is retained. See [0.4.1 Workshop closeout](Tests/WORKSHOP-CLOSEOUT-0.4.1.md) and the [release checklist](RELEASE-CHECKLIST.md).

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

Choose outfits and access permissions for RimWorld map areas. Work Area Rules prepare pawns for a freezer, workshop or reactor room. Non-Work Area Rules return Work outfits before entering a dining room, lounge or bedroom.

[quote][b]RimWorld 1.6[/b] | [b]Harmony required[/b] | Vanilla and modded apparel and primary weapons[/quote]

[quote][b]Latest update — 0.4.1[/b]
Improves outfit transitions, Pause activities, supply hauling and safe rest. Adds locker overlap warnings and highlights, clearer automatic outfit/saved item labels, and stronger separation between shared outfits and personal snapshots. AOM yields during mental breaks and resumes after recovery.
[url=https://steamcommunity.com/sharedfiles/filedetails/changelog/3792731788]Read the full change notes[/url][/quote]

[h1]Choose outfits by area[/h1]
[list]
[*]Require every selected garment and one of the selected primary weapons before entry. Requirements remain in force during work, meals, recreation, sleep and necessary transit. Empty categories add no requirement.
[*]Return Work outfits and prefer saved personal clothing. Select fallback gear for pawns without a saved outfit, or turn the preference off to require a selected outfit for everyone.
[*]All Work Outfits is the default. Choose individual source rules to keep other issued gear; shared items stay until all their sources are selected for removal.
[*]Set condition and quality ranges for selected apparel and weapons. Saved personal restoration ignores these ranges.
[*]Keep an outfit for up to 20 compatible follow-up tasks. Only successful tasks count. A task needing a different outfit can end the allowance early.
[/list]

[h1]Keep personal gear personal[/h1]
Pawns normally restore the same personal items, including their previous primary weapon or unarmed state. Missing saved items do not activate fallback. Tattered personal apparel can be replaced by a suitable better garment only after it is successfully worn.

Work/Non-Work selected types and retained shared stock cannot become personal snapshots. Existing saves return held shared items through the safe locker path. Nearby saved items are restored first; capable haulers can recover blocked items when suitable storage exists.

[h1]Area controls and activity tracking[/h1]
[list]
[*]Activities, Hauling and Wandering have separate permissions for colonists, mechs/robots, animals, guests, slaves and prisoners. Allow Children adds a child-access control; eligible children still follow outfit requirements.
[*]Workers and Occupants show humanlike activity, outfit changes and completed buffer counts. Haulers and Wanderers keep animal and robot activity separate.
[*]Rule colors match their areas. Hover a badge to highlight; click to center the map. Custom areas appear first in menus.
[*]Selected gear names its source rules. [Retained] marks unused automatic outfit stock; grey entries explain conflicts.
[*]Edit descriptions, collapse rules or Recall a pawn, including children. Delete asks for confirmation.
[/list]

[h1]Pause activities without blocking rest[/h1]
Pause activities stops ordinary work, meals, recreation and learning. Allowed hauling, wandering, sleep, bed rest and animal nursing continue with access and outfit requirements. Rest in a Work Area still requires its PPE.

Haulers can collect supplies of any item type from a paused Work Area for delivery outside it. Every delivery target must be outside that area, and the pawn needs the required outfit. This allows transport, not processing or construction inside a paused area. Unrelated locker trips avoid unnecessary protected-area entry.

[h1]Getting started[/h1]
[olist]
[*]Paint a Work Area and optional locker area. Add a Work Area Rule, choose the outfit and set access, gear standards and the task buffer.
[*]Provide reachable stock and capable haulers. For dedicated lockers, enable Automatic outfit apparel/weapons, disable their non-automatic counterparts and allow suitable item types and storage limits.
[*]Add a Non-Work Area Rule for downtime. Start with Default to Saved Personal Outfit and All Work Outfits.
[/olist]

[h1]Lockers and storage[/h1]
Remove and Clear keep types as automatic outfit stock. Forget releases unused types once no rule or outfit change needs them. Exact saved items use Automatic saved apparel/weapons tags and stay in automatic storage outside active outfit changes.

Storage limits are independent of rule sliders. To accept saved outfits of any condition or quality, allow their types and set storage to 0–100% and any quality. Saved tags do not bypass storage filters or create permanent reservations.

Locker warnings identify overlap and storage problems. Work/Non-Work overlap cells stay highlighted while the mod tab is open; hover a warning, locker button or badge to focus that locker. Keep changing space and personal-outfit storage outside Work Areas.

[h1]Overlaps and compatibility[/h1]
Compatible Work Areas combine requirements and keep separate buffers. A Work Area cannot be entirely inside a Non-Work Area, including identical areas. If painting creates that conflict, the Work rule is disabled and its pawns recalled; adjust the areas and re-enable it. Other overlaps still require compatible outfits.

[list]
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077]Harmony[/url] is the only dependency. You choose the areas and gear; AOM does not create rules from detected hazards.
[*]Animals and robots follow access permissions without changing outfits. Meals, rest and recreation use Activities; robot cleaning uses Wandering.
[*]Mental breaks, incapacitation and native emergency control suspend outfit intervention while preserving saved ownership. Normal handling resumes after recovery. Ordinary tiredness and sleep are not exemptions.
[*]Direct orders and drafting keep their normal behavior. Automatic sidearm choices do not override the required primary; Simple Sidearms memories are preserved.
[*]Borrowed protection stays on while removing it would expose the pawn or route to vacuum, dangerous temperatures or toxic conditions.
[*]Gravship areas follow copied destination areas. Hosted visitors return assigned borrowed outfits before naturally leaving.
[*]Normal equipment eligibility applies; persona weapons are excluded. No ammunition, sidearm/offhand or drafted weapon-switching management. English interface only.
[/list]

[h1]Support and source[/h1]
Mod settings offer Quiet, Basic (recommended) and Detailed logging. Use Detailed while reproducing a problem, then return to Basic. Include the version, affected pawn/rule and log; a short video helps with repeated outfit changes or persistent Standing.

[url=https://github.com/tekmojo/AutomaticOutfitManager]Player guide and source[/url] | [url=https://github.com/tekmojo/AutomaticOutfitManager/issues]Report an issue[/url]

[h1]License and legal notice[/h1]
Original source and documentation use the MIT License. Third-party software and game-derived materials retain their rights; see LICENSE and NOTICE.md.

Portions of the materials used to create this content/mod are trademarks and/or copyrighted works of Ludeon Studios Inc. All rights reserved by Ludeon. This content/mod is not official and is not endorsed by Ludeon.
```

## Version 0.4.1 change note — published

```text
[b]0.4.1 — Safer transitions and clearer locker controls[/b]

Pause activities now reflects both Work and Non-Work Areas. It stops ordinary work, meals, recreation and learning while permitted hauling, sleep, bed rest, nursing and wandering continue with access and outfit requirements. PPE-protected haulers can collect supplies of any item type for delivery outside a paused area; this does not reopen processing or construction inside it.

Improves raid/departure handoffs, saved-outfit continuation and recovery from temporary waits. Reduces unnecessary gearing for unrelated locker trips, preserves legitimate supply hauling and keeps interrupted tasks from consuming buffer progress. Children performing observed activities can also be recalled.

AOM yields during mental breaks, incapacitation and native emergency control, preserving saved outfit ownership and resuming safely after recovery. Ordinary rest still respects PPE requirements.

Storage filters now use Automatic outfit apparel/weapons and Non-automatic outfit apparel/weapons. Exact saved items are tagged Automatic saved apparel/weapons and remain in automatic storage even outside an active outfit change. Storage keeps its own item, condition and quality limits, independent of rule sliders.

Work/Non-Work selected types and retained shared stock cannot become personal snapshots. Existing saves return held shared items through the normal safe locker path; loose items are released from personal ownership without a special retrieval trip.

Locker overlap warnings highlight affected Work and Non-Work cells while the mod tab is open. Storage guidance is clearer, the loading description focuses on gameplay, and Detailed logging remains available for troubleshooting. Existing rule and filter identities are preserved.
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

The published 0.4.0 gallery remains the active gallery for 0.4.1, as requested by the maintainer. No screenshots were changed. The [capture plan](Screenshots/CAPTURE-PLAN-0.4.1.md) is deferred for a future refresh.

At 0.4.0 closeout, all eight [0.4.0 gallery images](Screenshots/README.md) were verified live on Workshop in the listed order, replacing the older gameplay gallery. Their public Steam image identifiers match the local originals; the branded cover remains separate. Every original is under 1,000,000 bytes. The same eight images form 0.4.0's [GitHub gallery](Screenshots/README.md).

## Historical change notes

The following describe their original releases, not current behavior where later releases supersede it.

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
