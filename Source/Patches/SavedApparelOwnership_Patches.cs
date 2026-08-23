using System.Collections.Generic;
using System.Reflection;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Storage;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutomaticOutfitManager.Patches
{
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), "ApparelScoreGain")]
    [HarmonyPriority(Priority.Last)]
    public static class JobGiverOptimizeApparel_ScoreGain_SavedOwner_Patch
    {
        public static void Postfix(Pawn __0, Apparel __1, ref float __result)
        {
            Pawn pawn = __0;
            Apparel apparel = __1;
            if (apparel == null || pawn == null)
                return;

            // Rule apparel belongs to the shared locker pool. It must only be
            // worn by an AutomaticOutfitManager transition (or an explicit player
            // order), never selected as a pawn's ordinary optimized outfit.
            if (ManagedApparelClassifier.Matches(apparel.def))
            {
                __result = float.MinValue;
                return;
            }

            if (AutomaticOutfitManagerGameComponent.Current?.IsSavedForOtherPawn(apparel, pawn) == true)
                __result = float.MinValue;
        }
    }

    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), "ApparelScoreRaw")]
    [HarmonyPriority(Priority.Last)]
    public static class JobGiverOptimizeApparel_ScoreRaw_SavedOwner_Patch
    {
        public static void Postfix(Pawn __0, Apparel __1, ref float __result)
        {
            Pawn pawn = __0;
            Apparel apparel = __1;
            if (apparel == null || pawn == null)
                return;

            if (ManagedApparelClassifier.Matches(apparel.def))
            {
                __result = float.MinValue;
                return;
            }

            if (AutomaticOutfitManagerGameComponent.Current?.IsSavedForOtherPawn(apparel, pawn) == true)
                __result = float.MinValue;
        }
    }

    [HarmonyPatch]
    public static class EquipmentUtility_CanEquip_SavedApparel_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(EquipmentUtility)))
            {
                if (method.Name == nameof(EquipmentUtility.CanEquip))
                    yield return method;
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Thing __0, Pawn __1, ref bool __result)
        {
            if (!__result || __1 == null)
                return;

            if (__0 is ThingWithComps weapon && weapon.def?.IsWeapon == true)
            {
                AutomaticOutfitManagerGameComponent component =
                    AutomaticOutfitManagerGameComponent.Current;
                if (component?.IsSavedWeaponForOtherPawn(weapon, __1) == true ||
                    component?.IsManagedWeaponAssignedToOtherPawn(weapon, __1) == true)
                {
                    __result = false;
                }
                return;
            }

            if (!(__0 is Apparel apparel))
                return;

            // Required work apparel is shared. Ownership only protects saved
            // personal apparel that is not itself assigned to a rule.
            if (ManagedApparelClassifier.Matches(apparel.def))
                return;

            if (AutomaticOutfitManagerGameComponent.Current?.IsSavedForOtherPawn(apparel, __1) == true)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker),
        nameof(Pawn_EquipmentTracker.AddEquipment))]
    [HarmonyPriority(Priority.First)]
    public static class PawnEquipmentTracker_AddEquipment_SavedWeapon_Patch
    {
        public static bool Prefix(
            Pawn_EquipmentTracker __instance, ThingWithComps newEq)
        {
            if (newEq?.def?.IsWeapon != true)
                return true;

            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            if (pawn?.Faction != Faction.OfPlayer &&
                (component?.IsManagedWeapon(newEq) == true ||
                 ManagedWeaponClassifier.Matches(newEq.def)) &&
                component.StateFor(pawn)?.IsManagedWeapon(newEq) != true)
            {
                return false;
            }
            return component?.IsSavedWeaponForOtherPawn(newEq, pawn) != true &&
                   component?.IsManagedWeaponAssignedToOtherPawn(newEq, pawn) != true;
        }
    }

    [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear),
        typeof(Apparel), typeof(bool), typeof(bool))]
    [HarmonyPriority(Priority.First)]
    public static class PawnApparelTracker_Wear_SavedApparel_Patch
    {
        public static bool Prefix(Pawn_ApparelTracker __instance, Apparel newApparel)
        {
            if (newApparel == null || ManagedApparelClassifier.Matches(newApparel.def))
                return true;

            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            return AutomaticOutfitManagerGameComponent.Current?.IsSavedForOtherPawn(newApparel, pawn) != true;
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetGizmos))]
    public static class Apparel_GetGizmos_SavedOwner_Patch
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, ThingWithComps __instance)
        {
            foreach (Gizmo gizmo in __result)
                yield return gizmo;

            AutomaticOutfitManagerGameComponent component = AutomaticOutfitManagerGameComponent.Current;
            if (__instance?.def?.IsWeapon == true && component != null)
            {
                Pawn weaponOwner = component.SavedPawnForWeapon(__instance);
                if (weaponOwner?.Spawned == true)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = $"Jump to {weaponOwner.LabelShortCap}",
                        defaultDesc = $"Select and center the camera on {weaponOwner.LabelShortCap}, the owner of this exact saved primary weapon.",
                        icon = TexButton.ShowImportantLocations,
                        action = () => CameraJumper.TryJumpAndSelect(weaponOwner)
                    };

                    yield return new Command_Action
                    {
                        defaultLabel = "Recall owner",
                        defaultDesc = $"Recall {weaponOwner.LabelShortCap} from managed work. They return to the locker room when configured, return managed items, and restore their exact saved apparel and primary weapon.",
                        icon = TexCommand.ClearPrioritizedWork,
                        action = () => component.RequestRecall(
                            component.StateFor(weaponOwner))
                    };

                    yield return ReleaseItemCommand(
                        __instance,
                        weaponOwner,
                        () => component.ReleaseSavedWeapon(__instance));
                }
                yield break;
            }

            Apparel apparel = __instance as Apparel;
            if (apparel == null || component == null ||
                ManagedApparelClassifier.Matches(apparel.def))
            {
                yield break;
            }

            Pawn owner = component.SavedPawnFor(apparel);
            if (owner?.Spawned != true)
                yield break;

            yield return new Command_Action
            {
                defaultLabel = $"Jump to {owner.LabelShortCap}",
                defaultDesc = $"Select and center the camera on {owner.LabelShortCap}, the owner of this exact saved apparel item.",
                icon = TexButton.ShowImportantLocations,
                action = () => CameraJumper.TryJumpAndSelect(owner)
            };

            yield return new Command_Action
            {
                defaultLabel = "Recall owner",
                defaultDesc = $"Recall {owner.LabelShortCap} from managed work. They return to the locker room when configured, return managed items, and restore their exact saved apparel and primary weapon.",
                icon = TexCommand.ClearPrioritizedWork,
                action = () => component.RequestRecall(
                    component.StateFor(owner))
            };

            yield return ReleaseItemCommand(
                apparel,
                owner,
                () => component.ClearSavedOwner(apparel));
        }

        private static Command_Action ReleaseItemCommand(
            ThingWithComps item, Pawn owner, System.Action release)
        {
            bool weapon = item?.def?.IsWeapon == true;
            string itemKind = weapon ? "saved primary weapon" : "saved apparel";
            string consequence = weapon
                ? $"{owner.LabelShortCap} will no longer restore this exact primary weapon. Automatic Outfit Manager will not choose a replacement saved weapon, so the pawn may finish restoration unarmed. The item becomes available to other pawns."
                : $"{owner.LabelShortCap} will no longer restore this exact apparel item. It becomes ordinary apparel and may be worn by another pawn.";

            return new Command_Action
            {
                defaultLabel = "Release item",
                defaultDesc = $"Permanently remove this exact {itemKind} from {owner.LabelShortCap}'s saved outfit and release it for normal use. A confirmation is required.",
                icon = TexCommand.ForbidOn,
                action = () => Find.WindowStack.Add(
                    Dialog_MessageBox.CreateConfirmation(
                        $"Release {item.LabelCap} from {owner.LabelShortCap}'s saved outfit?\n\n{consequence}",
                        release,
                        true))
            };
        }
    }
}
