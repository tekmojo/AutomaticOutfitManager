# Changelog

## 0.3.2 - 2026-08-23

First Steam Workshop release candidate.

### Added

- Optional exact primary-weapon alternatives for area-based rules.
- Skill-aware preference between valid ranged and melee alternatives.
- Exact saved-primary restoration, including returning pawns to an unarmed state.
- Weapon selection, readiness, storage, inspection, ownership, Recall, and Release controls.
- Managed and non-managed weapon storage filters with retained-stock handling.
- Save-safe preserved-job continuation and target claims during outfitting.
- Compatible nested apparel and weapon requirements with independent task buffers.

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
- Area-based apparel and PPE requirements.
- Optional locker rooms, task buffers, locker restocking, and exact apparel restoration.
- Rule pause/resume, per-worker Recall, live status, and pawn-category access controls.
- Bounded recovery behavior for unavailable saved items and invalidated jobs.
- Generic support for vanilla and modded apparel and weapons, with Harmony as the only dependency.

### Known boundaries

- No automatic hazard, temperature, hediff, JobDef, or WorkTypeDef detection.
- No manual priority for genuinely conflicting overlapping rules.
- No per-pawn assignment filters.
- Weapon rules do not manage ammunition, inventory sidearms, offhands, shields, or drafted weapon switching.
- English-only interface strings.
