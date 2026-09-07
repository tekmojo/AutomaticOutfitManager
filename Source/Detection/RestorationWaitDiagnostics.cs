using System;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.State;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    internal static class RestorationWaitDiagnostics
    {
        internal static void Report(Pawn pawn, PawnApparelState state)
        {
            if (!AomLog.DetailedEnabled || pawn?.Map == null ||
                state?.Transition != ApparelTransition.Restoring ||
                pawn.pather?.Moving == true) return;
            Job job = pawn.CurJob;
            if (job?.def == null) return;
            string name = job.def.defName ?? string.Empty;
            bool waiting = name.StartsWith("Wait", StringComparison.OrdinalIgnoreCase) ||
                (job.GetReport(pawn)?.IndexOf("Standing", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
            if (!waiting || !AomLog.ShouldLogDetailed(pawn, "restoration-wait-state", 600)) return;

            var driver = pawn.jobs.curDriver;
            string queued = pawn.jobs.jobQueue == null ? "none" : string.Join("; ",
                pawn.jobs.jobQueue.Take(12).Select(entry => Describe(entry.job)));
            AomLog.Detailed($"{pawn.LabelShortCap}: restoration wait state: {Describe(job)}; " +
                $"position={pawn.Position}, toil={driver?.CurToilIndex ?? -1}, " +
                $"ticksLeft={driver?.ticksLeftThisToil ?? -1}, carried={Describe(pawn.carryTracker?.CarriedThing)}, " +
                $"queueCount={pawn.jobs.jobQueue?.Count ?? 0}, queue=[{queued}], " +
                $"lastAttempt={state.LastRestorationAttemptTick}. No job changed by this diagnostic.");
            SavedGearRestorationDiagnostics.Report(pawn, state);
        }

        private static string Describe(Job job) => job == null ? "none" :
            $"{job.def?.defName ?? "cleared"} #{job.loadID}, forced={job.playerForced}, " +
            $"targetA={job.targetA}, targetB={job.targetB}";

        private static string Describe(Thing thing) => thing == null ? "none" :
            $"{thing.LabelCap} [{thing.ThingID}]";
    }
}
