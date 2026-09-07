using System.Linq;
using System.Runtime.CompilerServices;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    // Capture only the request returned to this path follower: general route
    // probes also call CreateRequest and must not replace its travel evidence.
    internal static class ProtectedRouteFailureDiagnostics
    {
        private sealed class Snapshot
        {
            internal Pawn Pawn;
            internal Map Map;
            internal int JobId;
            internal IntVec3 Destination;
            internal string Description;
        }
        private static ConditionalWeakTable<Pawn_PathFollower, Snapshot> requests =
            new ConditionalWeakTable<Pawn_PathFollower, Snapshot>();
        private static readonly AccessTools.FieldRef<Pawn_PathFollower, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_PathFollower, Pawn>("pawn");
        internal static void ResetForLoadedGame() => requests = new ConditionalWeakTable<Pawn_PathFollower, Snapshot>();

        internal static void Capture(Pawn_PathFollower follower, PathRequest request)
        {
            requests.Remove(follower);
            if (!AomLog.DetailedEnabled || request?.pawn?.CurJob == null) return;
            Pawn pawn = request.pawn;
            Job job = pawn.CurJob;
            var rules = RuleEvaluator.EnabledRulesForMap(request.map);
            if (rules.Count == 0) return;
            IntVec3 dest = request.Target.Cell;
            string destinationRules = string.Join("; ", rules.Where(rule =>
                dest.IsValid && dest.InBounds(request.map) && rule.Area[dest]).Select(rule =>
                    $"{rule.Name} [{rule.Id}]: activity=" +
                    (PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(pawn, job, rule) ? "allowed" : "denied") +
                    $", missingOutfit={RuleEvaluator.HasMissingRequiredGear(pawn, rule)}"));
            requests.Add(follower, new Snapshot {
                Pawn=pawn, Map=request.map, JobId=job.loadID, Destination=dest,
                Description=$"request tick {Find.TickManager?.TicksGame ?? 0}: {request.Start} -> {request.Target}, " +
                    $"mode={request.EndMode}; " +
                    $"job {job.def.defName} #{job.loadID}, targets {job.targetA}/{job.targetB}/{job.targetC}, " +
                    $"carried={pawn.carryTracker?.CarriedThing?.ThingID ?? "none"}; " +
                    $"actual customizer={ProtectedPathAvoidance.DescribeCustomizer(request.customizer)}; " +
                    $"destination rules=[{destinationRules}]"
            });
        }

        internal static void Failed(Pawn_PathFollower follower)
        {
            if (!AomLog.DetailedEnabled) return;
            Pawn pawn = PawnField(follower);
            Job job = pawn?.CurJob;
            if (pawn?.Map == null || job?.def == null) return;
            requests.TryGetValue(follower, out Snapshot snapshot);
            if (snapshot == null || snapshot.Pawn != pawn || snapshot.Map != pawn.Map ||
                snapshot.JobId != job.loadID || snapshot.Destination != follower.Destination.Cell) return;
            if (!AomLog.ShouldLogDetailed(pawn, $"protected-path-failure:{job.def.defName}:{follower.Destination}", 600)) return;
            AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: native path failed; " +
                snapshot.Description + ". Captured when the travel request was created; access checks unchanged.");
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "GenerateNewPathRequest")]
    internal static class PathFollower_CaptureActualRequest_Patch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Pawn_PathFollower __instance, PathRequest __result) =>
            ProtectedRouteFailureDiagnostics.Capture(__instance, __result);
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "PatherFailed")]
    internal static class PathFollower_ReportActualRequest_Patch
    {
        private static void Prefix(Pawn_PathFollower __instance) =>
            ProtectedRouteFailureDiagnostics.Failed(__instance);
    }
}
