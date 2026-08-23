using System.Reflection;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.Storage;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    [HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.SetForbidden),
        typeof(Thing), typeof(bool), typeof(bool))]
    public static class ForbidUtility_SetForbidden_Patch
    {
        public static void Postfix(Thing t, bool value)
        {
            if (!value || t == null)
                return;

            AutomaticOutfitManagerGameComponent component = AutomaticOutfitManagerGameComponent.Current;
            bool managed = t is Apparel apparel
                ? component?.IsTrackedApparel(apparel) == true ||
                  ManagedApparelClassifier.Matches(apparel)
                : t.def?.IsWeapon == true &&
                  ManagedWeaponClassifier.Matches(t);
            if (!managed)
                return;

            if (t.Spawned && t.IsForbidden(Faction.OfPlayer))
                t.SetForbidden(false, false);
        }
    }

    [HarmonyPatch(typeof(ForbidUtility), nameof(ForbidUtility.IsForbidden),
        typeof(Thing), typeof(Faction))]
    [HarmonyPriority(Priority.Last)]
    public static class ForbidUtility_IsForbidden_Patch
    {
        public static void Postfix(Thing t, Faction faction, ref bool __result)
        {
            if (!__result || faction != Faction.OfPlayer || t == null)
                return;

            if ((t is Apparel apparel && ManagedApparelClassifier.Matches(apparel)) ||
                (t.def?.IsWeapon == true && ManagedWeaponClassifier.Matches(t)))
                __result = false;
        }
    }

    [HarmonyPatch]
    [HarmonyPriority(Priority.Last)]
    public static class PawnEquipmentTracker_TryDropEquipment_Patch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(Pawn_EquipmentTracker),
                nameof(Pawn_EquipmentTracker.TryDropEquipment),
                new[]
                {
                    typeof(ThingWithComps),
                    typeof(ThingWithComps).MakeByRefType(),
                    typeof(IntVec3),
                    typeof(bool)
                });
        }

        public static void Prefix(
            Pawn_EquipmentTracker __instance,
            [HarmonyArgument(0)] ThingWithComps equipment,
            [HarmonyArgument(3)] ref bool forbid,
            out bool __state)
        {
            __state = false;
            if (!forbid || equipment?.def?.IsWeapon != true)
                return;

            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            PawnApparelState state =
                AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn);
            if (!IsAutomaticOutfitManagerDrop(pawn, state, equipment))
                return;

            // RimWorld's equipment tracker normally forbids the old primary
            // when Equip or DropEquipment places it on the ground. Automatic
            // Outfit Manager needs both the saved primary and temporary work
            // weapon to remain available for restoration and locker hauling.
            forbid = false;
            __state = true;
        }

        public static void Postfix(
            [HarmonyArgument(0)] ThingWithComps equipment,
            [HarmonyArgument(1)] ThingWithComps resultingEquipment,
            bool __state)
        {
            if (!__state)
                return;

            ThingWithComps dropped = resultingEquipment ?? equipment;
            if (dropped?.Spawned == true && dropped.IsForbidden(Faction.OfPlayer))
                dropped.SetForbidden(false, false);
        }

        private static bool IsAutomaticOutfitManagerDrop(
            Pawn pawn, PawnApparelState state, ThingWithComps equipment)
        {
            if (pawn?.Faction != Faction.OfPlayer ||
                state?.WeaponInterventionActive != true)
            {
                return false;
            }

            Job job = pawn.jobs?.curJob;
            if (job == null || job.playerForced ||
                job.targetA.Thing is not ThingWithComps targetWeapon ||
                targetWeapon.def?.IsWeapon != true)
            {
                return false;
            }

            if (job.def == JobDefOf.Equip)
            {
                bool equippingManaged = state.IsManagedWeapon(targetWeapon) &&
                    (equipment == state.OriginalWeapon ||
                     state.IsManagedWeapon(equipment));
                bool restoringOriginal = state.WeaponRestorationRequested &&
                    targetWeapon == state.OriginalWeapon &&
                    (state.IsManagedWeapon(equipment) ||
                     state.WeaponPlayerOverride);
                return equippingManaged || restoringOriginal;
            }

            return job.def == JobDefOf.DropEquipment &&
                   targetWeapon == equipment &&
                   state.WeaponRestorationRequested &&
                   (state.IsManagedWeapon(equipment) ||
                    (state.WeaponPlayerOverride &&
                     pawn.equipment?.Primary == equipment));
        }
    }
}
