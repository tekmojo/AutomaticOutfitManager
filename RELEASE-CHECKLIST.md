# Release checklist

This checklist validates implemented behavior. Use arbitrary compatible areas, apparel, weapons, pawn names, and jobs available in the test save; the README gameplay examples are player-facing starting points and are not test fixtures.

## Automated preflight

- [x] Release branch is based on the intended `main` commit and has no unrelated changes. Audited on 2026-08-30: merge base `6808d030dbd73b6162da833d934ab90dea738df2`; the deployed RC source and documentation are committed at `6b4f34eef9eb151b7deb2d3b1d195f725bae840e`, and the follow-up Workshop screenshots and release-copy cleanup were reviewed as intentional.
- [x] `About.xml`, project metadata, assembly metadata, README, and changelog all identify version `0.3.2`. Audited on 2026-08-30.
- [x] User-facing source labels, About copy, README, changelog, Workshop copy, and project design describe the same current feature boundaries. Audited and corrected on 2026-08-30.
- [x] `About/Preview.png` is 640x360 (16:9) and 369,895 bytes, below 1 MB.
- [x] `About/ModIcon.png` is 64x64 with a transparent background.
- [x] Every XML file parses successfully. Audited five files on 2026-08-30.
- [x] The documentation-corrected RC source builds successfully. Candidate DLL `168EA71AAF95416805CCB5D247F367B9514323C7771CA9A81A74E6E052B252C9`, product version `0.3.2`, built and deployed on 2026-08-30. Candidate, live repository, and installed junction hashes match.
- [x] `package-workshop.ps1` builds successfully and creates an allowlisted staging folder. Verified on 2026-08-30 at `work/workshop-staging/AutomaticOutfitManager`.
- [x] The staging folder contains nine release files under only `1.6`, `About`, `Defs`, and `Textures`; it contains no source, project, script, debug-symbol, Git, or work files. Independently audited on 2026-08-30.
- [x] The staged DLL reports version `0.3.2` and SHA-256 `168EA71AAF95416805CCB5D247F367B9514323C7771CA9A81A74E6E052B252C9`, matching the tested release candidate. Recorded on 2026-08-30.

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
- [ ] Make one selected weapon instance fail its native `Equip` while another selected alternative remains available. Verify the failed instance receives a bounded cooldown, the alternative is selected, and the pawn does not repeat weapon preparation or remain **Standing**.
- [ ] During the failed-`Equip` retry window, equip a different instance whose definition satisfies the exact requirement. Verify AOM accepts the current primary immediately even though that object was not the originally managed target.

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
- [ ] Save personal apparel below 50% hit points, provide a compatible outfit-allowed replacement at or above 50%, and complete restoration. Verify AOM adopts only a replacement that scores better under native apparel scoring, changes saved ownership only after the Wear job succeeds, releases the old exact claim, and does not use the work-rule Condition/Quality filters for this personal restoration choice.
- [ ] Use a garment definition as both shared work stock and one pawn's exact saved personal apparel. While that pawn restores it, let a second pawn attempt an automatic locker haul; verify the haul is cancelled once with all reservations released, the owner restores the exact garment, and both pawns leave restoration without repeated idle rebuilding.
- [ ] Interrupt the same saved-outfit restoration repeatedly without changing its targets. Verify recovery attempts back off progressively, reset as soon as job/toil/gear progress resumes, and yield immediately to downed, drafted, or emergency native behavior.
- [ ] Verify **Recall owner** and confirmed **Release item** behave non-destructively. Confirm **Release item** remains visible when the exact saved garment's definition is also shared work stock; releasing it must remove only that item from the saved snapshot, and Phase 3 must still return it normally if it was being reused as active work gear.
- [ ] After Phase 3 completes, drop one formerly saved personal item and verify its saved-owner label and claim are gone. Repeat with a valid off-map caravan owner and verify **Release item** remains available even though **Jump to owner** and **Recall owner** are not.
- [ ] Let a friendly visitor work in a protected area and then depart naturally. Verify the exit-map job bypasses the task buffer, the visitor returns through the locker and restores personal gear before leaving, and no managed apparel or weapon crosses the map edge. Interrupt that return once and verify the final exit safeguard drops only the visitor's exact assigned work gear.
- [ ] Save immediately after a visitor departs during preparation. Verify the save produces no unresolved `Lord_*` reference, then reload and confirm abandoned personal claims are released and any exact managed gear already carried into the world-pawn pool is returned to the configured locker map.

