# Automatic Outfit Manager — Project Design

## Goal

Automatic Outfit Manager provides context-aware RimWorld outfits without named content-mod integrations. A player-defined area and selected apparel or exact primary weapons can represent radiation PPE, freezer clothing, firefighting apparel, clean-room garments, industrial safety apparel, combat armor, guard posts, armories, or role-play uniforms.

The mod does not create or activate rules by detecting hazards. It reacts to jobs, routes, areas, gear, and player configuration. Once managed protective apparel is active, a generic removal guard can retain it across vacuum, dangerous temperatures, and toxic exposure until the pawn reaches a safe cell.

## Design principles

1. **Generic rules.** Operate on RimWorld concepts rather than named content mods.
2. **Vanilla jobs where practical.** Use normal wear, remove, haul, path, reservation, and storage behavior.
3. **Save-specific configuration.** Rules and active pawn transitions persist with the game.
4. **Compatibility first.** Intercept narrow boundaries and recover safely when another mod changes a job.
5. **Player control wins.** Drafting, forced orders, schedules, and explicit pause/return commands are not aggressively overridden.
6. **Bounded recovery.** Failed transitions use cooldowns instead of immediate retry loops.

## Product identity and compatibility

The player-facing product name is **Automatic Outfit Manager**. The broader “outfit” name covers apparel, PPE, and the optional exact primary-weapon requirements introduced in Phase 3.

The product is branded **Automatic Outfit Manager** in human-readable text, while the repository and technical identity use `AutomaticOutfitManager`. The package ID is `tekmojo.automaticoutfitmanager`; the assembly, namespace, Harmony ID, DefNames, serialized keys, source names, and asset paths use the compact technical identity. The rebrand intentionally establishes a clean identity rather than retaining compatibility with saves created under the former product name.

## Phase 1 — Area-triggered outfitting foundation (implemented)

The foundation introduced enabled rules tied to RimWorld work areas and required apparel definitions. The current rule model also includes the exact primary-weapon alternatives implemented in Phase 3 plus independent condition and quality ranges for apparel and weapons. Empty categories are shown as **Any apparel** or **Any weapon**; an empty category adds no requirement. All selected apparel is cumulative, while selected primary weapons are alternatives. An eligible undrafted humanlike colonist, slave, prisoner, or hosted guest qualifies when already inside, when a job target or interaction position enters the area, or when a protected transit route crosses it and its category is permitted. Activity type does not weaken the equipment requirement.

If required apparel is missing:

```text
qualifying job
  → capture exact worn apparel
  → find reachable and reservable required apparel
  → queue normal Wear jobs
  → resume qualifying work
```

The searchable apparel selector enumerates loaded wearable definitions, so vanilla and modded apparel work without direct integration.

When an exact primary weapon is required, preparation uses the same preserved-job workflow. Higher Shooting prefers ranged alternatives, higher Melee prefers melee alternatives, and ties use stable distribution. The locker and then the map are searched for the preferred type before selection crosses to the weaker combat category.

## Phase 2 — State, restoration, access, and monitoring (complete; playtested)

### Persistent pawn state

Each intervention records:

- Pawn and active rule
- Exact original apparel and primary-weapon references
- Exact automatic/work apparel and temporary managed-weapon references
- Weapon intervention, restoration-request, and player/mod override state
- Preparing, active, returning, or restoring transition
- Return-request state and safe-interrupt cooldown
- Task-buffer usage and current buffered job
- Pending outer and nested buffer candidates, which count only after successful completion
- Locker-return and restoration retry timing
- The exact intercepted work job, whether it carries managed-work context, and the last untagged work definition used for consistent buffer/UI classification

Runtime indexes accelerate frequent pawn-state and managed-item lookups without changing save data.

### Exact work continuation and claims

The interrupted `Job` is deep-saved only in pawn apparel state while preparation runs; it is not duplicated in RimWorld’s job queue. Direct player assignments and modded jobs without `workGiverDef` retain managed-work context explicitly so they reset the rule buffer and display as work rather than as follow-up activity.

