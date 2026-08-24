# Release checklist

This checklist validates features, not README example scenarios. Use arbitrary areas, apparel, weapons, pawn names, and jobs available in the test save; do not reproduce the named examples as fixtures.

## Automated preflight

- [ ] Release branch is based on the intended `main` commit and has no unrelated changes.
- [ ] `About.xml`, project metadata, assembly metadata, README, and changelog all identify version `0.3.2`.
- [ ] `About/Preview.png` is 16:9 and smaller than 1 MB.
- [ ] `About/ModIcon.png` is 64x64 with transparency.
- [ ] Every XML file parses successfully.
- [ ] `package-workshop.ps1` builds successfully and creates an allowlisted staging folder.
- [ ] The staging folder contains the release DLL and no source, project, script, debug-symbol, Git, or work files.
- [ ] The staged DLL reports version `0.3.2` and its SHA-256 is recorded.

## Feature-based manual matrix

Use temporary neutral rule names such as `RC Apparel`, `RC Weapon`, `RC Combined`, `RC Outer`, and `RC Inner`.

### RC-01: clean startup

- [ ] Start RimWorld 1.6 with only Harmony and Automatic Outfit Manager enabled.
- [ ] Create a new map and load an existing save.
- [ ] Confirm the mod tab opens and the player log contains no AOM exceptions or repeating warnings.

### RC-02: apparel-only / any weapon

- [ ] Configure arbitrary compatible apparel and leave weapon selection empty.
- [ ] Verify armed and unarmed eligible pawns can start qualifying work after apparel preparation.
- [ ] Verify the readiness row reports **Any weapon** and neither pawn's weapon state is changed by the rule.

### RC-03: weapon-only / any apparel

- [ ] Leave apparel selection empty and choose multiple exact weapon alternatives.
- [ ] Verify the readiness row reports **Any apparel**.
- [ ] Verify an eligible pawn equips one acceptable primary and later restores the exact previous primary or unarmed state.

### RC-04: combined requirements

- [ ] Select multiple compatible apparel requirements and multiple exact weapon alternatives.
- [ ] Verify every apparel item and one acceptable primary are equipped simultaneously before the pawn enters; no one-piece or partial-set transit is allowed when the complete set is available.
- [ ] Verify ranged/melee preference follows pawn skills when both categories are valid.
- [ ] While the pawn remains inside, switch naturally among work, hauling, wandering, eating, recreation, waiting, and sleep; verify the complete set remains equipped through every change.
- [ ] Send a pawn to an unrelated destination whose route crosses the area; verify the complete set is equipped before the first protected cell and retained until the route leaves it.
- [ ] Remove or destroy one required item while a pawn is idle or sleeping inside; verify bounded recovery re-prepares available gear without a Standing or job-retry loop.

### RC-05: restoration and ownership

- [ ] Exercise restoration with an initially armed pawn, an initially unarmed pawn, and two pawns competing for limited managed stock.
- [ ] Verify exact saved items show ownership and ordinary hauling cannot steal items needed for restoration.
- [ ] Verify **Recall owner** and confirmed **Release item** behave non-destructively.

### RC-06: buffers and nested rules

- [ ] Verify buffer values 0, 1, and a multi-task value.
- [ ] Verify compatible outer and inner rules combine requirements and track buffers independently.
- [ ] Verify inner-only requirements remain equipped until the pawn is physically outside the inner area, then restore only requirements no longer active.
- [ ] Remove the locker assignment and repeat with buffer 0; verify the pawn reaches a safe cell outside every applicable area before removing any managed item.

### RC-07: save/load continuity

- [ ] Save and reload while a pawn is preparing, performing preserved work, buffered, returning gear, and waiting for a saved item.
- [ ] Invalidate or destroy a queued pending-work target before saving; verify the continuation is discarded before serialization and the next load has no unresolved-object error from AOM's pending job.
- [ ] Verify the concrete job resumes only while still valid and contested or invalid work cancels cleanly.
- [ ] Confirm no retry loop, repeated Standing job, or `10 jobs in one tick` warning appears.

### RC-08: controls and access

