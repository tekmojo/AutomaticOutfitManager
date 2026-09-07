using System;
using System.Linq;
using System.Runtime.CompilerServices;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    // Read-only evidence for brief Standing periods after outfit transitions.
    // No path scans, reservations, job changes, or persistent save references.
    internal static class TransitionActivityDiagnostics
    {
        private const int RecentTicks = 2500;
        private sealed class Watch
        {
            internal Map Map;
            internal PawnApparelState State;
            internal ApparelTransition Phase;
            internal int Until, LastTick, WaitSince = -1, JobSince, JobId = int.MinValue;
            internal IntVec3 Position;
            internal string Recent, LastEnd = "none", Rejection = "none";
            internal int RejectionTick = -1, RejectionCount, EventTick, Events;
        }

        internal sealed class Step
        {
            internal Map Map;
            internal object Generation;
            internal string Description, Phase, Queue;
        }

        private static ConditionalWeakTable<Pawn, Watch> watches = new ConditionalWeakTable<Pawn, Watch>();
        private static bool collecting;
        internal static void ResetForLoadedGame()
        {
            watches = new ConditionalWeakTable<Pawn, Watch>();
            collecting = false;
        }

        private static bool Enabled(Pawn pawn)
        {
            if (!AomLog.DetailedEnabled)
            {
                if (collecting) ResetForLoadedGame();
                return false;
            }
            collecting = true;
            return pawn?.Spawned == true && pawn.RaceProps?.Humanlike == true && pawn.Map != null;
        }

        private static Watch Get(Pawn pawn, bool create)
        {
            if (!Enabled(pawn)) return null;
            int tick = Find.TickManager?.TicksGame ?? 0;
            if (watches.TryGetValue(pawn, out Watch watch) &&
                (watch.Map != pawn.Map || tick < watch.LastTick))
            {
                watches.Remove(pawn);
                watch = null;
            }
            if (watch == null && create)
            {
                watch = new Watch { Map = pawn.Map, LastTick = tick };
                watches.Add(pawn, watch);
            }
            if (watch != null) watch.LastTick = tick;
            return watch;
        }

        private static void Touch(Watch watch, string reason)
        {
            watch.Until = (Find.TickManager?.TicksGame ?? 0) + RecentTicks;
            watch.Recent = reason;
        }

        internal static void Cleared(Pawn pawn, string reason)
        {
            Watch watch = Get(pawn, true);
            if (watch == null) return;
            watch.State = null;
            Touch(watch, "session cleared: " + (reason ?? "outfit return or tracked activity complete"));
        }

        internal static void Rejected(Pawn pawn, Job job, string reason)
        {
            Watch watch = Get(pawn, false);
            int tick = Find.TickManager?.TicksGame ?? 0;
            if (watch == null || tick > watch.Until) return;
            watch.RejectionCount++;
            // Scanner/job-giver retries can be frequent. Keep one sampled
            // proposal per second, plus the total number rejected in the window.
            if (watch.RejectionTick >= 0 && tick - watch.RejectionTick < 60) return;
            watch.RejectionTick = tick;
            watch.Rejection = reason + ": " + Describe(job);
        }

        internal static void Sample(Pawn pawn, int tick)
        {
            if (!Enabled(pawn)) return;
            PawnApparelState state = AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn);
            Watch watch = Get(pawn, state != null);
            if (watch == null) return;
            if (!ReferenceEquals(watch.State, state) || (state != null && watch.Phase != state.Transition))
            {
                watch.State = state;
                watch.Phase = state?.Transition ?? default;
                Touch(watch, state == null ? "session cleared" : "observed " + state.Transition);
            }
            if (tick > watch.Until)
            {
                watch.WaitSince = -1;
                watch.LastEnd = watch.Rejection = "none";
                watch.RejectionTick = -1;
                watch.RejectionCount = 0;
                if (state == null) watches.Remove(pawn);
                return;
            }

            Job job = pawn.CurJob;
            int jobId = job?.loadID ?? -1;
            if (watch.JobId != jobId)
            {
                watch.JobId = jobId;
                watch.JobSince = tick;
            }
            bool waiting = job == null || (job.def?.defName?.StartsWith("Wait", StringComparison.Ordinal) == true);
            if (!waiting || job?.playerForced == true || pawn.Drafted || pawn.Downed || pawn.InMentalState || pawn.pather?.Moving == true)
            {
                watch.WaitSince = -1;
                return;
            }
            if (watch.WaitSince < 0 || watch.Position != pawn.Position)
            {
                watch.WaitSince = tick;
                watch.Position = pawn.Position;
            }
            if (tick - watch.WaitSince < 300 || !AomLog.ShouldLogDetailed(pawn, "recent-transition-wait", 600)) return;
            var driver = pawn.jobs?.curDriver;
            AomLog.Detailed($"{pawn.LabelShortCap}: recent-transition wait at tick {tick}; " +
                $"stationary wait observed for {tick - watch.WaitSince} ticks, current job observed for {tick - watch.JobSince} ticks; " +
                $"current={Describe(job)}, giver={job?.jobGiver?.GetType().FullName ?? "none"}, driver={driver?.GetType().FullName ?? "none"}, " +
                $"toil={driver?.CurToilIndex ?? -1}, ticksLeft={driver?.ticksLeftThisToil ?? -1}, position={pawn.Position}, " +
                $"moving={pawn.pather?.Moving == true}, pathDestination={pawn.pather?.Destination}, carried={pawn.carryTracker?.CarriedThing?.ThingID ?? "none"}; " +
                $"state={state?.Transition.ToString() ?? "none"}, rule={state?.ActiveRuleId ?? "none"}, recent={watch.Recent}; " +
                $"queue=[{Queue(pawn)}]; last outfit step ended=[{watch.LastEnd}]; " +
                $"sampled rejected proposal at tick {watch.RejectionTick}=[{watch.Rejection}], rejected proposals observed={watch.RejectionCount}. " +
                "Observational only; a native wait is not itself an AOM fault.");
        }

        internal static Step CaptureStep(Pawn pawn, Job job)
        {
            if (!Enabled(pawn) || job?.targetA.Thing is not Apparel item ||
                (job.def != JobDefOf.Wear && job.def != JobDefOf.RemoveApparel)) return null;
            PawnApparelState state = AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn);
            if (state == null || (state.Transition != ApparelTransition.Preparing && state.Transition != ApparelTransition.Restoring) ||
                !(state.IsPreparationApparel(item) || state.OriginalApparel?.Contains(item) == true)) return null;
            Watch watch = Get(pawn, true);
            Touch(watch, "outfit step during " + state.Transition);
            int tick = Find.TickManager?.TicksGame ?? 0;
            if (tick - watch.EventTick >= 600) { watch.EventTick = tick; watch.Events = 0; }
            if (watch.Events++ >= 60) return null;
            // Snapshot values before native EndCurrentJob/StartJob can pool and
            // reuse the Job object, or finish an adjacent step synchronously.
            return new Step { Map = pawn.Map, Generation = watch, Description = Describe(job), Phase = state.Transition.ToString(), Queue = Queue(pawn) };
        }

        internal static void AfterStep(Pawn pawn, Step step, string operation)
        {
            if (step == null || !Enabled(pawn) || pawn.Map != step.Map) return;
            Watch watch = Get(pawn, false);
            if (watch == null || !ReferenceEquals(watch, step.Generation)) return; // Reset/load invalidated the capture.
            string evidence = $"{step.Description}, phase={step.Phase}, {operation} at tick {Find.TickManager?.TicksGame ?? 0}";
            if (operation.StartsWith("EndCurrentJob", StringComparison.Ordinal)) watch.LastEnd = evidence;
            AomLog.Detailed($"{pawn.LabelShortCap}: outfit step {evidence}; " +
                $"queue before=[{step.Queue}]; after native call: current={Describe(pawn.CurJob)}, " +
                $"state={AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn)?.Transition.ToString() ?? "none"}, queue=[{Queue(pawn)}].");
        }

        private static string Describe(Job job) => job == null ? "none" :
            $"{job.def?.defName ?? "cleared"} #{job.loadID}, forced={job.playerForced}, targets={job.targetA}/{job.targetB}/{job.targetC}";

        private static string Queue(Pawn pawn) => pawn.jobs?.jobQueue == null ? "none" :
            $"count={pawn.jobs.jobQueue.Count}; " + string.Join("; ", pawn.jobs.jobQueue.Take(8).Select(entry => Describe(entry.job)));
    }
}
