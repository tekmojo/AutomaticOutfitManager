# Steam Workshop copy — 0.4.3 published

Published by the maintainer and verified on 2026-09-24 (America/Denver). The public description and separate rendered change note are archived in [Workshop closeout](Tests/WORKSHOP-CLOSEOUT-0.4.3.md). The unchanged 0.4.2 publication fields are preserved in [the published copy](WORKSHOP-PUBLISHED-0.4.2.md) and its [closeout](Tests/WORKSHOP-CLOSEOUT-0.4.2.md). See [release status](RELEASE-CHECKLIST.md) for remaining tests and publication gates.

- Existing item: `3792731788` — Automatic Outfit Manager. Preserve its identity and visibility.
- Supported version: RimWorld 1.6. Harmony required; Ideology optional for ceremony integration.
- Retain the branded preview and existing gallery until replacement captures are reviewed.
- About text, the compact Latest update block and the full change note are separate fields.

## Short description

Work and Non-Work Area rules for automatic outfits, saved personal gear, lockers and access permissions.

## Description (Steam BBCode)

```text
[h1]Automatic Outfit Manager[/h1]



[b]Outfits for work. Personal clothes for downtime.[/b]



Choose outfits and access permissions for RimWorld map areas. Work Area Rules prepare pawns for a freezer, workshop or reactor room. Non-Work Area Rules return Work outfits before entering a dining room, lounge or bedroom.



[quote][b]RimWorld 1.6[/b] | [b]Harmony required[/b] | Vanilla and modded apparel and primary weapons[/quote]



[quote][b]Latest update — 0.4.3[/b]

Children can use allowed areas without adult outfit restrictions. Ideology ceremonies in AOM-designated areas wait for eligible participants to finish available outfit changes before progressing.

[url=https://steamcommunity.com/sharedfiles/filedetails/changelog/3792731788]Read the full change notes[/url][/quote]



[h1]Choose outfits by area[/h1]

[list]

[*]Require every selected garment and one of the selected primary weapons before entry. Requirements remain in force during work, meals, recreation, sleep and necessary transit. Empty categories add no requirement.

[*]Return Work outfits and prefer saved personal clothing. Select fallback gear for pawns without a saved outfit, or turn the preference off to require a selected outfit for adults.

[*]With saved-personal preference off, a pawn inside who no longer matches an edited selection leaves safely before changing. Unavailable or unwearable gear keeps them outside while other reachable tasks can continue. Native safety and player-control exceptions remain.

[*]All Work Outfits is the default. Choose individual source rules to keep other issued gear; shared items stay until all their sources are selected for removal.

[*]Set condition and quality ranges for selected apparel and weapons. Saved personal restoration ignores these ranges.

[*]Keep an outfit for up to 20 compatible follow-up tasks. Only successful tasks count. A task needing a different outfit can end the allowance early.

[/list]



[h1]Keep personal gear personal[/h1]

Pawns normally restore the same personal items, including their previous primary weapon or unarmed state. Missing saved items do not activate fallback. Tattered personal apparel can be replaced by a suitable better garment only after it is successfully worn.



Selected outfit types and retained stock stay separate from personal snapshots. Older saves return borrowed items safely. Capable haulers can recover blocked saved items to suitable storage.



[h1]Area controls and activity tracking[/h1]

[list]

[*]Activities, Hauling and Wandering have separate permissions for colonists, mechs/robots, animals, guests, slaves and prisoners. Allow Children directly controls child access. Allowed children bypass adult clothing/weapon requirements, outfit buffers, group permissions and pause; unchecked areas block ordinary child entry. Every overlapping area must allow them. Native age and job eligibility still apply.

[*]Workers and Occupants show humanlike activity, outfit changes and completed buffer counts. Haulers and Wanderers keep animal and robot activity separate.

[*]Rule colors match their areas. Hover a badge to highlight; click to center the map. Custom areas appear first in menus.

[*]Selected gear names its source rules. [Retained] marks unused automatic outfit stock; grey entries explain conflicts.

[*]Edit descriptions, collapse rules or Recall a pawn, including children. Delete asks for confirmation.

[/list]



[h1]Pause activities without blocking rest[/h1]

Pause activities stops ordinary work, meals, recreation and learning. Allowed hauling, wandering, sleep, bed rest and animal nursing continue with access and outfit requirements. Adult rest in a Work Area still requires its PPE. Children use Allow Children instead of adult permissions, pause and outfit requirements.



Haulers may collect supplies from a paused Work Area when all delivery targets are outside and adult outfit requirements are met. Processing and construction remain paused. Unrelated locker trips avoid unnecessary protected-area entry.



[h1]Ideology ceremonies[/h1]

In AOM-designated areas, eligible participants finish available, reachable outfit changes before ceremony progress begins. A ready leader cannot bypass that wait. Dressing earns no attendance or progress. Native participation, cancellation and safety remain in control; impossible outfits do not hold the ceremony. Allowed children keep their clothes. Other ceremonies retain native timing. Ideology is optional.



[h1]Getting started[/h1]

[olist]

[*]Paint a Work Area and optional locker area. Add a Work Area Rule, choose the outfit and set access, gear standards and the task buffer.

[*]Provide reachable stock and capable haulers. For dedicated lockers, enable Automatic outfit apparel/weapons, disable their non-automatic counterparts and allow suitable item types and storage limits.

[*]Add a Non-Work Area Rule for downtime. Start with Default to Saved Personal Outfit and All Work Outfits.

[/olist]



[h1]Lockers and storage[/h1]

Remove and Clear keep types as automatic outfit stock. Forget releases unused types once no rule or outfit change needs them. Exact saved items use Automatic saved apparel/weapons tags and stay in automatic storage outside active outfit changes.



Storage limits are independent of rule sliders. To accept saved outfits of any condition or quality, allow their types and set storage to 0–100% and any quality. Saved tags do not bypass storage filters or create permanent reservations.



Locker warnings identify overlap and storage problems. Keep changing space and personal-outfit storage outside Work Areas. Hover warnings or area badges to locate conflicts.



[h1]Overlaps and compatibility[/h1]

Compatible Work Areas combine requirements with separate buffers. A Work Area entirely inside a Non-Work Area is disabled and its pawns recalled; adjust the areas and re-enable it. Other overlaps need compatible outfits.



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

## Version 0.4.3 published change note

```text
[b]0.4.3 — Child access and Ideology ceremony preparation[/b]

