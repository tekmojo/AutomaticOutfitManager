using System.Linq;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    // An unavailable saved item remains owned and recoverable while the pawn
    // sleeps/eats. No second snapshot, replacement outfit or saved Job owner.
    internal static class RestorationNeeds
    {
        internal static bool IsNeed(Job job) =>
            PausedAreaWorkFilter.IsEssentialPersonalJob(job) || job?.def == JobDefOf.Ingest;

        internal static bool MissingProtection(Pawn pawn, PawnApparelState state,
            AutomaticOutfitManager.Rules.ApparelRule rule)
        {
            if (!RuleEvaluator.UsesSavedNonWorkOutfit(pawn, rule))
                return RuleEvaluator.HasMissingRequiredGear(pawn, rule);
            return pawn.apparel.WornApparel.Any(item => NonWorkOutfitPolicy.ShouldReturn(pawn, rule, item)) ||
                (!state.WeaponRuleOverrideExplicit &&
                 NonWorkOutfitPolicy.ShouldReturn(pawn, rule, pawn.equipment?.Primary));
        }

        internal static bool CanDefer(Pawn pawn, PawnApparelState state, Job job,
            int executableSteps, bool unavailable)
        {
            if (pawn?.Spawned != true || pawn.Drafted || pawn.Downed || pawn.InMentalState ||
                state?.Transition != ApparelTransition.Restoring || state.MapDepartureRequested ||
                !IsNeed(job) || job.playerForced || executableSteps != 0 || !unavailable ||
                (pawn.jobs?.jobQueue?.Count ?? 0) != 0 ||
                PausedAreaWorkFilter.DeniedActivityRule(pawn, job) != null ||
                PausedAreaWorkFilter.DeniedPausedAreaRule(pawn, job) != null)
                return false;

            foreach (var rule in RuleEvaluator.EnabledRulesForMap(pawn.Map))
            {
                if (!RuleEvaluator.JobPreparationTargetsArea(job, rule.Area) &&
                    !ProtectedPathAvoidance.RouteRequiresRestrictedArea(pawn, job, new[] { rule }, false))
                    continue;
                if (!PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(pawn, job, rule)) return false;
                // A missing saved piece may wait; selected work removal and
                // actual Work/fallback protection remain mandatory.
                if (MissingProtection(pawn, state, rule)) return false;
            }
            return true;
        }
    }
}
