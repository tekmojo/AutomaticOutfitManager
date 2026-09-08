using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using AutomaticOutfitManager.Detection;

namespace AutomaticOutfitManager.Patches
{
    // Dining cells are chosen after pickup, independently of Ingest's food
    // target. Reject a closed dining destination before native StartPath.
    internal static class PausedMealDestination
    {
        internal static bool Redirect(Pawn pawn, Job job, ref IntVec3 cell)
        {
            if (pawn?.Map == null || pawn.CurJob != job ||
                job?.def != JobDefOf.Ingest || job.def.driverClass != typeof(JobDriver_Ingest) ||
                pawn.Drafted || pawn.Downed || pawn.InMentalState || job.playerForced ||
                !PawnAccessClassifier.IsApparelEligibleHuman(pawn) ||
                PawnAccessClassifier.IsNativeCustodyEscapeActive(pawn) ||
                !cell.IsValid || !cell.InBounds(pawn.Map)) return false;

            var rules = RuleEvaluator.EnabledRulesForMap(pawn.Map);
            IntVec3 requestedCell = cell;
            if (!rules.Any(rule => rule.Area[requestedCell] &&
                    !PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(pawn, job, rule)))
                return false;

            // Avoid every unrelated protected area, not only the paused room.
            // Already-occupied areas must remain traversable for safe egress.
            var avoid = rules.Where(rule => !rule.Area[pawn.Position]).ToList();
            foreach (IntVec3 candidate in GenRadial.RadialCellsAround(pawn.Position, 24f, true))
            {
                if (!candidate.IsValid || !candidate.InBounds(pawn.Map) ||
                    !candidate.Standable(pawn.Map) || candidate.IsForbidden(pawn) ||
                    (pawn.IsPrisoner && candidate.GetRoom(pawn.Map) != pawn.GetRoom()) ||
                    !pawn.CanReserveSittableOrSpot(candidate) ||
                    rules.Any(rule => rule.Area[candidate] &&
                        (!PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(pawn, job, rule) ||
                         RuleEvaluator.HasMissingRequiredGear(pawn, rule))) ||
                    !pawn.CanReach(candidate, PathEndMode.OnCell, Danger.Some) ||
                    !ProtectedPathAvoidance.SegmentAvoidsRules(pawn, pawn.Position, candidate, avoid))
                    continue;
                cell = candidate;
                return true;
            }

            // No legal dining spot: do not issue an impossible path or end a
            // job recursively from its toil. Runtime access enforcement owns
            // the bounded exit/recall on the next simulation tick.
            cell = pawn.Position;
            return true;
        }

        internal static void BeforePath(Pawn pawn, ref LocalTargetInfo destination, PathEndMode mode)
        {
            Job job = pawn?.CurJob;
            if (mode != PathEndMode.OnCell || destination.HasThing ||
                job?.targetA.Thing == null || job.targetA.Thing != pawn.carryTracker?.CarriedThing)
                return;
            IntVec3 cell = destination.Cell;
            if (Redirect(pawn, job, ref cell)) destination = cell;
        }
    }

    [HarmonyPatch(typeof(Toils_Ingest), "TryFindChairOrSpot")]
    internal static class ToilsIngest_AccessDiningSpot_Patch
    {
        [HarmonyPriority(Priority.Last)]
        internal static void Postfix(Pawn pawn, ref IntVec3 cell, ref bool __result)
        {
            if (__result) PausedMealDestination.Redirect(pawn, pawn.CurJob, ref cell);
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "StartPath")]
    internal static class PawnPathFollower_AccessDiningSpot_Patch
    {
        private static readonly AccessTools.FieldRef<Pawn_PathFollower, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_PathFollower, Pawn>("pawn");

        [HarmonyPriority(Priority.First)]
        internal static void Prefix(Pawn_PathFollower __instance, ref LocalTargetInfo dest, PathEndMode peMode) =>
            PausedMealDestination.BeforePath(PawnField(__instance), ref dest, peMode);
    }
}
