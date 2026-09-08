using AutomaticOutfitManager.Detection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    // Both native construction HasJobOnThing and JobOnThing call this shared
    // generator. Checking the complete delivery here includes its secondary
    // sources/recipients before HasJob promises a job for a free blueprint.
    // No second speculative scan or cached/serialized Job is introduced.
    [HarmonyPatch(typeof(WorkGiver_ConstructDeliverResources), "ResourceDeliverJobFor")]
    internal static class ConstructionDeliveryClaims_Patch
    {
        private static void Postfix(Pawn __0, bool __3, ref Job __result)
        {
            if (__result != null && !__3 && ManagedWorkCandidateFilter.Rejects(__0, __result))
                __result = null;
        }
    }

    // Frames and blueprints can first clear an obstructing item/plant/building.
    // That early native branch never reaches ResourceDeliverJobFor. Filter its
    // complete job at the shared generator too, before HasJob promises success.
    [HarmonyPatch(typeof(GenConstruct), nameof(GenConstruct.HandleBlockingThingJob))]
    internal static class ConstructionBlockingClaims_Patch
    {
        private static void Postfix(Pawn __1, bool __2, ref Job __result)
        {
            if (__result != null && !__2 && ManagedWorkCandidateFilter.Rejects(__1, __result))
                __result = null;
        }
    }
}
