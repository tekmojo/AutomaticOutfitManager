# 0.4.6 RimWorld 1.7 compatibility maintenance readiness

October 12, 2026. The maintainer accepted the current 1.7 tests as sufficient and requested the compatibility update. No more gameplay testing is required to prepare this candidate. Unexercised cases remain deferred rather than passed.

## Candidate scope

About metadata adds 1.7 while preserving 1.6. `LoadFolders.xml` maps both to root assets and the shared `1.6/Assemblies` DLL. Version remains 0.4.6 for this compatibility-only maintenance package; no runtime source, save format or defaults change. No new DLL is built.

RC/live DLL SHA-256: `A25B40980BBC58D419227DD1ECA4B8F855A8A8552F115E4688CA41D091FA98EE`. Hash checks identify local files, not the already-running process's loaded bytes. Startup provenance and supplied gameplay evidence are detailed in the linked audits. Package hashes distinguish the candidate from the October 2 published package.

## Accepted gameplay evidence

- [Apparel and reload](RIMWORLD-1.7-NEW-COLONY-2026-10-12.md): Kitchen preparation, buffers, exact personal restoration and loaded active sessions.
- [Weapons, combined gear and reload](RIMWORLD-1.7-WEAPONS-2026-10-12.md): exact original SMGs, combined apparel/weapon sequencing and three loaded combined sessions restoring successfully.
- [Saved-personal Dining and video follow-up](RIMWORLD-1.7-NON-WORK-2026-10-12.md): restored personal gear, visible meals followed by further activity, billiards and later Kitchen preparation.
- Reported runtime: RimWorld 1.7.4976 rev1043, Harmony Mod 3.0.0.0 / library 3.0.0.0-rc.2, AOM 0.4.6; minimal Harmony + all official DLC + AOM stack. No failed clothing/weapon endings or AOM exception headlines in the reviewed test excerpts.

## Deferred evidence

Selected-outfit Non-Work, precise painted-boundary timing, earlier multi-area hauling-conflict cause and exact interrupted-meal outcome, unavailable-item recovery, additional save phases and nonzero buffer continuity, 1.6 save migration, optional integrations/full mod list, method-body patch scenarios, DLC transitions, and fresh dual-version packaged-load checks remain unverified. This update does not claim to fix any of those cases. Prior [0.4.6 follow-up](PATCH-FOLLOWUP-0.4.6.md) and [archived readiness](READINESS-0.4.6.md) retain their limits.

## Build and release state

The 1.7 installation uses the Waylith layout with `Assembly-CSharp.dll` in the game root; legacy 1.6 managed references are absent. The shared binary was built previously against 1.6 and is intentionally preserved. Rebuilding against a different API merely to declare support would invalidate its exact tested hash. Future source changes require an identified compatible reference set and new validation.

Static verification and package hashes are recorded in [upload preparation](UPLOAD-PREP-0.4.6-RIMWORLD-1.7.md). Workshop publication, public copy, subscriber comparison and restoration of the temporary upload junction are complete. The live development checkout was restored without source/metadata synchronization. GitHub/source-control operations remain separate and unperformed. See [closeout](RELEASE-CLOSEOUT-0.4.6-RIMWORLD-1.7.md) and [checklist](../docs/releases/CHECKLIST.md).
