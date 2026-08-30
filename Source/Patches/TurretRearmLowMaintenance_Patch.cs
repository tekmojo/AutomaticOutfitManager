using System;
using HarmonyLib;
using RimWorld;

namespace AutomaticOutfitManager.Patches
{
    /// <summary>
    /// RimWorld scales a turret's fuel-per-item by dividing by the custom
    /// maintenance-cost factor, then defines IsFull as "cannot accept one
    /// complete item." At very low maintenance costs (for example 1%), one
    /// steel or uranium becomes 50 fuel. That makes a 30-capacity uranium
    /// turret report full even at 0/30 and prevents every native rearm job.
    /// Keep the compatibility correction limited to the two vanilla rearmable
    /// heavy turrets and use their real target rather than the oversized item
    /// increment. Native thresholds, fuel counts, reservations, and jobs still
    /// decide when and how rearming happens.
    /// </summary>
    [HarmonyPatch(
        typeof(CompRefuelable), nameof(CompRefuelable.IsFull),
        MethodType.Getter)]
    internal static class CompRefuelable_TurretLowMaintenanceFullness_Patch
    {
        private const float FuelEpsilon = 0.001f;

        private static void Postfix(
            CompRefuelable __instance, ref bool __result)
        {
            if (!__result || __instance?.parent is not Building_Turret turret ||
                turret.Faction != Faction.OfPlayerSilentFail ||
                __instance.Props?.factorByDifficulty != true)
            {
                return;
            }

            string defName = turret.def?.defName ?? string.Empty;
            if (!defName.Equals(
                    "Turret_Autocannon", StringComparison.OrdinalIgnoreCase) &&
                !defName.Equals(
                    "Turret_Sniper", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (__instance.Fuel + FuelEpsilon < __instance.TargetFuelLevel)
                __result = false;
        }
    }
}