### RC-06: buffers and nested rules

- [ ] Verify buffer values 0, 1, and a multi-task value.
- [ ] Interrupt, fail, and invalidate outer and nested buffer candidates, then complete valid candidates. Verify only exact jobs ending with `Succeeded` increment the displayed count, in-progress labels include the pending slot, compact hover counts show completed slots, and label/hover progress remains consistent.
- [ ] Verify compatible outer and inner rules combine requirements and track buffers independently.
- [ ] Verify inner-only requirements remain equipped until the pawn is physically outside the inner area, then restore only requirements no longer active.
- [ ] Remove the locker assignment and repeat with buffer 0; verify the pawn reaches a safe cell outside every applicable area before removing any managed item.
- [ ] Configure two disjoint protected areas with individually valid but mutually incompatible apparel, then send a pawn directly from one to work or sleep in the other. Verify the pawn exits through a neutral changing cell with the source outfit, restores once, prepares the destination outfit, and never treats the sequential route as a simultaneous overlap. Repeat with genuinely overlapping painted cells and verify the real conflict remains blocked.

### RC-07: save/load continuity

- [ ] Save and reload while a pawn is preparing, performing preserved work, buffered, returning gear, and waiting for a saved item.
- [ ] Invalidate or destroy a queued pending-work target before saving; verify the continuation is discarded before serialization and the next load has no unresolved-object error from AOM's pending job.
- [ ] Verify the concrete job resumes only while still valid and contested or invalid work cancels cleanly.
- [ ] Have three pawns haul different stacks to the same stockpile cell while outfit preparation is required; verify the destination is claimed during preparation and any stale continuation cancels before `StartJob` without a reservation error or repeated outfit cycle.
- [ ] Designate a building inside an active work area for deconstruction while its pawn needs managed gear; verify the Worker row says **Equipping required apparel for: Deconstructing...** during preparation, AOM confirms the original native work giver still accepts the exact target, then queues the preserved original job with its native thinker context behind one brief clean handoff; verify active `JobDriver_Deconstruct` progress is never recalled merely because its visible report says **Standing**, the building is removed once, and no repeated outfit cycle occurs.
- [ ] Confirm no retry loop, repeated Standing job, or `10 jobs in one tick` warning appears.
- [ ] Configure two distinct active rules with incompatible apparel and place their changing/storage or transit cells so a pawn can move directly from one protected rule into the other. Let the pawn finish the first rule and receive a native **Wait** while physically inside the second. Verify AOM retains the original personal-outfit snapshot, performs at most one safe rule-to-rule handoff, keeps the second outfit through the bounded Wait, and never alternates restoration with occupied-area reactivation.
- [ ] Fly a gravship carrying an active Work Area and Locker Area to another map. Verify both AOM buttons immediately highlight the copied destination areas without a save/reload, active pawns retain every required item throughout placement, fresh work continues to match, and no pawn begins saved-outfit restoration or removes protective apparel while still inside the copied Work Area. Fly again to a third or previously visited map and repeat the checks, then save immediately after landing and reload; verify the same references and protection remain active. After every active outfit snapshot has cleared, save and reload once more; verify the inactive rule remains on the destination map instead of jumping back to the identical source-map areas.
- [ ] While a gravship worker has an active saved-outfit snapshot, leave one exact personal garment spawned on the source map and fly to another map. After the pawn safely leaves every destination Work Area, verify AOM restores all reachable personal gear, retries the source-map garment only five times, leaves that garment untouched on its original map, releases its saved ownership, and ends restoration without repeated **Standing** recovery.
- [ ] On a space map, give an equipped worker a job outside the pressurized ship or beyond its Work/Locker Area. Verify AOM retains the vac suit for a vacuum destination and for a pressurized destination whose route crosses vacuum, refuses to remove protective apparel in a breached or otherwise hazardous locker cell, and restores the saved outfit only after the pawn reaches a genuinely safe interior. Repeat with dangerous heat/cold and toxic exposure, then breach the room during an in-progress return and verify the next protective removal is interrupted before the garment comes off.

