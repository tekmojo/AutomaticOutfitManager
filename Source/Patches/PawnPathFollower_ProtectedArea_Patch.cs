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
            if (pawn?.Map == null || pawn.Drafted ||
                !PawnAccessClassifier.IsApparelEligibleHuman(pawn) ||
                pawn.jobs?.curJob == null)
            {
                return true;
            }

            Job currentJob = pawn.jobs.curJob;
            PawnApparelState state = AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn);
            bool essentialPersonalJob =
                PausedAreaWorkFilter.IsEssentialPersonalJob(currentJob);

            // Preparation and recall can legitimately route a pawn through the
            // protected area to reach assigned work or saved apparel. StartJob
            // has already recorded these exact transition targets, so exempt
            // only those operations rather than broadly allowing stateful pawns.
            if (IsManagedApparelTransition(pawn, currentJob, state))
                return true;

            IntVec3 nextCell = NextCellField(__instance);
            if (!nextCell.IsValid || !nextCell.InBounds(pawn.Map))
                return true;

            ApparelRule rule = null;
            var rules = AutomaticOutfitManagerGameComponent.Current?.Rules;
            if (rules != null)
            {
                foreach (ApparelRule candidate in rules)
                {
                    if (candidate == null || !candidate.Enabled ||
                        candidate.Area?.Map != pawn.Map || !candidate.Area[nextCell])
                        continue;

                    // Job type never exempts an active area. Sleeping, eating,
                    // recreation, hauling, wandering, and pass-through all use
                    // the same complete-gear gate. Paused-area access remains
                    // governed by its existing activity permissions.
                    bool blocked = candidate.WorkAreaPaused
                        ? essentialPersonalJob
                            ? RuleEvaluator.HasMissingRequiredGear(pawn, candidate)
                            : !PausedAreaWorkFilter.JobMayEnterPausedRule(
                                pawn, currentJob, candidate)
                        : RuleEvaluator.HasMissingRequiredGear(pawn, candidate);
                    if (blocked)
                    {
                        rule = candidate;
                        break;
                    }
                }
            }
            if (rule == null)
                return true;

            if (Prefs.DevMode)
            {
                int tick = Find.TickManager?.TicksGame ?? 0;
                if (!LastBlockedLogTick.TryGetValue(pawn.thingIDNumber, out int lastTick) ||
                    tick - lastTick >= 600)
                {
                    LastBlockedLogTick[pawn.thingIDNumber] = tick;
                    string reason = rule.WorkAreaPaused
                        ? "while work is paused"
                        : "without its required apparel or weapon";
                    Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: stopped before entering '{rule.Name}' {reason}; reconsidering {currentJob.def.defName}.");
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
                return state.ManagedApparel?.Contains(workApparel) == true;
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

            if (state.RecallRequested != true)
                return false;

            if (state.Transition == ApparelTransition.ReturningToChangingArea &&
                currentJob.def == JobDefOf.Goto)
            {
                // Only AOM sets ReturningToChangingArea. The exact Goto may
                // target the preferred locker or the nearest safe exterior cell
                // when no locker exists or the locker overlaps the work area.
                // It remains exempt only while the full session requirement is
                // still equipped; losing one piece mid-return must stop the next
                // protected cell and re-enter ordinary preparation recovery.
                var component = AutomaticOutfitManagerGameComponent.Current;
                ApparelRule activeRule = component?.RuleById(state.ActiveRuleId);
                if (activeRule != null &&
                    RuleEvaluator.HasMissingRequiredGear(pawn, activeRule))
                {
                    return false;
                }
                foreach (string ruleId in state.CurrentRuleIds ??
                             new List<string>())
                {
                    ApparelRule rule = component?.RuleById(ruleId);
                    if (rule != null &&
                        RuleEvaluator.HasMissingRequiredGear(pawn, rule))
                    {
                        return false;
                    }
                }
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
    }
}
