using System.Collections.Generic;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.State;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    /// <summary>
    /// Gives an Ingest job that was delayed by managed outfit preparation one
    /// bounded recovery opportunity when it ends before making food progress.
    /// A compatibility haul that displaced the meal may finish once before the
    /// retry so its native side effect is not proposed forever. This registry is
    /// intentionally transient: native food selection remains authoritative,
    /// saves own no duplicate Job reference, and player or emergency work
    /// always wins.
    /// </summary>
    internal static class PreparedIngestRetryRegistry
    {
        private const int ActiveProtectionLifetimeTicks = 600;
        private const int RetryLifetimeTicks = 600;
        private const float MinimumFoodProgress = 0.02f;

        private sealed class Entry
        {
            public Job Job;
            public int JobLoadId;
            public int StartedTick;
            public int EndedTick = -1;
            public float FoodLevel;
            public JobCondition EndCondition;
            public bool RetryIssued;
            public int RetryAdmissionTick = -1;
            public bool RetryAdmissionAttempted;
        }

        private sealed class DeferredHaul
        {
            public int JobLoadId;
            public int DeferredTick;
            public bool AllowOnce;
            public bool Started;
        }

        private static readonly Dictionary<int, Entry> Entries =
            new Dictionary<int, Entry>();
        private static readonly Dictionary<int, DeferredHaul> DeferredHauls =
            new Dictionary<int, DeferredHaul>();

        public static void ResetForLoadedGame()
        {
            Entries.Clear();
            DeferredHauls.Clear();
        }

        public static void RecordResumed(Pawn pawn, Job job)
        {
            if (pawn == null || !IsIngest(job))
                return;

            Job clone = job.Clone();
            if (clone == null)
                return;

            DeferredHauls.Remove(pawn.thingIDNumber);
            Entries[pawn.thingIDNumber] = new Entry
            {
                Job = clone,
                JobLoadId = job.loadID,
                StartedTick = CurrentTick,
                FoodLevel = CurrentFoodLevel(pawn)
            };
        }

        public static bool TryGuardAutonomousHaul(
            Pawn pawn,
            Job currentJob,
            ref Job proposedJob,
            out bool skipOriginal,
            out bool clearQueuedJobs,
            out string description)
        {
            skipOriginal = false;
            clearQueuedJobs = false;
            description = null;
            if (pawn == null || proposedJob?.def == null)
                return false;

            bool nativeOverride = pawn.Drafted || pawn.Downed ||
                pawn.InMentalState || proposedJob.playerForced ||
                PawnJobTracker_StartJob_Patch
                    .IsNativeEmergencySafetyJob(proposedJob);
            if (nativeOverride)
            {
                Entries.Remove(pawn.thingIDNumber);
                DeferredHauls.Remove(pawn.thingIDNumber);
                return false;
            }

            // A compatibility prefix can call StartJob recursively. Once an
            // inner call identifies this exact autonomous haul as a meal
            // displacement, keep the decision authoritative for any outer copy
            // of the same logical StartJob call even if transient meal state has
            // changed in between.
            if (WasDeferredHaul(pawn, proposedJob))
            {
                DeferredHaul deferred =
                    DeferredHauls[pawn.thingIDNumber];
                bool alreadyRunning = currentJob?.loadID == proposedJob.loadID;
                skipOriginal = !deferred.AllowOnce || deferred.Started ||
                    alreadyRunning;
                description = skipOriginal
                    ? $"blocked the re-entrant outer {proposedJob.def.defName} " +
                      (deferred.AllowOnce
                          ? "after its nested call started the one allowed compatibility haul"
                          : "while the prepared Ingest retry remains authoritative")
                    : $"allowed the outer {proposedJob.def.defName} to finish once " +
                      "before retrying the prepared Ingest";
                return true;
            }

            if (!Entries.TryGetValue(pawn.thingIDNumber, out Entry entry))
                return false;

            if (entry.EndedTick >= 0)
            {
                int retryElapsed = CurrentTick - entry.EndedTick;
                if (retryElapsed < 0 || retryElapsed > RetryLifetimeTicks ||
                    HasFoodProgress(pawn, entry))
                {
                    Entries.Remove(pawn.thingIDNumber);
                    DeferredHauls.Remove(pawn.thingIDNumber);
                    return false;
                }

                // The main AOM prefix may already have run for an outer copy of
                // the StartJob call before the nested haul marks this entry for
                // retry. Keep the late decision authoritative until a later
                // boundary can admit the meal; otherwise that outer haul starts
                // and immediately consumes the protected area's task buffer.
                if (!IsAutonomousHaul(proposedJob))
                    return false;

                DeferredHauls[pawn.thingIDNumber] = new DeferredHaul
                {
                    JobLoadId = proposedJob.loadID,
                    DeferredTick = CurrentTick
                };
                skipOriginal = true;
                description =
                    $"blocked autonomous {proposedJob.def.defName} while the " +
                    "prepared Ingest awaits its one fresh retry";
                return true;
            }

            int elapsed = CurrentTick - entry.StartedTick;
            if (elapsed < 0 || elapsed > ActiveProtectionLifetimeTicks ||
                HasFoodProgress(pawn, entry))
            {
                Entries.Remove(pawn.thingIDNumber);
                DeferredHauls.Remove(pawn.thingIDNumber);
                return false;
            }

            // This late prefix can observe a nested Ingest call before vanilla
            // has actually placed it in the tracker. Allow the proposal, but do
            // not retire its independent template here. The postfix confirms
            // real tracker ownership after StartJob returns.
            if (IsTrackedIngest(proposedJob, entry))
                return false;

            // Treat the tracked prepared Ingest and its load ID as authoritative.
            // Opportunistic systems do not consistently report their haul until
            // after earlier StartJob prefixes have rewritten the proposal, so the
            // protection decision must use the final proposed job. One initial
            // compatibility haul may complete without buffer credit so its native
            // side effect clears; a rewrite of the one-shot retry is still blocked.
            // Every emergency, draft, mental state, forced order, ordinary job
            // end, and later native food selection remains authoritative.
            if (!IsAutonomousHaul(proposedJob))
                return false;

            Job deferredHaul = proposedJob;
            DeferredHauls[pawn.thingIDNumber] = new DeferredHaul
            {
                JobLoadId = deferredHaul.loadID,
                DeferredTick = CurrentTick
            };

            if (IsTrackedIngest(currentJob, entry) && entry.RetryIssued)
            {
                skipOriginal = true;
                description =
                    $"kept the exact prepared {currentJob.def.defName} active; " +
                    $"deferred autonomous {proposedJob.def.defName} until eating ends";
            }
            else if (entry.RetryIssued &&
                     !entry.RetryAdmissionAttempted &&
                     entry.RetryAdmissionTick == CurrentTick &&
                     entry.Job != null &&
                     PawnJobTracker_StartJob_Patch.PendingWorkJobIsViable(
                         pawn, entry.Job, out _))
            {
                // An opportunistic compatibility prefix rewrote the fresh retry
                // before vanilla could admit it. Discard any queued alias that
                // prefix created, then hand vanilla one final fresh object. The
                // template remains an independent object until the postfix can
                // confirm that vanilla really accepted the replacement.
                Job admittedRetry = entry.Job.Clone();
                if (admittedRetry == null)
                {
                    Entries.Remove(pawn.thingIDNumber);
                    skipOriginal = true;
                    description =
                        $"blocked autonomous {deferredHaul.def.defName}; the " +
                        "one-shot prepared Ingest retry could not be cloned";
                }
                else
                {
                    entry.JobLoadId = admittedRetry.loadID;
                    entry.RetryAdmissionTick = -1;
                    entry.RetryAdmissionAttempted = true;
                    proposedJob = admittedRetry;
                    clearQueuedJobs = true;
                    description =
                        $"restored one fresh prepared Ingest retry after " +
                        $"{deferredHaul.def.defName} rewrote its admission";
                }
            }
            else if (entry.RetryIssued)
            {
                // The bounded retry has already been handed off. Block every
                // duplicate haul without restarting or reusing the Job. Keep
                // the lightweight guard until a real Ingest owns the tracker,
                // reports an end condition, makes food progress, or expires.
                skipOriginal = true;
                description =
                    $"kept the one-shot prepared Ingest protected; blocked " +
                    $"duplicate autonomous {deferredHaul.def.defName}";
            }
            else
            {
                if (!PawnJobTracker_StartJob_Patch.PendingWorkJobIsViable(
                        pawn, entry.Job, out string invalidReason))
                {
                    Entries.Remove(pawn.thingIDNumber);
                    DeferredHauls.Remove(pawn.thingIDNumber);
                    return false;
                }

                // Let one compatibility haul finish instead of suppressing the
                // same native side effect until the protection timer expires.
                // Its successful end is excluded from task-buffer credit, and
                // the next clean StartJob boundary consumes one fresh Ingest
                // clone. A re-entrant outer copy is still blocked once the inner
                // haul actually owns the tracker.
                MarkEndedForRetry(entry);
                DeferredHauls[pawn.thingIDNumber].AllowOnce = true;
                skipOriginal = false;
                description =
                    $"allowed one autonomous {deferredHaul.def.defName} to " +
                    "finish before the prepared Ingest receives its fresh retry";
            }
            return true;
        }

        public static bool TrySuppressCompletedHaulBuffer(
            Pawn pawn,
            PawnApparelState state,
            Job endingJob,
            JobCondition condition,
            out string description)
        {
            description = null;
            if (pawn == null || state == null || endingJob?.def == null ||
                condition != JobCondition.Succeeded || endingJob.playerForced ||
                !IsAutonomousHaul(endingJob))
            {
                return false;
            }

            bool wasDeferred = WasDeferredHaul(pawn, endingJob);
            Entries.TryGetValue(pawn.thingIDNumber, out Entry entry);
            if (!wasDeferred && entry == null)
                return false;

            bool outerCandidate =
                state.PendingBufferedJobLoadId == endingJob.loadID;
            bool nestedCandidate = false;
            foreach (NestedRuleBufferState progress in
                     state.NestedRuleBuffers ?? new List<NestedRuleBufferState>())
            {
                if (progress?.PendingJobLoadId == endingJob.loadID)
                {
                    nestedCandidate = true;
                    break;
                }
            }

            if (!outerCandidate && !nestedCandidate && !wasDeferred)
                return false;

            if (entry == null)
            {
                DeferredHauls.Remove(pawn.thingIDNumber);
                return false;
            }

            bool canScheduleRetry = !entry.RetryIssued;
            int lifetimeStart = entry.EndedTick >= 0
                ? entry.EndedTick
                : entry.StartedTick;
            int lifetime = entry.EndedTick >= 0
                ? RetryLifetimeTicks
                : ActiveProtectionLifetimeTicks;
            int elapsed = CurrentTick - lifetimeStart;
            if (elapsed < 0 || elapsed > lifetime || pawn.Drafted ||
                pawn.Downed || pawn.InMentalState ||
                HasFoodProgress(pawn, entry))
            {
                Entries.Remove(pawn.thingIDNumber);
                DeferredHauls.Remove(pawn.thingIDNumber);
                return false;
            }

            if (canScheduleRetry)
                MarkEndedForRetry(entry);
            else
                Entries.Remove(pawn.thingIDNumber);

            if (outerCandidate)
                state.ClearPendingBufferedTask();
            foreach (NestedRuleBufferState progress in
                     state.NestedRuleBuffers ?? new List<NestedRuleBufferState>())
            {
                if (progress?.PendingJobLoadId == endingJob.loadID)
                    progress.PendingJobLoadId = -1;
            }
            DeferredHauls.Remove(pawn.thingIDNumber);

            description = canScheduleRetry
                ? $"completed the one allowed {endingJob.def.defName} without " +
                  "consuming task-buffer credit; scheduling the prepared Ingest retry"
                : $"ignored {endingJob.def.defName} buffer completion while " +
                  "the one-shot prepared Ingest retry was unresolved";
            return true;
        }

        public static void NotifyEnded(
            Pawn pawn, Job job, JobCondition condition)
        {
            if (pawn == null || !IsIngest(job) ||
                !Entries.TryGetValue(pawn.thingIDNumber, out Entry entry) ||
                entry.JobLoadId != job.loadID)
            {
                return;
            }

            if (condition == JobCondition.Succeeded ||
                HasFoodProgress(pawn, entry))
            {
                Entries.Remove(pawn.thingIDNumber);
                DeferredHauls.Remove(pawn.thingIDNumber);
                return;
            }

            if (entry.RetryIssued)
            {
                Entries.Remove(pawn.thingIDNumber);
                DeferredHauls.Remove(pawn.thingIDNumber);
                if (AomLog.DetailedEnabled)
                {
                    AomLog.Detailed(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                        $"one-shot prepared {job.def?.defName ?? "Ingest"} " +
                        $"retry ended {condition} before food progress; " +
                        "returning control to native food selection.");
                }
                return;
            }

            entry.EndCondition = condition;
            entry.EndedTick = CurrentTick;
            if (AomLog.DetailedEnabled)
            {
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: prepared " +
                    $"{job.def?.defName ?? "Ingest"} ended {condition} before " +
                    "food progress; scheduling one bounded recovery attempt.");
            }
        }

        public static bool TryConsume(
            Pawn pawn,
            Job proposedJob,
            out Job retryJob,
            out string description)
        {
            retryJob = null;
            description = null;
            if (pawn == null || proposedJob?.def == null ||
                !Entries.TryGetValue(pawn.thingIDNumber, out Entry entry) ||
                entry.EndedTick < 0)
            {
                return false;
            }

            int elapsed = CurrentTick - entry.EndedTick;
            if (elapsed < 0 || elapsed > RetryLifetimeTicks ||
                pawn.Drafted || pawn.Downed || pawn.InMentalState ||
                proposedJob.playerForced || HasFoodProgress(pawn, entry) ||
                entry.RetryIssued)
            {
                Entries.Remove(pawn.thingIDNumber);
                DeferredHauls.Remove(pawn.thingIDNumber);
                return false;
            }

            if (IsIngest(proposedJob))
            {
                // A queued/native meal is already the best recovery candidate,
                // but later compatibility prefixes can still rewrite it to a
                // haul. Keep an independent template until the priority-last
                // guard confirms what vanilla will actually receive.
                Job admissionTemplate = proposedJob.Clone();
                if (admissionTemplate == null)
                {
                    Entries.Remove(pawn.thingIDNumber);
                    DeferredHauls.Remove(pawn.thingIDNumber);
                    return false;
                }

                entry.Job = admissionTemplate;
                entry.JobLoadId = proposedJob.loadID;
                entry.StartedTick = CurrentTick;
                entry.EndedTick = -1;
                entry.RetryIssued = true;
                entry.RetryAdmissionTick = CurrentTick;
                entry.RetryAdmissionAttempted = false;
                DeferredHauls.Remove(pawn.thingIDNumber);
                return false;
            }

            string invalidReason = null;
            if (entry.Job != null &&
                PawnJobTracker_StartJob_Patch.PendingWorkJobIsViable(
                    pawn, entry.Job, out invalidReason))
            {
                retryJob = entry.Job.Clone();
                if (retryJob == null)
                {
                    Entries.Remove(pawn.thingIDNumber);
                    DeferredHauls.Remove(pawn.thingIDNumber);
                    description =
                        "the prepared Ingest retry could not be cloned; " +
                        "yielding once for native meal reselection";
                    return true;
                }

                entry.JobLoadId = retryJob.loadID;
                entry.StartedTick = CurrentTick;
                entry.EndedTick = -1;
                entry.RetryIssued = true;
                entry.RetryAdmissionTick = CurrentTick;
                entry.RetryAdmissionAttempted = false;
                DeferredHauls.Remove(pawn.thingIDNumber);
                description =
                    $"retrying the prepared Ingest once with a fresh Job after " +
                    $"{entry.EndCondition}";
                return true;
            }

            Entries.Remove(pawn.thingIDNumber);
            DeferredHauls.Remove(pawn.thingIDNumber);
            // The outfit delay can let another pawn consume or reserve the
            // original stack. One brief neutral wait gives RimWorld's own food
            // giver a fresh selection window without AOM choosing food itself.
            description =
                $"the prepared Ingest target became invalid " +
                $"({invalidReason ?? "unknown reason"}); yielding once for " +
                "native meal reselection";
            return true;
        }

        public static void ConfirmStarted(Pawn pawn, Job currentJob)
        {
            if (pawn == null || currentJob == null)
                return;

            if (DeferredHauls.TryGetValue(
                    pawn.thingIDNumber, out DeferredHaul deferred) &&
                deferred.JobLoadId == currentJob.loadID &&
                IsAutonomousHaul(currentJob))
            {
                deferred.Started = true;
            }

            if (!IsIngest(currentJob) ||
                !Entries.TryGetValue(pawn.thingIDNumber, out Entry entry) ||
                !entry.RetryIssued || !IsTrackedIngest(currentJob, entry))
            {
                return;
            }

            // The tracker now owns the live Job. Drop only the independent
            // template; retain the lightweight identity/progress guard until
            // the meal actually ends or raises the pawn's food level.
            entry.Job = null;
            entry.RetryAdmissionTick = -1;
            entry.RetryAdmissionAttempted = true;
            if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                    pawn, $"prepared-ingest-confirmed:{currentJob.loadID}", 600))
            {
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: confirmed " +
                    "the prepared Ingest owns the current job; retained only " +
                    "bounded meal protection.");
            }
        }

        private static bool HasFoodProgress(Pawn pawn, Entry entry) =>
            CurrentFoodLevel(pawn) >= entry.FoodLevel + MinimumFoodProgress;

        private static void MarkEndedForRetry(Entry entry)
        {
            if (entry.EndedTick >= 0)
                return;

            entry.EndCondition = JobCondition.InterruptForced;
            entry.EndedTick = CurrentTick;
        }

        private static float CurrentFoodLevel(Pawn pawn) =>
            pawn?.needs?.food?.CurLevelPercentage ?? 1f;

        private static int CurrentTick => Find.TickManager?.TicksGame ?? 0;

        private static bool WasDeferredHaul(Pawn pawn, Job job)
        {
            if (pawn == null || job == null ||
                !DeferredHauls.TryGetValue(
                    pawn.thingIDNumber, out DeferredHaul deferred) ||
                deferred.JobLoadId != job.loadID)
            {
                return false;
            }

            int elapsed = CurrentTick - deferred.DeferredTick;
            if (elapsed >= 0 && elapsed <= ActiveProtectionLifetimeTicks)
                return true;

            DeferredHauls.Remove(pawn.thingIDNumber);
            return false;
        }

        private static bool IsAutonomousHaul(Job job) =>
            job?.playerForced != true &&
            (job?.def == JobDefOf.HaulToCell ||
             job?.def == JobDefOf.HaulToContainer);

        private static bool IsTrackedIngest(Job job, Entry entry) =>
            job != null && entry != null && job.loadID == entry.JobLoadId &&
            IsIngest(job);

        private static bool IsIngest(Job job) =>
            job?.def == JobDefOf.Ingest ||
            (job?.def?.defName?.IndexOf(
                "Ingest", System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
    }

    /// <summary>
    /// Runs after the other StartJob prefixes so the guard sees the final job,
    /// including a compatibility or AOM rewrite to opportunistic hauling.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    [HarmonyPriority(Priority.Last)]
    internal static class PawnJobTracker_PreparedIngestGuard_Patch
    {
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

        public static bool Prefix(
            Pawn_JobTracker __instance,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref ThinkTreeDef thinkTree,
            ref JobTag? tag)
        {
            Pawn pawn = PawnField(__instance);
            Job deferredJob = newJob;
            if (!PreparedIngestRetryRegistry.TryGuardAutonomousHaul(
                    pawn, __instance?.curJob, ref newJob,
                    out bool skipOriginal,
                    out bool clearQueuedJobs,
                    out string description))
            {
                return true;
            }

            PawnApparelState state =
                AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn);
            bool replacedJob = !object.ReferenceEquals(deferredJob, newJob);
            bool deferredJobAlreadyRunning =
                __instance?.curJob?.loadID == deferredJob.loadID;
            if ((skipOriginal || replacedJob) && !deferredJobAlreadyRunning)
            {
                if (state?.PendingBufferedJobLoadId == deferredJob.loadID)
                    state.ClearPendingBufferedTask();
                foreach (NestedRuleBufferState progress in
                         state?.NestedRuleBuffers ?? new List<NestedRuleBufferState>())
                {
                    if (progress?.PendingJobLoadId == deferredJob.loadID)
                        progress.PendingJobLoadId = -1;
                }

                AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                    pawn, deferredJob);
            }
            if (clearQueuedJobs)
                __instance?.ClearQueuedJobs(false);
            if (replacedJob)
            {
                jobGiver = newJob.jobGiver;
                thinkTree = newJob.jobGiverThinkTree;
                tag = null;
            }
            if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                    pawn, $"prepared-ingest-displacement:{deferredJob.loadID}", 600))
            {
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                    $"{description}.");
            }
            return !skipOriginal;
        }

        public static void Postfix(Pawn_JobTracker __instance)
        {
            Pawn pawn = PawnField(__instance);
            PreparedIngestRetryRegistry.ConfirmStarted(
                pawn, __instance?.curJob);
        }
    }
}
