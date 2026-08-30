using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    public static class ApparelFinder
    {
        public static Apparel FindBest(
            Pawn pawn,
            ThingDef def,
            Area changingArea = null,
            ISet<Thing> excludedThings = null,
            IEnumerable<ApparelRule> standards = null)
        {
            if (pawn?.Map == null || def == null)
                return null;

            List<ApparelRule> requiredStandards = standards?
                .Where(rule => rule != null)
                .Distinct()
                .ToList() ?? new List<ApparelRule>();
            Apparel preferred = FindClosest(
                pawn, def, changingArea, excludedThings, requiredStandards);
            return preferred ?? FindClosest(
                pawn, def, null, excludedThings, requiredStandards);
        }

        private static Apparel FindClosest(
            Pawn pawn,
            ThingDef def,
            Area area,
            ISet<Thing> excludedThings,
            IReadOnlyList<ApparelRule> standards)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(def),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                thing => thing is Apparel apparel &&
                         (excludedThings == null || !excludedThings.Contains(apparel)) &&
                         apparel.Spawned &&
                         (area == null ||
                          (area.Map == apparel.Map && area[apparel.Position])) &&
                         !apparel.IsForbidden(pawn) &&
                         !apparel.IsBurning() &&
                         standards.All(rule => rule.Allows(apparel)) &&
                         EquipmentUtility.CanEquip(apparel, pawn) &&
                         AutomaticOutfitManager.Core.AutomaticOutfitManagerGameComponent.Current?
                             .IsManagedApparelAssignedToOtherPawn(apparel, pawn) != true &&
                         ReservationUtility_SavedApparel_Patch
                             .CanReserveForOutfit(pawn, apparel)) as Apparel;
        }
    }
}
