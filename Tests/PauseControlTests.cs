// Executes production ToggleWorkPause, recall scheduling, rest/nursing policies
// and restricted-route selection with the existing native-shaped job tracker.
using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.UI;
using Verse;
using Verse.AI;
using RimWorld;

namespace ZoologyMod {
 public class JobDriver_AnimalBreastfeed {}
 public class JobDriver_YoungSuckle {}
}
namespace Verse {
 public partial class RaceProperties { public bool Animal; }
 public partial class Pawn { public bool Dead; public Job CurJob=>jobs.curJob; }
}
namespace Verse.AI {
 public partial class Job { public bool NativeTargets; }
 public partial class Pawn_JobTracker {
  public int EndCalls; public Action OnEnd;
  public void EndCurrentJob(JobCondition condition,bool start) {EndCalls++;curJob=null;OnEnd?.Invoke();}
 }
}
namespace RimWorld { public static partial class JobDefOf {public static JobDef LayDown=new JobDef{defName="LayDown"};} }
namespace AutomaticOutfitManager.State {
 public class NestedRuleBufferState {public string RuleId,LastJobLabel;public int Completed,LastJobLoadId;public bool Finished;}
 public partial class PawnApparelState {
  public List<string> PauseRecallRuleIds=new List<string>();
  public List<NestedRuleBufferState> NestedRuleBuffers=new List<NestedRuleBufferState>();
  public string NonWorkRestorationRuleId;
  public bool ApparelInterventionActive,WeaponInterventionActive,DownedTransitionSuspended,DraftedTransitionSuspended;
  public int LastBufferedJobLoadId;
  public void ClearPendingBufferCandidates(){} public void ClearPendingBufferedTask(){}
 }
}
namespace AutomaticOutfitManager.Core {
 public partial class AutomaticOutfitManagerGameComponent {
  public IEnumerable<PawnApparelState> PawnStates=>States.Values;
  readonly Dictionary<Pawn,int> activeWorkProgress=new Dictionary<Pawn,int>(),jobTransitionFailureTicks=new Dictionary<Pawn,int>();
  public void RunRecall(int tick)=>ProcessPendingRecallInterrupts(tick);
  public void ExplicitRecall(PawnApparelState state){state.PauseRecallRuleIds.Clear();RequestRecallCore(state);}
  public void EndIntervention(Pawn pawn)=>States.Remove(pawn);
  bool TryJobTransition(Pawn p,int t,string reason,Action action){action();return true;}
  bool IsAssignedApparelTransitionJob(PawnApparelState s,Job j)=>j!=null&&PawnJobTracker_StartJob_Patch.IsAssignedTransitionApparelJob(s,j);
  bool IsAssignedWeaponTransitionJob(PawnApparelState s,Job j)=>j!=null&&PawnJobTracker_StartJob_Patch.IsAssignedTransitionWeaponJob(s,j);
  static void LogRecallRequest(PawnApparelState s,string requester){}
 }
}
namespace AutomaticOutfitManager.Detection {
 public static class ReadingDestination {public static bool IsCurrentDestination(Pawn p,Job j,LocalTargetInfo? d,Area a)=>false;}
 public static class EatingDestination {public static bool IsCurrentMealDestination(Pawn p,Job j,LocalTargetInfo? d,Area a)=>false;}
}
namespace AutomaticOutfitManager.Patches {
 public static class ChildcareContinuation {public static bool Admit(Pawn p,Job j)=>false;}
 public static class PawnPathFollower_ProtectedArea_Patch {
  public static bool IsManagedTransitionJob(Pawn p,Job j,PawnApparelState s)=>PreparationJobHandoff.IsOwnedStep(p,s,j);
  public static bool ManagedTransitionMayEnterRule(Pawn p,Job j,PawnApparelState s,ApparelRule r)=>false;
 }
 public static partial class ProtectedPathAvoidance {static readonly IReadOnlyList<ApparelRule> EmptyRules=new List<ApparelRule>();}
}
namespace AutomaticOutfitManager.UI {
 public static partial class MainRulesWindow {public static void Toggle(ApparelRule r,AutomaticOutfitManagerGameComponent c)=>ToggleWorkPause(r,c);}
}

partial class PausedHaulTests
{
 static Job Rest(Pawn pawn,string name="LayDown")=>new Job {def=new JobDef{defName=name},loadID=990,
  targetA=new LocalTargetInfo(new Thing {Map=pawn.Map,Position=1}),Targets=true};

