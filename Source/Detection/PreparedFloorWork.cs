using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    internal static class PreparedFloorWork
    {
        internal static bool UsesBlueprintScanner(Job job) =>
            job?.def == JobDefOf.RemoveFloor &&
            job.workGiverDef?.Worker is WorkGiver_ConstructDeliverResourcesToBlueprints;

        // A blueprint scanner produces a cell-targeted prerequisite. Keep its
        // source identity separately, without changing native Job targets or
        // serializing another Job/Thing owner. An ID also avoids dangling save
        // references if the blueprint is cancelled while the pawn dresses.
        internal static int CaptureBlueprintId(Pawn pawn, Job job)
        {
            if (!UsesBlueprintScanner(job) || pawn?.Map == null ||
                job.targetA.HasThing || !job.targetA.Cell.InBounds(pawn.Map))
                return -1;

            int id = -1;
            foreach (Thing thing in pawn.Map.thingGrid.ThingsListAt(job.targetA.Cell))
            {
                if (thing is not Blueprint blueprint || !blueprint.Spawned || blueprint.Destroyed ||
                    blueprint.def?.entityDefToBuild is not TerrainDef)
                    continue;
                if (id >= 0) return -1; // Do not guess among overlapping sources.
                id = blueprint.thingIDNumber;
            }
            return id;
        }

        internal static bool TryRefresh(Pawn pawn, Job pending, int blueprintId,
            out Job refreshed, out string reason)
        {
            refreshed = null;
            reason = "the original floor blueprint is unavailable";
            if (blueprintId < 0 || !UsesBlueprintScanner(pending) || pawn?.Map == null ||
                pending.targetA.HasThing || !pending.targetA.Cell.InBounds(pawn.Map))
                return false;

            Blueprint source = null;
            foreach (Thing thing in pawn.Map.thingGrid.ThingsListAt(pending.targetA.Cell))
            {
                if (thing.thingIDNumber == blueprintId && thing is Blueprint blueprint &&
                    blueprint.Spawned && !blueprint.Destroyed && blueprint.Map == pawn.Map &&
                    blueprint.def?.entityDefToBuild is TerrainDef)
                {
                    source = blueprint;
                    break;
                }
            }
            if (source == null) return false;

            var scanner = (WorkGiver_Scanner)pending.workGiverDef.Worker;
            reason = "the native blueprint work giver no longer accepts the floor prerequisite";
            if (!scanner.HasJobOnThing(pawn, source, pending.playerForced))
                return false;
            Job candidate = scanner.JobOnThing(pawn, source, pending.playerForced);
            // A finished floor can now yield delivery or frame work. That is
            // fresh work, not permission to replay the old RemoveFloor job.
            if (candidate?.def != pending.def || candidate.targetA != pending.targetA ||
                candidate.targetB != pending.targetB || candidate.targetC != pending.targetC ||
                candidate.ignoreDesignations != pending.ignoreDesignations)
                return false;

            refreshed = candidate;
            reason = null;
            return true;
        }
    }
}
