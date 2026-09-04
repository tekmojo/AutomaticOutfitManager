using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    /// <summary>
    /// Splits a simple source-material/destination-worksite job at the first
    /// neutral cell outside the source rule. This lets the source outfit carry
    /// the material out, while a detached retry can prepare the destination
    /// outfit and collect that same material without re-entering the source.
    /// </summary>
    internal static class MaterialHandoff
    {
        public static bool TryGetStagedSourceRulesForRuntime(
            Pawn pawn,
            Job job,
            PawnApparelState state,
            out List<ApparelRule> sourceRules)
        {
            sourceRules = new List<ApparelRule>();
            if (pawn?.Map == null || job?.def == null || state == null ||
                state.Transition != ApparelTransition.Active ||
                state.RecallRequested || pawn.Drafted || pawn.Downed ||
                !IsBreakdownRepair(job) ||
                state.PendingBoundaryRuleIds?.Count <= 0 ||
                !pawn.Position.IsValid || !pawn.Position.InBounds(pawn.Map))
            {
                return false;
            }

            // While preparation runs, PendingWorkJob owns the continuation. At
            // StartJob that ownership must transfer to RimWorld's live tracker
            // so the same Job is not deep-saved twice. PendingBoundaryRuleIds is
            // deliberately retained as the lightweight source-stage marker.
            bool pendingContinuation = state.PendingWorkJob?.def != null &&
                SameJob(job, state.PendingWorkJob);
            bool activatedContinuation = state.PendingWorkJob == null &&
                state.PendingBoundaryWorkJobLoadId == job.loadID;
            if (!pendingContinuation && !activatedContinuation)
                return false;

            IReadOnlyList<ApparelRule> activeRules =
                RuleEvaluator.ActiveRulesForMap(pawn.Map);

            // Real occupancy always wins. This override exists only for the
            // neutral approach between the locker and the protected material
            // source; once inside any live rule, ordinary runtime enforcement
            // remains authoritative.
            if (activeRules.Any(rule => rule.Area[pawn.Position]))
                return false;

            var stagedIds = new HashSet<string>(
                state.PendingBoundaryRuleIds.Where(id =>
                    !string.IsNullOrEmpty(id)));
            var currentIds = new HashSet<string>(
                state.CurrentRuleIds ?? Enumerable.Empty<string>());
            sourceRules = activeRules
                .Where(rule => stagedIds.Contains(rule.Id) &&
                               (currentIds.Count == 0 ||
                                currentIds.Contains(rule.Id)) &&
                               RuleEvaluator.RuleCanApplyToPawn(pawn, rule) &&
                               PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(
                                   pawn, job, rule))
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
            if (sourceRules.Count == 0)
                return false;

            var sourceIds = new HashSet<string>(
                sourceRules.Select(rule => rule.Id));
            bool hasSeparateDestination = activeRules.Any(rule =>
                !sourceIds.Contains(rule.Id) &&
                RuleEvaluator.JobPreparationTargetsArea(job, rule.Area));
            if (!hasSeparateDestination)
            {
                sourceRules.Clear();
                return false;
            }

            if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                    pawn,
                    $"material-source-stage:{job.loadID}", 600))
            {
                string names = string.Join(", ",
                    sourceRules.Select(rule => $"'{rule.Name}'"));
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: retaining " +
                    $"staged material-source protection for {job.def.defName} " +
                    $"through neutral cell {pawn.Position} for {names}; " +
                    "the destination outfit will wait for material handoff.");
            }

            return true;
        }

        public static bool TryStageAtSourceChangingArea(
            Pawn pawn,
            Job job,
            PawnApparelState state,
            IReadOnlyList<ApparelRule> mapRules,
            IntVec3 nextCell)
        {
            if (pawn?.Map == null || job?.def == null || state == null ||
                state.Transition != ApparelTransition.Active || pawn.Drafted ||
                pawn.Downed || mapRules == null ||
                !IsBreakdownRepair(job) ||
                !PausedAreaWorkFilter.UsesManagedWorkPreparation(job) ||
                PausedAreaWorkFilter.IsHaulingJob(job))
            {
                return false;
            }

            // This hook runs before every path step. Most repair travel occurs
            // before the component has been picked up, so reject that common
            // case before allocating filtered rule lists or evaluating gear.
            Thing carried = pawn.carryTracker?.CarriedThing;
            if (!IsEligibleWorkMaterial(carried))
                return false;

            List<ApparelRule> destinationRules = mapRules
                .Where(rule => rule?.Enabled == true && !rule.WorkAreaPaused &&
                               rule.Area?.Map == pawn.Map &&
                               PausedAreaWorkFilter
                                   .ActivityAllowedAtRuleBoundary(
                                       pawn, job, rule) &&
                               RuleEvaluator.JobPreparationTargetsArea(
                                   job, rule.Area))
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
            if (destinationRules.Count == 0 ||
                destinationRules.All(rule =>
                    !RuleEvaluator.HasMissingRequiredGear(pawn, rule)))
            {
                return false;
            }

            var destinationIds = new HashSet<string>(
                destinationRules.Select(rule => rule.Id));
            var trackedRuleIds = new HashSet<string>(
                state.CurrentRuleIds ?? Enumerable.Empty<string>());
            if (!string.IsNullOrEmpty(state.ActiveRuleId))
                trackedRuleIds.Add(state.ActiveRuleId);

            // TryEnterNextPathCell runs before the pawn steps out of the
            // protected source. The handoff contract is the inverse of that
            // source work area: stage at the first neutral exterior cell even
            // when pathfinding chose an exit other than the configured locker.
            // Requiring the next cell to belong to ChangingArea made a valid
            // component-carrying exit silently miss the handoff and restart the
            // incompatible source/destination outfit cycle.
            IntVec3 handoffCell = IntVec3.Invalid;
            List<ApparelRule> sourceRules = mapRules
                .Where(rule => rule?.Enabled == true &&
                               !rule.WorkAreaPaused &&
                               rule.Area?.Map == pawn.Map &&
                               trackedRuleIds.Contains(rule.Id) &&
                               !destinationIds.Contains(rule.Id) &&
                               pawn.Position.IsValid &&
                               pawn.Position.InBounds(pawn.Map) &&
                               rule.Area[pawn.Position] &&
                               !rule.Area[nextCell])
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();

            bool nextCellIsNeutral = nextCell.IsValid &&
                nextCell.InBounds(pawn.Map) &&
                !mapRules.Any(rule => rule?.Enabled == true &&
                                      rule.Area?.Map == pawn.Map &&
                                      rule.Area[nextCell]);
            if (sourceRules.Count > 0 && nextCellIsNeutral)
            {
                handoffCell = nextCell;
            }
            else
            {
                // A rebuilt path can first be observed after the pawn already
                // entered its locker. Retain the former fallback for that
                // narrow case, but do not make locker membership a requirement
                // for the ordinary protected-to-neutral boundary crossing.
                sourceRules = mapRules
                    .Where(rule => rule?.Enabled == true &&
                                   !rule.WorkAreaPaused &&
                                   rule.Area?.Map == pawn.Map &&
                                   rule.ChangingArea?.Map == pawn.Map &&
                                   trackedRuleIds.Contains(rule.Id) &&
                                   !destinationIds.Contains(rule.Id) &&
                                   pawn.Position.IsValid &&
                                   pawn.Position.InBounds(pawn.Map) &&
                                   !mapRules.Any(other =>
                                       other?.Enabled == true &&
                                       other.Area?.Map == pawn.Map &&
                                       other.Area[pawn.Position]) &&
                                   rule.ChangingArea[pawn.Position])
                    .GroupBy(rule => rule.Id)
                    .Select(group => group.First())
                    .ToList();
                if (sourceRules.Count > 0)
                    handoffCell = pawn.Position;
            }

            if (sourceRules.Count == 0 || !handoffCell.IsValid)
            {
                LogSkippedBoundaryCandidate(
                    pawn, job, nextCell,
                    "the route did not cross from its tracked material source into a neutral cell");
                return false;
            }

            if (!IsSingleAuxiliaryMaterial(job, carried, sourceRules))
            {
                LogSkippedBoundaryCandidate(
                    pawn, job, nextCell,
                    "the running job no longer exposes one safe auxiliary material target");
                return false;
            }

            if (!pawn.carryTracker.TryDropCarriedThing(
                    handoffCell, ThingPlaceMode.Direct,
                    out Thing stagedMaterial) || stagedMaterial == null)
            {
                return false;
            }

            // StartCarryThing can split one unit from a larger source stack.
            // Point target B at the exact staged unit before the retry registry
            // clones the running job, so preparation never sends the pawn back
            // into the source work area for the original stack.
            job.targetB = stagedMaterial;

            // The source boundary retry may still own this same load ID while
            // the running continuation carries the material out. Remove that
            // obsolete owner before recording the destination retry; otherwise
            // the next preparation pass can recombine both incompatible rule
            // sets and send the pawn back toward the original source stack.
            if (SameJob(job, state.PendingWorkJob))
            {
                AutomaticOutfitManagerGameComponent.ClearPendingWork(state);
            }
            else
            {
                ProtectedBoundaryRetryRegistry.Clear(pawn, job);
            }
            ManagedWorkClaimRegistry.Release(pawn, job);
            state.PendingBoundaryRuleIds?.Clear();
            state.PendingBoundaryWorkJobLoadId = -1;

            foreach (ApparelRule destinationRule in destinationRules)
            {
                ProtectedBoundaryRetryRegistry.Record(
                    pawn, job, destinationRule);
            }

            if (AomLog.DetailedEnabled)
            {
                string sourceNames = string.Join(", ",
                    sourceRules.Select(rule => $"'{rule.Name}'"));
                string destinationNames = string.Join(", ",
                    destinationRules.Select(rule => $"'{rule.Name}'"));
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: staged " +
                    $"{stagedMaterial.LabelCap} from {sourceNames} at neutral " +
                    $"material handoff cell {handoffCell} before " +
                    $"{destinationNames}; retained {job.def.defName} with its " +
                    "material target outside the source work area.");
            }

            return true;
        }

        public static void NotifyJobEnded(
            Pawn pawn,
            Job job,
            PawnApparelState state)
        {
            // A staged source marker normally clears when the carried material
            // is dropped at the neutral boundary above. If the live repair ends
            // first (player override, invalid target, danger, or failure), retire
            // it here so an unrelated later repair cannot inherit source gear.
            if (pawn == null || job?.def == null || state == null ||
                state.PendingWorkJob != null ||
                state.PendingBoundaryRuleIds?.Count <= 0 ||
                state.PendingBoundaryWorkJobLoadId != job.loadID ||
                !IsBreakdownRepair(job))
            {
                return;
            }

            ProtectedBoundaryRetryRegistry.Clear(pawn, job);
            state.PendingBoundaryRuleIds.Clear();
            state.PendingBoundaryWorkJobLoadId = -1;
        }

        private static void LogSkippedBoundaryCandidate(
            Pawn pawn, Job job, IntVec3 nextCell, string reason)
        {
            if (!AomLog.DetailedEnabled || pawn == null || job?.def == null ||
                !IsBreakdownRepair(job) ||
                !AomLog.ShouldLogDetailed(
                    pawn,
                    $"material-handoff-skip:{job.loadID}:{reason}", 600))
            {
                return;
            }

            AomLog.Detailed(
                $"[AutomaticOutfitManager] {pawn.LabelShortCap}: material " +
                $"handoff skipped for {job.def.defName} at " +
                $"{pawn.Position} -> {nextCell}; {reason}.");
        }

        private static bool IsEligibleWorkMaterial(Thing carried)
        {
            return carried != null && !carried.Destroyed &&
                   carried.stackCount > 0 && carried is not Pawn &&
                   carried is not Corpse && carried is not Apparel &&
                   carried.def?.IsWeapon != true;
        }

        private static bool IsSingleAuxiliaryMaterial(
            Job job,
            Thing carried,
            IEnumerable<ApparelRule> sourceRules)
        {
            // Primary target A remains the worksite. Limit the first material
            // handoff implementation to one concrete target-B ingredient (the
            // vanilla breakdown-component shape) so multi-ingredient bill and
            // queued-haul state is never partially rewritten.
            if (job?.targetB.HasThing != true || job.targetB.Thing == null ||
                job.targetB.Thing.def != carried?.def || job.targetC.IsValid ||
                job.targetQueueB?.Any(target => target.IsValid) == true)
            {
                return false;
            }

            Thing auxiliaryTarget = job.targetB.Thing;
            if (ReferenceEquals(auxiliaryTarget, carried))
                return true;

            if (auxiliaryTarget.Destroyed)
                return IsBreakdownRepair(job);

            return sourceRules.Any(rule =>
                ThingInsideArea(auxiliaryTarget, rule.Area));
        }

        private static bool IsBreakdownRepair(Job job) =>
            string.Equals(
                job?.def?.defName,
                "FixBrokenDownBuilding",
                System.StringComparison.OrdinalIgnoreCase);

        private static bool SameJob(Job left, Job right) =>
            left != null && right != null &&
            (ReferenceEquals(left, right) || left.loadID == right.loadID);

        private static bool ThingInsideArea(Thing thing, Area area)
        {
            return thing != null && !thing.Destroyed && area?.Map != null &&
                   thing.MapHeld == area.Map && thing.PositionHeld.IsValid &&
                   thing.PositionHeld.InBounds(area.Map) &&
                   area[thing.PositionHeld];
        }
    }
}
