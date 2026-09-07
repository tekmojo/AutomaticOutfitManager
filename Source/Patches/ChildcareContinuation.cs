using System.Runtime.CompilerServices;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.State;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    internal static class ChildcareContinuation
    {
        private sealed class Entry
        {
            internal Pawn Caregiver, Baby;
            internal Map Map;
            internal int LoadId;
        }
        private static ConditionalWeakTable<Job, Entry> entries = new ConditionalWeakTable<Job, Entry>();
        internal static void ResetForLoadedGame() => entries = new ConditionalWeakTable<Job, Entry>();

        internal static bool Admit(Pawn pawn, Job job)
        {
            if (pawn?.Spawned != true || pawn.Dead || pawn.Downed || pawn.Drafted || pawn.InMentalState ||
                job?.targetA.Thing is not Pawn baby || baby.Dead ||
                baby.RaceProps?.Humanlike != true || baby.DevelopmentalStage != DevelopmentalStage.Baby ||
                (job.def?.defName != "BringBabyToSafety" && job.def?.defName != "BringBabyToSafetyUnforced"))
                return false;

            // Preserve ownership through the final placement toil, including
            // the interval after the baby leaves the carry tracker.
            if (Owns(pawn, job)) return true;
            if (pawn.carryTracker?.CarriedThing != baby) return false;
            var component = AutomaticOutfitManagerGameComponent.Current;
            if (component?.StateFor(pawn) == null && NonWorkBufferTracker.For(pawn) == null)
                return false;
            entries.Remove(job);
            entries.Add(job, new Entry { Caregiver=pawn, Baby=baby, Map=pawn.Map, LoadId=job.loadID });
            if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(pawn, "childcare-before-outfit-return", 600))
                AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: completing the native safe placement of {baby.LabelShortCap} in the current outfit; no extra buffer credit.");
            return true;
        }

        private static bool Owns(Pawn pawn, Job job) => job != null && entries.TryGetValue(job, out Entry entry) &&
            entry.Caregiver == pawn && entry.Map == pawn?.Map && entry.LoadId == job.loadID &&
            job.targetA.Thing == entry.Baby &&
            (job.def?.defName == "BringBabyToSafety" || job.def?.defName == "BringBabyToSafetyUnforced");

        internal static bool End(Pawn pawn, Job job, JobCondition condition)
        {
            if (!Owns(pawn, job)) return false;
            entries.Remove(job);
            if (AomLog.DetailedEnabled)
                AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: childcare continuation ended ({condition}); " +
                    (pawn.carryTracker?.CarriedThing == job.targetA.Thing
                        ? "baby still carried; native childcare recovery remains responsible."
                        : "baby no longer carried; normal outfit and buffer evaluation resumes."));
            return true;
        }
    }
}
