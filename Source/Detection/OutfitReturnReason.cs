using System.Linq;
using System.Collections.Generic;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    internal static class OutfitReturnReason
    {
        internal static void RecordSequentialHandoff(Pawn pawn, PawnApparelState state,
            IEnumerable<ApparelRule> sources, IEnumerable<ApparelRule> destinations, Job proposed)
        {
            if (state?.Transition != ApparelTransition.Active) return;
            // This branch has already established incompatible sequential
            // outfits. Record that cause before clearing the original job.
            // Stale idle flags or completed buffers must not describe this as
            // an automatic return, and synthetic travel must not replace it.
            state.ReturnReason = "Changing between incompatible area outfits";
            if (!AomLog.DetailedEnabled) return;
            string source = string.Join(", ", sources.Select(rule => $"{rule.Name} [{rule.Id}]"));
            string destination = string.Join(", ", destinations.Select(rule => $"{rule.Name} [{rule.Id}]"));
            AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: outfit return decision: {state.ReturnReason}. " +
                $"Sources=[{source}], destinations=[{destination}]; buffer completed={state.BufferedTasksCompleted}; " +
                $"proposal {proposed?.def?.defName} #{proposed?.loadID}, targets {proposed?.targetA}/{proposed?.targetB}/{proposed?.targetC}; " +
                Patches.PreparationJobHandoff.TrackerDescription(pawn) + ".");
        }

        internal static void Record(Pawn pawn, PawnApparelState state, ApparelRule rule,
            Job proposed, bool sleep, bool otherRule)
        {
            if (state == null) return;
            // Recovery waits and locker travel must not replace the original
            // reason with the synthetic job used to finish the same return.
            if (state.Transition != ApparelTransition.Active &&
                !string.IsNullOrEmpty(state.ReturnReason)) return;
            bool resuming = state.Transition != ApparelTransition.Active;
            state.ReturnReason = Describe(state.MapDepartureRequested,
                !string.IsNullOrEmpty(state.NonWorkRestorationRuleId),
                rule?.WorkAreaPaused == true || state.PauseRecallRuleIds?.Count > 0,
                state.AutomaticIdleReturnRequested, state.RecallRequested,
                resuming, sleep, rule?.ReturnTaskBuffer ?? 0,
                state.BufferedTasksCompleted, otherRule);
            if (AomLog.DetailedEnabled)
            {
                string matched = string.Join(", ", RuleEvaluator.MatchingRules(pawn, proposed)
                    .Select(candidate => $"{candidate.Name} [{candidate.Id}]"));
                AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: outfit return decision: {state.ReturnReason}. " +
                    $"Rule {rule?.Name} [{rule?.Id}], buffer {state.BufferedTasksCompleted}/{rule?.ReturnTaskBuffer ?? 0}; " +
                    $"proposal {proposed?.def?.defName} #{proposed?.loadID}, targets {proposed?.targetA}/{proposed?.targetB}/{proposed?.targetC}; " +
                    $"transition={state.Transition}, recall={state.RecallRequested}, automaticIdle={state.AutomaticIdleReturnRequested}, " +
                    $"nonWorkDestination={state.NonWorkRestorationRuleId ?? "none"}, matchingRules=[{matched}]; " +
                    Patches.PreparationJobHandoff.TrackerDescription(pawn) + ".");
            }
        }

        internal static string Describe(bool departure, bool nonWork, bool paused,
            bool idle, bool recall, bool resuming, bool sleep, int limit, int completed, bool otherRule)
        {
            if (departure) return "Returning outfits before leaving the map";
            if (nonWork) return "Changing for a Non-Work Area";
            if (paused) return "Work paused";
            if (idle) return "No further activity progressed within the idle allowance";
            if (recall) return "Recall or access restriction requested a return";
            if (resuming) return "Continuing an earlier outfit return; original reason was not recorded";
            if (sleep) return "Restoring personal outfit before sleep";
            if (limit <= 0) return "Task Buffer is set to Immediate";
            if (completed >= limit) return "Task buffer completed";
            if (otherRule) return "Next activity requires another area rule";
            return "Next activity triggered an outfit handoff before the buffer was complete";
        }
    }
}
