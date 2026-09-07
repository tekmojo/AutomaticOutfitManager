using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.Storage;
using RimWorld;
using Verse;
using Verse.AI;

internal static class LockerRecoveryTests
{
    internal static void Run(Action<bool,string> check)
    {
        var map = new Map();
        var bowman = new Pawn { Map=map };
        var lumi = new Pawn { Map=map };
        map.mapPawns.AllPawnsSpawned.AddRange(new[] {bowman,lumi});
        var component = AutomaticOutfitManagerGameComponent.Current = new AutomaticOutfitManagerGameComponent();
        var locker = new Area(map,30,40);
        var rule = new ApparelRule { Id="locker",Area=new Area(map,90),ChangingArea=locker,UsesExactWeapons=true };
        component.Rules.Add(rule);
        var garment = new Apparel { Map=map,Position=new IntVec3(15) };
        var weapon = new ThingWithComps { Map=map,Position=new IntVec3(16),def=new ThingDef {IsWeapon=true} };
        rule.RequiredApparel.Add(garment.def); rule.RequiredWeapons.Add(weapon.def);
        map.listerThings.Things.Add(weapon);
        StoreUtility.Cells = new[] {new IntVec3(30),new IntVec3(40)};
        StoreUtility.CurrentPriority=StoragePriority.Normal;
        StoreUtility.DestinationPriority=StoragePriority.Critical;
        StoreUtility.NativeGoodCell=true; StoreUtility.AlreadyStored=false;
        ReservationUtility.NativeAllows=true;
        ProtectedPathAvoidance.BlockClosestTouch=false;
        Find.TickManager.TicksGame=100;
        ManagedWorkClaimRegistry.ResetForLoadedGame();
        var claimItem = new Apparel {Map=map,Position=new IntVec3(17)};
        var claim = JobMaker.MakeJob(JobDefOf.HaulToCell,claimItem,new IntVec3(30));
        check(ManagedWorkClaimRegistry.TryClaim(bowman,claim),"Bowman owns prepared haul destination 30");
        var apparelScanner = new WorkGiver_LockerRestock();
        var weaponScanner = new WorkGiver_WeaponLockerRestock();
        foreach (var pair in new[] { Tuple.Create<WorkGiver_Scanner,Thing>(apparelScanner,garment),Tuple.Create<WorkGiver_Scanner,Thing>(weaponScanner,weapon) })
        {
            Job job = Issue(pair.Item1,lumi,pair.Item2,check);
            check(job?.targetB.Cell.Value==40,"free source skips claimed destination and uses second cell");
            check(LockerHaulDestination.Current==null,"locker cell-search scope cleaned after Has/Job");
            StoreUtility.Cells = new[] {new IntVec3(30)};
            check(!pair.Item1.HasJobOnThing(lumi,pair.Item2),"all destinations claimed => target unavailable before selection");
            check(pair.Item1.JobOnThing(lumi,pair.Item2)==null,"direct job query also unavailable");
            check(pair.Item1.HasJobOnThing(lumi,pair.Item2,true),"explicit forced query retains native claim behavior");
            check(pair.Item1.JobOnThing(lumi,pair.Item2,true)?.targetB.Cell.Value==30,"forced job stage agrees with forced availability");
            StoreUtility.Cells = new[] {new IntVec3(50)};
            check(!pair.Item1.HasJobOnThing(lumi,pair.Item2),"shared storage group cannot restock outside actual locker area");
            StoreUtility.Cells = new[] {new IntVec3(30),new IntVec3(40)};
        }
        ManagedWorkClaimRegistry.ReleaseAll(bowman);
        check(Issue(apparelScanner,lumi,garment,check)?.targetB.Cell.Value==30,"claim release immediately restores first candidate");
        check(ManagedWorkClaimRegistry.TryClaim(bowman,JobMaker.MakeJob(JobDefOf.HaulToCell,garment,new IntVec3(30))),"source claimed for preparation");
        check(!apparelScanner.HasJobOnThing(lumi,garment),"claimed source rejected during availability");
        ManagedWorkClaimRegistry.ReleaseAll(bowman);
        map.Storage.Allows=false;
        check(!apparelScanner.HasJobOnThing(lumi,garment),"disallowed storage filter wins");
        map.Storage.Allows=true; garment.Forbidden=true;
        check(!apparelScanner.HasJobOnThing(lumi,garment),"forbidden source not offered");
        garment.Forbidden=false; garment.Unreachable=true;
        check(!apparelScanner.HasJobOnThing(lumi,garment),"native unreachable source not offered");
        garment.Unreachable=false; ReservationUtility.NativeAllows=false;
        check(!apparelScanner.HasJobOnThing(lumi,garment),"native reservation rejection wins");
        ReservationUtility.NativeAllows=true; StoreUtility.ThrowSearch=true;
        try { apparelScanner.HasJobOnThing(lumi,garment); } catch(InvalidOperationException) { }
        check(LockerHaulDestination.Current==null,"locker scope clears after a thrown native search");
        StoreUtility.ThrowSearch=false;
        garment.Position=new IntVec3(30); StoreUtility.AlreadyStored=true;
        check(!apparelScanner.HasJobOnThing(lumi,garment),"accepted gear already in locker does not endlessly move");
        StoreUtility.AlreadyStored=false;

        // Reproduce the stalled exact saved vest, including ordinary hauling
        // which does not offer a strictly-better storage job for it.
        var vest = new Apparel { Map=map,Position=new IntVec3(10) };
        var state = new PawnApparelState {Pawn=bowman,Transition=ApparelTransition.Restoring};
        state.OriginalApparel.Add(vest); component.States[bowman]=state;
        component.Rules.Add(new ApparelRule { Id="unrelated",Area=new Area(map,10) });
        StoreUtility.Cells=new[] {new IntVec3(10),new IntVec3(20)};
        StoreUtility.CurrentPriority=StoreUtility.DestinationPriority=StoragePriority.Critical;
        StoreUtility.BetterStorageExists=false;
        check(new WorkGiver_Haul().JobOnThing(lumi,vest,false)==null,"native strictly-better hauling leaves already stored vest waiting");
        check(!rule.RequiredApparel.Contains(vest.def),"saved personal vest is not required work stock");
        check(!GearRetrievalRoute.CanReach(bowman,vest),"owner cannot cross unrelated protected source");
        check(!lumi.CanReserve(vest),"saved ownership is exclusive outside recovery query");
        RestorationPlanProgress.Observe(state,100,0,true);
        Job recovery=Issue(apparelScanner,lumi,vest,check);
        check(recovery?.targetB.Cell.Value==20,"normal restock scanner offers exact saved vest to equal-priority safe storage");
        check(recovery.count==1&&!recovery.haulOpportunisticDuplicates,"recovery never collects neighboring gear");
        check(SavedGearRecovery.CurrentProbe==null,"recovery ownership scope cleaned after query");
        check(component.SavedPawnFor(vest)==bowman,"query preserves exact owner");
        lumi.CurJob=recovery;
        check(lumi.CanReserve(vest),"admitted exact recovery haul reserves saved vest");
        vest.Spawned=false; vest.Holder=lumi; lumi.carryTracker.CarriedThing=vest;
        check(SavedGearRecovery.AllowsHaul(lumi,recovery,bowman,vest),"pickup retains safe exact delivery authorization");
        vest.Spawned=true; vest.Holder=null; lumi.carryTracker.CarriedThing=null; vest.Position=new IntVec3(20);
        SavedGearRecovery.NotifyEnded(lumi,recovery);
        check(component.Woken==bowman,"actual delivery wakes exact restoring owner");
        check(RestorationPlanProgress.CanProbe(state,101),"delivery bypasses blocked-plan throttle immediately");
        check(GearRetrievalRoute.CanReach(bowman,vest),"delivered vest has a permitted owner Wear route");
        check(bowman.CanReserve(vest)&&!lumi.CanReserve(vest),"after delivery saved Wear owns reservation again");
        check(!apparelScanner.HasJobOnThing(lumi,vest),"reachable saved vest is not offered for another recovery haul");
        check(state.OriginalApparel.Single()==vest,"snapshot still contains exact delivered instance for restoration");
        lumi.CurJob=null; vest.Position=new IntVec3(10);
        StoreUtility.DestinationPriority=StoragePriority.Normal;
        check(!apparelScanner.HasJobOnThing(lumi,vest),"recovery never downgrades storage priority");
        StoreUtility.DestinationPriority=StoragePriority.Critical; map.Storage.Allows=false;
        check(!apparelScanner.HasJobOnThing(lumi,vest),"saved-gear recovery still obeys storage filters");
        map.Storage.Allows=true;
        PausedAreaWorkFilter.DenySource=true;
        check(!apparelScanner.HasJobOnThing(lumi,vest),"Hauling access denial still blocks recovery at scanner boundary");
        check(apparelScanner.JobOnThing(lumi,vest)==null,"same access denial also blocks job creation");
        PausedAreaWorkFilter.DenySource=false;
        vest.def.stackLimit=2;
        check(!apparelScanner.HasJobOnThing(lumi,vest),"stackable saved identity remains protected");
        check(SavedGearRecovery.DescribeLastQuery(bowman,vest).Contains("limit=2"),"eligibility failure is explained before an active probe exists");
        vest.def.stackLimit=1;
        StoreUtility.ThrowSearch=true;
        try {apparelScanner.HasJobOnThing(lumi,vest);}catch(InvalidOperationException) { }
        check(SavedGearRecovery.CurrentProbe==null,"recovery scope clears after thrown search");
        StoreUtility.ThrowSearch=false;
        state.OriginalWeapon=weapon; state.WeaponInterventionActive=true; weapon.Position=new IntVec3(10);
        rule.RequiredWeapons.Clear(); rule.UsesExactWeapons=false; ProtectedPathAvoidance.BlockClosestTouch=true;
        check(!weaponScanner.ShouldSkip(lumi),"exact restoring weapon keeps weapon scanner eligible without a weapon rule");
        check(weaponScanner.PotentialWorkThingsGlobal(lumi).Single()==weapon,"weapon scan includes saved instance without scanning unrelated definitions");
        check(Issue(weaponScanner,lumi,weapon,check)?.targetB.Cell.Value==20,"saved primary uses same safe recovery path");

        var waiting = new PawnApparelState();
        check(RestorationPlanProgress.CanProbe(waiting,100),"new restoration may probe");
        check(!RestorationPlanProgress.Observe(waiting,100,0,true),"empty blocked plan is not progress");
        check(Enumerable.Range(101,119).All(tick=>!RestorationPlanProgress.CanProbe(waiting,tick)),"unchanged empty plan does not rebuild every component pulse");
        check(!RestorationPlanProgress.ShouldRestart(0),"no same-cell restart for empty blocked plan");
        check(RestorationPlanProgress.CanProbe(waiting,220),"bounded recheck notices external movement/access change");
        check(RestorationPlanProgress.Observe(waiting,220,1,true),"one newly available exact item wakes even if another is blocked");
        check(RestorationPlanProgress.ShouldRestart(1),"new executable step can restart restoration");
        RestorationPlanProgress.Wake(waiting);
        check(RestorationPlanProgress.CanProbe(waiting,221),"successful step clears probe throttle");
        RestorationPlanProgress.Observe(waiting,300,0,true);
        check(RestorationPlanProgress.CanProbe(waiting,1),"tick rollback/load does not retain a future throttle");

        var harness = new CoreRecoveryHarness();
        var stalledState = new PawnApparelState();
        RestorationPlanner.Planned.Clear(); RestorationPlanner.Unavailable=true; RestorationPlanner.Probes=0;
        for(int tick=0;tick<=26400;tick+=30) harness.Pulse(bowman,stalledState,tick);
        check(harness.Restarts==0 && harness.Completed==0,"actual core decision block preserves blocked snapshot without any empty-plan Goto restart over Bowman-length stall");
        check(RestorationPlanner.Probes<=221,"actual core throttles unchanged probes to at most once per 120 ticks");
        int probes=RestorationPlanner.Probes;
        RestorationPlanner.Planned.Add(new Job { def=JobDefOf.Wear,targetA=vest });
        harness.Pulse(bowman,stalledState,26520);
        check(harness.Restarts==1,"newly available exact Wear restarts immediately without waiting for old long retry deadline");
        check(stalledState.LastRestorationAttemptTick<0,"availability wake clears old retry timestamp");
        check(RestorationPlanner.Probes==probes+1,"availability recovery uses one watchdog plan check");
        RestorationPlanner.Planned.Clear(); RestorationPlanner.Unavailable=false;
        harness.Pulse(bowman,stalledState,26640);
        check(harness.Completed==1 && harness.Restarts==1,"actual core completion branch ends intervention after last saved step instead of another restart");
    }

