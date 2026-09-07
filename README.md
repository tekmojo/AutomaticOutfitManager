# Automatic Outfit Manager

**Outfits for work. Personal clothes for downtime.**

Automatic Outfit Manager lets you choose outfits and access permissions for RimWorld map areas. Use **Work Area Rules** for protective clothing, uniforms or primary weapons. Use **Non-Work Area Rules** to return work outfits before entering a dining room, lounge or bedroom.

**Version 0.4.0** · [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3792731788) · [GitHub releases](https://github.com/tekmojo/AutomaticOutfitManager/releases) · [Changelog](CHANGELOG.md)

Requires **RimWorld 1.6** and **Harmony**. Vanilla and modded apparel and primary weapons are supported. Radiation equipment inspired the original mod, but Rimatomics is not required. You choose the areas and gear; AOM does not create rules from detected hazards.

## Start with a Work Area

1. Paint an area over the room or workspace that needs an outfit.
2. Open **Automatic Outfit Manager** and select **Add Work Area Rule**.
3. Name the rule and select its **Work Area**.
4. Optionally select a **Locker Room** with storage for the gear.
5. Choose apparel and primary weapons. **Every selected garment must be worn together; weapons are alternatives, so a pawn equips one.** Empty categories add no requirement.
6. Set **Condition**, **Quality**, **Task Buffer** and access permissions.
7. Provide reachable gear. For dedicated locker storage, enable the matching managed filters and assign capable haulers.

Eligible humanlike pawns save their personal outfit, collect the required gear and continue their task. The requirements apply while inside or necessarily passing through the area, including eating, recreation and sleeping. Pawns prefer an outside route when an unrelated area can be avoided.

After leaving, they keep the Work outfit for any allowed follow-up tasks, then return borrowed items and restore saved personal gear. Sleeping outside the applicable area, a different required outfit, Recall or Pause work can end the buffer early.

## Add a Non-Work Area

Use **Add Non-Work Area Rule** for a dining room, recreation room or another place where pawns should change out of Work outfits.

| Option | What it does |
| --- | --- |
| **Default to Saved Personal Outfit** — checked by default | Prefer the personal outfit saved before Work gear was issued. Selected apparel and weapons are a fallback only when no personal outfit has been saved. |
| **Default to Saved Personal Outfit** — unchecked | Use this rule's selected apparel and weapons for everyone, after returning the chosen Work outfits. |
| **Remove Work Outfits** | Defaults to **All Work Outfits**. Choose individual Work rules to keep gear from other rules. |
| **View saved outfits…** | Inspect saved apparel and the primary weapon, including an unarmed outfit. Viewing does not capture or change an outfit. |

Missing or unreachable saved items **do not activate fallback**. A saved unarmed outfit stays unarmed. An empty fallback category adds no requirement and does not choose random items. When saved-outfit preference is off, empty slots may stay empty and compatible personal gear already worn may remain.

Gear shared by several Work rules stays on unless all its recorded sources are selected for removal. Saved personal items fill compatible slots around Work gear you keep. Older issued items without source history are retained in individual-selection mode; **All Work Outfits** can still return them.

A Non-Work selection cannot also require a gear type used by an enabled Work rule on the same map whose outfit is selected for removal. The selectors explain the conflict and reject new contradictory choices. Existing conflicts remain removable. A valid saved personal outfit can still be used when dormant fallback choices conflict.

Items matching enabled Work Area requirements on the pawn's map, including those rules' condition and quality ranges, are excluded from personal snapshots. Paused Work rules retain this designation; disabled or other-map rules do not add new exclusions. Selecting Non-Work fallback gear makes it **selected gear**, not abandoned stock.

## Task buffers

Buffers allow **0–20 successful follow-up tasks**. They do not assign jobs or make pawns finish extra work just to fill a counter.

- **Work buffer:** retain the Work outfit after leaving, then return it and restore the personal outfit. Renewed activity for that rule resets its count. Compatible overlapping Work rules track separate counts.
- **Non-Work buffer:** retain the saved personal or selected outfit, including permitted Work items, during compatible follow-up tasks. A new task needing a different outfit ends the allowance early: the pawn leaves safely, obtains the new outfit and continues that task if it remains valid.
- **Immediate:** no additional follow-up allowance. Ending a personal-outfit buffer does not strip personal clothes; temporary fallback items still follow their normal return.

Only successful tasks count. Travel, brief waits, failed tasks and interrupted tasks do not. The current task is shown separately from the completed count. Unrelated Work Areas are avoided where possible, even while buffering.

## Access and activity lists

**Activities**, **Hauling** and **Wandering** have separate permissions for colonists, mechs/robots, animals, guests, slaves and prisoners.

| Permission | Examples |
| --- | --- |
| **Activities** | Work, meals, rest, learning and recreation. Human cleaning also belongs here. |
| **Hauling** | Carrying items into, out of or through the area. This permits hauling; it does not change work priorities. |
| **Wandering** | Idle wandering and autonomous robot cleaning. Required robot charging duties keep their normal behavior. |

**Allow Children** additionally controls ordinary child activity and transit. Group permissions and outfit requirements still apply; selected gear must be usable by that child. Babies and carried pawns are unaffected. Pawns already inside retain a safe way out. Direct player orders, drafting, emergencies and necessary outfit returns keep their normal exceptions.

Humanlike pawns change outfits. Animals, mechs and robots follow access permissions without changing outfits. Guests, slaves and prisoners use the same activity categories, subject to the jobs the game and their other mods permit.

**Workers** and **Occupants** show humanlike activity and outfit changes, with active tasks first, then changes/returns, buffered tasks and other activity. Animals and robots appear under **Haulers** or **Wanderers**, with their actual activity shown. Their display group does not change which access permission applies: an animal eating still uses Activities. Hover for concise status; click to select and jump to a pawn.

## Gear, lockers and saved outfits

**Condition and Quality** apply to selected rule gear, including Non-Work fallback selections. They do not filter saved personal restoration. Items without a quality level remain eligible. Availability counts show matching map stock, excluding individual saved items; a count does not guarantee reachability, reservations or equipment eligibility.

AOM prefers suitable locker stock, then reachable map stock. For weapons, higher Shooting prefers ranged and higher Melee prefers melee when both are selected; the preferred category is searched before the other. Only the equipped primary counts. Persona weapons are excluded from automatic selection, and normal eligibility and biocoding restrictions apply.

Pawns normally restore the same physical personal items, including their previous primary weapon or unarmed state. Nearby saved gear is restored before optional distant improvements. A personal garment below 50% condition may be replaced by a compatible, outfit-allowed, healthier and better-scoring item; the saved outfit changes only after the replacement is successfully worn.

With a **Locker Room**, pawns return borrowed gear there. Without one, they change back at a safe cell outside the applicable area. Gear borrowed from another rule follows its source locker; a Non-Work rule's locker supplies its selected outfit rather than replacing saved personal items with arbitrary stock.

Saved items stay associated with their pawn while shared Work stock remains reusable. Temporarily unavailable items can delay a return. If another protected area blocks an exact saved item, a capable hauler can bring it to accepting, owner-accessible storage of equal or higher priority. The hauler still needs Hauling work, access and any required outfit. Without a suitable hauler or storage, the owner may continue waiting. AOM avoids repeatedly restarting an empty restoration queue; it does not promise that every inaccessible item can be recovered.

## Selectors, colors and storage

Selected gear appears first in the selectors. Entries and rule references use their map area's color; conflicts use muted grey. Hover text stays readable and explains the specific conflict.

- **[Rule Name]** identifies another selecting rule. **+number** means additional rules, listed in the tooltip.
- **[Retained]** means remembered locker stock that no rule selects. It does not mean a copy is currently available.
- **Remove / Clear** change the rule's selections while keeping those types managed for storage.
- **Forget** returns unused stock of that type to ordinary storage. It is unavailable while a rule selects the type or a current outfit change uses it. Individual saved or borrowed items remain protected.

The paired storage filters are **Allow managed apparel / Allow non-managed apparel** and **Allow managed weapons / Allow non-managed weapons**. Managed includes selected types, retained stock and individually saved or borrowed gear. Both Work and Non-Work selections count. For a dedicated locker, enable the managed category and disable its non-managed counterpart. Normal item, quality and condition storage filters still apply.

Rule badges share the area color. Hover a badge to highlight the area; click it to center the map on the area's middle. Area menus put editable custom areas before special game/mod areas, sorted by name. A mod can also create an ordinary editable area; that appears in the custom group.

Click the short description beside a badge to write a note. **Use default** restores a description based on the rule settings. Notes do not change behavior. Collapse a rule for a compact summary; **Delete** asks for confirmation and leaves its painted map areas intact.

## Overlaps and controls

Compatible overlapping Work rules combine requirements and keep separate buffers. All apparel must be wearable together and the areas must accept a common primary weapon. There is no manual priority for contradictory requirements.

A Work Area cannot be entirely inside a Non-Work Area, including identical painted areas. If painting or loading creates that conflict, the Work rule is disabled and its pawns are recalled. Adjust the areas, then enable it again. A Non-Work room inside a larger Work Area and partial overlaps are allowed only where the outfit requirements can be satisfied together.

**Pause work** stops ordinary work here and recalls pawns using the rule. Access and outfit requirements stay active. **Resume work** reopens work even if a previous recall is still finishing. **Recall** applies to one pawn; a pawn already wearing compliant personal gear can end the task without borrowing or changing clothes. **Enabled** turns the whole rule on or off while preserving its settings.

## Compatibility and boundaries

- Harmony is the only dependency. Content mods provide their normal apparel and weapons.
- Direct player weapon choices take priority. Automatic sidearm re-equip jobs do not override required weapons or take another pawn's saved weapon. Simple Sidearms memories are preserved.
- Already-managed protection is retained while removing it would expose the pawn or route to vacuum, dangerous temperature or toxic conditions. This does not detect a hazard and create an outfit rule.
- Gravship area references follow the copied destination-map areas. Ambiguous inactive copies are not guessed during load repair.
- Hosted visitors bypass buffers when naturally departing and return assigned managed stock before leaving.
- A narrow cross-area breakdown-repair handoff supports a single component. It is not a general system for staging every bill ingredient.
- No automatic hazard/job/work-type triggers, per-pawn assignment filters or manual conflict priority.
- No ammunition, inventory-sidearm, offhand or drafted weapon-switching management. A narrow native heavy-turret rearm correction leaves RimWorld in charge of ammunition and jobs.
- English interface text only.

Existing 0.3.x Work rules remain Work rules. Non-Work rules are optional additions. This does not restore compatibility with saves from the former Automatic Apparel product identity.

## Troubleshooting and reporting

For a waiting pawn, hover their row to identify the missing item or blocked route. Check that the gear is allowed, reachable and accepted by storage, and that capable haulers have Hauling enabled. A brief wait during a successful outfit change is different from a pawn repeatedly restarting the same change.

For locker storage problems, check both the managed filter and ordinary item filters. **Retained** types remain managed after clearing a rule; use **Forget** when they should return to ordinary storage.

For a Non-Work conflict, change the listed apparel/weapon choice or **Remove Work Outfits**. For a child or guest access problem, check the permission for the actual activity; eating and recreation use **Activities**.

Open **Options → Mod settings → Automatic Outfit Manager**:

- **Quiet:** warnings and errors only.
- **Basic (recommended):** version information and occasional recovery messages. This is the default.
- **Detailed:** outfit transitions, jobs, gear, buffers, access and recovery decisions in the player log. Enable it while reproducing a problem; it works without Developer Mode. Return to Basic afterward.

Report persistent stalls, repeated gear swaps or errors with the mod version, map/save context, affected pawn and rule, the log covering the problem, and a short video if possible. The concise player tooltips do not replace the Detailed diagnostic log.

[Report an issue](https://github.com/tekmojo/AutomaticOutfitManager/issues) · [Workshop change notes](https://steamcommunity.com/sharedfiles/filedetails/changelog/3792731788)

## Screenshots

Work Area outfit requirements and access controls:

![Work Area setup](Screenshots/0.4.0/01-work-area.jpg)

Non-Work Areas for saved personal or selected outfits:

![Non-Work Area setup](Screenshots/0.4.0/02-non-work-area.jpg)

[See all eight screenshots and captions](Screenshots/README.md), including outfit removal, selections, activity tracking and saved-item ownership.

## Development and local installation

With RimWorld 1.6 and Harmony installed, run `build.ps1`; use `-RimWorldDir "D:\SteamLibrary\steamapps\common\RimWorld"` for another install location. The DLL is written to `1.6/Assemblies/AutomaticOutfitManager.dll`.

`package-workshop.ps1` builds an allowlisted local package under `work/workshop-staging`; it does not publish it. Enable Harmony before AOM when installing the mod folder in RimWorld's Mods directory. See [project design](PROJECT-DESIGN.md), [release checklist](RELEASE-CHECKLIST.md), [Workshop copy](WORKSHOP.md) and the [screenshot gallery](Screenshots/README.md).

## License and notices

Automatic Outfit Manager's original source code and documentation are available under the [MIT License](LICENSE). Third-party software, RimWorld materials, trademarks, and game-derived screenshots retain their own rights; see [Third-Party and Game Content Notices](NOTICE.md).

> Portions of the materials used to create this content/mod are trademarks and/or copyrighted works of Ludeon Studios Inc. All rights reserved by Ludeon. This content/mod is not official and is not endorsed by Ludeon.