### RC-08: controls and access

- [ ] Verify rule pause/resume, per-worker Recall, and enable/disable behavior.
- [ ] Verify an already-compliant pawn doing qualifying work appears under **Workers** with **Recall**, while their personal apparel and weapon remain unchanged and unclaimed.
- [ ] Recall one worker while the rule remains active; verify the current managed task is cancelled, the pawn immediately returns to the configured locker, saved gear is restored, the AOM session clears, another worker may continue, and native task selection resumes without a waiting-for-reassignment state.
- [ ] Interrupt or otherwise invalidate a pawn's locker-return movement; verify a visible **Standing** state is detected within a few in-game seconds, the locker route or restoration is rebuilt, and the pawn does not remain under **Workers** indefinitely.
- [ ] Recall multiple workers together with one locker cell occupied or already targeted; verify they choose separate usable cells or restore in place without repeating **Returning to locker room**.
- [ ] Verify hauling and wandering appear only in their dedicated activity rows, including immediately after managed work and during a task buffer.
- [ ] With hauling allowed and work active, verify an unequipped pawn prepares once, completes the original haul, and does not alternate between preparation and restoration for the same target.
- [ ] Haul managed apparel to locker storage along a route that crosses an active work area; verify the pawn prepares before the first protected cell, remains listed as a Hauler rather than a Worker, resumes the exact haul, and restocks without a boundary/recheck loop.
- [ ] With a required primary weapon available, let an unequipped pawn receive an allowed work or hauling job; verify exactly one **Preparing** worker entry and one Equip transition, then exact-job resumption with no growing queue, prolonged **Standing**, or `10 jobs in one tick` warning.
- [ ] Require two or more apparel pieces plus a primary weapon, and make the apparel changes take longer than the weapon retry interval. Verify the selected weapon is not rejected before its queued Equip actually starts and preparation completes without cycling through locker weapons.
- [ ] Give an unequipped pawn a haul whose source item is itself eligible as required work apparel or a required work weapon. Verify AOM never wears or equips the pending haul target (including queued targets), selects different locker stock when available, and resumes the haul without an `isn't spawned` carry error.
- [ ] Observe a haul with a visible delay toil; verify the advancing delay is not mistaken for finished work and the pawn completes the original haul before restoration.
- [ ] While a pawn is returning or restoring after a haul, verify it appears only under **Workers** and is not duplicated under **Haulers**.
- [ ] Place an assigned bed inside an active work area; verify the pawn equips every requirement, reaches the bed, and sleeps in the complete managed outfit without repeated **LayDown**, restoration, or Standing loops.
- [ ] Place an assigned bed outside an active work area so the pawn's initial route crosses the area but locker preparation moves the pawn onto a clear route. Verify the valid **LayDown** continuation survives preparation, AOM restores personal gear once if the route is now clear, and the pawn reaches the bed without repeating equip/cancel/restore cycles.
- [ ] Exhaust one required apparel or weapon type while an assigned bed remains inside the area; verify AOM first searches for the complete set, then allows the essential native **LayDown** only during a recorded unavailable-gear window. The pawn must sleep instead of repeating boundary/recheck jobs, and strict preparation must resume after stock becomes reachable again.
- [ ] Exhaust one required apparel or weapon type while an awake pawn without the complete set is already inside the work area. Verify AOM discards the blocked work target, sends the pawn once to the locker or nearest safe exterior cell, and then allows unrelated native work outside the area. The pawn must not work unequipped, re-enter on the internal egress job, or repeat **Standing** / occupied-area gear rechecks.
- [ ] With work paused, verify jobs targeted beyond the area do not create a walk-to-boundary/cancel/reselect loop when their shortest route crosses it.
- [ ] Disable hauling while leaving ordinary work enabled, then place a construction blueprint inside the area; verify a constructor can deliver resources and complete it through the **Construction** work givers, while general haulers do not assist through the separate **Hauling** work givers. Verify there is no `CanGiveJob`/`JobOnX` synchronization error and no pawn standing on a rejected hauling-assistance job.
- [ ] Disable hauling while leaving ordinary work enabled, then have a colonist perform Warden jobs inside the area: deliver food, feed a prisoner, chat/recruit/convert/enslave, escort or release a prisoner, and suppress/emancipate a slave where the active DLC permits. Verify item-carrying Warden jobs remain under **Workers**, prepare once, resume their exact native target, and are not blocked by the hauling toggle.
- [ ] Repeat representative Warden jobs with both prisoner and slave targets. Verify access is controlled by the acting warden's pawn-category **Work** permission, not by the target's category; if another mod permits a slave or prisoner to perform Warden work, verify that actor's own **Work** permission is authoritative.
- [ ] With Biotech or Anomaly active, smoke-test available Warden interactions such as hemogen delivery, interrogation, or activity suppression inside the area; verify native custody behavior resumes after outfit preparation without a Standing, reservation, or repeated preparation loop.
- [ ] With hauling disabled and several prohibited haul candidates available, verify three restored pawns skip each rejected concrete target, promptly choose other native work or recreation, and do not remain **Standing** between repeated three-second waits.
- [ ] While a pawn has an active valid work session, let RimWorld propose prohibited hauling or other work; verify only that exact candidate is skipped, the outfit session remains active, and the pawn does not restore and immediately re-equip for the same bill.
- [ ] Place construction or hauling work outside and beyond a disabled-access work area while leaving an exterior route available; verify pawns keep the native job, route around the area, and complete it without equipping work gear or remaining **Standing**.
- [ ] Complete arbitrary buffered work on the far side of an active area from its locker; verify automatic buffer completion returns each pawn through the protected route with the complete outfit, reaches the locker, restores saved gear, and never settles into repeated **Returning to locker room** / **Standing** recovery.
- [ ] Complete a natural task-buffer return that restores multiple apparel pieces without pressing **Recall**. If an exact AOM-owned `Wear`, `RemoveApparel`, or managed-apparel haul route touches the protected boundary, verify the transition is allowed once, the personal outfit is fully restored, and no boundary-stop / occupied-area gear-recheck cycle occurs.
- [ ] With short cleaning, hauling, eating, and sleep jobs repeatedly alternating across a ship work-area boundary, let a natural task buffer finish. Verify the pawn pauses once at the locker before Phase 3; if fresh protected activity becomes available during that pause, the complete managed outfit is retained without a restore/re-equip storm. Verify explicit **Recall**, drafting, danger, and map departure do not wait for this debounce.
- [ ] During that restoration, route an exact AOM-owned saved-weapon `Equip` or managed-weapon `DropEquipment` through a protected boundary. Verify the periodic occupancy audit and path boundary both leave the assigned transition intact until it completes.
- [ ] While one rule is still preparing, let RimWorld replace its pending job with a concrete job whose route enters a different protected rule. Verify AOM discards the stale continuation once, retargets the complete combined requirement to the occupied rule, and never repeats the same boundary stop or occupied-area recheck.
- [ ] Finish natural saved-outfit restoration while RimWorld proposes a safe unrelated job outside every protected area; verify that proposal starts directly after the snapshot clears instead of being replaced by a wait/reselection that immediately re-enters the former area.
- [ ] Start work, hauling, and wandering routes outside the area, change the map so each route recalculates through it, and verify the actual boundary reapplies the corresponding access toggle even when the pawn already wears the complete required set.
- [ ] Repaint protected areas repeatedly, including moving cells without changing the total cell count; verify routes reflect the new shape after the short fingerprint refresh window and process memory does not grow continuously from obsolete path grids.
- [ ] Allow a category, remove one required item after its route begins, and force a recalculation through the area; verify the pawn cannot enter until the complete set is restored.
- [ ] Let an unequipped pawn carry food outside a protected area while a table or dining spot inside is selected only after `Ingest` begins. Verify the first actual boundary discovery is remembered, the exact eating job prepares once and resumes with the held food, and the pawn neither enters unequipped nor repeats boundary stops.
- [ ] Let an unequipped pawn begin **SocialRelax** at a protected gathering table, including a multi-cell chair or seat whose concrete sitting cell is finalized after `StartJob`. Verify the running-job audit detects the final protected target, prepares the complete outfit once, and does not allow social relaxation to continue inside the Work Area without its gear.
- [ ] Enable guest access and provide complete reachable managed gear for a hosted visitor. Verify AOM's pre-assignment search can reserve and assign that stock, the visitor prepares once and enters, while unrelated native guest jobs still cannot reserve, wear, haul, or process managed gear.
- [ ] Disable guest Work, Hauling, and Wandering access, then let a hosted visitor's duty propose generic `Goto` and `Wait_MaintainPosture` jobs in the area. Verify the visitor is redirected outside without an outfit snapshot or managed-stock reservation, while emergency behavior and natural map departure remain native.
- [x] With colonist hauling allowed, pause a rule while a pawn is hauling into its area; the pawn finishes without repeatedly changing outfits or restarting the same haul. Verified in-game on 2026-08-23 with RC DLL `687AD839C5B766331FAB4C4D8B0D9CEA1E53490194CF02D5A85AE4ACBC203A81`.
- [ ] Verify work, hauling, wandering, child-watching, and pawn-category permissions independently.
- [ ] Trigger a prison break from a cell inside an active work area; verify the escaping prisoner can take a native `Equip`, combat, and escape route without AOM rechecking gear, blocking a boundary cell, creating an outfit state, or leaving the pawn **Standing**.
- [ ] With prisoner wandering disabled, let a prisoner's native `GotoWander` reach the protected-area boundary; verify the pawn receives one bounded custody-safe wait, never enters the prohibited cell, and does not emit `IdleError`.
- [ ] Start preparation while the preferred required apparel is burning; verify AOM selects another safe item or yields before one bounded retry, with no repeated `Wear` or `10 jobs in one tick` warning.
- [ ] During an active outfit session, expose a colonist or slave to danger so RimWorld selects `FleeAndCower`; verify the emergency job does not consume a task-buffer slot or start restoration. Draft and undraft that pawn; verify the interrupted civilian continuation is cancelled, drafted orders remain native, saved-outfit restoration resumes after undrafting, and the next save has no unresolved destination/attack reservation warning.
- [ ] On a safe map, draft several equipped workers while AOM apparel/weapon jobs are queued, then undraft them outside their configured locker. Verify no managed item is removed or returned while drafted, the task buffer restarts at zero and counts the configured follow-up work, hauling, and ordinary duties, every pawn keeps the complete work outfit until reaching a distinct usable locker cell, and saved-outfit restoration begins only after the buffer finishes and the pawn reaches the locker. Repeat after taking the drafted workers to another map while their gravship locker remains behind; on undraft they must retain the complete work outfit, count and perform their buffered native activity without an in-place removal attempt, and restore only after returning to a map with a reachable safe locker/exterior cell.
- [ ] Repeat the draft/undraft buffer test on an underground or pocket map whose locker room is on the parent gravship map. After the buffer completes (or the pawn becomes idle), verify the pawn uses the native exit portal, retains the complete work outfit during transfer, reaches the locker room, and restores there.
- [ ] Confirm underground loot hauling remains native: only items added to RimWorld's portal load list are hauled through the portal; AOM must not autonomously select or claim unrelated loot.
- [ ] Confirm drafted and explicit player-forced behavior is respected.

