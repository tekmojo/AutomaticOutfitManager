using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Patches;
using RimWorld;
using Verse;

namespace AutomaticOutfitManager.Detection
{
    internal sealed class LockerStorageAssessment
    {
        internal bool HasStorage;
        internal bool ExcludesManagedApparel;
        internal bool ExcludesManagedWeapons;
        internal readonly List<Thing> RejectedSavedItems = new List<Thing>();

        internal static LockerStorageAssessment Inspect(Map map, IEnumerable<IntVec3> cells,
            bool apparelNeeded, bool weaponsNeeded, IEnumerable<Thing> savedItems)
        {
            var result = new LockerStorageAssessment();
            if (map == null) return result;
            var groups = cells.Where(cell => cell.IsValid && cell.InBounds(map))
                .Select(cell => cell.GetSlotGroup(map)).Where(group => group?.Settings != null)
                .Distinct().ToList();
            result.HasStorage = groups.Count > 0;
            if (!result.HasStorage) return result;

            // Category flags are only advice about settings, never a promise
            // that a free/reachable/reservable destination exists.
            bool AllowsManaged(bool weapon)
            {
                var flag = ManagedGearStorageFilterDefs.For(weapon, true);
                return flag == null || groups.Any(group => group.Settings.filter.Allows(flag));
            }
            result.ExcludesManagedApparel = apparelNeeded && !AllowsManaged(false);
            result.ExcludesManagedWeapons = weaponsNeeded && !AllowsManaged(true);
            foreach (var item in (savedItems ?? Enumerable.Empty<Thing>()).Where(item =>
                item != null && !item.Destroyed).Distinct())
            {
                // Use exact items: definition-only checks miss condition,
                // quality and managed/saved classification, including mod filters.
                if (!groups.Any(group => group.Settings.AllowedToAccept(item)))
                    result.RejectedSavedItems.Add(item);
            }
            return result;
        }
    }
}
