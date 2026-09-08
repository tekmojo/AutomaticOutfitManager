using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;
using Verse.AI;

class TransitionActivityDiagnosticsTests
{
    static int passed;
    static Pawn pawn;
    static PawnApparelState state;
    static Apparel saved;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    static void Tick(int tick) { Find.TickManager.TicksGame=tick; TransitionActivityDiagnostics.Sample(pawn,tick); }
    static Job Job(JobDef def, int id, Thing target=null) => new Job { def=def,loadID=id,targetA=new LocalTargetInfo(target) };
    static void Setup()
    {
        TransitionActivityDiagnostics.ResetForLoadedGame(); AomLog.Reset(); Find.TickManager.TicksGame=100;
        pawn=new Pawn { Map=new Map() }; saved=new Apparel { ThingID="savedJacket#123" };
        state=new PawnApparelState { Transition=ApparelTransition.Restoring, ActiveRuleId="Kitchen" };
        state.OriginalApparel.Add(saved); AutomaticOutfitManagerGameComponent.Current.State=state;
        pawn.CurJob=Job(JobDefOf.Wait,10); Tick(100);
    }
    static string Last => AomLog.Messages[AomLog.Messages.Count-1];
    static int Main()
    {
        try
        {
            Setup(); Tick(399); Check(AomLog.Messages.Count==0,"brief native wait produces no diagnostic");
            Tick(400); Check(Last.Contains("300 ticks") && Last.Contains("Wait #10") && Last.Contains("state=Restoring"),"prolonged wait names actual job and observed duration");
            Check(pawn.CurJob.loadID==10 && state.Transition==ApparelTransition.Restoring && pawn.jobs.jobQueue.Count==0,"sampling never changes jobs, queues or transitions");
            Tick(401); Check(AomLog.Messages.Count==1,"repeated samples coalesce");
            Tick(1000); Check(AomLog.Messages.Count==2,"ongoing waits produce bounded later evidence");
            Setup(); pawn.CurJob=Job(JobDefOf.Wear,20,saved); Tick(800);
            Check(AomLog.Messages.Count==0,"timed native Wear is not labeled an idle wait");
            Setup(); pawn.pather.Moving=true; Tick(800); Check(AomLog.Messages.Count==0,"moving pawns are not reported as stationary");
            pawn.pather.Moving=false; Tick(810); Tick(1109); Check(AomLog.Messages.Count==0,"wait duration starts after movement ends");
            Tick(1110); Check(AomLog.Messages.Count==1,"new stationary interval is observable");
            Setup(); pawn.Position=new IntVec3(4); Tick(350); Tick(400);
            Check(AomLog.Messages.Count==0,"position change resets stationary evidence");
            Tick(650); Check(AomLog.Messages.Count==1,"stationary duration uses new position");
            Setup(); pawn.CurJob.loadID=11; Tick(300); Tick(400);
            Check(Last.Contains("Wait #11") && Last.Contains("current job observed for 100 ticks") && Last.Contains("stationary wait observed for 300 ticks"),"successive pooled wait identities do not hide continuous waiting");
            Setup(); pawn.CurJob=null; Tick(150); Tick(400);
            Check(Last.Contains("current=none") && Last.Contains("current job observed for 250 ticks"),"empty native tracker has a bounded observed duration");
            Setup(); pawn.Drafted=true; Tick(400); Check(AomLog.Messages.Count==0,"drafted waiting excluded");
            Setup(); pawn.Downed=true; Tick(400); Check(AomLog.Messages.Count==0,"downed waiting excluded");
            Setup(); pawn.InMentalState=true; Tick(400); Check(AomLog.Messages.Count==0,"mental-state waiting excluded");
            Setup(); pawn.CurJob.playerForced=true; Tick(400); Check(AomLog.Messages.Count==0,"player-forced waiting excluded");
            Setup(); pawn.RaceProps.Humanlike=false; Tick(400); Check(AomLog.Messages.Count==0,"animals and nonhuman robots excluded");
            Setup(); AomLog.DetailedEnabled=false; Tick(400); AomLog.DetailedEnabled=true; Tick(700);
            Check(AomLog.Messages.Count==0,"disabled logging clears earlier duration evidence");
            Setup(); AutomaticOutfitManagerGameComponent.Current.State=null; TransitionActivityDiagnostics.Cleared(pawn,"finished saved outfit"); Tick(400);
            Check(Last.Contains("state=none") && Last.Contains("finished saved outfit"),"recent cleared sessions remain observable");
            Tick(2701); Tick(4000); Check(AomLog.Messages.Count==1,"expired cleared-session window does not track unrelated later activity");
            Setup(); Tick(2701); Tick(4000); Check(AomLog.Messages.Count==0,"unchanged long-running state does not renew a transition window forever");
            state.Transition=ApparelTransition.Active; Tick(4010); Tick(4310);
            Check(Last.Contains("state=Active"),"new transition rearms an expired watch");
            Setup(); AutomaticOutfitManagerGameComponent.Current.State=null; TransitionActivityDiagnostics.Cleared(pawn,null);
            pawn.Map=new Map(); Tick(400); Check(AomLog.Messages.Count==0,"map transfer invalidates old session evidence");
            Setup(); AutomaticOutfitManagerGameComponent.Current.State=null; TransitionActivityDiagnostics.ResetForLoadedGame(); Tick(400);
            Check(AomLog.Messages.Count==0,"load reset invalidates cleared-session evidence");
            Setup(); Tick(50); Tick(100); Check(AomLog.Messages.Count==0,"clock rollback cannot inherit a future wait duration");

            Setup(); var wear=Job(JobDefOf.Wear,30,saved); pawn.CurJob=wear;
            var queued=Job(JobDefOf.Wear,31,new Apparel{ThingID="savedMask#456"}); pawn.jobs.jobQueue.Add(new QueuedJob{job=queued});
            var capture=TransitionActivityDiagnostics.CaptureStep(pawn,wear);
            // Simulate synchronous completion, tracker replacement and native Job pooling.
            wear.loadID=300; wear.def=JobDefOf.Wait; wear.targetA=default; queued.loadID=310;
            pawn.CurJob=Job(JobDefOf.Wait,32); pawn.jobs.jobQueue.Clear(); AutomaticOutfitManagerGameComponent.Current.State=null;
            TransitionActivityDiagnostics.Cleared(pawn,null);
            TransitionActivityDiagnostics.AfterStep(pawn,capture,"EndCurrentJob returned (Succeeded)");
            Check(Last.Contains("Wear #30") && Last.Contains("savedJacket#123") && Last.Contains("Wear #31") && Last.Contains("savedMask#456"),"pre-call exact item and queue identities survive Job pooling");
            Check(Last.Contains("current=Wait #32") && Last.Contains("state=none") && Last.Contains("count=0"),"post-call evidence reflects synchronous completion, not stale preparation");
            Tick(200); Tick(500); Check(Last.Contains("last outfit step ended=[Wear #30"),"later wait retains last outfit ending as context");
            Setup(); var remove=Job(JobDefOf.RemoveApparel,33,saved);
            Check(TransitionActivityDiagnostics.CaptureStep(pawn,remove)!=null,"assigned personal apparel removal is observable");
            state.Transition=ApparelTransition.Preparing; var managed=new Apparel{ThingID="workVest#77"}; state.Managed.Add(managed);
            Check(TransitionActivityDiagnostics.CaptureStep(pawn,Job(JobDefOf.Wear,34,managed))!=null,"assigned work apparel admission is observable");
            Check(TransitionActivityDiagnostics.CaptureStep(pawn,Job(JobDefOf.Wear,35,new Apparel()))==null,"unassigned automatic apparel is excluded");
            Check(TransitionActivityDiagnostics.CaptureStep(pawn,Job(JobDefOf.Wait,36,saved))==null,"non-apparel job does not masquerade as an outfit step");
            state.Transition=ApparelTransition.Active;
            Check(TransitionActivityDiagnostics.CaptureStep(pawn,Job(JobDefOf.Wear,37,managed))==null,"ordinary active work does not create transition step logs");
            Setup(); capture=TransitionActivityDiagnostics.CaptureStep(pawn,Job(JobDefOf.Wear,40,saved));
            TransitionActivityDiagnostics.ResetForLoadedGame(); Tick(200); TransitionActivityDiagnostics.AfterStep(pawn,capture,"StartJob returned");
            Check(AomLog.Messages.Count==0,"new watch after load cannot validate an old capture");
            Setup(); capture=TransitionActivityDiagnostics.CaptureStep(pawn,Job(JobDefOf.Wear,41,saved)); pawn.Map=new Map();
            TransitionActivityDiagnostics.AfterStep(pawn,capture,"StartJob returned"); Check(AomLog.Messages.Count==0,"map transfer invalidates step snapshot");
            Setup(); AomLog.DetailedEnabled=false;
            Check(TransitionActivityDiagnostics.CaptureStep(pawn,Job(JobDefOf.Wear,42,saved))==null,"ordinary logging allocates no apparel snapshot");
            Setup(); int count=0; for(int i=0;i<100;i++) if(TransitionActivityDiagnostics.CaptureStep(pawn,Job(JobDefOf.Wear,i,saved))!=null)count++;
            Check(count==60,"outfit retry storms have a strict per-pawn snapshot budget");
            Find.TickManager.TicksGame=701;
            Check(TransitionActivityDiagnostics.CaptureStep(pawn,Job(JobDefOf.Wear,100,saved))!=null,"bounded event budget refreshes");
            Setup(); var rejected=Job(new JobDef{defName="DoBill"},50,new Thing{ThingID="Stove#8"});
            TransitionActivityDiagnostics.Rejected(pawn,rejected,"unavailable outfit cooldown"); rejected.loadID=500;
            TransitionActivityDiagnostics.Rejected(pawn,rejected,"second rejection"); Tick(400);
            Check(Last.Contains("unavailable outfit cooldown: DoBill #50") && Last.Contains("rejected proposals observed=2"),"rejections coalesce as immutable sampled proposals, with total count");
            Check(rejected.loadID==500 && pawn.CurJob.loadID==10,"rejection observation cannot change native selection");
            Setup(); AutomaticOutfitManagerGameComponent.Current.State=null; TransitionActivityDiagnostics.ResetForLoadedGame();
            TransitionActivityDiagnostics.Rejected(pawn,rejected,"unrelated"); Tick(400);
            Check(AomLog.Messages.Count==0,"unrelated pawns are not tracked by rejection hook");
            var paused=new AutomaticOutfitManager.Rules.ApparelRule {Id="dining",Name="Dining",WorkAreaPaused=true};
            TransitionActivityDiagnostics.PausedActivityDenied(pawn,rejected,paused); Tick(400); Tick(700);
            Check(AomLog.Messages.Count==1 && Last.Contains("activity paused: Dining (dining)") && Last.Contains("state=none"),"pause denial opens evidence for untracked pawn");
            Check(Last.Contains("DoBill #500") && Last.Contains("rejected proposals observed=1"),"untracked pause records sampled job and rejection count");
            Check(pawn.CurJob.loadID==10 && pawn.jobs.jobQueue.Count==0,"untracked pause observation leaves native job and queue intact");
            for(int i=0;i<200;i++) TransitionActivityDiagnostics.PausedActivityDenied(pawn,rejected,paused);
            Tick(1300); Check(AomLog.Messages.Count==2 && Last.Contains("rejected proposals observed=201"),"pause retries coalesce with bounded log cadence");
            Tick(3001); int expiredCount=AomLog.Messages.Count;
            for(int t=3100;t<10000;t+=300) {Find.TickManager.TicksGame=t;TransitionActivityDiagnostics.PausedActivityDenied(pawn,rejected,paused);Tick(t);}
            Check(AomLog.Messages.Count==expiredCount,"untracked pause retries cannot rearm expired diagnostic window");
            paused.WorkAreaPaused=false;Tick(10100);paused.WorkAreaPaused=true;
            TransitionActivityDiagnostics.PausedActivityDenied(pawn,rejected,paused);Tick(10100);Tick(10400);
            Check(AomLog.Messages.Count==expiredCount+1,"later pause after resume opens a new bounded window");
            foreach(string reset in new[]{"map","load","quiet","rollback"}) {
                Setup();AutomaticOutfitManagerGameComponent.Current.State=null;TransitionActivityDiagnostics.ResetForLoadedGame();
                TransitionActivityDiagnostics.PausedActivityDenied(pawn,rejected,paused);Tick(100);
                if(reset=="map")pawn.Map=new Map();
                if(reset=="load")TransitionActivityDiagnostics.ResetForLoadedGame();
                if(reset=="quiet"){AomLog.DetailedEnabled=false;Tick(200);AomLog.DetailedEnabled=true;}
                if(reset=="rollback")Tick(50);
                Tick(400);Check(AomLog.Messages.Count==0,"untracked pause watch reset: "+reset);
            }
            Setup();AutomaticOutfitManagerGameComponent.Current.State=null;TransitionActivityDiagnostics.ResetForLoadedGame();
            paused.WorkAreaPaused=false;TransitionActivityDiagnostics.PausedActivityDenied(pawn,rejected,paused);Tick(100);Tick(400);
            Check(AomLog.Messages.Count==0,"ordinary access denial does not open pause diagnostics");
            AomLog.DetailedEnabled=false;paused.WorkAreaPaused=true;TransitionActivityDiagnostics.PausedActivityDenied(pawn,rejected,paused);
            AomLog.DetailedEnabled=true;Tick(500);Tick(800);Check(AomLog.Messages.Count==0,"quiet pause rejection creates no watch");
            var harmony=new Harmony("aom.transition-diagnostics.tests");
            harmony.CreateClassProcessor(typeof(OutfitStepAdmissionDiagnostics_Patch)).Patch();
            harmony.CreateClassProcessor(typeof(OutfitStepEndingDiagnostics_Patch)).Patch();
            Setup(); var instant=Job(JobDefOf.Wear,70,saved);
            pawn.jobs.OnStart=()=>pawn.jobs.EndCurrentJob(JobCondition.Succeeded,true,true);
            pawn.jobs.OnEnd=()=>
            {
                instant.loadID=700; instant.def=JobDefOf.Wait; instant.targetA=default;
                AutomaticOutfitManagerGameComponent.Current.State=null;
                TransitionActivityDiagnostics.Cleared(pawn,null);
            };
            pawn.jobs.StartJob(instant);
            Check(pawn.jobs.StartCalls==1 && pawn.jobs.EndCalls==1 && pawn.CurJob==null,"real Harmony hooks preserve synchronous native admission and ending");
            Check(AomLog.Messages.Count==2 && AomLog.Messages[0].Contains("Wear #70") && AomLog.Messages[0].Contains("EndCurrentJob returned (Succeeded)"),"real ending hook captures identity before native pooling");
            Check(Last.Contains("Wear #70") && Last.Contains("StartJob returned") && Last.Contains("state=none"),"outer admission hook survives nested completion without claiming the old step is active");
            Setup(); pawn.CurJob=Job(JobDefOf.RemoveApparel,71,saved); pawn.jobs.EndCurrentJob(JobCondition.Incompletable,true,true);
            Check(Last.Contains("RemoveApparel #71") && Last.Contains("Incompletable") && pawn.CurJob==null,"real ending hook preserves native failure condition");
            Setup(); AomLog.DetailedEnabled=false; pawn.jobs.StartJob(Job(JobDefOf.Wear,72,saved)); pawn.jobs.EndCurrentJob(JobCondition.Succeeded,true,true);
            Check(AomLog.Messages.Count==0 && pawn.jobs.StartCalls==1 && pawn.jobs.EndCalls==1,"disabled detailed logging leaves real patched native jobs intact");
            Console.WriteLine(passed+" transition activity diagnostic checks passed."); return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
namespace Verse
{
    public class Map { }
    public struct IntVec3 { public int x; public IntVec3(int value){x=value;} public static bool operator ==(IntVec3 a,IntVec3 b)=>a.x==b.x; public static bool operator !=(IntVec3 a,IntVec3 b)=>a.x!=b.x; public override bool Equals(object o)=>o is IntVec3 v&&v==this; public override int GetHashCode()=>x; public override string ToString()=>x.ToString(); }
    public struct LocalTargetInfo { public Thing Thing; public LocalTargetInfo(Thing thing){Thing=thing;} public override string ToString()=>Thing?.ThingID??"invalid"; }
    public class Thing { public string ThingID="item"; }
    public class RaceProperties { public bool Humanlike=true; }
    public class Pawn { public Pawn(){jobs=new Pawn_JobTracker(this);} public Map Map; public bool Spawned=true,Drafted,Downed,InMentalState; public RaceProperties RaceProps=new RaceProperties(); public IntVec3 Position; public Pawn_JobTracker jobs; public Job CurJob{get=>jobs.curJob;set=>jobs.curJob=value;} public Pawn_PathFollower pather=new Pawn_PathFollower(); public CarryTracker carryTracker=new CarryTracker(); public string LabelShortCap=>"Pawn"; }
    public class CarryTracker { public Thing CarriedThing; }
    public class JobDef { public string defName; }
    public class TickManager { public int TicksGame; }
    public static class Find { public static TickManager TickManager=new TickManager(); }
}
namespace Verse.AI
{
    public class Job { public int loadID; public JobDef def; public LocalTargetInfo targetA,targetB,targetC; public bool playerForced; public object jobGiver; }
    public class QueuedJob { public Job job; }
    public class JobDriver { public int CurToilIndex,ticksLeftThisToil; }
    public enum JobCondition { Succeeded,Incompletable }
    public class Pawn_JobTracker
    {
        private readonly Pawn pawn;
        public Pawn_JobTracker(Pawn value){pawn=value;}
        public Job curJob; public JobDriver curDriver=new JobDriver(); public List<QueuedJob> jobQueue=new List<QueuedJob>();
        public Action OnStart,OnEnd; public int StartCalls,EndCalls;
        [MethodImpl(MethodImplOptions.NoInlining)] public void StartJob(Job newJob){StartCalls++;curJob=newJob;OnStart?.Invoke();}
        [MethodImpl(MethodImplOptions.NoInlining)] public void EndCurrentJob(JobCondition condition,bool startNewJob,bool canReturnToPool){EndCalls++;curJob=null;OnEnd?.Invoke();}
    }
    public class Pawn_PathFollower { public bool Moving; public LocalTargetInfo Destination; }
}
namespace RimWorld
{
    public class Apparel:Thing { }
    public static class JobDefOf { public static JobDef Wear=new JobDef{defName="Wear"}, RemoveApparel=new JobDef{defName="RemoveApparel"}, Wait=new JobDef{defName="Wait"}; }
}
namespace AutomaticOutfitManager.State
{
    public enum ApparelTransition { Preparing,Active,ReturningToChangingArea,Restoring }
    public class PawnApparelState { public ApparelTransition Transition; public string ActiveRuleId; public List<Apparel> OriginalApparel=new List<Apparel>(), Managed=new List<Apparel>(); public bool IsPreparationApparel(Apparel item)=>Managed.Contains(item); }
}
namespace AutomaticOutfitManager.Rules { public class ApparelRule { public string Id,Name;public bool WorkAreaPaused; } }
namespace AutomaticOutfitManager.Core
{
    public class AutomaticOutfitManagerGameComponent { public static AutomaticOutfitManagerGameComponent Current=new AutomaticOutfitManagerGameComponent(); public PawnApparelState State; public PawnApparelState StateFor(Pawn pawn)=>State; }
    public static class AomLog { public static bool DetailedEnabled=true; public static List<string> Messages=new List<string>(); static int last=-100000; public static void Reset(){DetailedEnabled=true;Messages.Clear();last=-100000;} public static void Detailed(string text)=>Messages.Add(text); public static bool ShouldLogDetailed(Pawn pawn,string category,int interval){int tick=Find.TickManager.TicksGame;if(tick-last<interval)return false;last=tick;return true;} }
}
