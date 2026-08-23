using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Rules;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Storage
{
    /// <summary>
    /// Low-priority, ordinary hauling work that returns loose required gear to
    /// valid locker storage whenever its rule is enabled. Because this is a
    /// work giver, schedules, needs, drafting and forced orders retain priority.
    /// </summary>
    public sealed class WorkGiver_LockerRestock : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.Apparel);

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) =>
            TryMakeJob(pawn, t as Apparel, out _);

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) =>
            TryMakeJob(pawn, t as Apparel, out Job job) ? job : null;

        private static bool TryMakeJob(Pawn pawn, Apparel apparel, out Job job)
        {
            job = null;
            if (apparel?.Spawned != true || pawn?.Map == null ||
                apparel.Map != pawn.Map)
            {
                return false;
            }

            AutomaticOutfitManagerGameComponent component = AutomaticOutfitManagerGameComponent.Current;
            List<ApparelRule> rules = null;
            if (component?.Rules != null)
            {
                foreach (ApparelRule rule in component.Rules)
                {
                    if (rule != null && rule.Enabled &&
                        rule.ChangingArea?.Map == pawn.Map &&
                        rule.RequiredApparel?.Contains(apparel.def) == true)
                    {
                        (rules ??= new List<ApparelRule>()).Add(rule);
                    }
                }
            }
            if (rules == null || rules.Count == 0)
                return false;

            // Once the item is accepted by an enabled haul destination inside
            // any matching locker, restocking is complete. Without this check,
            // treating every scan as StoragePriority.Unstored lets the bot find
            // another nominally "better" locker cell forever.
            IHaulDestination currentDestination = StoreUtility.CurrentHaulDestinationOf(apparel);
            if (currentDestination != null && currentDestination.HaulDestinationEnabled &&
                currentDestination.Accepts(apparel) &&
                rules.Any(rule => rule.ChangingArea[apparel.Position]))
            {
                return false;
            }

            // Reservation and reachability can invoke pathfinding. Keep them
            // after the definition, rule, and already-stored checks so ordinary
            // map apparel never pays that cost during locker-restock scans.
            if (apparel.IsForbidden(pawn) || !pawn.CanReserve(apparel) ||
                !pawn.CanReach(apparel, PathEndMode.ClosestTouch, Danger.Some))
            {
                return false;
            }

            foreach (var rule in rules)
            {
                IEnumerable<ISlotGroup> lockerStorage = rule.ChangingArea.ActiveCells
                    .Select(cell => cell.GetSlotGroup(pawn.Map))
                    .Where(group => group != null)
                    .Distinct();

                foreach (ISlotGroup slotGroup in lockerStorage)
                {
                    if (!StoreUtility.TryFindBestBetterStoreCellForIn(
                            apparel, pawn, pawn.Map, StoragePriority.Unstored,
                            pawn.Faction, slotGroup, out IntVec3 destination))
                    {
                        continue;
                    }

                    job = JobMaker.MakeJob(JobDefOf.HaulToCell, apparel, destination);
                    job.count = 1;
                    job.haulOpportunisticDuplicates = false;
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Keeps exact rule-selected primary weapons in their locker storage using
    /// selected-definition scans, avoiding both a map-wide all-haulables scan
    /// and a scan of unrelated weapons.
    /// </summary>
    public sealed class WorkGiver_WeaponLockerRestock : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.Weapon);

        public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            return pawn?.Map == null || component?.Rules?.Any(rule =>
                rule?.Enabled == true && rule.ChangingArea?.Map == pawn.Map &&
                rule.UsesExactWeapons) != true;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            if (pawn?.Map == null || component == null)
                yield break;

            var visitedDefs = new HashSet<ThingDef>();
            foreach (ApparelRule rule in component.Rules.Where(rule =>
                         rule?.Enabled == true &&
                         rule.ChangingArea?.Map == pawn.Map &&
                         rule.UsesExactWeapons))
            {
                foreach (ThingDef def in rule.RequiredWeapons.Where(def =>
                             def?.IsWeapon == true && visitedDefs.Add(def)))
                {
                    foreach (Thing weapon in pawn.Map.listerThings.ThingsOfDef(def))
                        yield return weapon;
                }
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) =>
            TryMakeJob(pawn, t as ThingWithComps, out _);

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) =>
            TryMakeJob(pawn, t as ThingWithComps, out Job job) ? job : null;

        private static bool TryMakeJob(
            Pawn pawn, ThingWithComps weapon, out Job job)
        {
            job = null;
            if (weapon?.def?.IsWeapon != true || weapon.Spawned != true ||
                pawn?.Map == null || weapon.Map != pawn.Map)
            {
                return false;
            }

            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            if (component?.IsManagedWeaponDefinition(weapon.def) != true)
                return false;

            var rules = component?.Rules?
                .Where(rule => rule?.Enabled == true &&
                               rule.ChangingArea?.Map == pawn.Map &&
                               rule.UsesExactWeapons &&
                               rule.RequiredWeapons.Contains(weapon.def))
                .ToList();
            if (rules == null || rules.Count == 0)
                return false;

            // A saved primary must remain available to its pawn until the
            // restoration finishes, even when its definition is also selected
            // by a shared weapon rule.
            Pawn savedOwner = component.SavedPawnForWeapon(weapon);
            if (savedOwner != null)
                return false;

            // Reachability and reservation checks can invoke pathfinding and
            // compatibility patches. Perform them only after the cheap rule
            // and ownership checks have confirmed this is a restock candidate.
            if (weapon.IsForbidden(pawn) || !pawn.CanReserve(weapon) ||
                !pawn.CanReach(
                    weapon, PathEndMode.ClosestTouch, Danger.Some))
            {
                return false;
            }

            IHaulDestination currentDestination =
                StoreUtility.CurrentHaulDestinationOf(weapon);
            if (currentDestination != null &&
                currentDestination.HaulDestinationEnabled &&
                currentDestination.Accepts(weapon) &&
                rules.Any(rule => rule.ChangingArea[weapon.Position]))
            {
                return false;
            }

            foreach (ApparelRule rule in rules)
            {
                IEnumerable<ISlotGroup> lockerStorage =
                    rule.ChangingArea.ActiveCells
                        .Select(cell => cell.GetSlotGroup(pawn.Map))
                        .Where(group => group != null)
                        .Distinct();

                foreach (ISlotGroup slotGroup in lockerStorage)
                {
                    if (!StoreUtility.TryFindBestBetterStoreCellForIn(
                            weapon, pawn, pawn.Map, StoragePriority.Unstored,
                            pawn.Faction, slotGroup, out IntVec3 destination))
                    {
                        continue;
                    }

                    job = JobMaker.MakeJob(
                        JobDefOf.HaulToCell, weapon, destination);
                    job.count = 1;
                    job.haulOpportunisticDuplicates = false;
                    return true;
                }
            }

            return false;
        }
    }
}
