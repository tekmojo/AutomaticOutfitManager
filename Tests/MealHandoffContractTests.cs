// Compiles the production handoff controller and save record against a small
// deterministic world. Native pathfinding/animation still require a game test.
using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.UI;
using RimWorld;
using Verse;
using Verse.AI;
partial class MealHandoffContractTests
{
 static int passed;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);passed++;}
 static AutomaticOutfitManagerGameComponent c;
 static Pawn p;
 static ApparelRule source,dest;
 static Thing food;
 static Apparel shirt,armor;
 static void Setup(bool fallback=false){
  c=new AutomaticOutfitManagerGameComponent();AutomaticOutfitManagerGameComponent.Current=c;
  p=new Pawn{Map=new Map(),Position=1};p.jobs.pawn=p;p.carryTracker.pawn=p;
  source=new ApparelRule{Id="source",Area=new Area(p.Map,1),ChangingArea=new Area(p.Map,2)};
  dest=new ApparelRule{Id="destination",IsNonWork=true,Area=new Area(p.Map,4),DefaultToSavedPersonalOutfit=!fallback};
  c.Rules.AddRange(new[]{source,dest,new ApparelRule{Id="otherWork",Area=new Area(p.Map,5)}});
  armor=new Apparel{def=new ThingDef{apparel=true},Map=p.Map,Position=1};p.apparel.WornApparel.Add(armor);
  shirt=new Apparel{def=new ThingDef{apparel=true},Map=p.Map,Position=3};
  c.Saved=new SavedNonWorkOutfit{Pawn=p,Apparel=new List<Apparel>{shirt}};
  c.Target=fallback?new SavedNonWorkOutfit{Pawn=p}:c.Saved;
  c.State=new PawnApparelState{Pawn=p,ActiveRuleId="source",Transition=ApparelTransition.Active};
  food=new Thing{Map=p.Map,Position=1,stackCount=2};p.carryTracker.innerContainer.Add(food);
  p.jobs.curJob=JobMaker.MakeJob(JobDefOf.Ingest,food);p.CurJob.count=2;
  HazardousEnvironmentSafety.Hazard=false;ProtectedPathAvoidance.Fail=false;ApparelFinder.Candidate=shirt;Find.TickManager.TicksGame=0;
 }
 static void BufferTests(){
  Setup();CompleteReturn();p.Position=4;dest.ReturnTaskBuffer=3;p.jobs.curJob=JobMaker.MakeJob(JobDefOf.Ingest,new LocalTargetInfo(4));
  NonWorkBufferTracker.Refresh(p);var b=NonWorkBufferTracker.For(p);
  Check(b!=null&&c.State==null&&b.OutfitUnchanged(),"restored personal occupant gets retention without a managed snapshot");
  var task=JobMaker.MakeJob(JobDefOf.HaulToCell,new LocalTargetInfo(6));p.jobs.curJob=task;NonWorkBufferTracker.Refresh(p);
  Check(b.PendingJobId==task.loadID&&b.Completed==0,"actual outside haul starts an uncompleted personal-outfit candidate");
  NonWorkBufferTracker.End(p,task,JobCondition.Incompletable);Check(b.Completed==0,"failed outside task receives no credit through controller");
  task=JobMaker.MakeJob(JobDefOf.HaulToCell,new LocalTargetInfo(6));p.jobs.curJob=task;NonWorkBufferTracker.Refresh(p);NonWorkBufferTracker.End(p,task,JobCondition.Succeeded);
  Check(b.Completed==1&&p.apparel.WornApparel.Single()==shirt,"successful outside task keeps exact personal garment");
  task=JobMaker.MakeJob(JobDefOf.HaulToCell,new LocalTargetInfo(6));p.jobs.curJob=task;NonWorkBufferTracker.Refresh(p);NonWorkBufferTracker.End(p,task,JobCondition.Incompletable);
  NonWorkBufferTracker.Refresh(p);Check(b.PendingJobId==task.loadID,"restarted exact failed job can become a fresh candidate");
  Scribe.Values.Clear();b.ExposeData();Scribe.Loading=true;var restored=new NonWorkOutfitBuffer();restored.ExposeData();Scribe.Loading=false;
  Check(restored.Completed==1&&restored.RuleId==dest.Id&&restored.Apparel.Single()==shirt,"retention save record restores count, source and exact outfit references");
  p.Position=6;p.pather.Destination=new LocalTargetInfo(8);p.jobs.curJob=JobMaker.MakeJob(JobDefOf.HaulToCell,new LocalTargetInfo(8));
  BufferedTransitGuard.Reset();
  Check(BufferedTransitGuard.BlockUnnecessaryEntry(p,p.CurJob,5)&&p.pather.Repaths==1&&p.jobs.Ends==0,"buffered shortcut reroutes before unrelated Work Area entry");
  Check(BufferedTransitGuard.BlockUnnecessaryEntry(p,p.CurJob,5)&&p.jobs.Ends==1&&b.Completed==1,"repeated shortcut fails without task credit or another outfit");
  p.jobs.curJob=JobMaker.MakeJob(JobDefOf.HaulToCell,new LocalTargetInfo(5));p.pather.Destination=5;
  Check(!BufferedTransitGuard.BlockUnnecessaryEntry(p,p.CurJob,5),"real protected work destination remains subject to ordinary outfit preparation");
  p.jobs.curJob=JobMaker.MakeJob(JobDefOf.HaulToCell,new LocalTargetInfo(8));p.pather.Destination=8;ProtectedPathAvoidance.Fail=true;
  Check(!BufferedTransitGuard.BlockUnnecessaryEntry(p,p.CurJob,5),"unavoidable transit yields to normal boundary access and gear checks");
  ProtectedPathAvoidance.Fail=false;p.CurJob.playerForced=true;
  Check(!BufferedTransitGuard.BlockUnnecessaryEntry(p,p.CurJob,5),"explicit travel order retains native authority");
  p.Position=4;p.CurJob.playerForced=false;
  var work=JobMaker.MakeJob(JobDefOf.HaulToCell,food);NonWorkBufferTracker.PreserveHandoff(p,dest,work,2);
  Check(b.PendingWork!=work&&b.PendingWork.targetA.Thing==food,"conflicting work is detached from native job ownership");
  var proposal=JobMaker.MakeJob(JobDefOf.Wait,p);ThinkNode giver=null;ThinkTreeDef tree=null;JobTag? tag=null;
  Check(NonWorkBufferTracker.ResumeHandoff(p,ref proposal,ref giver,ref tree,ref tag,out bool exiting)&&exiting&&proposal.targetA.Cell==new IntVec3(2),"conflicting task exits Non-Work source before changing");
  p.Position=2;proposal=JobMaker.MakeJob(JobDefOf.Wait,p);
  Check(NonWorkBufferTracker.ResumeHandoff(p,ref proposal,ref giver,ref tree,ref tag,out exiting)&&!exiting&&proposal.targetA.Thing==food&&NonWorkBufferTracker.For(p)==null,"safe exit resumes exact work and retires the old allowance");
  p.Position=4;c.State=null;p.jobs.curJob=JobMaker.MakeJob(JobDefOf.Ingest,new LocalTargetInfo(4));NonWorkBufferTracker.Refresh(p);p.Drafted=true;NonWorkBufferTracker.Refresh(p);
  Check(NonWorkBufferTracker.For(p)==null&&p.apparel.WornApparel.Contains(shirt),"draft releases personal buffer without stripping clothing");
  p.Drafted=false;NonWorkBufferTracker.Refresh(p);NonWorkBufferTracker.PreserveHandoff(p,dest,work,2);food.Destroyed=true;proposal=JobMaker.MakeJob(JobDefOf.Wait,p);
  Check(!NonWorkBufferTracker.ResumeHandoff(p,ref proposal,ref giver,ref tree,ref tag,out exiting)&&NonWorkBufferTracker.For(p)==null,"destroyed work target cancels saved handoff safely");
 }
 static bool Capture(){LocalTargetInfo t=new LocalTargetInfo(4);return NonWorkMealHandoff.BeforePath(p,ref t,PathEndMode.OnCell);}
 static void RuleBufferDisplayTests(){
  Setup();source.ReturnTaskBuffer=3;dest.ReturnTaskBuffer=2;c.State.BufferedTasksCompleted=2;
  var display=RuleBufferProgress.For(dest.Id,2,c.State,null);
  Check(!display.Started&&display.Completed==0&&display.Summary()=="Buffer: not started","destination hover does not borrow outgoing Work buffer");
  Check(RuleBufferProgress.For(source.Id,3,c.State,null).Completed==2,"outgoing Work rule keeps its actual counter");
  var personal=new NonWorkOutfitBuffer{RuleId=dest.Id,Completed=1,PendingJobId=42};
  display=RuleBufferProgress.For(dest.Id,2,c.State,personal);
  Check(display.Completed==1&&display.PendingJobId==42&&display.Headline("Hauling").Contains("1 of 2 complete")&&display.Summary().Contains("1/2"),"personal occupant row and hover share only its source rule count");
  Check(!RuleBufferProgress.For("otherNonWork",5,c.State,personal).Started,"other Non-Work rule cannot inherit personal buffer");
  personal.PendingWork=JobMaker.MakeJob(JobDefOf.HaulToCell,food);
  Check(RuleBufferProgress.For(dest.Id,2,null,personal).Summary().EndsWith("ended"),"conflicting work handoff shows allowance ended, not another active candidate");
  c.State.ActiveRuleId=dest.Id;c.State.BufferedTasksCompleted=1;
  Check(RuleBufferProgress.For(dest.Id,2,c.State,null).Completed==1,"managed fallback occupant uses its own active rule count");
  c.State.ActiveRuleId=source.Id;c.State.NestedRuleBuffers.Add(new NestedRuleBufferState{RuleId=dest.Id,Completed=1,PendingJobLoadId=99});
  display=RuleBufferProgress.For(dest.Id,2,c.State,null);
  Check(display.Completed==1&&display.PendingJobId==99,"nested Non-Work rule displays its own progress instead of outer progress");
  Check(RuleBufferProgress.For(dest.Id,0,c.State,null).Summary().Contains("off (Immediate)"),"Immediate setting is shown as disabled, not a zero-length active allowance");
  personal.Completed=4;personal.PendingWork=null;
  Check(RuleBufferProgress.For(dest.Id,2,null,personal).Completed==2,"lowering configured allowance clamps row and hover consistently");
 }
 static void CompleteReturn(){p.Position=2;p.apparel.WornApparel.Clear();p.apparel.WornApparel.AddRange(c.Target.Apparel);c.State=null;}
 static Job Resume(){Job j=JobMaker.MakeJob(JobDefOf.Wait,p);NonWorkMealHandoff.BeforeJob(p,ref j);p.jobs.curJob=j;return j;}
 static void AccessRevocationTests(){
  foreach(NonWorkMealStage stage in Enum.GetValues(typeof(NonWorkMealStage))){
   Setup();Capture();var trip=c.NonWorkMealTrips.Single();trip.Stage=stage;dest.ActivitiesAllowed=false;
   var state=c.State;var saved=c.Saved;var job=p.CurJob;var original=job;
   Check(!NonWorkMealHandoff.BeforeJob(p,ref job)&&job==original&&c.NonWorkMealTrips.Count==0,
    "revoked activity cancels "+stage+" handoff before it can own admission");
   Check(p.carryTracker.CarriedThing==food&&c.State==state&&c.Saved==saved&&p.jobs.Ends==0,
    "revoked "+stage+" preserves meal, outfit owner and native job lifecycle");
  }
  Setup();Capture();var exit=JobMaker.MakeJob(JobDefOf.Goto,new LocalTargetInfo(6));AccessExitJobs.Mark(p,exit);
  var originalExit=exit;
  Check(!NonWorkMealHandoff.BeforeJob(p,ref exit)&&exit==originalExit&&c.NonWorkMealTrips.Count==0,
   "owned access exit supersedes a pending meal without replacement");
  Setup();Capture();dest.ActivitiesAllowed=false;LocalTargetInfo target=4;
  Check(NonWorkMealHandoff.BeforePath(p,ref target,PathEndMode.OnCell)&&c.NonWorkMealTrips.Count==0&&p.jobs.Ends==0,
   "path callback yields revoked meal to ordinary boundary guard without ending a toil");
  Setup();Capture();dest.ActivitiesAllowed=false;NonWorkMealHandoff.Tick(c);
  Check(c.NonWorkMealTrips.Count==0&&c.BeginReturns==0&&p.carryTracker.CarriedThing==food,
   "tick cancels stale access before stowing or beginning a meal return");
  Setup();Capture();var eating=c.NonWorkMealTrips.Single();eating.Stage=NonWorkMealStage.Eating;dest.ActivitiesAllowed=false;
  Check(!NonWorkMealHandoff.GuardMealFromHaul(p,JobMaker.MakeJob(JobDefOf.HaulToCell,food))&&c.NonWorkMealTrips.Count==0,
   "meal guard cannot suppress access enforcement after permission revocation");
  Setup();Capture();eating=c.NonWorkMealTrips.Single();eating.Stage=NonWorkMealStage.Eating;dest.ActivitiesAllowed=false;
  IntVec3 spot=4;bool found=false;
  Check(!NonWorkMealHandoff.DiningSpot(p,food,ref spot,ref found)&&c.NonWorkMealTrips.Count==0,
   "revoked dining spot returns to native selection and boundary checks");
 }
 static void Tests(){
  Setup();var original=p.CurJob;Check(!Capture(),"late dining path defers for a source-locker handoff");
  Check(p.CurJob==original&&p.carryTracker.CarriedThing==food&&p.jobs.Ends==0,"capture does not mutate the native job inside its toil");
  var trip=c.NonWorkMealTrips.Single();NonWorkMealHandoff.Tick(c);
  Check(p.inventory.innerContainer.Contains(food)&&p.carryTracker.CarriedThing==null,"exact carried meal is stowed before interrupting its job");
  Check(!p.carryTracker.innerContainer.LastMerge,"stowing disables stack merging to preserve identity");
  Check(trip.Stage==NonWorkMealStage.Returning&&c.BeginReturns==1&&trip.SourceRuleId=="source"&&trip.LockerCell==new IntVec3(2),"return uses the source locker and a single restoration");
  CompleteReturn();var eat=Resume();Check(eat.def==JobDefOf.Ingest&&eat.targetA.Thing==food&&eat.count==2,"same meal and count resume after the personal outfit is worn");
  Check(NonWorkMealHandoff.Restricted(p,trip).Any(r=>r.Id=="source")&&NonWorkMealHandoff.Restricted(p,trip).Any(r=>r.Id=="otherWork")&&!NonWorkMealHandoff.Restricted(p,trip).Contains(dest),"completed change blocks all Work Areas while admitting its non-work destination");
  Check(NonWorkMealHandoff.GuardMealFromHaul(p,JobMaker.MakeJob(JobDefOf.HaulToCell,food)),"automatic hauling cannot displace the active preserved meal");
  var forced=JobMaker.MakeJob(JobDefOf.HaulToCell,food);forced.playerForced=true;
  Check(!NonWorkMealHandoff.GuardMealFromHaul(p,forced),"explicit hauling remains authoritative");
  Check(NonWorkMealHandoff.BlockEntry(p,5)&&trip.EatInPlace,"an actual rerouted Work Area cell triggers recovery before entry");
  NonWorkMealHandoff.Tick(c);Check(p.CurJob.def==JobDefOf.Ingest&&p.CurJob.targetA.Thing==food&&c.BeginReturns==1,"route recovery retains the meal without a second outfit return");
  IntVec3 spot=4;bool found=false;NonWorkMealHandoff.DiningSpot(p,food,ref spot,ref found);
  Check(found&&spot==p.Position,"unusable dining detour falls back to eating at the current location");
  NonWorkMealHandoff.NotifyEnded(p,p.CurJob);Check(c.NonWorkMealTrips.Count==0,"completed eating releases the transit restriction");
  Setup();ProtectedPathAvoidance.Fail=true;LocalTargetInfo blocked=4;
  Check(NonWorkMealHandoff.BeforePath(p,ref blocked,PathEndMode.OnCell)&&blocked.Cell==p.Position&&c.BeginReturns==0,"failed preflight eats at pickup without stripping work gear");
  Setup();HazardousEnvironmentSafety.Hazard=true;LocalTargetInfo hazardPath=4;NonWorkMealHandoff.BeforePath(p,ref hazardPath,PathEndMode.OnCell);
  Check(hazardPath.Cell==p.Position&&c.BeginReturns==0,"hazardous post-removal gear routes retain the current protective outfit");
  Setup();Capture();p.Drafted=true;NonWorkMealHandoff.Tick(c);
  Check(c.NonWorkMealTrips.Count==0&&p.carryTracker.CarriedThing==food&&c.BeginReturns==0,"draft cancels pending handoff without moving food or gear");
  Setup();Capture();food.Destroyed=true;NonWorkMealHandoff.Tick(c);
  Check(c.NonWorkMealTrips.Count==0&&c.BeginReturns==0,"lost food retires the continuation instead of recreating food");
  Setup();Capture();shirt.Unavailable=true;NonWorkMealHandoff.Tick(c);
  Check(c.BeginReturns==0&&c.NonWorkMealTrips.Single().EatInPlace,"changed item availability is checked again before returning gear");
  Setup();Capture();NonWorkMealHandoff.Tick(c);p.Position=2;shirt.Unavailable=true;Job waiting=JobMaker.MakeJob(JobDefOf.Wait,p);
  NonWorkMealHandoff.BeforeJob(p,ref waiting);Check(c.NonWorkMealTrips.Single().EatInPlace&&p.apparel.WornApparel.Contains(armor),"gear claimed at the locker is caught before removal");
  Setup();Capture();NonWorkMealHandoff.Tick(c);dest.Enabled=false;NonWorkMealHandoff.Tick(c);
  Check(c.NonWorkMealTrips.Single().EatInPlace&&p.CurJob.targetA.Thing==food,"disabling the destination yields a meal recovery instead of a transition loop");
  Setup();Capture();NonWorkMealHandoff.Tick(c);Find.TickManager.TicksGame=5001;NonWorkMealHandoff.Tick(c);
  Check(c.NonWorkMealTrips.Single().EatInPlace&&c.BeginReturns==1,"a stalled changing trip has a bounded recovery");
  Setup();Capture();p.Map=new Map();NonWorkMealHandoff.Tick(c);
  Check(c.NonWorkMealTrips.Count==0,"map changes retire map-specific dining coordinates");
  Setup(true);dest.RequiredApparel.Add(shirt.def);Capture();var personalSnapshot=c.Saved;
  NonWorkMealHandoff.Tick(c);CompleteReturn();var wear=Resume();
  Check(wear.def==JobDefOf.Wear&&wear.targetA.Thing==shirt&&c.State.NonWorkFallbackActive,"fallback preparation begins after source gear return with its normal temporary ownership");
  p.apparel.WornApparel.Add(shirt);p.Position=3;var fallbackEat=Resume();
  Check(fallbackEat.def==JobDefOf.Ingest&&c.State.Transition==ApparelTransition.Active&&c.Saved==personalSnapshot&&c.BeginReturns==1,"fallback resumes the meal without replacing the saved personal snapshot or visiting a second locker");
  Setup(true);Capture();NonWorkMealHandoff.Tick(c);CompleteReturn();Check(Resume().def==JobDefOf.Ingest&&p.apparel.WornApparel.Count==0,"unchecked preference with no fallback allows empty gear slots");
  Setup();Capture();trip=c.NonWorkMealTrips.Single();Scribe.Values.Clear();trip.ExposeData();Scribe.Loading=true;var loaded=new NonWorkMealTrip();loaded.ExposeData();Scribe.Loading=false;
  Check(loaded.Meal==food&&loaded.Pawn==p&&loaded.JobLoadId==trip.JobLoadId&&loaded.SourceRuleId==source.Id&&loaded.DiningCell==trip.DiningCell,"save record restores exact meal identity, stage context, and destination");
  Check(!typeof(NonWorkMealTrip).GetFields().Any(f=>typeof(Job).IsAssignableFrom(f.FieldType)),"persisted handoff has no duplicated native Job owner");
  AccessRevocationTests();
  BufferTests();
  RuleBufferDisplayTests();
  ChildcareTests();
  Console.WriteLine(passed+" meal/buffer controller checks passed; native gameplay still requires RimWorld.");
 }
 public static int Main(){try{Tests();return 0;}catch(Exception e){Console.Error.WriteLine("FAIL "+e);return 1;}}
}
namespace Verse {
 public interface IExposable{void ExposeData();} public interface IThingHolder{IThingHolder ParentHolder{get;}}
 public class Map{}
 public struct IntVec3:IEquatable<IntVec3>{public int x;public IntVec3(int n){x=n;}public static IntVec3 Invalid=>new IntVec3(-1);public bool IsValid=>x>=0;public bool InBounds(Map m)=>IsValid;public bool Equals(IntVec3 b)=>x==b.x;public override bool Equals(object b)=>b is IntVec3 c&&Equals(c);public override int GetHashCode()=>x;public static implicit operator IntVec3(int n)=>new IntVec3(n);public static bool operator==(IntVec3 a,IntVec3 b)=>a.Equals(b);public static bool operator!=(IntVec3 a,IntVec3 b)=>!a.Equals(b);}
 public struct LocalTargetInfo{public static bool operator ==(LocalTargetInfo a,LocalTargetInfo b)=>a.Thing==b.Thing&&a.Cell==b.Cell;public static bool operator !=(LocalTargetInfo a,LocalTargetInfo b)=>!(a==b);public bool IsValid=>Cell.IsValid;public Thing Thing;IntVec3 pos;public LocalTargetInfo(IntVec3 p){pos=p;Thing=null;}public bool HasThing=>Thing!=null;public IntVec3 Cell=>Thing?.Position??pos;public static implicit operator LocalTargetInfo(IntVec3 p)=>new LocalTargetInfo(p);public static implicit operator LocalTargetInfo(int p)=>new LocalTargetInfo(new IntVec3(p));public static implicit operator LocalTargetInfo(Thing t)=>new LocalTargetInfo{Thing=t};}
 public class Area{public Map Map;HashSet<IntVec3> cells;public Area(Map m,params int[] c){Map=m;cells=new HashSet<IntVec3>(c.Select(x=>new IntVec3(x)));}public bool this[IntVec3 c]=>cells.Contains(c);}
 public class ThingDef{public bool apparel,IsDrug,IsWeapon;public object ingestible=new object();}
 public class Thing{public ThingDef def=new ThingDef();public Map Map;public IntVec3 Position;public bool Destroyed,Unavailable;public int stackCount=1;public bool IngestibleNow=>!Destroyed;public bool Spawned=>true;public Map MapHeld=>Map;public IntVec3 PositionHeld=>Position;public IThingHolder ParentHolder=>null;public float GetStatValue(object stat)=>1;}
 public class ThingWithComps:Thing{}
 public class Pawn:Thing,IThingHolder{public DevelopmentalStage DevelopmentalStage=DevelopmentalStage.Adult;public bool Dead,Downed,Drafted,InMentalState;public object LabelShortCap=>"test";public Pawn_JobTracker jobs=new Pawn_JobTracker();public Pawn_PathFollower pather=new Pawn_PathFollower();public Pawn_CarryTracker carryTracker=new Pawn_CarryTracker();public Pawn_InventoryTracker inventory=new Pawn_InventoryTracker();public ApparelTracker apparel=new ApparelTracker();public EquipmentTracker equipment=new EquipmentTracker();public RaceProps RaceProps=new RaceProps();public Job CurJob=>jobs.curJob;public bool CanReserve(Thing t)=>!t.Unavailable;public bool CanReserveSittableOrSpot(IntVec3 c)=>true;}
 public enum DevelopmentalStage { Baby, Child, Adult }
 public class RaceProps{public object body;public bool Humanlike=true;}
 public class ApparelTracker{public List<Apparel> WornApparel=new List<Apparel>();}
 public class EquipmentTracker{public ThingWithComps Primary;}
 public class ThingOwner:List<Thing>{public bool LastMerge;public bool TryTransferToContainer(Thing t,ThingOwner dest,bool merge){LastMerge=merge;if(!Remove(t))return false;dest.Add(t);return true;}}
 public class Pawn_CarryTracker{public Pawn pawn;public ThingOwner innerContainer=new ThingOwner();public Thing CarriedThing=>innerContainer.FirstOrDefault();}
 public class Pawn_InventoryTracker{public ThingOwner innerContainer=new ThingOwner();}
 public static class Find{public static TickManager TickManager=new TickManager();}public class TickManager{public int TicksGame;}
 public static class Scribe{public static bool Loading;public static Dictionary<string,object> Values=new Dictionary<string,object>();public static void Look<T>(ref T v,string name){if(Loading){if(Values.TryGetValue(name,out var x))v=(T)x;}else Values[name]=v;}}
 public static class Scribe_References{public static void Look<T>(ref T v,string key)=>Scribe.Look(ref v,key);}
 public static class Scribe_Values{public static void Look<T>(ref T v,string key,T def=default(T))=>Scribe.Look(ref v,key);}
 public enum LookMode{Reference} public static class Scribe_Collections{public static void Look<T>(ref List<T> v,string key,LookMode mode)=>Scribe.Look(ref v,key);}
 public static class Scribe_Deep{public static void Look<T>(ref T v,string key)=>Scribe.Look(ref v,key);}
}
namespace Verse.AI {
 public class ThinkNode{} public class ThinkTreeDef{} public enum JobTag{Misc}

