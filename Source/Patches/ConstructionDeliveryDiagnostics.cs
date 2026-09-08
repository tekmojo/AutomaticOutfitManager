using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using Verse;
using Verse.AI;
using RimWorld;

namespace AutomaticOutfitManager.Patches
{
    internal static class ConstructionDeliveryDiagnostics
    {
        internal static void Report(Pawn pawn, Job job, string stage)
        {
            if (!AomLog.DetailedEnabled || pawn?.Map == null ||
                job?.def != JobDefOf.HaulToContainer ||
                !(job.targetB.Thing is Frame || job.targetB.Thing is Blueprint))
                return;

            // The frame may be outside all rules after materials leave their
            // protected source. Keep its end event even if the outfit session
            // has just cleared; otherwise this is exactly the evidence we lose.
            if (RuleEvaluator.EnabledRulesForMap(pawn.Map).Count == 0)
                return;
            if (!AomLog.ShouldLogDetailed(pawn,
                    $"construction-delivery:{job.loadID}:{stage}", 600))
                return;

            Thing destination = job.targetB.Thing;
            string received = destination is Frame frame && frame.resourceContainer != null
                ? string.Join(", ", frame.resourceContainer.Select(item =>
                    $"{item.def.defName} x{item.stackCount}"))
                : "not a live frame";
            AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                $"construction delivery {stage}; job {job.def.defName} #{job.loadID}, " +
                $"source={Describe(job.targetA.Thing)}, requested={job.count}, " +
                $"destination={Describe(destination)}, originalTarget={Describe(job.targetC.Thing)}, " +
                $"sourceQueue=[{string.Join(", ", job.targetQueueA?.Select(target => target.ToString()) ?? Enumerable.Empty<string>())}], " +
                $"recipientQueue=[{string.Join(", ", job.targetQueueB?.Select(target => target.ToString()) ?? Enumerable.Empty<string>())}], " +
                $"carried={Describe(pawn.carryTracker?.CarriedThing)}, " +
                $"frame contents=[{received}], forced={job.playerForced}.");
        }

        private static string Describe(Thing thing) => thing == null ? "none" :
            $"{thing.ThingID} ({thing.LabelCap}) x{thing.stackCount} " +
            $"at {thing.PositionHeld}, spawned={thing.Spawned}, destroyed={thing.Destroyed}";
    }
}
