using System;
using System.Collections.Generic;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.Patches;
using RimWorld;
using Verse;
using Verse.AI;

internal static class SavedRecoveryPickupTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (bool weapon in new[] { false, true })
        {
            var map = new Map();
            var owner = new Pawn { Map = map, Position = new IntVec3(20) };
            var helper = new Pawn { Map = map, Position = new IntVec3(10) };
            var item = weapon ? new ThingWithComps() : (ThingWithComps)new Apparel();
            item.def.IsWeapon = weapon; item.Map = map; item.Position = new IntVec3(10);
            var component = AutomaticOutfitManagerGameComponent.Current = new AutomaticOutfitManagerGameComponent();
            var state = new PawnApparelState { Pawn = owner, Transition = ApparelTransition.Restoring };
            if (weapon) { state.OriginalWeapon = item; state.WeaponInterventionActive = true; }
            else state.OriginalApparel.Add((Apparel)item);
            component.States[owner] = state;
            component.States[helper] = new PawnApparelState { Pawn = helper, Transition = ApparelTransition.Active };
            component.Rules.Add(new ApparelRule { Id = "protected", Area = new Area(map, 10) });
            map.mapPawns.AllPawnsSpawned.AddRange(new[] { owner, helper });
            ProtectedPathAvoidance.BlockClosestTouch = true;
            StoreUtility.Cells = new[] { new IntVec3(10), new IntVec3(20) };
            StoreUtility.CurrentPriority = StoragePriority.Unstored;
            StoreUtility.DestinationPriority = StoragePriority.Normal;
            StoreUtility.NativeGoodCell = StoreUtility.BetterStorageExists = true;
            check(SavedGearRecovery.TryMakeRecoveryJob(helper, item, false, out Job job), "native scanner creates exact saved recovery");
            helper.CurJob = job;
            check(!component.OwnershipPulse(helper), "ownership watchdog preserves approved recovery before pickup");
            job.count = 0;
            check(!SavedGearRecovery.AllowsHaul(helper, job, owner, item), "zero-count proposal before pickup is not a recovery");
            job.count = 1;

            // Native StartCarryThing ordering: take the exact non-stackable
            // instance, subtract the quantity taken, update target A to carry.
            item.Spawned = false; item.Holder = helper; helper.carryTracker.CarriedThing = item;
            job.count -= item.stackCount; job.targetA = item;
            for (int tick = 0; tick < 120; tick += 30)
                check(!component.OwnershipPulse(helper), "watchdog keeps native zero-count delivery active after pickup");
            check(helper.CurJob == job && helper.carryTracker.CarriedThing == item && component.Releases == 0,
                "pickup does not drop saved gear, clear the native haul, or create a wait");
            check(SavedGearRecovery.InProgress(owner, item) && helper.CanReserve(item), "zero-count delivery retains in-flight ownership and reservations");
            check(!StoreUtility.IsGoodStoreCell(new IntVec3(10), map, item, helper, helper.Faction), "storage replan cannot put carried item back behind protected boundary");
            check(StoreUtility.IsGoodStoreCell(new IntVec3(30), map, item, helper, helper.Faction), "storage replan can choose another owner-reachable cell");
            job.targetB = new IntVec3(30);
            check(!component.OwnershipPulse(helper), "changed valid storage destination retains exact current haul");

            var detached = new Job { def = job.def, targetA = item, targetB = job.targetB, count = 0 };
            component.States[helper].PendingWorkJob = detached;
            check(!SavedGearRecovery.AllowsHaul(helper, detached, owner, item), "pending zero-count clone cannot borrow current carrier authorization");
            component.States[helper].PendingWorkJob = null;
            helper.CurJob = detached; job = detached;
            check(!component.OwnershipPulse(helper), "loaded current job plus exact carry reconstructs authorization without transient token");

            var wrong = new ThingWithComps { Map = map, def = item.def };
            helper.carryTracker.CarriedThing = wrong;
            check(!SavedGearRecovery.AllowsHaul(helper, job, owner, item), "same-definition different carried instance has no continuation");
            helper.carryTracker.CarriedThing = item;
            job.targetQueueA = new List<LocalTargetInfo> { wrong };
            check(!SavedGearRecovery.AllowsHaul(helper, job, owner, item), "extra payload cannot share saved-item permission");
            job.targetQueueA = null; job.haulOpportunisticDuplicates = true;
            check(!SavedGearRecovery.AllowsHaul(helper, job, owner, item), "opportunistic pickup stays excluded after initial pickup");
            job.haulOpportunisticDuplicates = false; job.count = -1;
            check(!SavedGearRecovery.AllowsHaul(helper, job, owner, item), "invalid count does not become authorized by carrying");
            job.count = 2;
            check(!SavedGearRecovery.AllowsHaul(helper, job, owner, item), "multi-item count remains excluded");
            job.count = 0; job.playerForced = true;
            check(!SavedGearRecovery.AllowsHaul(helper, job, owner, item) && !component.OwnershipPulse(helper), "explicit order remains native rather than borrowing automatic recovery");
            job.playerForced = false;
            foreach (var def in new[] { JobDefOf.Wear, JobDefOf.Equip, JobDefOf.DoBill, JobDefOf.HaulToContainer })
            {
                job.def = def;
                check(!SavedGearRecovery.AllowsHaul(helper, job, owner, item), "carrying does not authorize unrelated use of saved gear");
            }
            job.def = JobDefOf.HaulToCell;
            map.Storage.Allows = false;
            check(!SavedGearRecovery.AllowsHaul(helper, job, owner, item), "storage filter revocation still wins after pickup");
            map.Storage.Allows = true; map.Storage.Enabled = false;
            check(!SavedGearRecovery.AllowsHaul(helper, job, owner, item), "disabled storage still revokes delivery");
            map.Storage.Enabled = true;
            job.targetB = new IntVec3(10);
            check(!SavedGearRecovery.AllowsHaul(helper, job, owner, item), "owner-inaccessible destination still rejected");
            job.targetB = new IntVec3(30);
            owner.Map = new Map();
            check(!SavedGearRecovery.AllowsHaul(helper, job, owner, item), "owner map change revokes carried continuation");
            owner.Map = map; state.Transition = ApparelTransition.Active;
            check(!SavedGearRecovery.AllowsHaul(helper, job, owner, item), "ended owner restoration revokes carried continuation");
            state.Transition = ApparelTransition.Restoring;

            // Native placement, then ending callback. No pickup/drop watchdog
            // pulse may masquerade as this actual owner-reachable delivery.
            item.Spawned = true; item.Holder = null; item.Position = job.targetB.Cell;
            helper.carryTracker.CarriedThing = null;
            SavedGearRecovery.NotifyEnded(helper, job);
            check(component.Woken == owner && GearRetrievalRoute.CanReach(owner, item), "actual safe placement wakes saved owner with a usable restoration route");
            check(owner.CanReserve(item) && !helper.CanReserve(item), "placed saved item returns to exclusive owner reservation");
            check(component.RestoringOwnerForSavedGear(item) == owner, "recovery preserves exact saved identity for restoration");

            // An actual competing job must still be released by the same
            // production watchdog body that the valid recovery bypassed.
            helper.CurJob = new Job { def = JobDefOf.Wear, targetA = item };
            check(component.OwnershipPulse(helper) && component.Releases == 1 && helper.CurJob.def == JobDefOf.Wait,
                "ownership watchdog still interrupts a competing outfit job");
        }
    }
}