 public enum PathEndMode{OnCell} public enum JobCondition{InterruptForced,Succeeded,Incompletable}
 public class JobDef{public string defName="test";}
 public class Job{public int expiryInterval;public ThinkNode jobGiver;public ThinkTreeDef jobGiverThinkTree;public JobDef def;public int count=1,loadID;public LocalTargetInfo targetA;public bool playerForced;}
 public static class JobMaker{static int seq;public static Job MakeJob(JobDef def,LocalTargetInfo a)=>new Job{def=def,targetA=a,loadID=++seq};}
 public class Pawn_JobTracker{public Pawn pawn;public Job curJob;public int Ends;public void ClearQueuedJobs(bool b){}public void EndCurrentJob(JobCondition c,bool a,bool b){Ends++;curJob=null;}public void StartJob(Job j,JobCondition c,object a,bool b,bool d){curJob=j;}}
 public class Pawn_PathFollower{public LocalTargetInfo Destination; public int Repaths; public void StopDead(){}public void StartPath(LocalTargetInfo dest,PathEndMode mode){Destination=dest;Repaths++;}}
 public class Toil{public Action initAction;}
}
namespace RimWorld {
 public class JobGiver_OptimizeApparel{}
 public class Apparel:ThingWithComps{}
 public static class StatDefOf{public static object Nutrition;}
 public static class JobDefOf{public static JobDef Goto=new JobDef(),Ingest=new JobDef(),Wear=new JobDef(),Equip=new JobDef(),Wait=new JobDef(),HaulToCell=new JobDef();}
 public static class EquipmentUtility{public static bool CanEquip(Thing t,Pawn p)=>!t.Unavailable;}
 public static class ApparelUtility{public static bool CanWearTogether(ThingDef a,ThingDef b,object body)=>a!=b;}
 public static class Toils_Ingest{}
 public static class Toils_Misc{public static void TakeItemFromInventoryToCarrier(){}}
}
namespace HarmonyLib {
 public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string s){}}public class HarmonyPriority:Attribute{public HarmonyPriority(int n){}}public static class Priority{public const int Last=0;public const int First=0;}
 public class HarmonyPostfix:Attribute{} public static class AccessTools{public delegate V FieldRef<T,V>(T obj);public static FieldRef<T,V> FieldRefAccess<T,V>(string field)=>obj=>default(V);}
}
namespace AutomaticOutfitManager.Rules {
 public class ApparelRule{public int ReturnTaskBuffer;public string Id;public Area Area,ChangingArea;public bool IsNonWork,WorkAreaPaused,HasWeaponRequirement;public bool ActivitiesAllowed=true;public bool Enabled=true,DefaultToSavedPersonalOutfit=true;public List<ThingDef> RequiredApparel=new List<ThingDef>();public bool Allows(Apparel a)=>true;}
}
namespace AutomaticOutfitManager.State {
 public enum ApparelTransition{Active,ReturningToChangingArea,Restoring,Preparing}
 public class NestedRuleBufferState{public int PendingJobLoadId,Completed;public bool Finished;public string RuleId;}
 public class PawnApparelState{public int PendingBufferedJobLoadId=-1,BufferedTasksCompleted;public List<NestedRuleBufferState> NestedRuleBuffers=new List<NestedRuleBufferState>();public Pawn Pawn;public string ActiveRuleId;public ApparelTransition Transition;public bool RecallRequested,RecallInterruptPending,WeaponRuleOverrideExplicit,NonWorkFallbackActive;public List<string> CurrentRuleIds;}
 public class SavedNonWorkOutfit{public Pawn Pawn;public List<Apparel> Apparel=new List<Apparel>();public ThingWithComps Weapon;public bool ApparelSatisfied(Pawn p)=>Apparel.All(p.apparel.WornApparel.Contains)&&p.apparel.WornApparel.All(Apparel.Contains);public bool WeaponSatisfied(Pawn p)=>Weapon==p.equipment.Primary;}
 public static class NonWorkOutfitPolicy{public static SavedNonWorkOutfit Target(Pawn p,ApparelRule r)=>AutomaticOutfitManagerGameComponent.Current.Target;public static bool NeedsStateHandoff(Pawn p,ApparelRule r)=>false;public static bool Keep(Pawn p,ApparelRule r,ThingWithComps t)=>false;}
}
namespace AutomaticOutfitManager.Core {
 public class AutomaticOutfitManagerGameComponent{public static AutomaticOutfitManagerGameComponent Current;public List<NonWorkOutfitBuffer> NonWorkOutfitBuffers=new List<NonWorkOutfitBuffer>();public List<NonWorkMealTrip> NonWorkMealTrips=new List<NonWorkMealTrip>();public List<ApparelRule> Rules=new List<ApparelRule>();public PawnApparelState State;public SavedNonWorkOutfit Saved,Target;public int BeginReturns;public PawnApparelState StateFor(Pawn p)=>State;public ApparelRule RuleById(string id)=>Rules.FirstOrDefault(r=>r.Id==id);public void BeginNonWorkRestoration(Pawn p,ApparelRule r,SavedNonWorkOutfit s,bool f){BeginReturns++;State.RecallRequested=true;State.Transition=ApparelTransition.ReturningToChangingArea;}public PawnApparelState BeginIntervention(Pawn p,ApparelRule r,IEnumerable<Apparel>a,ThingWithComps w,bool f){return State=new PawnApparelState{Pawn=p,ActiveRuleId=r.Id,NonWorkFallbackActive=f,Transition=ApparelTransition.Preparing};}public bool IsSavedForOtherPawn(Apparel a,Pawn p)=>false;public bool IsManagedApparelAssignedToOtherPawn(Apparel a,Pawn p)=>false;public bool IsSavedWeaponForOtherPawn(ThingWithComps a,Pawn p)=>false;public bool IsManagedWeaponAssignedToOtherPawn(ThingWithComps a,Pawn p)=>false;public static void ReleaseNativeReservations(Pawn p,Job j){} }
 public static class AomLog{public static bool DetailedEnabled=>false;public static bool ShouldLogDetailed(Pawn p,string key,int ticks)=>false;public static void Detailed(string m){}}
}
namespace AutomaticOutfitManager.Detection {
 public static class RuleEvaluator{public static IReadOnlyList<ApparelRule> ActiveRulesForMap(Map m)=>EnabledRulesForMap(m).Where(r=>!r.WorkAreaPaused).ToList(); public static List<ApparelRule> MatchingRules(Pawn p,Job j)=>ActiveRulesForMap(p.Map).Where(r=>JobTargetsArea(j,r.Area)).ToList();public static bool JobTargetsArea(Job j,Area a)=>j!=null&&a[j.targetA.Cell];public static IReadOnlyList<ApparelRule> EnabledRulesForMap(Map m)=>AutomaticOutfitManagerGameComponent.Current.Rules.Where(r=>r.Enabled&&r.Area.Map==m).ToList();public static bool UsesSavedNonWorkOutfit(Pawn p,ApparelRule r)=>r.DefaultToSavedPersonalOutfit;public static bool RuleCanApplyToPawn(Pawn p,ApparelRule r)=>true;public static bool SelectedNonWorkOutfitConflicts(Pawn p,IEnumerable<ApparelRule> r)=>false;public static bool HasMissingRequiredGear(Pawn p,ApparelRule r)=>r.DefaultToSavedPersonalOutfit?!AutomaticOutfitManagerGameComponent.Current.Target.ApparelSatisfied(p):(AutomaticOutfitManagerGameComponent.Current.State?.ActiveRuleId=="source"&&p.apparel.WornApparel.Count>0)||r.RequiredApparel.Any(d=>!p.apparel.WornApparel.Any(a=>a.def==d));public static bool TryCombinedWeaponRequirement(ApparelRule[] r,out CombinedWeaponRequirement v,Pawn p){v=new CombinedWeaponRequirement();return true;}}
 public class CombinedWeaponRequirement{public bool Matches(ThingWithComps t)=>true;}
 public static class PawnAccessClassifier{public static bool IsNativeCustodyEscapeActive(Pawn p)=>false;public static bool IsApparelEligibleHuman(Pawn p)=>true;}
 public static class ApparelFinder{public static Apparel Candidate;public static Apparel FindBest(Pawn p,ThingDef d,Area a,object x,IEnumerable<ApparelRule>r,Predicate<Apparel> f)=>f(Candidate)?Candidate:null;}
 public static class WeaponFinder{public static ThingWithComps FindBest(Pawn p,CombinedWeaponRequirement r,Area a,object x,Predicate<ThingWithComps> f)=>null;}
 public static class UnavailableWorkRegistry{public static void Block(Pawn p,ApparelRule r,Job j){}public static bool HasActiveRuleBlock(Pawn p,ApparelRule r)=>false;}
 public static class ProtectedBoundaryRetryRegistry{public static Job DetachedClone(Job j)=>new Job{def=j.def,targetA=j.targetA,count=j.count,loadID=j.loadID};public static void Clear(Pawn p){}}
 public static class PreparedIngestRetryRegistry{public static void Clear(Pawn p){}}
}
namespace AutomaticOutfitManager.Patches {
 public static class HazardousEnvironmentSafety{public static bool Hazard;public static Predicate<IntVec3> RemovalHazards(Pawn p,IEnumerable<Apparel> retained)=>cell=>Hazard&&cell==new IntVec3(3);}
 public static class ProtectedPathAvoidance{public static bool Fail;public static bool SegmentAvoidsRules(Pawn p,IntVec3 s,LocalTargetInfo d,List<ApparelRule> r,Predicate<IntVec3> unsafeCell=null,PathEndMode? exactEndMode=null)=>!Fail&&!r.Any(x=>x.Area[d.Cell])&&unsafeCell?.Invoke(d.Cell)!=true;}
 public static class PawnJobTracker_StartJob_Patch{public static bool IsBufferableJob(Job j)=>j.def!=JobDefOf.Wait&&j.def!=JobDefOf.Goto&&j.def!=JobDefOf.Wear&&j.def!=JobDefOf.Equip;public static Job MakeChangingAreaTravelJob(IntVec3 c)=>JobMaker.MakeJob(JobDefOf.Goto,c); public static bool PendingWorkJobIsViable(Pawn p,Job j,out string reason){reason=null;return j?.def!=null&&j.targetA.Thing?.Destroyed!=true;}public static bool IsNativeEmergencySafetyJob(Job j)=>false;public static bool IsMapDepartureJob(Job j)=>false;public static bool TryFindSafeTransitionCell(Pawn p,Area a,IEnumerable<ApparelRule>r,out IntVec3 cell,PawnApparelState s){cell=2;return true;}public static Job MakeSafeWaitJob(Pawn p,int t)=>JobMaker.MakeJob(JobDefOf.Wait,p);}
 public static class PawnPathFollower_ProtectedArea_Patch{public static bool IsManagedTransitionJob(Pawn p,Job j,PawnApparelState s)=>s!=null&&(j?.def==JobDefOf.Wear||j?.def==JobDefOf.Equip);}
 public static class PausedAreaWorkFilter{public static bool WorkAllowedFor(ApparelRule r,Pawn p)=>r.ActivitiesAllowed;public static bool IsEssentialPersonalJob(Job j)=>false;public static bool ActivityAllowedAtRuleBoundary(Pawn p,Job j,ApparelRule r)=>true;public static bool IsHaulingJob(Job j)=>j?.def==JobDefOf.HaulToCell;}
}
