using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    internal static class ConstructionDestination
    {
        // A construction target's footprint need not include the cell its
        // driver chooses to stand on. Admit only that current adjacent route;
        // activity and outfit checks still run at the actual area boundary.
        internal static bool IsCurrentDestination(Pawn pawn, Job job,
            LocalTargetInfo? destination, Area area)
        {
            if (pawn?.Map == null || pawn.CurJob != job || job == null ||
                area?.Map != pawn.Map || !destination.HasValue || pawn.pather == null)
                return false;

            LocalTargetInfo target = destination.Value;
            if (!target.IsValid || target.HasThing || !target.Cell.InBounds(pawn.Map) ||
                !area[target.Cell] || pawn.pather.Destination.HasThing ||
                pawn.pather.Destination.Cell != target.Cell)
                return false;

            Thing building;
            if (job.def == JobDefOf.HaulToContainer &&
                pawn.carryTracker?.CarriedThing != null &&
                (job.targetB.Thing is Frame || job.targetB.Thing is Blueprint))
                building = job.targetB.Thing;
            else if (job.def == JobDefOf.FinishFrame && job.targetA.Thing is Frame)
                building = job.targetA.Thing;
            else
                return false;

            return building.Spawned && building.Map == pawn.Map &&
                building.OccupiedRect().ExpandedBy(1).Contains(target.Cell);
        }
    }
}
