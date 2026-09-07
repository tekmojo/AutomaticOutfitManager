using System;
using System.Collections.Generic;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    // Native DoBill collects targetQueueB[0] before visiting its worksite.
    // Check that concrete first pickup at admission, before the driver pops
    // either queue. Do not turn arbitrary auxiliary targets into work rules.
    internal static class NonWorkIngredientAdmission
    {
        private static readonly IReadOnlyList<ApparelRule> Empty = Array.Empty<ApparelRule>();

        internal static IReadOnlyList<ApparelRule> RequiredRules(
            Pawn pawn, PawnApparelState state, Job job)
        {
            if (pawn?.Spawned != true || pawn.Map == null || pawn.Drafted ||
                pawn.Downed || pawn.InMentalState ||
                !PawnAccessClassifier.IsApparelEligibleHuman(pawn) ||
                state?.Pawn != pawn || state.Transition != ApparelTransition.Active ||
                state.RecallRequested || job?.def != JobDefOf.DoBill ||
                job.def.driverClass != typeof(JobDriver_DoBill) || job.playerForced ||
                job.workGiverDef == null || job.bill == null || pawn.CurJob == job ||
                pawn.carryTracker?.CarriedThing != null || job.placedThings?.Count > 0 ||
                job.targetQueueB == null || job.targetQueueB.Count == 0 ||
                job.countQueue == null || job.countQueue.Count != job.targetQueueB.Count ||
                !pawn.Position.IsValid || !pawn.Position.InBounds(pawn.Map))
                return Empty;

            var component = AutomaticOutfitManagerGameComponent.Current;
            if (component?.RuleById(state.ActiveRuleId)?.IsNonWork != false)
                return Empty;

            Thing worksite = job.targetA.Thing;
            if (worksite?.Spawned != true || worksite.Destroyed || worksite.Map != pawn.Map ||
                !worksite.Position.IsValid || !worksite.Position.InBounds(pawn.Map) ||
                worksite.IsForbidden(pawn))
                return Empty;
            for (int i = 0; i < job.targetQueueB.Count; i++)
            {
                Thing item = job.targetQueueB[i].Thing;
                if (item?.Spawned != true || item.Destroyed || item.Map != pawn.Map ||
                    !item.Position.IsValid || !item.Position.InBounds(pawn.Map) || item.IsForbidden(pawn) ||
                    job.countQueue[i] <= 0 || job.countQueue[i] > item.stackCount)
                    return Empty;
            }

            IReadOnlyList<ApparelRule> rules = RuleEvaluator.ActiveRulesForMap(pawn.Map);
            // Multi-area ingredient logistics need their own sequential carry
            // handoff. This early change is only for an outside worksite whose
            // ingredients do not require entering another Work Area.
            foreach (ApparelRule rule in rules)
            {
                if (!rule.IsNonWork && RuleEvaluator.JobTargetsArea(job, rule.Area))
                    return Empty;
            }

            LocalTargetInfo pickup = job.targetQueueB[0];
            List<ApparelRule> required = null;
            foreach (ApparelRule rule in rules)
            {
                if (!rule.IsNonWork || rule.Area[pawn.Position] ||
                    !rule.Area[pickup.Cell] ||
                    !RuleEvaluator.RuleCanApplyToPawn(pawn, rule) ||
                    !PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(pawn, job, rule) ||
                    UnavailableWorkRegistry.HasActiveRuleBlock(pawn, rule) ||
                    (!RuleEvaluator.HasMissingRequiredGear(pawn, rule) &&
                     !NonWorkOutfitPolicy.NeedsStateHandoff(pawn, rule)))
                    continue;

                // ClosestTouch is the native DoBill ingredient Goto mode. An
                // adjacent pickup reachable outside the area needs no change.
                // An unreachable or contested pickup must not send us changing.
                if (!pawn.CanReserve(pickup, 1, -1, null, false) ||
                    !pawn.CanReserve(job.targetA, 1, -1, null, false) ||
                    !pawn.CanReach(pickup, PathEndMode.ClosestTouch, Danger.Deadly) ||
                    ProtectedPathAvoidance.SegmentAvoidsRules(pawn, pawn.Position,
                        pickup, new List<ApparelRule> { rule }, null, PathEndMode.ClosestTouch))
                    continue;

                if (required == null)
                    required = new List<ApparelRule>();
                required.Add(rule);
            }
            return required ?? Empty;
        }

        internal static void Preserve(Pawn pawn, Job job, IReadOnlyList<ApparelRule> rules)
        {
            if (rules == null || rules.Count == 0)
                return;
            // The registry owns detached job/queue data. Native initialization
            // has not consumed the first ingredient or its count yet.
            foreach (ApparelRule rule in rules)
                ProtectedBoundaryRetryRegistry.Record(pawn, job, rule);
            if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                    pawn, $"early-non-work-pickup:{job.loadID}", 600))
                AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                    $"changing before the first ingredient trip for {job.def.defName} " +
                    $"#{job.loadID}; pickup={job.targetQueueB[0]}, " +
                    $"Non-Work Area='{rules[0].Name}'; exact job and ingredient queues retained.");
        }
    }
}
