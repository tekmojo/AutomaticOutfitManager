# Personal snapshot exclusions — 2026-09-07

Supersedes the exclusion scope in the historical [Work-only cleanup audit](WORK-SNAPSHOT-CLEANUP.md).

## Behavior

- Apparel and primary weapon types selected by any Work or Non-Work rule cannot become personal snapshot items. Disabled, paused, unconfigured and other-map selections still count. Item condition and quality do not change selection identity.
- Retained shared stock types are excluded through the existing shared type catalogs until the player releases the type with **Forget**. Selection checks still cover legacy ranged/melee requirements. Empty gear selections do not claim all items.
- Exact ordinary personal items remain personal even when stored in a locker or included in the automatic storage category. Storage acceptance, rule outfit standards and personal snapshot eligibility are separate decisions.
- Existing capture, requirement-edit, load/maintenance and transition cleanup paths apply the same exclusion. Existing snapshots lose shared items; clearing a selection or forgetting a stock type does not silently recreate removed snapshot membership.
- Pawns without an active AOM session save exact held exclusions in `PendingSharedReturns`. The queue survives load and waits for an idle, unforced opportunity with no queued work, care, active Non-Work buffer or meal handoff. A normal return session then uses the associated source locker when available; missing source rules do not grant access through unrelated protected areas. Existing sessions can take over their exact items, and dropped/destroyed items are pruned.
- Held items move from personal originals to managed return ownership. Loose items lose obsolete personal ownership without a new retrieval assignment. Cleanup does not physically strip gear; normal locker, hazard and player-order handling remains responsible for changes.
- Work source attribution stays separate from personal exclusion. Non-Work-only and retained-only stock do not invent Work source IDs or new Work-removal ledger entries.
- Temporary Non-Work fallback/partial-return targets, buffers, explicit primary overrides and chosen Work-outfit retention keep their existing behavior. These temporary targets are not personal snapshots.
- No saved-weapon condition/quality setting was added. Native apparel-policy and saved-item replacement behavior are unchanged.

## Verification

`Tests/run-non-work-contracts.ps1 -VerifyFailureReporting`: **309 checks passed**, including 131 new checks covering both gear types, both rule kinds, selection scope, standards independence, safe-return ownership, loose items, stale Wear/Equip jobs, transient targets, retained stock and ordinary personal items.

The previous production policy fails the new exclusion regression. Its negative-control copy adds only the optional callback parameters needed to compile the updated fixture; its decisions remain unchanged. The callback is intentionally unused there.

Adjacent production checks: **50 restoration**, **89 meal/buffer**, **296 storage**. Also **39 locker warning** checks. Total: **783**. The normal build and `git diff --check` pass. These are headless checks, not live Unity/save round trips.

Candidate SHA-256: `7F226B03CDEA53324FEFC6170CB539FC95C1B17FF29336E6CBFB5780A896261F`.

Build includes the pending automatic-storage naming/scope and locker-highlight changes. Local deployment completed on 2026-09-07 with RimWorld closed. Candidate, live and installed DLL hashes match; the renamed storage-filter XML also matches at all three paths, with its saved identities/defaults unchanged. No game launch or publication. Live gameplay verification remains pending.

[Before copies, negative control, build output and check logs](../../testing/2026-09-07/personal-snapshot-exclusion/).

[Inactive migration checks and deployment record](../../testing/2026-09-07/inactive-snapshot-return/validation.json). The missing-session negative control reproduces the former null-state behavior and fails the expected return-ownership assertion.

## Focused gameplay verification

1. Load a snapshot containing selected Work/Non-Work apparel and a primary weapon. Include damaged/low-quality examples. After maintenance, the shared items should disappear from the personal snapshot while unrelated exact personal items remain.
2. Let a pawn return gear from an existing Work session. Previously personal shared items should use normal safe return; no repeated restore/remove cycle or new cross-area shortcut should appear.
3. Start with a legitimate saved outfit stored on the ground or in automatic-only locker storage. It should restore normally without using shared stock as a replacement. Native storage limits still control deposit destinations.
4. Exercise Non-Work fallback and partial Work-outfit removal, including a buffered meal and a player-selected primary. Intended temporary outfits should remain intact without appearing in personal history.
5. Clear all selections for a shared type. It should remain excluded while marked Retained. After Forget, a future fresh pre-work capture may include the now ordinary item; existing cleaned snapshots should not change automatically.
6. Save/reload during restoration and after cleanup. Confirm correct ownership, no obsolete shared-item restore steps and no duplicate-job warnings.
