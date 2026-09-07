using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    // Only the vanilla-style one-component breakdown repair is staged here.
    internal static class RepairMaterialStage
    {
        internal static bool IsRepair(Job job) => job?.def?.defName == "FixBrokenDownBuilding";

        internal static List<ApparelRule> StatusSources(Pawn pawn, Job job,
            PawnApparelState state, ApparelRule destination, IEnumerable<ApparelRule> rules)
        {
            // Read the existing stage only. UI inspection must not start a stage,
            // find a path, or let a source session excuse missing destination gear.
            if (pawn?.Map == null || pawn.Drafted || pawn.Downed || !IsRepair(job) ||
                job.playerForced || state?.Pawn != pawn || state.RecallRequested ||
                (state.Transition != ApparelTransition.Preparing && state.Transition != ApparelTransition.Active) ||
                destination?.Enabled != true || destination.IsNonWork || destination.WorkAreaPaused ||
                state.ActiveRuleId == destination.Id || state.CurrentRuleIds?.Contains(destination.Id) == true ||
                destination.Area?.Map != pawn.Map || !pawn.Position.IsValid || !pawn.Position.InBounds(pawn.Map) ||
                destination.Area[pawn.Position] ||
                !GearRetrievalRoute.OwnsTarget(job.targetA.Thing, destination.Area) ||
                job.targetA.Thing?.Spawned != true || job.targetA.Thing.Destroyed ||
                job.targetB.Thing == null || job.targetB.Thing.Destroyed ||
                state.PendingBoundaryRuleIds == null ||
                (state.PendingWorkJob != null ? state.PendingWorkJob != job :
                    state.PendingBoundaryWorkJobLoadId != job.loadID))
                return new List<ApparelRule>();

            return (rules ?? Enumerable.Empty<ApparelRule>()).Where(source =>
                source?.Enabled == true && !source.IsNonWork && !source.WorkAreaPaused &&
                source.Id != destination.Id && source.Area?.Map == pawn.Map &&
                state.PendingBoundaryRuleIds.Contains(source.Id) &&
                (state.ActiveRuleId == source.Id || state.CurrentRuleIds?.Contains(source.Id) == true))
                .GroupBy(source => source.Id).Select(group => group.First()).ToList();
        }

        internal static List<ApparelRule> SourceRules(Pawn pawn, Job job, IEnumerable<ApparelRule> rules)
        {
            if (pawn?.Map == null || pawn.Drafted || pawn.Downed || !IsRepair(job) ||
                job.targetA.Thing?.Spawned != true || job.targetA.Thing.Map != pawn.Map ||
                job.targetB.Thing?.Spawned != true || job.targetB.Thing.Map != pawn.Map ||
                job.targetB.Thing.Destroyed || job.targetB.Thing.stackCount <= 0 ||
                job.targetB.Thing.def?.category != ThingCategory.Item ||
                job.targetB.Thing.def.apparel != null || job.targetB.Thing.def.IsWeapon ||
                job.targetC.IsValid || job.targetQueueA?.Any(target => target.IsValid) == true ||
                job.targetQueueB?.Any(target => target.IsValid) == true)
                return new List<ApparelRule>();
            return (rules ?? Enumerable.Empty<ApparelRule>()).Where(rule =>
                rule?.Enabled == true && !rule.IsNonWork && !rule.WorkAreaPaused &&
                rule.Area?.Map == pawn.Map && RuleEvaluator.RuleCanApplyToPawn(pawn, rule) &&
                GearRetrievalRoute.OwnsTarget(job.targetB.Thing, rule.Area) &&
                !GearRetrievalRoute.OwnsTarget(job.targetA.Thing, rule.Area))
                .GroupBy(rule => rule.Id).Select(group => group.First()).ToList();
        }
    }
}
