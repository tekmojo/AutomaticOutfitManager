// Exercise production classifiers, special workers and Harmony patch bodies.
// Minimal API doubles provide selection state; this does not simulate hauling.
using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Storage;
using RimWorld;
using Verse;

internal static class StorageFilterContractTests
{
    static int passed;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        passed++;
    }
    static ThingFilter Filter(bool managedApparel, bool ordinaryApparel, bool managedWeapons, bool ordinaryWeapons)
    {
        var filter = new ThingFilter();
        filter.SetAllow(ManagedGearStorageFilterDefs.For(false, true), managedApparel);
        filter.SetAllow(ManagedGearStorageFilterDefs.For(false, false), ordinaryApparel);
        filter.SetAllow(ManagedGearStorageFilterDefs.For(true, true), managedWeapons);
        filter.SetAllow(ManagedGearStorageFilterDefs.For(true, false), ordinaryWeapons);
        return filter;
    }
    static bool Allows(ThingFilter filter, Thing thing, bool nativeAllowed = true)
    {
        bool result = nativeAllowed;
        ThingFilter_EnforceManagedOutfit_Patch.Postfix(filter, thing, ref result);
        return result;
    }
    static bool Storage(ThingFilter filter, Thing thing, bool callThingFilter)
    {
        StorageSettings_EnforceManagedOutfit_Patch.Prefix();
        bool result = !callThingFilter || Allows(filter, thing);
        StorageSettings_EnforceManagedOutfit_Patch.Postfix(filter, thing, ref result);
        return result;
    }
    public static int Main()
    {
        try
        {
            foreach (string name in new[] { "AllowManaged", "AllowUnmanaged", "AllowManagedWeapons", "AllowUnmanagedWeapons" })
                DefDatabase<SpecialThingFilterDef>.AllDefsListForReading.Add(new SpecialThingFilterDef { defName = "AutomaticOutfitManager_" + name });
            var component = new AutomaticOutfitManagerGameComponent();
            AutomaticOutfitManagerGameComponent.Current = component;
            var workShirtDef = new ThingDef { apparel = new object() };
            var personalShirtDef = new ThingDef { apparel = new object() };
            var workGunDef = new ThingDef { IsWeapon = true };
            var personalGunDef = new ThingDef { IsWeapon = true };
            DefDatabase<ThingDef>.AllDefsListForReading.AddRange(new[] { workShirtDef, personalShirtDef, workGunDef, personalGunDef });
            component.ApparelDefs.Add(workShirtDef);
            component.WeaponDefs.Add(workGunDef);
            var workShirt = new Apparel { def = workShirtDef };
            var personalShirt = new Apparel { def = personalShirtDef };
            var workGun = new ThingWithComps { def = workGunDef };
            var personalGun = new ThingWithComps { def = personalGunDef };
            var things = new Thing[] { workShirt, personalShirt, workGun, personalGun };
            for (int mask = 0; mask < 16; mask++)
            {
                var filter = Filter((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0);
                for (int i = 0; i < things.Length; i++)
                {
                    bool expected = (mask & (1 << i)) != 0;
                    Check(Allows(filter, things[i]) == expected, "ThingFilter managed/ordinary matrix " + mask + "/" + i);
                    Check(Storage(filter, things[i], true) == expected, "normal storage matrix " + mask + "/" + i);
                    Check(Storage(filter, things[i], false) == expected, "framework fallback matrix " + mask + "/" + i);
                    Check(!Allows(filter, things[i], false), "native type/quality/condition rejection remains authoritative " + mask + "/" + i);
                }
            }
            var managedOnly = Filter(true, false, true, false);
            var ordinaryOnly = Filter(false, true, false, true);
            component.PawnStates.Add(new PawnApparelState { OriginalApparel = new List<Apparel> { personalShirt } });
            component.TrackedWeapons.Add(personalGun);
            Check(Allows(managedOnly, personalShirt) && !Allows(ordinaryOnly, personalShirt), "exact active saved apparel follows managed selection");
            Check(Allows(managedOnly, personalGun) && !Allows(ordinaryOnly, personalGun), "exact active saved weapon follows managed selection");
            Check(Allows(ordinaryOnly, new Apparel { def = personalShirtDef }), "saving one garment does not classify every copy of its definition");
            Check(Allows(ordinaryOnly, new ThingWithComps { def = personalGunDef }), "saving one weapon does not classify every copy of its definition");
            component.PawnStates.Clear();
            component.TrackedWeapons.Clear();
            Check(Allows(ordinaryOnly, personalShirt) && Allows(ordinaryOnly, personalGun), "released exact personal items return to ordinary classification");
            component.ManagedApparel.Add(personalShirt);
            component.TrackedWeapons.Add(personalGun);
            Check(new SpecialThingFilterWorker_ManagedOutfit().Matches(personalShirt), "exact managed apparel special worker");
            Check(!new SpecialThingFilterWorker_NonManagedOutfit().Matches(personalShirt), "apparel special categories are complementary");
            Check(new SpecialThingFilterWorker_ManagedWeapon().Matches(personalGun), "exact managed weapon special worker");
            Check(!new SpecialThingFilterWorker_NonManagedWeapon().Matches(personalGun), "weapon special categories are complementary");
            var enable = Filter(false, true, false, true);
            enable.SetAllow(ManagedGearStorageFilterDefs.For(false, true), true);
            ThingFilter_SetAllow_Patch.Postfix(enable, ManagedGearStorageFilterDefs.For(false, true), true);
            Check(enable.EnabledDefs.Contains(workShirtDef) && !enable.EnabledDefs.Contains(personalShirtDef), "enabling managed stock enables known stock definitions without broadening personal types");
            Check(Allows(Filter(false, false, false, false), new Thing { def = new ThingDef() }), "non-gear is unaffected");

            // A framework may ask another filter/item during acceptance. That
            // answer cannot stand in for the actual destination's selection.
            StorageSettings_EnforceManagedOutfit_Patch.Prefix();
            Check(Allows(managedOnly, workShirt), "unrelated filter accepted managed apparel");
            bool result = true;
            StorageSettings_EnforceManagedOutfit_Patch.Postfix(ordinaryOnly, workShirt, ref result);
            Check(!result, "another filter cannot bypass destination managed exclusion");
            StorageSettings_EnforceManagedOutfit_Patch.Prefix();
            Check(Allows(ordinaryOnly, new Apparel { def = personalShirtDef }), "same filter accepted a different ordinary item");
            result = true;
            StorageSettings_EnforceManagedOutfit_Patch.Postfix(ordinaryOnly, workShirt, ref result);
            Check(!result, "another item cannot bypass destination managed exclusion");
            StorageSettings_EnforceManagedOutfit_Patch.Prefix();
            Check(!Allows(ordinaryOnly, workShirt), "inner ThingFilter rejected managed apparel");
            result = true; // A storage framework subsequently returns true.
            StorageSettings_EnforceManagedOutfit_Patch.Postfix(ordinaryOnly, workShirt, ref result);
            Check(!result, "later storage acceptance cannot override the managed rejection");
            var changingFilter = Filter(true, true, true, true);
            StorageSettings_EnforceManagedOutfit_Patch.Prefix();
            Check(Allows(changingFilter, workGun), "destination initially accepts the managed weapon");
            changingFilter.SetAllow(ManagedGearStorageFilterDefs.For(true, true), false);
            result = true;
            StorageSettings_EnforceManagedOutfit_Patch.Postfix(changingFilter, workGun, ref result);
            Check(!result, "current selection is enforced even after a positive inner check");
            StorageSettings_EnforceManagedOutfit_Patch.Prefix();
            Check(Storage(managedOnly, workShirt, true), "nested destination accepts managed apparel");
            result = true;
            StorageSettings_EnforceManagedOutfit_Patch.Postfix(ordinaryOnly, workShirt, ref result);
            Check(!result, "nested acceptance does not approve the outer destination");
            StorageSettings_EnforceManagedOutfit_Patch.Prefix();
            Check(Allows(managedOnly, workGun), "classification exists before simulated storage exception");
            StorageSettings_EnforceManagedOutfit_Patch.Finalizer(new InvalidOperationException());
            Check(!Storage(ordinaryOnly, workGun, false), "storage checks recover cleanly after an exception");
            component.ManagedApparel.Clear(); component.TrackedWeapons.Clear();
            var remembered = new SavedNonWorkOutfit {Pawn=new Pawn(), Apparel=new List<Apparel>{personalShirt}, Weapon=personalGun};
            component.SavedNonWorkOutfits.Add(remembered);
            Check(Allows(managedOnly,personalShirt) && Allows(managedOnly,personalGun), "inactive automatic saved items included in automatic outfit category");
            Check(!Allows(ordinaryOnly,personalShirt) && !Allows(ordinaryOnly,personalGun), "inactive saved items excluded from non-automatic category");
            Check(!ManagedApparelClassifier.Matches(personalShirt) && !ManagedWeaponClassifier.Matches(personalGun), "storage scope does not grant live ownership or forbid-policy membership");
            Check(Allows(ordinaryOnly,new Apparel {def=personalShirtDef}) && Allows(ordinaryOnly,new ThingWithComps {def=personalGunDef}), "ordinary copies remain non-automatic");
            Check(new SpecialThingFilterWorker_ManagedOutfit().Matches(personalShirt) && !new SpecialThingFilterWorker_ManagedOutfit().Matches(personalGun), "automatic apparel worker does not match weapons");
            Check(new SpecialThingFilterWorker_ManagedWeapon().Matches(personalGun) && !new SpecialThingFilterWorker_ManagedWeapon().Matches(personalShirt), "automatic weapons worker does not match apparel");
            foreach(bool weaponCategory in new[]{false,true})
            {
                var flag=ManagedGearStorageFilterDefs.For(weaponCategory,true);
                var newFilter=Filter(false,true,false,true); newFilter.SetAllow(flag,true);
                ThingFilter_SetAllow_Patch.Postfix(newFilter,flag,true);
                Check(newFilter.EnabledDefs.Contains(weaponCategory?personalGunDef:personalShirtDef), "enabling automatic filter includes currently saved item types");
                Check(!newFilter.EnabledDefs.Contains(weaponCategory?personalShirtDef:personalGunDef), "enabling one automatic category does not alter another");
                Check(!Allows(newFilter,weaponCategory?(Thing)personalGun:personalShirt,false), "saved scope does not override native item condition or quality rejection");
            }
            remembered.RetiredManualSnapshot=true;
            Check(!AutomaticOutfitStorageScope.Matches(personalShirt), "retired prototype snapshot excluded");
            remembered.RetiredManualSnapshot=false; remembered.Pawn=null;
            Check(!AutomaticOutfitStorageScope.Matches(personalGun), "orphan snapshot excluded");
            remembered.Pawn=new Pawn(); personalShirt.Destroyed=true;
            Check(!AutomaticOutfitStorageScope.Matches(personalShirt), "destroyed saved garment excluded");
            personalShirt.Destroyed=false; remembered.Apparel.Clear(); remembered.Weapon=null;
            Check(Allows(ordinaryOnly,personalShirt) && Allows(ordinaryOnly,personalGun), "forgetting saved preference returns exact item to non-automatic category");
            component.PawnStates.Add(new PawnApparelState {OriginalApparel=new List<Apparel>{personalShirt},OriginalWeapon=personalGun});
            Check(AutomaticOutfitStorageScope.KnownDefinitions(false).Contains(personalShirtDef) && AutomaticOutfitStorageScope.KnownDefinitions(true).Contains(personalGunDef), "active restoration snapshots included in filter enablement");
            Console.WriteLine(passed + " storage checks passed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL after " + passed + " checks: " + error); return 1; }
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)] public class HarmonyPatch : Attribute { public HarmonyPatch(params object[] values) {} }
    public class HarmonyPriority : Attribute { public HarmonyPriority(int value) {} }
    public static class Priority { public const int Last = 0; }
}
namespace Verse
{
    public class ThingDef { public object apparel; public bool IsWeapon; }
    public class Thing { public ThingDef def; public bool Destroyed; }
    public class Pawn {}
    public class ThingWithComps : Thing {}
    public class SpecialThingFilterDef { public string defName; }
    public abstract class SpecialThingFilterWorker { public abstract bool Matches(Thing thing); public abstract bool CanEverMatch(ThingDef def); }
    public static class DefDatabase<T> where T : class
    {
        public static List<T> AllDefsListForReading = new List<T>();
        public static T GetNamedSilentFail(string name) => AllDefsListForReading.FirstOrDefault(d => (d as SpecialThingFilterDef)?.defName == name);
    }
    public class ThingFilter
    {
        readonly HashSet<SpecialThingFilterDef> allowed = new HashSet<SpecialThingFilterDef>();
        public HashSet<ThingDef> EnabledDefs = new HashSet<ThingDef>();
        public bool Allows(SpecialThingFilterDef def) => allowed.Contains(def);
        public bool Allows(Thing t) => true;
        public void SetAllow(SpecialThingFilterDef def, bool value) { if (value) allowed.Add(def); else allowed.Remove(def); }
        public void SetAllow(ThingDef def, bool value) { if (value) EnabledDefs.Add(def); else EnabledDefs.Remove(def); }
    }
}
namespace RimWorld
{
    public class Apparel : ThingWithComps {}
    public class StorageSettings { public bool AllowedToAccept(Thing t) => true; }
}
namespace AutomaticOutfitManager.Core
{
    public class PawnApparelState { public List<Apparel> OriginalApparel; public List<Apparel> ManagedApparel; public ThingWithComps OriginalWeapon; public List<ThingWithComps> ManagedWeapons; }
    public class SavedNonWorkOutfit {public Pawn Pawn; public bool RetiredManualSnapshot; public List<Apparel> Apparel; public ThingWithComps Weapon;}
    public class AutomaticOutfitManagerGameComponent
    {
        public static AutomaticOutfitManagerGameComponent Current;
        public HashSet<ThingDef> ApparelDefs = new HashSet<ThingDef>();
        public HashSet<ThingDef> WeaponDefs = new HashSet<ThingDef>();
        public HashSet<Apparel> ManagedApparel = new HashSet<Apparel>();
        public HashSet<ThingWithComps> TrackedWeapons = new HashSet<ThingWithComps>();
        public List<PawnApparelState> PawnStates = new List<PawnApparelState>();
        public List<SavedNonWorkOutfit> SavedNonWorkOutfits = new List<SavedNonWorkOutfit>();
        public bool IsManagedApparelDefinition(ThingDef def) => ApparelDefs.Contains(def);
        public bool IsManagedWeaponDefinition(ThingDef def) => WeaponDefs.Contains(def);
        public bool IsManagedApparel(Apparel item) => ManagedApparel.Contains(item);
        public bool IsTrackedWeapon(ThingWithComps item) => TrackedWeapons.Contains(item);
    }
}