### RC-09: locker storage

- [ ] Verify locker-preferred selection with map-wide fallback.
- [ ] Verify managed/non-managed apparel and weapon filters, restocking, retained cyan stock, and **Forget** blockers.
- [ ] Set narrow apparel and weapon Condition/Quality ranges. Verify out-of-range work gear is excluded from availability and cannot satisfy the rule, gear without a quality level remains eligible, changing a range safely recalls affected workers, and saved personal restoration remains independent of these work-gear ranges.
- [ ] Verify temporary items return to the locker when configured.
- [ ] Draw a locker that overlaps the work area; verify AOM chooses a safe locker or exterior cell and never restores a required item while still inside.

### RC-10: compatibility smoke test

- [x] Repeat weapon equip, explicit weapon override, restoration, and save/load with Simple Sidearms enabled. Verified in-game on 2026-08-30 with RC DLL `F48DE859FFB22CE937AFE9C0DACBA2E2DA900F5505242257519B5BE16AF48AF4`.
- [x] Confirm AOM does not rewrite Simple Sidearms memories or retain a temporary primary as a sidearm. Verified in the same session: AOM yielded to Jonah's external weapon switch, continued work with that protected choice, recovered its temporary managed weapon from inventory or its holder, restored the exact saved outfit and primary, cleared the snapshot, and rejected later automatic `ReequipSecondary` attempts for managed stock.
- [ ] Under a very low RimWorld maintenance-cost setting, empty a player autocannon and uranium slug turret on a gravship. Verify the native right-click rearm option and automatic rearm job both become available, use RimWorld's normal steel/uranium costs and Refuel work, still prepare required AOM gear before protected entry, and do not affect other refuelable buildings or turret types.
- [ ] With a content mod that can briefly register destroyed or zero-stack haulables, verify opportunistic hauling skips those invalid things without an exception and continues selecting valid haul targets.

