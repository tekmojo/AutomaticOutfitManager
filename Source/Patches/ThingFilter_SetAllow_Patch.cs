using AutomaticOutfitManager.Storage;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutomaticOutfitManager.Patches
{
    internal static class ManagedGearStorageFilterDefs
    {
        private static SpecialThingFilterDef managedApparel;
        private static SpecialThingFilterDef unmanagedApparel;
        private static SpecialThingFilterDef managedWeapons;
        private static SpecialThingFilterDef unmanagedWeapons;
        [System.ThreadStatic] private static int storageAcceptanceDepth;
        [System.ThreadStatic] private static bool classifiedDuringStorageAcceptance;

        public static void BeginStorageAcceptance()
        {
            if (storageAcceptanceDepth++ == 0)
                classifiedDuringStorageAcceptance = false;
        }

        public static void NoteThingFilterClassification()
        {
            if (storageAcceptanceDepth > 0)
                classifiedDuringStorageAcceptance = true;
        }

        public static bool EndStorageAcceptance()
        {
            bool classified = classifiedDuringStorageAcceptance;
            if (storageAcceptanceDepth > 0)
                storageAcceptanceDepth--;
            if (storageAcceptanceDepth == 0)
                classifiedDuringStorageAcceptance = false;
            return classified;
        }

        public static SpecialThingFilterDef For(bool weapon, bool managed)
        {
            if (weapon)
            {
                if (managed)
                {
                    return managedWeapons ??= DefDatabase<SpecialThingFilterDef>
                        .GetNamedSilentFail(
                            "AutomaticOutfitManager_AllowManagedWeapons");
                }

                return unmanagedWeapons ??= DefDatabase<SpecialThingFilterDef>
                    .GetNamedSilentFail(
                        "AutomaticOutfitManager_AllowUnmanagedWeapons");
            }

            if (managed)
            {
                return managedApparel ??= DefDatabase<SpecialThingFilterDef>
                    .GetNamedSilentFail("AutomaticOutfitManager_AllowManaged");
            }

            return unmanagedApparel ??= DefDatabase<SpecialThingFilterDef>
                .GetNamedSilentFail("AutomaticOutfitManager_AllowUnmanaged");
        }
    }

    [HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.SetAllow), typeof(SpecialThingFilterDef), typeof(bool))]
    public static class ThingFilter_SetAllow_Patch
    {
        public static void Postfix(ThingFilter __instance, SpecialThingFilterDef sfDef, bool allow)
        {
            if (!allow || sfDef == null)
                return;

            if (sfDef.defName == "AutomaticOutfitManager_AllowManaged")
            {
                foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    if (ManagedApparelClassifier.Matches(def))
                        __instance.SetAllow(def, true);
                }
            }
            else if (sfDef.defName == "AutomaticOutfitManager_AllowManagedWeapons")
            {
                foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    if (ManagedWeaponClassifier.Matches(def))
                        __instance.SetAllow(def, true);
                }
            }
        }
    }

    [HarmonyPatch(typeof(ThingFilter), nameof(ThingFilter.Allows), typeof(Thing))]
    [HarmonyPriority(Priority.Last)]
    public static class ThingFilter_EnforceManagedOutfit_Patch
    {
        public static void Postfix(ThingFilter __instance, Thing t, ref bool __result)
        {
            if (!__result || t?.def == null)
                return;

            bool automatic;
            if (t.def.apparel != null)
            {
                automatic = ManagedApparelClassifier.Matches(t);
            }
            else if (t.def.IsWeapon)
            {
                automatic = ManagedWeaponClassifier.Matches(t);
            }
            else
            {
                return;
            }
            ManagedGearStorageFilterDefs.NoteThingFilterClassification();
            SpecialThingFilterDef filterDef =
                ManagedGearStorageFilterDefs.For(t.def.IsWeapon, automatic);

            if (filterDef != null && !__instance.Allows(filterDef))
                __result = false;
        }
    }

    // Storage frameworks may call StorageSettings directly or replace the
    // ordinary ThingFilter evaluation. Enforce the special selection at the
    // shared storage-settings boundary as well.
    [HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.AllowedToAccept), typeof(Thing))]
    [HarmonyPriority(Priority.Last)]
    public static class StorageSettings_EnforceManagedOutfit_Patch
    {
        public static void Prefix() =>
            ManagedGearStorageFilterDefs.BeginStorageAcceptance();

        public static void Postfix(
            ThingFilter ___filter, Thing t, ref bool __result)
        {
            bool alreadyClassified =
                ManagedGearStorageFilterDefs.EndStorageAcceptance();
            if (!__result || t?.def == null)
                return;

            // Vanilla StorageSettings calls ThingFilter.Allows, whose patch has
            // already enforced the managed/unmanaged split. Skip the duplicate
            // classifier pass there while retaining this fallback for storage
            // frameworks that bypass or replace ThingFilter evaluation.
            if (___filter == null || alreadyClassified)
                return;

            bool automatic;
            if (t.def.apparel != null)
            {
                automatic = ManagedApparelClassifier.Matches(t);
            }
            else if (t.def.IsWeapon)
            {
                automatic = ManagedWeaponClassifier.Matches(t);
            }
            else
            {
                return;
            }
            SpecialThingFilterDef filterDef =
                ManagedGearStorageFilterDefs.For(t.def.IsWeapon, automatic);

            if (filterDef != null && !___filter.Allows(filterDef))
                __result = false;
        }
    }

}
