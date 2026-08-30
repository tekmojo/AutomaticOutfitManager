using System.Collections.Generic;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    /// <summary>
    /// Rechecks the cell RimWorld is actually about to enter. A job's route can
    /// change after StartJob (doors, reservations, congestion, or a modded
    /// pathfinder), so the initial protected-transit prediction is not enough
    /// to guarantee that an unequipped pawn never crosses a managed work area.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_PathFollower), "TryEnterNextPathCell")]
    public static class PawnPathFollower_ProtectedArea_Patch
    {
        private static readonly Dictionary<int, int> LastBlockedLogTick =
            new Dictionary<int, int>();
        private static readonly AccessTools.FieldRef<Pawn_PathFollower, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_PathFollower, Pawn>("pawn");
        private static readonly AccessTools.FieldRef<Pawn_PathFollower, IntVec3> NextCellField =
            AccessTools.FieldRefAccess<Pawn_PathFollower, IntVec3>("nextCell");

        public static bool Prefix(Pawn_PathFollower __instance)
        {
            Pawn pawn = PawnField(__instance);
            if (pawn?.Map == null || pawn.Drafted || pawn.jobs?.curJob == null)
            {
                return true;
            }

            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            IReadOnlyList<ApparelRule> rules =
                RuleEvaluator.EnabledRulesForMap(pawn.Map);
            if (component == null || rules.Count == 0 ||
                !PawnAccessClassifier.IsApparelEligibleHuman(pawn))
            {
                return true;
            }

            Job currentJob = pawn.jobs.curJob;
            if (PawnJobTracker_StartJob_Patch
                .IsNativeEmergencySafetyJob(currentJob))
            {
                return true;
            }
            PawnApparelState state = component.StateFor(pawn);

            // StartJob already forces an active AOM session through Phase 3
            // before allowing a native departure. Once that snapshot has been
            // cleared, the retried exit Goto must be allowed to cross the ship
            // instead of being treated as fresh protected-area activity. The
            // Pawn.ExitMap safeguard still returns any managed gear if a modded
            // departure bypassed the ordinary restoration path.
            if (state == null && PawnJobTracker_StartJob_Patch
                    .IsMapDepartureJob(currentJob))
            {
                return true;
            }

            // Preparation and restoration can legitimately route a pawn through
            // the protected area to reach assigned work or saved apparel.
            // StartJob has already recorded these exact transition targets, so
            // exempt only those operations rather than broadly allowing
            // stateful pawns.
            if (IsManagedApparelTransition(pawn, currentJob, state) ||
                PawnJobTracker_StartJob_Patch
                    .IsAssignedTransitionWeaponJob(state, currentJob))
                return true;

            IntVec3 nextCell = NextCellField(__instance);
            if (!nextCell.IsValid || !nextCell.InBounds(pawn.Map))
                return true;

            ApparelRule rule = null;
            bool blockedByActivity = false;
            foreach (ApparelRule candidate in rules)
            {
                if (!candidate.Area[nextCell])
                    continue;

                // Recheck both category access and the complete requirement
                // at the actual boundary. Routes can change after StartJob,
                // and an already-equipped pawn must not use that reroute to
                // bypass disabled work, hauling, or wandering access.
                bool activityAllowed =
                    PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(
                        pawn, currentJob, candidate);
                bool missingRequiredGear =
                    RuleEvaluator.HasMissingRequiredGear(pawn, candidate);
                bool allowedUnavailablePrisonerFallback =
                    activityAllowed && missingRequiredGear &&
                    PawnJobTracker_StartJob_Patch
                        .IsNativePrisonerUnavailableGearFallbackJobFamily(
                            pawn, currentJob) &&
                    UnavailableWorkRegistry.HasActiveRuleBlock(pawn, candidate);
                bool allowedUnavailableEssentialFallback =
                    activityAllowed && missingRequiredGear &&
                    PausedAreaWorkFilter.IsEssentialPersonalJob(currentJob) &&
                    UnavailableWorkRegistry.HasActiveRuleBlock(pawn, candidate);
                bool allowedManagedIncompatibleIngestFallback =
                    activityAllowed && missingRequiredGear &&
                    PawnJobTracker_StartJob_Patch
                        .IsManagedIncompatibleIngestFallback(
                            pawn, state, currentJob, candidate, nextCell);
                bool allowedUnavailableGearEgress =
                    PawnJobTracker_StartJob_Patch
                        .IsUnavailableGearEgressJob(
                            pawn, currentJob, candidate);
                bool blocked = !allowedUnavailableGearEgress &&
                    (!activityAllowed ||
                      (missingRequiredGear &&
                       !allowedUnavailablePrisonerFallback &&
                       !allowedUnavailableEssentialFallback &&
                       !allowedManagedIncompatibleIngestFallback));
                if (blocked)
                {
                    rule = candidate;
                    blockedByActivity = !activityAllowed;
                    break;
                }
            }
            if (rule == null)
                return true;

            if (AomLog.DetailedEnabled)
            {
                int tick = Find.TickManager?.TicksGame ?? 0;
                if (!LastBlockedLogTick.TryGetValue(pawn.thingIDNumber, out int lastTick) ||
                    tick - lastTick >= 600)
                {
                    LastBlockedLogTick[pawn.thingIDNumber] = tick;
                    string reason = blockedByActivity
                        ? "because the rule does not permit this activity"
                        : "without its required apparel or weapon";
                    AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: stopped before entering '{rule.Name}' {reason}; reconsidering {currentJob.def.defName}.");
                }
            }

            // End the candidate before it enters the protected cell. RimWorld's
            // normal think tree will select it (or another useful job) again;
            // StartJob then queues the required apparel using the now-current
            // route. Avoid substituting Wait here, which can strand pawns at a
            // doorway when their original task remains the best available job.
            // Paused work must not retain a queued continuation. Otherwise
            // RimWorld can immediately restart the same blocked Goto and invoke
            // this path guard again indefinitely. Unpaused missing-gear jobs keep
            // their queue so the apparel intervention can resume the real task.
            if (rule.WorkAreaPaused)
                pawn.jobs.ClearQueuedJobs(false);

            if (!blockedByActivity)
            {
                // Some native drivers choose their real destination after
                // StartJob. Record the concrete job that exposed this boundary
                // so its next native retry can prepare instead of repeating the
                // same stop forever. Activity denials are intentionally omitted:
                // wearing gear cannot make a prohibited activity legal.
                ProtectedBoundaryRetryRegistry.Record(pawn, currentJob, rule);
            }

            if (blockedByActivity &&
                PawnAccessClassifier.IsHostedGuest(pawn))
            {
                // Ingest and several recreation drivers choose their final
                // dining/interaction cell only after StartJob. If that late
                // destination reaches a guest-disabled boundary, ending the job
                // alone lets the thinker select the same activity and route on
                // the next tick. Move an inside guest out when possible, or
                // yield safely in place when they are already outside.
                pawn.jobs.ClearQueuedJobs(false);
                Job safeGuestJob;
                if (!PausedAreaWorkFilter.TryMakeWanderingExitJob(
                        pawn, out safeGuestJob))
                {
                    safeGuestJob =
                        PawnJobTracker_StartJob_Patch.MakeSafeWaitJob(
                            pawn, 180);
                }
                pawn.jobs.StartJob(
                    safeGuestJob, JobCondition.InterruptForced,
                    null, false, true);
                return false;
            }

            if (blockedByActivity &&
                PawnAccessClassifier.IsColonyPrisoner(pawn) &&
                PawnJobTracker_StartJob_Patch
                    .IsNativePrisonerUnavailableGearFallbackJobFamily(
                        pawn, currentJob))
            {
                // A prisoner's native think tree can have no second roaming
                // choice after its GotoWander is rejected at an area boundary.
                // Starting a fixed prisoner-safe posture wait avoids IdleError
                // without allowing the prohibited cell or synchronously asking
                // the thinker for the same route again.
                pawn.jobs.ClearQueuedJobs(false);
                Job safeWait = PawnJobTracker_StartJob_Patch.MakeSafeWaitJob(
                    pawn, 120);
                pawn.jobs.StartJob(
                    safeWait, JobCondition.InterruptForced,
                    null, false, true);
                return false;
            }

            // Do not synchronously select another job from inside the path-cell
            // callback. If the thinker returns the same candidate, recursive
            // EndCurrentJob calls can produce hundreds of retries in one tick.
            // Leaving selection to the next job-tracker tick gives StartJob a
            // clean opportunity to prepare every rule crossed by the new path.
            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, false, true);
            return false;
        }

        private static bool IsManagedApparelTransition(
            Pawn pawn, Job currentJob, PawnApparelState state)
        {
            if (pawn?.Map == null || currentJob?.def == null || state == null)
            {
                return false;
            }

            // A preparation Wear job is the narrowly assigned operation that
            // resolves the missing-gear condition. Blocking it at the boundary
            // can create a circular need-gear-to-reach-gear retry and repeatedly
            // enqueue the same intercepted work. Exempt only the exact apparel
            // recorded for this pawn's current transition.
            if (state.Transition == ApparelTransition.Preparing &&
                currentJob.def == JobDefOf.Wear &&
                currentJob.targetA.Thing is Apparel workApparel)
            {
                return state.IsPreparationApparel(workApparel);
            }

            // Weapon preparation has the same circular-access risk as apparel:
            // the selected primary may be stored inside (or reached through) the
            // protected area whose requirement it satisfies. Exempt only the
            // exact weapon recorded for this transition, never an arbitrary
            // automatic or player equipment job.
            if (state.Transition == ApparelTransition.Preparing &&
                currentJob.def == JobDefOf.Equip &&
                currentJob.targetA.Thing is ThingWithComps workWeapon)
            {
                return state.ManagedWeapons?.Contains(workWeapon) == true;
            }

            if (PawnJobTracker_StartJob_Patch.IsAssignedChangingAreaReturnJob(
                    state, currentJob))
            {
                // Only AOM records this return destination. The exact Goto may
                // target the preferred locker or the nearest safe exterior cell
                // when no locker exists or the locker overlaps the work area.
                // Never make that transition fight the boundary guard that
                // sent the pawn home. A requirement edit can make the currently
                // worn managed set noncompliant while this exact return is in
                // flight; blocking it here only restarts the same return forever
                // and prevents the pawn from reaching the gear that can resolve
                // the shortage. Ordinary Goto and native jobs remain guarded.
                return true;
            }

            if (state.Transition != ApparelTransition.Restoring ||
                currentJob.targetA.Thing is not Apparel apparel)
            {
                return false;
            }

            if (currentJob.def == JobDefOf.Wear)
                return state.OriginalApparel?.Contains(apparel) == true;

            if (currentJob.def == JobDefOf.RemoveApparel)
                return state.ManagedApparel?.Contains(apparel) == true;

            return (currentJob.def == JobDefOf.HaulToCell ||
                    currentJob.def == JobDefOf.HaulToContainer) &&
                   state.ManagedApparel?.Contains(apparel) == true;
        }

        internal static void ResetRuntimeCache() => LastBlockedLogTick.Clear();
    }
}
