using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    // Pause exempts rest, not its outfit requirements. This policy preserves
    // the exact bed job through preparation without granting unrelated jobs
    // the same exception or treating a locker detour as a new destination.
    internal static class RestActivityPolicy
    {
        internal static bool Allowed(Pawn pawn, Job job, ApparelRule rule) =>
            pawn?.Map != null && !pawn.Drafted &&
            PausedAreaWorkFilter.IsEssentialPersonalJob(job) &&
            rule?.Enabled == true && rule.Area?.Map == pawn.Map &&
            PausedAreaWorkFilter.WorkAllowedFor(rule, pawn) &&
            !ChildAreaAccessPolicy.Disallows(pawn, rule);

        internal static List<ApparelRule> MatchingPausedRules(Pawn pawn, Job job)
        {
            if (!PausedAreaWorkFilter.IsEssentialPersonalJob(job) || pawn?.Map == null)
                return new List<ApparelRule>();
            return RuleEvaluator.PausedRulesForMap(pawn.Map).Where(rule =>
                Allowed(pawn, job, rule) &&
                (RuleEvaluator.JobPreparationTargetsArea(job, rule.Area) ||
                 ProtectedPathAvoidance.RouteRequiresRestrictedArea(pawn, job, new[] { rule }, false)))
                .ToList();
        }

        internal static bool HasPendingRest(Pawn pawn, PawnApparelState state, Job job) =>
            PreparationJobHandoff.CanContinue(pawn, state) && job != null &&
            ReferenceEquals(job, state.PendingWorkJob) &&
            (state.Transition == ApparelTransition.Preparing || state.Transition == ApparelTransition.Active) &&
            AutomaticOutfitManagerGameComponent.Current.Rules.Any(rule =>
                rule != null && (state.ActiveRuleId == rule.Id || state.CurrentRuleIds?.Contains(rule.Id) == true) &&
                Allowed(pawn, job, rule));

        internal static bool Preserves(PawnApparelState state, ApparelRule rule, Job job)
        {
            Pawn pawn = state?.Pawn;
            if (!PreparationJobHandoff.CanContinue(pawn, state) || job?.def == null ||
                rule == null || (state.ActiveRuleId != rule.Id && state.CurrentRuleIds?.Contains(rule.Id) != true &&
                    state.NestedRuleBuffers?.Any(progress => progress?.RuleId == rule.Id) != true) ||
                (state.Transition != ApparelTransition.Preparing && state.Transition != ApparelTransition.Active))
                return false;
            if (state.PendingWorkJob != null)
                return Allowed(pawn, state.PendingWorkJob, rule) &&
                    (ReferenceEquals(job, state.PendingWorkJob) ||
                     PawnJobTracker_StartJob_Patch.IsAssignedTransitionApparelJob(state, job) ||
                     PawnJobTracker_StartJob_Patch.IsAssignedTransitionWeaponJob(state, job) ||
                     PreparationJobHandoff.IsConnectiveWait(pawn, job) ||
                     PreparationJobHandoff.IsFinalizer(pawn, state, job));
            if (state.Transition != ApparelTransition.Active) return false;
            if (Allowed(pawn, job, rule)) return true;
            if (!(PreparationJobHandoff.IsConnectiveWait(pawn, job) ||
                  PreparationJobHandoff.IsFinalizer(pawn, state, job)) || pawn.jobs?.jobQueue == null)
                return false;
            for (int i = 0; i < pawn.jobs.jobQueue.Count; i++)
            {
                Job parent = pawn.jobs.jobQueue[i].job;
                if (Allowed(pawn, parent, rule) && PreparationJobHandoff.IsPreparedActivity(pawn, state, parent))
                    return true;
            }
            return false;
        }
    }
}
