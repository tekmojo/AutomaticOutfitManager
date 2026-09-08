// Production BuildJobs, replacement selection, BuildWeaponJobs, ordering and
// status formatting are compiled against this deterministic world. Native
// pathfinding, equipment drivers and successful-wear callbacks need game tests.
using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.UI;
using AutomaticOutfitManager.Rules;
using RimWorld;
using Verse;
using Verse.AI;

class RestorationContractTests
{
    static int passed;
    static Pawn p;
    static PawnApparelState state;
    static Apparel old, replacement, helmet, work;
    static ThingWithComps weapon, workWeapon;
    static ApparelRule rule;
    static void Check(bool value, string label) { if (!value) throw new Exception(label); passed++; }
    static Apparel Apparel(string name, int cell, int slot, float score=1, int hp=100) => new Apparel
    { LabelCap=name, Map=p.Map, Position=new IntVec3(cell), def=new ThingDef { Slot=slot }, Score=score, HitPoints=hp };
    static void CheckCooldown()
    {
        int discardedPlans = 0;
        foreach (int elapsed in new[] {30, 60, 90, 120})
            if (!RestorationPlanner.RecoveryCooldownActive(1000 + elapsed, 1000)) discardedPlans++;
        Check(discardedPlans == 1, "minimum retry window avoids three discarded plans but admits the due retry");
        Check(!RestorationPlanner.RecoveryCooldownActive(1030, -1), "freed exact gear or successful step bypasses cooldown immediately");
        Check(!RestorationPlanner.RecoveryCooldownActive(3400, 1000), "unavailable gear can still be reevaluated at its existing deadline");
        Check(!RestorationPlanner.RecoveryCooldownActive(0, 1000), "a rewound tick is not cached as a valid cooldown");
    }
    static void Setup()
    {
        p = new Pawn { Map=new Map(), Position=new IntVec3(0) };
        old=Apparel("Camelhide robe (48%)", 2, 1, 1, 48);
        helmet=Apparel("Cadian helmet", 4, 2);
        replacement=Apparel("Plainleather robe", 100, 1, 2);
        work=Apparel("Work armor", 0, 3);
        weapon=new ThingWithComps { LabelCap="Saved weapon", Map=p.Map, Position=new IntVec3(1) };
        workWeapon=new ThingWithComps { LabelCap="Work weapon", Map=p.Map };
        p.equipment.Primary=workWeapon; p.apparel.WornApparel.Add(work);
        p.Map.listerThings.Items.Add(replacement);
        state=new PawnApparelState { OriginalApparel=new List<Apparel> { old,helmet },
            ManagedApparel=new List<Apparel>{work}, OriginalWeapon=weapon,
            ManagedWeapons=new List<ThingWithComps>{workWeapon}, WeaponInterventionActive=true };
        rule=new ApparelRule { ChangingArea=new Area(p.Map,0,1,2,4) };
    }
    static List<Job> Plan(out bool missing) => RestorationPlanner.BuildJobs(p,state,rule,out missing);
    static void Tests()
    {
        CheckCooldown();
        Setup(); var jobs=Plan(out bool missing);
        Check(!missing && jobs.Count==5,"complete plan has two returns, two exact retrievals and one upgrade");
        Check(jobs[0].def==JobDefOf.RemoveApparel && jobs[1].def==JobDefOf.DropEquipment,"returns precede retrieval and keep safe cleanup order");
        Check(jobs[2].targetA.Thing==weapon && jobs[3].targetA.Thing==helmet && jobs[4].targetA.Thing==replacement,
            "Gonzalez regression: local saved weapon and helmet precede distant replacement");
        Check(jobs.All(j=>j.targetA.Thing!=old),"confirmed upgrade avoids wearing the old robe first");
        Check(state.OriginalApparel.Contains(old) && !state.OriginalApparel.Contains(replacement),"planning cannot adopt a replacement before successful wear");
        Check(!jobs[2].playerForced && jobs[3].playerForced && jobs[4].playerForced,"weapon preference and existing apparel admission flags preserved");
        var robeJob=jobs[4];
        Check(RestorationActivity.Detail(p,state,robeJob)=="Replacement: Plainleather robe\nReplaces: Camelhide robe (48%)","replacement hover names both actual and displaced garment");
        Check(RestorationActivity.WearLabel(state,robeJob)=="Changing personal apparel","new garment is not called an already saved garment");
        Check(RestorationActivity.Detail(p,state,jobs[3])=="Restoring automatic saved apparel: Cadian helmet","helmet hover shows active target instead of first missing robe");
        Check(RestorationActivity.Detail(p,state,jobs[2])=="Restoring automatic saved weapons: Saved weapon","weapon hover shows active weapon even with missing apparel");
        Check(RestorationActivity.Detail(p,state,jobs[0])=="Returning automatic outfit apparel: Work armor","removal detail uses actual target");
        Check(RestorationActivity.Detail(p,state,jobs[1])=="Returning weapon: Work weapon","drop detail uses actual target");
        Check(RestorationActivity.Detail(p,state,JobMaker.MakeJob(JobDefOf.Wait,weapon))==null,"idle job uses unavailable-item fallback");
        robeJob.targetA.Thing.Destroyed=true;
        Check(RestorationActivity.Detail(p,state,robeJob)==null,"destroyed target is not shown as active retrieval");

        Setup(); rule.ChangingArea=null; jobs=Plan(out missing);
        Check(jobs[2].targetA.Thing==weapon && jobs[3].targetA.Thing==helmet,"no-locker return uses nearer saved targets");
        Setup(); helmet.Position=new IntVec3(20); rule.ChangingArea=new Area(p.Map,20);
        jobs=Plan(out missing); Check(jobs[2].targetA.Thing==helmet,"locker originals are grouped before outside originals");
        Setup(); replacement.Position=new IntVec3(1); jobs=Plan(out missing);
        Check(jobs.Last().targetA.Thing==replacement,"even a nearby optional upgrade does not delay exact saved gear");
        Setup(); helmet.Reserved=true; jobs=Plan(out missing);
        Check(missing && !jobs.Any(j=>j.targetA.Thing==helmet) && jobs.Any(j=>j.targetA.Thing==weapon),"unavailable original cannot block remaining progress");
        Setup(); weapon.Reserved=true; jobs=Plan(out missing);
        Check(missing && jobs.Any(j=>j.targetA.Thing==helmet),"unavailable weapon preserves apparel progress and retry flag");
        Setup(); replacement.Reserved=true; jobs=Plan(out missing);
        Check(jobs.Any(j=>j.targetA.Thing==old) && !jobs.Any(j=>j.targetA.Thing==replacement),"reserved upgrade falls back to exact saved garment");
        Setup(); replacement.RouteAllowed=false; jobs=Plan(out missing);
        Check(jobs.Any(j=>j.targetA.Thing==old) && !jobs.Any(j=>j.targetA.Thing==replacement),"blocked protected route cannot be promoted by sorting");
        Setup(); replacement.Destroyed=true; jobs=Plan(out missing);
        Check(jobs.Any(j=>j.targetA.Thing==old),"destroyed upgrade uses exact original");
        Setup(); replacement.def.Managed=true; jobs=Plan(out missing);
        Check(!jobs.Any(j=>j.targetA.Thing==replacement),"managed stock cannot become a personal upgrade");
        Setup(); old.HitPoints=50; jobs=Plan(out missing);
        Check(jobs.Any(j=>j.targetA.Thing==old),"non-tattered saved garment is unchanged");
        Setup(); replacement.HitPoints=49; jobs=Plan(out missing);
        Check(jobs.Any(j=>j.targetA.Thing==old),"tattered candidate is rejected");
        Setup(); replacement.Score=0.5f; jobs=Plan(out missing);
        Check(jobs.Any(j=>j.targetA.Thing==old),"inferior candidate is rejected");
        Setup(); p.apparel.WornApparel.Add(old); jobs=Plan(out missing);
        Check(!jobs.Any(j=>j.targetA.Thing==old || j.targetA.Thing==replacement),"already restored garment cannot create an upgrade past snapshot completion");
        Setup(); jobs=Plan(out missing); replacement.Reserved=true;
        jobs=Plan(out missing);
        Check(jobs.Any(j=>j.targetA.Thing==old) && state.OriginalApparel.Contains(old),"rebuild after failed upgrade retains original ownership and finds fallback");
        Setup(); old.Destroyed=true; jobs=Plan(out missing);
        Check(!jobs.Any(j=>j.targetA.Thing==replacement),"missing original cannot invent a replacement relationship");
        Setup(); state.OriginalWeapon=null; jobs=Plan(out missing);
        Check(jobs.Any(j=>j.def==JobDefOf.DropEquipment) && !jobs.Any(j=>j.def==JobDefOf.Equip),"saved unarmed state stays unarmed");
        Setup(); var extra=Apparel("Explicit personal item",5,99);
        Check(RestorationActivity.Detail(p,state,JobMaker.MakeJob(JobDefOf.Wear,extra))=="Current apparel: Explicit personal item","unrelated wear is not mislabeled as replacement");
        state.Transition=ApparelTransition.Active;
        Check(RestorationActivity.Detail(p,state,JobMaker.MakeJob(JobDefOf.Wear,extra))==null,"restoration detail is confined to restoration");
        Setup(); helmet.Position=weapon.Position; jobs=Plan(out missing);
        Check(jobs.Where(j=>j.def==JobDefOf.Wear || j.def==JobDefOf.Equip).First().targetA.Thing==helmet,"equal-distance retrievals preserve stable input order");
        Setup(); AomLog.DetailedEnabled=true; AomLog.AllowLog=true; AomLog.Messages.Clear();
        helmet.Reserved=true; p.Map.reservationManager.Reserver=new Pawn(); jobs=Plan(out missing);
        Check(AomLog.Messages.Any(m=>m.Contains("Cadian helmet") && m.Contains("canReserve=False") && m.Contains("reserver=test")),"restoration diagnostic names exact reserved saved garment and reserver");
        Check(jobs.Any(j=>j.targetA.Thing==weapon) && state.OriginalApparel.Contains(helmet),"diagnostics do not release blocked saved ownership or block available gear");
        AomLog.Messages.Clear(); AomLog.AllowLog=false; jobs=Plan(out missing);
        Check(!AomLog.Messages.Any(m=>m.Contains("unresolved restoration item")),"repeated restoration detail obeys diagnostic coalescing");
        AomLog.AllowLog=true; helmet.Reserved=false;helmet.RouteAllowed=false;jobs=Plan(out missing);
        Check(AomLog.Messages.Any(m=>m.Contains("Cadian helmet") && m.Contains("nativeReach=False") && m.Contains("protectedReach=not checked")),"native-unreachable gear does not claim a protected probe failure");
        AomLog.Messages.Clear(); helmet.RouteAllowed=true;helmet.ProtectedAllowed=false;jobs=Plan(out missing);
        Check(AomLog.Messages.Any(m=>m.Contains("Cadian helmet") && m.Contains("nativeReach=True") && m.Contains("protectedReach=False")),"protected retrieval failure is distinguished from native reachability");
        Check(AomLog.Messages.Any(m=>m.Contains("Cadian helmet") && m.Contains("nativeHaulCandidate=False")),"blocked garment absent from native hauling is identified");
        p.Map.listerHaulables.Items.Add(helmet);AomLog.Messages.Clear();jobs=Plan(out missing);
        Check(AomLog.Messages.Any(m=>m.Contains("Cadian helmet") && m.Contains("nativeHaulCandidate=True")),"blocked garment present in native hauling is distinguished");
        AomLog.Messages.Clear();helmet.Spawned=false;jobs=Plan(out missing);
        Check(AomLog.Messages.Any(m=>m.Contains("Cadian helmet") && m.Contains("spawned=False") && m.Contains("nativeReach=not checked")),"held gear does not trigger native path probes");
        AomLog.DetailedEnabled=false;
        // Bowman: after all other steps succeeded, the sole remaining saved
        // garment is in an unrelated protected area. The native haul/driver
        // movement is modeled here; BuildJobs is the production planner.
        Setup(); old.HitPoints=100; old.ProtectedAllowed=false;
        state.ManagedApparel.Clear(); state.ManagedWeapons.Clear();
        state.WeaponInterventionActive=false; state.OriginalWeapon=null;
        p.equipment.Primary=null; p.apparel.WornApparel.Clear(); p.apparel.WornApparel.Add(helmet);
        jobs=Plan(out missing);
        Check(missing && jobs.Count==0,"blocked last original leaves an empty plan without substituting unrelated clothing");
        Check(state.OriginalApparel.Contains(old),"waiting retains exact saved ownership");
        old.Position=new IntVec3(2); old.ProtectedAllowed=true;
        jobs=Plan(out missing);
        Check(!missing && jobs.Count==1 && jobs[0].def==JobDefOf.Wear && jobs[0].targetA.Thing==old,
            "safe delivery creates exactly the missing original Wear job");
        Check(jobs[0].playerForced,"recovered saved garment uses existing forced Wear restoration semantics");
        p.apparel.WornApparel.Add(old); old.Spawned=false;
        jobs=Plan(out missing);
        Check(!missing && jobs.Count==0,"successful exact Wear reaches the empty-complete condition used to clear the snapshot");
        Console.WriteLine("PASS " + passed + " production restoration planning/status checks; native gameplay still requires RimWorld.");
    }
    static int Main(){try{Tests();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}

namespace Verse {
 public class ListerHaulables { public List<Thing> Items=new List<Thing>(); public ICollection<Thing> ThingsPotentiallyNeedingHauling()=>Items; }
 public class Map { public ListerHaulables listerHaulables=new ListerHaulables(); public ListerThings listerThings=new ListerThings(); public ReservationManager reservationManager=new ReservationManager();public string GetUniqueLoadID()=>"map"; }
 public class ListerThings { public List<Thing> Items=new List<Thing>(); public IEnumerable<Thing> ThingsInGroup(ThingRequestGroup g)=>Items; }
 public enum ThingRequestGroup { Apparel } public enum Danger { Deadly }
 public struct IntVec3 { public int x;public IntVec3(int v){x=v;}public int DistanceToSquared(IntVec3 b)=>(x-b.x)*(x-b.x); }
 public class Area { public Map Map;HashSet<int> cells;public Area(Map m,params int[] c){Map=m;cells=new HashSet<int>(c);}public bool this[IntVec3 v]=>cells.Contains(v.x); }
 public class ThingDef { public bool Managed;public int Slot; }
 public interface IThingHolder { IThingHolder ParentHolder { get; } } public class Thing : IThingHolder { public string ThingID=>LabelCap;public IThingHolder ParentHolder { get; set; }public bool ProtectedAllowed=true; public string LabelCap;public bool Destroyed,Reserved,RouteAllowed=true,Spawned=true,Forbidden; public ThingDef def=new ThingDef();public Map Map;public Map MapHeld=>Map;public IntVec3 Position;public IntVec3 PositionHeld=>Position;public bool IsForbidden(Pawn p)=>Forbidden;public void SetForbidden(bool b,bool ignored){Forbidden=b;}public bool IsBurning()=>false; }
 public class ThingWithComps:Thing { }
 public class Pawn:Thing { public object LabelShortCap=>"test";public ApparelTracker apparel=new ApparelTracker();public EquipmentTracker equipment=new EquipmentTracker();public RaceProps RaceProps=new RaceProps();public Outfits outfits=new Outfits();public bool CanReserve(Thing t)=>!t.Reserved;public bool CanReach(Thing t,Verse.AI.PathEndMode m,Danger d)=>t.RouteAllowed; }
 public class RaceProps { public object body; } public class Outfits {public Policy CurrentApparelPolicy=new Policy();} public class Policy {public Filter filter=new Filter();}public class Filter{public bool Allows(Thing t)=>true;}
 public class ApparelTracker { public List<RimWorld.Apparel> WornApparel=new List<RimWorld.Apparel>(); } public class EquipmentTracker {public ThingWithComps Primary;}
 public struct LocalTargetInfo {public Thing Thing;public static implicit operator LocalTargetInfo(Thing t)=>new LocalTargetInfo{Thing=t};}
}
namespace Verse.AI { public enum PathEndMode{ClosestTouch,OnCell}public class JobDef{}public class Job{public JobDef def;public LocalTargetInfo targetA;public bool playerForced;}public static class JobMaker{public static Job MakeJob(JobDef d,LocalTargetInfo t)=>new Job{def=d,targetA=t};} }
namespace RimWorld {
 public class Apparel:ThingWithComps {public int HitPoints=100,MaxHitPoints=100;public float Score;}
 public static class JobDefOf {public static JobDef Wear=new JobDef(),Equip=new JobDef(),RemoveApparel=new JobDef(),DropEquipment=new JobDef(),Wait=new JobDef();}
 public static class BodyDefOf {public static object Human;}
 public static class ApparelUtility {public static bool CanWearTogether(ThingDef a,ThingDef b,object body)=>a.Slot!=b.Slot;}
 public static class EquipmentUtility {public static bool CanEquip(Thing t,Pawn p)=>true;}
}
namespace AutomaticOutfitManager.State {
 public enum ApparelTransition {Restoring,Active}
 public class PawnApparelState {public ApparelTransition Transition=ApparelTransition.Restoring;public List<Apparel> OriginalApparel=new List<Apparel>(),ManagedApparel=new List<Apparel>();public List<ThingWithComps> ManagedWeapons=new List<ThingWithComps>();public ThingWithComps OriginalWeapon;public bool WeaponInterventionActive,WeaponPlayerOverride;public void AddManagedApparel(IEnumerable<Apparel> a){ManagedApparel.AddRange(a);}public void RequestWeaponRestoration(){}public void CompleteWeaponRestoration(){}public bool IsManagedWeapon(ThingWithComps w)=>ManagedWeapons.Contains(w);public void AbandonWeaponManagementForOverride(){} }
}
namespace AutomaticOutfitManager.Rules {public class ApparelRule{public Area ChangingArea;public List<ThingDef> RequiredApparel=new List<ThingDef>();}}
namespace AutomaticOutfitManager.Core {public static class AomLog{public static bool DetailedEnabled,AllowLog;public static List<string> Messages=new List<string>();public static bool ShouldLogDetailed(Pawn p,string key,int ticks)=>AllowLog;public static void Detailed(string s){Messages.Add(s);}}public class AutomaticOutfitManagerGameComponent{public static AutomaticOutfitManagerGameComponent Current=new AutomaticOutfitManagerGameComponent();public bool IsSavedForOtherPawn(Apparel a,Pawn p)=>false;public bool IsManagedApparelAssignedToOtherPawn(Apparel a,Pawn p)=>false;}}
namespace AutomaticOutfitManager.Storage {public static class ManagedApparelClassifier{public static bool Matches(ThingDef d)=>d.Managed;}}
namespace AutomaticOutfitManager.Patches {
 public static class NonWorkMealHandoff{public static object For(Pawn p)=>null;}
 public static class ReservationUtility_SavedApparel_Patch{public static bool CanReserveForOutfit(Pawn p,Apparel a)=>p.CanReserve(a);}
 public static class SavedApparelReplacementPolicy {public const float MinimumScoreGain=0.05f;public static float NativeScore(Pawn p,Apparel a)=>a.Score;public static List<Apparel> ConflictingSavedApparel(Pawn p,PawnApparelState s,Apparel a)=>s.OriginalApparel.Where(x=>!ApparelUtility.CanWearTogether(x.def,a.def,null)).ToList();}
}
namespace AutomaticOutfitManager.Detection {public static class GearRetrievalRoute{public static PathEndMode EndMode(Thing t)=>t is Apparel ? PathEndMode.OnCell : PathEndMode.ClosestTouch;public static bool CanReach(Pawn p,Thing t)=>t.RouteAllowed && t.ProtectedAllowed;} internal static class SavedGearRecovery { internal static string DescribeLastQuery(Pawn pawn, Thing gear) => "no eligible native hauling query observed"; }}

namespace Verse {public class ReservationManager {public Pawn Reserver;public Pawn FirstRespectedReserver(Thing t,Pawn p,object layer)=>Reserver;}}