Every concrete Thing target in A/B/C and both target queues receives one atomic, short-lived claim. Jobs without Thing targets fall back to a cell claim. Pending claims are rebuilt deterministically after a save finishes loading, before pawn AI can take the preserved targets. Before saving and again after loading, target existence, map membership, real RimWorld reservations, rule applicability, recall/urgency, and claim contention are revalidated so destroyed queued targets are never newly persisted. A valid job replaces the next thinker candidate exactly. An invalid or contested continuation is released safely and logs its bounded cancellation reason in developer mode.

### Restoration

After managed work and the configured task buffer finish, the pawn first clears every still-applicable protected area, then returns to the optional locker room, removes managed apparel, releases the tracked temporary primary through the equipment tracker so Simple Sidearms cannot retain it as a secondary weapon, and restores the saved apparel and exact primary weapon. Without a locker, the nearest reachable exterior cell becomes the safe restoration point. Locker destinations exclude occupied, reserved, and concurrently targeted cells; when none is usable after the protected area is clear, bounded recovery restores safely in place instead of repeatedly selecting the same failed destination. Managed protection is not removed at a cell or along a pending route where its absence would expose the pawn to vacuum, dangerous temperatures, or toxic conditions.

Destroyed references are skipped. Temporarily unavailable items report their status and retry after a cooldown. Exact saved weapons that remain inaccessible receive at most five bounded recovery attempts before only that stale claim is released and restoration finishes unarmed. A reachable item that enters RimWorld's native Equip job uses a separate actual-failure counter: five for slaves or prisoners and 30 for other pawns. Saved personal apparel below 50% hit points may be replaced by a compatible, outfit-allowed item that is at least 50% healthy and scores better under RimWorld's native optimizer; ownership changes only after the replacement is successfully worn. Recovery/wait jobs pass through so a failed wear operation cannot create a same-tick retry storm. Saved apparel and primary weapons expose consistent owner navigation, recall, and confirmed per-item release actions.

### Task buffer

Each rule allows 0–20 successfully completed ordinary follow-up jobs after leaving its protected context before restoration. A candidate is recorded when a new bufferable job starts and consumes its slot only when that exact job ends successfully; interrupted, failed, or invalidated candidates are cleared without counting. Renewed qualifying work resets usage, including player-assigned and modded work that lacks a normal work-giver tag. Sleep outside every applicable active area begins restoration immediately; a bed or sleep route inside an active area keeps the complete requirement. The worker UI distinguishes the current in-progress candidate from completed counts and uses the same retained work context as the transition logic.

### Locker rooms and storage

Rules may reference a separate locker-room area:

- Acquisition prefers required apparel and weapons inside the locker.
- Restoration returns the pawn there first.
- Managed/non-managed special storage filters separately classify apparel and weapons. Rule-selected definitions enter persistent managed-stock catalogs, so clearing, disabling, or deleting a requirement cannot silently evacuate unused stock into general storage. An explicit selector action forgets a catalog entry when the player intends to release it.
- Dropped managed apparel and weapons remain unforbidden.
- A low-priority hauling work giver restocks locker storage whenever its rule is enabled, including during normal active operation and while work is paused.
- Condition and quality ranges affect rule availability and satisfaction but do not filter saved personal gear restoration.

### Saved ownership

- Required work apparel and weapons are shared.
- Displaced personal apparel is claimed for its original pawn.
- Outfit optimization, wear/equip, reservation, repair, processing, and hauling guards protect claimed apparel and weapons.
- Non-colony pawns cannot target managed apparel.
- A periodic invariant check removes wrongly worn claimed apparel if another mod bypasses normal validation.
- Tattered saved apparel may transfer its claim to a valid better replacement, but only after the replacement Wear job succeeds.
- Inspection text plus saved apparel and weapon gizmos expose role, owner, areas, **Jump to owner**, **Recall owner**, and confirmed **Release item** actions.

### Pause and resume work