// Native side effects for the verbatim production ownership watchdog. The
// policy itself and target lookup are extracted by the runner, not duplicated.
namespace AutomaticOutfitManager.Core
{
    public partial class AutomaticOutfitManagerGameComponent
    {
        public int Releases;
        public bool OwnershipPulse(Pawn pawn) => TryReleaseSavedGearNeededForRestoration(pawn, 100);
        private bool TryJobTransition(Pawn p, int tick, string context, Action action) { Releases++; action(); return true; }
        private void MakeReleasedSavedGearAvailable(Thing gear, Pawn owner) { WakeRestoringSavedGearOwner(owner); }
    }
}
namespace Verse
{
    public partial class Pawn
    {
        public Pawn() { jobs = new Pawn_JobTracker(this); }
        public Pawn_JobTracker jobs;
        public ApparelTracker apparel = new ApparelTracker();
    }
    public enum ThingPlaceMode { Near }
    public class ApparelTracker
    {
        public List<Apparel> WornApparel = new List<Apparel>();
        public bool TryDrop(Apparel a, out Apparel dropped, IntVec3 cell, bool forbidden) { dropped = a; WornApparel.Remove(a); return true; }
    }
    public partial class CarryTracker
    {
        public bool TryDropCarriedThing(IntVec3 cell, ThingPlaceMode mode, out Thing dropped)
        { dropped = CarriedThing; if (dropped == null) return false; dropped.Map = dropped.MapHeld; dropped.Holder = null; dropped.Spawned = true; dropped.Position = cell; CarriedThing = null; return true; }
    }
    public static class ForbiddenFixture
    {
        public static bool IsForbidden(this Thing t, Faction faction) => t.Forbidden;
        public static void SetForbidden(this Thing t, bool value, bool warn) { t.Forbidden = value; }
    }
}
namespace Verse.AI
{
    public enum JobCondition { InterruptForced }
    public class Pawn_JobTracker
    {
        private readonly Pawn pawn;
        public Pawn_JobTracker(Pawn pawn) { this.pawn = pawn; }
        public Job curJob => pawn.CurJob;
        public void ClearQueuedJobs(bool forced) { }
        public void StartJob(Job job, JobCondition end, object giver, bool resume, bool cancel) { pawn.CurJob = job; }
    }
}
namespace AutomaticOutfitManager.Patches
{
    public static partial class PawnJobTracker_StartJob_Patch
    {
        public static Job MakeSafeWaitJob(Pawn p, int ticks) => new Job { def = JobDefOf.Wait };
    }
}
