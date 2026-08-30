using AutomaticOutfitManager.Core;
using HarmonyLib;
using Verse;

namespace AutomaticOutfitManager.Patches
{
    /// <summary>
    /// Final safety net for native or modded departures that bypass StartJob,
    /// including drafted, emergency, and direct ExitMap calls. The ordinary
    /// path restores the exact saved outfit at the locker first; this hook only
    /// prevents an interrupted departure from taking assigned work stock away.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
    public static class Pawn_ExitMap_ManagedGear_Patch
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(Pawn __instance)
        {
            AutomaticOutfitManagerGameComponent.Current?
                .FinalizeInterruptedMapDeparture(__instance);
        }
    }
}
