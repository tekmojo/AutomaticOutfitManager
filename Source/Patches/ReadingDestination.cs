using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    internal static class ReadingDestination
    {
        internal static bool IsCurrentDestination(Pawn pawn, Job job,
            LocalTargetInfo? destination, Area area)
        {
            if (pawn?.Map == null || pawn.CurJob != job ||
                job?.def != JobDefOf.Reading ||
                job.def.driverClass != typeof(JobDriver_Reading) ||
                !(job.targetA.Thing is Book) ||
                job.targetA.Thing != pawn.carryTracker?.CarriedThing ||
                area?.Map != pawn.Map || !destination.HasValue)
                return false;

            LocalTargetInfo target = destination.Value;
            if (!target.IsValid || target.HasThing ||
                !target.Cell.InBounds(pawn.Map) || !area[target.Cell])
                return false;

            // Native CarryToReadingSpot reserves its chosen cell immediately
            // before StartPath. Requiring that exact live reservation excludes
            // speculative chair/path searches and stale jobs after a reload.
            var reservation = pawn.Map.pawnDestinationReservationManager
                ?.MostRecentReservationFor(pawn);
            return reservation != null && !reservation.obsolete &&
                reservation.claimant == pawn && reservation.job == job &&
                reservation.target == target.Cell;
        }
    }
}