**Pause work** closes one rule to ordinary work, interrupts active work safely, and restores current workers. **Resume work** reopens it. The control remains available in collapsed view.

Per-worker **Recall** leaves the rule active for everyone else. It cancels the pawn's current managed task, returns them to the configured locker room, completes any exact outfit and primary-weapon restoration, clears the AOM session, and yields immediately to normal RimWorld task selection. Recall does not create a persistent work restriction; native AI remains free to choose any subsequent task.

Work-giver result patches reject paused-area jobs early. A periodic consolidated scan catches jobs injected by other mods. Job transitions share rate-limited exception handling.

Pauses use deterministic, safety-first overlap precedence: if any enabled overlapping rule is paused, ordinary work is blocked in the shared cells. Readiness distinguishes a partially restricted rule that remains active elsewhere from a rule whose entire work area is covered by paused overlaps. Return travel and exact restoration jobs are narrowly exempt so a worker cannot be stranded while complying with the pause.

### Access controls

Hauling and wandering permissions are independently configurable for colonists, mechs/robots, animals, guests, slaves, and prisoners. Child work watching has its own toggle.

Restrictions evaluate targets and relevant routes. Units inside receive safe exits. Outside work, hauling, and wandering retain their native jobs and use a protected-cell path grid to take an available route around disabled-access areas; exact candidates are rejected only when no avoiding route exists. Non-humanlike units obey access rules but never enter the apparel intervention system. Hosted visitors whose native departure begins while a session is active bypass the ordinary buffer, restore personal gear through the locker when possible, and have a final exit safeguard that releases only their assigned managed stock before they leave the map.

### Path safety

Incoming jobs, current area occupancy, and actual next path cells are checked for eligible humanlike pawns. This catches route changes caused by doors, congestion, reservations, modded pathing, loaded saves, area edits, and external gear changes. Work, hauling, wandering, eating, recreation, waiting, sleeping, and pass-through use the same full-gear gate. Essential personal jobs restore first only when their destination and route are outside every applicable active area; a bed inside an area retains every required apparel item and an acceptable primary weapon for travel and sleep. The same route model retains already-managed protection when current cells, direct targets, hauling endpoints, queued targets, or traversed cells would expose the pawn after removal.

Hot-path checks use cached field access, non-allocating missing-item tests, indexed state, and a single periodic pawn traversal. Apparel and weapon stock definitions, exact item IDs, saved owners, and active assignments are indexed rather than rescanning rules or pawn snapshots from storage and reservation hooks. Weapon locker restocking enumerates only definitions selected by active rules and defers reservation and pathfinding until after cheap rule and ownership checks. Requirement edits safely recall affected workers, and queued automatic hauling revalidates its concrete destination at job start.

### User interface

The **Automatic Outfit Manager** main tab provides:

- Named enabled/disabled rules
- Work-area and locker-area selection with native hover overlays
- Searchable apparel and exact primary-weapon selection, with selected and retained-stock groups
- Independent storage-style condition and quality ranges for work apparel and weapons
- Hauling, wandering, and child permissions
- 0–20 task buffer
- Readiness and apparel/weapon availability
- Worker, hauler, and wanderer activity
- Detailed hover status and click-to-jump
- Per-worker Recall and area-wide pause/resume
- Managed/non-managed apparel and weapon storage filters with explicit retained-stock Forget actions
- Persistent collapse/expand state
- Rule deletion and RimWorld area management

## Phase 2 behavior boundaries

- Drafted and forced behavior takes priority where practical.
- Sleeping outside active protected contexts begins restoration instead of consuming an ordinary buffer slot; sleeping inside retains the complete requirement.
- A missing item can delay restoration but cannot cause unbounded retries.
- A lost, destroyed, recalled, urgent, reserved, or contested continuation falls back to normal job selection with a specific developer-mode reason.
- Compatible overlapping and nested rules combine requirements and track separate buffers. Known incompatible selector combinations are blocked, but genuinely conflicting overlaps have no configurable manual priority.
- Debug logging is tied to RimWorld developer mode. Repeated guest diagnostics use a one-day per-pawn/category interval; colony diagnostics retain the shorter stabilization interval.

