// Shares the native-shaped, Harmony-patched queue body with preparation tests.
// Area/path projection is controlled here; queue ownership is production code.
using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using HarmonyLib;
using Verse;
using Verse.AI;
using RimWorld;

namespace Verse {
 public struct IntVec3 {public int X;public static IntVec3 Invalid=>(IntVec3)(-1);public bool IsValid=>X>=0;public bool InBounds(Map m)=>IsValid;public static implicit operator IntVec3(int x)=>new IntVec3{X=x};public static implicit operator int(IntVec3 c)=>c.X;}
 public partial struct LocalTargetInfo {IntVec3 cell; public bool HasThing=>Thing!=null; public IntVec3 Cell{get=>Thing!=null?Thing.Position:(HasCell?cell:(IntVec3)(-1));set{cell=value;HasCell=value.IsValid;}}}
 public enum ThingCategory {Item,Building}
 public class ThingDef {public ThingCategory category;public bool IsApparel,IsWeapon;public int stackLimit=75;}
 public partial class Thing {public IntVec3 Position;public IntVec3 PositionHeld=>Position;public Map MapHeld=>Map;public ThingDef def=new ThingDef();public string LabelCap=>"target";}
 public class Faction {public static Faction OfPlayerSilentFail=new Faction();}
 public partial class Thing {public List<IntVec3> Footprint;}
 public static class GenAdj {public static IEnumerable<IntVec3> CellsOccupiedBy(Thing t)=>t.Footprint??new List<IntVec3>{t.Position};}
 public class CarryTracker {public Thing CarriedThing;}
 public partial class Pawn {public CarryTracker carryTracker=new CarryTracker();public Faction Faction=Faction.OfPlayerSilentFail;}
 public class Area { public Map Map; public int Cell=1; public bool this[int cell]=>cell==Cell; }
 public partial class RaceProperties {public object body;public bool Humanlike=true;}
 public partial class Pawn {public string LabelShortCap=>"Test";public RaceProperties RaceProps=new RaceProperties();public bool Child,CanWear=true;public ApparelTracker apparel=new ApparelTracker();public Equipment equipment=new Equipment();public HashSet<Thing> Reserved=new HashSet<Thing>();public bool CellReserved;public bool CanReserve(LocalTargetInfo t,int a,int b,object c,bool d)=>t.HasThing?!Reserved.Contains(t.Thing):!CellReserved;}
 public class ApparelTracker {public List<Apparel> WornApparel=new List<Apparel>();}
 public class Equipment {public ThingWithComps Primary;}
}
namespace RimWorld {public class Frame:Thing{} public class Blueprint:Thing{} public class JobDriver_HaulToCell{} public class JobDriver_HaulToContainer{} public class JobDriver_Refuel{} public class WorkTypeDef {public string defName;} public class WorkGiverDef {public string defName;public WorkTypeDef workType;} public static class WorkTypeDefOf {public static WorkTypeDef Hauling=new WorkTypeDef{defName="Hauling"};} public static partial class JobDefOf {public static JobDef HaulToCell=new JobDef{defName="HaulToCell",driverClass=typeof(JobDriver_HaulToCell),allowOpportunisticPrefix=true};public static JobDef HaulToContainer=new JobDef{defName="HaulToContainer",driverClass=typeof(JobDriver_HaulToContainer),allowOpportunisticPrefix=true};}}
namespace Verse.AI {
 public class ThinkNode {}
 public enum JobCondition {Succeeded,Incompletable,InterruptForced}
 public partial class Job {public ThinkNode jobGiver;public WorkGiverDef workGiverDef;public bool Targets,Crosses,Avoidable;}
}
namespace RimWorld {
 public static partial class JobDefOf {public static JobDef Ingest=new JobDef{defName="Ingest"};public static JobDef Goto=new JobDef{defName="Goto"};public static JobDef GotoWander=new JobDef{defName="GotoWander"};}
 public class WorkGiver_Scanner {
  public WorkGiverDef def=new WorkGiverDef();public Job Candidate;public int NativeCreates;
  [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
  public bool HasJobOnThing(Pawn p,Thing t,bool forced=false)=>Candidate!=null&&!t.Destroyed&&!p.Reserved.Contains(t);
  [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
  public Job JobOnThing(Pawn p,Thing t,bool forced=false){NativeCreates++;return Candidate;}
 }
 public class WorkGiver_HaulGeneral:WorkGiver_Scanner{}
 public class WorkGiver_Refuel:WorkGiver_Scanner{}
}
namespace AutomaticOutfitManager.Rules {
 public class ApparelRule {public string Id="ship";public string Name=>Id;public Area Area;public bool Enabled=true,WorkAreaPaused=true,Hauling=true,Wandering,Activities=true,Children=true,IsNonWork,MissingGear,SavedPersonal,MustReturnWork;public int ReturnTaskBuffer=3;}
}
namespace AutomaticOutfitManager.State {
 public static class NonWorkOutfitPolicy {public static bool ShouldReturn(Pawn p,ApparelRule r,ThingWithComps t)=>r.MustReturnWork;}
 public partial class PawnApparelState {public Pawn Pawn;public string ActiveRuleId="ship";public List<string> CurrentRuleIds=new List<string>{"ship"};public int PermittedHaulGraceUntilTick=-1,BufferedTasksCompleted;public string PermittedHaulGraceRuleId;}
}
namespace AutomaticOutfitManager.Core {
 public static class AomLog {public static bool DetailedEnabled=>false;public static void Detailed(string s){}public static bool ShouldLogDetailed(Pawn p,string s,int ticks)=>false;}
 public partial class AutomaticOutfitManagerGameComponent {
  // Suspension state cleanup is exercised by NativeRuleControlTests. This
  // pause fixture uses the production predicate at that component boundary.
  bool UpdateNativeRuleSuspension(Pawn p,Job j)=>NativeRuleControl.MentalOrEmergency(p,j);
  public List<ApparelRule> Rules=new List<ApparelRule>();
  public ApparelRule RuleById(string id)=>Rules.FirstOrDefault(r=>r.Id==id);
  public static void ClearPendingWork(PawnApparelState s){s.PendingWorkJob=null;}
  // The scheduler uses these same two production decisions, in this order.
  public void Pulse(Pawn p,ApparelRule r) {
   var s=StateFor(p);var j=p.jobs.curJob;
   if(PausedAreaWorkFilter.DeniedActivityRule(p,j)!=null) s.RecallRequested=true;
   else if(PausedAreaWorkFilter.ShouldRecallForPausedRule(s,r,PausedAreaWorkFilter.JobMayEnterPausedRule(p,j,r))) s.RecallRequested=true;
  }
 }
}
namespace AutomaticOutfitManager.Detection {
 public static class ManagedWorkCandidateFilter {public static bool Rejects(Pawn p,Job j)=>false;}
 public static class ManagedWorkClaimRegistry {public static bool IsClaimedByOther(Pawn p,Map m,Thing t,IntVec3 c)=>false;}
 public class CombinedWeaponRequirement {public bool Matches(Thing t)=>true;}
 public static class ProtectedBoundaryRetryRegistry {public static List<ApparelRule> MatchingRules(Pawn p,Job j)=>new List<ApparelRule>();}
 public static class PawnAccessClassifier {public static bool IsNativeCustodyEscapeActive(Pawn p)=>false;public static bool IsApparelEligibleHuman(Pawn p)=>p.RaceProps.Humanlike;public static bool IsHostedGuest(Pawn p)=>false;public static bool IsColonyPrisoner(Pawn p)=>false;}
 public static class UnavailableWorkRegistry {public static void ClearPauseBlocks(ApparelRule r){}public static HashSet<string> Blocks=new HashSet<string>();public static bool HasActiveRuleBlock(Pawn p,ApparelRule r)=>Blocks.Contains(r.Id);public static bool ShouldReject(Pawn p,Map m,Thing t,IntVec3 c)=>false;public static bool ShouldReject(Pawn p,Job j)=>false;}
 public static class RuleEvaluator {
  public static void ResetRuntimeCache(){}
  public static List<ApparelRule> EnabledRulesForMap(Map m)=>AutomaticOutfitManagerGameComponent.Current.Rules.Where(r=>r.Enabled&&r.Area.Map==m).ToList();
  public static List<ApparelRule> PausedRulesForMap(Map m)=>EnabledRulesForMap(m).Where(r=>r.WorkAreaPaused).ToList();
  public static List<ApparelRule> MatchingRules(Pawn p,Job j)=>new List<ApparelRule>();
  public static List<ApparelRule> MatchingLocationRules(Pawn p)=>new List<ApparelRule>();
  public static bool UsesSavedNonWorkOutfit(Pawn p,ApparelRule r)=>r.SavedPersonal;
  public static bool HasMissingRequiredGear(Pawn p,ApparelRule r)=>r.MissingGear;
  public static bool TryCombinedWeaponRequirement(List<ApparelRule> r,out CombinedWeaponRequirement w,Pawn p){w=new CombinedWeaponRequirement();return true;}
  public static bool RuleCanApplyToPawn(Pawn p,ApparelRule r)=>p.CanWear;
  public static bool JobTargetsArea(Job j,Area a)=>j!=null&&(j.NativeTargets?(j.targetA.IsValid&&a[j.targetA.Cell]||j.targetB.IsValid&&a[j.targetB.Cell]):j.Targets);
  public static bool JobPreparationTargetsArea(Job j,Area a)=>j?.Targets==true;
 }
}
namespace AutomaticOutfitManager.Patches {
 internal static class AutomaticOutfitManagerJobDefOf {internal static JobDef AutomaticOutfitManager_LockerReturn=new JobDef{defName="AOM_LockerReturn"};}
 public static class AccessExitJobs {public static bool IsOwned(Pawn p,Job j)=>false;}
 internal static partial class PawnJobTracker_StartJob_Patch {
  static bool SameJob(Job a,Job b)=>ReferenceEquals(a,b);
  static bool IsRecoveryWaitJob(Job j)=>j?.def==JobDefOf.Wait;
  static List<ApparelRule> PersistedBoundaryRulesForJob(Pawn p,Job j)=>new List<ApparelRule>();
  internal static bool IsAssignedTransitionApparelJob(PawnApparelState s,Job j)=>j.def==JobDefOf.Wear&&PreparationJobHandoff.IsOwnedStep(s.Pawn,s,j);
  internal static bool IsAssignedTransitionWeaponJob(PawnApparelState s,Job j)=>j.def==JobDefOf.Equip&&PreparationJobHandoff.IsOwnedStep(s.Pawn,s,j);
 }
 public static class ChildAreaAccessPolicy {public static bool Disallows(Pawn p,ApparelRule r)=>p.Child&&!r.Children;}
 public static partial class ProtectedPathAvoidance {
  public static bool JobPathCrossesArea(Pawn p,Job j,Area a)=>j.Crosses;
  // Path search is a controlled oracle; the production combined-route policy
  // decides whether this avoidable crossing may request preparation.
  static bool RouteExistsAvoiding(Pawn p,Job j,List<ApparelRule> r)=>j.Avoidable;
 }
 public static partial class PausedAreaWorkFilter {
  static bool IsRobotOrMechanoid(Pawn p)=>false;
  static bool IsManagedPawn(Pawn p)=>true;
  internal static bool IsHaulingJob(Job j)=>ActivityJobClassifier.IsHauling(j);
  internal static List<ApparelRule> MatchingProtectedTransitRules(Pawn p,Job j)=>new List<ApparelRule>();
  static bool HaulingAllowedFor(ApparelRule r,Pawn p)=>r.Hauling&&!ChildAreaAccessPolicy.Disallows(p,r);
  static bool WanderingAllowedFor(ApparelRule r,Pawn p)=>r.Wandering;
  internal static bool WorkAllowedFor(ApparelRule r,Pawn p)=>r.Activities;
  static bool IsRestrictedRoamingJob(Pawn p,Job j,ThinkNode n)=>j?.def?.defName=="Goto";
  static bool HasNativeActivityOverride(Pawn p,Job j,bool transitions=true)=>j.playerForced||p.Drafted||p.Downed||p.InMentalState||(transitions&&PreparationJobHandoff.IsOwnedStep(p,AutomaticOutfitManagerGameComponent.Current.StateFor(p),j));
  static bool ChildActivityHasNativeOverride(Pawn p,Job j,bool transitions)=>HasNativeActivityOverride(p,j,transitions);
  static bool IsChildExitJob(Pawn p,Job j,ApparelRule r)=>false;
  static bool IsOwnedAccessEgress(Pawn p,Job j,ApparelRule r)=>false;
  static bool IsRestrictedRoamingEgress(Pawn p,Job j,ApparelRule r)=>false;
  static bool ShouldAllowEssentialActivityFallback(Pawn p,Job j,List<ApparelRule> r)=>false;
  static bool HaulingPathCrossesArea(Pawn p,Job j,Area a)=>j.Crosses;
  static bool MatchesPermittedWanderingRule(Pawn p,Job j,ApparelRule r)=>false;
 }
}
partial class PausedHaulTests {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
 static Job Haul(string kind="HaulToCell")=>new Job{def=kind=="HaulToCell"?JobDefOf.HaulToCell:JobDefOf.HaulToContainer,Targets=true,loadID=300};
 static Job Step(Pawn p,PawnApparelState s,bool weapon=false){
  ThingWithComps t=weapon?new ThingWithComps():new Apparel();t.Map=p.Map;
  if(weapon)s.Weapons.Add(t);else s.Apparel.Add((Apparel)t);
  return new Job{def=weapon?JobDefOf.Equip:JobDefOf.Wear,targetA=new LocalTargetInfo(t),loadID=weapon?203:202,playerForced=!weapon};
 }
 static Pawn Setup(out PawnApparelState s,out ApparelRule r){
  var p=new Pawn();p.Position=1;s=new PawnApparelState{Pawn=p,PendingWorkJob=Haul()};
  r=new ApparelRule{Area=new Area{Map=p.Map}};
  var c=new AutomaticOutfitManagerGameComponent();AutomaticOutfitManagerGameComponent.Current=c;c.States[p]=s;c.Rules.Add(r);
  p.jobs.Guards=true;return p;
 }
 static void Pulse(Pawn p,ApparelRule r)=>AutomaticOutfitManagerGameComponent.Current.Pulse(p,r);
 static int Main(){try{
  var harmony=new Harmony("aom.tests.paused-haul");harmony.PatchAll(typeof(PreparationJobHandoff).Assembly);
  foreach(string kind in new[]{"HaulToCell","HaulToContainer"}){
   var p=Setup(out var s,out var r);s.PendingWorkJob=Haul(kind);var haul=s.PendingWorkJob;
   var suit=Step(p,s);var helmet=Step(p,s);var equip=Step(p,s,true);
   p.jobs.jobQueue.Add(new QueuedJob{job=helmet});p.jobs.jobQueue.Add(new QueuedJob{job=equip});
   p.jobs.StartJob(suit);Pulse(p,r);Check(!s.RecallRequested,"permitted source haul begins PPE");
   p.Position=0;haul.Targets=false;haul.Crosses=false;Pulse(p,r);
   Check(!s.RecallRequested,"locker route change must not recall owned haul");
   // Wear success clears current, then native StartJob inserts a posture wait.
   p.jobs.curJob=null;var wait=PreparationHandoffTests.Wait();wait.jobGiver=new ThinkNode();p.Position=1;
   p.jobs.StartJob(wait);Pulse(p,r);
   Check(!s.RecallRequested,"native connective wait must not recall owned haul");
   Check(p.jobs.jobQueue.Count==2&&s.PendingWorkJob==haul,"wait preserves exact helmet weapon and native haul");
   Check(PausedAreaWorkFilter.DeniedPausedAreaRule(p,wait)==null,"pause candidate guard agrees with watchdog");
   p.jobs.AdvanceQueue();Check(p.jobs.curJob==helmet,"helmet resumes after native wait");Pulse(p,r);
   p.jobs.AdvanceQueue();Check(p.jobs.curJob==equip&&!s.RecallRequested,"weapon finishes without fresh PPE plan");
   s.Transition=ApparelTransition.Active;
   p.jobs.curJob=wait;Pulse(p,r);
   Check(!s.RecallRequested,"active pending haul survives completion wait");
   var component=AutomaticOutfitManagerGameComponent.Current;
   Check(PawnJobTracker_StartJob_Patch.ShouldResumePreparedJob(p,component,s,wait),"active pending haul resumes after complete PPE");
   r.MissingGear=true;Check(!PawnJobTracker_StartJob_Patch.ShouldResumePreparedJob(p,component,s,wait),"incomplete PPE cannot resume haul");r.MissingGear=false;
   haul.targetA=new LocalTargetInfo(new Thing{Map=p.Map});haul.targetB=new LocalTargetInfo{Cell=0};
   Check(PawnJobTracker_StartJob_Patch.PendingWorkJobIsViable(p,haul,out var why,out var retry),"full viability survives locker route change");
   p.CellReserved=true;Check(!PawnJobTracker_StartJob_Patch.PendingWorkJobIsViable(p,haul,out why,out retry)||kind!="HaulToCell","destination reservation rechecked");p.CellReserved=false;
   p.Reserved.Add(haul.targetA.Thing);Check(!PawnJobTracker_StartJob_Patch.PendingWorkJobIsViable(p,haul,out why,out retry)&&retry,"source reservation rechecked");p.Reserved.Clear();
   haul.targetA.Thing.Destroyed=true;Check(!PawnJobTracker_StartJob_Patch.PendingWorkJobIsViable(p,haul,out why,out retry),"destroyed target rejected");haul.targetA.Thing.Destroyed=false;
   PawnJobTracker_StartJob_Patch.CompletePreparedJobAdmission(p,s,haul);
   Check(s.PendingWorkJob==null,"native admission has one serialized owner");
   p.jobs.NativeFinalizer=PreparationHandoffTests.Wait();p.jobs.StartJob(haul);Pulse(p,r);
   Check(!s.RecallRequested&&p.jobs.jobQueue[0].job==haul,"queued prepared haul retains exception through finalizer");
   p.jobs.AdvanceQueue();Pulse(p,r);
   Check(p.jobs.curJob==haul&&!s.RecallRequested,"native exact haul admitted once after complete outfit");
   Check(!PreparationJobHandoff.IsPreparedActivity(p,s,haul),"native callback clears admission marker");
   // Pickup no longer targets the protected source; delivery is still hauling.
   p.Position=0;Pulse(p,r);Check(!s.RecallRequested,"pickup and outbound delivery keep PPE session");
   Check(p.jobs.Attempts==1&&p.jobs.Rejections==0,"single weapon preparation without queue cancellation");
   p.jobs.curJob=new Job{def=new JobDef{defName="DoBill"},Targets=true};p.Position=1;Pulse(p,r);
   Check(s.RecallRequested,"new paused activity cannot inherit haul permission");
  }
  {
   var p=Setup(out var s,out var r);p.jobs.curJob=Step(p,s);s.PendingWorkJob.Targets=false;
   var wait=PreparationHandoffTests.Wait();wait.jobGiver=new ThinkNode();
   Check(PausedAreaWorkFilter.DeniedActivityRule(p,wait)==null,"owned wait accepted even with native work giver");
   foreach(string activity in new[]{"Ingest","Recreation","DoBill","Repair"}){
    var j=new Job{def=new JobDef{defName=activity},Targets=true};
    Check(PausedAreaWorkFilter.DeniedActivityRule(p,j)==r,"paused activity rejected while haul prepares: "+activity);
    Check(PausedAreaWorkFilter.DeniedPausedAreaRule(p,j)==r,"paused target guard rejects: "+activity);
   }
   s.RecallRequested=true;Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(s,r),"explicit recall wins");s.RecallRequested=false;
   r.Hauling=false;Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(s,r),"hauling revocation wins");r.Hauling=true;
   r.Children=false;p.Child=true;Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(s,r),"children access still enforced");p.Child=false;
   p.CanWear=false;Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(s,r),"incompatible PPE still rejected");p.CanWear=true;
   p.Drafted=true;Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(s,r),"drafted control preserved");p.Drafted=false;
   s.MapDepartureRequested=true;Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(s,r),"departure restoration preserved");s.MapDepartureRequested=false;
   var other=new ApparelRule{Id="other",Area=r.Area};Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(s,other),"unrelated paused rule gains no exception");
   var map=p.Map;p.Map=new Map();Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(s,r),"no stale exception on another map");p.Map=map;
   s.PendingWorkJob=null;Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(s,r),"cancelled haul cannot protect outfit steps");
   s.Transition=ApparelTransition.Active;p.jobs.curJob=wait;Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(s,r),"completed haul cannot protect indefinite idle wait");
   p.jobs.jobQueue.Add(new QueuedJob{job=Haul()});Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(s,r),"unmarked future haul is not exact continuation");
   var meal=new Job{def=new JobDef{defName="Ingest"},Targets=true};meal.playerForced=true;
   Check(PausedAreaWorkFilter.DeniedActivityRule(p,meal)==null,"direct order retains native override");
  }
  {
   var p=Setup(out var s,out var r);var haul=s.PendingWorkJob;haul.Targets=false;haul.Crosses=true;haul.Avoidable=true;
   Check(PausedAreaWorkFilter.MatchingPermittedHaulingRule(p,haul)==null,"avoidable locker shortcut does not require PPE");
   haul.Avoidable=false;Check(PausedAreaWorkFilter.MatchingPermittedHaulingRule(p,haul)==r,"unavoidable haul route still requires PPE");
   haul.Targets=true;haul.Avoidable=true;Check(PausedAreaWorkFilter.MatchingPermittedHaulingRule(p,haul)==r,"pickup inside still requires PPE");
   s.Transition=ApparelTransition.Active;s.PendingWorkJob=null;p.Position=0;p.jobs.curJob=PreparationHandoffTests.Wait();
   Find.TickManager.TicksGame=1000;
   PausedAreaWorkFilter.NotifyPermittedHaulEnded(p,s,haul,JobCondition.Incompletable);
   Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(s,r),"failed haul gains no grace");
   PausedAreaWorkFilter.NotifyPermittedHaulEnded(p,s,haul,JobCondition.Succeeded);Pulse(p,r);
   Check(!s.RecallRequested&&s.PermittedHaulGraceUntilTick==1120,"successful haul keeps outfit during bounded native wait");
   s.PermittedHaulGraceRuleId="other";Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(s,r),"grace cannot transfer to another rule");s.PermittedHaulGraceRuleId=r.Id;
   s.BufferedTasksCompleted=3;Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(s,r),"completed buffer cannot prolong wait");s.BufferedTasksCompleted=2;
   Find.TickManager.TicksGame=1100;PausedAreaWorkFilter.NotifyPermittedHaulEnded(p,s,p.jobs.curJob,JobCondition.Succeeded);
   Check(s.PermittedHaulGraceUntilTick==1120,"wait cannot refresh haul grace");
   Find.TickManager.TicksGame=1121;Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(s,r),"expired grace releases idle outfit");
  }
  {
   var p=Setup(out var s,out var r);var delivery=Haul("HaulToContainer");
   delivery.workGiverDef=new WorkGiverDef{workType=new WorkTypeDef{defName="Construction"}};
   delivery.targetA=new LocalTargetInfo(new Thing{Map=p.Map,Position=1});
   delivery.targetB=new LocalTargetInfo(new Frame{Map=p.Map,Position=0});s.PendingWorkJob=delivery;
   Check(!ActivityJobClassifier.IsHauling(delivery),"construction material delivery retains real activity classification");
   Check(PausedAreaWorkFilter.MatchingPermittedHaulingRule(p,delivery)==r,"exterior construction delivery may prepare source PPE");
   Check(PausedAreaWorkFilter.DeniedActivityRule(p,delivery)==null&&PausedAreaWorkFilter.DeniedPausedAreaRule(p,delivery)==null,"exterior component pickup passes both pause guards");
   delivery.Targets=false;p.Position=0;s.Transition=ApparelTransition.Active;p.jobs.curJob=PreparationHandoffTests.Wait();
   Check(PawnJobTracker_StartJob_Patch.PendingWorkJobIsViable(p,delivery,out var why,out var retry),"material delivery survives pickup and route change");
   var other=new ApparelRule{Id="other",Area=r.Area,Hauling=false};AutomaticOutfitManagerGameComponent.Current.Rules.Add(other);delivery.Targets=true;
   Check(!PawnJobTracker_StartJob_Patch.PendingWorkJobIsViable(p,delivery,out why,out retry),"remembered source cannot bypass a newly blocked destination");AutomaticOutfitManagerGameComponent.Current.Rules.Remove(other);
   delivery.targetB.Thing.Position=1;delivery.Targets=true;
   Check(PausedAreaWorkFilter.DeniedActivityRule(p,delivery)==r&&PausedAreaWorkFilter.DeniedPausedAreaRule(p,delivery)==r,"construction inside paused destination stays blocked");
   delivery.workGiverDef.workType=WorkTypeDefOf.Hauling;
   Check(PausedAreaWorkFilter.DeniedActivityRule(p,delivery)==r&&PausedAreaWorkFilter.DeniedPausedAreaRule(p,delivery)==r,"hauling tag cannot bypass paused construction destination");
   delivery.targetB.Thing.Position=0;delivery.targetQueueB=new List<LocalTargetInfo>{new LocalTargetInfo(new Blueprint{Map=p.Map,Position=1})};
   Check(!PausedAreaWorkFilter.IsPermittedMaterialCollection(p,delivery,r),"queued interior recipient rejected");delivery.targetQueueB=null;
   r.Hauling=false;Check(!PausedAreaWorkFilter.IsPermittedMaterialCollection(p,delivery,r),"material pickup obeys hauling denial");r.Hauling=true;
   p.Child=true;r.Children=false;Check(!PausedAreaWorkFilter.IsPermittedMaterialCollection(p,delivery,r),"material pickup obeys child denial");r.Children=true;
   r.IsNonWork=true;Check(PausedAreaWorkFilter.IsPermittedMaterialCollection(p,delivery,r),"Non-Work source permits exterior hauling under its own access");r.IsNonWork=false;
   delivery.targetB.Thing.Map=new Map();Check(!PausedAreaWorkFilter.IsPermittedMaterialCollection(p,delivery,r),"stale map destination rejected");
  }
  {
   var p=Setup(out var s,out var r);r.WorkAreaPaused=false;r.Wandering=true;
   var exit=new Job{def=AutomaticOutfitManagerJobDefOf.AutomaticOutfitManager_LockerReturn,targetA=new LocalTargetInfo{Cell=0},Crosses=true};
   AutomaticOutfitManager.Detection.UnavailableWorkRegistry.Blocks.Add(r.Id);
   Check(!PausedAreaWorkFilter.MatchesProtectedTransitRule(p,exit,r),"safe shortage exit must not prepare another outfit");
   var other=new ApparelRule{Id="other",WorkAreaPaused=false,Area=r.Area};
   Check(PausedAreaWorkFilter.MatchesProtectedTransitRule(p,exit,other),"exit has no exception through unrelated area");
   p.Position=0;Check(PausedAreaWorkFilter.MatchesProtectedTransitRule(p,exit,r),"shortage exit cannot waive reentry PPE");p.Position=1;
   exit.def=new JobDef{defName="Goto"};Check(PausedAreaWorkFilter.MatchesProtectedTransitRule(p,exit,r),"ordinary transit still needs PPE");
   exit.def=AutomaticOutfitManagerJobDefOf.AutomaticOutfitManager_LockerReturn;
   AutomaticOutfitManager.Detection.UnavailableWorkRegistry.Blocks.Clear();
   Check(PausedAreaWorkFilter.MatchesProtectedTransitRule(p,exit,r),"unrecorded locker travel has no shortage exemption");
  }
  foreach(string kind in new[]{"HaulToCell","HaulToContainer"})
  foreach(string itemKind in new[]{"ingredient","fuel","food","apparel","weapon"}){
   var p=Setup(out var s,out var r);var delivery=Haul(kind);
   delivery.workGiverDef=new WorkGiverDef{workType=new WorkTypeDef{defName="Crafting"}};
   var item=new Thing{Map=p.Map,Position=1,def=new ThingDef{IsApparel=itemKind=="apparel",IsWeapon=itemKind=="weapon"}};
   delivery.targetA=new LocalTargetInfo(item);
   delivery.targetB=kind=="HaulToCell"?new LocalTargetInfo{Cell=0}:new LocalTargetInfo(new Thing{Map=p.Map,Position=0,def=new ThingDef{category=ThingCategory.Building}});
   s.PendingWorkJob=delivery;
   Check(!ActivityJobClassifier.IsHauling(delivery),"supply work-giver classification retained");
   Check(PausedAreaWorkFilter.MatchingPermittedHaulingRule(p,delivery)==r,"native exterior supply transfer admitted: "+kind+" "+itemKind);
   Check(PausedAreaWorkFilter.DeniedActivityRule(p,delivery)==null&&PausedAreaWorkFilter.DeniedPausedAreaRule(p,delivery)==null,"supply transport passes pause guards");
   Check(PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(p,delivery,r),"source boundary admits supply transport for outfit preparation");
   var suit=Step(p,s);var equip=Step(p,s,true);p.jobs.jobQueue.Add(new QueuedJob{job=equip});
   p.jobs.StartJob(suit);Pulse(p,r);Check(!s.RecallRequested,"supply haul PPE remains owned");
   p.jobs.curJob=null;var wait=PreparationHandoffTests.Wait();p.jobs.StartJob(wait);Pulse(p,r);
   Check(s.PendingWorkJob==delivery&&!s.RecallRequested&&p.jobs.jobQueue.Count==1,"supply haul survives native clothing wait");
   p.jobs.AdvanceQueue();s.Transition=ApparelTransition.Active;p.jobs.curJob=wait;Pulse(p,r);
   Check(!s.RecallRequested&&PawnJobTracker_StartJob_Patch.ShouldResumePreparedJob(p,AutomaticOutfitManagerGameComponent.Current,s,wait),"supply haul reaches ready replay");
   item.Position=0;delivery.Targets=false;p.Position=0;
   Check(PawnJobTracker_StartJob_Patch.PendingWorkJobIsViable(p,delivery,out var reason,out var retry),"supply pickup movement retains exact pending job");
   p.Reserved.Add(item);
   Check(!PawnJobTracker_StartJob_Patch.PendingWorkJobIsViable(p,delivery,out reason,out retry)&&retry,"hauling permission cannot bypass saved-item reservation");p.Reserved.Clear();
   PawnJobTracker_StartJob_Patch.CompletePreparedJobAdmission(p,s,delivery);
   p.jobs.NativeFinalizer=PreparationHandoffTests.Wait();p.jobs.StartJob(delivery);Pulse(p,r);
   Check(s.PendingWorkJob==null&&p.jobs.jobQueue.Count==1&&p.jobs.jobQueue[0].job==delivery&&!s.RecallRequested,"supply native finalizer retains a single continuation");
   p.jobs.AdvanceQueue();Pulse(p,r);
   Check(p.jobs.curJob==delivery&&!s.RecallRequested&&p.jobs.Rejections==0,"supply delivery starts without outfit restart");
  }
  {
   var p=Setup(out var s,out var r);var delivery=Haul();s.PendingWorkJob=delivery;
   delivery.workGiverDef=new WorkGiverDef{workType=new WorkTypeDef{defName="Crafting"}};
   delivery.targetA=new LocalTargetInfo(new Thing{Map=p.Map,Position=1});delivery.targetB=new LocalTargetInfo{Cell=0};
   var queued=new Thing{Map=p.Map,Position=1};delivery.targetQueueA=new List<LocalTargetInfo>{new LocalTargetInfo(queued)};
   Check(PausedAreaWorkFilter.IsPermittedMaterialCollection(p,delivery,r),"valid extra supply pickups retained");
   queued.Map=new Map();Check(!PausedAreaWorkFilter.IsPermittedMaterialCollection(p,delivery,r),"cross-map supply queue rejected");queued.Map=p.Map;
   queued.Destroyed=true;Check(!PausedAreaWorkFilter.IsPermittedMaterialCollection(p,delivery,r),"destroyed queued supplies rejected");queued.Destroyed=false;
   delivery.targetQueueB=new List<LocalTargetInfo>{new LocalTargetInfo{Cell=1}};
   Check(PausedAreaWorkFilter.DeniedPausedAreaRule(p,delivery)==r,"exterior exception cannot hide interior queued delivery");delivery.targetQueueB=null;
   delivery.targetB=new LocalTargetInfo{Cell=1};
   Check(!PausedAreaWorkFilter.IsPermittedMaterialCollection(p,delivery,r),"new supply exception remains exterior only");
   delivery.workGiverDef.workType=WorkTypeDefOf.Hauling;
   Check(PausedAreaWorkFilter.DeniedActivityRule(p,delivery)==null&&PausedAreaWorkFilter.DeniedPausedAreaRule(p,delivery)==null,"ordinary permitted interior storage restock unchanged");
   delivery.def=JobDefOf.HaulToContainer;delivery.targetB=new LocalTargetInfo(new Thing{Map=p.Map,Position=0});
   delivery.targetQueueB=new List<LocalTargetInfo>{new LocalTargetInfo(new Frame{Map=p.Map,Position=1})};
   Check(PausedAreaWorkFilter.DeniedActivityRule(p,delivery)==r&&PausedAreaWorkFilter.DeniedPausedAreaRule(p,delivery)==r,"mixed recipient queue cannot deliver paused construction");
   delivery.targetQueueB=null;delivery.def=JobDefOf.HaulToCell;delivery.targetB=new LocalTargetInfo{Cell=0};
   delivery.def=JobDefOf.HaulToContainer;var recipient=new Frame{Map=p.Map,Position=0,Footprint=new List<IntVec3>{0,1}};delivery.targetB=new LocalTargetInfo(recipient);
   Check(!PausedAreaWorkFilter.IsPermittedMaterialCollection(p,delivery,r)&&PausedAreaWorkFilter.DeniedPausedAreaRule(p,delivery)==r,"recipient footprint cannot overlap paused construction");
   delivery.def=JobDefOf.HaulToCell;delivery.targetB=new LocalTargetInfo{Cell=0};
   delivery.targetA.Thing.def.category=ThingCategory.Building;
   Check(!PausedAreaWorkFilter.IsPermittedMaterialCollection(p,delivery,r),"building target is not an item transport");delivery.targetA.Thing.def.category=ThingCategory.Item;
   var nativeDriver=delivery.def.driverClass;delivery.def.driverClass=typeof(JobDriver_Refuel);
   Check(!PausedAreaWorkFilter.IsMaterialDeliveryJob(delivery),"rewritten driver cannot impersonate transport");delivery.def.driverClass=nativeDriver;
   r.Hauling=false;Check(!PausedAreaWorkFilter.IsPermittedMaterialCollection(p,delivery,r),"broad supply hauling honors revoked hauling");r.Hauling=true;
   s.RecallRequested=true;Check(!PausedAreaWorkFilter.HasPermittedPendingHaul(p,delivery),"explicit recall still cancels supply continuation");
  }
  foreach(string activity in new[]{"DoBill","Refuel","RefuelAtomic","RearmTurret","RearmTurretAtomic","FixBrokenDownBuilding","Repair","Ingest","Wear","Equip","RemoveApparel"}){
   var p=Setup(out var s,out var r);s.Transition=ApparelTransition.Active;s.PendingWorkJob=null;
   var job=new Job{def=new JobDef{defName=activity},workGiverDef=new WorkGiverDef{workType=WorkTypeDefOf.Hauling},Targets=true};p.jobs.curJob=job;
   Check(ActivityJobClassifier.IsHauling(job),"activity keeps its native work priority: "+activity);
   Check(!PausedAreaWorkFilter.IsHaulingOnlyJob(job),"supply use is not transport: "+activity);
   Check(PausedAreaWorkFilter.DeniedActivityRule(p,job)==r&&PausedAreaWorkFilter.DeniedPausedAreaRule(p,job)==r,"hauling priority cannot reopen paused supply use: "+activity);
   Check(!PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(p,job,r),"paused boundary rejects supply use activity");
   Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(s,r),"supply use cannot retain hauling pause exception");
   job.playerForced=true;Check(PausedAreaWorkFilter.DeniedActivityRule(p,job)==null&&PausedAreaWorkFilter.DeniedPausedAreaRule(p,job)==null,"explicit native order preserved");
  }
  foreach(string priority in new[]{"Hauling","FSFHauling","Crafting"}){
   var p=Setup(out var s,out var r);var item=new Thing{Map=p.Map,Position=1};
   var scanner=new WorkGiver_HaulGeneral{def=new WorkGiverDef{workType=priority=="Hauling"?WorkTypeDefOf.Hauling:new WorkTypeDef{defName=priority}},Candidate=Haul()};
   scanner.Candidate.targetA=new LocalTargetInfo(item);scanner.Candidate.targetB=new LocalTargetInfo{Cell=0};
   Check(scanner.HasJobOnThing(p,item),"native scanner accepts paused supply pickup: "+priority);
   var job=scanner.JobOnThing(p,item);
   Check(job==scanner.Candidate&&scanner.NativeCreates==1,"native HasJob/JobOnThing agree on exact haul");
   r.Hauling=false;Check(!scanner.HasJobOnThing(p,item)&&scanner.JobOnThing(p,item)==null,"both scanner callbacks reject revoked hauling");r.Hauling=true;
   p.Child=true;r.Children=false;Check(!scanner.HasJobOnThing(p,item)&&scanner.JobOnThing(p,item)==null,"both scanner callbacks enforce child access");r.Children=true;p.Child=false;
   p.Reserved.Add(item);Check(!scanner.HasJobOnThing(p,item),"native saved-item reservation prevents candidate acceptance");p.Reserved.Clear();
   var frame=new Frame{Map=p.Map,Position=1,def=new ThingDef{category=ThingCategory.Building}};
   Check(!scanner.HasJobOnThing(p,frame)&&scanner.JobOnThing(p,frame)==null,"hauling scanner cannot reopen paused construction worksite");
   r.Hauling=false;Check(scanner.HasJobOnThing(p,item,true)&&scanner.JobOnThing(p,item,true)==scanner.Candidate,"forced scanner query preserves native control");
  }
  {
   var p=Setup(out var s,out var r);var target=new Thing{Map=p.Map,Position=1,def=new ThingDef{category=ThingCategory.Building}};
   var scanner=new WorkGiver_Refuel{def=new WorkGiverDef{workType=WorkTypeDefOf.Hauling},Candidate=new Job{def=new JobDef{defName="RearmTurret"}}};
   Check(!scanner.HasJobOnThing(p,target)&&scanner.JobOnThing(p,target)==null,"native rearm scan stays blocked while paused");
   r.WorkAreaPaused=false;r.Activities=false;
   Check(!scanner.HasJobOnThing(p,target)&&scanner.JobOnThing(p,target)==null,"rearm scanner uses Activities permission before job creation");
   r.Activities=true;Check(scanner.HasJobOnThing(p,target)&&scanner.JobOnThing(p,target)==scanner.Candidate,"unpaused permitted refueling remains native");
  }
  {
   var p=Setup(out var s,out var r);var item=new Thing{Map=p.Map,Position=1};
   var scanner=new WorkGiver_Scanner{def=new WorkGiverDef{workType=new WorkTypeDef{defName="FSFHauling"}},Candidate=Haul()};
   Check(scanner.HasJobOnThing(p,item)&&scanner.JobOnThing(p,item)==scanner.Candidate,"FSF hauling scanner preserves transport permission without type-name fallback");
  }
  RestAndSupplyCases(); HaulRecoveryCases(); PauseControlCases();
  Console.WriteLine("Passed "+checks+" paused hauling/restoration checks.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
