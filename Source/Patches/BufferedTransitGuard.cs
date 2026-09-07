using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.State;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    internal static class BufferedTransitGuard
    {
        private static readonly AccessTools.FieldRef<Pawn_PathFollower, PathEndMode> EndMode =
            AccessTools.FieldRefAccess<Pawn_PathFollower, PathEndMode>("peMode");
        private static readonly Dictionary<Pawn, int> RepathedJobs = new Dictionary<Pawn, int>();
        internal static void Reset() => RepathedJobs.Clear();

        internal static bool BlockUnnecessaryEntry(Pawn pawn, Job job, IntVec3 next)
        {
            var state = AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn);
            if (job == null || job.playerForced || pawn.Drafted || pawn.Downed || pawn.InMentalState ||
                PawnJobTracker_StartJob_Patch.IsNativeEmergencySafetyJob(job) ||
                PawnJobTracker_StartJob_Patch.IsMapDepartureJob(job) ||
                PawnPathFollower_ProtectedArea_Patch.IsManagedTransitionJob(pawn, job, state)) return false;
            bool buffered = NonWorkBufferTracker.For(pawn) != null ||
                (state?.Transition == ApparelTransition.Active && !state.RecallRequested &&
                 (state.PendingBufferedJobLoadId == job.loadID ||
                  AutomaticOutfitManagerGameComponent.Current.RuleById(state.ActiveRuleId)?.ReturnTaskBuffer > state.BufferedTasksCompleted ||
                  state.NestedRuleBuffers.Any(item => item != null && !item.Finished &&
                      AutomaticOutfitManagerGameComponent.Current.RuleById(item.RuleId)?.ReturnTaskBuffer > item.Completed)));
            if (!buffered) return false;
            var destination = pawn.pather.Destination;
            if (!destination.IsValid || !destination.Cell.InBounds(pawn.Map)) return false;
            var rules = RuleEvaluator.EnabledRulesForMap(pawn.Map);
            // Late-bound dining seats and native job destinations are real task
            // targets too. Egress is always allowed; only unrelated entry is checked.
            bool crossing = rules.Any(rule => !rule.IsNonWork && rule.Area[next] &&
                !rule.Area[pawn.Position] && !rule.Area[destination.Cell] &&
                !RuleEvaluator.JobTargetsArea(job, rule.Area));
            if (!crossing) return false;
            var avoid = rules.Where(rule => !rule.Area[pawn.Position] &&
                !rule.Area[destination.Cell] && !RuleEvaluator.JobTargetsArea(job, rule.Area)).ToList();
            if (!ProtectedPathAvoidance.SegmentAvoidsRules(pawn, pawn.Position, destination, avoid, null, EndMode(pawn.pather)))
            {
                // Another area can be unavoidable without making this particular
                // entry necessary. Check the actual boundary independently too.
                avoid = avoid.Where(rule => rule.Area[next]).ToList();
                if (!ProtectedPathAvoidance.SegmentAvoidsRules(pawn, pawn.Position, destination,
                        avoid, null, EndMode(pawn.pather)))
                    return false; // Necessary transit still requires normal access and gear.
            }
            if (!RepathedJobs.TryGetValue(pawn, out int previous) || previous != job.loadID)
            {
                RepathedJobs[pawn] = job.loadID;
                PathEndMode mode = EndMode(pawn.pather);
                pawn.pather.StopDead();
                pawn.pather.StartPath(destination, mode);
                if (AomLog.DetailedEnabled)
                    AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: rerouting buffered {job.def.defName} around an unnecessary Work Area; outfit retained.");
            }
            else
            {
                // A compatibility pathfinder may ignore the avoidance grid.
                // Do not retry forever, consume credit, or equip for its shortcut.
                foreach (var rule in avoid.Where(rule => rule.Area[next]))
                    UnavailableWorkRegistry.Block(pawn, rule, job);
                if (AomLog.DetailedEnabled)
                    AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: buffered route still crossed an unnecessary Work Area after rerouting; cancelled {job.def.defName} without buffer credit.");
                pawn.jobs.EndCurrentJob(JobCondition.Incompletable, false, true);
            }
            return true;
        }
    }
}