 static void PauseControlCases()
 {
  foreach(bool nonWork in new[]{false,true}) foreach(string name in new[]{"LayDown","GotoBed"}) {
   var p=Setup(out var s,out var r);var c=AutomaticOutfitManagerGameComponent.Current;
   r.IsNonWork=nonWork;r.WorkAreaPaused=false;s.Transition=ApparelTransition.Active;s.PendingWorkJob=null;
   var rest=Rest(p,name);p.jobs.curJob=rest;s.BufferedTasksCompleted=1;
   MainRulesWindow.Toggle(r,c);c.RunRecall(1000);Pulse(p,r);
   Check(r.WorkAreaPaused&&!s.RecallRequested&&p.jobs.EndCalls==0&&p.CurJob==rest,"pause button preserves permitted sleeper");
   Check(c.StateFor(p)==s&&s.BufferedTasksCompleted==1&&p.jobs.jobQueue.Count==0,"sleep keeps state buffer and single native job");
   MainRulesWindow.Toggle(r,c);c.RunRecall(1010);
   Check(!r.WorkAreaPaused&&p.CurJob==rest&&p.jobs.EndCalls==0,"resume does not disturb preserved sleeper");
   // Schmurda's Non-Work rule is associated with the route, not the bed.
   p.Position=0;rest.Targets=false;r.WorkAreaPaused=false;
   MainRulesWindow.Toggle(r,c);c.RunRecall(1020);
   Check(!s.RecallRequested&&p.CurJob==rest,"route-associated rest avoids empty locker detour");
  }
  foreach(bool nested in new[]{false,true}) {
   var p=Setup(out var s,out var r);var c=AutomaticOutfitManagerGameComponent.Current;
   s.Transition=ApparelTransition.Active;s.PendingWorkJob=null;p.jobs.curJob=Rest(p);
   s.ActiveRuleId="other";s.CurrentRuleIds.Clear();r.WorkAreaPaused=false;
   if(nested)s.NestedRuleBuffers.Add(new NestedRuleBufferState{RuleId=r.Id});else s.CurrentRuleIds.Add(r.Id);
   MainRulesWindow.Toggle(r,c);c.RunRecall(1100);
   Check(!s.RecallRequested&&p.jobs.EndCalls==0,"nested or overlapping rest keeps its paused rule");
  }
  foreach(string step in new[]{"wear","equip","wait","active-wait","queued-rest"}) {
   var p=Setup(out var s,out var r);var c=AutomaticOutfitManagerGameComponent.Current;
   r.IsNonWork=true;r.WorkAreaPaused=false;var bed=Rest(p);s.PendingWorkJob=bed;
   Job current=step=="wear"?Step(p,s):step=="equip"?Step(p,s,true):PreparationHandoffTests.Wait();
   if(step=="active-wait"||step=="queued-rest")s.Transition=ApparelTransition.Active;
   if(step=="queued-rest") {s.PendingWorkJob=null;PreparationJobHandoff.RecordPreparedActivity(p,s,bed);p.jobs.jobQueue.Add(new QueuedJob{job=bed});}
   p.jobs.curJob=current;r.MissingGear=true;
   MainRulesWindow.Toggle(r,c);c.RunRecall(1200);Pulse(p,r);
   Check(!s.RecallRequested&&p.CurJob==current&&p.jobs.EndCalls==0,"pause button preserves exact bed preparation: "+step);
   Check(!PawnJobTracker_StartJob_Patch.ShouldResumePreparedJob(p,c,s,PreparationHandoffTests.Wait()),"pause preservation never waives missing PPE");
   r.MissingGear=false;
   if(step=="queued-rest")p.jobs.AdvanceQueue();
   else {s.Transition=ApparelTransition.Active;PawnJobTracker_StartJob_Patch.CompletePreparedJobAdmission(p,s,bed);p.jobs.StartJob(bed);}
   Check(p.CurJob==bed&&s.PendingWorkJob==null&&p.jobs.jobQueue.Count==0,"single native bed job after preserved preparation");
  }
  foreach(string kind in new[]{"DoBill","Ingest","WatchTelevision"}) foreach(bool intervention in new[]{false,true}) {
   var p=Setup(out var s,out var r);var c=AutomaticOutfitManagerGameComponent.Current;
   r.WorkAreaPaused=false;s.Transition=ApparelTransition.Active;s.PendingWorkJob=null;s.ApparelInterventionActive=intervention;
   p.jobs.curJob=new Job{def=new JobDef{defName=kind},Targets=true};
   p.jobs.OnEnd=()=>{s.Transition=ApparelTransition.ReturningToChangingArea;p.jobs.curJob=new Job{def=JobDefOf.Goto};};
   MainRulesWindow.Toggle(r,c);
   Check(s.RecallRequested&&p.jobs.EndCalls==0,"pause requests ordinary return without ending from UI");
   c.RunRecall(1300);
   Check(p.jobs.EndCalls==1&&!s.RecallInterruptPending&&c.StateFor(p)==s&&s.Transition==ApparelTransition.ReturningToChangingArea,"native recall callback retains returning state");
   MainRulesWindow.Toggle(r,c);
   Check(s.RecallRequested&&s.Transition==ApparelTransition.ReturningToChangingArea,"resume finishes already-started return");
  }
  foreach(bool child in new[]{false,true}) {
   var p=Setup(out var s,out var r);var c=AutomaticOutfitManagerGameComponent.Current;
   r.WorkAreaPaused=false;s.Transition=ApparelTransition.Active;s.PendingWorkJob=null;p.jobs.curJob=Rest(p);
   if(child){p.Child=true;r.Children=false;}else r.Activities=false;
   MainRulesWindow.Toggle(r,c);Check(s.RecallRequested,"pause still recalls rest with revoked access");
  }
  {
   var p=Setup(out var s,out var r);var c=AutomaticOutfitManagerGameComponent.Current;
   r.WorkAreaPaused=false;s.Transition=ApparelTransition.Active;s.PendingWorkJob=null;p.jobs.curJob=Rest(p);
   c.ExplicitRecall(s);MainRulesWindow.Toggle(r,c);MainRulesWindow.Toggle(r,c);
   Check(s.RecallRequested&&s.RecallInterruptPending&&s.PauseRecallRuleIds.Count==0,"pause resume cannot relabel or cancel explicit recall");
  }
  {
   var p=Setup(out var s,out var r);var c=AutomaticOutfitManagerGameComponent.Current;
   var other=new ApparelRule{Id="other",Area=r.Area,WorkAreaPaused=false};c.Rules.Add(other);s.CurrentRuleIds.Add(other.Id);
   r.WorkAreaPaused=false;s.Transition=ApparelTransition.Active;s.PendingWorkJob=null;p.jobs.curJob=new Job{def=new JobDef{defName="DoBill"},Targets=true};
   MainRulesWindow.Toggle(r,c);MainRulesWindow.Toggle(other,c);MainRulesWindow.Toggle(r,c);
   Check(s.RecallRequested&&s.PauseRecallRuleIds.SequenceEqual(new[]{other.Id}),"overlap resume cannot cancel another paused rule recall");
   MainRulesWindow.Toggle(other,c);Check(!s.RecallRequested&&!s.RecallInterruptPending,"resume cancels pending pause-only recall after all sources resume");
  }
  {
   var p=Setup(out var s,out var r);var c=AutomaticOutfitManagerGameComponent.Current;
   r.WorkAreaPaused=false;p.jobs.curJob=Step(p,s);
   MainRulesWindow.Toggle(r,c);c.RunRecall(1400);
   Check(!s.RecallRequested&&s.PendingWorkJob!=null&&p.jobs.EndCalls==0,"pause button still preserves permitted haul preparation");
  }
  NursingCases();
 }

