# Automatic Outfit Manager

Automatic Outfit Manager is a RimWorld 1.6 mod for area-based work apparel, personal protective equipment (PPE), and optional exact primary-weapon requirements.

Repository: [github.com/tekmojo/AutomaticOutfitManager](https://github.com/tekmojo/AutomaticOutfitManager)

Create a rule, select a work area, require any number of apparel items and optional exact primary-weapon alternatives, and optionally assign a locker room. Eligible undrafted humanlike pawns equip every selected apparel item and one acceptable selected primary weapon before entering, remain fully equipped for every activity and protected route inside the area, then return managed items and restore their saved apparel and primary weapon after leaving and completing any configured buffer. The original physical items are restored by default; tattered personal apparel may instead be replaced by a valid better item after that replacement is successfully worn.

The mod uses ordinary RimWorld areas, jobs, apparel, equipment, reservations, and storage. It supports vanilla and modded apparel and weapons without named content-mod integrations. Rules remain player-configured rather than hazard-triggered, while a generic safety guard keeps already-managed protective apparel on across vacuum, dangerous temperatures, and toxic exposure until the pawn can reach a safe cell.

The compact technical identity is `AutomaticOutfitManager`, including the repository, package, assembly, namespaces, DefNames, serialized keys, filenames, and asset paths. This breaking rebrand does not preserve saves created under the former product identity.

## Requirements

- RimWorld 1.6
- Harmony

Current release candidate: version `0.3.2`, combining the playtested area/outfit foundation and exact primary-weapon requirements with the RC hardening described below.

Dubs Rimatomics inspired the original radiation-PPE scenario, but it is not a dependency. Automatic Outfit Manager does not detect radiation automatically.

## Feature summary

- Area-triggered apparel and exact primary-weapon rules using ordinary RimWorld areas and jobs
- All-of apparel requirements and one-of primary-weapon alternatives
- Continuous full-gear enforcement for work, hauling, wandering, eating, recreation, sleep, waiting, and pass-through
- Shooting-versus-Melee preference when both ranged and melee alternatives are available
- Saved apparel and exact primary-weapon restoration, including save/load continuity, bounded unavailable-item recovery, and better replacements for tattered personal apparel
- Exact queued-job continuation with temporary claims for real job targets and queues
- Optional locker rooms, low-priority restocking, and separate apparel/weapon storage filters
- Configurable 0–20 successful-task buffers, including compatible nested work-area buffers; interrupted or failed jobs do not count
- Storage-style condition and quality ranges for both required work apparel and primary weapons
- Rule-wide **Pause work**, per-worker **Recall**, and saved-item **Release item** controls
- Work, hauling, wandering, child-watching, and pawn-category access controls
- Live readiness, availability, worker, hauler, wanderer, and restoration diagnostics
- Generic vanilla and modded apparel/weapon discovery without named content-mod integrations
- Generic protection retention across vacuum, dangerous temperatures, and toxic exposure while already-managed gear is still needed
- Gravship Work Area and Locker room references remap to their copied destination-map areas after flight

## Quick start

1. Create a RimWorld area covering the workspace that needs special apparel or a primary weapon.
2. Optionally create a second area around the changing and storage space.
3. Open the **Automatic Outfit Manager** main tab.
4. Select **Add rule**, name it, and choose the **Work area**.
5. Select an optional **Locker room**.
6. Use **Choose apparel** to add every apparel item the worker must wear. Green apparel entries are cumulative requirements.
7. Optionally use **Choose weapons** to select one or more acceptable primary weapons. Green weapon entries are alternatives; the pawn equips one.
8. Set the allowed **Condition** and **Quality** ranges for work apparel and weapons. These ranges filter rule gear, not saved personal gear restoration.
9. Configure the task buffer and access permissions.
10. Put reachable copies of the required apparel and weapons on the map.
11. For dedicated locker storage, enable **Allow managed apparel** and/or **Allow managed weapons**, and disable the corresponding non-managed filter when the storage should hold only managed stock.

### Selector colors and ordering

Both selectors search the displayed item name and technical DefName, and keep each group in stable alphabetical order:

1. Green entries are selected for the current rule and remain at the top.
2. Cyan entries are retained managed stock that is not selected for the current rule.
3. Uncolored entries are ordinary definitions that are neither selected nor retained.

Removing a green entry keeps its type managed and moves it to the cyan group. **Add** selects it again. **Forget** releases an unused cyan stock type to the ordinary group; while another rule or active pawn transition still uses that type, **Forget** is disabled and its tooltip explains every blocker.

## How each feature works

### Work area and entry protection

The work area is an existing RimWorld area. Its equipment requirement follows the location, not the pawn's activity. It applies when a job target or interaction location is inside, when the pawn is already inside, or when the actual route must cross the area.

The mod checks the initial job, current occupancy, and actual movement. If a route changes, an eligible pawn missing even one required apparel item or an acceptable required primary weapon is stopped before entering an active protected area and allowed to reconsider after obtaining the complete set. A periodic check repairs loaded saves, area edits, or external gear changes that leave an eating, recreating, waiting, or sleeping pawn inside without every available requirement.

The exact job that triggered outfitting is preserved while the pawn changes, including direct player assignments and compatible modded work that does not provide RimWorld’s usual work-giver tag. Its concrete targets are temporarily claimed so another outfitting pawn cannot take the same frame, bill ingredients, haul targets, or similar work. Before resuming, the mod confirms that the job and its reservations are still valid; invalid or contested work is released safely for normal reconsideration.

Activity type is never an entry exemption. Work, hauling, wandering, eating, recreation, waiting, sleep, and unrelated pass-through all retain the complete requirement for as long as their destination or route is protected. A bed inside an active work area therefore requires the same full gear while the pawn travels to it and sleeps. Sleeping outside every applicable area still bypasses the ordinary follow-up buffer and begins locker return/restoration.

### Required apparel

The searchable selector includes loaded vanilla and modded apparel. Every selected apparel definition is required simultaneously. With no apparel selected, the rule imposes no apparel requirement. Before an applicable job or route starts, an eligible humanlike pawn:

1. Saves every personal apparel item currently worn.
2. Finds reachable, reservable copies of missing required apparel.
3. Uses normal RimWorld `Wear` jobs to equip them.
4. Resumes the exact original job after preparation succeeds and its targets remain valid.

The **Condition** and **Quality** controls work like storage-filter ranges. Required apparel outside either range is not counted as available and does not satisfy the rule. Apparel without a quality level remains eligible. These controls apply only to shared work gear; they do not filter saved personal apparel during restoration.

Humanlike children can participate in assigned work when their category is allowed and the selected apparel supports their developmental stage. A pawn that cannot wear the complete requirement is blocked from that managed work instead of entering an outfit loop. **Allow work watching** separately controls children entering only to observe an adult. Non-humanlike pawns use access controls but do not participate in outfit changes.

### Required primary weapon

A rule can optionally list exact acceptable primary weapons using the same searchable selection pattern as apparel. Selected weapons are alternatives: a pawn needs one of them actively equipped, not every selected weapon. With no weapon selected, there is no primary-weapon requirement—the pawn may remain unarmed or keep any current weapon.

When both ranged and melee alternatives are selected, a pawn whose Shooting skill is higher than Melee prefers ranged, while higher Melee prefers melee; ties use a stable distribution. Automatic Outfit Manager searches the locker and then the map for the skill-preferred type before crossing to the weaker category, so locker proximity cannot reverse the pawn's combat-skill preference. Persona weapons and weapons biocoded to another pawn are never selected automatically; ordinary unbound weapons remain eligible.

Weapons have their own **Condition** and **Quality** ranges. Selected primary weapons outside either range are not counted as available and do not satisfy the rule. Weapons without a quality level remain eligible. Saved personal weapon restoration is not filtered by these work-gear standards.

The exact previous primary weapon and every temporary managed weapon are saved through game saves. At restoration, temporary weapons are released through the pawn's equipment tracker at the locker room when one is configured, bypassing Simple Sidearms' secondary-weapon handling without changing its memories, and the exact original primary is re-equipped. A pawn who started unarmed returns to being unarmed.

Drafting pauses management. A player-forced equipment job, a Simple Sidearms preference, or another weapon-management job wins immediately and suppresses further automatic weapon changes for that outfit session without blocking the pawn's work. The saved primary is retained and restored with the rest of the saved outfit when the session ends or the worker's **Recall** button is selected. Requirements apply only to the active primary weapon; inventory sidearms do not satisfy them.

### Locker room

The optional locker room controls where restoration occurs and where shared apparel and weapons should be stored.

- Required apparel and weapons in the locker are preferred; suitable map-wide items are a fallback.
- After the task buffer is exhausted, the pawn returns there before restoring saved clothing and any managed primary weapon. Simultaneous returns choose separate usable cells; if the locker has no free reachable cell, recovery restores in place rather than leaving the pawn Standing indefinitely.
- Low-priority apparel- and weapon-specific hauling work givers return loose selected apparel and weapons to valid locker storage whenever their rule is enabled, including during normal active operation and while work is paused.
- Rules without a locker still change outfits without a dedicated storage return; restoration waits until the pawn reaches the nearest reachable cell outside every still-applicable protected area.

### Task buffer

The task buffer controls how many ordinary jobs a pawn may complete successfully after leaving qualifying work while still wearing the work outfit. It ranges from 0 to 20.

- `0 tasks`: restore saved clothing as soon as managed work ends.
- `1 task`: allow one follow-up job, such as eating or hauling.
- Higher values reduce repeated changes around busy work areas.
- Renewed qualifying work inside the area resets the counter, including direct orders and compatible modded work without a normal work-giver tag.
- Sleeping outside every applicable protected area bypasses the buffer and begins restoration; sleeping in an active area keeps the complete managed outfit.
- Pausing work, drafting, forced orders, and item availability can alter when restoration completes.

The worker row identifies the activity, for example:

`Foto — Buffered task 1 of 3 in progress: Consuming fine meal`

The worker tooltip summarizes every active buffer, for example `Buffers: Radiation Zone 1/3`. Compatible nested work areas track their own buffer progress instead of consuming the outer rule's buffer. A candidate is recorded when its job starts and counts only when that exact job ends successfully. Interrupted, failed, or invalidated jobs are cleared without consuming a slot.

### Saved apparel, weapons, and ownership

The mod records the exact apparel and primary-weapon instances captured at the start. It restores those physical items by default rather than choosing arbitrary copies of the same type.

- Saved personal apparel and the saved primary weapon remain claimed for their original pawn.
- Other pawns cannot optimize into, reserve, process, haul away, wear, or equip claimed items while they are needed.
- Shared rule-required apparel and weapons are not permanently assigned.
- A safety check removes wrongly worn saved apparel if another mod bypasses normal wear validation.
- Saved personal apparel below 50% hit points may be replaced with a compatible, outfit-allowed item that is at least 50% healthy and scores better under RimWorld's native apparel optimizer. Ownership changes only after the replacement is successfully worn; the original saved claim is then released.
- Destroyed items are skipped. Temporarily unavailable items retry with cooldowns instead of creating Standing loops. An exact saved weapon that remains inaccessible after five bounded recovery attempts is released from the snapshot and the pawn finishes restoration unarmed. If a reachable item instead reaches RimWorld's native `Equip` job but is repeatedly rejected, the limit is five actual failed Equip jobs for slaves or prisoners and 30 for other pawns.

Saved personal apparel and primary weapons show their owner in the inspect text. Selecting either item offers **Jump to owner**, **Recall owner**, and **Release item**. **Recall owner** returns the pawn from managed work and restores the whole saved outfit. **Release item** requires confirmation and removes only that exact item from the saved outfit; releasing a primary weapon does not select a replacement and may leave the pawn unarmed after restoration. The same non-destructive action is available as **Recall** beside an active worker in the Automatic Outfit Manager tab.

### Pause and resume work

**Pause work** closes the rule’s area to ordinary work and returns active workers to the locker. The rule remains configured and enabled.

While work is paused, existing ordinary work is interrupted safely, new ordinary work is rejected before assignment, and workers finish restoring their saved apparel and primary weapon. Permitted hauling and wandering remain independently controlled. The button changes to **Resume work**, including in collapsed view. Readiness changes immediately with the rule; worker rows independently show anyone still returning managed items or restoring saved items.

When enabled work areas overlap, the most restrictive pause wins in their shared cells. A larger or partially overlapping rule reports **Active — shared cells paused: Rule name** and continues working elsewhere. A rule whose every work cell is covered reports **Blocked — work area covered by paused: Rule name**. Return travel and exact saved-clothing restoration remain allowed so workers can leave safely and finish changing.

### Hauling and wandering access

Each rule has separate **Hauling** and **Wandering** permissions for colonists, mechs/robots, animals, guests, slaves, and prisoners.

These permissions govern travel into or through the area; they do not outfit non-humanlike units. Disallowed units inside receive an exit job. For a job whose targets are outside, RimWorld keeps the native task and routes around the restricted area when another path exists. The exact task is rejected only when the protected area is the sole route. The **Haulers** and **Wanderers** rows show current relevant activity.

### Children and work watching

**Allow work watching** controls whether children may enter the active work area for learning and observation. When disabled, unsafe learning jobs are redirected or rejected before entry.

### Readiness and worker status

Readiness reports whether a rule currently accepts work, is configured, and has the required apparel, weapons, and storage available. **Work paused** means the rule was paused and remains closed until resumed. **Active — shared cells paused** means only the overlap is closed; **Blocked — work area covered** means paused overlaps cover the entire rule. Returning workers do not make an active rule appear paused. Availability is a map-level summary with each selected exact weapon counted separately; an item can still become reserved, unreachable, worn, equipped, or moved.

The **Workers** section includes every qualifying worker, including pawns who already had every required item before work began. All receive the same **Recall** action. Recall cancels that worker's current managed task, sends them to the configured locker room, restores any saved apparel and primary weapon, clears the AOM session, and immediately returns control to normal RimWorld task selection. It creates no lasting work block and does not pause the rule for other workers. An already-compliant session performs the same locker return and task cancellation without changing or claiming the pawn's personal outfit. Hauling and wandering remain in their dedicated access-activity rows, including when a managed outfit is retained through a task buffer.

Worker rows and hover tooltips expose the current transition:

- **Equipping required apparel** — collecting or wearing required apparel.
- **Equipping required weapon** — collecting or equipping a selected primary weapon.
- **Outfit requirements met** — prepared for managed work.
- **Buffered task X of Y: activity** — performing the named follow-up task.
- **Returning to locker room** — traveling to the configured locker room.
- **Restoring saved outfit** — returning managed items and restoring the exact saved apparel and primary weapon.
- **Restoration paused — sleeping or resting/drafted/forced order** — higher-priority behavior currently wins after the pawn is outside every applicable active area.
- **Return pending** — waiting for a safe job transition.

Hovering also shows the rule, buffer count, missing apparel or weapon, destination, or why saved apparel or a saved primary weapon is unavailable. Clicking a worker selects and jumps to that pawn.

### Rule management

Rules are named, enabled or disabled, and saved with the game. The **Enabled** checkbox changes whether the rule participates without deleting it; this is separate from **Pause work**. **Collapse** creates a compact summary for multi-rule management. **Delete** removes a rule and recalls affected workers while retaining its previously selected stock classifications. **Edit map areas** opens RimWorld’s normal area interface, and assigned areas use the native hover overlay.

## Storage filters

The mod adds paired special filters under both Apparel and Weapons:

- **Allow managed apparel** — accepts rule-required apparel, retained AOM locker stock, and exact managed or saved apparel.
- **Allow non-managed apparel** — accepts ordinary apparel not managed by the mod.
- **Allow managed weapons** — accepts rule-selected weapons, retained AOM locker stock, and exact managed or saved primary weapons.
- **Allow non-managed weapons** — accepts weapons not managed by the mod.

For a dedicated locker, enable the managed apparel and/or weapon filters and disable their non-managed counterparts. Selecting an apparel or weapon type remembers it as managed stock. Removing, clearing, disabling, or deleting a requirement does not immediately reclassify existing stock into general storage: the selector shows the retained type in cyan. Use **Forget** only when that unused stock type should become non-managed. **Forget** remains unavailable while another rule requires the type or an active pawn transition uses it, and its tooltip lists those blockers. Exact saved or currently tracked items remain protected individually. Dropped managed apparel and weapons are kept unforbidden so normal hauling can move them. Filters are enforced at the thing-filter, storage-acceptance, and hauling-job boundaries for compatibility with alternate storage systems and queued jobs.

## Gameplay examples

These are practical starting points built from native and commonly modded RimWorld gameplay. They are not built-in presets, automatic hazard detectors, or release-test fixtures: the player creates the areas, chooses the gear, and can adjust access permissions, lockers, and buffers for the colony.

| Gameplay idea | Content | Suggested Work Area | Suggested requirements | Suggested locker and buffer |
|---|---|---|---|---|
| Freezer shifts | Core | Freezer interior and frequently used access tiles | Parka + tuque; **Any weapon** | Entrance locker; buffer 1 to finish one nearby follow-up task before changing |
| Toxic-waste handling | Biotech | Wastepack storage, loading, or atomizer work cells | Gas mask + any player-chosen protective apparel; **Any weapon** | Changing point outside the contaminated route; buffer 0 |
| Gravship EVA and exterior maintenance | Odyssey | Airlock route, exposed deck, and exterior service cells | Vacsuit + vacsuit helmet; **Any weapon** | Pressurized airlock locker; buffer 0 |
| Security post or armory shift | Core or modded weapons | Guard post, armory, firing position, or defensive workshop | **Any apparel**, or selected armor; one of several exact primary-weapon alternatives | Nearby weapon rack; buffer 0–1 |
| Specialized workshop shift | Core or modded apparel | Fabrication, smelting, machining, or other dedicated work cells | Player-selected protective workwear; optional exact work/security weapon alternatives | Equipment closet; buffer 1–2 to reduce short gear-change cycles |
| Radiation maintenance | Modded industry or reactor content | Player-painted reactor service and material-handling cells | Mod-provided radiation suit + mask; **Any weapon** | Entry locker; buffer 0 so protective gear returns promptly after the route is safe |
| Cleanroom or medical containment | Modded medical or laboratory content | Laboratory, hospital, sterile production, or containment cells | Mod-provided cleanroom apparel + mask; **Any weapon** | Changing room; buffer 0 |

### Apparel-only pattern

Select any Work Area, choose one or more compatible apparel definitions, and leave the weapon selector empty. Every eligible pawn wears all selected items simultaneously before entering or crossing the area. The readiness row displays **Any weapon**, and Automatic Outfit Manager leaves the pawn's existing primary weapon unchanged.

### Combined apparel-and-weapon pattern

Select compatible apparel plus one or more exact primary-weapon alternatives. Every eligible pawn wears the complete apparel set and equips one acceptable primary before entering, retains the requirements throughout protected activity or transit, and later restores the saved personal outfit and exact previous primary. Tattered saved apparel may be improved only under the replacement policy described above.

### Weapon-only pattern

Leave apparel empty and select one or more exact primary-weapon alternatives. The readiness row displays **Any apparel**. Pawns equip one acceptable primary before entering or crossing the area and restore their exact previous primary afterward; a pawn who began unarmed returns to being unarmed.

### Gravship pattern

Select native map areas carried by a gravship as its Work Area and optional Locker Area, then configure any compatible apparel and primary-weapon requirements. When RimWorld copies those areas to the destination map after flight, Automatic Outfit Manager remaps the rule to the copied areas and keeps active managed protection equipped throughout placement. The same rule continues after landing, and restoration waits until the pawn leaves every applicable area and can change safely. The player still defines the areas and gear; Automatic Outfit Manager does not infer a ship boundary, detect a hazard, or create the rule automatically.

### Nested and overlapping patterns

Compatible outer and inner rules combine while both apply. A pawn can add inner-area requirements without discarding still-active outer requirements, and each rule tracks its own successful-task buffer. The selectors block apparel combinations that cannot be worn together and weapon selections with no compatible alternative.

### Access-control pattern

Work, Hauling, and Wandering permissions can be configured independently for each supported pawn category. **Allow work watching** separately controls children entering only to observe work. Non-humanlike units obey access permissions but do not participate in apparel or primary-weapon changes.

## Compatibility and boundaries

- Harmony is the only dependency.
- No hazard, apparel, robot, storage, or race content mod is hard-coded. Rules are never selected automatically from a named hazard or mod.
- Modded apparel appears automatically when it is a normal wearable `ThingDef`.
- Modded robots use native mechanoid properties and common mechanical identifiers for access controls.
- Drafted and player-forced behavior is respected rather than aggressively overridden.
- Simple Sidearms preferences are detected without changing that mod's weapon memories. Automatic re-equip jobs cannot steal AOM-managed weapons, while an explicit player or weapon-mod choice takes control for the remainder of the active work session. The exact saved primary remains available for restoration.
- Native gravship area copying is supported: after flight, Work Area and Locker room references are remapped to the copied destination-map areas. Load repair is conservative and does not guess between indistinguishable inactive source/destination copies without an active pawn state to identify the current map.
- Hosted visitors may use managed gear only when their category permissions allow it. Natural departures bypass the follow-up buffer, restore personal gear through the locker when possible, and use a final exit safeguard so interrupted departures do not take assigned managed stock off-map.
- Already-managed protective apparel remains equipped while the pawn, destination, hauling endpoints, or route would otherwise be exposed to vacuum, dangerous heat or cold, tox gas, toxic fallout, or noxious haze. This is a removal safety guard, not automatic hazard detection or automatic PPE selection.
- A narrow RimWorld 1.6 compatibility correction keeps native rearm jobs available for player autocannon and uranium slug turrets when very low maintenance-cost settings make vanilla report an empty turret as full. RimWorld still controls fuel, materials, reservations, work type, and the rearm job; AOM does not manage ammunition.
- A narrow opportunistic-hauling guard ignores destroyed or zero-stack things before vanilla hauling code can inspect them, avoiding a crash when another content mod briefly leaves malformed haulables registered.
- Weapon selection uses RimWorld's normal equipment eligibility checks, including ideology and biocoding restrictions. Persona weapons are excluded from automatic selection.
- Apparel and weapon storage classification uses cached definition and exact-item indexes. Locker hauling scans only exact definitions selected by active rules before attempting pathfinding.
- Cooldowns, idle recovery, and continuation validation prevent broken jobs or unavailable saved items from causing retry storms or permanent Standing states.
- Compatible overlapping and nested rules combine apparel and weapon requirements and maintain separate task buffers. The selectors reject known incompatible combinations. There is no configurable manual priority for genuinely conflicting overlaps.

## Troubleshooting

### Waiting for saved apparel or weapon

Hover the worker. The tooltip reports whether saved apparel or the saved primary weapon is used by another pawn, carried, in a container, on another map, forbidden, reserved, unreachable, or ready to retrieve. AOM retries with bounded cooldowns; an exact saved weapon that remains inaccessible after five recovery attempts is released automatically and the pawn finishes unarmed. A reachable weapon that RimWorld repeatedly rejects uses the separate actual-Equip limits described above. You may fix the condition and use **Recall** again, or select an accessible saved item and use the confirmed **Release item** action to remove only that exact item from the saved outfit. AOM also interrupts ordinary hauling jobs that attempt to move an item needed for restoration.

### Apparel or weapons are not being stored in the locker

Confirm storage inside the locker accepts the item and has **Allow managed apparel** or **Allow managed weapons** enabled as appropriate. An eligible hauler must be permitted and able to reach both the item and storage.

Clearing a rule requirement retains its apparel or weapon types as managed locker stock so general haulers do not immediately move the stock elsewhere. Open **Choose apparel** or **Choose weapons** and select **Forget** beside an unselected cyan stock type only when it should be released to non-managed storage. If **Forget** is disabled, hover it to see every rule or active pawn transition that still uses the type.

### A pawn does not change apparel or primary weapon

Confirm the rule is **Enabled** and work is resumed, the pawn is inside or its job targets, interacts with, or routes through the area, the pawn is an eligible undrafted humanlike colonist, slave, prisoner, or hosted guest, its category is permitted, and reachable copies of every required apparel item plus at least one acceptable selected primary weapon exist within the configured Condition and Quality ranges. Inventory sidearms do not satisfy the primary-weapon requirement.

### A mech, robot, or animal enters

Check its Work, Hauling, and Wandering columns for the activity that brought it into the area. These units obey access permissions but do not equip apparel or primary weapons.

### Developer logs

With developer mode enabled, successful interceptions, exact-job resumptions, load-time pending-claim restoration, buffer changes, apparel and weapon restoration, managed-item protection, Simple Sidearms overrides, idle recovery, and safety redirects are logged. A cancelled continuation includes its reason, such as an invalid target, reservation conflict, pause request, or urgent personal job. Identical guest-access diagnostics are limited to once per pawn per in-game day, and task-buffer reset diagnostics are rate-limited per pawn and rule. Repeated task transitions, repeated Standing jobs, or `10 jobs in one tick` warnings still indicate a bug worth reporting with the current log and a short video.

## Current limitations

- No configurable manual priority for conflicting overlapping rules; known incompatible selector combinations are blocked.
- No per-pawn assignment filters.
- No direct JobDef, WorkTypeDef, temperature, hediff, or hazard rule triggers. The generic removal safety guard described above only retains already-managed protection; it does not create or activate rules.
- No strict/warning/best-effort modes.
- Weapon rules do not manage ammunition, count inventory sidearms, control offhands or shields, or automatically switch weapons while drafted.
- English-only interface strings.

## Build

Install RimWorld 1.6 and Harmony, then run:

```powershell
.\build.ps1
```

For a non-default RimWorld location:

```powershell
.\build.ps1 -RimWorldDir "D:\SteamLibrary\steamapps\common\RimWorld"
```

The DLL is written to `1.6\Assemblies\AutomaticOutfitManager.dll`.

To build and create an allowlisted Steam Workshop staging folder, run:

```powershell
.\package-workshop.ps1
```

The staged mod and its SHA-256 manifest are written below `work\workshop-staging`. See [`RELEASE-CHECKLIST.md`](RELEASE-CHECKLIST.md) for the feature-based release validation and [`WORKSHOP.md`](WORKSHOP.md) for the prepared Workshop listing copy.

## Install for local testing

Copy the repository folder into RimWorld’s `Mods` directory, enable **Harmony** first, then enable **Automatic Outfit Manager**.

See [`PROJECT-DESIGN.md`](PROJECT-DESIGN.md) for implementation scope and future phases.
