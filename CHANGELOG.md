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

- The Workers list now shows qualifying pawns who were already fully equipped, while distinguishing them from recallable AOM outfit sessions.
- Permitted hauling now continues through outfit preparation while ordinary work is paused, without restarting the same haul indefinitely.
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
