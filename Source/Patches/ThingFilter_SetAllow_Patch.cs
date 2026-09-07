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
        [System.ThreadStatic] private static ThingFilter classifiedFilter;
        [System.ThreadStatic] private static Thing classifiedThing;
        [System.ThreadStatic] private static bool classifiedManaged;

        public static void BeginStorageAcceptance()
        {
            storageAcceptanceDepth++;
            classifiedFilter = null;
            classifiedThing = null;
        }

        public static void NoteThingFilterClassification(ThingFilter filter, Thing thing, bool managed)
        {
            if (storageAcceptanceDepth <= 0)
                return;
            classifiedFilter = filter;
            classifiedThing = thing;
            classifiedManaged = managed;
        }

        public static bool EndStorageAcceptance(ThingFilter filter, Thing thing, out bool managed)
        {
            // Reuse only the category of this exact filter/item pair, never
            // an earlier acceptance result. The destination's flags are checked
            // again even if a framework overrides ThingFilter's rejection.
            bool classified = classifiedFilter != null && classifiedThing != null &&
                ReferenceEquals(classifiedFilter, filter) && ReferenceEquals(classifiedThing, thing);
            managed = classifiedManaged;
            if (storageAcceptanceDepth > 0)
                storageAcceptanceDepth--;
            classifiedFilter = null;
            classifiedThing = null;
            return classified;
        }

        public static void ResetStorageAcceptance()
        {
            storageAcceptanceDepth = 0;
            classifiedFilter = null;
            classifiedThing = null;
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
            ManagedGearStorageFilterDefs.NoteThingFilterClassification(__instance, t, automatic);
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

        public static void Finalizer(System.Exception __exception)
        {
            if (__exception != null)
                ManagedGearStorageFilterDefs.ResetStorageAcceptance();
        }

        public static void Postfix(
            ThingFilter ___filter, Thing t, ref bool __result)
        {
            bool automatic;
            bool alreadyClassified = ManagedGearStorageFilterDefs.EndStorageAcceptance(
                ___filter, t, out automatic);
            if (!__result || t?.def == null || ___filter == null)
                return;

            // A nested check of another filter/item cannot satisfy this one.
            // Matching categories avoid duplicate work, but never skip enforcing
            // this destination's current managed/non-managed selection.
            if (!alreadyClassified)
            {
                if (t.def.apparel != null)
                    automatic = ManagedApparelClassifier.Matches(t);
                else if (t.def.IsWeapon)
                    automatic = ManagedWeaponClassifier.Matches(t);
                else
                    return;
            }
            SpecialThingFilterDef filterDef =
                ManagedGearStorageFilterDefs.For(t.def.IsWeapon, automatic);

            if (filterDef != null && !___filter.Allows(filterDef))
                __result = false;
        }
    }

}