    private static Job Issue(WorkGiver_Scanner scanner,Pawn pawn,Thing item,Action<bool,string> check)
    {
        // Match the native chosen-target boundary: accept the target first,
        // then ask JobOnThing. The production general scanner postfixes apply
        // to both stages through Harmony, including complete-job late rejection.
        check(scanner.HasJobOnThing(pawn,item),"scanner accepts available target");
        Job job=scanner.JobOnThing(pawn,item);
        check(job!=null,"accepted scanner target must yield an actual job, not the RimWorld mismatch error");
        return job;
    }
}

// Fixture effects around the verbatim production idle-restoration block.
internal partial class CoreRecoveryHarness
{
    internal PawnApparelState ActiveState;
    internal int Restarts,Completed;
    private const int RestorationIdleGraceTicks=240;
    private void EndIntervention(Pawn pawn) { Completed++; ActiveState=null; }
    private bool TryCompleteForeignMapDepartureWithUnavailableSavedGear(Pawn pawn,PawnApparelState state)=>false;
    private bool TryReleasePersistentlyUnavailableSavedWeapon(Pawn pawn,PawnApparelState state)=>false;
    private bool TryReleasePersistentlyRejectedSavedWeapon(Pawn pawn,PawnApparelState state)=>false;
    private void WakeRestoringSavedGearOwner(Pawn pawn) { ActiveState.LastRestorationAttemptTick=-1; ActiveState.ActiveIdleTicks=240; RestorationPlanProgress.Wake(ActiveState); }
    private string DescribeRestorationProgress(Pawn pawn,Job job)=>"fixture wait";
    private bool StartRestorationRecovery(Pawn pawn,PawnApparelState state,int tick,string context) { Restarts++; return true; }
    private PawnApparelState StateFor(Pawn pawn)=>ActiveState;
}

