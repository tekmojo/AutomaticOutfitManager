using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

// The complete production gate and Harmony patches execute against controlled
// world/driver dependencies. The native IL probe separately checks the installed
// timer, attendance and stage-trigger bodies this fixture models.
class RitualOutfitPreparationTests
{
    static int passed;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); passed++; }
    static LordJob_Ritual Ritual(out Pawn pawn, out PawnApparelState state)
    {
        Find.TickManager.TicksGame += 60;
        var map = new Map();
        var ritual = new LordJob_Ritual { Map = map, selectedTarget = new TargetInfo(new IntVec3(10)) };
        ritual.lord = new Lord { LordJob = ritual };
        var rule = new ApparelRule { Id = "temple", Area = new Area { Map = map } };
        rule.Area.Cells.Add(new IntVec3(10));
        AutomaticOutfitManagerGameComponent.Current = new AutomaticOutfitManagerGameComponent();
        AutomaticOutfitManagerGameComponent.Current.Rules.Add(rule);
        pawn = new Pawn { Map = map, Lord = ritual.lord, LabelShortCap = "Spectator" };
        ritual.lord.ownedPawns.Add(pawn); ritual.assignments.Pawns.Add(pawn);
        var robe = new Apparel { Map = map };
        state = new PawnApparelState { ActiveRuleId = "temple" };
        state.ManagedApparel.Add(robe);
        AutomaticOutfitManagerGameComponent.Current.States[pawn] = state;
        pawn.CurJob = new Job { def = JobDefOf.Wear, targetA = new LocalTargetInfo(robe), Owned = true, playerForced = true };
        return ritual;
    }
    static void Excluded(string name, Action<LordJob_Ritual,Pawn,PawnApparelState> change)
    {
        var ritual = Ritual(out var pawn,out var state); change(ritual,pawn,state);
        Check(!RitualOutfitPreparation.IsHolding(ritual), name + " cannot hold the ceremony");
    }
    static void GateTests()
    {
        var ritual = Ritual(out var pawn,out var state);
        Check(RitualOutfitPreparation.Report(ritual) == null, "UI cannot release a gate before native duty setup");
        var toil = new LordToil_Ritual { ritual = ritual };
        Trigger success = new StageEndTrigger_TestArrival().MakeTrigger();
        ritual.LordJobTick(); toil.LordToilTick();
        Check(ritual.TicksPassed == 0 && ritual.StageTicks == 0 && ritual.BehaviorTicks == 0,
            "native ritual timers and timed behavior stay at the beginning while dressing");
        Check(ritual.TicksPassedWithProgress == 0 && toil.AttendanceTicks == 0,
            "dressing earns neither ritual progress nor attendance");
        Check(!success.Check(ritual.lord), "ready leader cannot advance while spectator dresses");
        Check(ritual.GetReport(pawn).Contains("Waiting for outfits: Spectator"), "native report explains the hold");
        Check(pawn.CurJob.def == JobDefOf.Wear && pawn.CurJob.playerForced, "gate leaves exact AOM wear job unchanged");
        // A native failure/cancel trigger has no AOM filter.
        Check(new Trigger().Check(ritual.lord), "native failure and cancellation remain executable during hold");
        state.Transition = ApparelTransition.Active; Find.TickManager.TicksGame += 30;
        ritual.LordJobTick(); toil.LordToilTick();
        Check(success.Check(ritual.lord), "last outfit completed releases the native arrival trigger");
        Check(ritual.TicksPassed == 1 && ritual.TicksPassedWithProgress == 1 && toil.AttendanceTicks == 1,
            "native clocks and attendance resume without added dressing time");
        state.Transition = ApparelTransition.Preparing; Find.TickManager.TicksGame += 30;
        Check(!RitualOutfitPreparation.IsHolding(ritual), "released ceremony never pauses again");

        foreach(string role in new[]{"Leader", "Convertee", "Spectator"}) {
            ritual=Ritual(out pawn,out state); pawn.LabelShortCap=role;
            Check(RitualOutfitPreparation.IsHolding(ritual), role + " uses the same outfit preparation gate");
        }
        ritual=Ritual(out pawn,out state);
        var second=new Pawn { Map=pawn.Map,Lord=ritual.lord,LabelShortCap="Leader" };
        ritual.lord.ownedPawns.Add(second);ritual.assignments.Pawns.Add(second);
        Check(RitualOutfitPreparation.IsHolding(ritual),"ready leader does not mask dressing spectator");
        pawn.InMentalState=true;Find.TickManager.TicksGame+=30;
        Check(!RitualOutfitPreparation.IsHolding(ritual),"mental break during preparation releases spectator hold");

        Excluded("unmanaged destination",(r,p,s)=>r.selectedTarget=new TargetInfo(new IntVec3(90)));
        Excluded("disabled rule",(r,p,s)=>AutomaticOutfitManagerGameComponent.Current.Rules[0].Enabled=false);
        Excluded("no outfit state",(r,p,s)=>AutomaticOutfitManagerGameComponent.Current.States.Clear());
        Excluded("mental distress",(r,p,s)=>p.InMentalState=true);
        Excluded("downed pawn",(r,p,s)=>p.Downed=true);
        Excluded("drafted pawn",(r,p,s)=>p.Drafted=true);
        Excluded("dead pawn",(r,p,s)=>p.Dead=true);
        Excluded("native emergency",(r,p,s)=>p.Emergency=true);
        Excluded("native attendance refusal",(r,p,s)=>p.NativeEligible=false);
        Excluded("removed assignment",(r,p,s)=>r.assignments.Pawns.Clear());
        Excluded("left native lord",(r,p,s)=>p.Lord=new Lord());
        Excluded("despawned pawn",(r,p,s)=>p.Spawned=false);
        Excluded("different map",(r,p,s)=>p.Map=new Map());
        Excluded("unreachable outfit",(r,p,s)=>s.ManagedApparel[0].Reachable=false);
        Excluded("protected transit blocks outfit",(r,p,s)=>s.ManagedApparel[0].ProtectedRoute=false);
        Excluded("reserved by another pawn",(r,p,s)=>s.ManagedApparel[0].Reservable=false);
        Excluded("cannot equip outfit",(r,p,s)=>s.ManagedApparel[0].Equippable=false);
        Excluded("forbidden outfit",(r,p,s)=>s.ManagedApparel[0].Forbidden=true);
        Excluded("burning outfit",(r,p,s)=>s.ManagedApparel[0].Burning=true);
        Excluded("destroyed outfit",(r,p,s)=>s.ManagedApparel[0].Destroyed=true);
        Excluded("activities denied",(r,p,s)=>AutomaticOutfitManagerGameComponent.Current.Rules[0].WorkAllowed=false);
        Excluded("activities paused",(r,p,s)=>AutomaticOutfitManagerGameComponent.Current.Rules[0].WorkAreaPaused=true);
        Excluded("child access denied",(r,p,s)=>p.DevelopmentalStage=DevelopmentalStage.Child);
        Excluded("allowed child does not hold for legacy outfit transition",(r,p,s)=> {
            p.DevelopmentalStage=DevelopmentalStage.Child;
            AutomaticOutfitManagerGameComponent.Current.Rules[0].AllowChildren=true;
        });
        Excluded("no executable transition",(r,p,s)=>p.CurJob=null);
        Excluded("unrelated native work",(r,p,s)=>p.CurJob.Owned=false);
        Excluded("player-forced unrelated order",(r,p,s)=>p.CurJob=new Job{playerForced=true});
        Excluded("explicit recall",(r,p,s)=>s.RecallRequested=true);
        Excluded("map departure",(r,p,s)=>s.MapDepartureRequested=true);
        Excluded("ceremony already progressed",(r,p,s)=>r.TicksPassedWithProgress=1);
        Excluded("later stage",(r,p,s)=>r.StageIndex=1);
        foreach(string denial in new[]{"Activity","Haul","Pause","Wander"})
            Excluded(denial+" restriction on continuation",(r,p,s)=>s.PendingWorkJob=new Job{Denied=denial});

        ritual=Ritual(out pawn,out state); var wear=pawn.CurJob;
        pawn.CurJob=new Job{def=JobDefOf.Wait};pawn.jobs.jobQueue.Add(new QueuedJob{job=wear});
        Check(RitualOutfitPreparation.IsHolding(ritual),"connective wait retains queued outfit progress");
        ritual=Ritual(out pawn,out state); state.Transition=ApparelTransition.Restoring;
        state.NonWorkRestorationRuleId="temple";state.RecallRequested=true;
        state.OriginalApparel.Add(state.ManagedApparel[0]);
        Check(RitualOutfitPreparation.IsHolding(ritual),"non-work personal restoration is included");
        ritual=Ritual(out pawn,out state);state.Transition=ApparelTransition.ReturningToChangingArea;
        state.NonWorkRestorationRuleId="temple";state.RecallRequested=true;
        state.OriginalApparel.Add(state.ManagedApparel[0]);pawn.CurJob=new Job{def=JobDefOf.Goto,Owned=true,targetA=new LocalTargetInfo(new IntVec3(20))};
        Check(RitualOutfitPreparation.IsHolding(ritual),"safe locker return before restoration is included");
        Find.TickManager.TicksGame+=30;pawn.RouteAllowed=false;
        Check(!RitualOutfitPreparation.IsHolding(ritual),"blocked protected locker route cannot hold indefinitely");

        ReadinessTests();

        // Save only the one-time release decision, not participants or Jobs.
        ritual=Ritual(out pawn,out state);RitualOutfitPreparation.StateFor(ritual).Released=true;
        Scribe.mode=LoadSaveMode.Saving;ritual.ExposeData();
        var loaded=Ritual(out pawn,out state);Scribe.mode=LoadSaveMode.LoadingVars;loaded.ExposeData();
        Scribe.mode=LoadSaveMode.PostLoadInit;loaded.ExposeData();Scribe.mode=LoadSaveMode.Inactive;
        Check(!RitualOutfitPreparation.IsHolding(loaded),"save/load preserves already released gathering stage");
        ritual=Ritual(out pawn,out state);RitualOutfitPreparation.IsHolding(ritual);
        Scribe.mode=LoadSaveMode.Saving;ritual.ExposeData();
        loaded=Ritual(out pawn,out state);Scribe.mode=LoadSaveMode.LoadingVars;loaded.ExposeData();
        Scribe.mode=LoadSaveMode.PostLoadInit;loaded.ExposeData();Scribe.mode=LoadSaveMode.Inactive;
        Check(RitualOutfitPreparation.IsHolding(loaded),"save/load recomputes still-pending dressing hold");
    }
    static void ReadinessTests()
    {
        var ritual=Ritual(out var pawn,out var state);
        var robe=state.ManagedApparel[0];
        var rule=AutomaticOutfitManagerGameComponent.Current.Rules[0];
        rule.RequiredApparel.Add(robe.def);pawn.Map.Stock.Add(robe);
        AutomaticOutfitManagerGameComponent.Current.States.Clear();pawn.CurJob=null;
        Check(RitualOutfitPreparation.IsHolding(ritual),"eligible participant waits before outfit state exists");
        // Still assigned, not yet attached to the native lord: the roster counts.
        pawn.Lord=null;ritual.lord.ownedPawns.Clear();
        Find.TickManager.TicksGame+=30;
        Check(RitualOutfitPreparation.IsHolding(ritual),"assigned participant waits before native lord admission");
        pawn.Lord=ritual.lord;ritual.lord.ownedPawns.Add(pawn);
        robe.Reachable=false;Find.TickManager.TicksGame+=30;
        Check(!RitualOutfitPreparation.IsHolding(ritual),"unreachable pre-admission outfit cannot hold");
        robe.Reachable=true;Find.TickManager.TicksGame+=30;
        Check(RitualOutfitPreparation.IsHolding(ritual),"clear scan does not permanently release native gathering");
        robe.ProtectedRoute=false;Find.TickManager.TicksGame+=30;
        Check(!RitualOutfitPreparation.IsHolding(ritual),"pre-admission outfit cannot borrow unrelated protected transit");
        robe.ProtectedRoute=true;pawn.Map.Stock.Clear();Find.TickManager.TicksGame+=30;
        Check(!RitualOutfitPreparation.IsHolding(ritual),"unavailable pre-admission stock cannot hold");
        pawn.Map.Stock.Add(robe);pawn.RouteAllowed=false;Find.TickManager.TicksGame+=30;
        Check(!RitualOutfitPreparation.IsHolding(ritual),"unreachable ceremony does not hold before preparation");
        pawn.RouteAllowed=true;AutomaticOutfitManagerGameComponent.Current.States[pawn]=state;
        state.Transition=ApparelTransition.Active;pawn.apparel.WornApparel.Add(robe);
        pawn.CurJob=new Job{def=JobDefOf.Wait};
        Check(RitualOutfitPreparation.RetainReadyOutfit(pawn,state),"ready leader retains outfit during native gathering Wait");
        state.RecallRequested=true;
        Check(!RitualOutfitPreparation.RetainReadyOutfit(pawn,state),"explicit recall overrides gathering retention");
        state.RecallRequested=false;pawn.InMentalState=true;
        Check(!RitualOutfitPreparation.RetainReadyOutfit(pawn,state),"mental control overrides gathering retention");
        pawn.InMentalState=false;rule.Enabled=false;
        Check(!RitualOutfitPreparation.RetainReadyOutfit(pawn,state),"disabled area cannot retain gathering outfit");
        rule.Enabled=true;ritual.StageIndex=1;
        Check(!RitualOutfitPreparation.RetainReadyOutfit(pawn,state),"gathering retention ends when ceremony begins");
        ritual.StageIndex=0;ritual.cancelled=true;
        Check(!RitualOutfitPreparation.RetainReadyOutfit(pawn,state),"native cancellation overrides gathering retention");
    }
    static void ClaimTests()
    {
        var map=new Map();var a=new Pawn{Map=map};var b=new Pawn{Map=map};
        var mat=new Building{Map=map,Position=new IntVec3(10)};mat.def.building.multiSittable=true;
        map.Buildings[new IntVec3(10)]=mat;map.Buildings[new IntVec3(11)]=mat;
        var def=new JobDef{defName="SpectateCeremony"};
        var first=new Job{def=def,targetA=new LocalTargetInfo(new IntVec3(10)),targetB=new LocalTargetInfo(new IntVec3(90)),targetC=new LocalTargetInfo(mat)};
        var second=new Job{def=def,targetA=new LocalTargetInfo(new IntVec3(11)),targetB=first.targetB,targetC=first.targetC};
        ManagedWorkClaimRegistry.ResetForLoadedGame();ManagedWorkClaimRegistry.TryClaim(a,first);
        Check(!ManagedWorkClaimRegistry.IsClaimedByOther(b,second),"shared ritual target does not block other spectator seats");
        Check(ManagedWorkClaimRegistry.IsClaimedByOther(b,first),"same spectator seat remains exclusive");
        Check(!ManagedWorkClaimRegistry.IsClaimedByOther(b,map,mat,mat.Position),"multi-seat mat is not exclusively claimed");
        b.Lord=new Lord{LordJob=new LordJob_Ritual()};
        bool available=true;RitualSpectatorSeat_Patch.Postfix(b,first.targetA.Cell,false,ref available);
        Check(!available,"native seat search skips a dressing pawn's claimed seat");
        available=true;RitualSpectatorSeat_Patch.Postfix(b,second.targetA.Cell,false,ref available);
        Check(available,"native seat search keeps alternate seat available");
        available=true;RitualSpectatorSeat_Patch.Postfix(b,first.targetA.Cell,true,ref available);
        Check(available,"forced native reservation override is preserved");
        available=true;RitualSpectatorSeat_Patch.Postfix(a,first.targetA.Cell,false,ref available);
        Check(available,"seat owner can resume its native reservation");
        mat.def.building.multiSittable=false;mat.def.building.isSittable=true;
        ManagedWorkClaimRegistry.ResetForLoadedGame();ManagedWorkClaimRegistry.TryClaim(a,first);
        Check(ManagedWorkClaimRegistry.IsClaimedByOther(b,second),"single sittable retains native exclusive thing reservation");
        ManagedWorkClaimRegistry.ResetForLoadedGame();first.targetA=LocalTargetInfo.Invalid;
        ManagedWorkClaimRegistry.TryClaim(a,first);
        Check(!ManagedWorkClaimRegistry.HasActiveClaim(a),"missing spectator seat cannot claim shared focus or origin");
    }
    static int Main(string[] args)
    {
        try {
            new Harmony("aom.tests.ritual").PatchAll(Assembly.GetExecutingAssembly());
            if(args.Contains("--claims-only")) ClaimTests(); else { GateTests();ClaimTests(); }
            Console.WriteLine("PASS "+passed+" ritual outfit preparation checks");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}

namespace Verse {
 public static class DevelopmentalStage {public const int Child=2;}
 public class Map { public List<Apparel> Stock=new List<Apparel>(); public Dictionary<IntVec3,Building> Buildings=new Dictionary<IntVec3,Building>(); }
 public class Area { public Map Map;public HashSet<IntVec3> Cells=new HashSet<IntVec3>();public bool this[IntVec3 c]=>Cells.Contains(c); }
 public struct IntVec3 { public int value;public IntVec3(int v){value=v;}public bool IsValid=>value>=0;public static IntVec3 Invalid=>new IntVec3(-1000);public bool InBounds(Map m)=>IsValid&&m!=null;public override int GetHashCode()=>value;public override bool Equals(object o)=>o is IntVec3 v&&v.value==value;public static bool operator ==(IntVec3 a,IntVec3 b)=>a.Equals(b);public static bool operator !=(IntVec3 a,IntVec3 b)=>!a.Equals(b); }
 public struct TargetInfo { public IntVec3 Cell;public TargetInfo(IntVec3 c){Cell=c;} }
 public class ThingDef { public BuildingProperties building=new BuildingProperties(); }
 public class Thing { public Map Map;public Map MapHeld=>Map;public IntVec3 Position;public IntVec3 PositionHeld=>Position;public string LabelCap=>"gear";public ThingDef def=new ThingDef();public bool Destroyed,Forbidden,Burning;public bool Spawned=true,Equippable=true,Reservable=true,Reachable=true,ProtectedRoute=true; }
 public class ThingWithComps:Thing {}
 public class Building:Thing {}
 public class ApparelTracker { public List<Apparel> WornApparel=new List<Apparel>(); }
 public class EquipmentTracker { public ThingWithComps Primary; }
 public class Pawn:Thing { public RaceProperties RaceProps=new RaceProperties(); public string LabelShortCap="pawn";public bool Dead,Downed,Drafted,InMentalState,Emergency,ChildDenied;public int DevelopmentalStage;public bool NativeEligible=true,RouteAllowed=true;public Lord Lord;public Job CurJob;public JobTracker jobs=new JobTracker();public ApparelTracker apparel=new ApparelTracker();public EquipmentTracker equipment=new EquipmentTracker();public PawnMindState mindState=new PawnMindState(); }
 public class RaceProperties { public bool Humanlike=true;public object body; }
 public class PawnMindState { public PawnDuty duty; }
 public static class WorldExtensions { public static Building GetEdifice(this IntVec3 c,Map m)=>m.Buildings.TryGetValue(c,out var b)?b:null;public static bool IsForbidden(this Thing t,Pawn p)=>t.Forbidden;public static bool IsBurning(this Thing t)=>t.Burning;public static bool CanReach(this Pawn p,LocalTargetInfo t,PathEndMode e,Danger d)=>p.RouteAllowed&&(t.Thing?.Reachable??true);public static Lord GetLord(this Pawn p)=>p.Lord; }
 public struct LocalTargetInfo { public Thing Thing;private IntVec3 cell;public IntVec3 Cell=>Thing?.Position??cell;public bool IsValid=>Thing!=null||cell.IsValid;public bool HasThing=>Thing!=null;public LocalTargetInfo(Thing t){Thing=t;cell=IntVec3.Invalid;}public LocalTargetInfo(IntVec3 c){Thing=null;cell=c;}public static LocalTargetInfo Invalid=>new LocalTargetInfo(IntVec3.Invalid);public static implicit operator LocalTargetInfo(Thing t)=>new LocalTargetInfo(t); }
 public enum Danger { Some }
 public static class Find { public static TickManager TickManager=new TickManager(); }
 public class TickManager { public int TicksGame; }
 public enum LoadSaveMode { Inactive,Saving,LoadingVars,PostLoadInit }
 public static class Scribe { public static LoadSaveMode mode; }
 public static class Scribe_Values { static bool saved;public static void Look(ref bool b,string name,bool def){if(Scribe.mode==LoadSaveMode.Saving)saved=b;if(Scribe.mode==LoadSaveMode.LoadingVars)b=saved;} }
}
namespace Verse.AI {
 public static class ReservationUtility { public static bool CanReserveSittableOrSpot(Pawn p,IntVec3 c,bool ignoreOtherReservations=false)=>true; }
 public enum PathEndMode { OnCell,ClosestTouch,Touch }
 public class JobDef { public string defName; }
 public class Job { public JobDef def=JobDefOf.Wait;public LocalTargetInfo targetA=LocalTargetInfo.Invalid,targetB=LocalTargetInfo.Invalid,targetC=LocalTargetInfo.Invalid;public List<LocalTargetInfo> targetQueueA,targetQueueB;public bool Owned,playerForced;public string Denied; }
 public class QueuedJob { public Job job; }
 public class JobTracker { public List<QueuedJob> jobQueue=new List<QueuedJob>(); }
 public class PawnDuty { public LocalTargetInfo focus=LocalTargetInfo.Invalid,focusSecond=LocalTargetInfo.Invalid; }
}
namespace Verse.AI.Group {
 public class Lord { public LordJob_Ritual LordJob;public List<Pawn> ownedPawns=new List<Pawn>(); }
 public struct TriggerSignal {}
 public abstract class TriggerFilter { public abstract bool AllowActivation(Lord l,TriggerSignal s); }
 public class Trigger { public List<TriggerFilter> filters;public bool Check(Lord l)=>filters==null||filters.All(f=>f.AllowActivation(l,new TriggerSignal())); }
}
namespace RimWorld {
 public class BuildingProperties { public bool isSittable,multiSittable; }
 public class Apparel:ThingWithComps {}
 public static class EquipmentUtility { public static bool CanEquip(Thing t,Pawn p)=>t.Equippable; }
 public static class JobDefOf { public static JobDef Wear=new JobDef{defName="Wear"},Equip=new JobDef{defName="Equip"},Goto=new JobDef{defName="Goto"},RemoveApparel=new JobDef{defName="RemoveApparel"},HaulToCell=new JobDef{defName="HaulToCell"},HaulToContainer=new JobDef{defName="HaulToContainer"},Wait=new JobDef{defName="Wait"}; }
 public class RitualRoleAssignments { public List<Pawn> Participants=>Pawns.ToList(); public HashSet<Pawn> Pawns=new HashSet<Pawn>();public bool PawnParticipating(Pawn p)=>Pawns.Contains(p); }
 public class LordJob_Ritual { public Lord lord;public Map Map;public object Ritual=new object();public bool cancelled;public int StageIndex,TicksPassed,StageTicks,BehaviorTicks;public float TicksPassedWithProgress;public string RitualLabel=>"Role change";public TargetInfo selectedTarget;public RitualRoleAssignments assignments=new RitualRoleAssignments();public float VoluntaryJoinPriorityFor(Pawn p)=>p.NativeEligible?1:0;
  [MethodImpl(MethodImplOptions.NoInlining)]public void LordJobTick(){TicksPassed++;StageTicks++;BehaviorTicks++;}
  [MethodImpl(MethodImplOptions.NoInlining)]public string GetReport(Pawn p)=>"Attending ritual";
  [MethodImpl(MethodImplOptions.NoInlining)]public void ExposeData(){}
 }
 public class LordToil_Ritual { public LordJob_Ritual ritual;public int AttendanceTicks;[MethodImpl(MethodImplOptions.NoInlining)]public void LordToilTick(){AttendanceTicks++;ritual.TicksPassedWithProgress++;} }
 public abstract class StageEndTrigger { public abstract Trigger MakeTrigger(); }
 public class StageEndTrigger_TestArrival:StageEndTrigger { [MethodImpl(MethodImplOptions.NoInlining)]public override Trigger MakeTrigger()=>new Trigger(); }
}
namespace AutomaticOutfitManager.Rules { public class ApparelRule { public List<ThingDef> RequiredApparel=new List<ThingDef>(); public Area ChangingArea;public bool HasWeaponRequirement;public bool Allows(Thing t)=>true;public string Id;public Area Area;public bool AllowChildren,Enabled=true,WorkAllowed=true,WorkAreaPaused; } }
namespace AutomaticOutfitManager.State {
 public enum ApparelTransition { Preparing,Active,ReturningToChangingArea,Restoring }
 public class SavedNonWorkOutfit { public List<Apparel> Apparel=new List<Apparel>();public ThingWithComps Weapon; }
 public static class NonWorkOutfitPolicy { public static SavedNonWorkOutfit Target(Pawn p,ApparelRule r)=>new SavedNonWorkOutfit(); }
 public class PawnApparelState { public ApparelTransition Transition;public bool WeaponRuleOverrideExplicit; public bool NativeControlSuspended,DownedTransitionSuspended,DraftedTransitionSuspended,MapDepartureRequested,RecallRequested,WeaponRestorationRequested;public string ActiveRuleId,NonWorkRestorationRuleId;public List<string> CurrentRuleIds=new List<string>();public Job PendingWorkJob;public List<Apparel> ManagedApparel=new List<Apparel>(),OriginalApparel=new List<Apparel>();public List<ThingWithComps> ManagedWeapons=new List<ThingWithComps>();public ThingWithComps OriginalWeapon; }
}
namespace AutomaticOutfitManager.Core {
 public class AutomaticOutfitManagerGameComponent { public static AutomaticOutfitManagerGameComponent Current;public Dictionary<Pawn,PawnApparelState> States=new Dictionary<Pawn,PawnApparelState>();public List<ApparelRule> Rules=new List<ApparelRule>();public PawnApparelState StateFor(Pawn p)=>States.TryGetValue(p,out var s)?s:null;public ApparelRule RuleById(string id)=>Rules.FirstOrDefault(r=>r.Id==id); }
 public static class AomLog { public static bool DetailedEnabled=true;public static void Detailed(string s){} }
}
namespace AutomaticOutfitManager.Detection {
 public static class GearRetrievalRoute { public static PathEndMode EndMode(Thing t)=>t is Apparel?PathEndMode.OnCell:PathEndMode.ClosestTouch;public static bool CanReach(Pawn p,Thing t,List<ApparelRule> r)=>t.ProtectedRoute; }
 public class CombinedWeaponRequirement { public bool Matches(Thing t)=>true; }
 public static class ApparelCompatibility { public static object FindConflict(List<ApparelRule> r,object b,Pawn p)=>null; }
 public static class ApparelFinder { public static Apparel FindBest(Pawn p,ThingDef d,Area a=null,ISet<Thing> excludedThings=null,IEnumerable<ApparelRule> standards=null,Predicate<Apparel> candidateAllowed=null)=>p.Map.Stock.FirstOrDefault(t=>t.def==d&&(candidateAllowed==null||candidateAllowed(t))); }
 public static class WeaponFinder { public static ThingWithComps FindBest(Pawn p,CombinedWeaponRequirement r,Area a=null,Predicate<ThingWithComps> candidateAllowed=null)=>null; }
 public static class RuleEvaluator {
 public static bool RuleCanApplyToPawn(Pawn p,ApparelRule r)=>true;
 public static bool HasMissingRequiredGear(Pawn p,ApparelRule r)=>r.RequiredApparel.Any(d=>!p.apparel.WornApparel.Any(t=>t.def==d));
 public static bool SelectedNonWorkOutfitConflicts(Pawn p,List<ApparelRule> r)=>false;
 public static bool SavedNonWorkOutfitConflicts(Pawn p,List<ApparelRule> r)=>false;
 public static bool TryCombinedWeaponRequirement(List<ApparelRule> r,out CombinedWeaponRequirement w,Pawn p){w=new CombinedWeaponRequirement();return true;}
 public static bool UsesSavedNonWorkOutfit(Pawn p,ApparelRule r)=>false;
 public static IEnumerable<ThingDef> RequiredApparelFor(Pawn p,ApparelRule r)=>r.RequiredApparel;
 public static IEnumerable<ApparelRule> EnabledRulesForMap(Map m)=>AutomaticOutfitManagerGameComponent.Current.Rules.Where(r=>r.Enabled&&r.Area.Map==m); }
}
namespace AutomaticOutfitManager.Patches {
 public static class NativeRuleControl { public static bool Suspends(Pawn p,Job j)=>p.Dead||p.Downed||p.Drafted||p.InMentalState||p.Emergency; }
 public static class PawnPathFollower_ProtectedArea_Patch { public static bool IsManagedTransitionJob(Pawn p,Job j,PawnApparelState s)=>j.Owned;public static bool ManagedTransitionMayEnterRule(Pawn p,Job j,PawnApparelState s,ApparelRule r)=>p.RouteAllowed; }
 public static class ProtectedPathAvoidance { public static bool SegmentAvoidsRules(Pawn p,IntVec3 start,LocalTargetInfo end,List<ApparelRule> rules,PathEndMode exactEndMode)=>p.RouteAllowed; }
 public static class ReservationUtility_SavedApparel_Patch { public static bool CanReserveForOutfit(Pawn p,Thing t)=>t.Reservable; }

 public static class PausedAreaWorkFilter { public static bool WorkAllowedFor(ApparelRule r,Pawn p)=>r.WorkAllowed;public static ApparelRule DeniedActivityRule(Pawn p,Job j)=>j.Denied=="Activity"?new ApparelRule():null;public static ApparelRule DeniedHaulingRule(Pawn p,Job j)=>j.Denied=="Haul"?new ApparelRule():null;public static ApparelRule DeniedPausedAreaRule(Pawn p,Job j)=>j.Denied=="Pause"?new ApparelRule():null;public static bool ShouldRejectWanderingJob(Pawn p,Job j)=>j.Denied=="Wander"; }
}
