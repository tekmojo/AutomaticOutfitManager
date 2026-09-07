using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

internal static class RecoveryTests
{
    static int checks;
    static void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
    static void Main()
    {
        try { Run(); LockerRecoveryTests.Run(Check); Console.WriteLine($"PASS {checks} saved gear recovery contracts (production policy and real Harmony patches)."); }
        catch (Exception error) { Console.Error.WriteLine("FAIL " + error); Environment.ExitCode = 1; }
    }

    static void Run()
    {
        new Harmony("aom.tests.saved-recovery").PatchAll(typeof(RecoveryTests).Assembly);
        var map = new Map();
        var owner = new Pawn { Map = map };
        var helper = new Pawn { Map = map };
        map.mapPawns.AllPawnsSpawned.AddRange(new[] { owner, helper });
        var vest = new Apparel { Map = map, Position = new IntVec3(10) };
        var other = new Apparel { Map = map, Position = vest.Position, def = vest.def };
        var state = new PawnApparelState { Transition = ApparelTransition.Restoring, OriginalApparel = new List<Apparel> { vest } };
        var component = AutomaticOutfitManagerGameComponent.Current = new AutomaticOutfitManagerGameComponent();
        component.States[owner] = state;
        component.States[helper] = new PawnApparelState();
        var foreign = new ApparelRule { Id = "foreign", Area = new Area(map, 10) };
        component.Rules.Add(foreign);
        var native = new WorkGiver_Haul();
        StoreUtility.Cells = new[] { new IntVec3(10), new IntVec3(20) };

        Check(!GearRetrievalRoute.CanReach(owner, vest), "Wear cannot stop beside the forbidden garment cell");
        Check(ProtectedPathAvoidance.LastEnd == PathEndMode.OnCell, "apparel uses native Wear end mode");
        var weapon = new ThingWithComps { Map = map, Position = vest.Position, def = new ThingDef { IsWeapon = true } };
        GearRetrievalRoute.CanReach(owner, weapon);
        Check(ProtectedPathAvoidance.LastEnd == PathEndMode.ClosestTouch, "weapon uses native Equip end mode");
        Check(!helper.CanReserve(vest), "ordinary reservation cannot take exact saved apparel");
        Check(!SavedGearRecovery.MatchesProbe(null, null), "no ambient null probe permission");
        var recovery = native.JobOnThing(helper, vest, false);
        Check(recovery?.targetB.Cell.Value == 20, "native hauling selects reachable storage instead of foreign source");
        Check(recovery.count == 1 && !recovery.haulOpportunisticDuplicates, "recovery carries only the exact saved instance");
        Check(SavedGearRecovery.CurrentProbe == null, "native query clears probe");
        Check(component.SavedPawnFor(vest) == owner, "recovery keeps exact saved ownership");
        Check(!helper.CanReserve(vest), "query permission does not leak to later reservations");
        Check(SavedGearRecovery.AllowsHaul(helper, recovery, owner, vest), "exact safe haul allowed at StartJob boundary");
        helper.CurJob = recovery;
        Check(helper.CanReserve(vest), "native reservation succeeds for admitted haul");
        Check(!ReservationUtility_SavedApparel_Patch.CanReserveForOutfit(helper, vest), "outfit selection cannot borrow haul permission");
        ReservationUtility.NativeAllows = false;
        Check(!helper.CanReserve(vest), "native and third party reservation refusal remains final");
        ReservationUtility.NativeAllows = true;
        Check(!SavedGearRecovery.AllowsHaul(helper, new Job { def = JobDefOf.Wear, targetA = vest }, owner, vest), "Wear remains blocked");
        Check(!SavedGearRecovery.AllowsHaul(helper, new Job { def = JobDefOf.Equip, targetA = vest }, owner, vest), "Equip remains blocked");
        Check(!SavedGearRecovery.AllowsHaul(helper, new Job { def = JobDefOf.DoBill, targetA = vest }, owner, vest), "bill use remains blocked");
        Check(!SavedGearRecovery.AllowsHaul(helper, new Job { def = JobDefOf.HaulToContainer, targetA = vest }, owner, vest), "container does not gain recovery permission");
        Check(!SavedGearRecovery.AllowsHaul(helper, recovery, owner, other), "same definition is not the exact saved item");
        vest.def.stackLimit = 2;
        Check(!SavedGearRecovery.AllowsHaul(helper, recovery, owner, vest), "stackable mod gear cannot merge away saved identity");
        vest.def.stackLimit = 1;
        vest.stackCount = 2;
        Check(!SavedGearRecovery.AllowsHaul(helper, recovery, owner, vest), "malformed stacked apparel cannot split saved identity");
        vest.stackCount = 1;
        recovery.targetQueueA = new List<LocalTargetInfo> { other };
        Check(!SavedGearRecovery.AllowsHaul(helper, recovery, owner, vest), "multi-item compatibility haul cannot borrow exact-item permission");
        recovery.targetQueueA = null;
        recovery.playerForced = true;
        Check(!SavedGearRecovery.AllowsHaul(helper, recovery, owner, vest), "forced orders do not use automatic recovery exception");
        recovery.playerForced = false;
        helper.Drafted = true;
        Check(!SavedGearRecovery.AllowsHaul(helper, recovery, owner, vest), "drafted helper stays native");
        helper.Drafted = false;
        helper.Faction = new Faction();
        Check(!SavedGearRecovery.AllowsHaul(helper, recovery, owner, vest), "uncontrolled guest cannot claim recovery");
        helper.Faction = Faction.OfPlayer;
        map.Storage.Allows = false;
        Check(!SavedGearRecovery.AllowsHaul(helper, recovery, owner, vest), "storage filter refusal wins on admission");
        Check(native.JobOnThing(helper, vest, false) == null, "native storage refusal yields no recovery job");
        map.Storage.Allows = true;
        recovery.targetB = new IntVec3(10);
        Check(!SavedGearRecovery.AllowsHaul(helper, recovery, owner, vest), "changed unsafe destination revoked");
        recovery.targetB = new IntVec3(20);
        StoreUtility.NativeGoodCell = false;
        Check(native.JobOnThing(helper, vest, false) == null, "native cell invalidity remains final");
        StoreUtility.NativeGoodCell = true;

        helper.CurJob = null;
        component.States[helper].PendingWorkJob = recovery;
        Check(helper.CanReserve(vest), "pending native haul retains exact reservation through outfit preparation");
        Check(SavedGearRecovery.InProgress(owner, vest), "pending haul protects saved weapon timeout path");
        component.States[helper].PendingWorkJob = null;
        Check(!SavedGearRecovery.InProgress(owner, vest), "cancelled pending haul leaves no stale claim");
        helper.CurJob = new Job { def = recovery.def, targetA = vest, targetB = recovery.targetB };
        Check(helper.CanReserve(vest), "reloaded native haul reconstructed from job and snapshot without serialized duplicate");
        vest.Spawned = false;
        vest.Holder = helper;
        helper.carryTracker.CarriedThing = vest;
        helper.Position = new IntVec3(25);
        Check(SavedGearRecovery.AllowsHaul(helper, helper.CurJob, owner, vest), "carried exact item finishes delivery across neutral space");
        Check(!StoreUtility.IsGoodStoreCell(new IntVec3(10), map, vest, helper, helper.Faction), "runtime destination replan cannot re-strand carried gear");
        Check(StoreUtility.IsGoodStoreCell(new IntVec3(20), map, vest, helper, helper.Faction), "runtime destination replan retains reachable storage");
        Check(SavedGearRecovery.InProgress(owner, vest), "carried saved item retains in-flight protection");
        component.States[owner].Transition = ApparelTransition.Active;
        Check(!SavedGearRecovery.AllowsHaul(helper, helper.CurJob, owner, vest), "ended restoration revokes exception");
        component.States[owner].Transition = ApparelTransition.Restoring;
        owner.Map = new Map();
        Check(!SavedGearRecovery.AllowsHaul(helper, helper.CurJob, owner, vest), "map change revokes exception");
        owner.Map = map;
        vest.Spawned = true;
        vest.Holder = null;
        helper.carryTracker.CarriedThing = null;
        vest.Position = new IntVec3(20);
        SavedGearRecovery.NotifyEnded(helper, helper.CurJob);
        Check(component.Woken == owner, "actual accessible placement wakes exact owner");
        Check(!SavedGearRecovery.AllowsHaul(helper, helper.CurJob, owner, vest), "accessible placed gear returns to exclusive restoration ownership");
        Check(!helper.CanReserve(vest), "hauler cannot steal item again after delivery");
        vest.Position = new IntVec3(10);
        component.Woken = null;
        SavedGearRecovery.NotifyEnded(helper, helper.CurJob);
        Check(component.Woken == null, "interrupted inaccessible drop is not a false progress signal");

        helper.CurJob = null;
        var outer = SavedGearRecovery.Begin(helper, vest, false);
        Check(SavedGearRecovery.MatchesProbe(helper, vest), "outer exact query scope established");
        var nestedPrevious = SavedGearRecovery.Begin(helper, other, false);
        Check(!SavedGearRecovery.MatchesProbe(helper, vest), "unrelated nested query cannot inherit permission");
        SavedGearRecovery.CurrentProbe = nestedPrevious;
        Check(SavedGearRecovery.MatchesProbe(helper, vest), "nested query restores previous context");
        SavedGearRecovery.CurrentProbe = outer;
        WorkGiver_Haul.Throw = true;
        try { native.JobOnThing(helper, vest, false); } catch (InvalidOperationException) { }
        Check(SavedGearRecovery.CurrentProbe == null, "Harmony finalizer clears context on native exception");
        WorkGiver_Haul.Throw = false;
        Check(native.JobOnThing(helper, vest, true) == null && SavedGearRecovery.CurrentProbe == null,
            "forced query does not acquire automatic ownership bypass");

        var prior = SavedGearRecovery.Begin(helper, vest, false);
        IHaulDestination container;
        Check(!StoreUtility.TryFindBestBetterNonSlotGroupStorageFor(vest, helper, map, StoragePriority.Normal,
            helper.Faction, out container, false, true), "recovery scan excludes containers");
        SavedGearRecovery.CurrentProbe = prior;
        Check(StoreUtility.TryFindBestBetterNonSlotGroupStorageFor(vest, helper, map, StoragePriority.Normal,
            helper.Faction, out container, false, true), "ordinary container storage remains available");
        state.OriginalApparel.Clear();
        Check(!SavedGearRecovery.AllowsHaul(helper, recovery, owner, vest), "cleared snapshot cannot authorize recovery");
        state.OriginalWeapon = weapon;
        state.WeaponInterventionActive = true;
        ProtectedPathAvoidance.BlockClosestTouch = true;
        var weaponHaul = native.JobOnThing(helper, weapon, false);
        Check(weaponHaul != null, "same safe recovery supports an exact saved primary");
        helper.CurJob = weaponHaul;
        Check(helper.CanReserve(weapon), "saved weapon reservation permits exact recovery haul");
        Check(!ReservationUtility_SavedApparel_Patch.CanReserveForOutfit(helper, weapon), "weapon outfit finder cannot claim another owner's primary");
        state.RestorationSourceRuleIds.Add(foreign.Id);
        Check(GearRetrievalRoute.CanReach(owner, weapon), "tracked owning source retains legitimate retrieval access");
        Check(!SavedGearRecovery.AllowsHaul(helper, weaponHaul, owner, weapon), "reachable tracked source needs no recovery exception");
        Check(component.SavedPawnForWeapon(weapon) == owner, "weapon owner remains unchanged");

        state.RestorationSourceRuleIds.Clear(); helper.CurJob = null;
        AomLog.DetailedEnabled = true;
        Check(SavedGearRecovery.DescribeLastQuery(owner, weapon).Contains("no eligible native hauling query"),
            "absence of native recovery queries is explicit");
        StoreUtility.BetterStorageExists = false; StoreUtility.CurrentPriority = StoragePriority.Critical;
        Check(native.JobOnThing(helper, weapon, false) == null, "diagnostics never override native better-storage requirement");
        string last = SavedGearRecovery.DescribeLastQuery(owner, weapon);
        Check(last.Contains("sourcePriority=Critical") && last.Contains("nativeEligible=True") &&
            last.Contains("storageCellsChecked=0") && last.Contains("returned no job"),
            "query distinguishes native eligibility from a storage search which finds no better cell");
        StoreUtility.BetterStorageExists = true; ReservationUtility.NativeAllows = false;
        Check(native.JobOnThing(helper, weapon, false) == null &&
            SavedGearRecovery.DescribeLastQuery(owner, weapon).Contains("nativeEligible=False"),
            "native hauling refusal is recorded without granting reservation access");
        ReservationUtility.NativeAllows = true; StoreUtility.NativeGoodCell = false;
        Check(native.JobOnThing(helper, weapon, false) == null &&
            SavedGearRecovery.DescribeLastQuery(owner, weapon).Contains("ordinaryCellRejections=2"),
            "ordinary storage-filter or cell rejection stays authoritative");
        StoreUtility.NativeGoodCell = true;
        Check(native.JobOnThing(helper, weapon, false) != null &&
            SavedGearRecovery.DescribeLastQuery(owner, weapon).Contains("ownerRouteRejections=1"),
            "owner-unreachable cells distinguished from ordinary storage refusal");
        Check(SavedGearRecovery.CurrentProbe == null && component.SavedPawnForWeapon(weapon) == owner,
            "diagnostic sampling preserves scope cleanup and exact saved owner");
        Check(SavedGearRecovery.DescribeLastQuery(helper, weapon).Contains("no eligible native hauling query"),
            "another owner cannot inherit a stale recovery result");
        Find.TickManager.TicksGame = 0;
        Check(SavedGearRecovery.DescribeLastQuery(owner, weapon).Contains("no eligible native hauling query"),
            "tick rollback does not report future recovery evidence");
    }
}