namespace AutomaticOutfitManager.Detection
{
    internal static class RestorationPlanner
    {
        internal static List<Job> Planned=new List<Job>();
        internal static bool Unavailable; internal static int Probes;
        internal static bool RecoveryCooldownActive(int tick,int last)=>last>=0 && tick>=last && tick-last<120;
        internal static void TryMakeHeldOriginalsAccessible(Pawn pawn,PawnApparelState state) { }
        internal static List<Job> BuildJobs(Pawn pawn,PawnApparelState state,ApparelRule rule,out bool unavailable) {Probes++;unavailable=Unavailable;return new List<Job>(Planned);}
    }
}

namespace AutomaticOutfitManager.Patches
{
    internal static class DeferredWorkScannerPatches { internal static bool Ready=>true; }
    internal static class PausedAreaWorkFilter
    {
        internal static bool DenySource;
        internal enum ScannerSignature { StandardThing }
        internal static IEnumerable<MethodBase> ScannerMethods(Type result,ScannerSignature signature) =>
            new[] {typeof(WorkGiver_LockerRestock),typeof(WorkGiver_WeaponLockerRestock)}
                .Select(t=>(MethodBase)t.GetMethod(result==typeof(bool)?"HasJobOnThing":"JobOnThing"));
        internal static bool ShouldReject(Pawn p,Thing t)=>DenySource;
        internal static bool ShouldRejectScannerTarget(WorkGiver_Scanner scanner,Pawn p,Thing t)=>
            ManagedWorkClaimRegistry.IsClaimedByOther(p,p.Map,t,t.Position);
    }
}
