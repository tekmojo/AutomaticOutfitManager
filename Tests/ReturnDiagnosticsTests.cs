using System;
using System.Collections.Generic;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using Verse;
using Verse.AI;

class ReturnDiagnosticsTests
{
 static int passed;
 static void Check(bool ok,string name) { if(!ok)throw new Exception(name); passed++; Console.WriteLine("PASS "+name); }
 static int Main()
 {
  try {
   var map=new Map(); var pawn=new Pawn { Map=map }; var rule=new ApparelRule { Name="Work",Id="work",ReturnTaskBuffer=2 };
   var state=new PawnApparelState { BufferedTasksCompleted=1,Transition=ApparelTransition.Active };
   var job=new Job { def=new JobDef { defName="LayDown" },loadID=42,targetA=new LocalTargetInfo(7) }; pawn.CurJob=job;
   OutfitReturnReason.Record(pawn,state,rule,job,true,false);
   Check(state.ReturnReason.Contains("sleep"),"1/2 sleep return has a specific reason");
   Check(state.BufferedTasksCompleted==1 && !state.RecallRequested && state.Transition==ApparelTransition.Active,
    "recording a reason never consumes credit or initiates a return");
   Check(AomLog.Messages[0].Contains("LayDown #42") && AomLog.Messages[0].Contains("1/2"),"decision log identifies original job and incomplete count");
   state.Transition=ApparelTransition.ReturningToChangingArea; string original=state.ReturnReason;
   OutfitReturnReason.Record(pawn,state,rule,new Job(),false,false);
   Check(state.ReturnReason==original && AomLog.Messages.Count==1,"synthetic locker travel preserves original reason without duplicate decision logs");
   state.Transition=ApparelTransition.Restoring; OutfitReturnReason.Record(pawn,state,rule,new Job(),false,false);
   Check(state.ReturnReason==original,"restoration recovery preserves the reason");
   state.ReturnReason=null; OutfitReturnReason.Record(pawn,state,rule,job,true,false);
   Check(state.ReturnReason.Contains("original reason was not recorded"),"older saved return does not invent sleep as its original cause");
   state.Transition=ApparelTransition.Active; state.BufferedTasksCompleted=2;
   OutfitReturnReason.Record(pawn,state,rule,job,false,false);
   Check(state.ReturnReason=="Task buffer completed","a genuinely completed allowance is identified");
   state.BufferedTasksCompleted=1; OutfitReturnReason.Record(pawn,state,rule,job,false,true);
   Check(state.ReturnReason.Contains("another area rule"),"required area handoff is separate from buffer completion");
   OutfitReturnReason.Record(pawn,state,rule,job,false,false);
   Check(state.ReturnReason.Contains("before the buffer was complete"),"unexplained early handoff is reported honestly");
   state.BufferedTasksCompleted=2; state.AutomaticIdleReturnRequested=true;
   var destination=new ApparelRule { Name="Destination", Id="destination" };
   OutfitReturnReason.RecordSequentialHandoff(pawn,state,new[]{rule},new[]{destination},job);
   Check(state.ReturnReason=="Changing between incompatible area outfits","known sequential handoff overrides stale idle or completed-buffer narration");
   Check(AomLog.Messages[AomLog.Messages.Count-1].Contains("Destination [destination]") &&
    AomLog.Messages[AomLog.Messages.Count-1].Contains("LayDown #42"),"handoff captures exact destination and original job before synthetic travel");
   state.Transition=ApparelTransition.ReturningToChangingArea;
   OutfitReturnReason.Record(pawn,state,rule,new Job(),false,false);
   Check(state.ReturnReason=="Changing between incompatible area outfits","later synthetic waits preserve the handoff cause");
   Check(state.BufferedTasksCompleted==2 && state.AutomaticIdleReturnRequested,"handoff diagnostic does not alter counters or idle policy");
   state.Transition=ApparelTransition.Active; state.BufferedTasksCompleted=1;
   state.AutomaticIdleReturnRequested=true; state.RecallRequested=true;
   OutfitReturnReason.Record(pawn,state,rule,job,true,false);
   Check(state.ReturnReason.Contains("idle allowance"),"automatic idle recall is not mislabeled as explicit recall or sleep");
   state.AutomaticIdleReturnRequested=false; OutfitReturnReason.Record(pawn,state,rule,job,true,false);
   Check(state.ReturnReason.StartsWith("Recall or access"),"recall does not falsely imply a player click");
   rule.WorkAreaPaused=true; OutfitReturnReason.Record(pawn,state,rule,job,true,false);
   Check(state.ReturnReason=="Work paused","pause is identified before generic recall");
   state.NonWorkRestorationRuleId="dining"; OutfitReturnReason.Record(pawn,state,rule,job,true,false);
   Check(state.ReturnReason.Contains("Non-Work"),"Non-Work destination identifies its outfit change");
   state.MapDepartureRequested=true; OutfitReturnReason.Record(pawn,state,rule,job,true,false);
   Check(state.ReturnReason.Contains("leaving the map"),"departure has its own return reason");
   Check(OutfitReturnReason.Describe(false,false,false,false,false,false,false,0,0,false).Contains("Immediate"),"disabled allowance is not reported as a completed task");

   AomLog.Messages.Clear(); var follower=new Pawn_PathFollower { pawn=pawn,Destination=new LocalTargetInfo(9) };
   var request=new PathRequest { pawn=pawn,map=map,Start=new IntVec3(2),Target=new LocalTargetInfo(9),customizer=new object() };
   ProtectedRouteFailureDiagnostics.Capture(follower,request);
   request.Target=new LocalTargetInfo(99); request.customizer=null;
   ProtectedRouteFailureDiagnostics.Failed(follower);
   Check(AomLog.Messages.Count==1 && AomLog.Messages[0].Contains("2 -> 9") && AomLog.Messages[0].Contains("actual customizer=System.Object"),
    "failed path uses captured original request and customizer after request object changes");
   AomLog.Messages.Clear(); job.loadID++;
   ProtectedRouteFailureDiagnostics.Failed(follower);
   Check(AomLog.Messages.Count==0,"new job cannot inherit a prior request failure");
   job.loadID--; follower.Destination=new LocalTargetInfo(8); ProtectedRouteFailureDiagnostics.Failed(follower);
   Check(AomLog.Messages.Count==0,"different destination cannot borrow captured travel context");
   follower.Destination=new LocalTargetInfo(9); pawn.Map=new Map(); ProtectedRouteFailureDiagnostics.Failed(follower);
   Check(AomLog.Messages.Count==0,"map transfer invalidates old request evidence");
   pawn.Map=map; ProtectedRouteFailureDiagnostics.ResetForLoadedGame(); ProtectedRouteFailureDiagnostics.Failed(follower);
   Check(AomLog.Messages.Count==0,"load reset clears transient request evidence");
   request.Target=follower.Destination; ProtectedRouteFailureDiagnostics.Capture(follower,request);
   ProtectedRouteFailureDiagnostics.Capture(follower,null); ProtectedRouteFailureDiagnostics.Failed(follower);
   Check(AomLog.Messages.Count==0,"failed request generation cannot retain older evidence");
   AomLog.DetailedEnabled=false; ProtectedRouteFailureDiagnostics.Capture(follower,request); AomLog.DetailedEnabled=true;
   ProtectedRouteFailureDiagnostics.Failed(follower);
   Check(AomLog.Messages.Count==0,"normal logging does not collect detailed route snapshots");
   Console.WriteLine(passed+" return-reason and travel-evidence checks passed.");return 0;
  } catch(Exception e) {Console.Error.WriteLine(e);return 1;}
 }
}
namespace Verse {
 public class Map {}
 public struct IntVec3 { public int x; public IntVec3(int v){x=v;}public bool IsValid=>x>=0;public bool InBounds(Map m)=>true; public override string ToString()=>x.ToString(); public static bool operator ==(IntVec3 a,IntVec3 b)=>a.x==b.x;public static bool operator !=(IntVec3 a,IntVec3 b)=>a.x!=b.x;public override bool Equals(object o)=>o is IntVec3 v&&v==this;public override int GetHashCode()=>x; }
 public struct LocalTargetInfo { public IntVec3 Cell;public LocalTargetInfo(int v){Cell=new IntVec3(v);}public override string ToString()=>Cell.ToString(); }
 public class Thing { public string ThingID="thing"; }
 public class CarryTracker { public Thing CarriedThing; }
 public class Pawn {public Map Map;public Job CurJob;public CarryTracker carryTracker=new CarryTracker();public string LabelShortCap=>"pawn";}
 public class JobDef {public string defName;}
 public class PathRequest {public Pawn pawn;public Map map;public IntVec3 Start,ExactDestination;public LocalTargetInfo Target;public string EndMode="OnCell";public object customizer;}
 public static class Find {public static TickManager TickManager=new TickManager();} public class TickManager {public int TicksGame=12;}
}
namespace Verse.AI { public class Job {public JobDef def;public int loadID;public LocalTargetInfo targetA,targetB,targetC;}public class Pawn_PathFollower {public Pawn pawn;public LocalTargetInfo Destination;} }
namespace AutomaticOutfitManager.Rules {public class Area {public bool this[IntVec3 c]=>true;}public class ApparelRule {public string Name,Id;public int ReturnTaskBuffer;public bool WorkAreaPaused;public Area Area=new Area();}}
namespace AutomaticOutfitManager.State {public enum ApparelTransition {Active,ReturningToChangingArea,Restoring} public class PawnApparelState {public string ReturnReason,NonWorkRestorationRuleId;public ApparelTransition Transition;public bool MapDepartureRequested,AutomaticIdleReturnRequested,RecallRequested;public int BufferedTasksCompleted;public List<string> PauseRecallRuleIds=new List<string>();}}
namespace AutomaticOutfitManager.Core {public static class AomLog {public static bool DetailedEnabled=true;public static List<string> Messages=new List<string>();public static void Detailed(string s)=>Messages.Add(s);public static bool ShouldLogDetailed(Pawn p,string s,int ticks)=>true;}}
namespace AutomaticOutfitManager.Detection {public static class RuleEvaluator {public static List<ApparelRule> MatchingRules(Pawn p,Job j)=>new List<ApparelRule>();public static List<ApparelRule> EnabledRulesForMap(Map m)=>new List<ApparelRule>{new ApparelRule()};public static bool HasMissingRequiredGear(Pawn p,ApparelRule r)=>false;}}
namespace AutomaticOutfitManager.Patches {public static class PreparationJobHandoff {public static string TrackerDescription(Pawn p)=>"tick=100, current=Wait#1, queue=[HaulToCell#2]";}public static class PausedAreaWorkFilter {public static bool ActivityAllowedAtRuleBoundary(Pawn p,Job j,ApparelRule r)=>true;}public static class ProtectedPathAvoidance {public static string DescribeCustomizer(object o)=>o?.GetType().FullName??"none";}}
