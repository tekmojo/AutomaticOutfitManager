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
- Paused work is rejected across concrete job-giver overrides and again at job start, preventing boundary cancel/reselect loops when a route crosses the paused area.
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
