// Real Harmony installation with production Prepare gates and installer.
// Deterministic pawn state tests compile production diagnostics unchanged.
using System; using System.Collections.Generic; using System.Runtime.CompilerServices;
using HarmonyLib; using Verse; using Verse.AI;
using AutomaticOutfitManager.Core; using AutomaticOutfitManager.State;
using AutomaticOutfitManager.Patches; using AutomaticOutfitManager.Detection;
class StartupTarget {
    static StartupTarget(){if(!StartupTests.LanguageReady)throw new Exception("Translation requested before language ready");StartupTests.Initializations++;}
    [MethodImpl(MethodImplOptions.NoInlining)] public static bool Run()=>true;
}
class StartupTests {
    public static bool LanguageReady; public static int Initializations,Postfixes;static int passed;
    static void Check(bool b,string s){if(!b)throw new Exception(s);passed++;}
    static void Main(){
        new Harmony(AutomaticOutfitManagerMod.HarmonyId).PatchAll(typeof(StartupTests).Assembly);
        Check(!DeferredWorkScannerPatches.Ready,"Constructor patch pass must leave scanner group closed");
        Check(Initializations==0,"Early patch pass must not initialize translated target");
        LanguageReady=true; DeferredWorkScannerPatches.Install();
        Check(DeferredWorkScannerPatches.Ready,"Post-load scanner group enabled");
        Check(!StartupTarget.Run(),"Filtering installed before gameplay");
        Check(Postfixes==7,"All seven production-gated groups installed");
        Check(Initializations==1,"Target initialized exactly once after language ready");
        DeferredWorkScannerPatches.Install();Postfixes=0;StartupTarget.Run();
        Check(Postfixes==7,"Repeated install does not duplicate patches");
        var p=new Pawn(); var s=new PawnApparelState{Transition=ApparelTransition.Restoring};
        p.CurJob=new Job{def=new JobDef{defName="Wait_MaintainPosture"},loadID=42};
        p.jobs.curDriver.ticksLeftThisToil=-12; p.jobs.jobQueue.Add(new QueuedJob{job=new Job{def=new JobDef{defName="Wear"},loadID=43,targetA="saved sash"}});
        var book=new Thing{LabelCap="Novel",ThingID="Novel41035"};p.carryTracker.CarriedThing=book;
        AomLog.DetailedEnabled=false;RestorationWaitDiagnostics.Report(p,s);Check(AomLog.Messages.Count==0,"Quiet mode has no wait tracing");
        AomLog.DetailedEnabled=true;p.pather.Moving=true;RestorationWaitDiagnostics.Report(p,s);Check(AomLog.Messages.Count==0,"Moving pawn not reported as waiting");
        p.pather.Moving=false;s.Transition=ApparelTransition.Active;RestorationWaitDiagnostics.Report(p,s);Check(AomLog.Messages.Count==0,"Active work excluded");
        s.Transition=ApparelTransition.Restoring;RestorationWaitDiagnostics.Report(p,s);
        Check(AomLog.Messages.Count==1,"Carried book does not hide diagnostic");
        string m=AomLog.Messages[0];Check(m.Contains("Novel41035")&&m.Contains("#42")&&m.Contains("#43")&&m.Contains("saved sash")&&m.Contains("ticksLeft=-12"),"Actionable current/carry/queue/timer identities");
        Check(SavedGearRestorationDiagnostics.Calls==1,"Wait includes retrieval checks");
        RestorationWaitDiagnostics.Report(p,s);Check(AomLog.Messages.Count==1&&SavedGearRestorationDiagnostics.Calls==1,"Repeated wait coalesces probes and output");
        Check(p.CurJob.loadID==42&&p.jobs.jobQueue.Count==1&&p.carryTracker.CarriedThing==book&&s.Transition==ApparelTransition.Restoring,"Diagnostic preserves job queue cargo and state");
        AomLog.Tick=600;RestorationWaitDiagnostics.Report(p,s);Check(AomLog.Messages.Count==2,"Later wait can report changed evidence");
        p.CurJob.def.defName="Wear";p.CurJob.Report="Wearing sash";AomLog.Tick=1200;RestorationWaitDiagnostics.Report(p,s);Check(AomLog.Messages.Count==2,"Real wear progress excluded");
        Console.WriteLine(passed+" Ocag follow-up contracts passed.");
    }
}
namespace Verse {
    public class StaticConstructorOnStartupAttribute:Attribute{}
    public class Map{} public class Thing {public string LabelCap,ThingID;}
    public class JobDef {public string defName;}
    public class Pawn:Thing {public Map Map=new Map();public object Position="(1,0,2)";public PawnPather pather=new PawnPather();public CarryTracker carryTracker=new CarryTracker();public Tracker jobs=new Tracker();public Job CurJob;public string LabelShortCap="Ocag";}
    public class PawnPather {public bool Moving;} public class CarryTracker {public Thing CarriedThing;}
    public class Tracker {public Driver curDriver=new Driver();public List<QueuedJob> jobQueue=new List<QueuedJob>();}
    public class Driver{public int CurToilIndex,ticksLeftThisToil;}
    public class QueuedJob{public Job job;}
}
namespace Verse.AI {public class Job{public JobDef def;public int loadID;public bool playerForced;public string targetA,targetB;public string Report="Standing";public string GetReport(Pawn p)=>Report;}}
namespace AutomaticOutfitManager.State{public enum ApparelTransition{Active,Restoring}public class PawnApparelState{public ApparelTransition Transition;public int LastRestorationAttemptTick;}}
namespace AutomaticOutfitManager.Core {
    public class AutomaticOutfitManagerMod{public const string HarmonyId="aom.test.deferred";}
    public class AomLog{public static bool DetailedEnabled;public static int Tick;static int last=-600;public static List<string> Messages=new List<string>();public static bool ShouldLogDetailed(Pawn p,string key,int interval){if(Tick-last<interval)return false;last=Tick;return true;}public static void Detailed(string m)=>Messages.Add(m);}
}
namespace AutomaticOutfitManager.Detection {public class SavedGearRestorationDiagnostics{public static int Calls;public static void Report(Pawn p,PawnApparelState s){Calls++;}}}
