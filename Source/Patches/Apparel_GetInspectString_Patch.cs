using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Storage;
using AutomaticOutfitManager.UI;
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
            if (string.IsNullOrEmpty(savedOwner))
                savedOwner = string.Join(", ", AutomaticOutfitStorageScope.SavedFor(__instance).Select(pawn => pawn.LabelShortCap.ToString()).Distinct());
            if (!string.IsNullOrEmpty(savedOwner))
            {
                managedLabel =
                    $"Automatic saved apparel — {savedOwner}";
            }
            else
            {
                var matchingRules = GearSelectionPolicy.SelectingRules(__instance.def, component.Rules);
                var lockerAreas = matchingRules
                    .Where(rule => rule.ChangingArea != null)
                    .Select(rule => RuleTypeStyle.AreaName(rule.ChangingArea))
                    .Distinct()
                    .ToList();

                if (matchingRules.Count > 0)
                {
                    managedLabel = "Automatic outfit apparel\n" +
                        RuleTypeStyle.SourceTip(matchingRules).TrimEnd();
                    if (lockerAreas.Count > 0)
                        managedLabel += $"\nLocker Room: {string.Join(", ", lockerAreas)}";
                }
                else if (component.IsManagedApparelDefinition(__instance.def))
                {
                    managedLabel =
                        "Automatic outfit apparel — retained stock";
                }
                else if (component.IsManagedApparel(__instance))
                {
                    managedLabel = "Automatic outfit apparel — borrowed";
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
            string savedNames = savedOwner != null ? savedOwner.LabelShortCap.ToString() :
                string.Join(", ", AutomaticOutfitStorageScope.SavedFor(__instance).Select(pawn => pawn.LabelShortCap.ToString()).Distinct());
            if (!string.IsNullOrEmpty(savedNames))
            {
                managedLabel = $"Automatic saved weapons — {savedNames}";
            }
            else
            {
                var matchingRules = GearSelectionPolicy.SelectingRules(__instance.def, component.Rules);
                var lockerAreas = matchingRules
                    .Where(rule => rule.ChangingArea != null)
                    .Select(rule => RuleTypeStyle.AreaName(rule.ChangingArea))
                    .Distinct()
                    .ToList();

                if (matchingRules.Count > 0)
                {
                    managedLabel = "Automatic outfit weapons\n" +
                        RuleTypeStyle.SourceTip(matchingRules).TrimEnd();
                    if (lockerAreas.Count > 0)
                        managedLabel += $"\nLocker Room: {string.Join(", ", lockerAreas)}";
                }
                else if (component.IsManagedWeaponDefinition(__instance.def))
                {
                    managedLabel =
                        "Automatic outfit weapons — retained stock";
                }
                else if (component.IsManagedWeapon(__instance))
                {
                    managedLabel = "Automatic outfit weapons — borrowed";
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