- [ ] Verify rule pause/resume, per-worker Recall, and enable/disable behavior.
- [ ] Verify an already-compliant pawn doing qualifying work appears under **Workers** with **Recall**, while their personal apparel and weapon remain unchanged and unclaimed.
- [ ] Recall one worker while the rule remains active; verify the current managed task is cancelled, the pawn immediately returns to the configured locker, saved gear is restored, the AOM session clears, another worker may continue, and native task selection resumes without a waiting-for-reassignment state.
- [ ] Interrupt or otherwise invalidate a pawn's locker-return movement; verify a visible **Standing** state is detected within a few in-game seconds, the locker route or restoration is rebuilt, and the pawn does not remain under **Workers** indefinitely.
- [ ] Recall multiple workers together with one locker cell occupied or already targeted; verify they choose separate usable cells or restore in place without repeating **Returning to locker room**.
- [ ] Verify hauling and wandering appear only in their dedicated activity rows, including immediately after managed work and during a task buffer.
- [ ] With hauling allowed and work active, verify an unequipped pawn prepares once, completes the original haul, and does not alternate between preparation and restoration for the same target.
- [ ] Observe a haul with a visible delay toil; verify the advancing delay is not mistaken for finished work and the pawn completes the original haul before restoration.
- [ ] While a pawn is returning or restoring after a haul, verify it appears only under **Workers** and is not duplicated under **Haulers**.
- [ ] Place an assigned bed inside an active work area; verify the pawn equips every requirement, reaches the bed, and sleeps in the complete managed outfit without repeated **LayDown**, restoration, or Standing loops.
- [ ] With work paused, verify jobs targeted beyond the area do not create a walk-to-boundary/cancel/reselect loop when their shortest route crosses it.
- [ ] Disable hauling while leaving ordinary work enabled, then place a construction blueprint inside the area; verify resource-delivery scanners reject it consistently with no `CanGiveJob`/`JobOnX` synchronization error and no pawn standing on a prohibited delivery job.
- [ ] With hauling disabled and several prohibited haul candidates available, verify three restored pawns skip each rejected concrete target, promptly choose other native work or recreation, and do not remain **Standing** between repeated three-second waits.
- [ ] While a pawn has an active valid work session, let RimWorld propose prohibited hauling or other work; verify only that exact candidate is skipped, the outfit session remains active, and the pawn does not restore and immediately re-equip for the same bill.
- [ ] Place construction or hauling work outside and beyond a disabled-access work area while leaving an exterior route available; verify pawns keep the native job, route around the area, and complete it without equipping work gear or remaining **Standing**.
- [ ] Start work, hauling, and wandering routes outside the area, change the map so each route recalculates through it, and verify the actual boundary reapplies the corresponding access toggle even when the pawn already wears the complete required set.
- [ ] Allow a category, remove one required item after its route begins, and force a recalculation through the area; verify the pawn cannot enter until the complete set is restored.
- [x] With colonist hauling allowed, pause a rule while a pawn is hauling into its area; the pawn finishes without repeatedly changing outfits or restarting the same haul. Verified in-game on 2026-08-23 with RC DLL `687AD839C5B766331FAB4C4D8B0D9CEA1E53490194CF02D5A85AE4ACBC203A81`.
- [ ] Verify work, hauling, wandering, child-watching, and pawn-category permissions independently.
- [ ] Confirm drafted and explicit player-forced behavior is respected.

### RC-09: locker storage

- [ ] Verify locker-preferred selection with map-wide fallback.
- [ ] Verify managed/non-managed apparel and weapon filters, restocking, retained cyan stock, and **Forget** blockers.
- [ ] Verify temporary items return to the locker when configured.
- [ ] Draw a locker that overlaps the work area; verify AOM chooses a safe locker or exterior cell and never restores a required item while still inside.

### RC-10: compatibility smoke test

- [ ] Repeat weapon equip, explicit weapon override, restoration, and save/load with Simple Sidearms enabled.
- [ ] Confirm AOM does not rewrite Simple Sidearms memories or retain a temporary primary as a sidearm.

## Hidden Workshop validation

- [ ] Upload the staged folder as hidden and accept the Steam Workshop legal agreement if prompted.
- [ ] Preserve the new `About/PublishedFileId.txt` in the repository before any update upload.
- [ ] Add Harmony as a required Workshop item.
- [ ] Apply the prepared title, description, tags, preview, screenshots, and `0.3.2` change note from `WORKSHOP.md`.
- [ ] Test the Steam-downloaded copy with the local development junction disabled so it cannot mask packaging problems.
- [ ] Confirm dependency messaging, load order, title, icon, preview, and description display correctly.
- [ ] Make the item public only after the downloaded-copy smoke test passes.

## Release closeout

- [ ] Merge the release-prep branch to `main`.
- [ ] Tag the verified commit as `v0.3.2`.
- [ ] Publish matching GitHub release notes and attach the staged package if desired.
- [ ] Record the final commit, DLL SHA-256, Workshop ID, and public URL in the handoff state.
- [ ] Decide and add a source-code license if public reuse or redistribution should be permitted.
