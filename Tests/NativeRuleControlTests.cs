using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Detection;

static class NativeRuleControlTests
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static Job Job(string name, bool owned=false) => new Job { def=new JobDef { defName=name }, Owned=owned };
    static Pawn Setup(bool state=true)
    {
        var pawn=new Pawn(); pawn.jobs=new Pawn_JobTracker(pawn);
        AutomaticOutfitManagerGameComponent.Current=new AutomaticOutfitManagerGameComponent();
        if(state) AutomaticOutfitManagerGameComponent.Current.State=new PawnApparelState();
        RuleEvaluator.Rules=new List<ApparelRule> { new ApparelRule() };
        ProtectedBoundaryRetryRegistry.Root=Job("GotoWander");
        return pawn;
    }
    static bool Move(Pawn p)
    {
        // Native-shaped path body: the actual production Prefix must permit
        // entry before the next-cell movement can happen. Ending a job clears
        // the tracker; the next native thinker iteration proposes a fresh job.
        var follower=new Pawn_PathFollower { Pawn=p, Next=new IntVec3 { Value=1 } };
        if(!PawnPathFollower_ProtectedArea_Patch.Prefix(follower)) return false;
        p.Position=follower.Next; return true;
    }
    static void MentalMealLoop()
    {
        foreach(bool session in new[]{false,true})
        {
            var p=Setup(session); p.InMentalState=true;
            var component=AutomaticOutfitManagerGameComponent.Current;
            for(int n=0;n<20;n++)
            {
                var ingest=Job("Ingest"); p.jobs.StartJob(ingest);
                component.UpdateNativeRuleSuspension(p, p.CurJob);
                Check(Move(p),"mental meal reaches next cell without a boundary interruption");
                Check(p.CurJob==ingest && p.jobs.Ends==0,"native meal retains tracker ownership");
                Check(ProtectedBoundaryRetryRegistry.Root==null,"obsolete wandering retry is cleared");
            }
            Check(p.jobs.ManagedChecks==0,"mental meal never enters civilian preparation");
        }
    }
    static void ControlAndRecovery()
    {
        foreach(string control in new[]{"mental","social","flee","downed","drafted","custody"})
        {
            var p=Setup(); p.InMentalState=control=="mental"; p.Downed=control=="downed";
            p.Drafted=control=="drafted"; p.CustodyEscape=control=="custody";
            var job=Job(control=="social"?"SocialFight":control=="flee"?"FleeAndCower":"Ingest");
            p.jobs.curJob=job;
            Check(NativeRuleControl.Suspends(p,job),"rule participation suspended: "+control);
            Check(Move(p) && p.jobs.Ends==0,"native boundary movement preserved: "+control);
        }
        foreach(string ordinary in new[]{"Ingest","LayDown","HaulToCell","DoBill"})
        {
            var p=Setup(); var job=Job(ordinary); p.jobs.curJob=job;
            Check(!NativeRuleControl.Suspends(p,job),"ordinary needs/activity still participates: "+ordinary);
            Check(!Move(p) && p.jobs.Ends==1,"ordinary missing PPE still blocks: "+ordinary);
            p.Compliant=true; p.jobs.curJob=Job(ordinary);
            Check(Move(p),"ordinary complete PPE permits entry: "+ordinary);
            RuleEvaluator.Rules[0].Allows=false;
            p.jobs.curJob=Job(ordinary);
            Check(!Move(p),"ordinary access restrictions still enforced: "+ordinary);
        }
        foreach(ApparelTransition phase in Enum.GetValues(typeof(ApparelTransition)))
        {
            var p=Setup(); var c=AutomaticOutfitManagerGameComponent.Current; var s=c.State;
            s.Transition=phase; s.PendingWorkJob=Job("DoBill"); s.BufferedTasksCompleted=1;
            object snapshot=s.Snapshot, gear=s.ManagedGear;
            var native=Job("Wait"); var owned=Job("Wear",true);
            p.jobs.jobQueue.Add(native); p.jobs.jobQueue.Add(owned);
            c.NonWorkOutfitBuffers.Add(new PawnRecord {Pawn=p}); c.NonWorkMealTrips.Add(new PawnRecord {Pawn=p});
            p.InMentalState=true; var mental=Job("Ingest"); p.jobs.StartJob(mental);
            Check(s.NativeControlSuspended && s.PendingWorkJob==null,"civilian continuation retired in "+phase);
            Check(s.Snapshot==snapshot && s.ManagedGear==gear,"ownership preserved in "+phase);
            Check(p.jobs.jobQueue.SequenceEqual(new[]{native}),"only assigned outfit queue removed in "+phase);
            Check(s.BufferedTasksCompleted==1 && s.PendingBufferedJobLoadId==-1,"no buffer credit in "+phase);
            Check(c.NonWorkOutfitBuffers.Count==0 && c.NonWorkMealTrips.Count==0,"stale Non-Work detours removed");
            p.InMentalState=false; var ordinary=Job("HaulToCell"); p.jobs.StartJob(ordinary);
            Check(!s.NativeControlSuspended && p.CurJob==ordinary,"fresh native selection after recovery");
            Check(s.Transition==ApparelTransition.Active,"recovery starts at active/locker boundary");
            Check(s.RecallRequested==(phase!=ApparelTransition.Active),"interrupted transition returns safely; active outfit can continue");
            Check(!Move(p),"PPE rules resume after mental recovery");
        }
        var queuedPawn=Setup(); queuedPawn.InMentalState=true;
        queuedPawn.jobs.StartJob(Job("Wear",true),fromQueue:true);
        Check(queuedPawn.CurJob.def.defName=="Wait","dequeued AOM wear cannot run under mental control");
    }
    public static int Main()
    {
        try { MentalMealLoop(); ControlAndRecovery(); BoundaryBetweenJobTicks(); Console.WriteLine("PASS "+checks+" native rule-control checks (production next-cell guard, suspension and admission blocks)."); return 0; }
        catch(Exception e) { Console.Error.WriteLine("FAIL "+e.Message); return 1; }
    }
    static void BoundaryBetweenJobTicks()
    {
        foreach(string name in new[]{"DoBill","HaulToCell","Ingest"})
        {
            var p=Setup(false); RuleEvaluator.Rules[0].IsNonWork=true;
            RuleEvaluator.Rules[0].Area.Cells=new HashSet<int>{1};
            ProtectedBoundaryRetryRegistry.Root=null;
            var original=Job(name); p.jobs.curJob=original;
            var follower=new Pawn_PathFollower {Pawn=p}; follower.StartPath(1);
            bool stoppedBeforeCleanup=false;
            p.jobs.BeforeCleanup=()=>stoppedBeforeCleanup=!follower.Moving && !follower.HasPath;
            follower.PatherTick();
            Check(p.Position.Value==0 && p.CurJob==null,"boundary rejects original native task while still outside: "+name);
            // RimWorld movement ticks can precede the next job-tracker interval.
            // EndCurrentJob(false) clears curJob but does not cancel that path.
            for(int tick=0;tick<5;tick++)follower.PatherTick();
            Check(p.Position.Value==0,"blocked path stays outside until next job interval: "+name);
            Check(stoppedBeforeCleanup,"path stopped before cleanup callbacks: "+name);
            Check(!follower.HasPath && !follower.Moving && follower.Next.Value==0,"old path and next step retired: "+name);
            Check(ProtectedBoundaryRetryRegistry.Root==original,"exact native continuation retained for preparation: "+name);
            Check(p.jobs.Ends==1,"jobless movement ticks do not repeat interruption: "+name);

            // Resume native scheduling: gear retrieval stays outside. Completion
            // changes actual compliance before the protected task is retried.
            p.jobs.StartJob(Job("Wear",true)); follower.StartPath(3); follower.PatherTick();
            Check(p.Position.Value==3 && !p.Compliant,"outside gear retrieval can proceed before outfit completion: "+name);
            p.Compliant=true; p.jobs.StartJob(original); follower.StartPath(1); follower.PatherTick();
            Check(p.Position.Value==1 && p.CurJob==original,"prepared original task enters after actual gear completion: "+name);

            p.Compliant=false; p.jobs.curJob=Job("Goto"); follower.StartPath(0); follower.PatherTick();
            Check(p.Position.Value==0 && p.jobs.Ends==1,"mismatched occupant keeps outward movement: "+name);
        }
    }
}
namespace Verse
{
    public class Pawn { public bool Dead,Downed,Drafted,InMentalState,CustodyEscape,Compliant; public Map Map=new Map(); public Pawn_JobTracker jobs; public Job CurJob=>jobs?.curJob; public int thingIDNumber=1; public string LabelShortCap=>"Ocag"; public IntVec3 Position; }
    public class Map { }
    public struct IntVec3 { public int Value; public bool IsValid=>Value>=0; public bool InBounds(Map m)=>m!=null; public static IntVec3 Invalid=>new IntVec3 {Value=-1}; }
    public class Area { public HashSet<int> Cells; public bool this[IntVec3 cell]=>Cells==null||Cells.Contains(cell.Value); }
    public static class Find { public static TickManager TickManager=new TickManager(); }
    public class TickManager { public int TicksGame=1000; }
    public class ThinkTreeDef { }
}
namespace Verse.AI
{
    public class JobDef { public string defName; }
    public class Job { public JobDef def; public bool Owned; }
    public class ThinkNode { }
    public enum JobTag { Misc }
    public enum JobCondition { InterruptForced }
    public class JobQueue : List<Job> { public void RemoveAll(Pawn p,Predicate<Job> match)=>base.RemoveAll(match); }
    public class Pawn_JobTracker
    {
        public Pawn Pawn; public Job curJob; public JobQueue jobQueue=new JobQueue(); public int Ends,ManagedChecks;
        public Action BeforeCleanup;
        public Pawn_JobTracker(Pawn p) {Pawn=p;}
        public void StartJob(Job job, JobCondition condition=JobCondition.InterruptForced, ThinkNode node=null, bool a=false,bool b=false,bool fromQueue=false)
        { PawnJobTracker_StartJob_Patch.Admit(this,ref job,fromQueue); curJob=job; }
        public void EndCurrentJob(JobCondition condition,bool startNext=false,bool pool=true) {BeforeCleanup?.Invoke();Ends++;curJob=null;}
        public void ClearQueuedJobs(bool ignored) {jobQueue.Clear();}
    }
    public class Pawn_PathFollower
    {
        public Pawn Pawn; public IntVec3 Next; public bool Moving,HasPath; public float CostLeft;
        public void StartPath(int cell){Next=new IntVec3{Value=cell};Moving=true;HasPath=true;CostLeft=1;}
        public void StopDead(){HasPath=false;Moving=false;Next=Pawn.Position;CostLeft=0;}
        public void PatherTick()
        {
            // Relevant native PatherTick body ordering: retained path and cost
            // drive movement independently of the interval-based job tracker.
            if(!HasPath || (!Moving && CostLeft<=0))return;
            if(CostLeft>0)CostLeft--;
            if(CostLeft<=0 && PawnPathFollower_ProtectedArea_Patch.Prefix(this))Pawn.Position=Next;
        }
    }
}
namespace AutomaticOutfitManager.Rules { public class ApparelRule {public string Id="kitchen",Name="Kitchen"; public bool IsNonWork,WorkAreaPaused,Allows=true; public Area Area=new Area();} }
namespace AutomaticOutfitManager.State
{
    public enum ApparelTransition {Preparing,Active,ReturningToChangingArea,Restoring}
    public class PawnApparelState
    {
        public bool NativeControlSuspended,RecallRequested,AutomaticIdleReturnRequested,RecallInterruptPending;
        public int ActiveIdleTicks,LastRestorationAttemptTick,LastChangingAreaReturnAttemptTick,NaturalLockerDwellUntilTick,BufferedTasksCompleted,PendingBufferedJobLoadId=77;
        public ApparelTransition Transition=ApparelTransition.Active; public IntVec3 ChangingAreaReturnCell;
        public Job PendingWorkJob; public object Snapshot=new object(),ManagedGear=new object();
        public void ClearPendingBufferCandidates(){PendingBufferedJobLoadId=-1;}
    }
}
namespace AutomaticOutfitManager.Core
{
    public class PawnRecord {public Pawn Pawn;}
    public partial class AutomaticOutfitManagerGameComponent
    {
        public static AutomaticOutfitManagerGameComponent Current; public PawnApparelState State;
        public List<PawnRecord> NonWorkOutfitBuffers=new List<PawnRecord>(),NonWorkMealTrips=new List<PawnRecord>();
        public Dictionary<Pawn,int> occupiedGearRecoveryTicks=new Dictionary<Pawn,int>(),restorationProgress=new Dictionary<Pawn,int>(),activeWorkProgress=new Dictionary<Pawn,int>(),restorationRecoveryBackoff=new Dictionary<Pawn,int>(),jobTransitionFailureTicks=new Dictionary<Pawn,int>();
        public PawnApparelState StateFor(Pawn p)=>State;
        public static void ClearPendingWork(PawnApparelState s){s.PendingWorkJob=null;}
        static bool IsAssignedApparelTransitionJob(PawnApparelState s,Job j)=>j.Owned;
        static bool IsAssignedWeaponTransitionJob(PawnApparelState s,Job j)=>j.Owned;
    }
    public static class AomLog { public static bool DetailedEnabled=false; public static void Detailed(string s){} }
}
namespace AutomaticOutfitManager.Detection
{
    public static class PawnAccessClassifier { public static bool IsNativeCustodyEscapeActive(Pawn p)=>p?.CustodyEscape==true; public static bool IsApparelEligibleHuman(Pawn p)=>true; }
    public static class ProtectedBoundaryRetryRegistry { public static Job Root; public static void Clear(Pawn p){Root=null;} public static void Record(Pawn p,Job j,ApparelRule r){if(Root==null)Root=j;} }
    public static class PreparedIngestRetryRegistry {public static void Clear(Pawn p){} }
    public static class ManagedWorkClaimRegistry {public static void ReleaseAll(Pawn p){} }
    public static class RuleEvaluator
    {
        public static List<ApparelRule> Rules;
        public static IReadOnlyList<ApparelRule> EnabledRulesForMap(Map m)=>Rules;
        public static bool HasMissingRequiredGear(Pawn p,ApparelRule r)=>!p.Compliant;
        public static bool SavedNonWorkOutfitConflicts(Pawn p,IEnumerable<ApparelRule> r)=>false;
        public static bool SelectedNonWorkOutfitConflicts(Pawn p,IEnumerable<ApparelRule> r)=>false;
    }
    public static class UnavailableWorkRegistry { public static bool HasActiveRuleBlock(Pawn p,ApparelRule r)=>false; public static void Block(Pawn p,ApparelRule r,Job j){} }
    public static class TransitionActivityDiagnostics {public static void PausedActivityDenied(Pawn p,Job j,ApparelRule r){} }
}
namespace AutomaticOutfitManager.Patches
{
    public static class BufferedTransitGuard {public static bool BlockUnnecessaryEntry(Pawn p,Job j,IntVec3 c)=>false;}
    public static class NonWorkMealHandoff {public static bool BlockEntry(Pawn p,IntVec3 c)=>false;}
    public static class MaterialHandoff {public static bool TryStageAtSourceChangingArea(Pawn p,Job j,PawnApparelState s,IReadOnlyList<ApparelRule> r,IntVec3 c)=>false;}
    public static class PausedAreaWorkFilter
    {
        public static bool IsOwnedAccessEgress(Pawn p,Job j,ApparelRule r)=>false;
        public static bool ActivityAllowedAtRuleBoundary(Pawn p,Job j,ApparelRule r)=>r.Allows;
        public static bool IsEssentialPersonalJob(Job j)=>j.def.defName=="LayDown";
        public static bool TryMakeAccessExitJob(Pawn p,Job j,out Job next){next=null;return false;}
    }
    public static partial class PawnPathFollower_ProtectedArea_Patch
    {
        static Pawn PawnField(Pawn_PathFollower f)=>f.Pawn;
        static IntVec3 NextCellField(Pawn_PathFollower f)=>f.Next;
        static Dictionary<int,int> LastBlockedLogTick=new Dictionary<int,int>();
        public static bool IsManagedTransitionJob(Pawn p,Job j,PawnApparelState s)=>s!=null && j?.Owned==true;
        static bool ManagedTransitionMayEnterRule(Pawn p,Job j,PawnApparelState s,ApparelRule r)=>j.Owned;
    }
    public static partial class PawnJobTracker_StartJob_Patch
    {
        public static bool IsMapDepartureJob(Job j)=>false;
        public static bool IsAssignedChangingAreaReturnJob(PawnApparelState s,Job j)=>j.Owned;
        public static bool IsNativePrisonerUnavailableGearFallbackJobFamily(Pawn p,Job j)=>false;
        public static bool IsManagedIncompatibleIngestFallback(Pawn p,PawnApparelState s,Job j,ApparelRule r,IntVec3 c)=>false;
        public static bool IsUnavailableGearEgressJob(Pawn p,Job j,ApparelRule r)=>false;
        public static Job MakeSafeWaitJob(Pawn p,int ticks)=>new Job {def=new JobDef {defName="Wait"}};
        static void ReplaceWithBriefWait(Pawn p,ref Job j,ref ThinkNode node,ref JobTag? tag){j=MakeSafeWaitJob(p,30);}
    }
}