## Phase 3 — Exact primary weapons (implemented; RC playtested)

- Searchable exact primary-weapon alternatives for armories, guard posts, hazardous workshops, and similar work areas
- One-of alternative matching, with skill-aware ranged/melee preference and stable ties
- Locker-first and map-wide fallback searches that preserve the preferred combat category
- Exact previous-primary restoration with save/load continuity, including originally unarmed pawns
- Persona and biocoding safeguards plus normal RimWorld equipment eligibility
- Simple Sidearms preference detection, automatic managed-weapon rejection, and player/mod override handling
- Managed/non-managed weapon storage filters, retained-stock catalogs, per-type Forget controls, and locker restocking
- Exact saved-weapon ownership, hauling protection, owner navigation, Recall, and confirmed per-item Release
- Allocation-conscious weapon definition, ownership, and assignment indexes for hot storage and job boundaries

## Deferred rule-engine expansion

- Deterministic manual rule priority and broader conflict resolution
- Per-pawn assignment and filters
- JobDef and WorkTypeDef triggers
- Current-area and destination-area combinations
- Drafted, temperature, environment, hediff, or generic hazard triggers where appropriate
- Strict, warning, and best-effort behavior modes
- Apparel and weapon material filters

## Phase 4 — User experience (planned)

- Copy/duplicate and reorder rules
- Presets or import/export if useful
- Localization
- Dedicated diagnostic/logging option
- Clearer visual severity for blocked transitions

## Compatibility strategy

Harmony is the only dependency. Rimatomics and other content mods are examples, not integrations. Simple Sidearms behavior is detected narrowly without changing that mod's weapon memories. RimWorld 1.6 gravship area copying is patched so rule references follow the copied Work/Locker areas. A separate narrow vanilla correction restores native autocannon and uranium slug turret rearm availability at very low maintenance-cost settings; it does not replace native refueling behavior. Optional compatibility code should be introduced only when generic RimWorld systems cannot preserve the intended contract.

Harmony patches should remain narrow, avoid destructive replacement of core systems, and preserve the game’s normal recovery jobs.

## Repository structure

```text
AutomaticOutfitManager/
├── About/
│   ├── About.xml
│   ├── ModIcon.png
│   └── Preview.png
├── Defs/
│   ├── MainButtonDefs/
│   ├── JobDefs/
│   ├── SpecialThingFilterDefs/
│   └── WorkGiverDefs/
├── 1.6/
│   └── Assemblies/
├── Source/
│   ├── Core/
│   ├── Detection/
│   ├── Patches/
│   ├── Rules/
│   ├── State/
│   ├── Storage/
│   └── UI/
├── Textures/
│   └── UI/Buttons/MainButtons/
├── build.ps1
├── package-workshop.ps1
├── CHANGELOG.md
├── PROJECT-DESIGN.md
├── README.md
├── RELEASE-CHECKLIST.md
└── WORKSHOP.md
```

The project references local RimWorld and Harmony assemblies; copyrighted game assemblies and third-party binaries are not committed.

## Development workflow

`main` represents the stable development baseline. Significant changes should be validated in a live modded colony, checked for log loops and exceptions, built successfully, and reviewed through a branch or pull request before merging.

The current 0.3.2 release candidate combines exact primary-weapon requirements with the tested Phase 2 foundation and RC hardening for successful-completion buffers, condition and quality standards, tattered saved-apparel improvement, visitor departures, hazardous-route protection retention, gravship area continuity, Simple Sidearms, and large modded colonies. It has been exercised through repeated rule edits, pause/resume, drafting, saved-item contention, nested work, long sessions, large-map scaling, and save/load transitions. Manual priority and pawn-eligibility prototypes remain deferred; ammunition, inventory sidearms, offhands, shields, and automatic drafted switching are outside this weapon scope.
