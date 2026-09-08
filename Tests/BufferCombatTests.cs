using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

internal static class BufferCombatTests
{
    static int checks;
    static Pawn pawn;
    static PawnApparelState state;
    static AutomaticOutfitManagerGameComponent component;
    static Job NewJob(string name, int id=10) => new Job {def=name=="Wear" ? JobDefOf.Wear : name=="RemoveApparel" ? JobDefOf.RemoveApparel : name=="Wait" ? JobDefOf.Wait : new JobDef {defName=name},loadID=id};
    static void Check(bool result,string message) { if(!result) throw new Exception(message); checks++; }
    static Job Setup(string name="DoBill")
    {
        pawn=new Pawn(); pawn.jobs=new Pawn_JobTracker(pawn);
        component=AutomaticOutfitManagerGameComponent.Current=new AutomaticOutfitManagerGameComponent();
        component.Rules.Add(new ApparelRule {Id="work",Area=new Area {Map=pawn.Map}});
        component.Rules.Add(new ApparelRule {Id="nested",Area=new Area {Map=pawn.Map}});
        component.Rules.Add(new ApparelRule {Id="dining",Area=new Area {Map=pawn.Map},IsNonWork=true});
        state=component.State=new PawnApparelState {ActiveRuleId="work",PendingBufferedRuleId="work",PendingBufferedJobLoadId=10};
        state.NestedRuleBuffers.Add(new NestedRuleBufferState {RuleId="nested",PendingJobLoadId=10});
        NonWorkBufferTracker.Buffer=null;
        var job=NewJob(name); pawn.jobs.curJob=job; return job;
    }
    static void End(Job job, JobCondition condition=JobCondition.Succeeded)
    {
        pawn.jobs.curJob=job;
        pawn.jobs.EndCurrentJob(condition,true,true);
    }
    static void Admission()
    {
        var job=Setup("SocialFight");
        Check(!PawnJobTracker_StartJob_Patch.CanCountBufferedTask(pawn,job),"social fight cannot become a buffer candidate after mental state clears");
        foreach(string activity in new[]{"DoBill","HaulToCell","Ingest","Reading","SocialRelax","Hunt","AttackMelee"})
        {
            job=NewJob(activity);
            Check(PawnJobTracker_StartJob_Patch.CanCountBufferedTask(pawn,job),"ordinary job classification preserved: "+activity);
            pawn.InMentalState=true;
            Check(!PawnJobTracker_StartJob_Patch.CanCountBufferedTask(pawn,job),"mental control prevents credit: "+activity);
            pawn.InMentalState=false;
        }
        foreach(string connective in new[]{"Wait","Wait_MaintainPosture","Goto","Wear","RemoveApparel","TakeInventory","LayDown","FleeAndCower","ExitMapFlying"})
            Check(!PawnJobTracker_StartJob_Patch.CanCountBufferedTask(pawn,NewJob(connective)),"non-task does not count: "+connective);
    }
    static void CombatCompletion(bool nested)
    {
        // A save or earlier admission can leave a candidate even after its
        // classification changed. Native success must still clear it uncredited.
        var job=Setup("SocialFight");
        if(nested) state.PendingBufferedJobLoadId=-1;
        else state.NestedRuleBuffers.Clear();
        object snapshot=state.Snapshot;
        pawn.jobs.OnEnded=()=>Check((nested ? state.NestedRuleBuffers[0].Completed : state.BufferedTasksCompleted)==0,
            nested ? "nested social fight completion cannot spend a buffer" : "outer social fight completion cannot spend a buffer");
        End(job);
        Check(state.PendingBufferedJobLoadId==-1 && state.NestedRuleBuffers.All(n=>n.PendingJobLoadId==-1),"combat candidate cleared");
        Check(state.Snapshot==snapshot && state.Transition==ApparelTransition.Active && !state.RecallRequested,"combat ending preserves outfit state");
        pawn.jobs.OnEnded=null;
        End(NewJob("SocialFight",11));
        Check(state.BufferedTasksCompleted==0 && state.NestedRuleBuffers.All(n=>n.Completed==0),"repeated fight completion earns no credit");
    }
    static void NonWorkCompletion()
    {
        var job=Setup("SocialFight"); component.State=null;
        var buffer=NonWorkBufferTracker.Buffer=new NonWorkOutfitBuffer {Pawn=pawn,Map=pawn.Map,RuleId="dining",PendingJobId=job.loadID};
        End(job);
        Check(buffer.Completed==0 && buffer.PendingJobId==-1,"non-work stale social fight candidate cannot count");
        job=NewJob("Reading",20); buffer.Start(job.loadID,2,false,true,true); End(job);
        Check(buffer.Completed==1,"non-work ordinary task still counts after rejected combat candidate");
        job=NewJob("DoBill",21); buffer.Start(job.loadID,2,false,true,true); pawn.InMentalState=true; End(job);
        Check(NonWorkBufferTracker.Buffer==null && buffer.Completed==1,"non-work native override clears retention without credit");
    }
    static void NativeAdmission()
    {
        foreach(bool mental in new[]{false,true})
        {
            var job=Setup(mental ? "AttackMelee" : "SocialFight"); pawn.InMentalState=mental;
            object snapshot=state.Snapshot; pawn.jobs.Queue.Add(NewJob("Wear",30));
            pawn.jobs.StartJob(job);
            Check(pawn.jobs.ManagedChecks==0,"native mental activity bypasses civilian outfit admission");
            Check(pawn.CurJob==job && pawn.jobs.NativeStarts==1 && pawn.jobs.Queue.Count==1,"native job and queue retain ownership");
            Check(state.Snapshot==snapshot && state.PendingBufferedJobLoadId==-1 && state.NestedRuleBuffers[0].PendingJobLoadId==-1,"mental admission clears candidates without losing snapshot");
        }
    }
    static void NormalAndInvalidCompletion()
    {
        foreach(string activity in new[]{"DoBill","HaulToCell","Ingest","Reading"})
        {
            var job=Setup(activity);
            // Prefix must capture/count before native pooling clears the Job,
            // and before the next job's completion can re-enter the tracker.
            pawn.jobs.OnEnded=()=>Check(state.BufferedTasksCompleted==1 && state.NestedRuleBuffers[0].Completed==1,"successful callback credited before native pooling");
            End(job);
            Check(job.def==null && state.LastBufferedJobLoadId==10,"native pooling cannot change completed job identity");
            pawn.jobs.OnEnded=null; End(NewJob(activity));
            Check(state.BufferedTasksCompleted==1 && state.NestedRuleBuffers[0].Completed==1,"same completed job cannot double count");
        }
        foreach(string reason in new[]{"mental","drafted","downed","dead","despawned","forced","escape","paused","recall","restoring","failed","interrupted","wrongid"})
        {
            var job=Setup();
            pawn.InMentalState=reason=="mental"; pawn.Drafted=reason=="drafted"; pawn.Downed=reason=="downed";
            pawn.Dead=reason=="dead"; pawn.Spawned=reason!="despawned"; pawn.CustodyEscape=reason=="escape";
            job.playerForced=reason=="forced"; state.RecallRequested=reason=="recall";
            if(reason=="restoring") state.Transition=ApparelTransition.Restoring;
            if(reason=="paused") foreach(var r in component.Rules) r.WorkAreaPaused=true;
            if(reason=="wrongid") job.loadID=99;
            End(job,reason=="failed" ? JobCondition.Incompletable : reason=="interrupted" ? JobCondition.InterruptForced : JobCondition.Succeeded);
            Check(state.BufferedTasksCompleted==0 && state.NestedRuleBuffers[0].Completed==0,"invalid completion not credited: "+reason);
            Check(state.PendingBufferedJobLoadId==(reason=="wrongid" ? 10 : -1),"only ending candidate cleared: "+reason);
        }
    }
    static int Main(string[] args)
    {
        try
        {
            new Harmony("aom.tests.combat-buffer").PatchAll();
            switch(args.FirstOrDefault())
            {
                case "admission": Admission(); break;
                case "outer": CombatCompletion(false); break;
                case "nested": CombatCompletion(true); break;
                case "nonwork": NonWorkCompletion(); break;
                case "native": NativeAdmission(); break;
                default: Admission(); CombatCompletion(false); CombatCompletion(true); NonWorkCompletion(); NativeAdmission(); NormalAndInvalidCompletion(); break;
            }
            Console.WriteLine($"PASS {checks} combat buffer checks (production predicates/completion and native callback ordering)."); return 0;
        }
        catch(Exception e) { Console.Error.WriteLine("FAIL "+e.Message); return 1; }
    }
}

