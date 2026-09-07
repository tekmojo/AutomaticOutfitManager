using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    internal static class RestorationJobOrder
    {
        internal static List<Job> Order(Pawn pawn, Area locker, List<Job> jobs,
            ISet<Job> replacements)
        {
            // All removals/drops precede retrieval. Equip must never get sorted
            // ahead of dropping the temporary primary. Preserve cleanup order.
            // Exact saved apparel AND weapons precede optional replacements.
            // Keep locker items together, then prefer nearer targets within
            // each group. This changes neither targets nor ownership/permissions.
            return jobs.OrderBy(job => IsRetrieval(job)
                    ? replacements.Contains(job) ? 2 : 1 : 0)
                .ThenBy(job => !IsRetrieval(job) ? 0 :
                    locker?.Map == pawn.Map && job.targetA.Thing?.MapHeld == pawn.Map &&
                    locker[job.targetA.Thing.PositionHeld] ? 0 : 1)
                .ThenBy(job => IsRetrieval(job) && job.targetA.Thing?.MapHeld == pawn.Map
                    ? pawn.Position.DistanceToSquared(job.targetA.Thing.PositionHeld) : 0)
                .ToList();
        }

        private static bool IsRetrieval(Job job) =>
            job?.def == JobDefOf.Wear || job?.def == JobDefOf.Equip;
    }
}
