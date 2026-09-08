using System;
using System.Runtime.CompilerServices;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.State;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    // Native StartJob can queue its requested Wear/Equip before starting a
    // finalizer. That queued job still owns preparation; a proposal is not a
    // failed acquisition. Never store another serialized owner of either job.
    internal static class PreparationJobHandoff
    {
        private sealed class Finalizer
        {
            internal Pawn Pawn;
            internal PawnApparelState State;
            internal Job Parent;
            internal int ParentId, ChildId;
            internal Thing Target;
        }

        private static ConditionalWeakTable<Job, Finalizer> finalizers =
            new ConditionalWeakTable<Job, Finalizer>();

        private sealed class PreparedActivity
        {
            internal Pawn Pawn;
            internal PawnApparelState State;
            internal Map Map;
            internal int JobId;
        }

        private static ConditionalWeakTable<Job, PreparedActivity> activities =
            new ConditionalWeakTable<Job, PreparedActivity>();

        internal static void ResetForLoadedGame()
        {
            finalizers = new ConditionalWeakTable<Job, Finalizer>();
            activities = new ConditionalWeakTable<Job, PreparedActivity>();
        }

        // Once AOM transfers the exact prepared job to the native tracker, do
        // not insert unrelated optional hauling ahead of it. That detour can
        // spend the entire task buffer before the prepared work ever starts.
        // This marker owns no serialized Job and grants no area/reservation
        // access. Native care finalizers still run. Meals retain their separate
        // one-haul/one-retry compatibility contract.
        internal static void RecordPreparedActivity(Pawn pawn, PawnApparelState state, Job job)
        {
            if (job?.def == null || job.playerForced || !CanContinue(pawn, state) ||
                IsConnectiveWait(pawn, job) ||
                job.def.defName.IndexOf("Ingest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                PawnJobTracker_StartJob_Patch.IsNativeEmergencySafetyJob(job)) return;
            activities.Remove(job);
            activities.Add(job, new PreparedActivity
            { Pawn = pawn, State = state, Map = pawn.Map, JobId = job.loadID });
        }

        internal static bool IsPreparedActivity(Pawn pawn, PawnApparelState state, Job job) =>
            job != null && !job.playerForced && CanContinue(pawn, state) &&
            (state.Transition == ApparelTransition.Preparing || state.Transition == ApparelTransition.Active) &&
            activities.TryGetValue(job, out PreparedActivity entry) &&
            entry.Pawn == pawn && ReferenceEquals(entry.State, state) &&
            entry.Map == pawn.Map && entry.JobId == job.loadID;

        internal static void ConfirmActivityStarted(Pawn pawn, Job job)
        {
            if (job != null && ReferenceEquals(pawn?.jobs?.curJob, job))
                activities.Remove(job);
        }

        internal static bool PreservePreparedActivity(Pawn pawn, PawnApparelState state, Job next)
        {
            if (!CanContinue(pawn, state) || state.Transition != ApparelTransition.Active ||
                next?.def == null || next.playerForced || pawn.jobs?.jobQueue == null ||
                !(IsConnectiveWait(pawn, next) || IsFinalizer(pawn, state, next))) return false;
            for (int i = 0; i < pawn.jobs.jobQueue.Count; i++)
            {
                Job parent = pawn.jobs.jobQueue[i].job;
                if ((IsPreparedActivity(pawn, state, parent) || PreparedIngestRetryRegistry.ProtectsRetry(pawn, parent)) &&
                    PawnJobTracker_StartJob_Patch.PendingWorkJobIsViable(pawn, parent, out _)) return true;
            }
            return false;
        }

        internal static bool CanContinue(Pawn pawn, PawnApparelState state) =>
            pawn?.Spawned == true && !pawn.Drafted && !pawn.Downed &&
            !pawn.InMentalState && state != null && !state.RecallRequested &&
            !state.MapDepartureRequested;

        internal static bool IsConnectiveWait(Pawn pawn, Job job) =>
            job?.def != null && !job.playerForced &&
            (job.def == JobDefOf.Wait || job.def.defName == "Wait_MaintainPosture") &&
            (!job.targetA.IsValid || job.targetA.Thing == pawn) &&
            !job.targetB.IsValid && !job.targetC.IsValid &&
            (job.targetQueueA?.Count ?? 0) == 0 &&
            (job.targetQueueB?.Count ?? 0) == 0;

        internal static bool IsOwnedStep(Pawn pawn, PawnApparelState state, Job job)
        {
            if (state?.Transition != ApparelTransition.Preparing ||
                job?.targetA.Thing is not Thing target || target.Destroyed ||
                !target.Spawned || target.Map != pawn?.Map)
                return false;
            if (job.def == JobDefOf.Wear && target is Apparel apparel)
                return state.IsPreparationApparel(apparel);
            return job.def == JobDefOf.Equip && !job.playerForced &&
                !state.WeaponRuleOverrideExplicit && target is ThingWithComps weapon &&
                state.IsManagedWeapon(weapon);
        }

        internal static bool IsRestorationStep(Pawn pawn, PawnApparelState state, Job job)
        {
            // Recall and departure are reasons to finish restoration, not to
            // revoke its exact saved-item queue. Native emergency/draft control
            // and the ordinary admission/path/hazard checks remain authoritative.
            if (pawn?.Spawned != true || pawn.Drafted || pawn.Downed || pawn.InMentalState ||
                state?.Transition != ApparelTransition.Restoring ||
                job?.targetA.Thing is not Thing target || target.Destroyed ||
                !target.Spawned || target.Map != pawn.Map) return false;
            if (job.def == JobDefOf.Wear && target is Apparel apparel)
                return state.OriginalApparel?.Contains(apparel) == true;
            return job.def == JobDefOf.Equip && !job.playerForced &&
                state.WeaponRestorationRequested && state.OriginalWeapon == target;
        }

        internal static bool PreserveRestorationBeforeRetry(Pawn pawn, PawnApparelState state, Job next)
        {
            if (state?.Transition != ApparelTransition.Restoring || pawn?.jobs?.jobQueue == null ||
                !(IsConnectiveWait(pawn, next) || IsFinalizer(pawn, state, next))) return false;
            for (int i = 0; i < pawn.jobs.jobQueue.Count; i++)
                if (IsRestorationStep(pawn, state, pawn.jobs.jobQueue[i].job)) return true;
            return false;
        }

        internal static bool IsQueued(Pawn pawn, Job job)
        {
            if (job == null || pawn?.jobs?.jobQueue == null) return false;
            for (int i = 0; i < pawn.jobs.jobQueue.Count; i++)
                if (ReferenceEquals(pawn.jobs.jobQueue[i].job, job)) return true;
            return false;
        }

        internal static bool HasQueuedStep(Pawn pawn, PawnApparelState state)
        {
            if (!CanContinue(pawn, state) || state.Transition != ApparelTransition.Preparing ||
                pawn.jobs?.jobQueue == null) return false;
            for (int i = 0; i < pawn.jobs.jobQueue.Count; i++)
                if (IsOwnedStep(pawn, state, pawn.jobs.jobQueue[i].job)) return true;
            return false;
        }

        internal static void MarkFinalizer(Pawn pawn, Job parent, Job child)
        {
            if (child == null) return;
            PreparedIngestRetryRegistry.RecordRetryFinalizer(pawn, parent, child);
            finalizers.Remove(child);
            finalizers.Add(child, new Finalizer
            {
                Pawn = pawn, State = AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn),
                Parent = parent, ParentId = parent.loadID, ChildId = child.loadID,
                Target = parent.targetA.Thing
            });
        }

        internal static bool IsFinalizer(Pawn pawn, PawnApparelState state, Job job) =>
            job != null && finalizers.TryGetValue(job, out Finalizer entry) &&
            ReferenceEquals(entry.Pawn, pawn) && ReferenceEquals(entry.State, state) &&
            entry.ChildId == job.loadID && entry.ParentId == entry.Parent.loadID &&
            ReferenceEquals(entry.Target, entry.Parent.targetA.Thing) &&
            (IsOwnedStep(pawn, state, entry.Parent) || IsRestorationStep(pawn, state, entry.Parent) ||
             IsPreparedActivity(pawn, state, entry.Parent) ||
             PreparedIngestRetryRegistry.ProtectsRetry(pawn, entry.Parent)) &&
            IsQueued(pawn, entry.Parent);

        internal static bool PreserveBeforeRetry(Pawn pawn, PawnApparelState state, Job next)
        {
            if (!HasQueuedStep(pawn, state)) return false;
            // The caller has already checked emergency, player, departure and
            // area access. Only a connective wait or the exact native finalizer
            // may run ahead of the queue without becoming the saved activity.
            return IsConnectiveWait(pawn, next) || IsFinalizer(pawn, state, next);
        }

        internal static bool PreservePendingWait(Pawn pawn, PawnApparelState state, Job next) =>
            CanContinue(pawn, state) && state.Transition == ApparelTransition.Preparing &&
            state.PendingWorkJob != null && IsConnectiveWait(pawn, next);

        internal static bool CanResumeQueuedActivity(Pawn pawn, PawnApparelState state)
        {
            if (!CanContinue(pawn, state) || state.Transition != ApparelTransition.Active ||
                pawn.jobs == null || (pawn.jobs.curJob != null && !IsConnectiveWait(pawn, pawn.jobs.curJob)) ||
                pawn.jobs.jobQueue == null)
                return false;
            // Inspect the next real native job, not a new work search. Its usual
            // StartJob checks still enforce buffer limits, permissions and PPE.
            for (int i = 0; i < pawn.jobs.jobQueue.Count; i++)
            {
                Job queued = pawn.jobs.jobQueue[i].job;
                if (IsConnectiveWait(pawn, queued)) continue;
                return queued?.def != null &&
                    (pawn.jobs.curJob != null || PreparedIngestRetryRegistry.ProtectsRetry(pawn, queued)) &&
                    PawnJobTracker_StartJob_Patch.PendingWorkJobIsViable(pawn, queued, out _);
            }
            return false;
        }

        private static readonly Action<Pawn_JobTracker> FindAndStartNativeJob =
            AccessTools.MethodDelegate<Action<Pawn_JobTracker>>(
                AccessTools.Method(typeof(Pawn_JobTracker), "TryFindAndStartJob"));

        internal static bool ResumeEmptyTracker(Pawn pawn, PawnApparelState state)
        {
            if (pawn?.jobs?.curJob != null || !CanResumeQueuedActivity(pawn, state)) return false;
            // No EndCurrentJob when there is no current job. Native queue
            // consumption retains a single owner and normal StartJob checks.
            FindAndStartNativeJob(pawn.jobs);
            return true;
        }

        internal static string QueueDescription(Pawn pawn)
        {
            var queue = pawn?.jobs?.jobQueue;
            if (queue == null || queue.Count == 0) return "empty";
            string result = "";
            for (int i = 0; i < Math.Min(queue.Count, 4); i++)
            {
                Job job = queue[i].job;
                result += (i == 0 ? "" : ", ") +
                    $"{job?.def?.defName ?? "none"}#{job?.loadID ?? -1}";
            }
            return result + (queue.Count > 4 ? ", ..." : "");
        }

        internal static string TrackerDescription(Pawn pawn) =>
            $"tick={Find.TickManager?.TicksGame ?? 0}, " +
            $"current={pawn?.jobs?.curJob?.def?.defName ?? "none"}#{pawn?.jobs?.curJob?.loadID ?? -1}, " +
            $"queue=[{QueueDescription(pawn)}]";
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), "TryOpportunisticJob")]
    internal static class PawnJobTracker_PreparationOpportunistic_Patch
    {
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

        private static bool Applies(Pawn pawn, Job job)
        {
            var state = AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn);
            return (PreparationJobHandoff.CanContinue(pawn, state) &&
                    PreparationJobHandoff.IsOwnedStep(pawn, state, job)) ||
                   PreparationJobHandoff.IsRestorationStep(pawn, state, job) ||
                   PreparationJobHandoff.IsPreparedActivity(pawn, state, job) ||
                   (PreparationJobHandoff.CanContinue(pawn, state) &&
                    PreparedIngestRetryRegistry.ProtectsRetry(pawn, job));
        }

        // Equip must stay non-player-forced (sidearm preferences). Suppress
        // optional stock hauling only for an exact assigned/saved gear step or
        // the exact activity that caused preparation,
        // while preserving the previous driver's real finalizer, including care.
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(Pawn_JobTracker __instance, Job __0, Job __1, ref Job __result)
        {
            Pawn pawn = PawnField(__instance);
            if (!Applies(pawn, __1)) return true;
            __result = PausedAreaWorkFilter.FinalizerForPreparedHaul(pawn, __1, __0);
            PreparationJobHandoff.MarkFinalizer(pawn, __1, __result);
            return false;
        }

        [HarmonyPriority(Priority.Last)]
        internal static void Postfix(Pawn_JobTracker __instance, Job __0, Job __1, ref Job __result)
        {
            Pawn pawn = PawnField(__instance);
            if (!Applies(pawn, __1)) return;
            __result = PausedAreaWorkFilter.FinalizerForPreparedHaul(pawn, __1, __0);
            PreparationJobHandoff.MarkFinalizer(pawn, __1, __result);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    internal static class PawnJobTracker_PreparedActivityStarted_Patch
    {
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

        [HarmonyPriority(Priority.Last)]
        internal static void Postfix(Pawn_JobTracker __instance) =>
            PreparationJobHandoff.ConfirmActivityStarted(PawnField(__instance), __instance?.curJob);
    }
}
