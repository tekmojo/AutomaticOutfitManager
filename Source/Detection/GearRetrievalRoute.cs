using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    internal static class GearRetrievalRoute
    {
        internal static bool OwnsTarget(Thing item, Area area) => item != null && area?.Map != null &&
            item.MapHeld == area.Map && item.PositionHeld.IsValid && item.PositionHeld.InBounds(area.Map) &&
            (area[item.PositionHeld] || (item.Spawned && item.Map == area.Map &&
                GenAdj.CellsOccupiedBy(item).Any(cell => cell.IsValid && cell.InBounds(area.Map) && area[cell])));

        internal static bool IsSavedTarget(PawnApparelState state, Thing item) =>
            state != null && item != null && (state.OriginalWeapon == item ||
                (item is RimWorld.Apparel apparel && state.OriginalApparel?.Contains(apparel) == true));

        internal static bool IsTrackedSource(PawnApparelState state, Thing item, ApparelRule rule,
            IEnumerable<ApparelRule> planned = null) => state?.ActiveRuleId == rule.Id ||
            state?.CurrentRuleIds?.Contains(rule.Id) == true || planned?.Any(plan => plan.Id == rule.Id) == true ||
            (IsSavedTarget(state, item) && state.RestorationSourceRuleIds.Contains(rule.Id));

        internal static List<ApparelRule> RestrictedRules(Pawn pawn, Thing item,
            IEnumerable<ApparelRule> planned = null)
        {
            var state = AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn);
            return RuleEvaluator.EnabledRulesForMap(pawn?.Map).Where(rule =>
                !(pawn.Position.IsValid && pawn.Position.InBounds(pawn.Map) && rule.Area[pawn.Position]) &&
                !(IsTrackedSource(state, item, rule, planned) && OwnsTarget(item, rule.Area))).ToList();
        }

        internal static bool CanReach(Pawn pawn, Thing item, IEnumerable<ApparelRule> planned = null)
        {
            if (pawn?.Map == null || item?.Spawned != true || item.Map != pawn.Map) return false;
            var restricted = RestrictedRules(pawn, item, planned);
            return restricted.Count == 0 || ProtectedPathAvoidance.SegmentAvoidsRules(
                pawn, pawn.Position, item, restricted, exactEndMode: EndMode(item));
        }

        // Match the native drivers: Wear must stand on the garment, whereas
        // Equip goes to ClosestTouch. Touch can falsely accept a cell beside a
        // garment whose own cell is inside an unrelated protected area.
        internal static PathEndMode EndMode(Thing item) => item is RimWorld.Apparel
            ? PathEndMode.OnCell : PathEndMode.ClosestTouch;

        internal static bool CanReachStorageCell(Pawn pawn, Thing item, IntVec3 cell)
        {
            if (pawn?.Map == null || !cell.IsValid || !cell.InBounds(pawn.Map)) return false;
            var state = AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn);
            var restricted = RuleEvaluator.EnabledRulesForMap(pawn.Map).Where(rule =>
                !(pawn.Position.IsValid && pawn.Position.InBounds(pawn.Map) && rule.Area[pawn.Position]) &&
                !(IsTrackedSource(state, item, rule) && rule.Area[cell])).ToList();
            // A storage handoff must leave the exact cell accessible, even for
            // weapons. It never grants a new source-rule exemption to the owner.
            if (restricted.Any(rule => rule.Area[cell])) return false;
            return ProtectedPathAvoidance.SegmentAvoidsRules(
                pawn, pawn.Position, cell, restricted, exactEndMode: PathEndMode.OnCell);
        }
    }
}