namespace Verse
{
    public class Pawn { public bool Spawned=true,Dead,Downed,Drafted,InMentalState,CustodyEscape; public Map Map=new Map(); public Pawn_JobTracker jobs; public Job CurJob=>jobs.curJob; public string LabelShortCap=>"Gonzo"; public ApparelTracker apparel=new ApparelTracker(); public EquipmentTracker equipment=new EquipmentTracker(); }
    public class ApparelTracker { public List<Apparel> WornApparel=new List<Apparel>(); }
    public class EquipmentTracker { public ThingWithComps Primary; }
    public class Map { }
    public class Area { public Map Map; }
    public class ThingWithComps { public bool Destroyed; }
    public struct IntVec3 { public int Value; public static IntVec3 Invalid=>new IntVec3 {Value=-1}; public static bool operator ==(IntVec3 a,IntVec3 b)=>a.Value==b.Value; public static bool operator !=(IntVec3 a,IntVec3 b)=>a.Value!=b.Value; public override bool Equals(object o)=>o is IntVec3 v && this==v; public override int GetHashCode()=>Value; }
    public struct LocalTargetInfo { public IntVec3 Cell; }
    public interface IExposable { void ExposeData(); }
    public enum LookMode { Reference }
    public static class Scribe_References { public static void Look<T>(ref T v,string key) { } }
    public static class Scribe_Values { public static void Look<T>(ref T v,string key,T fallback=default(T)) { } }
    public static class Scribe_Collections { public static void Look<T>(ref List<T> v,string key,LookMode mode) { } }
    public static class Scribe_Deep { public static void Look<T>(ref T v,string key) { } }
}
namespace Verse.AI
{
    public class ThinkNode { }
    public class ThinkTreeDef { }
    public enum JobTag { Misc }
    public class JobDef { public string defName; }
    public class Job { public JobDef def; public int loadID; public bool playerForced,exitMapOnArrival; public LocalTargetInfo targetA; public string GetReport(Pawn p)=>def?.defName; }
    public enum JobCondition { Succeeded,Incompletable,InterruptForced }
    public class Pawn_JobTracker
    {
        public Pawn Pawn; public Job curJob; public int ManagedChecks,NativeStarts; public List<Job> Queue=new List<Job>(); public Action OnEnded;
        public Pawn_JobTracker(Pawn p) {Pawn=p;}
        [MethodImpl(MethodImplOptions.NoInlining)] public void StartJob(Job newJob) { NativeStarts++;curJob=newJob; }
        [MethodImpl(MethodImplOptions.NoInlining)] public void EndCurrentJob(JobCondition condition,bool startNewJob,bool canReturnToPool)
        { var ended=curJob;curJob=null;if(canReturnToPool) ended.def=null;OnEnded?.Invoke(); }
    }
}
namespace RimWorld
{
    public class Apparel : ThingWithComps { }
    public static class JobDefOf { public static JobDef Wait=new JobDef{defName="Wait"},Wear=new JobDef{defName="Wear"},RemoveApparel=new JobDef{defName="RemoveApparel"}; }
}
namespace AutomaticOutfitManager.Rules
{
    public class ApparelRule { public string Id,Name="Rule"; public Area Area; public bool Enabled=true,IsNonWork,WorkAreaPaused; public int ReturnTaskBuffer=2; }
}
namespace AutomaticOutfitManager.State
{
    public enum ApparelTransition { Active,Preparing,ReturningToChangingArea,Restoring }
    public class NestedRuleBufferState { public string RuleId,LastJobLabel; public int PendingJobLoadId=-1,LastJobLoadId=-1,Completed; public bool Finished; }
    public partial class PawnApparelState { public string ActiveRuleId,PendingBufferedRuleId,LastNestedBufferStatus; public int PendingBufferedJobLoadId=-1,LastBufferedJobLoadId=-1,BufferedTasksCompleted; public List<NestedRuleBufferState> NestedRuleBuffers=new List<NestedRuleBufferState>(); public ApparelTransition Transition=ApparelTransition.Active; public bool RecallRequested; public object Snapshot=new object(); }
}
namespace AutomaticOutfitManager.Core
{
    public class AutomaticOutfitManagerGameComponent { public static AutomaticOutfitManagerGameComponent Current; public PawnApparelState State; public List<ApparelRule> Rules=new List<ApparelRule>(); public PawnApparelState StateFor(Pawn p)=>State; public ApparelRule RuleById(string id)=>Rules.FirstOrDefault(r=>r.Id==id); public bool UpdateNativeRuleSuspension(Pawn p,Job j)=>true; }
    public static class AomLog { public static bool DetailedEnabled=true; public static void Detailed(string s) { } }
}
namespace AutomaticOutfitManager.Detection
{
    public static class PawnAccessClassifier { public static bool IsNativeCustodyEscapeActive(Pawn p)=>p.CustodyEscape; }
}
namespace AutomaticOutfitManager.Patches
{
    public static class PausedAreaWorkFilter { public static bool IsEssentialPersonalJob(Job j)=>j?.def?.defName=="LayDown"; }
    public static partial class PawnJobTracker_StartJob_Patch { private static bool IsChangingAreaTravelJob(Job j)=>false; private static void ReplaceWithBriefWait(Pawn p,ref Job j,ref ThinkNode n,ref JobTag? t){j=new Job {def=JobDefOf.Wait};} }
    public static class PawnPathFollower_ProtectedArea_Patch { public static bool IsManagedTransitionJob(Pawn p,Job j,PawnApparelState s)=>false; }
    public static partial class NonWorkBufferTracker
    {
        public static NonWorkOutfitBuffer Buffer;
        private static AutomaticOutfitManagerGameComponent Component=>AutomaticOutfitManagerGameComponent.Current;
        internal static NonWorkOutfitBuffer For(Pawn p)=>Buffer;
        internal static void Clear(Pawn p)=>Buffer=null;
    }
}
