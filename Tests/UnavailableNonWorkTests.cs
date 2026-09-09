using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using Verse;
using Verse.AI;

class UnavailableNonWorkTests
{
    static int checks;
    static void Check(bool pass, string name) { if (!pass) throw new Exception("ASSERT: " + name); checks++; }
    static Job Work(string name, int cell) => new Job { def=new JobDef { defName=name }, targetA=cell };
    static Pawn Reset(out ApparelRule rule)
    {
        UnavailableWorkRegistry.ResetForLoadedGame(); ProtectedBoundaryRetryRegistry.Pending=null;
        var pawn=new Pawn { Map=new Map(), Position=10, thingIDNumber=1 };
        rule=new ApparelRule { Id="dining", Name="Dining", Area=new Area { Map=pawn.Map, Cells=new HashSet<IntVec3>{10,11} },
            ChangingArea=new Area { Map=pawn.Map, Cells=new HashSet<IntVec3>{90} } };
        AutomaticOutfitManagerGameComponent.Current=new AutomaticOutfitManagerGameComponent { Rules=new List<ApparelRule>{rule} };
        return pawn;
    }
    static int Main()
    {
        try { Run(); return 0; }
        catch (Exception error) { Console.WriteLine(error.Message); return 1; }
    }
    static void Run()
    {
        var pawn=Reset(out var rule); pawn.Position=5;
        var blocked=Work("DoBill",40); var other=Work("DoBill",50);
        ProtectedBoundaryRetryRegistry.Pending=blocked;
        PawnJobTracker_StartJob_Patch.Reject(pawn,rule,blocked);
        // Native selection sees an outside worksite with a late ingredient in
        // Dining. A rejected attempt must let the next candidate own the job.
        var tracker=new Pawn_JobTracker(pawn);
        tracker.ScanAndStart(new[]{blocked,other});
        Check(tracker.Current==other,"outside ingredient task yields to unrelated native work");
        Check(ProtectedBoundaryRetryRegistry.Pending==null,"unavailable boundary continuation retired");
        Check(UnavailableWorkRegistry.ShouldReject(pawn,pawn.Map,null,40),"scanner filters same failed outside worksite");
        Check(UnavailableWorkRegistry.ShouldReject(pawn,Work("HaulToCell",10)),"inside-area targets remain blocked");
        Check(!UnavailableWorkRegistry.ShouldReject(pawn,Work("HaulToCell",50)),"unrelated hauling stays available");
        var second=new Pawn { Map=pawn.Map, Position=5, thingIDNumber=2 };
        PawnJobTracker_StartJob_Patch.Reject(second,rule,blocked);
        AutomaticOutfitManagerGameComponent.Current.NotifyRuleRequirementsChanged(rule.Id,"correct apparel selected");
        Check(!UnavailableWorkRegistry.ShouldReject(pawn,blocked) && !UnavailableWorkRegistry.ShouldReject(second,blocked),"selection edit releases stateless pawns immediately");
        tracker.ScanAndStart(new[]{blocked,other});
        Check(tracker.Current==blocked,"corrected selection lets original native task resume");

        pawn=Reset(out rule); pawn.Position=5; rule.DefaultToSavedPersonalOutfit=true;
        PawnJobTracker_StartJob_Patch.Reject(pawn,rule,blocked);
        Check(!UnavailableWorkRegistry.ShouldReject(pawn,blocked),"saved-personal continuation policy preserved");
        rule.DefaultToSavedPersonalOutfit=false; rule.IsNonWork=false;
        PawnJobTracker_StartJob_Patch.Reject(pawn,rule,blocked);
        Check(!UnavailableWorkRegistry.ShouldReject(pawn,blocked),"ordinary Work policy preserved");
        rule.IsNonWork=true; blocked.playerForced=true;
        PawnJobTracker_StartJob_Patch.Reject(pawn,rule,blocked);
        Check(!UnavailableWorkRegistry.ShouldReject(pawn,blocked),"forced order not added to autonomous blacklist");
        blocked.playerForced=false;

        pawn=Reset(out rule); tracker=new Pawn_JobTracker(pawn);
        var proposed=Work("DoBill",40); ThinkNode giver=null; JobTag? tag=null;
        Check(PawnJobTracker_StartJob_Patch.Exit(tracker,pawn,ref proposed,ref giver,ref tag),"failed occupied selection obtains exit");
        Check(proposed.targetA.Cell==12,"Non-Work shortage uses nearby exit instead of distant locker");
        Check(!rule.Area[proposed.targetA.Cell],"exit destination outside area");
        Check(tracker.Clears==1 && AutomaticOutfitManagerGameComponent.Released==1,"failed work queue and native reservations released");
        // The nearest candidate (9) has a route that exits then re-enters.
        // Production path validation rejects it; the next route exits once.
        Check(ProtectedPathAvoidance.RejectedReentry>0,"re-entry route rejected before selecting exit");
        var foreign=new ApparelRule { Id="storage", Area=new Area { Map=pawn.Map, Cells=new HashSet<IntVec3>{12} } };
        AutomaticOutfitManagerGameComponent.Current.Rules.Add(foreign); proposed=blocked;
        Check(PawnJobTracker_StartJob_Patch.Exit(tracker,pawn,ref proposed,ref giver,ref tag) && proposed.targetA.Cell==13,"local exit cannot enter unrelated protected storage");
        AutomaticOutfitManagerGameComponent.Current.Rules.Remove(foreign);
        pawn.Position=12; proposed=other;
        Check(!PawnJobTracker_StartJob_Patch.Exit(tracker,pawn,ref proposed,ref giver,ref tag) && proposed==other,"already-outside worker gets no locker detour");
        pawn.Position=10; rule.DefaultToSavedPersonalOutfit=true; proposed=blocked;
        Check(PawnJobTracker_StartJob_Patch.Exit(tracker,pawn,ref proposed,ref giver,ref tag) && proposed.targetA.Cell==90,"saved-outfit locker preference preserved");

        pawn=Reset(out rule); tracker=new Pawn_JobTracker(pawn);
        // Walls and unusable door thresholds occupy the immediate candidate
        // ring. A native path can cross a threshold without ending there.
        pawn.Map.StandingCells=new HashSet<IntVec3>{14,15}; proposed=Work("Wait",-1);
        Check(PawnJobTracker_StartJob_Patch.Exit(tracker,pawn,ref proposed,ref giver,ref tag) && proposed.targetA.Cell==14,"exit finishes beyond blocked threshold");
        Check(!rule.Area[proposed.targetA.Cell],"threshold exit remains outside protected room");
        pawn.Map.UnavailableCells.Add(14); proposed=blocked;
        Check(PawnJobTracker_StartJob_Patch.Exit(tracker,pawn,ref proposed,ref giver,ref tag) && proposed.targetA.Cell==15,"reserved exterior cell yields to another destination");
        pawn.Map.UnreachableCells.Add(15); proposed=blocked;
        Check(!PawnJobTracker_StartJob_Patch.Exit(tracker,pawn,ref proposed,ref giver,ref tag),"sealed room does not invent a reachable exit");
        pawn.Map.UnreachableCells.Clear(); pawn.Map.UnavailableCells.Clear();
        foreign=new ApparelRule { Id="storage", Area=new Area { Map=pawn.Map, Cells=new HashSet<IntVec3>{14,15} } };
        AutomaticOutfitManagerGameComponent.Current.Rules.Add(foreign); proposed=blocked;
        Check(!PawnJobTracker_StartJob_Patch.Exit(tracker,pawn,ref proposed,ref giver,ref tag),"expanded exit cannot end in unrelated protected storage");

        pawn=Reset(out rule); rule.Area.Cells.Add(12); pawn.Map.CompareNativeRoutes=true;
        Check(ProtectedPathAvoidance.SegmentAvoidsRules(pawn,10,16,new List<ApparelRule>{rule},allowInitialEgress:true),"initial occupied route avoids entry penalty");
        Check(ProtectedPathAvoidance.LastCostRules.Count==0,"occupied area omitted only from native path costs");
        Check(!ProtectedPathAvoidance.SegmentAvoidsRules(pawn,10,16,new List<ApparelRule>{rule}),"strict retrieval still rejects occupied path");
        foreign=new ApparelRule { Id="storage", Area=new Area { Map=pawn.Map, Cells=new HashSet<IntVec3>{99} } };
        Check(ProtectedPathAvoidance.SegmentAvoidsRules(pawn,10,16,new List<ApparelRule>{rule,foreign},allowInitialEgress:true) && ProtectedPathAvoidance.LastCostRules.SequenceEqual(new[]{foreign}),"unoccupied rule retains avoidance cost during egress");
        pawn.Map.CompareNativeRoutes=false;
        rule.Area.Cells.Remove(12);
        Check(!ProtectedPathAvoidance.SegmentAvoidsRules(pawn,10,9,new List<ApparelRule>{rule},allowInitialEgress:true),"occupied rule still rejects re-entry after cost exemption");

        pawn=Reset(out rule); pawn.jobs=new Pawn_JobTracker(pawn); pawn.pather.Moving=true;
        PawnJobTracker_StartJob_Patch.Preparations=0;
        Check(PawnJobTracker_StartJob_Patch.TryPrepareForOccupiedRules(pawn,new[]{rule}) && pawn.jobs.Current.def.defName=="AutomaticOutfitManager_LockerReturn" && PawnJobTracker_StartJob_Patch.Preparations==0,"unchecked occupant exits before available gear preparation");
        Check(pawn.jobs.Rewrites==0,"native admission keeps the exact exit job without another redirect");
        pawn.IsChild=true; pawn.pather.Moving=false;
        Check(PawnJobTracker_StartJob_Patch.TryPrepareForOccupiedRules(pawn,new[]{rule}) && pawn.jobs.Current.def.defName=="AutomaticOutfitManager_LockerReturn" && PawnJobTracker_StartJob_Patch.Preparations==0,"unwearable child takes the same exit before preparation");
        rule.DefaultToSavedPersonalOutfit=true;
        Check(PawnJobTracker_StartJob_Patch.TryPrepareForOccupiedRules(pawn,new[]{rule}) && PawnJobTracker_StartJob_Patch.Preparations==1,"checked saved-personal occupancy keeps original planner");
        rule.DefaultToSavedPersonalOutfit=false; rule.IsNonWork=false;
        Check(PawnJobTracker_StartJob_Patch.TryPrepareForOccupiedRules(pawn,new[]{rule}) && PawnJobTracker_StartJob_Patch.Preparations==2,"ordinary Work occupancy keeps original planner");

        pawn=Reset(out rule); tracker=new Pawn_JobTracker(pawn); proposed=Work("DoBill",40); pawn.pather.Moving=true;
        Check(PawnJobTracker_StartJob_Patch.Redirect(tracker,pawn,ref proposed,ref giver,ref tag) && proposed.def.defName=="AutomaticOutfitManager_LockerReturn","moving adult task yields to unchecked mismatch exit");
        proposed=Work("DoBill",40); proposed.playerForced=true;
        Check(!PawnJobTracker_StartJob_Patch.Redirect(tracker,pawn,ref proposed,ref giver,ref tag),"explicit player order keeps native control");
        proposed.playerForced=false; pawn.NativeOverride=true;
        Check(!PawnJobTracker_StartJob_Patch.Redirect(tracker,pawn,ref proposed,ref giver,ref tag),"draft mental emergency and downed policy keeps native control");
        pawn.NativeOverride=false; rule.Satisfied=true;
        Check(!PawnJobTracker_StartJob_Patch.Redirect(tracker,pawn,ref proposed,ref giver,ref tag),"matching outfit permits continued native task");
        rule.Satisfied=false; rule.DefaultToSavedPersonalOutfit=true;
        Check(!PawnJobTracker_StartJob_Patch.Redirect(tracker,pawn,ref proposed,ref giver,ref tag),"saved personal option does not force selected-outfit egress");
        rule.DefaultToSavedPersonalOutfit=false; pawn.Position=5;
        Check(!PawnJobTracker_StartJob_Patch.Redirect(tracker,pawn,ref proposed,ref giver,ref tag),"outside pawn retains task for normal entry preparation");
        pawn.Position=10; pawn.carryTracker.CarriedThing=new Thing();
        Check(!PawnJobTracker_StartJob_Patch.Redirect(tracker,pawn,ref proposed,ref giver,ref tag),"carried thing keeps existing placement handling");
        pawn.carryTracker.CarriedThing=null;
        var state=new PawnApparelState { Pawn=pawn,Transition=ApparelTransition.Preparing,PendingWorkJob=Work("Ingest",40) };
        AutomaticOutfitManagerGameComponent.Current.TestState=state;
        Check(!PawnJobTracker_StartJob_Patch.Redirect(tracker,pawn,ref proposed,ref giver,ref tag),"existing exact preparation continuation is preserved");
        state.Transition=ApparelTransition.Restoring;
        Check(!PawnJobTracker_StartJob_Patch.Redirect(tracker,pawn,ref proposed,ref giver,ref tag),"existing restoration queue is preserved");
        state.Transition=ApparelTransition.Active; pawn.Hazard=true;
        Check(!PawnJobTracker_StartJob_Patch.Redirect(tracker,pawn,ref proposed,ref giver,ref tag),"hazard protection retains existing recovery planner");
        pawn.Hazard=false;
        Check(PawnJobTracker_StartJob_Patch.Redirect(tracker,pawn,ref proposed,ref giver,ref tag) && state.RecallRequested && state.PendingWorkJob==null,"managed outfit exits with snapshot retained and old task cleared");
        Console.WriteLine("Unavailable Non-Work contracts passed: " + checks);
    }
}
namespace Verse
{
    public class Map
    {
        public HashSet<IntVec3> StandingCells;
        public HashSet<IntVec3> UnavailableCells=new HashSet<IntVec3>(),UnreachableCells=new HashSet<IntVec3>();
        public bool CompareNativeRoutes;
    }
    public struct IntVec3
    {
        public int Value; public static IntVec3 Invalid=>-1; public bool IsValid=>Value>=0;
        public bool InBounds(Map m)=>m!=null && Value>=0 && Value<100;
        public bool Standable(Map m)=>m.StandingCells==null||m.StandingCells.Contains(this);
        public int DistanceToSquared(IntVec3 other)=>(Value-other.Value)*(Value-other.Value);
        public static implicit operator IntVec3(int v)=>new IntVec3{Value=v};
        public static bool operator ==(IntVec3 a,IntVec3 b)=>a.Value==b.Value;
        public static bool operator !=(IntVec3 a,IntVec3 b)=>a.Value!=b.Value;
        public override bool Equals(object o)=>o is IntVec3 b && this==b;
        public override int GetHashCode()=>Value; public override string ToString()=>Value.ToString();
    }
    public struct LocalTargetInfo
    {
        public Thing Thing; IntVec3 cell; public bool HasThing=>Thing!=null;
        public IntVec3 Cell=>Thing?.PositionHeld??cell;
        public bool IsValid=>HasThing||Cell.IsValid;
        public static implicit operator LocalTargetInfo(int n)=>new LocalTargetInfo{cell=n};
        public static implicit operator LocalTargetInfo(IntVec3 n)=>new LocalTargetInfo{cell=n};
    }
    public class Thing { public bool Destroyed; public Map MapHeld; public IntVec3 PositionHeld; }
    public class Pawn:Thing
    {
        public Map Map; public IntVec3 Position; public int thingIDNumber; public string LabelShortCap=>"Pawn";
        public bool IsColonist=true,IsSlave,IsChild,NativeOverride,Hazard;
        public Pawn_JobTracker jobs; public Pather pather=new Pather(); public CarryTracker carryTracker=new CarryTracker();
        public bool CanReach(IntVec3 c,PathEndMode e,Danger d)=>!Map.UnreachableCells.Contains(c);
    }
    public class Pather { public bool Moving; }
    public class CarryTracker { public Thing CarriedThing; }
    public class Area { public Map Map; public HashSet<IntVec3> Cells=new HashSet<IntVec3>(); public bool this[IntVec3 c]=>Cells.Contains(c); public IEnumerable<IntVec3> ActiveCells=>Cells; }
    public static class GenRadial { public static IEnumerable<IntVec3> RadialCellsAround(IntVec3 c,float radius,bool center)=>radius<2?new IntVec3[]{9,12,13}:new IntVec3[]{9,12,13,14,15}; }
    public enum Danger { Deadly }
    public static class Find { public static TickManager TickManager=new TickManager(); }
    public class TickManager { public int TicksGame=100; }
}
namespace Verse.AI
{
    public class JobDef { public string defName; }
    public class Job { public JobDef def; public bool playerForced; public LocalTargetInfo targetA=-1,targetB=-1,targetC=-1; public List<LocalTargetInfo> targetQueueA,targetQueueB; public int expiryInterval; public LocomotionUrgency locomotionUrgency; }
    public class ThinkNode { } public enum JobTag { Misc } public enum PathEndMode { OnCell } public enum LocomotionUrgency { Jog }
    public enum JobCondition { InterruptForced }
    public class Pawn_JobTracker
    {
        Pawn pawn; public Job Current; public int Clears,Rewrites;
        public Pawn_JobTracker(Pawn p){pawn=p;}
        public void ClearQueuedJobs(bool release){Clears++;}
        public void StartJob(Job next,JobCondition condition,ThinkNode giver,bool resume,bool cancel)
        {
            Current=null; Job admitted=next; JobTag? tag=null;
            if(PawnJobTracker_StartJob_Patch.Redirect(this,pawn,ref admitted,ref giver,ref tag))Rewrites++;
            Current=admitted;
        }
        public void ScanAndStart(IEnumerable<Job> jobs)
        {
            var selected=jobs.First(j=>!UnavailableWorkRegistry.ShouldReject(pawn,j));
            // A still-owned boundary retry preempts an unrelated native choice.
            Current=ProtectedBoundaryRetryRegistry.Pending??selected;
        }
    }
}
namespace RimWorld { }
namespace AutomaticOutfitManager.Rules
{
    public class ApparelRule { public string Id,Name; public bool Enabled=true,IsNonWork=true,DefaultToSavedPersonalOutfit,WorkAreaPaused,Satisfied; public Area Area,ChangingArea; }
}
namespace AutomaticOutfitManager.State
{
    public enum ApparelTransition { Active,Preparing,ReturningToChangingArea,Restoring }
    public class PawnApparelState
    {
        public Pawn Pawn; public string ActiveRuleId; public List<string> CurrentRuleIds;
        public ApparelTransition Transition; public Job PendingWorkJob; public bool RecallRequested,RecallInterruptPending;
        public int LastApparelPreparationAttemptTick,LastApparelPreparationThingId,ActiveIdleTicks;
        public void ClearWeaponPreparationRetry(){}
    }
}
namespace AutomaticOutfitManager.Core
{
    public partial class AutomaticOutfitManagerGameComponent
    {
        public static AutomaticOutfitManagerGameComponent Current; public static int Released;
        public List<ApparelRule> Rules; public List<PawnApparelState> PawnStates=new List<PawnApparelState>();
        bool managedApparelDefIndexDirty,managedWeaponDefIndexDirty,workSnapshotCleanupPending;
        public PawnApparelState TestState;
        public PawnApparelState StateFor(Pawn p)=>TestState;
        void CleanAllWorkGearSnapshots(){} public void RequestRecall(PawnApparelState s){s.RecallRequested=true;}
        public static void ClearPendingWork(PawnApparelState s){s.PendingWorkJob=null;}
        public ApparelRule RuleById(string id)=>Rules.FirstOrDefault(r=>r.Id==id);
        public static void ReleaseNativeReservations(Pawn p,Job j){Released++;}
    }
    public static class AomLog { public static bool DetailedEnabled=false; public static void Detailed(string s){} public static bool ShouldLogDetailed(Pawn p,string key,int ticks)=>true; }
}
namespace AutomaticOutfitManager.Detection
{
    public static class PawnAccessClassifier { public static bool IsHostedGuest(Pawn p)=>false;public static bool IsColonyPrisoner(Pawn p)=>false; }
    public static class WeaponPreparationRetryRegistry { public static void ResetForLoadedGame(){} }
    public static class ManagedWorkClaimRegistry { public static void Release(Pawn p,Job j){} public static void ReleaseAll(Pawn p){} }
    public static class ProtectedBoundaryRetryRegistry { public static Job Pending; public static void Clear(Pawn p,Job j){if(Pending==j)Pending=null;} }
    public static class RuleEvaluator
    {
        public static bool HasMissingRequiredGear(Pawn p,ApparelRule r)=>!r.Satisfied;
        public static bool JobTargetsArea(Job j,Area a)=>new[]{j.targetA,j.targetB,j.targetC}.Any(t=>t.IsValid&&a[t.Cell]);
    }
}
namespace AutomaticOutfitManager.Patches
{
    public static partial class PawnJobTracker_StartJob_Patch
    {
        public static int Preparations;
        public static bool Redirect(Pawn_JobTracker t,Pawn p,ref Job j,ref ThinkNode n,ref JobTag? tag)=>TryRedirectIdleMissingGearWaitWithEgress(t,p,AutomaticOutfitManagerGameComponent.Current,AutomaticOutfitManagerGameComponent.Current.StateFor(p),ref j,ref n,ref tag);
        static bool TryPrepareForMatchingRules(Pawn_JobTracker t,Pawn p,AutomaticOutfitManagerGameComponent c,List<ApparelRule> rules,ref Job j,ref ThinkNode giver,ref JobTag? tag,bool preserve){Preparations++;j=WorkJob("Wear");return true;}
        static Job WorkJob(string name)=>new Job{def=new JobDef{defName=name}};
        static Job MakeSafeWaitJob(Pawn p,int ticks)=>WorkJob("Wait");
        static void ReplaceWithWait(Pawn p,int ticks,ref Job j,ref ThinkNode giver,ref JobTag? tag){j=MakeSafeWaitJob(p,ticks);}
        static bool IsTargetlessRecoveryWaitJob(Job j)=>j?.def?.defName=="Wait";
        static bool IsChangingAreaTravelJob(Job j)=>j?.def?.defName=="AutomaticOutfitManager_LockerReturn";
        public static void Reject(Pawn p,ApparelRule r,Job j)=>BlockUnavailableGear(p,r,j);
        public static bool Exit(Pawn_JobTracker t,Pawn p,ref Job j,ref ThinkNode n,ref JobTag? tag)=>TryReplaceUnavailableGearWaitWithEgress(t,p,ref j,ref n,ref tag);
        static bool IsBufferableJob(Job j)=>j!=null && j.def.defName!="Wait";
        static bool PawnInsideArea(Pawn p,Area a)=>a[p.Position];
        static bool ChangingCellIsAvailable(Pawn p,IntVec3 c)=>!p.Map.UnavailableCells.Contains(c);
        static Job MakeChangingAreaTravelJob(IntVec3 c)=>new Job{def=new JobDef{defName="AutomaticOutfitManager_LockerReturn"},targetA=c};
    }
    public static class NativeRuleControl { public static bool Suspends(Pawn p,Job j)=>p.NativeOverride; }
    public static class HazardousEnvironmentSafety { public static bool MustRetainManagedProtectionAt(Pawn p,PawnApparelState s,IntVec3 c,out string why){why=null;return p.Hazard;} }
    public static partial class ProtectedPathAvoidance
    {
        public static int RejectedReentry;
        public static List<ApparelRule> LastCostRules;
        static bool BeginAutomaticCustomizerSuppression()=>false;
        static void EndAutomaticCustomizerSuppression(bool previous){}
        static List<ApparelRule> GridFor(Map map,List<ApparelRule> rules)=>rules;
        // Native path selection happens before AOM validates traversal. Two
        // fixture routes let the entry penalty select a cheaper but invalid
        // leave/re-enter route over the valid initial-occupancy route.
        static bool SegmentFound(Pawn p,IntVec3 start,LocalTargetInfo dest,List<ApparelRule> costRules,List<ApparelRule> rules,Predicate<IntVec3> unsafeCell=null,PathEndMode? exactEndMode=null,bool allowInitialEgress=false)
        {
            LastCostRules=costRules??new List<ApparelRule>();
            IntVec3[] reverse=dest.Cell==9?new IntVec3[]{9,11,12,10}:new[]{dest.Cell,start};
            if(p.Map.CompareNativeRoutes)
            {
                var routes=new[]{new IntVec3[]{16,12,11,10},new IntVec3[]{16,12,15,14,13,10}};
                reverse=routes.OrderBy(route=>route.Length*13+route.Take(route.Length-1).Count(cell=>LastCostRules.Any(r=>r.Area[cell]))*10000).First();
            }
            bool ok=PathAvoidsRules(reverse,start,p.Map,rules,unsafeCell,allowInitialEgress);
            if(!ok)RejectedReentry++; return ok;
        }
    }
}