## Hidden Workshop validation

- [x] Upload the staged folder as hidden and accept the Steam Workshop legal agreement if prompted.
- [x] Preserve the new `About/PublishedFileId.txt` in the repository before any update upload.
- [x] Add Harmony as a required Workshop item.
- [x] Apply the prepared title, description, tags, preview, screenshots, and `0.3.2` change note from `WORKSHOP.md`. Verified on 2026-08-30: Steam displays the prepared title, description, preview, and screenshot gallery; the RimWorld owner UI exposes the applicable `Mod` and `1.6` tags; and the initial upload note was replaced with the exact prepared `0.3.2` change note while the item remained Hidden.
- [x] Test the Steam-downloaded copy with the local development junction disabled so it cannot mask packaging problems. Verified on 2026-08-30 by loading `GooseButter_03_ALL_MODS` with Workshop DLL `168EA71AAF95416805CCB5D247F367B9514323C7771CA9A81A74E6E052B252C9`; AOM 0.3.2 loaded four active snapshots, resumed restoration and buffer state, enforced the `Radiation Zone` boundary, and produced no AOM exception or repeating warning in the supplied session log.
- [x] Confirm dependency messaging, load order, title, icon, preview, and description display correctly.
- [x] Make the item public only after the downloaded-copy smoke test passes. Published on 2026-08-30 as Workshop item `3792731788`: https://steamcommunity.com/sharedfiles/filedetails/?id=3792731788.