[b]Simpler child access[/b]
Allow Children now directly controls child access in both Work and Non-Work Areas. Allowed children keep their own clothes and weapons, bypassing adult outfit requirements, task buffers, activity permissions and area pauses.

Unchecked areas still deny ordinary child entry, with safe exit preserved. Every overlapping area enforces its own checkbox. Native age and job eligibility still apply.

[b]Prepare outfits before ceremonies[/b]
In AOM-designated areas, Ideology ceremonies wait at gathering for eligible participants to finish available, reachable outfit changes. A ready leader or role recipient cannot start ahead of other participants who are still preparing.

Dressing time earns no ritual progress or attendance credit. Native participation, cancellation and safety decisions remain in control. Unavailable or unwearable outfits do not hold the ceremony indefinitely, and ceremonies outside designated areas retain native timing.

[b]Smoother gathering[/b]
Spectators can prepare concurrently without competing over the common ceremony focus. Ready participants keep their outfits while gathering. Allowed children can participate without an adult outfit change, subject to the ceremony’s normal eligibility rules.

[b]Existing saves[/b]
Existing child-checkbox values and the default-off setting are preserved. Older child outfit sessions return borrowed gear through normal restoration. Babies, carried pawns and native/player safety exceptions keep their normal behavior.

Updated tooltips explain child access, adult permissions and pause behavior. Harmony remains required; Ideology is only needed for ceremony integration.
```

## Screenshots

The existing eight-image gallery remains a legacy UI example. See [0.4.3 capture plan](Screenshots/CAPTURE-PLAN-0.4.3.md). The description and change note were updated; the existing eight-image gallery is unchanged.

## Historical publication text

Published notes are preserved in [0.4.2 Workshop copy](WORKSHOP-PUBLISHED-0.4.2.md), [0.4.1 Workshop copy](WORKSHOP-PUBLISHED-0.4.1.md) and [the changelog](CHANGELOG.md).
