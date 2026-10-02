using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Storage
{
    // Locker choice owns the location of exact personal gear while work gear
    // is in use. Storage filters still own acceptance; a loose item can remain
    // on the locker floor instead of being exported to another rule's storage.
    internal static class SavedGearLockerPolicy
    {
        [System.ThreadStatic] internal static bool ForcedSearch;
        [System.ThreadStatic] private static bool checkingHome;
        [System.ThreadStatic] internal static Search Current;
        internal sealed class Search
        {
            internal Thing Gear;
            internal Pawn Owner;
            internal Area Home;
        }

        internal static bool TryGetHome(Thing gear, out Pawn owner, out Area home)
        {
            owner = null;
            home = null;
            var component = AutomaticOutfitManagerGameComponent.Current;
            if (gear == null || gear.Destroyed || component == null) return false;
            owner = gear is Apparel apparel ? component.SavedPawnFor(apparel) :
                gear is ThingWithComps weapon && weapon.def?.IsWeapon == true
                    ? component.SavedPawnForWeapon(weapon) : null;
            var state = component.StateFor(owner);
            if (owner?.Map == null || gear.MapHeld != owner.Map || state == null ||
                state.MapDepartureRequested || !GearRetrievalRoute.IsSavedTarget(state, gear) ||
                (state.OriginalWeapon == gear && state.WeaponRestorationRequested) ||
                (state.Transition != ApparelTransition.Preparing && state.Transition != ApparelTransition.Active))
                return false;

            state.SavedGearLockerRuleIds ??= new Dictionary<string, string>();
            if (state.SavedGearLockerRuleIds.TryGetValue(gear.ThingID, out string id))
            {
                // Removing the locker or deleting its rule releases this policy.
                home = component.RuleById(id)?.ChangingArea;
                return home?.Map == owner.Map;
            }

            var rules = new[] { state.ActiveRuleId }
                .Concat(state.RestorationSourceRuleIds ?? Enumerable.Empty<string>())
                .Concat(state.CurrentRuleIds ?? Enumerable.Empty<string>())
                .Distinct().Select(component.RuleById)
                .Where(rule => rule?.ChangingArea?.Map == gear.MapHeld).ToList();
            // Prefer the actual source locker for multi-area sessions. Older
            // saves with displaced gear can use an unambiguous single locker.
            ApparelRule source = rules.FirstOrDefault(rule =>
                gear.PositionHeld.IsValid && gear.PositionHeld.InBounds(gear.MapHeld) &&
                rule.ChangingArea[gear.PositionHeld]);
            if (source == null && rules.Select(rule => rule.ChangingArea).Distinct().Count() == 1)
                source = rules.First();
            if (source == null) return false;
            state.SavedGearLockerRuleIds[gear.ThingID] = source.Id;
            home = source.ChangingArea;
            return true;
        }

        internal static IEnumerable<Thing> WeaponsOnMap(Map map)
        {
            var states = AutomaticOutfitManagerGameComponent.Current?.PawnStates;
            if (states == null || map == null) yield break;
            foreach (var state in states)
                if (state?.OriginalWeapon?.Spawned == true && state.OriginalWeapon.Map == map)
                    yield return state.OriginalWeapon;
        }

        internal static bool UsableGround(Pawn owner, Thing gear, Area home, IntVec3 cell)
        {
            return ReachableHomeCell(owner, gear, home, cell) && cell.Standable(home.Map);
        }

        // PassThroughOnly shelves/lockers are walkable storage cells, but native
        // Standable rejects them. Only loose floor placement needs Standable;
        // storage acceptance and capacity remain the native store-cell check.
        private static bool ReachableHomeCell(Pawn owner, Thing gear, Area home, IntVec3 cell)
        {
            Map map = home?.Map;
            return map != null && cell.IsValid && cell.InBounds(map) && home[cell] &&
                cell.Walkable(map) && !cell.Fogged(map) && !cell.IsForbidden(owner) &&
                !cell.ContainsStaticFire(map) &&
                owner.CanReach(cell, PathEndMode.OnCell, Danger.Some) &&
                GearRetrievalRoute.CanReachStorageCell(owner, gear, cell);
        }

        internal static bool HasUsableHome(Pawn owner, Thing gear, Area home)
        {
            if (gear.Spawned && ReachableHomeCell(owner, gear, home, gear.Position)) return true;
            bool previous = checkingHome;
            checkingHome = true;
            try
            {
                return home.ActiveCells.Any(cell => ReachableHomeCell(owner, gear, home, cell) &&
                    ((cell.Standable(home.Map) && GroundHasSpace(home.Map, cell)) ||
                     (cell.GetSlotGroup(home.Map)?.parent?.HaulDestinationEnabled == true &&
                      cell.GetSlotGroup(home.Map).parent.Accepts(gear) &&
                      StoreUtility.IsGoodStoreCell(cell, home.Map, gear, owner, owner.Faction))));
            }
            finally { checkingHome = previous; }
        }

        private static bool GroundHasSpace(Map map, IntVec3 cell) =>
            cell.GetSlotGroup(map) == null &&
            !cell.GetThingList(map).Any(thing => thing.def.category == ThingCategory.Item);

        internal static Search BeginSearch(Thing gear)
        {
            Search previous = Current;
            if (Current?.Gear == gear) return previous;
            Current = new Search { Gear = gear };
            if (!ForcedSearch && TryGetHome(gear, out Pawn owner, out Area home) &&
                HasUsableHome(owner, gear, home))
            {
                Current.Home = home;
                Current.Owner = owner;
            }
            return previous;
        }

        internal static bool AllowsCell(Thing gear, IntVec3 cell)
        {
            if (ForcedSearch || checkingHome) return true;
            if (Current?.Gear == gear) return Current.Home == null ||
                ReachableHomeCell(Current.Owner, gear, Current.Home, cell);
            return !TryGetHome(gear, out Pawn owner, out Area home) ||
                ReachableHomeCell(owner, gear, home, cell) || !HasUsableHome(owner, gear, home);
        }

        internal static bool ForcedHaul(Pawn carrier, Thing gear) =>
            carrier?.CurJob is Job job && job.playerForced && job.targetA.Thing == gear &&
            (job.def == JobDefOf.HaulToCell || job.def == JobDefOf.HaulToContainer);

        // True means this policy handled the query, including a deliberate
        // no-job result for valid local storage or the floor fallback.
        internal static bool TryMakeJob(Pawn hauler, Thing gear, bool forced, out Job job)
        {
            job = null;
            if (forced || ForcedSearch || hauler?.Map == null || hauler.Faction != Faction.OfPlayer ||
                gear?.Spawned != true || gear.Map != hauler.Map ||
                !TryGetHome(gear, out Pawn owner, out Area home) || !HasUsableHome(owner, gear, home))
                return false;

            if (gear.IsForbidden(hauler) ||
                ManagedWorkClaimRegistry.IsClaimedByOther(hauler, hauler.Map, gear, gear.Position) ||
                !HaulAIUtility.PawnCanAutomaticallyHaulFast(hauler, gear, false)) return true;
            var stored = StoreUtility.CurrentHaulDestinationOf(gear);
            if (ReachableHomeCell(owner, gear, home, gear.Position) &&
                stored?.HaulDestinationEnabled == true && stored.Accepts(gear))
                return true;

            // Choose the best accepting storage within this locker. A linked
            // storage group can extend outside it: filter individual cells in
            // the native search, before a destination is selected.
            var groups = home.ActiveCells.Select(cell => cell.GetSlotGroup(hauler.Map))
                .Where(group => group?.parent?.HaulDestinationEnabled == true &&
                    group.parent.Accepts(gear)).Distinct()
                .OrderByDescending(group => group.Settings.Priority);
            foreach (var group in groups)
            {
                if (!LockerHaulDestination.TryFind(hauler, gear, home, group, false,
                        out IntVec3 cell, owner)) continue;
                Job candidate = MakeHaul(gear, cell, HaulMode.ToCellStorage);
                if (ManagedWorkCandidateFilter.Rejects(hauler, candidate)) continue;
                job = candidate;
                return true;
            }

            // The saved reference already tracks a loose item. Do not invent
            // a same-cell haul when it is on a usable part of its locker floor.
            if (ReachableHomeCell(owner, gear, home, gear.Position)) return true;
            foreach (IntVec3 cell in home.ActiveCells.OrderBy(cell => cell.DistanceToSquared(gear.Position)))
            {
                if (!UsableGround(owner, gear, home, cell) || cell.IsForbidden(hauler) ||
                    !hauler.CanReserve(cell) || !hauler.CanReach(cell, PathEndMode.OnCell, Danger.Some) ||
                    !GroundHasSpace(hauler.Map, cell)) continue;
                Job candidate = MakeHaul(gear, cell, HaulMode.ToCellNonStorage);
                if (ManagedWorkCandidateFilter.Rejects(hauler, candidate)) continue;
                job = candidate;
                return true;
            }
            // No safe floor slot or accepting storage exists: ordinary hauling
            // and blocked-item recovery remain available instead of deadlocking.
            return false;
        }

        private static Job MakeHaul(Thing gear, IntVec3 cell, HaulMode mode)
        {
            Job job = JobMaker.MakeJob(JobDefOf.HaulToCell, gear, cell);
            job.count = 1;
            job.haulMode = mode;
            job.haulOpportunisticDuplicates = false;
            return job;
        }
    }

    [HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.HaulToStorageJob))]
    internal static class SavedGearLocker_HaulToStorage_Patch
    {
        private static bool Prefix(Pawn p, Thing t, bool forced, ref Job __result, out bool __state)
        {
            __state = SavedGearLockerPolicy.ForcedSearch;
            SavedGearLockerPolicy.ForcedSearch |= forced;
            if (!SavedGearLockerPolicy.TryMakeJob(p, t, forced, out Job job)) return true;
            __result = job;
            return false;
        }
        private static void Finalizer(bool __state) => SavedGearLockerPolicy.ForcedSearch = __state;
    }

    // Native hauling can search again after pickup when a shelf fills up.
    // Constrain that search too, with one reachability assessment per query.
    [HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterStoreCellFor))]
    internal static class SavedGearLocker_Search_Patch
    {
        private static void Prefix(Thing t, out SavedGearLockerPolicy.Search __state) =>
            __state = SavedGearLockerPolicy.BeginSearch(t);
        private static void Finalizer(SavedGearLockerPolicy.Search __state) =>
            SavedGearLockerPolicy.Current = __state;
    }

    [HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.IsGoodStoreCell))]
    internal static class SavedGearLocker_Cell_Patch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Thing t, IntVec3 c, Pawn carrier, ref bool __result)
        {
            if (__result && !SavedGearLockerPolicy.ForcedHaul(carrier, t) &&
                !SavedGearLockerPolicy.AllowsCell(t, c)) __result = false;
        }
    }

    [HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.TryFindBestBetterNonSlotGroupStorageFor))]
    internal static class SavedGearLocker_Container_Patch
    {
        private static bool Prefix(Thing t, Pawn carrier, ref bool __result)
        {
            if (SavedGearLockerPolicy.ForcedSearch ||
                SavedGearLockerPolicy.ForcedHaul(carrier, t) ||
                !SavedGearLockerPolicy.TryGetHome(t, out Pawn owner, out Area home) ||
                !SavedGearLockerPolicy.HasUsableHome(owner, t, home)) return true;
            // Exact saved gear must remain spawned for native Wear/Equip.
            __result = false;
            return false;
        }
    }
}
