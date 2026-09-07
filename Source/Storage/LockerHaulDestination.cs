using System;
using AutomaticOutfitManager.Detection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutomaticOutfitManager.Storage
{
    // Filter cells inside native storage selection, before it commits to one
    // destination. This keeps HasJobOnThing and JobOnThing in agreement even
    // when a different pawn owns the nearest cell during outfit preparation.
    internal static class LockerHaulDestination
    {
        internal sealed class Search
        {
            internal Pawn Pawn;
            internal Thing Gear;
            internal Area Locker;
            internal bool Forced;
        }

        [ThreadStatic] internal static Search Current;

        internal static bool TryFind(Pawn pawn, Thing gear, Area locker,
            ISlotGroup group, bool forced, out IntVec3 destination)
        {
            Search previous = Current;
            Current = new Search { Pawn = pawn, Gear = gear, Locker = locker, Forced = forced };
            try
            {
                return StoreUtility.TryFindBestBetterStoreCellForIn(gear, pawn, pawn.Map,
                    StoragePriority.Unstored, pawn.Faction, group, out destination);
            }
            finally { Current = previous; }
        }

        internal static bool Accepts(Pawn pawn, Thing gear, IntVec3 cell)
        {
            Search search = Current;
            if (search == null || search.Pawn != pawn || search.Gear != gear) return true;
            return cell.IsValid && cell.InBounds(pawn.Map) &&
                search.Locker != null && search.Locker.Map == pawn.Map && search.Locker[cell] &&
                (search.Forced || !ManagedWorkClaimRegistry.IsClaimedByOther(pawn, pawn.Map, null, cell));
        }
    }

    [HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.IsGoodStoreCell))]
    internal static class LockerHaulDestination_Patch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(IntVec3 c, Thing t, Pawn carrier, ref bool __result)
        {
            if (__result && !LockerHaulDestination.Accepts(carrier, t, c)) __result = false;
        }
    }
}