## Release closeout

- [x] Merge the release-prep branch to `main`. Fast-forwarded `main` to release commit `790e5473e900f0476b353a111ccbaa832295f8cf` on 2026-08-30 without modifying the separate dirty live checkout.
- [x] Tag the verified commit as `v0.3.2`. The annotated tag points to `790e5473e900f0476b353a111ccbaa832295f8cf`.
- [x] Publish matching GitHub release notes and attach the staged package if desired. Published https://github.com/tekmojo/AutomaticOutfitManager/releases/tag/v0.3.2 with `AutomaticOutfitManager-0.3.2.zip` (SHA-256 `E56B14670D57432B1F43B738C1A72041E9731D58F39020A7993EAEE044E16A6B`).
- [x] Record the final commit, DLL SHA-256, Workshop ID, and public URL in the handoff state. Release commit `790e5473e900f0476b353a111ccbaa832295f8cf`; DLL `168EA71AAF95416805CCB5D247F367B9514323C7771CA9A81A74E6E052B252C9`; Workshop item `3792731788`; https://steamcommunity.com/sharedfiles/filedetails/?id=3792731788.
- [x] License the original Automatic Outfit Manager software and associated documentation under MIT, copyright 2026 tekmojo, while preserving RimWorld/Ludeon, Harmony, trademark, and game-derived material notices separately. Added on 2026-08-30 for future source and package updates without rewriting the published `v0.3.2` tag.
