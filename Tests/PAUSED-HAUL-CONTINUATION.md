# Paused hauling continuation — 2026-09-07

User authorized fixes and local deployment after the urh65lrs79 gravship pause audit. Preserve allowed hauling to retrieve protected stock for outbound deliveries, including supplies intended for another map, with complete PPE. Pause must still block new ordinary activities.

## Evidence and scope

The supplied log recorded 20 HaulToCell preparation starts and 20 returns labeled Work paused (15 while Preparing). Lumi's first Wear completed while the remaining helmet and weapon were queued, but recall redirected the continuation into restoration. The old return label alone does not identify the caller that set RecallRequested. These fixes reproduce source-level failures with native-shaped callback ordering; a game replay remains required to verify the observed sequence end to end.

The previous pause watchdog recomputed haul relevance from the current pawn position and live pickup target. Traveling to a locker or carrying the source outside the rule can remove that relevance while the original haul still owns the outfit. Separately, a native connective wait carrying work-giver context could be rejected by DeniedActivityRule before the later haul-preservation check. The regression fixture fails independently with either previous decision.

Ocag's safe shortage exit also qualified as protected transit through the area being exited. Transit admission now consults the same directional shortage-exit decision already used by the runtime and path guards. Other areas and reentry still require protection.

## Contract

- Preserve a paused rule's existing, allowed haul through its exact assigned outfit steps and targetless native connective waits. A captured haul remains valid through both Preparing and Active. Native queued finalizers keep the exact prepared haul; admission transfers ownership out of the deep-saved pending field.
- Qualify a new haul using actual protected targets or a route that cannot avoid the area. An avoidable shortcut between exterior stock and locker storage must not request PPE. Both legs of material delivery use the hauling route policy.
- Use the session's tracked rule IDs rather than rediscovering ownership from a changing route. This grants no new path or PPE exemption and creates no extra serialized Job owner.
- The current RC extends the construction-only exception to native HaulToCell/HaulToContainer item transport, with no material-type whitelist. All recipients using the additional exception must be outside the paused rule on the same map, including their footprints and queues. Work and Non-Work source rules retain their own hauling, child, and outfit requirements. Ordinary permitted interior storage hauling is unchanged. Construction recipients inside a paused rule remain blocked.
- Combined supply-use jobs remain activities. Bills, refueling/rearming, repairs, meals, Wear/Equip and RemoveApparel do not inherit a transport exception merely from a Hauling work-priority tag. This does not split those jobs into new hauls or create cross-map logistics. See [SUPPLY-HAULING.md](SUPPLY-HAULING.md) for the expansion and verification.
- Explicit Recall, hauling revocation, child access, PPE capability, map changes, draft/departure, existing target viability checks, and normal preparation recovery remain authoritative.
- A new ordinary activity cannot inherit the exception. A successful permitted haul may keep its rule's outfit through a native connective wait for at most 120 ticks while buffer allowance remains. Failed jobs and waits cannot refresh that grace. It is transient, bound to its rule, and expires without awarding task credit. Existing successful-task counters remain authoritative.
- Detailed logging records the first recall requester, transition, rule, current job and captured job. No extra player tooltip text.

## Verification

Build passed using build.ps1 against the local RimWorld install; git diff --check passed.

After RJPH8MgiwH, 1,107 checks passed across paused hauling (89), preparation handoff (134), pause behavior (69), Non-Work (178), area permission (295), child access (30), access exit (20), activity classification (125), observed transition (29), boundary admission (42), managed-work candidates (48), and session-audit fixes (48). The expanded fixture executes production pause decisions, actual hauling classification, the full pending-job viability method, preparation-complete/resume decisions, ownership transfer, and the PreparationJobHandoff registry/Harmony patches with the native-shaped queue/finalizer body. Path search and game objects remain controlled fixtures; this is not an in-game replay.

Nine independent negative controls reproduce route-context loss, connective-wait recall, shortage-egress reentry, avoidable shortcuts, Active pending-handoff recall, viability cancellation, missing Active replay, missing bounded grace, and blocked typed material pickup. Five existing pause negative controls also fail as expected.

## Next game replay

1. Keep USS Pissant Activities paused and Hauling allowed. Request native stock delivery out of the area. With missing PPE, verify one preparation sequence, then the original haul and delivery.
2. Follow Lumi/Bowman through suit, helmet and weapon steps and through pickup. There should be no repeated pause recall/reprepare cycle. Buffer credits must still follow successful tasks only.
3. While the rule remains paused, verify no new autonomous cooking, eating, recreation or repair there. Explicit orders and essential care retain existing overrides.
4. Revoke Hauling or use Recall during preparation and active hauling; verify safe return without repeated queue rebuilds. Reenable only when ready to repeat.
5. For unavailable gear inside an active Non-Work rule, verify the pawn finishes its safe exit without outfitting merely for that exit. It must not gain an unrelated area shortcut.
6. Haul exterior apparel to exterior locker storage while the ship is paused. Verify the pawn takes the available exterior route without starting ship PPE preparation.
7. Request components from inside the ship for an exterior cooler blueprint/frame. Verify one PPE preparation and completed delivery; repeat with an interior recipient and with Hauling disabled to confirm those cases remain blocked.
8. Observe the first native wait after delivery. A remaining task buffer may preserve PPE briefly; completed/failed/wait jobs must not invent credit or hold the outfit indefinitely.

The supply expansion is now deployed locally following explicit user approval. RimWorld was closed; candidate, live repository, and installed junction DLL SHA-256 all match `0D58D4850FC4F525E3E328E4E47046C6DE2346B9A43ADC5F65256730E18EFB31`. The game was not launched by the agent. No source synchronization, commit, push, Workshop update or release publication. Live gameplay validation remains pending.