namespace Verse
{
    public class TickManager { public int TicksGame = 100; }
    public static class Find { public static TickManager TickManager = new TickManager(); }
    public struct IntVec3 { public static IntVec3 Invalid => new IntVec3(-1); public static bool operator ==(IntVec3 a,IntVec3 b)=>a.Value==b.Value; public static bool operator !=(IntVec3 a,IntVec3 b)=>a.Value!=b.Value; public override bool Equals(object o)=>o is IntVec3 c && c==this; public override int GetHashCode()=>Value; public int Value; public IntVec3(int value) { Value = value; } public bool IsValid => Value >= 0; public bool InBounds(Map map) => Value >= 0 && Value < 100; }
    public class Map { public ListerThings listerThings=new ListerThings(); public MapPawns mapPawns = new MapPawns(); public RimWorld.Destination Storage = new RimWorld.Destination(); }
    public class ListerThings { public List<Thing> Things=new List<Thing>(); public List<Thing> ThingsOfDef(ThingDef d)=>Things.Where(t=>t.def==d).ToList(); }
    public enum ThingRequestGroup { Apparel, Weapon } public struct ThingRequest { public static ThingRequest ForGroup(ThingRequestGroup g)=>new ThingRequest(); }
    public enum Danger { Some, Deadly }
    public static class Reach { public static bool CanReach(this Pawn p,Thing t,PathEndMode mode,Danger danger)=>!t.Unreachable; public static bool IsForbidden(this Thing t,Pawn p)=>t.Forbidden; }
    public class MapPawns { public List<Pawn> AllPawnsSpawned = new List<Pawn>(); }
    public class Area { public Map Map; HashSet<int> cells; public Area(Map map, params int[] values) { Map = map; cells = new HashSet<int>(values); } public IEnumerable<IntVec3> ActiveCells=>cells.Select(v=>new IntVec3(v)); public bool this[IntVec3 cell] => cells.Contains(cell.Value); }
    public class ThingDef { public bool IsWeapon; public int stackLimit = 1; }
    public class Thing { public Map Map; public bool Forbidden, Unreachable, Destroyed, Spawned = true; public Pawn Holder; public int stackCount = 1; public ThingDef def = new ThingDef(); public IntVec3 Position; public IntVec3 PositionHeld => Holder?.Position ?? Position; public Map MapHeld => Holder?.Map ?? Map; public string LabelCap => "gear"; public string ThingID => "exact"; }
    public class ThingWithComps : Thing { }
    public class Pawn { public Map Map; public IntVec3 Position; public Faction Faction = Faction.OfPlayer; public bool Spawned=true, Drafted, Downed, InMentalState; public Job CurJob; public CarryTracker carryTracker = new CarryTracker(); public string LabelShortCap => "pawn"; }
    public class CarryTracker { public Thing CarriedThing; }
    public struct LocalTargetInfo { public Thing Thing; public IntVec3 Cell; public bool HasThing => Thing != null; public bool IsValid => HasThing || Cell.Value > 0; public static implicit operator LocalTargetInfo(Thing t) => new LocalTargetInfo { Thing = t, Cell = t.Position }; public static implicit operator LocalTargetInfo(IntVec3 c) => new LocalTargetInfo { Cell = c }; }
    public static class GenAdj { public static IEnumerable<IntVec3> CellsOccupiedBy(Thing t) => new[] { t.Position }; }
}
namespace Verse.AI
{
    public static class HaulAIUtility
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool PawnCanAutomaticallyHaulFast(Pawn pawn, Thing t, bool forced) => pawn.CanReserve(t);
    }
    public enum PathEndMode { OnCell, Touch, ClosestTouch }
    public class JobDef { public string defName="job"; }
    public static class JobMaker { public static Job MakeJob(JobDef d,Thing t,IntVec3 c)=>new Job { def=d,targetA=t,targetB=c }; }
    public class Job { public JobDef def; public bool playerForced, haulOpportunisticDuplicates; public int count = 1; public LocalTargetInfo targetA, targetB, targetC; public List<LocalTargetInfo> targetQueueA, targetQueueB; }
    public static class ReservationUtility
    {
        public static bool NativeAllows = true;
        [MethodImpl(MethodImplOptions.NoInlining)] public static bool CanReserve(this Pawn pawn, LocalTargetInfo target) => NativeAllows;
        [MethodImpl(MethodImplOptions.NoInlining)] public static bool CanReserveAndReach(Pawn pawn, LocalTargetInfo target) => NativeAllows;
    }
}
namespace RimWorld
{
    public class Faction { public static Faction OfPlayer = new Faction(); }
    public class Apparel : ThingWithComps { }
    public static class JobDefOf { public static JobDef Wear = new JobDef(), Equip = new JobDef(), DoBill = new JobDef(), HaulToCell = new JobDef(), HaulToContainer = new JobDef(); }
    public enum StoragePriority { Unstored, Low, Normal, Critical }
    public interface ISlotGroup { }
    public interface IHaulDestination { bool HaulDestinationEnabled {get;} bool Accepts(Thing t); }
    public class Destination : IHaulDestination, ISlotGroup { public bool HaulDestinationEnabled=>true; public bool Allows = true; public bool Accepts(Thing t) => Allows; }
    public static class StoreUtility
    {
        public static IntVec3[] Cells; public static bool NativeGoodCell = true;
        public static bool BetterStorageExists = true;
        public static StoragePriority CurrentPriority = StoragePriority.Normal;
        public static StoragePriority CurrentStoragePriorityOf(Thing t, bool forced) => CurrentPriority;
        public static bool AlreadyStored, ThrowSearch;
        public static StoragePriority DestinationPriority=StoragePriority.Critical;
        public static IHaulDestination CurrentHaulDestinationOf(Thing t)=>AlreadyStored?t.Map.Storage:null;
        public static ISlotGroup GetSlotGroup(this IntVec3 cell, Map map) => map.Storage;
        public static bool TryFindBestBetterStoreCellForIn(Thing t,Pawn pawn,Map map,StoragePriority min,Faction faction,ISlotGroup group,out IntVec3 cell)=>TryFindBestBetterStoreCellFor(t,pawn,map,min,faction,out cell);
        public static bool TryFindBestBetterStoreCellFor(Thing t,Pawn pawn,Map map,StoragePriority min,Faction faction,out IntVec3 cell,bool needAccurateResult=true) {
            if(ThrowSearch) throw new InvalidOperationException("storage search failure");
            cell=IntVec3.Invalid;
            if(DestinationPriority<=min)return false;
            foreach(var c in Cells)if(IsGoodStoreCell(c,map,t,pawn,faction)){cell=c;return true;}
            return false;
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public static bool IsGoodStoreCell(IntVec3 c, Map map, Thing t, Pawn carrier, Faction faction) => NativeGoodCell && map.Storage.Accepts(t);
        [MethodImpl(MethodImplOptions.NoInlining)] public static bool TryFindBestBetterNonSlotGroupStorageFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, out IHaulDestination haulDestination, bool acceptSamePriority, bool requiresDestReservation) { haulDestination = map.Storage; return true; }
    }
    public class WorkGiver_Scanner {
        public virtual ThingRequest PotentialWorkThingRequest=>default;
        public virtual PathEndMode PathEndMode=>PathEndMode.ClosestTouch;
        public virtual bool ShouldSkip(Pawn pawn,bool forced=false)=>false;
        public virtual IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)=>pawn.Map.listerThings.Things;
        public virtual bool HasJobOnThing(Pawn pawn,Thing t,bool forced=false)=>JobOnThing(pawn,t,forced)!=null;
        public virtual Job JobOnThing(Pawn pawn,Thing t,bool forced=false)=>null;
    }
    public class WorkGiver_Haul : WorkGiver_Scanner
    {
        public static bool Throw;
        // Native-boundary fixture: reservation and each storage decision go
        // through production Harmony patches; pathfinding is modeled below.
        [MethodImpl(MethodImplOptions.NoInlining)] public override Job JobOnThing(Pawn pawn, Thing t, bool forced)
        {
            if (Throw) throw new InvalidOperationException("native failure");
            if (!HaulAIUtility.PawnCanAutomaticallyHaulFast(pawn, t, forced) || !StoreUtility.BetterStorageExists) return null;
            foreach (var cell in StoreUtility.Cells)
                if (StoreUtility.IsGoodStoreCell(cell, pawn.Map, t, pawn, pawn.Faction))
                    return new Job { def = JobDefOf.HaulToCell, targetA = t, targetB = cell, count = 99999, haulOpportunisticDuplicates = true };
            return null;
        }
    }
}
namespace AutomaticOutfitManager.State
{
    public enum ApparelTransition { Preparing, Active, ReturningToChangingArea, Restoring }
    public class PawnApparelState
    {
        public int ActiveIdleTicks, UnavailableRestorationAttempts; public int LastRestorationAttemptTick=-1; public bool MapDepartureRequested;
        public Pawn Pawn; public ApparelTransition Transition; public List<Apparel> OriginalApparel = new List<Apparel>(), ManagedApparel = new List<Apparel>();
        public Thing OriginalWeapon; public bool WeaponInterventionActive, WeaponRestorationRequested; public Job PendingWorkJob;
        public string ActiveRuleId; public List<string> CurrentRuleIds = new List<string>(), RestorationSourceRuleIds = new List<string>();
        public bool IsManagedWeapon(Thing t) => false; public bool IsPreparationApparel(Apparel a) => false;
    }
}
namespace AutomaticOutfitManager.Rules { public class ApparelRule { public bool Enabled=true,UsesExactWeapons; public string Id; public Area Area,ChangingArea; public List<ThingDef> RequiredApparel=new List<ThingDef>(),RequiredWeapons=new List<ThingDef>(); } }
namespace AutomaticOutfitManager.Core
{
    public class AutomaticOutfitManagerGameComponent
    {
        public static AutomaticOutfitManagerGameComponent Current;
        public Dictionary<Pawn, PawnApparelState> States = new Dictionary<Pawn, PawnApparelState>(); public List<ApparelRule> Rules = new List<ApparelRule>(); public Pawn Woken;
        public List<PawnApparelState> PawnStates=>States.Select(kv=>{kv.Value.Pawn=kv.Key;return kv.Value;}).ToList();
        public static void ReleaseNativeReservations(Pawn pawn,Job job) { }
        public bool IsManagedWeaponDefinition(ThingDef d)=>Rules.Any(r=>r.RequiredWeapons.Contains(d));
        public PawnApparelState StateFor(Pawn pawn) => pawn != null && States.TryGetValue(pawn, out var state) ? state : null;
        public Pawn SavedPawnFor(Apparel a) => States.FirstOrDefault(kv => kv.Value.OriginalApparel.Contains(a)).Key;
        public Pawn SavedPawnForWeapon(Thing w) => States.FirstOrDefault(kv => w != null && kv.Value.OriginalWeapon == w).Key;
        public Pawn RestoringOwnerForSavedGear(Thing gear) { var owner = gear is Apparel a ? SavedPawnFor(a) : SavedPawnForWeapon(gear); return StateFor(owner)?.Transition == ApparelTransition.Restoring ? owner : null; }
        public bool IsManagedApparel(Apparel a) => SavedPawnFor(a) != null;
        public bool IsManagedWeaponAssignedToOtherPawn(Thing t, Pawn pawn) => false;
        public bool IsManagedApparelAssignedToOtherPawn(Apparel a, Pawn pawn) => false;
        public void WakeRestoringSavedGearOwner(Pawn owner) { Woken = owner; RestorationPlanProgress.Wake(StateFor(owner)); }
    }
    public static class AomLog { public static bool DetailedEnabled = false; public static bool ShouldLogDetailed(Pawn p, string k, int ticks) => true; public static void Detailed(string message) { } }
}
namespace AutomaticOutfitManager.Storage
{
    public static class ManagedWeaponClassifier { public static bool Matches(ThingDef d) => false; }
    public static class ManagedApparelClassifier { public static bool Matches(ThingDef d) => false; }
}
namespace AutomaticOutfitManager.Detection
{
    public static class RuleEvaluator { public static IEnumerable<ApparelRule> EnabledRulesForMap(Map map) => AutomaticOutfitManagerGameComponent.Current.Rules.Where(rule => rule.Area?.Map == map); }
}
namespace AutomaticOutfitManager.Patches
{
    public static class ProtectedPathAvoidance
    {
        public static PathEndMode? LastEnd; public static bool BlockClosestTouch;
        public static bool SegmentAvoidsRules(Pawn pawn, IntVec3 start, LocalTargetInfo target, List<ApparelRule> rules, Predicate<IntVec3> unsafeCell = null, PathEndMode? exactEndMode = null)
        {
            LastEnd = exactEndMode;
            // Minimal reproduction: Touch reaches the neutral neighboring cell;
            // OnCell cannot enter the protected target. Optional weapon case is
            // an interior item with no unprotected adjacent cell.
            return (exactEndMode != PathEndMode.OnCell && !BlockClosestTouch) ||
                !rules.Any(rule => rule.Area[target.HasThing ? target.Thing.Position : target.Cell]);
        }
    }
}

namespace AutomaticOutfitManager.Detection { public static class TransitionActivityDiagnostics { public static void Rejected(Pawn p,Job j,string r) { } } }
namespace AutomaticOutfitManager.Patches {
    public static class PawnJobTracker_StartJob_Patch {
        public static bool IsNativeEmergencySafetyJob(Job j)=>false;
        public static bool IsAssignedTransitionApparelJob(PawnApparelState s,Job j)=>j.def==JobDefOf.Wear && s.OriginalApparel.Contains(j.targetA.Thing);
        public static bool IsAssignedTransitionWeaponJob(PawnApparelState s,Job j)=>j.def==JobDefOf.Equip && s.OriginalWeapon==j.targetA.Thing;
    }
}
