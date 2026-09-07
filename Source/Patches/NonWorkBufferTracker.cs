using System.Collections.Generic;
using System.Linq;
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
    internal static class NonWorkBufferTracker
    {
        private static AutomaticOutfitManagerGameComponent Component => AutomaticOutfitManagerGameComponent.Current;
        internal static NonWorkOutfitBuffer For(Pawn pawn) => pawn == null ? null :
            Component?.NonWorkOutfitBuffers.FirstOrDefault(item => item.Pawn == pawn && item.Map == pawn.Map);

        private static bool NativeOverride(Pawn pawn, Job job) => pawn?.Spawned != true ||
            pawn.Dead || pawn.Downed || pawn.Drafted || pawn.InMentalState ||
            job?.playerForced == true || PawnAccessClassifier.IsNativeCustodyEscapeActive(pawn) ||
            PawnJobTracker_StartJob_Patch.IsNativeEmergencySafetyJob(job) ||
            PawnJobTracker_StartJob_Patch.IsMapDepartureJob(job);

        internal static void Clear(Pawn pawn) => Component?.NonWorkOutfitBuffers.RemoveAll(item => item.Pawn == pawn);

        internal static void Begin(Pawn pawn, ApparelRule rule)
        {
            if (Component == null || rule?.IsNonWork != true || rule.ReturnTaskBuffer <= 0 ||
                !rule.Enabled || rule.WorkAreaPaused || rule.Area?.Map != pawn?.Map ||
                !PawnAccessClassifier.IsApparelEligibleHuman(pawn) || NativeOverride(pawn, pawn.CurJob) ||
                Component.StateFor(pawn) != null || RuleEvaluator.HasMissingRequiredGear(pawn, rule)) return;
            Clear(pawn);
            Component.NonWorkOutfitBuffers.Add(new NonWorkOutfitBuffer {
                Pawn = pawn, Map = pawn.Map, RuleId = rule.Id,
                Apparel = pawn.apparel.WornApparel.ToList(), Weapon = pawn.equipment?.Primary
            });
        }

        // Also called by the existing runtime pawn scan: enroll already-dressed
        // occupants and validate loaded records without resetting completed tasks.
        internal static void Refresh(Pawn pawn)
        {
            if (Component == null || pawn == null) return;
            var buffer = For(pawn);
            Job job = pawn.CurJob;
            if (ChildcareContinuation.Admit(pawn, job)) return;
            if (buffer?.PendingWork != null)
            {
                var source = Component.RuleById(buffer.RuleId);
                if (NativeOverride(pawn, job) || source?.Enabled != true || source.WorkAreaPaused ||
                    source.Area?.Map != pawn.Map || Component.StateFor(pawn)?.RecallRequested == true ||
                    (Find.TickManager?.TicksGame ?? 0) - buffer.HandoffStartedTick > 2500) Clear(pawn);
                return;
            }
            if (NativeOverride(pawn, job) || Component.StateFor(pawn) != null)
            { if (buffer != null) Clear(pawn); return; }
            var rule = buffer == null ? null : Component.RuleById(buffer.RuleId);
            if (buffer != null && (rule?.Enabled != true || !rule.IsNonWork || rule.WorkAreaPaused ||
                rule.ReturnTaskBuffer <= 0 || buffer.Map != pawn.Map || rule.Area?.Map != buffer.Map ||
                !buffer.OutfitUnchanged() || RuleEvaluator.HasMissingRequiredGear(pawn, rule)))
            { Clear(pawn); buffer = null; }
            if (buffer == null)
            {
                rule = RuleEvaluator.ActiveRulesForMap(pawn.Map).FirstOrDefault(candidate =>
                    candidate.IsNonWork && candidate.ReturnTaskBuffer > 0 && candidate.Area[pawn.Position] &&
                    !RuleEvaluator.HasMissingRequiredGear(pawn, candidate));
                if (rule == null) return;
                Begin(pawn, rule);
                buffer = For(pawn);
            }
            if (buffer == null || job == null || buffer.LastStartedJobId == job.loadID) return;
            bool ownTask = RuleEvaluator.JobTargetsArea(job, rule.Area) ||
                (NonWorkMealHandoff.For(pawn) is NonWorkMealTrip trip &&
                 trip.Stage == NonWorkMealStage.Eating && trip.DestinationRuleId == rule.Id &&
                 job.def == JobDefOf.Ingest);
            bool compatible = !RuleEvaluator.MatchingRules(pawn, job).Any(candidate =>
                candidate.Id != rule.Id && RuleEvaluator.HasMissingRequiredGear(pawn, candidate));
            // A real managed Work/fallback session owns its own buffer. Never
            // retain this allowance as an overlapping clothing requirement.
            bool countable = PawnJobTracker_StartJob_Patch.IsBufferableJob(job);
            if (PausedAreaWorkFilter.IsEssentialPersonalJob(job) && !ownTask) compatible = false;
            if (!buffer.Start(job.loadID, rule.ReturnTaskBuffer, ownTask, compatible, countable)) Clear(pawn);
        }

        internal static void End(Pawn pawn, Job job, JobCondition condition)
        {
            var buffer = For(pawn);
            if (buffer == null) return;
            if (buffer.PendingWork != null)
            {
                if (condition != JobCondition.Succeeded && job.targetA.Cell == buffer.ExitCell) Clear(pawn);
                return;
            }
            var rule = Component.RuleById(buffer.RuleId);
            bool valid = !NativeOverride(pawn, job) && Component.StateFor(pawn) == null &&
                rule?.Enabled == true && !rule.WorkAreaPaused && buffer.Map == pawn.Map &&
                rule.Area?.Map == pawn.Map && buffer.OutfitUnchanged();
            if (!valid) { Clear(pawn); return; }
            bool pending = buffer.PendingJobId == job.loadID;
            if (buffer.Complete(job.loadID, condition == JobCondition.Succeeded, rule.ReturnTaskBuffer))
            {
                if (AomLog.DetailedEnabled)
                    AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: non-work task buffer {buffer.Completed}/{rule.ReturnTaskBuffer} completed by {job.def.defName}; retained outfit unchanged.");
            }
            else if (pending && condition != JobCondition.Succeeded && AomLog.DetailedEnabled)
                AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: non-work buffer candidate {job.def.defName} ended {condition}; not counted.");
        }

        internal static void PreserveHandoff(Pawn pawn, ApparelRule source, Job job, IntVec3 exit)
        {
            // Separate from the active tracker/queue and any fallback snapshot.
            // Only this record deep-saves the detached continuation during egress.
            Job pending = ProtectedBoundaryRetryRegistry.DetachedClone(job);
            if (pending == null) return;
            var buffer = For(pawn);
            if (buffer == null)
            {
                buffer = new NonWorkOutfitBuffer { Pawn = pawn, Map = pawn.Map,
                    RuleId = source.Id, Apparel = pawn.apparel.WornApparel.ToList(),
                    Weapon = pawn.equipment?.Primary };
                Component.NonWorkOutfitBuffers.Add(buffer);
            }
            buffer.PendingJobId = -1;
            buffer.PendingWork = pending;
            buffer.ExitCell = exit;
            buffer.HandoffStartedTick = Find.TickManager?.TicksGame ?? 0;
        }

        internal static bool ResumeHandoff(Pawn pawn, ref Job job, ref ThinkNode giver,
            ref ThinkTreeDef tree, ref JobTag? tag, out bool exiting)
        {
            exiting = false;
            var buffer = For(pawn);
            if (buffer?.PendingWork == null) return false;
            var source = Component.RuleById(buffer.RuleId);
            if (NativeOverride(pawn, job) || buffer.Map != pawn.Map || source?.Enabled != true ||
                source.WorkAreaPaused || source.Area?.Map != pawn.Map ||
                !buffer.ExitCell.IsValid || !buffer.ExitCell.InBounds(pawn.Map) || source.Area[buffer.ExitCell] ||
                (Find.TickManager?.TicksGame ?? 0) - buffer.HandoffStartedTick > 2500 ||
                Component.StateFor(pawn)?.RecallRequested == true ||
                !PawnJobTracker_StartJob_Patch.PendingWorkJobIsViable(pawn, buffer.PendingWork, out _))
            { Clear(pawn); return false; }
            if (source.Area[pawn.Position])
            {
                // Do not outfit for the destination while still under source rules.
                job = PawnJobTracker_StartJob_Patch.MakeChangingAreaTravelJob(buffer.ExitCell);
                job.expiryInterval = 2000;
                giver = null; tree = null; tag = null;
                exiting = true;
                return true;
            }
            Job pending = buffer.PendingWork;
            buffer.PendingWork = null;
            Clear(pawn); // The old Non-Work allowance cannot reassert its outfit.
            AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(pawn, job);
            job = pending;
            giver = pending.jobGiver;
            tree = pending.jobGiverThinkTree;
            tag = null;
            if (AomLog.DetailedEnabled)
                AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: ended Non-Work buffer; resuming exact {job.def.defName} through normal destination outfit preparation.");
            return true;
        }

    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    internal static class NonWorkBufferStartedPatch
    {
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Pawn_JobTracker __instance) => NonWorkBufferTracker.Refresh(PawnField(__instance));
    }

    // Retention must not be defeated by the ordinary apparel optimizer between
    // two buffered tasks. Explicit orders and required AOM changes still work.
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), "TryGiveJob")]
    internal static class NonWorkBufferOptimizePatch
    {
        private static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (NonWorkBufferTracker.For(pawn) == null || pawn.Drafted || pawn.Downed) return true;
            __result = null;
            return false;
        }
    }
}