 static void NursingCases()
 {
  foreach(bool nonWork in new[]{false,true}) {
   var mother=Setup(out var state,out var r);var c=AutomaticOutfitManagerGameComponent.Current;
   c.States.Clear();mother.RaceProps.Humanlike=false;mother.RaceProps.Animal=true;mother.Position=0;r.IsNonWork=nonWork;
   var young=new Pawn{Map=mother.Map,Position=1};young.RaceProps.Humanlike=false;young.RaceProps.Animal=true;
   var feeding=new Job{def=new JobDef{defName="Zoology_Breastfeed",driverClass=typeof(ZoologyMod.JobDriver_AnimalBreastfeed)},NativeTargets=true,
    targetA=new LocalTargetInfo(young),targetB=new LocalTargetInfo(mother)};
   mother.jobs.curJob=feeding;
   Check(PausedAreaWorkFilter.DeniedActivityRule(mother,feeding)==null,"nursing admitted by paused activity selection");
   Check(PausedAreaWorkFilter.DeniedPausedAreaRule(mother,feeding)==null,"nursing admitted by secondary pause selection");
   Check(PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(mother,feeding,r),"nursing admitted at paused path boundary");
   Check(!ProtectedPathAvoidance.RestrictedTransitRules(mother,feeding).Contains(r),"actual nursing destination is not surrounded by avoidance grid");
   Check(PausedAreaWorkFilter.JobMayEnterPausedRule(mother,feeding,r),"ongoing nursing agrees with pause enforcement");
   var original=feeding;MainRulesWindow.Toggle(r,c);MainRulesWindow.Toggle(r,c);c.RunRecall(1500);
   Check(mother.CurJob==original&&mother.jobs.EndCalls==0&&c.States.Count==0,"untracked animal feeding survives UI toggle without outfit state");
   var other=new ApparelRule{Id="unrelated",Area=new Area{Map=mother.Map,Cell=2}};c.Rules.Add(other);
   Check(ProtectedPathAvoidance.RestrictedTransitRules(mother,feeding).Contains(other),"nursing does not grant unrelated area shortcut");
   c.Rules.Remove(other);
   // Native mother driver starts YoungSuckle without a target once together.
   var suckle=new Job{def=new JobDef{defName="Zoology_YoungSuckle",driverClass=typeof(ZoologyMod.JobDriver_YoungSuckle)},NativeTargets=true};
   young.jobs.curJob=suckle;
   Check(PausedAreaWorkFilter.DeniedActivityRule(young,suckle)==null&&PausedAreaWorkFilter.DeniedPausedAreaRule(young,suckle)==null,"paired targetless suckling remains admitted during nursing");
   young.Position=0;
   Check(ProtectedPathAvoidance.RestrictedTransitRules(young,suckle).Contains(r),"targetless nursing grants no new protected route");
   mother.Position=1;suckle.targetA=new LocalTargetInfo(mother);
   Check(!ProtectedPathAvoidance.RestrictedTransitRules(young,suckle).Contains(r),"young food-giver route to exact mother remains admitted");
   r.Activities=false;
   Check(!AnimalNursingPolicy.Allowed(mother,feeding,r)&&!AnimalNursingPolicy.Allowed(young,suckle,r),"nursing never overrides animal activity permission");
   Check(PausedAreaWorkFilter.DeniedActivityRule(young,suckle)==r&& !PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(young,suckle,r),"revoked nursing access agrees at selection and boundary");
   r.WorkAreaPaused=false;Check(!AnimalNursingPolicy.Allowed(young,suckle,r),"unpausing never changes category permission");
   r.Activities=true;r.WorkAreaPaused=true;mother.Position=0;young.Position=1;
   foreach(string unrelated in new[]{"Ingest","DoBill","Zoology_ProtectYoung","Zoology_AnimalLickWounds","Zoology_PetPlay","SomeBreastfeedWork"}) {
    var j=new Job{def=new JobDef{defName=unrelated},targetA=new LocalTargetInfo(young),NativeTargets=true};
    Check(!AnimalNursingPolicy.Allowed(mother,j,r)&&PausedAreaWorkFilter.DeniedActivityRule(mother,j)==r,"unrelated animal activity stays paused: "+unrelated);
   }
   feeding.targetB=new LocalTargetInfo(young);Check(!AnimalNursingPolicy.Allowed(mother,feeding,r),"mother identity must match native job");feeding.targetB=new LocalTargetInfo(mother);
   feeding.def.driverClass=typeof(ZoologyMod.JobDriver_YoungSuckle);Check(!AnimalNursingPolicy.Allowed(mother,feeding,r),"job name alone cannot impersonate nursing");feeding.def.driverClass=typeof(ZoologyMod.JobDriver_AnimalBreastfeed);
   young.Map=new Map();Check(!AnimalNursingPolicy.Allowed(mother,feeding,r),"nursing requires same-map recipient");young.Map=mother.Map;
   young.Dead=true;Check(!AnimalNursingPolicy.Allowed(mother,feeding,r),"dead recipient has no nursing exemption");young.Dead=false;
   mother.RaceProps.Animal=false;Check(!AnimalNursingPolicy.Allowed(mother,feeding,r),"animal compatibility does not broaden human care policy");
  }
 }
}
