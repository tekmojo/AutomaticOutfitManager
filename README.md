# Automatic Outfit Manager

Automatic Outfit Manager is a RimWorld 1.6 mod for area-based work apparel, personal protective equipment (PPE), and optional exact primary-weapon requirements.

Repository: [github.com/tekmojo/AutomaticOutfitManager](https://github.com/tekmojo/AutomaticOutfitManager)

Create a rule, select a work area, require any number of apparel items and optional exact primary-weapon alternatives, and optionally assign a locker room. Eligible undrafted humanlike pawns equip every selected apparel item and one acceptable selected primary weapon before entering, remain fully equipped for every activity and protected route inside the area, then return managed items and restore the exact apparel and primary weapon they had beforehand after leaving and completing any configured buffer.

The mod uses ordinary RimWorld areas, jobs, apparel, equipment, reservations, and storage. It supports vanilla and modded apparel and weapons without hard-coded hazard or content-mod integrations.

The compact technical identity is `AutomaticOutfitManager`, including the repository, package, assembly, namespaces, DefNames, serialized keys, filenames, and asset paths. This breaking rebrand does not preserve saves created under the former product identity.

## Requirements

- RimWorld 1.6
- Harmony

Current release candidate: version `0.3.2`, with the playtested Phase 2 foundation and Phase 3 exact primary-weapon requirements.

Dubs Rimatomics inspired the original radiation-PPE scenario, but it is not a dependency. Automatic Outfit Manager does not detect radiation automatically.

## Feature summary

- Area-triggered apparel and exact primary-weapon rules using ordinary RimWorld areas and jobs
- All-of apparel requirements and one-of primary-weapon alternatives
- Continuous full-gear enforcement for work, hauling, wandering, eating, recreation, sleep, waiting, and pass-through
- Shooting-versus-Melee preference when both ranged and melee alternatives are available
- Exact saved apparel and primary-weapon restoration, including save/load continuity
- Exact queued-job continuation with temporary claims for real job targets and queues
- Optional locker rooms, low-priority restocking, and separate apparel/weapon storage filters
- Configurable 0–20 task buffers, including compatible nested work-area buffers
- Rule-wide **Pause work**, per-worker **Recall**, and saved-item **Release item** controls
- Work, hauling, wandering, child-watching, and pawn-category access controls
- Live readiness, availability, worker, hauler, wanderer, and restoration diagnostics
- Generic vanilla and modded apparel/weapon discovery without hazard-specific integrations

## Quick start

1. Create a RimWorld area covering the workspace that needs special apparel or a primary weapon.
2. Optionally create a second area around the changing and storage space.
3. Open the **Automatic Outfit Manager** main tab.
4. Select **Add rule**, name it, and choose the **Work area**.
5. Select an optional **Locker room**.
6. Use **Choose apparel** to add every apparel item the worker must wear. Green apparel entries are cumulative requirements.
7. Optionally use **Choose weapons** to select one or more acceptable primary weapons. Green weapon entries are alternatives; the pawn equips one.
8. Configure the task buffer and access permissions.
9. Put reachable copies of the required apparel and weapons on the map.
10. For dedicated locker storage, enable **Allow managed apparel** and/or **Allow managed weapons**, and disable the corresponding non-managed filter when the storage should hold only managed stock.

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

Humanlike children can participate in assigned work when their category is allowed and the selected apparel supports their developmental stage. A pawn that cannot wear the complete requirement is blocked from that managed work instead of entering an outfit loop. **Allow work watching** separately controls children entering only to observe an adult. Non-humanlike pawns use access controls but do not participate in outfit changes.

### Required primary weapon

A rule can optionally list exact acceptable primary weapons using the same searchable selection pattern as apparel. Selected weapons are alternatives: a pawn needs one of them actively equipped, not every selected weapon. With no weapon selected, there is no primary-weapon requirement—the pawn may remain unarmed or keep any current weapon.

When both ranged and melee alternatives are selected, a pawn whose Shooting skill is higher than Melee prefers ranged, while higher Melee prefers melee; ties use a stable distribution. Automatic Outfit Manager searches the locker and then the map for the skill-preferred type before crossing to the weaker category, so locker proximity cannot reverse the pawn's combat-skill preference. Persona weapons and weapons biocoded to another pawn are never selected automatically; ordinary unbound weapons remain eligible.

The exact previous primary weapon and every temporary managed weapon are saved through game saves. At restoration, temporary weapons are released through the pawn's equipment tracker at the locker room when one is configured, bypassing Simple Sidearms' secondary-weapon handling without changing its memories, and the exact original primary is re-equipped. A pawn who started unarmed returns to being unarmed.

Drafting pauses management. A player-forced equipment job, a Simple Sidearms preference, or another weapon-management job wins immediately and suppresses further automatic weapon changes for that outfit session without blocking the pawn's work. The saved primary is retained and restored with the rest of the saved outfit when the session ends or the worker's **Recall** button is selected. Requirements apply only to the active primary weapon; inventory sidearms do not satisfy them.

### Locker room

The optional locker room controls where restoration occurs and where shared apparel and weapons should be stored.

- Required apparel and weapons in the locker are preferred; suitable map-wide items are a fallback.
- After the task buffer is exhausted, the pawn returns there before restoring saved clothing and any managed primary weapon. Simultaneous returns choose separate usable cells; if the locker has no free reachable cell, recovery restores in place rather than leaving the pawn Standing indefinitely.
- Low-priority apparel- and weapon-specific hauling work givers return loose selected apparel and weapons to valid locker storage whenever their rule is enabled, including during normal active operation and while work is paused.
- Rules without a locker still change outfits without a dedicated storage return; restoration waits until the pawn reaches the nearest reachable cell outside every still-applicable protected area.

### Task buffer

The task buffer controls how many ordinary jobs a pawn may start after leaving qualifying work while still wearing the work outfit. It ranges from 0 to 20.

- `0 tasks`: restore saved clothing as soon as managed work ends.
- `1 task`: allow one follow-up job, such as eating or hauling.
- Higher values reduce repeated changes around busy work areas.
- Renewed qualifying work inside the area resets the counter, including direct orders and compatible modded work without a normal work-giver tag.
- Sleeping outside every applicable protected area bypasses the buffer and begins restoration; sleeping in an active area keeps the complete managed outfit.
- Pausing work, drafting, forced orders, and item availability can alter when restoration completes.

The worker row identifies the activity, for example:

`Foto — Buffered task 1 of 3: Consuming fine meal`

The worker tooltip summarizes every active buffer, for example `Buffers: Radiation Zone 1/3`. Compatible nested work areas track their own buffer progress instead of consuming the outer rule's buffer. A slot is reserved when its job starts so the same job cannot consume it repeatedly. Interrupted jobs are not currently rolled back.

### Saved apparel, weapons, and ownership

The mod restores the exact apparel and primary-weapon instances captured at the start, not merely another item of the same type.

- Saved personal apparel and the saved primary weapon remain claimed for their original pawn.
- Other pawns cannot optimize into, reserve, process, haul away, wear, or equip claimed items while they are needed.
- Shared rule-required apparel and weapons are not permanently assigned.
- A safety check removes wrongly worn saved apparel if another mod bypasses normal wear validation.
- Destroyed items are skipped; temporarily unavailable items retry with a cooldown.

Saved personal apparel and primary weapons show their owner in the inspect text. Selecting either item offers **Jump to owner**, **Recall owner**, and **Release item**. **Recall owner** returns the pawn from managed work and restores the whole saved outfit. **Release item** requires confirmation and removes only that exact item from the saved outfit; releasing a primary weapon does not select a replacement and may leave the pawn unarmed after restoration. The same non-destructive action is available as **Recall** beside an active worker in the Automatic Outfit Manager tab.

### Pause and resume work

**Pause work** closes the rule’s area to ordinary work and returns active workers to the locker. The rule remains configured and enabled.

While work is paused, existing ordinary work is interrupted safely, new ordinary work is rejected before assignment, and workers finish restoring their saved apparel and primary weapon. Permitted hauling and wandering remain independently controlled. The button changes to **Resume work**, including in collapsed view. Readiness changes immediately with the rule; worker rows independently show anyone still returning managed items or restoring saved items.

When enabled work areas overlap, the most restrictive pause wins in their shared cells. A larger or partially overlapping rule reports **Active — shared cells paused: Rule name** and continues working elsewhere. A rule whose every work cell is covered reports **Blocked — work area covered by paused: Rule name**. Return travel and exact saved-clothing restoration remain allowed so workers can leave safely and finish changing.

### Hauling and wandering access

Each rule has separate **Hauling** and **Wandering** permissions for colonists, mechs/robots, animals, guests, slaves, and prisoners.

These permissions govern travel into or through the area; they do not outfit non-humanlike units. Disallowed units inside receive an exit job. Units already outside briefly wait instead of repeatedly selecting a route through the restriction. The **Haulers** and **Wanderers** rows show current relevant activity.

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

## Example scenarios

These are illustrative configurations, not built-in hazard detectors, presets, or test requirements. The named apparel and weapons can come from vanilla RimWorld or any compatible content mod, and any valid custom configuration is equally supported.

| Scenario | Work area | Apparel requirement | Primary-weapon alternatives | Locker and buffer |
|---|---|---|---|---|
| Radiation maintenance | Reactor and machining room | Radiation suit + radiation mask | Any weapon | Airlock locker; buffer 0–2 |
| Freezer work | Freezer interior | Parka + cold-weather headwear | Any weapon | Entrance storage; buffer 1–3 |
| Guarded armory | Armory, gatehouse, or defensive workshop | Any apparel | Rifle, shotgun, or melee weapon as exact alternatives | Nearby armory racks; buffer 0 |
| Clean room | Laboratory, sterile kitchen, or hospital | Clean-room suit + mask | Any weapon | Changing bay; buffer 0 |
| Industrial workshop | Fabrication room | Apron + helmet + respirator | Any weapon or selected work/security weapon | Equipment closet; buffer 2–5 |
| Emergency fire zone | Temporary player-created response area | Every selected fire-resistant item | Optional emergency weapon/tool represented as a weapon | Emergency locker; buffer 0 |

### Apparel-only rule

Create a **Radiation Maintenance** rule over the reactor and machining tables. Select the radiation suit and mask in **Choose apparel**, and leave weapons empty. Every eligible pawn inside or passing through wears both apparel items simultaneously. The readiness row displays **Any weapon**, and Automatic Outfit Manager does not disarm or replace the pawn's existing primary weapon merely because the rule has no weapon requirement.

### Apparel-and-weapon rule

Create an **Industrial Safety** rule for a fabrication room. Select an apron, helmet, and respirator in **Choose apparel**, then select one or more exact work or security weapons in **Choose weapons**. Every eligible pawn wears all selected apparel and equips one acceptable primary weapon before entering, keeps the full set through every activity in the room, then restores the exact clothing and primary weapon used before the protected session.

### Weapon-only rule

Create a **Guard Weapons** rule, leave apparel empty, and select several exact rifles or melee weapons. The readiness row displays **Any apparel**. Pawns equip one acceptable primary weapon before entering or passing through and restore their exact previous primary afterward; a pawn who began unarmed returns to being unarmed.

### Nested areas

Use a broad **Industrial Safety** rule for a workshop and a smaller compatible **Hazard Bay** rule inside it. A pawn can add the inner area's apparel while retaining the outer requirements. Each rule tracks its own task buffer, and completed inner requirements are returned without discarding the still-active outer outfit. The selectors block apparel combinations that cannot be worn together and weapon alternatives with no valid overlap.

### Traffic restrictions

For a hazardous laboratory, disable animal or mech wandering without disabling permitted hauling. Keep **Allow work watching** off to stop children entering solely to observe work. These access controls also apply to non-humanlike units, but those units never participate in apparel or weapon changes.

## Compatibility and boundaries

- Harmony is the only dependency.
- No hazard, apparel, robot, storage, or race mod is hard-coded.
- Modded apparel appears automatically when it is a normal wearable `ThingDef`.
- Modded robots use native mechanoid properties and common mechanical identifiers for access controls.
- Drafted and player-forced behavior is respected rather than aggressively overridden.
- Simple Sidearms preferences are detected without changing that mod's weapon memories. Automatic re-equip jobs cannot steal AOM-managed weapons, while an explicit player or weapon-mod choice takes control for the remainder of the active work session. The exact saved primary remains available for restoration.
- Weapon selection uses RimWorld's normal equipment eligibility checks, including ideology and biocoding restrictions. Persona weapons are excluded from automatic selection.
- Apparel and weapon storage classification uses cached definition and exact-item indexes. Locker hauling scans only exact definitions selected by active rules before attempting pathfinding.
- Cooldowns, idle recovery, and continuation validation prevent broken jobs or unavailable saved items from causing retry storms or permanent Standing states.
- Compatible overlapping and nested rules combine apparel and weapon requirements and maintain separate task buffers. The selectors reject known incompatible combinations. There is no configurable manual priority for genuinely conflicting overlaps.

## Troubleshooting

### Waiting for saved apparel or weapon

Hover the worker. The tooltip reports whether saved apparel or the saved primary weapon is used by another pawn, carried, in a container, on another map, forbidden, reserved, unreachable, or ready to retrieve. Fix the condition and use **Recall** again, or select an accessible saved item and use the confirmed **Release item** action to remove only that exact item from the saved outfit. AOM also interrupts ordinary hauling jobs that attempt to move an item needed for restoration.

### Apparel or weapons are not being stored in the locker

Confirm storage inside the locker accepts the item and has **Allow managed apparel** or **Allow managed weapons** enabled as appropriate. An eligible hauler must be permitted and able to reach both the item and storage.

Clearing a rule requirement retains its apparel or weapon types as managed locker stock so general haulers do not immediately move the stock elsewhere. Open **Choose apparel** or **Choose weapons** and select **Forget** beside an unselected cyan stock type only when it should be released to non-managed storage. If **Forget** is disabled, hover it to see every rule or active pawn transition that still uses the type.

### A pawn does not change apparel or primary weapon

Confirm the rule is **Enabled** and work is resumed, the pawn is inside or its job targets, interacts with, or routes through the area, the pawn is an eligible undrafted humanlike colonist, slave, prisoner, or hosted guest, its category is permitted, and reachable copies of every required apparel item plus at least one acceptable selected primary weapon exist. Inventory sidearms do not satisfy the primary-weapon requirement.

### A mech, robot, or animal enters

Check its Work, Hauling, and Wandering columns for the activity that brought it into the area. These units obey access permissions but do not equip apparel or primary weapons.

### Developer logs

With developer mode enabled, successful interceptions, exact-job resumptions, load-time pending-claim restoration, buffer changes, apparel and weapon restoration, managed-item protection, Simple Sidearms overrides, idle recovery, and safety redirects are logged. A cancelled continuation includes its reason, such as an invalid target, reservation conflict, pause request, or urgent personal job. Identical guest-access diagnostics are limited to once per pawn per in-game day, and task-buffer reset diagnostics are rate-limited per pawn and rule. Repeated task transitions, repeated Standing jobs, or `10 jobs in one tick` warnings still indicate a bug worth reporting with the current log and a short video.

## Current limitations

- No configurable manual priority for conflicting overlapping rules; known incompatible selector combinations are blocked.
- No per-pawn assignment filters.
- No direct JobDef, WorkTypeDef, temperature, hediff, or hazard triggers.
- No strict/warning/best-effort modes.
- Weapon rules do not manage ammunition, count inventory sidearms, control offhands or shields, or automatically switch weapons while drafted.
- English-only interface strings.
- Buffer slots are reserved when jobs start, not after successful completion.

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
