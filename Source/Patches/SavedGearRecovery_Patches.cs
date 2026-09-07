using AutomaticOutfitManager.Detection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    [HarmonyPatch(typeof(WorkGiver_Haul), nameof(WorkGiver_Haul.JobOnThing))]
    internal static class SavedGearRecovery_HaulProbe_Patch
    {
        private static void Prefix(Pawn pawn, Thing t, bool forced, out SavedGearRecovery.Probe __state) =>
            __state = SavedGearRecovery.Begin(pawn, t, forced);

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Pawn pawn, Thing t, ref Job __result)
        {
            if (!SavedGearRecovery.MatchesProbe(pawn, t)) return;
            if (__result == null)
            {
                SavedGearRecovery.ReportQuery(pawn, t, null, false);
                return;
            }
            // Native storage hauling normally collects opportunistic duplicates.
            // This exception owns only one non-stackable saved instance; never
            // widen it to neighboring stock or split/merge its saved identity.
            if (__result.def == JobDefOf.HaulToCell && __result.targetA.Thing == t)
            {
                __result.count = 1;
                __result.haulOpportunisticDuplicates = false;
            }
            bool rejected = !SavedGearRecovery.AllowsHaul(pawn, __result, SavedGearRecovery.CurrentProbe.Owner, t);
            if (rejected)
                __result = null;
            SavedGearRecovery.ReportQuery(pawn, t, __result, rejected);
        }

        // Also restore the previous scope when native or compatibility code throws.
        private static void Finalizer(SavedGearRecovery.Probe __state) => SavedGearRecovery.CurrentProbe = __state;
    }

    [HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.PawnCanAutomaticallyHaulFast),
        typeof(Pawn), typeof(Thing), typeof(bool))]
    internal static class SavedGearRecovery_NativeEligibility_Patch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Pawn __0, Thing __1, bool __result)
        {
            if (!SavedGearRecovery.MatchesProbe(__0, __1)) return;
            SavedGearRecovery.CurrentProbe.EligibilityChecked = true;
            SavedGearRecovery.CurrentProbe.EligibleForNativeHaul = __result;
        }
    }

    [HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.IsGoodStoreCell))]
    internal static class SavedGearRecovery_StoreCell_Patch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(IntVec3 c, Thing t, Pawn carrier, ref bool __result)
        {
            bool nativeAccepted = __result;
            if (__result && !SavedGearRecovery.AcceptsCell(carrier, t, c)) __result = false;
            if (SavedGearRecovery.MatchesProbe(carrier, t))
            {
                var probe = SavedGearRecovery.CurrentProbe;
                probe.CheckedCells++;
                if (!nativeAccepted) probe.NativeRejectedCells++;
                else if (!__result) probe.OwnerRejectedCells++;
            }
        }
    }

    [HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterNonSlotGroupStorageFor))]
    internal static class SavedGearRecovery_Container_Patch
    {
        // This recovery is only to reachable slot storage, never into another
        // holder that the saved Wear/Equip continuation cannot retrieve from.
        private static bool Prefix(Thing t, Pawn carrier, ref bool __result)
        {
            if (SavedGearRecovery.StorageSearchOwner(carrier, t) == null) return true;
            __result = false;
            return false;
        }
    }
}
