# Changelog

## 0.3.7 - 2026-09-03

### Fixed

- Shortened bounded idle recovery after completed work, incomplete buffers, preparation, locker return, and saved-outfit restoration so genuine transition gaps no longer appear as extended Standing jobs.
- Preserved RimWorld's existing queued saved-outfit Wear and Equip jobs across compatibility-inserted targetless waits instead of rebuilding the full restoration plan.
- Kept a complete managed outfit when new protected work actually becomes available during an automatic idle return, avoiding unnecessary restore/re-equip churn while preserving explicit Recall behavior.
- Redirected safe targetless waits with missing work gear out of protected areas instead of cycling through otherwise-valid gear candidates merely to resume Standing. Already-managed protection remains equipped where hazardous exposure still requires it.
- Scoped AOM-owned apparel, weapon, and locker-return path access to the active rule that owns the exact transition target, preventing those jobs from using unrelated managed areas as shortcuts while preserving safe egress.
- Allowed a sequential handoff between incompatible rule outfits when the pawn's current cell does not actually require both sets, even if the configured areas overlap elsewhere on the map.
- Preserved completed outer-buffer progress when a new work proposal is selected after locker return is already committed and that proposal never takes control.

### Changed

- Worker rows and compact hover tooltips now both report completed buffer tasks; the current in-progress job is shown separately and counts only after successful completion.
- Reduced natural locker dwell to a brief job boundary and gave automatic idle returns one short additional thinker window for newly available protected work.
- Coalesced repeated Detailed diagnostics when a stale protected-boundary retry is released.

## 0.3.6 - 2026-09-01

### Fixed

- Made jobs with targets outside a managed area prefer an exterior route instead of using the area as an unequipped shortcut, including otherwise permitted work, hauling, wandering, and autonomous recreation.
- Preserved an uncustomized exit route for pawns, animals, mechs, guests, and equipped workers that already occupy a managed area.
- Classified autonomous recreation under the Wandering access setting, including modded recreation such as swimming whose job names do not contain "Wander".
- Preserved the first exact late-bound job across a protected-boundary retry and retained it through temporary reservation conflicts instead of allowing a later autonomous job to replace it.
- Limited required-primary-weapon overrides to explicit player-forced choices; automatic sidearm or loadout selections no longer waive a rule's weapon requirement.

### Changed

- Clarified worker status text when an explicit player weapon override leaves the rule's required primary weapon unequipped.

## 0.3.5 - 2026-08-31

### Fixed

- Preserved the exact interrupted native job when a running, late-bound route first reaches a protected-area boundary, allowing ordinary AOM preparation to resume that job when it remains valid instead of losing the continuation to generic occupancy recovery.
- Kept a pawn outside the first protected cell while the late-bound job is reconsidered and required gear is prepared.

### Changed

- Expanded Detailed boundary diagnostics to report the current and next path cells and whether the pawn's current cell is inside or outside the protected area.

## 0.3.4 - 2026-08-30

### Changed

- Improved management-window responsiveness on large colonies by reusing each pawn's current RimWorld path for display-only hauling, wandering, and protected-transit classification instead of launching new pathfinder requests from the UI.
- Reused short-lived rule-readiness and pawn-status cache entries before rebuilding display data.
- Avoided drawing rules outside the visible scroll area and reused rule-height layout results within each UI pass.
- Kept the full occupancy, route, access, and gear checks used by gameplay enforcement unchanged.

## 0.3.3 - 2026-08-30

### Added

- Added a persisted global logging level under **Options → Mod settings → Automatic Outfit Manager**: Quiet, Basic (recommended), or Detailed. New installations and upgrades from earlier versions default to Basic; an explicitly saved choice persists.

### Changed

- Made detailed AOM diagnostics explicitly opt-in and independent of RimWorld Developer Mode.
- Centralized AOM log output so genuine warnings and errors remain visible while routine pawn decisions stay out of Quiet and Basic logs.
- Coalesced repeated managed-gear rejections by pawn, item, and outfit session; removed candidate-start chatter; and limited task-buffer progress to its first and final milestones.
- Prevented automatic sidearm or loadout jobs from repeatedly reacquiring another pawn's exact saved weapon during restoration, with a rate-limited fallback diagnostic for integrations that bypass normal job selection.
- Moved expected unavailable-gear, access, apparel-conflict, and protected-player-weapon states from warning spam to rate-limited Detailed diagnostics.
- Skipped detailed message construction and diagnostic-cache work entirely in Quiet and Basic mode, and bounded the cache to active sessions.

- Licensed the original Automatic Outfit Manager software and associated documentation under the MIT License, with separate notices for RimWorld/Ludeon, Harmony, and game-derived material.
- Added `LICENSE` and `NOTICE.md` to future Workshop package inputs.

## 0.3.2 - 2026-08-30

First Steam Workshop release.

### Added

