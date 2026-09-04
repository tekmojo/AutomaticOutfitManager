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
    [HarmonyPatch(
        typeof(Pawn_JobTracker),
        nameof(Pawn_JobTracker.EndCurrentJob),
        typeof(JobCondition), typeof(bool), typeof(bool))]
    public static class PawnJobTracker_EndCurrentJob_Patch
    {
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

        public static void Prefix(
            Pawn_JobTracker __instance,
            JobCondition condition)
        {
            Pawn pawn = PawnField(__instance);
            Job endingJob = __instance?.curJob;
            if (pawn == null || endingJob == null)
                return;

            PreparedIngestRetryRegistry.NotifyEnded(
                pawn, endingJob, condition);

            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            component?.NotifySavedWeaponHaulReleased(pawn, endingJob);
            PawnApparelState state = component?.StateFor(pawn);
            if (state == null)
                return;

            MaterialHandoff.NotifyJobEnded(pawn, endingJob, state);

            if (condition == JobCondition.Succeeded &&
                state.Transition == ApparelTransition.Restoring &&
                (PawnJobTracker_StartJob_Patch
                     .IsAssignedTransitionApparelJob(state, endingJob) ||
                 PawnJobTracker_StartJob_Patch
                     .IsAssignedTransitionWeaponJob(state, endingJob)))
            {
                component.NotifySuccessfulRestorationStep(pawn, state);
            }

            RecordDepartureRestorationProgress(state, endingJob, condition);
            RecordSavedWeaponRestorationOutcome(
                pawn, state, endingJob, condition);
            if (PreparedIngestRetryRegistry.TrySuppressCompletedHaulBuffer(
                    pawn, state, endingJob, condition,
                    out string ingestBufferDescription))
            {
                if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                        pawn,
                        $"prepared-ingest-buffer:{endingJob.loadID}", 600))
                {
                    AomLog.Detailed(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                        $"{ingestBufferDescription}.");
                }
                return;
            }
            CompleteOuterBufferCandidate(
                pawn, component, state, endingJob, condition);
            CompleteNestedBufferCandidates(
                pawn, component, state, endingJob, condition);
        }

        private static void RecordDepartureRestorationProgress(
            PawnApparelState state,
            Job endingJob,
            JobCondition condition)
        {
            if (state.MapDepartureRequested != true ||
                state.Transition != ApparelTransition.Restoring ||
                condition != JobCondition.Succeeded)
            {
                return;
            }

            Thing target = endingJob.targetA.Thing;
            bool restoredSavedApparel =
                endingJob.def == JobDefOf.Wear &&
                target is RimWorld.Apparel apparel &&
                state.OriginalApparel?.Contains(apparel) == true;
            bool restoredSavedWeapon =
                endingJob.def == JobDefOf.Equip &&
                target == state.OriginalWeapon;
            bool returnedManagedGear =
                (endingJob.def == JobDefOf.RemoveApparel ||
                 endingJob.def == JobDefOf.DropEquipment ||
                 endingJob.def == JobDefOf.HaulToCell ||
                 endingJob.def == JobDefOf.HaulToContainer) &&
                ((target is RimWorld.Apparel managedApparel &&
                  state.ManagedApparel?.Contains(managedApparel) == true) ||
                 (target is ThingWithComps managedWeapon &&
                  state.ManagedWeapons?.Contains(managedWeapon) == true));
            if (restoredSavedApparel || restoredSavedWeapon ||
                returnedManagedGear)
            {
                // Only consecutive rebuilds without a successful Phase 3 step
                // count toward the bounded guest-departure fallback. A large
                // valid outfit must not be abandoned merely because restoring
                // it naturally requires more than three separate jobs.
                state.DepartureRestorationAttempts = 0;
            }
        }

        private static void RecordSavedWeaponRestorationOutcome(
            Pawn pawn,
            PawnApparelState state,
            Job endingJob,
            JobCondition condition)
        {
            if (state.Transition != ApparelTransition.Restoring ||
                state.WeaponInterventionActive != true ||
                endingJob.def != JobDefOf.Equip ||
                endingJob.targetA.Thing is not ThingWithComps weapon ||
                weapon != state.OriginalWeapon)
            {
                return;
            }

            if (pawn.equipment?.Primary == weapon)
            {
                state.RejectedWeaponRestorationAttempts = 0;
                return;
            }

            // Drafting, incapacitation, and mental-state overrides interrupt a
            // valid native Equip without rejecting the saved weapon. Phase 3 is
            // suspended in those states and will retry after native control ends.
            if (pawn.Drafted || pawn.Downed || pawn.InMentalState)
                return;

            // Count the concrete Equip job that actually ended unsuccessfully,
            // not recovery waits or attempts merely proposed by the component.
            // This prevents a redirected wake-up Goto from exhausting the retry
            // limit without RimWorld ever receiving the saved-weapon job.
            if (condition != JobCondition.Succeeded ||
                pawn.equipment?.Primary != weapon)
            {
                state.RejectedWeaponRestorationAttempts++;
            }
        }

        private static void CompleteOuterBufferCandidate(
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            PawnApparelState state,
            Job endingJob,
            JobCondition condition)
        {
            if (state.PendingBufferedJobLoadId != endingJob.loadID)
                return;

            string pendingRuleId = state.PendingBufferedRuleId;
            state.ClearPendingBufferedTask();

            ApparelRule rule = component.RuleById(pendingRuleId);
            bool accepted = condition == JobCondition.Succeeded &&
                state.Transition == ApparelTransition.Active &&
                !state.RecallRequested &&
                rule?.Enabled == true &&
                !rule.WorkAreaPaused &&
                state.ActiveRuleId == pendingRuleId &&
                state.BufferedTasksCompleted < rule.ReturnTaskBuffer;
            if (!accepted)
            {
                if (AomLog.DetailedEnabled && condition != JobCondition.Succeeded)
                {
                    AomLog.Detailed(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                        $"task buffer candidate {endingJob.def?.defName ?? "job"} " +
                        $"ended {condition}; not counted.");
                }
                return;
            }

            state.BufferedTasksCompleted++;
            state.LastBufferedJobLoadId = endingJob.loadID;
            if (AomLog.DetailedEnabled &&
                (state.BufferedTasksCompleted == 1 ||
                 state.BufferedTasksCompleted >= rule.ReturnTaskBuffer))
            {
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: task buffer " +
                    $"{state.BufferedTasksCompleted}/{rule.ReturnTaskBuffer} " +
                    $"completed by {endingJob.def?.defName ?? "job"}.");
            }
        }

        private static void CompleteNestedBufferCandidates(
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            PawnApparelState state,
            Job endingJob,
            JobCondition condition)
        {
            foreach (NestedRuleBufferState progress in
                     state.NestedRuleBuffers ?? new List<NestedRuleBufferState>())
            {
                if (progress?.PendingJobLoadId != endingJob.loadID)
                    continue;

                progress.PendingJobLoadId = -1;
                ApparelRule rule = component.RuleById(progress.RuleId);
                bool accepted = condition == JobCondition.Succeeded &&
                    state.Transition == ApparelTransition.Active &&
                    !state.RecallRequested &&
                    !progress.Finished &&
                    rule?.Enabled == true &&
                    progress.Completed < rule.ReturnTaskBuffer;
                if (!accepted)
                {
                    if (AomLog.DetailedEnabled && condition != JobCondition.Succeeded)
                    {
                        AomLog.Detailed(
                            $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                            $"nested task buffer candidate {endingJob.def?.defName ?? "job"} " +
                            $"for '{rule?.Name ?? "missing rule"}' ended {condition}; " +
                            "not counted.");
                    }
                    continue;
                }

                progress.Completed++;
                progress.LastJobLoadId = endingJob.loadID;
                progress.LastJobLabel = endingJob.GetReport(pawn);
                state.LastNestedBufferStatus =
                    $"{rule.Name}: {progress.Completed} of " +
                    $"{rule.ReturnTaskBuffer} outer tasks completed" +
                    (string.IsNullOrEmpty(progress.LastJobLabel)
                        ? "."
                        : $"; last: {progress.LastJobLabel}.");
                if (AomLog.DetailedEnabled &&
                    (progress.Completed == 1 ||
                     progress.Completed >= rule.ReturnTaskBuffer))
                {
                    AomLog.Detailed(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: nested " +
                        $"task buffer {progress.Completed}/{rule.ReturnTaskBuffer} " +
                        $"completed by {endingJob.def?.defName ?? "job"} after " +
                        $"leaving '{rule.Name}'.");
                }
            }
        }
    }
}
