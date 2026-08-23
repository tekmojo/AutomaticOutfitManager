using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Storage;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutomaticOutfitManager.Patches
{
    [HarmonyPatch(typeof(Apparel), nameof(Apparel.GetInspectString))]
    public static class Apparel_GetInspectString_Patch
    {
        public static void Postfix(Apparel __instance, ref string __result)
        {
            AutomaticOutfitManagerGameComponent component = AutomaticOutfitManagerGameComponent.Current;
            if (__instance == null || component == null)
                return;

            string managedLabel = null;
            string savedOwner = component.SavedOwnerFor(__instance);
            if (!string.IsNullOrEmpty(savedOwner))
            {
                managedLabel =
                    $"Automatic Outfit Manager: Saved personal apparel — {savedOwner}";
            }
            else
            {
                var matchingRules = component.Rules
                    .Where(rule => rule != null &&
                                   rule.Enabled &&
                                   rule.RequiredApparel != null &&
                                   rule.RequiredApparel.Contains(__instance.def))
                    .ToList();
                var workAreas = matchingRules
                    .Where(rule => rule.Area != null)
                    .Select(rule => rule.Area.Label)
                    .Distinct()
                    .ToList();
                var lockerAreas = matchingRules
                    .Where(rule => rule.ChangingArea != null)
                    .Select(rule => rule.ChangingArea.Label)
                    .Distinct()
                    .ToList();

                if (matchingRules.Count > 0)
                {
                    managedLabel = "Automatic Outfit Manager: Required work apparel";
                    if (workAreas.Count > 0)
                        managedLabel += $"\nRequired in: {string.Join(", ", workAreas)}";
                    if (lockerAreas.Count > 0)
                        managedLabel += $"\nLocker room: {string.Join(", ", lockerAreas)}";
                }
                else if (component.IsManagedApparelDefinition(__instance.def))
                {
                    managedLabel =
                        "Automatic Outfit Manager: Managed apparel stock — retained for locker storage";
                }
                else if (component.IsManagedApparel(__instance))
                {
                    managedLabel = "Automatic Outfit Manager: Managed work apparel";
                }
            }

            if (managedLabel == null)
                return;

            __result = string.IsNullOrEmpty(__result)
                ? managedLabel
                : __result + "\n" + managedLabel;
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetInspectString))]
    public static class Weapon_GetInspectString_Patch
    {
        public static void Postfix(ThingWithComps __instance, ref string __result)
        {
            if (__instance?.def?.IsWeapon != true)
                return;

            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            if (component == null)
                return;

            string managedLabel = null;
            Pawn savedOwner = component.SavedPawnForWeapon(__instance);
            if (savedOwner != null)
            {
                managedLabel = $"Automatic Outfit Manager: Saved primary weapon — {savedOwner.LabelShortCap}";
            }
            else
            {
                var matchingRules = component.Rules
                    .Where(rule => rule?.Enabled == true &&
                                   rule.UsesExactWeapons &&
                                   rule.RequiredWeapons.Contains(__instance.def))
                    .ToList();
                var workAreas = matchingRules
                    .Where(rule => rule.Area != null)
                    .Select(rule => rule.Area.Label)
                    .Distinct()
                    .ToList();
                var lockerAreas = matchingRules
                    .Where(rule => rule.ChangingArea != null)
                    .Select(rule => rule.ChangingArea.Label)
                    .Distinct()
                    .ToList();

                if (matchingRules.Count > 0)
                {
                    managedLabel = "Automatic Outfit Manager: Required primary weapon";
                    if (workAreas.Count > 0)
                        managedLabel += $"\nRequired in: {string.Join(", ", workAreas)}";
                    if (lockerAreas.Count > 0)
                        managedLabel += $"\nLocker room: {string.Join(", ", lockerAreas)}";
                }
                else if (component.IsManagedWeaponDefinition(__instance.def))
                {
                    managedLabel =
                        "Automatic Outfit Manager: Managed weapon stock — retained for locker storage";
                }
                else if (component.IsManagedWeapon(__instance))
                {
                    managedLabel = "Automatic Outfit Manager: Managed work weapon";
                }
            }

            if (managedLabel == null)
                return;

            __result = string.IsNullOrEmpty(__result)
                ? managedLabel
                : __result + "\n" + managedLabel;
        }
    }
}
