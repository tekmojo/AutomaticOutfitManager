using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    internal static class ConstructionChildAccess
    {
        private sealed class Selection
        {
            internal Pawn Pawn;
            internal Thing Target;
            internal List<ApparelRule> Rules;
            internal IntVec3 Start;
        }

        // The scanner has a prospective Job; CurJob still belongs to earlier
        // work. Scope only this synchronous native cell search, never save it.
        [ThreadStatic] private static Selection selection;

        private static bool Applies(Pawn pawn, Job job) =>
            pawn?.Spawned == true && ChildAreaAccessPolicy.IsChild(pawn) &&
            PausedAreaWorkFilter.IsManagedPawn(pawn) && job != null &&
            !job.playerForced && !NativeRuleControl.Suspends(pawn, job);

        private static bool IsConstruction(Thing thing, Pawn pawn) =>
            (thing is Frame || thing is Blueprint) && thing.Spawned && thing.Map == pawn.Map;

        private static Selection For(Pawn pawn, Thing target)
        {
            if (!IsConstruction(target, pawn)) return null;
            var rules = RuleEvaluator.EnabledRulesForMap(pawn.Map)
                .Where(rule => ChildAreaAccessPolicy.Disallows(pawn, rule)).ToList();
            if (rules.Count == 0) return null;
            // Ordinary remote construction should not incur per-cell path
            // probes. This fix concerns a worksite on a denied area boundary.
            if (!target.OccupiedRect().ExpandedBy(1).Any(cell =>
                    cell.InBounds(pawn.Map) && rules.Any(rule => rule.Area[cell])))
                return null;
            return new Selection { Pawn = pawn, Target = target, Rules = rules, Start = pawn.Position };
        }

        internal static bool RejectsDelivery(Pawn pawn, Job job) =>
            TryGetRouteRestriction(pawn, job, out ApparelRule denied) && denied != null;

        // Admission and periodic activity enforcement must ask the same question.
        // Generic Touch routes can choose the forbidden side of a boundary frame;
        // GotoBuild instead uses the native adjacent-cell picker patched below.
        internal static bool TryGetRouteRestriction(Pawn pawn, Job job, out ApparelRule denied)
        {
            denied = null;
            if (!Applies(pawn, job) || job.def != JobDefOf.HaulToContainer ||
                !IsConstruction(job.targetB.Thing, pawn)) return false;
            var recipients = new List<Thing> { job.targetB.Thing };
            if (job.targetQueueB != null)
                recipients.AddRange(job.targetQueueB.Select(target => target.Thing));
            if (!recipients.Any(target => For(pawn, target) != null)) return false;

            var rules = RuleEvaluator.EnabledRulesForMap(pawn.Map)
                .Where(rule => ChildAreaAccessPolicy.Disallows(pawn, rule)).ToList();
            var origins = new List<IntVec3> { pawn.Position };
            var pickups = new List<Thing> { job.targetA.Thing };
            if (job.targetQueueA != null)
                pickups.AddRange(job.targetQueueA.Select(target => target.Thing));
            foreach (Thing source in pickups.Distinct())
            {
                // After native pickup the held item is not a new destination.
                // Retain any remaining spawned source queue: native hauling can
                // still collect more stock before travelling to the construction.
                if (source == null || source == pawn.carryTracker?.CarriedThing || !source.Spawned)
                    continue;
                if (source.Map != pawn.Map) return false; // native validity owns this
                if (!ProtectedPathAvoidance.SegmentAvoidsRules(pawn, pawn.Position,
                        source, rules, exactEndMode: PathEndMode.Touch, allowInitialEgress: true))
                {
                    denied = rules.FirstOrDefault(rule =>
                        !ProtectedPathAvoidance.SegmentAvoidsRules(pawn, pawn.Position,
                            source, new List<ApparelRule> { rule }, exactEndMode: PathEndMode.Touch,
                            allowInitialEgress: true)) ?? rules[0];
                    ReportDenial(pawn, job, "material pickup", source, pawn.Position);
                    return true;
                }
                origins.Add(source.Position);
            }
            foreach (Thing target in recipients.Distinct())
            {
                if (!IsConstruction(target, pawn)) return false;
                foreach (IntVec3 start in origins.Distinct())
                {
                    var context = new Selection { Pawn = pawn, Target = target, Rules = rules, Start = start };
                    if (RecipientPermitted(context)) continue;
                    // Identify an individually blocking rule when possible.
                    // If only the combination blocks every route, retain a
                    // representative rule and report the complete restricted set.
                    denied = rules.FirstOrDefault(rule => !RecipientPermitted(new Selection {
                        Pawn = pawn, Target = target, Rules = new List<ApparelRule> { rule }, Start = start
                    })) ?? rules[0];
                    ReportDenial(pawn, job, "construction approach", target, start);
                    return true;
                }
            }
            return true;
        }

        private static bool RecipientPermitted(Selection context)
        {
            Selection previous = selection;
            selection = context;
            try
            {
                // False makes native GotoBuild try the footprint cell. Validate
                // that fallback too, using the same origin as the candidate pass.
                RCellFinder.TryFindGoodAdjacentSpotToTouch(context.Pawn, context.Target, out IntVec3 cell);
                return Permitted(context, cell);
            }
            finally { selection = previous; }
        }

        private static void ReportDenial(Pawn pawn, Job job, string leg, Thing target, IntVec3 start)
        {
            if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(pawn,
                    $"child-construction-route:{job.loadID}:{leg}", 600))
                AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: child construction route rejected; " +
                    $"job {job.def.defName} #{job.loadID}, leg={leg}, target={target.ThingID}, " +
                    $"position={pawn.Position}, routeStart={start}, carrying={pawn.carryTracker?.CarriedThing?.ThingID ?? "none"}, " +
                    $"restricted areas=[{string.Join(", ", RuleEvaluator.EnabledRulesForMap(pawn.Map).Where(r => ChildAreaAccessPolicy.Disallows(pawn, r)).Select(r => r.Name))}].");
        }

        internal static IEnumerable<IntVec3> FilterCells(
            IEnumerable<IntVec3> cells, Pawn pawn, Thing target)
        {
            Selection context = selection;
            if (context == null || context.Pawn != pawn || context.Target != target)
            {
                Job job = pawn?.CurJob;
                if (!Applies(pawn, job) ||
                    !((job.def == JobDefOf.HaulToContainer && job.targetB.Thing == target) ||
                      (job.def == JobDefOf.FinishFrame && job.targetA.Thing == target)))
                    return cells;
                context = For(pawn, target);
            }
            return context == null ? cells : cells.Where(cell => Permitted(context, cell));
        }

        private static bool Permitted(Selection context, IntVec3 cell)
        {
            Pawn pawn = context.Pawn;
            return cell.IsValid && cell.InBounds(pawn.Map) &&
                !context.Rules.Any(rule => rule.Area[cell]) &&
                cell.WalkableBy(pawn.Map, pawn) &&
                pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly) &&
                ProtectedPathAvoidance.SegmentAvoidsRules(pawn, context.Start, cell,
                    context.Rules, exactEndMode: PathEndMode.OnCell, allowInitialEgress: true);
        }
    }

    // Both HasJobOnThing and JobOnThing call this shared generator. Inspect
    // all real recipients before selection promises a job or picks up stock.
    [HarmonyPatch(typeof(WorkGiver_ConstructDeliverResources), "ResourceDeliverJobFor")]
    internal static class ConstructionChildDelivery_Patch
    {
        private static void Postfix(Pawn __0, bool __3, ref Job __result)
        {
            if (!__3 && __result != null && ConstructionChildAccess.RejectsDelivery(__0, __result))
                __result = null;
        }
    }

    [HarmonyPatch(typeof(RCellFinder), nameof(RCellFinder.TryFindGoodAdjacentSpotToTouch))]
    internal static class ConstructionChildDestination_Patch
    {
        // Filter the native enumeration in BOTH passes, preserving its own
        // standability, reachability, corner-touch checks and ranking. Filtering
        // only the final answer would hide a valid approach on another side.
        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var adjacent = AccessTools.Method(typeof(GenAdj), nameof(GenAdj.CellsAdjacent8Way), new[] { typeof(Thing) });
            var filter = AccessTools.Method(typeof(ConstructionChildAccess), nameof(ConstructionChildAccess.FilterCells));
            int patched = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (!instruction.Calls(adjacent)) continue;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Call, filter);
                patched++;
            }
            if (patched != 2)
                throw new InvalidOperationException("AOM: construction destination search no longer has its two native candidate passes.");
        }
    }
}
