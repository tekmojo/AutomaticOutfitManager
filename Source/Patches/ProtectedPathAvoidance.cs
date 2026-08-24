using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using HarmonyLib;
using RimWorld;
using Unity.Collections;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    /// <summary>
    /// Makes a disabled activity route around a protected area when the job's
    /// concrete targets are outside it. RimWorld 1.6 supports a per-request path
    /// grid customizer, so outside work can retain its native job, reservations,
    /// and driver while protected cells are made impassable for that route.
    /// </summary>
    internal static class ProtectedPathAvoidance
    {
        private const ushort BlockedPathCost = 10000;

        private sealed class AreaFingerprint
        {
            public int Tick;
            public int TrueCount;
            public int Hash;
        }

        private sealed class ProtectedAreaGrid : PathRequest.IPathGridCustomizer, IDisposable
        {
            private NativeArray<ushort> grid;

            public ProtectedAreaGrid(Map map, IEnumerable<ApparelRule> rules)
            {
                grid = new NativeArray<ushort>(
                    map.cellIndices.NumGridCells,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);

                foreach (ApparelRule rule in rules)
                {
                    foreach (IntVec3 cell in rule.Area.ActiveCells)
                    {
                        if (cell.IsValid && cell.InBounds(map))
                            grid[map.cellIndices.CellToIndex(cell)] = BlockedPathCost;
                    }
                }
            }

            public NativeArray<ushort> GetOffsetGrid() => grid;

            public void Dispose()
            {
                if (grid.IsCreated)
                    grid.Dispose();
            }
        }

        private static readonly Dictionary<Area, AreaFingerprint> Fingerprints =
            new Dictionary<Area, AreaFingerprint>();
        private static readonly Dictionary<string, ProtectedAreaGrid> Grids =
            new Dictionary<string, ProtectedAreaGrid>();

        [ThreadStatic]
        private static bool suppressAutomaticCustomizer;

        public static bool SuppressAutomaticCustomizer => suppressAutomaticCustomizer;

        public static void ResetForLoadedGame()
        {
            foreach (ProtectedAreaGrid grid in Grids.Values)
                grid.Dispose();
            Grids.Clear();
            Fingerprints.Clear();
        }

        public static PathRequest.IPathGridCustomizer CustomizerFor(Pawn pawn, Job job)
        {
            List<ApparelRule> rules = RestrictedTransitRules(pawn, job);
            return rules.Count == 0 ? null : GridFor(pawn.Map, rules);
        }

        public static bool RouteRequiresRestrictedArea(
            Pawn pawn, Job job, IEnumerable<ApparelRule> restrictedRules)
        {
            List<ApparelRule> rules = restrictedRules?
                .Where(rule => rule?.Enabled == true && rule.Area?.Map == pawn?.Map)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList() ?? new List<ApparelRule>();
            if (pawn?.Map == null || job == null || rules.Count == 0)
                return false;

            if (rules.Any(rule => RuleEvaluator.JobTargetsArea(job, rule.Area)))
                return true;
            if (!rules.Any(rule => JobPathCrossesArea(pawn, job, rule.Area)))
                return false;

            return !RouteExistsAvoiding(pawn, job, rules);
        }

        public static bool JobPathCrossesArea(Pawn pawn, Job job, Area area)
        {
            if (pawn?.Map == null || job == null || area?.Map != pawn.Map)
                return false;

            if (!PausedAreaWorkFilter.IsHaulingJob(job))
            {
                LocalTargetInfo target = FirstDestination(job);
                return target.IsValid &&
                       SegmentCrossesArea(pawn, pawn.Position, target, area);
            }

            LocalTargetInfo pickup = job.targetA;
            LocalTargetInfo destination = job.targetB.IsValid
                ? job.targetB
                : job.targetC;
            if (pickup.IsValid &&
                SegmentCrossesArea(pawn, pawn.Position, pickup, area))
            {
                return true;
            }

            IntVec3 pickupCell = pickup.IsValid ? pickup.Cell : IntVec3.Invalid;
            return pickupCell.IsValid && destination.IsValid &&
                   SegmentCrossesArea(pawn, pickupCell, destination, area);
        }

        private static List<ApparelRule> RestrictedTransitRules(Pawn pawn, Job job)
        {
            Faction playerFaction = Faction.OfPlayerSilentFail;
            bool managedPawn = playerFaction != null && pawn?.Faction == playerFaction ||
                               PawnAccessClassifier.IsHostedGuest(pawn) ||
                               PawnAccessClassifier.IsColonyPrisoner(pawn);
            if (pawn?.Map == null || job == null || pawn.Drafted || !managedPawn)
            {
                return new List<ApparelRule>();
            }

            PawnApparelState state =
                AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn);
            if (state != null && state.Transition != ApparelTransition.Active)
                return new List<ApparelRule>();

            return AutomaticOutfitManagerGameComponent.Current?.Rules?
                .Where(rule => rule?.Enabled == true &&
                               rule.Area?.Map == pawn.Map &&
                               !PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(
                                   pawn, job, rule))
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList() ?? new List<ApparelRule>();
        }

        private static bool RouteExistsAvoiding(
            Pawn pawn, Job job, List<ApparelRule> rules)
        {
            PathRequest.IPathGridCustomizer customizer = GridFor(pawn.Map, rules);
            if (!PausedAreaWorkFilter.IsHaulingJob(job))
            {
                LocalTargetInfo target = FirstDestination(job);
                return !target.IsValid || SegmentFound(
                    pawn, pawn.Position, target, customizer);
            }

            LocalTargetInfo pickup = job.targetA;
            LocalTargetInfo destination = job.targetB.IsValid
                ? job.targetB
                : job.targetC;
            if (pickup.IsValid && !SegmentFound(
                    pawn, pawn.Position, pickup, customizer))
            {
                return false;
            }

            IntVec3 pickupCell = pickup.IsValid ? pickup.Cell : IntVec3.Invalid;
            return !pickupCell.IsValid || !destination.IsValid ||
                   SegmentFound(pawn, pickupCell, destination, customizer);
        }

        private static bool SegmentFound(
            Pawn pawn, IntVec3 start, LocalTargetInfo destination,
            PathRequest.IPathGridCustomizer customizer)
        {
            if (!start.IsValid || !start.InBounds(pawn.Map) || !destination.IsValid)
                return false;

            PathEndMode endMode = destination.HasThing
                ? PathEndMode.Touch
                : PathEndMode.OnCell;
            PawnPath path = null;
            try
            {
                path = pawn.Map.pathFinder.FindPathNow(
                    start, destination, TraverseParms.For(pawn), null,
                    endMode, customizer);
                return path?.Found == true;
            }
            finally
            {
                path?.ReleaseToPool();
            }
        }

        private static bool SegmentCrossesArea(
            Pawn pawn, IntVec3 start, LocalTargetInfo destination, Area area)
        {
            if (!start.IsValid || !start.InBounds(pawn.Map) || !destination.IsValid)
                return false;

            PathEndMode endMode = destination.HasThing
                ? PathEndMode.Touch
                : PathEndMode.OnCell;
            PawnPath path = null;
            bool previousSuppression = suppressAutomaticCustomizer;
            suppressAutomaticCustomizer = true;
            try
            {
                path = pawn.Map.pathFinder.FindPathNow(
                    start, destination, pawn, null, endMode);
                return path?.Found == true && path.NodesReversed.Any(cell =>
                    cell.IsValid && cell.InBounds(pawn.Map) && area[cell]);
            }
            finally
            {
                path?.ReleaseToPool();
                suppressAutomaticCustomizer = previousSuppression;
            }
        }

        private static LocalTargetInfo FirstDestination(Job job)
        {
            return job.targetA.IsValid
                ? job.targetA
                : job.targetB.IsValid ? job.targetB : job.targetC;
        }

        private static ProtectedAreaGrid GridFor(
            Map map, IEnumerable<ApparelRule> rules)
        {
            List<ApparelRule> orderedRules = rules
                .Where(rule => rule?.Area?.Map == map)
                .OrderBy(rule => rule.Id, StringComparer.Ordinal)
                .ToList();
            string signature = map.GetUniqueLoadID() + ":" + string.Join(
                "|", orderedRules.Select(rule =>
                    rule.Id + ":" + FingerprintFor(rule.Area)));
            if (!Grids.TryGetValue(signature, out ProtectedAreaGrid grid))
            {
                grid = new ProtectedAreaGrid(map, orderedRules);
                Grids.Add(signature, grid);
            }

            return grid;
        }

        private static int FingerprintFor(Area area)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            int trueCount = area?.TrueCount ?? 0;
            if (area != null &&
                Fingerprints.TryGetValue(area, out AreaFingerprint cached) &&
                cached.TrueCount == trueCount && now - cached.Tick < 60)
            {
                return cached.Hash;
            }

            int hash = 17;
            if (area != null)
            {
                unchecked
                {
                    foreach (IntVec3 cell in area.ActiveCells)
                        hash = (hash * 31) ^ cell.GetHashCode();
                }

                Fingerprints[area] = new AreaFingerprint
                {
                    Tick = now,
                    TrueCount = trueCount,
                    Hash = hash
                };
            }

            return hash;
        }
    }

    [HarmonyPatch]
    internal static class PathFinder_ProtectedAreaAvoidance_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(PathFinder))
                .Where(method => method.Name == "CreateRequest" &&
                                 method.ReturnType == typeof(PathRequest));
        }

        private static void Postfix(PathRequest __result)
        {
            if (__result == null || __result.customizer != null ||
                ProtectedPathAvoidance.SuppressAutomaticCustomizer)
            {
                return;
            }

            Pawn pawn = __result.pawn;
            Job job = pawn?.jobs?.curJob;
            if (job != null)
                __result.customizer = ProtectedPathAvoidance.CustomizerFor(pawn, job);
        }
    }
}
