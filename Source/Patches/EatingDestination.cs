using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    internal static class EatingDestination
    {
        // Native Ingest chooses its dining cell after picking up target A.
        // Recognize that exact carried meal's route, not unrelated path probes
        // or another driver's interpretation of targets. Boundary outfit and
        // activity checks still own permission to enter the destination rule.
        internal static bool IsCurrentMealDestination(Pawn pawn, Job job,
            LocalTargetInfo? destination, Area area)
        {
            if (pawn?.Map == null || pawn.CurJob != job ||
                job?.def != JobDefOf.Ingest || job.def.driverClass != typeof(JobDriver_Ingest) ||
                job.targetA.Thing == null || job.targetA.Thing != pawn.carryTracker?.CarriedThing ||
                area?.Map != pawn.Map || !destination.HasValue) return false;
            LocalTargetInfo target = destination.Value;
            return target.IsValid && (!target.HasThing || target.Thing.MapHeld == pawn.Map) &&
                target.Thing != job.targetA.Thing && target.Cell.IsValid &&
                target.Cell.InBounds(pawn.Map) && area[target.Cell];
        }
    }
}
