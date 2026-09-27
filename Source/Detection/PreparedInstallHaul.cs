using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    internal static class PreparedInstallHaul
    {
        // Native pickup consumes count. If a protected-boundary interruption
        // drops that minified building, replay starts pickup again rather than
        // continuing the old driver's carrying toil. Only repair the exact
        // single installation item; ordinary resource counts remain native.
        internal static bool RepairPickupCount(Job job)
        {
            if (job?.def != JobDefOf.HaulToContainer || job.playerForced || job.count > 0 ||
                !(job.targetA.Thing is MinifiedThing item) || item.Destroyed ||
                !item.Spawned || item.stackCount != 1 ||
                !(job.targetB.Thing is Blueprint_Install blueprint) ||
                blueprint.Destroyed || !blueprint.Spawned || blueprint.Map != item.Map ||
                blueprint.MiniToInstallOrBuildingToReinstall != item)
                return false;

            job.count = 1;
            return true;
        }
    }
}
