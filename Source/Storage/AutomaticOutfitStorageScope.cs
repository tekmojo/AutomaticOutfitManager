using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using RimWorld;
using Verse;

namespace AutomaticOutfitManager.Storage
{
    // Storage membership includes inactive saved outfit preferences. It does
    // not grant reservations, source-area access or ownership of every copy.
    internal static class AutomaticOutfitStorageScope
    {
        internal static bool Matches(Thing item) => item != null && !item.Destroyed &&
            ((item is Apparel && ManagedApparelClassifier.Matches(item)) ||
             (item.def?.IsWeapon == true && ManagedWeaponClassifier.Matches(item)) ||
             SavedFor(item).Any());

        internal static IEnumerable<Pawn> SavedFor(Thing item)
        {
            var component = AutomaticOutfitManagerGameComponent.Current;
            if (item == null || item.Destroyed || component == null) yield break;
            foreach (var saved in component.SavedNonWorkOutfits)
            {
                if (saved?.Pawn == null || saved.RetiredManualSnapshot) continue;
                if (saved.Weapon == item || (item is Apparel apparel &&
                    saved.Apparel?.Contains(apparel) == true)) yield return saved.Pawn;
            }
        }

        internal static IEnumerable<ThingDef> KnownDefinitions(bool weapon)
        {
            var component = AutomaticOutfitManagerGameComponent.Current;
            if (component == null) return Enumerable.Empty<ThingDef>();
            var defs = new HashSet<ThingDef>(DefDatabase<ThingDef>.AllDefsListForReading.Where(def =>
                weapon ? ManagedWeaponClassifier.Matches(def) : ManagedApparelClassifier.Matches(def)));
            void Add(Thing item)
            {
                if (item != null && !item.Destroyed && (weapon ? item.def?.IsWeapon == true :
                    item.def?.apparel != null)) defs.Add(item.def);
            }
            foreach (var state in component.PawnStates)
            {
                if (state == null) continue;
                if (state.OriginalApparel != null) foreach (var item in state.OriginalApparel) Add(item);
                if (state.ManagedApparel != null) foreach (var item in state.ManagedApparel) Add(item);
                Add(state.OriginalWeapon);
                if (state.ManagedWeapons != null) foreach (var item in state.ManagedWeapons) Add(item);
            }
            foreach (var saved in component.SavedNonWorkOutfits)
            {
                if (saved?.Pawn == null || saved.RetiredManualSnapshot) continue;
                if (saved.Apparel != null) foreach (var item in saved.Apparel) Add(item);
                Add(saved.Weapon);
            }
            return defs;
        }
    }
}