- Optional exact primary-weapon alternatives for area-based rules.
- Skill-aware preference between valid ranged and melee alternatives.
- Exact saved-primary restoration, including returning pawns to an unarmed state.
- Weapon selection, readiness, storage, inspection, ownership, Recall, and Release controls.
- Managed and non-managed weapon storage filters with retained-stock handling.
- Save-safe preserved-job continuation and target claims during outfitting.
- Compatible nested apparel and weapon requirements with independent task buffers.
- Independent storage-style condition and quality ranges for required work apparel and primary weapons.
- Successful-completion task buffers for outer and nested rules; interrupted, failed, and invalidated jobs no longer consume slots.
- Better-item replacement for saved personal apparel below 50% hit points, using native outfit scoring and adopting the replacement only after a successful wear job.

### Retained and hardened

- Already-equipped qualifying pawns now receive the same tracked worker status and Recall control as workers outfitted by AOM, without claiming their personal gear.
- Per-worker Recall now cancels the current managed task, returns the pawn to the configured locker, restores saved gear, clears the AOM session, and immediately yields to normal RimWorld task selection without a lasting reassignment hold.
- Locker returns now restart actual locker travel or begin restoration directly when RimWorld or a compatibility job ends the return movement and leaves the pawn visibly Standing.
- Hauling and wandering remain in their dedicated activity rows instead of being promoted to Workers by work-giver metadata or a retained outfit session.
- Permitted hauling now remains part of its active outfit session until the haul completes, including while ordinary work is paused, without repeatedly changing outfits and reacquiring the same haul.
- Required apparel and primary weapons now follow active-area occupancy instead of job labels: eligible pawns retain the complete set while working, hauling, wandering, eating, recreating, waiting, sleeping, or passing through.
- Assigned beds inside active work areas now keep the full managed outfit for travel and sleep; essential jobs outside protected routes still begin restoration immediately.
- A bounded runtime check repairs missing pieces caused by loaded saves, area edits, or external gear changes while a pawn is already inside.
- Legitimate haul delay toils no longer look like completed or stalled work, disabled activity recalls enter the locker-return path directly, and simultaneous returns avoid occupied or already-targeted locker cells.
- Invalid destroyed targets are discarded before a pending continuation is saved, preventing new saves from persisting unresolved job references.
- Paused work is rejected across concrete job-giver overrides and again at job start, preventing boundary cancel/reselect loops when a route crosses the paused area.
- Construction resource delivery now uses hauling access consistently at both scanner phases, avoiding `CanGiveJob`/`JobOnX` mismatches when hauling is disabled.
- Route-dependent access denials now cool down only the rejected concrete target, letting native task selection immediately choose other legal work instead of repeating visible Standing waits.
- A prohibited job proposal no longer recalls a pawn from an otherwise valid active work session, avoiding restore/re-equip churn between repeated bills.
- Jobs with outside targets now keep their native task and take an available route around disabled-access work areas; they are rejected only when the protected area is the sole route.
- Automatic task-buffer completion now records and protects its exact locker-return destination, preventing shared job or boundary guards from replacing the return with a permanent Standing wait.
- Locker-return travel now uses a dedicated non-opportunistic job, preventing vanilla hauling prefixes from replacing an automatic return with a bounded but visible Standing delay.
- Recalculated routes now recheck both complete gear and work/hauling/wandering access at the actual protected-cell boundary.
- New-world weapon generation no longer queries the player faction before that faction exists.
- Work Area and Locker room references now remap to RimWorld's copied destination-map areas after gravship flight, with conservative state-corroborated repair for older post-flight saves.
- Managed protective apparel remains equipped while the pawn, destination, hauling endpoints, or route would otherwise expose the pawn to vacuum, dangerous heat or cold, tox gas, toxic fallout, or noxious haze.
- Hosted visitors bypass follow-up buffers when departing, restore personal gear before the native exit when possible, and cannot take assigned managed stock off-map when a departure is interrupted.
- Inaccessible exact saved weapons now use five bounded recovery attempts before only the stale saved claim is released and the pawn safely finishes unarmed. Repeated native Equip rejection is counted separately from recovery wakes and uses a longer limit for ordinary colonists than for custody pawns.
- A narrow RimWorld 1.6 compatibility correction restores native rearm availability for player autocannon and uranium slug turrets under very low maintenance-cost settings without replacing native fuel, reservation, or work logic.
- Forced right-click turret rearm queries no longer inherit an unrelated protected-area route customizer; the resulting native job still passes through AOM's gear and access checks.
- Destroyed or zero-stack things are rejected before vanilla opportunistic hauling inspects them, avoiding a compatibility crash from malformed haulable registries.
- Area-based apparel and PPE requirements.
- Optional locker rooms, successful-completion task buffers, locker restocking, and saved-apparel restoration with tattered-item improvement.
- Rule pause/resume, per-worker Recall, live status, and pawn-category access controls.
- Bounded recovery behavior for unavailable saved items and invalidated jobs.
- Generic support for vanilla and modded apparel and weapons, with Harmony as the only dependency.

### Known boundaries

- No automatic hazard, temperature, hediff, JobDef, or WorkTypeDef rule triggers. Hazard safety only retains already-managed protection; it does not create or activate a rule.
- No manual priority for genuinely conflicting overlapping rules.
- No per-pawn assignment filters.
- Weapon rules do not manage ammunition, inventory sidearms, offhands, shields, or drafted weapon switching.
- English-only interface strings.
