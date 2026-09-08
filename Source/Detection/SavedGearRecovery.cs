using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    // A narrow ownership exception for ordinary hauling. Native scheduling,
    // reservations and storage filters still apply, along with normal AOM
    // entry/preparation. Locker work givers can also recover stranded saved gear.
    internal static class SavedGearRecovery
    {
        internal sealed class Probe
        {
            internal Pawn Hauler, Owner;
            internal Thing Gear;
            internal readonly Dictionary<IntVec3, bool> Destinations = new Dictionary<IntVec3, bool>();
            internal bool EligibilityChecked, EligibleForNativeHaul;
            internal bool LockerRecovery;
            internal string FailureReason;
            internal int CheckedCells, NativeRejectedCells, OwnerRejectedCells;
        }

        private sealed class QueryRecord
        {
            internal Pawn Owner;
            internal int Tick;
            internal string Result;
        }
        private static readonly ConditionalWeakTable<Thing, QueryRecord> LastQueries =
            new ConditionalWeakTable<Thing, QueryRecord>();

        [ThreadStatic] internal static Probe CurrentProbe;

        internal static Probe Begin(Pawn hauler, Thing gear, bool forced)
        {
            Probe previous = CurrentProbe;
            CurrentProbe = null;
            Pawn owner = AutomaticOutfitManagerGameComponent.Current?.RestoringOwnerForSavedGear(gear);
            if (!forced && Eligible(hauler, owner, gear) && Blocked(owner, gear))
                CurrentProbe = new Probe { Hauler = hauler, Owner = owner, Gear = gear };
            else if (!forced && owner != null && owner != hauler && !Eligible(hauler, owner, gear))
                ReportSkippedQuery(hauler, owner, gear);
            return previous;
        }

        internal static IEnumerable<Thing> SavedWeaponsOnMap(Map map)
        {
            var states = AutomaticOutfitManagerGameComponent.Current?.PawnStates;
            if (map == null || states == null) yield break;
            foreach (var state in states)
                if (state?.Transition == ApparelTransition.Restoring && state.Pawn?.Map == map &&
                    state.WeaponInterventionActive && state.OriginalWeapon?.Spawned == true &&
                    state.OriginalWeapon.Map == map)
                    yield return state.OriginalWeapon;
        }

        // The existing locker work givers already run under ordinary Hauling
        // priority/schedules. They can offer this exact saved item even when
        // vanilla's haulables scan does not reach it or it is already stored.
        internal static bool TryMakeRecoveryJob(Pawn hauler, Thing gear, bool forced, out Job job)
        {
            job = null;
            Probe previous = Begin(hauler, gear, forced);
            try
            {
                if (!MatchesProbe(hauler, gear)) return false;
                CurrentProbe.LockerRecovery = true;
                if (gear.IsForbidden(hauler) ||
                    !HaulAIUtility.PawnCanAutomaticallyHaulFast(hauler, gear, forced))
                {
                    CurrentProbe.FailureReason = gear.IsForbidden(hauler)
                        ? "saved item is forbidden to helper" : "native fast hauling eligibility rejected";
                    ReportQuery(hauler, gear, null, false);
                    return false;
                }

                // A stranded saved item may move to equal-priority storage, but
                // never lower-priority storage. Native selection still chooses
                // the best accepting cell, with the owner's route checked below.
                StoragePriority priority = StoreUtility.CurrentStoragePriorityOf(gear, false);
                var minimum = (StoragePriority)Math.Max((int)StoragePriority.Unstored, (int)priority - 1);
                if (!StoreUtility.TryFindBestBetterStoreCellFor(gear, hauler, hauler.Map,
                        minimum, hauler.Faction, out IntVec3 destination))
                {
                    CurrentProbe.FailureReason = CurrentProbe.CheckedCells == 0
                        ? "no eligible accepting storage group reached cell validation"
                        : "no accepting reachable storage cell passed native and owner-route checks";
                    ReportQuery(hauler, gear, null, false);
                    return false;
                }
                job = JobMaker.MakeJob(JobDefOf.HaulToCell, gear, destination);
                job.count = 1;
                job.haulOpportunisticDuplicates = false;
                bool rejected = !AllowsHaul(hauler, job, CurrentProbe.Owner, gear) ||
                    ManagedWorkCandidateFilter.Rejects(hauler, job);
                if (rejected) job = null;
                ReportQuery(hauler, gear, job, rejected);
                return job != null;
            }
            finally { CurrentProbe = previous; }
        }

        private static void ReportSkippedQuery(Pawn hauler, Pawn owner, Thing gear)
        {
            if (!AomLog.DetailedEnabled || gear == null) return;
            string reason = hauler?.Map == null || hauler.Map != owner.Map || gear.MapHeld != owner.Map
                ? "helper/item is not on the owner's map"
                : hauler.Faction != Faction.OfPlayer || hauler.Drafted ? "helper is not an undrafted colony hauler"
                : gear.Destroyed ? "item destroyed"
                : $"saved identity is not a single non-stackable item (count={gear.stackCount}, limit={gear.def.stackLimit})";
            int tick = Find.TickManager?.TicksGame ?? 0;
            LastQueries.Remove(gear);
            LastQueries.Add(gear, new QueryRecord { Owner = owner, Tick = tick, Result = "recovery eligibility rejected: " + reason });
            if (AomLog.ShouldLogDetailed(owner, "saved-recovery-ineligible:" + gear.ThingID, 600))
                AomLog.Detailed($"[AutomaticOutfitManager] {owner.LabelShortCap}: saved-item recovery query " +
                    $"for {gear.LabelCap} [{gear.ThingID}] skipped: {reason}.");
        }

        private static bool Eligible(Pawn hauler, Pawn owner, Thing gear) =>
            hauler?.Map != null && hauler.Faction == Faction.OfPlayer && !hauler.Drafted &&
            owner != null && owner != hauler && owner.Map == hauler.Map &&
            gear != null && !gear.Destroyed && gear.MapHeld == hauler.Map &&
            gear.stackCount == 1 && gear.def.stackLimit == 1;

        private static bool Blocked(Pawn owner, Thing gear) => gear.Spawned &&
            !GearRetrievalRoute.CanReach(owner, gear);

        internal static bool MatchesProbe(Pawn hauler, Thing gear) =>
            CurrentProbe != null && CurrentProbe.Hauler == hauler && CurrentProbe.Gear == gear;

        internal static bool AcceptsCell(Pawn hauler, Thing gear, IntVec3 cell)
        {
            if (!MatchesProbe(hauler, gear))
            {
                Pawn owner = StorageSearchOwner(hauler, gear);
                return owner == null || GearRetrievalRoute.CanReachStorageCell(owner, gear, cell);
            }
            var probe = CurrentProbe;
            if (ManagedWorkClaimRegistry.IsClaimedByOther(hauler, hauler.Map, null, cell)) return false;
            if (!probe.Destinations.TryGetValue(cell, out bool reachable))
            {
                reachable = GearRetrievalRoute.CanReachStorageCell(probe.Owner, gear, cell);
                probe.Destinations[cell] = reachable;
            }
            return reachable;
        }

        internal static void ReportQuery(Pawn hauler, Thing gear, Job result, bool rejected)
        {
            if (!AomLog.DetailedEnabled || !MatchesProbe(hauler, gear)) return;
            Probe probe = CurrentProbe;
            string outcome = rejected ? "AOM rejected recovery ownership, destination, job shape or prepared-work claim" :
                result != null ? "native haul approved" : probe.FailureReason ?? "native hauling returned no job";
            string detail = $"{outcome}; helper={hauler.LabelShortCap}, " +
                $"source={gear.PositionHeld}, sourcePriority={StoreUtility.CurrentStoragePriorityOf(gear, false)}, " +
                $"nativeEligible={(probe.EligibilityChecked ? probe.EligibleForNativeHaul.ToString() : "not checked")}, " +
                $"lockerRecovery={probe.LockerRecovery}, " +
                $"storageCellsChecked={probe.CheckedCells}, ordinaryCellRejections={probe.NativeRejectedCells}, " +
                $"ownerRouteRejections={probe.OwnerRejectedCells}";
            int tick = Find.TickManager?.TicksGame ?? 0;
            LastQueries.Remove(gear);
            LastQueries.Add(gear, new QueryRecord { Owner = probe.Owner, Tick = tick, Result = detail });
            if (AomLog.ShouldLogDetailed(probe.Owner, "saved-recovery-query:" + gear.ThingID, 600))
                AomLog.Detailed($"[AutomaticOutfitManager] {probe.Owner.LabelShortCap}: saved-item recovery query " +
                    $"for {gear.LabelCap} [{gear.ThingID}] at tick {tick}: {detail}. " +
                    "Native work priorities, storage priorities and filters remain authoritative.");
        }

        internal static string DescribeLastQuery(Pawn owner, Thing gear)
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            return gear != null && LastQueries.TryGetValue(gear, out QueryRecord entry) &&
                entry.Owner == owner && tick >= entry.Tick
                ? $"last hauling query {tick - entry.Tick} ticks ago: {entry.Result}"
                : "no eligible native hauling query observed for this owner/item while Detailed logging was enabled";
        }

        private static bool SingleItemHaul(Pawn hauler, Job job, Thing gear) => job?.def == JobDefOf.HaulToCell &&
            !job.playerForced && job.targetA.Thing == gear &&
            // Native StartCarryThing subtracts the item taken from job.count.
            // Zero means this exact current haul has picked up its one item,
            // not that the owner can retrieve it yet. Reconstruct continuation
            // from the native job/carry tracker so loaded hauls work too; a
            // pending or unrelated zero-count job cannot borrow permission.
            (job.count == 1 || (job.count == 0 && ReferenceEquals(hauler?.CurJob, job) &&
                gear != null && hauler.carryTracker?.CarriedThing == gear)) &&
            !job.haulOpportunisticDuplicates &&
            !job.targetB.HasThing && !job.targetC.IsValid &&
            (job.targetQueueA == null || job.targetQueueA.Count == 0) &&
            (job.targetQueueB == null || job.targetQueueB.Count == 0);

        internal static Pawn StorageSearchOwner(Pawn hauler, Thing gear)
        {
            if (MatchesProbe(hauler, gear)) return CurrentProbe.Owner;
            var component = AutomaticOutfitManagerGameComponent.Current;
            Pawn owner = component?.RestoringOwnerForSavedGear(gear);
            if (!Eligible(hauler, owner, gear) ||
                !(SingleItemHaul(hauler, hauler.CurJob, gear) || SingleItemHaul(hauler, component.StateFor(hauler)?.PendingWorkJob, gear)) ||
                (hauler.carryTracker?.CarriedThing != gear && !Blocked(owner, gear))) return null;
            return owner;
        }

        internal static bool AllowsHaul(Pawn hauler, Job job, Pawn owner, Thing gear)
        {
            if (!Eligible(hauler, owner, gear) || !SingleItemHaul(hauler, job, gear) ||
                !job.targetB.Cell.IsValid || !job.targetB.Cell.InBounds(hauler.Map) ||
                AutomaticOutfitManagerGameComponent.Current?.RestoringOwnerForSavedGear(gear) != owner)
                return false;

            // Once picked up, finish the safe delivery instead of dropping the
            // item as soon as its carrier steps outside the protected source.
            if (hauler.carryTracker?.CarriedThing != gear && !Blocked(owner, gear)) return false;
            var destination = job.targetB.Cell.GetSlotGroup(hauler.Map)?.parent;
            return destination?.HaulDestinationEnabled == true && destination.Accepts(gear) &&
                GearRetrievalRoute.CanReachStorageCell(owner, gear, job.targetB.Cell);
        }

        internal static bool AllowsReservation(Pawn hauler, Thing gear)
        {
            if (MatchesProbe(hauler, gear)) return true;
            var component = AutomaticOutfitManagerGameComponent.Current;
            Pawn owner = component?.RestoringOwnerForSavedGear(gear);
            return AllowsHaul(hauler, hauler?.CurJob, owner, gear) ||
                AllowsHaul(hauler, component?.StateFor(hauler)?.PendingWorkJob, owner, gear);
        }

        internal static bool InProgress(Pawn owner, Thing gear) => owner?.Map != null &&
            owner.Map.mapPawns.AllPawnsSpawned.Any(hauler =>
                AllowsHaul(hauler, hauler.CurJob, owner, gear) ||
                AllowsHaul(hauler, AutomaticOutfitManagerGameComponent.Current?.StateFor(hauler)?.PendingWorkJob,
                    owner, gear));

        internal static void NotifyEnded(Pawn hauler, Job job)
        {
            if (job?.def != JobDefOf.HaulToCell || job.targetA.Thing is not Thing gear || !gear.Spawned)
                return;
            var component = AutomaticOutfitManagerGameComponent.Current;
            Pawn owner = component?.RestoringOwnerForSavedGear(gear);
            if (owner == null || owner == hauler || owner.Map != gear.Map ||
                !GearRetrievalRoute.CanReach(owner, gear)) return;
            component.WakeRestoringSavedGearOwner(owner);
            if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(owner, "saved-gear-recovery-haul", 300))
                AomLog.Detailed($"{hauler.LabelShortCap}: released hauled saved item {gear.LabelCap} " +
                    $"[{gear.ThingID}] at {gear.Position}; waking {owner.LabelShortCap}'s restoration.");
        }
    }
}
