// Production UI selection + production assignment predicates. API doubles
// provide cells and status text; they do not simulate native pathfinding/UI.
using System;
using System.Collections.Generic;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.UI;
using RimWorld;
using Verse;
using Verse.AI;

internal static class ObservedTransitionContractTests
{
    static int passed;
    static Pawn pawn;
    static PawnApparelState state;
    static ApparelRule visited;
    static Apparel vest;
    static void Check(bool value, string message)
    { if (!value) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
    static void Setup()
    {
        var map = new Map();
        pawn = new Pawn { Map=map, Position=new IntVec3(1) };
        vest = new Apparel { Position=new IntVec3(2), Map=map };
        pawn.CurJob = new Job { def=JobDefOf.Wear, targetA=vest, playerForced=true };
        state = new PawnApparelState { Pawn=pawn, Transition=ApparelTransition.Restoring };
        state.OriginalApparel.Add(vest);
        visited = new ApparelRule { Area=new Area { Map=map } };
        visited.Area.Cells.Add(2);
        PawnAutomaticOutfitStatus.Calls=0;
    }
    static string Status()=>ObservedOutfitTransition.Build(pawn,state,pawn.CurJob,visited);
    static void Positive(string message)=>Check(Status()==PawnAutomaticOutfitStatus.OwningStatus,message);
    static void Negative(string message)=>Check(Status()==null,message);
    static void Run()
    {
        Setup(); Positive("Reba's exact forced saved Wear reports the owning restoration in a visited area");
        Check(state.OriginalApparel.Contains(vest) && state.Transition==ApparelTransition.Restoring,"display preserves saved ownership and transition");
        Check(!Status().Contains("Buffer: 0/3"),"visited area's buffer is not substituted for the owner's status");
        Setup(); pawn.Position=new IntVec3(2); vest.Position=new IntVec3(3);
        Positive("assigned restoration already inside the area keeps its progress/egress description");
        Setup(); visited.Area.Cells.Clear(); Negative("historical source with neither current pawn nor job is not listed");
        Check(PawnAutomaticOutfitStatus.Calls==0,"unrelated areas do not build transition status");
        Setup(); visited.Area.Map=new Map(); Negative("same coordinates on another map do not qualify");
        Setup(); state.OriginalApparel.Clear(); Negative("unrelated player-forced apparel keeps ordinary activity classification");
        Setup(); pawn.CurJob.def=new JobDef(); Negative("ordinary work on saved apparel is not an outfit transition");
        Setup(); state.Transition=ApparelTransition.Active; Negative("active sessions do not blanket hide untracked work elsewhere");
        Setup(); state.Transition=ApparelTransition.Preparing; Negative("saved personal gear is not automatically preparation gear");
        state.ManagedApparel.Add(vest); Positive("exact preparation garment uses the current owning status");
        Setup(); state.OriginalApparel.Clear(); state.ManagedApparel.Add(vest); pawn.CurJob.def=JobDefOf.RemoveApparel;
        Positive("exact managed-apparel removal is a transition");
        state.ManagedApparel.Clear(); Negative("unassigned removal is not hidden");
        Setup(); var weapon=new ThingWithComps { def=new ThingDef{IsWeapon=true},Map=pawn.Map,Position=new IntVec3(2) };
        pawn.CurJob=new Job {def=JobDefOf.Equip,targetA=weapon}; state.OriginalWeapon=weapon; state.WeaponRestorationRequested=true;
        Positive("exact saved primary restoration is recognized");
        pawn.CurJob.playerForced=true; Negative("explicit weapon override stays outside automatic transition observation");
        pawn.CurJob.playerForced=false; state.WeaponRestorationRequested=false; Negative("unrequested saved weapon is not claimed");
        state.ManagedWeapons.Add(weapon); pawn.CurJob.def=JobDefOf.DropEquipment; Positive("assigned managed weapon return is recognized");
        Setup(); state.Transition=ApparelTransition.ReturningToChangingArea; state.ChangingAreaReturnCell=new IntVec3(2);
        pawn.CurJob=new Job {def=AutomaticOutfitManagerJobDefOf.AutomaticOutfitManager_LockerReturn,targetA=new IntVec3(2)};
        Positive("exact assigned locker destination is reported without making the visited rule the owner");
        pawn.CurJob.targetA=new IntVec3(3); pawn.Position=new IntVec3(2); Negative("ordinary travel to a different cell is not called a locker return");
        Setup(); vest.Destroyed=true; Negative("destroyed target is not advertised as an active restoration");
        Setup(); pawn.Drafted=true; Negative("draft override preserves native activity display");
        Setup(); pawn.Downed=true; Negative("downed pawn is not shown performing a stale outfit job");
        Setup(); state.Pawn=new Pawn(); Negative("another pawn's session cannot claim the current job");
        Setup(); var queued=new Job{def=JobDefOf.Wear,targetA=vest};
        Check(ObservedOutfitTransition.Build(pawn,state,queued,visited)==null,"queued future item is not displayed as current restoration");
        Setup(); pawn.RaceProps.Animal=true; Negative("animals remain outside Worker/Occupant transition rows");
        Setup(); pawn.RaceProps.Humanlike=false; Negative("robot activity keeps its existing row classification");
        Setup(); state=null; Negative("untracked pawn cannot invent an outfit session");
        Setup(); pawn.CurJob=null; Negative("missing current job cannot invent restoration activity");
        Console.WriteLine("PASS " + passed + " observed transition contracts; native UI validation remains pending.");
    }
    static int Main(){try{Run();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}

namespace Verse {
 public class Map {}
 public struct IntVec3 { public int Value;public IntVec3(int value){Value=value;}public bool IsValid=>Value>0;public bool InBounds(Map map)=>IsValid;public static bool operator==(IntVec3 a,IntVec3 b)=>a.Value==b.Value;public static bool operator!=(IntVec3 a,IntVec3 b)=>a.Value!=b.Value;public override bool Equals(object o)=>o is IntVec3 p && this==p;public override int GetHashCode()=>Value; }
 public class Area {public Map Map;public HashSet<int> Cells=new HashSet<int>();public bool this[IntVec3 cell]=>Cells.Contains(cell.Value);}
 public class ThingDef {public bool IsWeapon;}
 public class Thing {public bool Destroyed;public ThingDef def=new ThingDef();public Map Map;public IntVec3 Position;}
 public class ThingWithComps:Thing {}
 public class RaceProperties {public bool Humanlike=true,Animal;}
 public class Equipment {public ThingWithComps Primary;}
 public class Pawn:Thing {public RaceProperties RaceProps=new RaceProperties();public object apparel=new object();public bool Drafted,Downed;public Verse.AI.Job CurJob;public Equipment equipment=new Equipment();}
 public struct LocalTargetInfo {public Thing Thing;public IntVec3 Cell=>Thing?.Position ?? cell;private IntVec3 cell;public static implicit operator LocalTargetInfo(Thing t)=>new LocalTargetInfo{Thing=t};public static implicit operator LocalTargetInfo(IntVec3 c)=>new LocalTargetInfo{cell=c};}
}
namespace Verse.AI {public class JobDef {}public class Job {public JobDef def;public LocalTargetInfo targetA;public bool playerForced;}}
namespace RimWorld {public class Apparel:ThingWithComps {}public static class JobDefOf {public static JobDef Wear=new JobDef(),Equip=new JobDef(),RemoveApparel=new JobDef(),DropEquipment=new JobDef(),HaulToCell=new JobDef(),HaulToContainer=new JobDef(),Goto=new JobDef(),EnterPortal=new JobDef();}}
namespace AutomaticOutfitManager.State {
 public enum ApparelTransition {Preparing,Active,ReturningToChangingArea,Restoring}
 public class PawnApparelState {public Pawn Pawn;public ApparelTransition Transition;public List<Apparel> OriginalApparel=new List<Apparel>(),ManagedApparel=new List<Apparel>();public List<ThingWithComps> ManagedWeapons=new List<ThingWithComps>();public ThingWithComps OriginalWeapon;public bool WeaponRestorationRequested,WeaponPlayerOverride;public IntVec3 ChangingAreaReturnCell;public bool IsPreparationApparel(Apparel a)=>ManagedApparel.Contains(a);public bool IsManagedWeapon(ThingWithComps w)=>ManagedWeapons.Contains(w);}
}
namespace AutomaticOutfitManager.Rules {public class ApparelRule {public Area Area;}}
namespace AutomaticOutfitManager.Core {public static class AutomaticOutfitManagerJobDefOf {public static JobDef AutomaticOutfitManager_LockerReturn=new JobDef();}}
namespace AutomaticOutfitManager.Detection {public static class RuleEvaluator {public static bool JobTargetsArea(Job job,Area area)=>job.targetA.Thing!=null ? job.targetA.Thing.Map==area.Map && area[job.targetA.Cell] : area[job.targetA.Cell];}}
namespace AutomaticOutfitManager.UI {public static class PawnAutomaticOutfitStatus {public const string OwningStatus="Automatic Outfit Manager: Restoring automatic saved apparel\nRule: Kitchen\nBuffer: 0/1\nRestoring automatic saved apparel: Flak vest";public static int Calls;public static string Build(Pawn pawn){Calls++;return OwningStatus;}}}
