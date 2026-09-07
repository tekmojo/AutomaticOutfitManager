using RimWorld;
using AutomaticOutfitManager.Detection;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.UI
{
    // Presentation only. Do not feed these groups into access or buffer accounting.
    internal static class PawnActivityPresentation
    {
        // Called only after the UI observes the animal in/approaching this area.
        // 0: not an animal, 1: Haulers, 2: Wanderers (including eating/resting).
        internal static int AnimalRow(Pawn pawn, Job job) => pawn?.RaceProps?.Animal == true
            ? ActivityJobClassifier.IsHauling(job) ? 1 : 2 : 0;

        internal static int Priority(Job job, bool transition, bool buffered)
        {
            if (transition) return 1;
            if (buffered) return 2;
            if (job?.def == null || job.def == JobDefOf.LayDown ||
                job.def == JobDefOf.Wait || job.def == JobDefOf.Wait_MaintainPosture) return 3;
            if (job.def == JobDefOf.Ingest || job.def.joyKind != null ||
                job.workGiverDef != null || job.jobGiver is JobGiver_Work) return 0;
            if (job.def.isIdle) return 3;
            return 0;
        }
    }
}
