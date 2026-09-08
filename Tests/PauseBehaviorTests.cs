using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using Verse;
using Verse.AI;
using RimWorld;

class PauseBehaviorTests
{
    static int count;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); count++; }
    static int Main()
    {
        try {
            var map = new Map();
            var rule = new ApparelRule { Area = new Area(map, 4), Enabled = true, WorkAreaPaused = true };
            var c = new AutomaticOutfitManagerGameComponent();
            AutomaticOutfitManagerGameComponent.Current = c; c.Rules.Add(rule);
            var p = new Pawn { Map = map, Position = 4 }; p.jobs = new Pawn_JobTracker();
            foreach (string group in new[] { "colonist", "guest", "slave", "prisoner" }) {
                p.Group = group;
                foreach (JobDef activity in new[] { JobDefOf.Ingest, JobDefOf.Recreation }) {
                    var j = new Job { def = activity, targetA = 4 }; p.jobs.curJob = j;
                    Check(PausedAreaWorkFilter.DeniedActivityRule(p, j) == rule, "paused native occupant must be denied: " + group);
                    j.playerForced = true;
                    Check(PausedAreaWorkFilter.DeniedActivityRule(p, j) == null, "direct order preserved");
                }
            }
            Check(PausedAreaWorkFilter.DeniedActivityRule(p, new Job { def=JobDefOf.Haul, targetA=4 }) == null, "haul retains separate controls");
            Check(PausedAreaWorkFilter.DeniedActivityRule(p, new Job { def=JobDefOf.Goto, targetA=4 }) == null, "wandering retains separate controls");
            Check(PausedAreaWorkFilter.DeniedActivityRule(p, new Job { def=JobDefOf.LayDown, targetA=4 }) == null, "essential rest retained");
            rule.WorkAreaPaused = false;
            Check(PausedAreaWorkFilter.DeniedActivityRule(p, new Job { def=JobDefOf.Ingest, targetA=4 }) == null, "resume reopens meals");
            rule.ActivitiesAllowed = false;
            Check(PausedAreaWorkFilter.DeniedActivityRule(p, new Job { def=JobDefOf.Ingest, targetA=4 }) == rule, "category still enforced after resume");
            rule.ActivitiesAllowed = true; rule.WorkAreaPaused = true;

            var food = new Thing(); p.Position=1; p.carryTracker.CarriedThing=food;
            p.jobs.curJob = new Job { def=JobDefOf.Ingest, targetA=food };
            IntVec3 spot=4; bool found=true;
            ToilsIngest_AccessDiningSpot_Patch.Postfix(p, ref spot, ref found);
            Check(found && spot==p.Position, "native closed chair corrected before path");
            LocalTargetInfo path=4;
            PausedMealDestination.BeforePath(p, ref path, PathEndMode.OnCell);
            Check(path.Cell==p.Position && p.carryTracker.CarriedThing==food, "pause after chair selection retains exact carried meal");
            path=4;
            PausedMealDestination.BeforePath(p, ref path, PathEndMode.Touch);
            Check(path.Cell==4, "food pickup and unrelated travel untouched");
            p.jobs.curJob.playerForced=true; spot=4;
            Check(!PausedMealDestination.Redirect(p,p.CurJob,ref spot) && spot==4, "forced meal destination unchanged");
            p.jobs.curJob.playerForced=false;
            rule.WorkAreaPaused=false; spot=4;
            Check(!PausedMealDestination.Redirect(p,p.CurJob,ref spot) && spot==4, "active room keeps native chair");
            rule.WorkAreaPaused=true; p.Position=4;
            GenRadial.Cells=new[] { new IntVec3(4), new IntVec3(5), new IntVec3(1) };
            var other=new ApparelRule { Enabled=true, Area=new Area(map,5), WorkAreaPaused=true }; c.Rules.Add(other);
            spot=4;
            Check(PausedMealDestination.Redirect(p,p.CurJob,ref spot) && spot==1, "alternative avoids both denied destinations");
            p.IsPrisoner=true; spot=4;
            Check(PausedMealDestination.Redirect(p,p.CurJob,ref spot) && spot==4, "confined prisoner not routed out of custody");
            p.IsPrisoner=false;
            ProtectedPathAvoidance.Fail=true; spot=4;
            Check(PausedMealDestination.Redirect(p,p.CurJob,ref spot) && spot==4, "no route yields in place for scheduled exit without ending a toil");
            ProtectedPathAvoidance.Fail=false;

            foreach (ApparelTransition next in new[] { ApparelTransition.ReturningToChangingArea, ApparelTransition.Restoring }) {
                var state=new PawnApparelState { Pawn=p, RecallInterruptPending=true, Transition=ApparelTransition.Active };
                c.PawnStates.Clear(); c.PawnStates.Add(state); p.jobs.curJob=new Job();
                p.jobs.OnEnd=()=> { state.Transition=next; p.jobs.curJob=new Job { def=JobDefOf.Goto }; };
                c.RunRecall(1000);
                Check(c.StateFor(p)==state && !state.RecallInterruptPending, "native callback return keeps owning state");
            }
            var oldState=new PawnApparelState { Pawn=p, RecallInterruptPending=true, Transition=ApparelTransition.Active };
            c.PawnStates.Clear(); c.PawnStates.Add(oldState); p.jobs.curJob=new Job();
            var replacement=new PawnApparelState { Pawn=p };
            p.jobs.OnEnd=()=> { c.PawnStates.Clear(); c.PawnStates.Add(replacement); };
            c.RunRecall(2000);
            Check(c.StateFor(p)==replacement, "native callback replacement state not cleared by stale recall");
            c.PawnStates.Clear(); oldState.RecallInterruptPending=true; oldState.LastRecallInterruptAttemptTick=-1; c.PawnStates.Add(oldState);
            p.jobs.curJob=new Job(); p.jobs.OnEnd=()=>p.jobs.curJob=null;
            c.RunRecall(3000);
            Check(c.StateFor(p)==null, "completed tracked-only recall still clears");

            foreach (string control in new[] { "mental", "downed", "social", "emergency" }) {
                var deferred = new PawnApparelState { Pawn=p, RecallInterruptPending=true, Transition=ApparelTransition.Active };
                c.PawnStates.Clear(); c.PawnStates.Add(deferred);
                p.InMentalState=control=="mental"; p.Downed=control=="downed";
                var nativeJob=new Job { def=new JobDef { defName=control=="social" ? "SocialFight" : control=="emergency" ? "FleeAndCower" : "Ingest" } };
                p.jobs.curJob=nativeJob; p.jobs.OnEnd=()=> { throw new Exception("recall interrupted native " + control); };
                c.RunRecall(4000);
                Check(p.CurJob==nativeJob && c.StateFor(p)==deferred,"recall defers to native control: " + control);
            }
            p.InMentalState=false; p.Downed=false; p.jobs.OnEnd=null; c.PawnStates.Clear();

            p.Position=4; p.jobs.curJob=new Job { def=JobDefOf.Ingest, targetA=4 };
            GenRadial.Cells=new[] { new IntVec3(4), new IntVec3(1) };
            GenRadial.NearBoundary=true;
            Check(PausedAreaWorkFilter.TryMakeAccessExitJob(p,p.CurJob,out Job exit) && exit.targetA.Cell==1,
                "narrow corridor exit works without four-cell clearance");
            Check(PausedAreaWorkFilter.IsOwnedAccessEgress(p,exit,rule), "owned exit allowed inside source");
            Check(!PausedAreaWorkFilter.IsOwnedAccessEgress(p,new Job{def=JobDefOf.Goto,targetA=1},rule), "ordinary goto has no owned exit permission");
            p.Position=1;
            Check(!PausedAreaWorkFilter.IsOwnedAccessEgress(p,exit,rule), "exit ownership cannot grant reentry from outside");
            p.Position=4; exit.targetA=4;
            Check(!PausedAreaWorkFilter.IsOwnedAccessEgress(p,exit,rule), "interior destination is not egress");
            p.IsPrisoner=true;
            Check(!PausedAreaWorkFilter.TryMakeAccessExitJob(p,p.CurJob,out exit), "narrow exit preserves prisoner room confinement");
            p.IsPrisoner=false; rule.Id="dining"; rule.WorkAreaPaused=false;
            c.Rules.Clear(); c.Rules.Add(rule); c.PawnStates.Clear();
            p.jobs.curJob=new Job { def=new JobDef {defName="Floordrawing"}, targetA=4 };
            Check(c.CanRecallObservedActivity(p,rule), "observed child learning offers recall");
            var learning=p.CurJob;
            NonWorkBufferTracker.Buffer=new RetainedBuffer {RuleId=rule.Id};
            p.jobs.OnEnd=()=>Check(NonWorkBufferTracker.For(p)==null,"buffer cleared before interrupted native callback");
            Check(c.RecallObservedActivity(p,rule),"observed learning recall accepted");
            NonWorkBufferTracker.Refresh(p); // Native StartJob postfix, pawn still inside.
            Check(NonWorkBufferTracker.For(p)==null,"exit callback must not reenroll cleared buffer");
            Check(p.CurJob.def==JobDefOf.Goto && p.CurJob.targetA.Cell==1 && AccessExitJobs.IsOwned(p,p.CurJob),"observed recall starts owned exterior exit");
            Check(c.StateFor(p)==null,"observed recall does not invent outfit state or snapshot");
            Check(p.jobs.LastCondition==JobCondition.InterruptForced,"interrupted learning cannot earn success credit");
            Check(!c.CanRecallObservedActivity(p,rule),"exit itself cannot be recalled repeatedly");
            p.jobs.OnEnd=null; p.jobs.curJob=learning;
            GenRadial.Cells=new[]{new IntVec3(4)};
            NonWorkBufferTracker.Buffer=new RetainedBuffer {RuleId=rule.Id};
            Check(!c.RecallObservedActivity(p,rule) && p.CurJob==learning && NonWorkBufferTracker.For(p)!=null,"no safe exit preserves current job and buffer");
            GenRadial.Cells=new[]{new IntVec3(4),new IntVec3(1),new IntVec3(0)};
            var protectedOther=new ApparelRule {Id="other",Enabled=true,Area=new Area(map,1)}; c.Rules.Add(protectedOther);
            Check(PausedAreaWorkFilter.TryMakeAccessExitJob(p,p.CurJob,out exit,rule) && exit.targetA.Cell==0,"individual recall avoids unrelated active area");
            c.Rules.Remove(protectedOther);
            var owned=new PawnApparelState {Pawn=p,ActiveRuleId=rule.Id,Transition=ApparelTransition.Active}; c.PawnStates.Add(owned);
            Check(c.RecallObservedActivity(p,rule) && owned.RecallInterruptPending && p.CurJob==learning,"owned outfit delegates to existing recall without new exit");
            owned.ActiveRuleId="other";
            Check(!c.RecallObservedActivity(p,rule),"visited rule cannot recall unrelated owned session");
            owned.ActiveRuleId=rule.Id; owned.Transition=ApparelTransition.Restoring;
            Check(!c.RecallObservedActivity(p,rule),"borrowed restoration cannot be interrupted"); c.PawnStates.Clear();
            p.jobs.curJob.playerForced=true;
            Check(!c.CanRecallObservedActivity(p,rule),"forced activity preserved"); p.jobs.curJob.playerForced=false;
            p.Drafted=true; Check(!c.CanRecallObservedActivity(p,rule),"drafted activity preserved"); p.Drafted=false;
            p.Downed=true; Check(!c.CanRecallObservedActivity(p,rule),"downed pawn preserved"); p.Downed=false;
            p.InMentalState=true; Check(!c.CanRecallObservedActivity(p,rule),"mental state preserved"); p.InMentalState=false;
            p.carryTracker.CarriedThing=new Pawn();
            Check(!c.CanRecallObservedActivity(p,rule),"carried child placement preserved"); p.carryTracker.CarriedThing=null;
            foreach(string care in new[]{"TendPatient","FeedPatient","Breastfeed","BringBabyToSafety","Rescue","GiveBirth"}) {
                p.jobs.curJob=new Job {def=new JobDef {defName=care},targetA=4};
                Check(!c.CanRecallObservedActivity(p,rule),"essential care preserved: "+care);
            }
            p.jobs.curJob=new Job {def=JobDefOf.LayDown,targetA=4};
            Check(!c.CanRecallObservedActivity(p,rule),"rest preserved");
            p.RaceProps.Humanlike=false; p.jobs.curJob=learning;
            Check(!c.CanRecallObservedActivity(p,rule),"nonhuman not promoted to observed recall"); p.RaceProps.Humanlike=true;
            foreach(string activity in new[]{"Ingest","Recreation","Floordrawing"}) {
                p.jobs.curJob=new Job {def=new JobDef {defName=activity},targetA=4};
                Check(c.CanRecallObservedActivity(p,rule),"purposeful activity recall: "+activity);
            }
            p.Position=1; p.jobs.curJob.targetA=1;
            Check(c.RecallObservedActivity(p,rule) && NonWorkBufferTracker.For(p)==null,"outside retained task interrupted without extra travel");
            Console.WriteLine("Passed " + count + " pause behavior checks."); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e.Message); return 1; }
    }
}
namespace Verse {
 public class Map {}
 public struct IntVec3 : IEquatable<IntVec3> {
  public int X; public IntVec3(int x){X=x;} public bool IsValid=>X>=0;
  public bool InBounds(Map m)=>IsValid; public bool Standable(Map m)=>true;
  public bool IsForbidden(Pawn p)=>false; public int GetRoom(Map m)=>X==4?1:2;
  public static IntVec3 Invalid=>new IntVec3(-1); public int DistanceToSquared(IntVec3 c)=>(X-c.X)*(X-c.X);
  public bool Equals(IntVec3 o)=>X==o.X; public override bool Equals(object o)=>o is IntVec3 c&&Equals(c);
  public override int GetHashCode()=>X; public static implicit operator IntVec3(int x)=>new IntVec3(x);
  public static bool operator==(IntVec3 a,IntVec3 b)=>a.Equals(b); public static bool operator!=(IntVec3 a,IntVec3 b)=>!a.Equals(b);
 }
 public class Area { public Map Map; HashSet<IntVec3> cells; public Area(Map m,params int[] xs){Map=m;cells=new HashSet<IntVec3>(xs.Select(x=>(IntVec3)x));}public bool this[IntVec3 c]=>cells.Contains(c); }
 public static class GenRadial { public static IntVec3[] Cells; public static bool NearBoundary; public static IEnumerable<IntVec3> RadialCellsAround(IntVec3 p,float r,bool center)=>NearBoundary&&r<4f?new[]{new IntVec3(4)}:Cells??new[]{p}; }
 public static class Extensions { public static IEnumerable<T> InRandomOrder<T>(this IEnumerable<T> xs)=>xs; }
 public class Thing {public Map MapHeld;}
 public class RaceProperties {public bool Humanlike=true;}
 public class Pawn : Thing { public Map Map; public RaceProperties RaceProps=new RaceProperties(); public IntVec3 Position; public string Group; public bool Dead,Drafted,Downed,InMentalState,IsPrisoner; public bool Spawned=true; public Verse.AI.Pawn_JobTracker jobs; public Verse.AI.Job CurJob=>jobs?.curJob; public Carry carryTracker=new Carry(); public int GetRoom()=>Position.GetRoom(Map); public bool CanReserveSittableOrSpot(IntVec3 c)=>true; public bool CanReach(IntVec3 c,Verse.AI.PathEndMode m,Danger d)=>true; }
 public static class Messages {public static void Message(string s,Pawn p,object type,bool historical){}}
 public static class Find {public static TickManager TickManager=new TickManager();} public class TickManager {public int TicksGame;}
 public class Carry { public Thing CarriedThing; } public enum Danger {Some}
 public struct LocalTargetInfo { public Thing Thing; IntVec3 cell; public bool HasThing=>Thing!=null; public IntVec3 Cell=>cell; public bool IsValid=>HasThing||cell.IsValid; public static implicit operator LocalTargetInfo(IntVec3 c)=>new LocalTargetInfo{cell=c}; public static implicit operator LocalTargetInfo(int c)=>(IntVec3)c;public static implicit operator LocalTargetInfo(Thing t)=>new LocalTargetInfo{Thing=t}; }
}
namespace Verse.AI {
 public class JobDef {public Type driverClass;public string defName;}
 public class Job {public JobDef def;public bool playerForced;public LocalTargetInfo targetA;public ThinkNode jobGiver;public int expiryInterval;public int loadID=1;}
 public static class JobMaker {public static Job MakeJob(JobDef d,IntVec3 c)=>new Job {def=d,targetA=c};}
 public class ThinkNode {}
 public class Pawn_JobTracker {public Job curJob; public Action OnEnd;public JobCondition LastCondition; public void StartJob(Job job,JobCondition c){EndCurrentJob(c,false);curJob=job;} public void EndCurrentJob(JobCondition c,bool start){LastCondition=c;OnEnd?.Invoke();}}
 public class Pawn_PathFollower {public Pawn pawn;}
 public enum PathEndMode {OnCell,Touch} public enum JobCondition {InterruptForced}
}
namespace RimWorld { public class JobDriver_Ingest {} public class Toils_Ingest {} public static class JobDefOf {public static JobDef Ingest=new JobDef{driverClass=typeof(JobDriver_Ingest)},Recreation=new JobDef(),LayDown=new JobDef(),Goto=new JobDef(),Haul=new JobDef();} }
namespace HarmonyLib {
 public class HarmonyPatch:Attribute {public HarmonyPatch(Type t,string n){}} public class HarmonyPriority:Attribute {public HarmonyPriority(int n){}} public static class Priority {public const int Last=0,First=800;}
 public static class AccessTools {public delegate U FieldRef<T,U>(T value);public static FieldRef<T,U> FieldRefAccess<T,U>(string n)=>v=>default(U);}
}
namespace RimWorld {public static class MessageTypeDefOf {public static object RejectInput;}}
namespace AutomaticOutfitManager.Rules {public class ApparelRule {public string Id;public Area Area;public bool Enabled,WorkAreaPaused; public bool ActivitiesAllowed=true,IsNonWork=true;public int ReturnTaskBuffer=2;}}
namespace AutomaticOutfitManager.State {public class NestedBuffer {public string RuleId;} public enum ApparelTransition {Active,ReturningToChangingArea,Restoring} public class PawnApparelState {public string ActiveRuleId,NonWorkRestorationRuleId;public List<string> CurrentRuleIds;public List<NestedBuffer> NestedRuleBuffers;public Pawn Pawn; public ApparelTransition Transition;public bool RecallRequested,RecallInterruptPending,ApparelInterventionActive,WeaponInterventionActive;public int LastRecallInterruptAttemptTick=-1;}}
namespace AutomaticOutfitManager.Core {
 public partial class AutomaticOutfitManagerGameComponent {
  public static AutomaticOutfitManagerGameComponent Current;public List<ApparelRule> Rules=new List<ApparelRule>();public List<PawnApparelState> PawnStates=new List<PawnApparelState>();
  public PawnApparelState StateFor(Pawn p)=>PawnStates.FirstOrDefault(s=>s.Pawn==p);
  public ApparelRule RuleById(string id)=>Rules.FirstOrDefault(r=>r.Id==id);
  public void EndIntervention(Pawn p)=>PawnStates.RemoveAll(s=>s.Pawn==p);
  public void RunRecall(int tick)=>ProcessPendingRecallInterrupts(tick);
  bool UpdateNativeRuleSuspension(Pawn p,Job j)=>NativeRuleControl.MentalOrEmergency(p,j);
  public void RequestRecall(PawnApparelState state){state.RecallInterruptPending=true;}
  bool TryJobTransition(Pawn p,int tick,string why,Action a){a();return true;}
  bool IsAssignedApparelTransitionJob(PawnApparelState s,Job j)=>false;
  bool IsAssignedWeaponTransitionJob(PawnApparelState s,Job j)=>false;
 }
}
namespace AutomaticOutfitManager.Detection {
 public static class PawnAccessClassifier {public static bool IsNativeCustodyEscapeActive(Pawn p)=>false;public static bool IsApparelEligibleHuman(Pawn p)=>true;}
 public static class RuleEvaluator {public static IEnumerable<ApparelRule> ActiveRulesForMap(Map m)=>EnabledRulesForMap(m).Where(r=>!r.WorkAreaPaused);public static IEnumerable<ApparelRule> MatchingRules(Pawn p,Job j)=>EnabledRulesForMap(p.Map).Where(r=>JobTargetsArea(j,r.Area));public static IReadOnlyList<ApparelRule> EnabledRulesForMap(Map m)=>AutomaticOutfitManagerGameComponent.Current.Rules.Where(r=>r.Enabled&&r.Area.Map==m).ToList();public static bool JobTargetsArea(Job j,Area a)=>a[j.targetA.Cell];public static bool HasMissingRequiredGear(Pawn p,ApparelRule r)=>false;}
}
namespace AutomaticOutfitManager.Patches {
 // Animal nursing is exercised with the production policy in PauseControlTests.
 internal static class AnimalNursingPolicy {internal static bool Allowed(Pawn p,Job j,ApparelRule r)=>false;}
 // Rest continuation is exercised with the production policy in RestAndSupplyTests.
 internal static class RestActivityPolicy {internal static bool Preserves(PawnApparelState s,ApparelRule r,Job j)=>false;}
 public class RetainedBuffer {public string RuleId;public Job PendingWork;public Map Map;public int HandoffStartedTick,LastStartedJobId;public bool OutfitUnchanged()=>true;public bool Start(int jobId,int limit,bool own,bool compatible,bool countable)=>true;}
 public static partial class NonWorkBufferTracker {static AutomaticOutfitManagerGameComponent Component=>AutomaticOutfitManagerGameComponent.Current;public static RetainedBuffer Buffer;public static RetainedBuffer For(Pawn p)=>Buffer;public static void Clear(Pawn p){Buffer=null;}static bool NativeOverride(Pawn p,Job j)=>false;static void Begin(Pawn p,ApparelRule r){Buffer=new RetainedBuffer {RuleId=r.Id,Map=p.Map};}}
 public enum NonWorkMealStage {Eating} public class NonWorkMealTrip {public NonWorkMealStage Stage;public string DestinationRuleId;}public static class NonWorkMealHandoff {public static NonWorkMealTrip For(Pawn p)=>null;}
 public static partial class PawnJobTracker_StartJob_Patch {public static bool IsBufferableJob(Job j)=>j.def!=JobDefOf.Goto;public static bool CanCountBufferedTask(Pawn p,Job j)=>!p.Drafted&&!p.Downed&&!p.InMentalState&&!j.playerForced&&IsBufferableJob(j);}
 public static class ChildcareContinuation {public static bool Admit(Pawn p,Job j)=>false;}
 public static class ProtectedPathAvoidance {public static bool Fail;public static bool SegmentAvoidsRules(Pawn p,IntVec3 start,LocalTargetInfo end,List<ApparelRule> rules)=>!Fail&&!rules.Any(r=>r.Area[end.Cell]);public static bool JobPathCrossesArea(Pawn p,Job j,Area a)=>false;public static bool RouteRequiresRestrictedArea(Pawn p,Job j,List<ApparelRule> rs)=>false;}
 public static partial class PausedAreaWorkFilter {
  // This fixture covers meals/recall; typed material collection is exercised
  // with production classification in PausedHaulTests.
  static bool IsMaterialDeliveryJob(Job j)=>false;
  static bool IsConstructionMaterialDelivery(Job j)=>false;
  static bool IsHaulingOnlyJob(Job j)=>IsHaulingJob(j);
  static bool IsPermittedMaterialCollection(Pawn p,Job j,ApparelRule r)=>false;
  // Owned-haul continuations are exercised with production queue ownership in PausedHaulTests.
  static bool IsPermittedHaulingContinuation(PawnApparelState s,ApparelRule r,Job j)=>false;
  static bool IsManagedPawn(Pawn p)=>true;static bool HasNativeActivityOverride(Pawn p,Job j)=>j.playerForced||p.Drafted||p.Downed||p.InMentalState;
  static bool ContainsIgnoreCase(string s,string term)=>s.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0;
  static bool IsHaulingJob(Job j)=>j.def==JobDefOf.Haul;static bool IsRestrictedRoamingJob(Pawn p,Job j,ThinkNode n)=>j.def==JobDefOf.Goto;
  static bool WorkAllowedFor(ApparelRule r,Pawn p)=>r.ActivitiesAllowed;public static bool IsEssentialPersonalJob(Job j)=>j.def==JobDefOf.LayDown;
  static bool ShouldAllowEssentialActivityFallback(Pawn p,Job j,List<ApparelRule> rs)=>false;
  static bool IsPrisoner(Pawn p)=>p.IsPrisoner;static bool PathCrossesArea(Pawn p,IntVec3 a,IntVec3 b,Area r)=>r[b];static LocalTargetInfo TransitDestinationFor(Job j)=>j.targetA;
  public static bool ActivityAllowedAtRuleBoundary(Pawn p,Job j,ApparelRule r)=>!ActivityRestrictedFor(r,p,j);
 }
 public static class AccessExitJobs {static HashSet<Job> owned=new HashSet<Job>();public static void Mark(Pawn p,Job j)=>owned.Add(j);public static bool IsOwned(Pawn p,Job j)=>owned.Contains(j);}
}
