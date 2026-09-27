using System;
using System.Collections.Generic;
using AutomaticOutfitManager.State;
using Verse;

namespace AutomaticOutfitManager.Storage
{
    // Exact IDs describe outstanding outfit work, not a permanent stock catalog.
    // Selected/retained types and inactive personal preferences have their own
    // classifiers; removing an obsolete ID must not alter either of those.
    internal static class ManagedGearTracking
    {
        internal static int Prune(List<string> apparelIds, List<string> weaponIds,
            IEnumerable<PawnApparelState> states, IEnumerable<SavedNonWorkOutfit> savedOutfits,
            IEnumerable<NonWorkMealTrip> mealTrips, Func<Thing, Pawn, bool> heldByPawn)
        {
            var needed = new HashSet<string>();
            void Keep(Thing item)
            {
                if (item != null && !item.Destroyed) needed.Add(item.GetUniqueLoadID());
            }
            void KeepOutfit(SavedNonWorkOutfit outfit)
            {
                if (outfit == null) return;
                if (outfit.Apparel != null) foreach (var item in outfit.Apparel) Keep(item);
                Keep(outfit.Weapon);
            }
            foreach (var state in states)
            {
                if (state?.Pawn == null) continue;
                if (state.OriginalApparel != null) foreach (var item in state.OriginalApparel) Keep(item);
                if (state.ManagedApparel != null) foreach (var item in state.ManagedApparel) Keep(item);
                Keep(state.OriginalWeapon);
                if (state.ManagedWeapons != null) foreach (var item in state.ManagedWeapons) Keep(item);
            }
            foreach (var saved in savedOutfits)
            {
                if (saved?.Pawn == null || saved.RetiredManualSnapshot) continue;
                // A partial Non-Work return may end its transition while keeping
                // issued gear worn or in inventory. Preserve that return obligation.
                if (saved.WorkGear != null)
                    foreach (var entry in saved.WorkGear)
                        if (entry?.Item != null && heldByPawn(entry.Item, saved.Pawn)) Keep(entry.Item);
                if (saved.PendingSharedReturns != null)
                    foreach (var item in saved.PendingSharedReturns)
                        if (item != null && heldByPawn(item, saved.Pawn)) Keep(item);
            }
            foreach (var trip in mealTrips)
            {
                if (trip?.Pawn == null) continue;
                KeepOutfit(trip.ReturnOutfit);
                KeepOutfit(trip.DestinationOutfit);
            }
            return apparelIds.RemoveAll(id => !needed.Contains(id)) +
                weaponIds.RemoveAll(id => !needed.Contains(id));
        }
    }
}
