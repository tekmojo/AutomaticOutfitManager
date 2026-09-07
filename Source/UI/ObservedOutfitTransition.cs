using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.UI
{
    // Presentation only: visiting an area for an assigned outfit step does not
    // make that area an owner of the session, its buffer, or its Recall action.
    internal static class ObservedOutfitTransition
    {
        internal static string Build(Pawn pawn, PawnApparelState state, Job job, ApparelRule areaRule)
        {
            if (pawn?.Map == null || pawn.RaceProps?.Humanlike != true ||
                pawn.RaceProps.Animal || pawn.apparel == null || pawn.Destroyed ||
                pawn.Drafted || pawn.Downed || state?.Pawn != pawn ||
                state.Transition == ApparelTransition.Active || job?.def == null ||
                job != pawn.CurJob || areaRule?.Area?.Map != pawn.Map)
                return null;

            bool assigned = PawnJobTracker_StartJob_Patch.IsAssignedTransitionApparelJob(state, job) ||
                PawnJobTracker_StartJob_Patch.IsAssignedTransitionWeaponJob(state, job) ||
                PawnJobTracker_StartJob_Patch.IsAssignedChangingAreaReturnJob(state, job);
            if (!assigned || job.targetA.Thing?.Destroyed == true)
                return null;

            bool inside = pawn.Position.IsValid && pawn.Position.InBounds(pawn.Map) &&
                areaRule.Area[pawn.Position];
            if (!inside && !RuleEvaluator.JobTargetsArea(job, areaRule.Area))
                return null;

            // Use the owning session's status, not the visited area's buffer or
            // Non-Work destination status. Existing status caching bounds work.
            return PawnAutomaticOutfitStatus.Build(pawn);
        }
    }
}
