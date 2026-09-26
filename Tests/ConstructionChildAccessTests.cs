using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

// Native map/path sinks are deterministic. The scanner and two-pass picker
// retain the installed native call ordering verified in the companion probe.
namespace Verse
{
    public enum DevelopmentalStage { Baby, Child, Adult }
    public class RaceProperties { public bool Humanlike = true; }
    public class Map
    {
        public List<ApparelRule> Rules = new List<ApparelRule>();
        public HashSet<int> Blocked = new HashSet<int>(), Unreachable = new HashSet<int>(), UnsafeRoute = new HashSet<int>();
        public bool ThrowPath;
        public int Probes;
        public HashSet<string> UnsafeLegs = new HashSet<string>();
    }
    public struct IntVec3
    {
        public int x; public IntVec3(int value) { x=value; }
        public bool IsValid => x>=0; public static IntVec3 Invalid => new IntVec3(-1);
        public bool InBounds(Map map) => x>=0 && x<100;
        public bool WalkableBy(Map map,Pawn pawn) => InBounds(map) && !map.Blocked.Contains(x);
    }
    public class CellRect : List<IntVec3> { public CellRect ExpandedBy(int size)=>this; }
    public class Thing
    {
        public string ThingID="test"; public Map Map; public bool Spawned=true; public IntVec3 Position=new IntVec3(50);
        public List<IntVec3> Adjacent=new List<IntVec3>();
        public CellRect OccupiedRect() { var cells=new CellRect{Position};cells.AddRange(Adjacent);return cells; }
    }
    public class Pawn : Thing
    {
        public RaceProperties RaceProps=new RaceProperties(); public DevelopmentalStage DevelopmentalStage=DevelopmentalStage.Child;
        public string LabelShortCap="Child"; public CarryTracker carryTracker=new CarryTracker();
        public Job CurJob; public bool Managed=true, Drafted, Downed, InMentalState, Dead, Escape;
        public bool CanReach(LocalTargetInfo target,PathEndMode mode,Danger danger)=>!Map.Unreachable.Contains(target.Cell.x);
    }
    public class CarryTracker { public Thing CarriedThing; }
    public class Area { public Map Map; public HashSet<int> Cells=new HashSet<int>();public bool this[IntVec3 c]=>Cells.Contains(c.x); }
    public enum Danger { Deadly }
    public struct LocalTargetInfo
    {
        public Thing Thing; private IntVec3 cell; public IntVec3 Cell=>Thing?.Position??cell;
        public static implicit operator LocalTargetInfo(Thing t)=>new LocalTargetInfo{Thing=t};
        public static implicit operator LocalTargetInfo(IntVec3 c)=>new LocalTargetInfo{cell=c};
    }
    public static class GenAdj { public static IEnumerable<IntVec3> CellsAdjacent8Way(Thing t)=>t.Adjacent; }
}
namespace Verse.AI
{
    public enum PathEndMode { OnCell, Touch }
    public class ThinkNode { }
    public class JobDef { public string defName="HaulToContainer"; }
    public class Job { public int loadID=1;public ThinkNode jobGiver; public JobDef def;public bool playerForced;public LocalTargetInfo targetA,targetB;public List<LocalTargetInfo> targetQueueA,targetQueueB; }
}
namespace AutomaticOutfitManager.Rules
{
    public class ApparelRule { public string Name="Child area";public bool Enabled=true,AllowChildren;public Area Area; }
}
namespace AutomaticOutfitManager.Detection
{
    public static class RuleEvaluator {
        public static bool JobTargetsArea(Job j,Area a) =>
            new[]{j.targetA,j.targetB}.Concat(j.targetQueueA??new List<LocalTargetInfo>()).Concat(j.targetQueueB??new List<LocalTargetInfo>())
                .Any(t=>t.Thing!=null && t.Thing.Spawned && a[t.Cell]);
        public static IEnumerable<ApparelRule> EnabledRulesForMap(Map m)=>m.Rules.Where(r=>r.Enabled && r.Area?.Map==m); }
}
namespace AutomaticOutfitManager.Patches
{
    public static partial class PausedAreaWorkFilter {
        public static bool IsManagedPawn(Pawn p)=>p.Managed;
        static bool HasNativeActivityOverride(Pawn p,Job j)=>j.playerForced||NativeRuleControl.Suspends(p,j);
        static bool IsHaulingOnlyJob(Job j)=>true;
        static bool IsConstructionMaterialDelivery(Job j)=>j.def==JobDefOf.HaulToContainer;
        static bool IsRestrictedRoamingJob(Pawn p,Job j,ThinkNode n)=>false;
        static bool ActivityRestrictedFor(ApparelRule r,Pawn p,Job j)=>ChildAreaAccessPolicy.Disallows(p,r);
        static bool IsPermittedMaterialCollection(Pawn p,Job j,ApparelRule r)=>false;
        static bool IsPermittedHaulingContinuation(AutomaticOutfitManager.State.PawnApparelState s,ApparelRule r,Job j)=>false;
        static bool IsOwnedAccessEgress(Pawn p,Job j,ApparelRule r)=>false;
        static bool ShouldAllowEssentialActivityFallback(Pawn p,Job j,List<ApparelRule> r)=>false;
    }
    public static class RestActivityPolicy { public static bool Preserves(AutomaticOutfitManager.State.PawnApparelState s,ApparelRule r,Job j)=>false; }
    public static class NativeRuleControl { public static bool Suspends(Pawn p,Job j)=>p.Drafted||p.Downed||p.InMentalState||p.Dead||p.Escape; }
    public static class ProtectedPathAvoidance
    {
        // Generic Touch route reproduces the observed carried-phase rejection;
        // native exact-cell selection has a viable exterior route in this map.
        public static bool JobPathCrossesArea(Pawn p,Job j,Area a)=>p.carryTracker.CarriedThing!=null;
        public static bool RouteRequiresRestrictedArea(Pawn p,Job j,IEnumerable<ApparelRule> r)=>true;
        public static bool SegmentAvoidsRules(Pawn p,IntVec3 start,LocalTargetInfo dest,List<ApparelRule> rules,
            Predicate<IntVec3> unsafeCell=null,PathEndMode? exactEndMode=null,bool allowInitialEgress=false)
        { p.Map.Probes++;if(p.Map.ThrowPath)throw new Exception("path sink failure");return !p.Map.UnsafeRoute.Contains(dest.Cell.x) && !p.Map.UnsafeLegs.Contains(start.x+":"+dest.Cell.x); }
    }
}
namespace AutomaticOutfitManager.State { public class PawnApparelState { } }
namespace AutomaticOutfitManager.Core
{
    public static class AomLog {
        public static bool DetailedEnabled=false;
        public static bool ShouldLogDetailed(Pawn p,string key,int ticks)=>true;
        public static void Detailed(string text) { }
    }
    public class AutomaticOutfitManagerGameComponent {
        public static AutomaticOutfitManagerGameComponent Current;
        public List<ApparelRule> Rules;
        public AutomaticOutfitManager.State.PawnApparelState StateFor(Pawn p)=>null;
    }
}
namespace RimWorld
{
    public class Frame : Thing { } public class Blueprint : Thing { }
    public static class JobDefOf { public static JobDef HaulToContainer=new JobDef(),FinishFrame=new JobDef(); }
    public static class RCellFinder
    {
        public static HashSet<int> NotGood=new HashSet<int>();
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool TryFindGoodAdjacentSpotToTouch(Pawn pawn,Thing target,out IntVec3 result)
        {
            // First pass: native picks the closest good, reachable touching cell.
            int best=int.MaxValue;result=IntVec3.Invalid;
            foreach(IntVec3 cell in GenAdj.CellsAdjacent8Way(target))
            {
                if(NotGood.Contains(cell.x)||!cell.WalkableBy(pawn.Map,pawn)||!pawn.CanReach(cell,PathEndMode.OnCell,Danger.Deadly))continue;
                int distance=Math.Abs(cell.x-pawn.Position.x);
                if(distance<best){best=distance;result=cell;}
            }
            if(result.IsValid)return true;
            // Native's fallback enumerates adjacent cells again in random order,
            // checking WalkableBy and CanReach, then falls back to the footprint.
            foreach(IntVec3 cell in GenAdj.CellsAdjacent8Way(target))
                if(cell.WalkableBy(pawn.Map,pawn)&&pawn.CanReach(cell,PathEndMode.OnCell,Danger.Deadly)){result=cell;return true;}
            result=target.Position;return false;
        }
    }
    public class WorkGiver_ConstructDeliverResources
    {
        public Job Candidate;public int Calls;
        [MethodImpl(MethodImplOptions.NoInlining)]
        protected Job ResourceDeliverJobFor(Pawn pawn,Thing target,bool removeFloor,bool forced){Calls++;return Candidate;}
        public bool HasJobOnThing(Pawn pawn,Thing target,bool forced=false)=>ResourceDeliverJobFor(pawn,target,false,forced)!=null;
        public Job JobOnThing(Pawn pawn,Thing target,bool forced=false)=>ResourceDeliverJobFor(pawn,target,false,forced);
    }
}
internal static class ConstructionChildAccessTests
{
    static int count;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;}
    static ApparelRule Rule(Map map,params int[] cells){var r=new ApparelRule{Area=new Area{Map=map,Cells=new HashSet<int>(cells)}};map.Rules.Add(r);return r;}
    static Job Delivery(Thing target)=>new Job{def=JobDefOf.HaulToContainer,targetB=target};
    static void Run(bool previous)
    {
        var harmony=new Harmony("aom.tests.child.construction");
        if(!previous){harmony.CreateClassProcessor(typeof(ConstructionChildDestination_Patch)).Patch();harmony.CreateClassProcessor(typeof(ConstructionChildDelivery_Patch)).Patch();}
        foreach(bool blueprint in new[]{false,true})
        {
            var map=new Map();var pawn=new Pawn{Map=map,Position=new IntVec3(40)};
            Thing target=blueprint?(Thing)new Blueprint{Map=map}:new Frame{Map=map};
            target.Adjacent.AddRange(new[]{new IntVec3(41),new IntVec3(55)});
            var rule=Rule(map,41,50,55);var job=Delivery(target);
            var scanner=new WorkGiver_ConstructDeliverResources{Candidate=job};
            Check(!scanner.HasJobOnThing(pawn,target),"restricted child does not accept impossible boundary delivery");
            Check(scanner.JobOnThing(pawn,target)==null,"job creation agrees with HasJob before pickup");
            Check(scanner.Calls==2,"each scanner stage generates once");
            Check(pawn.CurJob==null && job.targetB.Thing==target,"prospective search does not replace current job or recipient");
            rule.Area.Cells.Remove(55);
            Check(scanner.HasJobOnThing(pawn,target)&&scanner.JobOnThing(pawn,target)==job,"farther legal approach keeps delivery available");
            pawn.CurJob=job;
            Check(RCellFinder.TryFindGoodAdjacentSpotToTouch(pawn,target,out IntVec3 chosen)&&chosen.x==55,"actual movement picks permitted side instead of nearer denied side");
            RCellFinder.NotGood.Add(55);
            Check(RCellFinder.TryFindGoodAdjacentSpotToTouch(pawn,target,out chosen)&&chosen.x==55,"native fallback pass also filters forbidden side");
            RCellFinder.NotGood.Clear();
            map.UnsafeRoute.Add(55);
            Check(!scanner.HasJobOnThing(pawn,target),"outside endpoint with route through denied area is rejected");map.UnsafeRoute.Clear();
            map.Unreachable.Add(55);Check(!scanner.HasJobOnThing(pawn,target),"unreachable exterior cell is not an alternative");map.Unreachable.Clear();
            map.Blocked.Add(55);Check(!scanner.HasJobOnThing(pawn,target),"blocked exterior cell is not an alternative");map.Blocked.Clear();
            var overlap=Rule(map,55);Check(!scanner.HasJobOnThing(pawn,target),"all denied overlaps must allow the approach");overlap.Enabled=false;
            Check(scanner.HasJobOnThing(pawn,target),"disabling overlap takes effect without stale rejection");
            rule.AllowChildren=true;
            Check(RCellFinder.TryFindGoodAdjacentSpotToTouch(pawn,target,out chosen)&&chosen.x==41,"Allow Children immediately restores native nearest cell");
            rule.AllowChildren=false;
            foreach(DevelopmentalStage stage in new[]{DevelopmentalStage.Baby,DevelopmentalStage.Adult}){pawn.DevelopmentalStage=stage;Check(RCellFinder.TryFindGoodAdjacentSpotToTouch(pawn,target,out chosen)&&chosen.x==41,"non-child remains native");}
            pawn.DevelopmentalStage=DevelopmentalStage.Child;
            pawn.RaceProps.Humanlike=false;Check(RCellFinder.TryFindGoodAdjacentSpotToTouch(pawn,target,out chosen)&&chosen.x==41,"mech/animal life stage does not trigger child gate");pawn.RaceProps.Humanlike=true;
            job.playerForced=true;Check(scanner.HasJobOnThing(pawn,target)&&RCellFinder.TryFindGoodAdjacentSpotToTouch(pawn,target,out chosen)&&chosen.x==41,"forced job remains native");job.playerForced=false;
            rule.Area.Cells.Add(55);Check(scanner.HasJobOnThing(pawn,target,true)&&scanner.JobOnThing(pawn,target,true)==job,"forced scanner ignores prospective access gate");rule.Area.Cells.Remove(55);
            foreach(Action<bool> flag in new Action<bool>[] {v=>pawn.Drafted=v,v=>pawn.Downed=v,v=>pawn.InMentalState=v,v=>pawn.Dead=v,v=>pawn.Escape=v,v=>pawn.Managed=!v}){flag(true);Check(RCellFinder.TryFindGoodAdjacentSpotToTouch(pawn,target,out chosen)&&chosen.x==41,"native control/unmanaged pawn stays native");flag(false);}
            pawn.CurJob=new Job{def=new JobDef()};Check(RCellFinder.TryFindGoodAdjacentSpotToTouch(pawn,target,out chosen)&&chosen.x==41,"unrelated native touch search stays native");
            pawn.CurJob=new Job{def=JobDefOf.FinishFrame,targetA=target};Check(RCellFinder.TryFindGoodAdjacentSpotToTouch(pawn,target,out chosen)&&chosen.x==55,"current frame finishing chooses legal approach");
            pawn.CurJob=null;
            var deniedSecond=new Frame{Map=map,Position=new IntVec3(60),Adjacent=new List<IntVec3>{new IntVec3(61)}};
            Rule(map,60,61);job.targetQueueB=new List<LocalTargetInfo>{deniedSecond};Check(!scanner.HasJobOnThing(pawn,target),"queued recipient is validated before pickup");job.targetQueueB=null;
            map.ThrowPath=true;bool threw=false;try{scanner.HasJobOnThing(pawn,target);}catch(Exception){threw=true;}map.ThrowPath=false;
            Check(threw,"fixture exercises exceptional native search exit");Check(RCellFinder.TryFindGoodAdjacentSpotToTouch(pawn,target,out chosen)&&chosen.x==41,"search context restored after exception");
            // GotoBuild uses the target cell when the adjacent picker returns false.
            target.Adjacent.Clear();target.Adjacent.Add(new IntVec3(41));rule.Area.Cells.Remove(50);
            Check(scanner.HasJobOnThing(pawn,target),"legal native footprint fallback remains usable");
            map.Blocked.Add(50);Check(!scanner.HasJobOnThing(pawn,target),"blocked footprint cannot rescue an impossible adjacent search");map.Blocked.Clear();
            rule.Area.Cells.Add(50);Check(!scanner.HasJobOnThing(pawn,target),"forbidden footprint fallback cannot bypass access");
            rule.Enabled=false;int probes=map.Probes;Check(scanner.HasJobOnThing(pawn,target)&&map.Probes==probes,"disabled rules require no path probes");
            target.Map=new Map();Check(scanner.HasJobOnThing(pawn,target),"foreign-map target left to native validity checks");
        }
        AdmissionChecks();
        Console.WriteLine("PASS: "+count+" construction child access checks.");
    }
    static void AdmissionChecks()
    {
        var map=new Map();var pawn=new Pawn{Map=map,Position=new IntVec3(10)};
        var rule=Rule(map,41);
        AutomaticOutfitManager.Core.AutomaticOutfitManagerGameComponent.Current=
            new AutomaticOutfitManager.Core.AutomaticOutfitManagerGameComponent{Rules=map.Rules};
        var frame=new Frame{Map=map,Position=new IntVec3(50),Adjacent=new List<IntVec3>{new IntVec3(41),new IntVec3(55)}};
        var source=new Thing{Map=map,Position=new IntVec3(20)};
        var job=Delivery(frame);job.targetA=source;
        var scanner=new WorkGiver_ConstructDeliverResources{Candidate=job};
        Check(scanner.HasJobOnThing(pawn,frame)&&scanner.JobOnThing(pawn,frame)==job,"legal exterior delivery survives both native scanner stages");
        Check(PausedAreaWorkFilter.DeniedActivityRule(pawn,job)==null,"pre-pickup activity admission permits exterior delivery");
        // Native job starts, splits/picks up material, then the periodic guard runs.
        pawn.CurJob=job;source.Spawned=false;pawn.carryTracker.CarriedThing=source;
        Check(PausedAreaWorkFilter.DeniedActivityRule(pawn,job)==null,"carried-phase runtime retains legal exterior delivery");
        Check(RCellFinder.TryFindGoodAdjacentSpotToTouch(pawn,frame,out IntVec3 cell)&&cell.x==55,"retained job executes native legal-side movement");
        var next=new Thing{Map=map,Position=new IntVec3(22)};job.targetQueueA=new List<LocalTargetInfo>{next};
        map.UnsafeLegs.Add("10:22");
        Check(ConstructionChildAccess.RejectsDelivery(pawn,job)&&PausedAreaWorkFilter.DeniedActivityRule(pawn,job)!=null,"remaining spawned pickup is checked while first stack is carried");
        map.UnsafeLegs.Clear();map.UnsafeLegs.Add("22:55");map.Blocked.Add(50);
        Check(ConstructionChildAccess.RejectsDelivery(pawn,job),"route from future pickup to construction is checked before collection");
        map.UnsafeLegs.Clear();map.Blocked.Clear();job.targetQueueA=null;
        source.Spawned=true;pawn.carryTracker.CarriedThing=null;map.UnsafeLegs.Add("10:20");
        Check(!scanner.HasJobOnThing(pawn,frame)&&scanner.JobOnThing(pawn,frame)==null,"impossible pickup route is rejected before native pickup");
        map.UnsafeLegs.Clear();
        rule.AllowChildren=true;rule.Area.Cells.Add(55);rule.Area.Cells.Add(50);
        Check(scanner.HasJobOnThing(pawn,frame),"allowed child can select interior-only construction");
        pawn.CurJob=job;source.Spawned=false;pawn.carryTracker.CarriedThing=source;rule.AllowChildren=false;
        Check(PausedAreaWorkFilter.DeniedActivityRule(pawn,job)!=null,"disallow toggle revokes already selected interior work");
        Check(!scanner.HasJobOnThing(pawn,frame),"new interior-only delivery is rejected after disallow toggle");
        var unrelated=Rule(map,88);map.Rules.Remove(unrelated);map.Rules.Insert(0,unrelated);
        Check(ConstructionChildAccess.TryGetRouteRestriction(pawn,job,out ApparelRule denied)&&denied==rule,
            "denial identifies blocking boundary rule instead of unrelated first rule");
        map.Rules.Remove(unrelated);
        rule.Area.Cells.Remove(50);rule.Area.Cells.Remove(55);
        Check(PausedAreaWorkFilter.DeniedActivityRule(pawn,job)==null,"restoring legal exterior route takes effect immediately");
        pawn.Position=new IntVec3(41);
        Check(PausedAreaWorkFilter.DeniedActivityRule(pawn,job)!=null,"inside child still reaches existing exit enforcement");
        pawn.Position=new IntVec3(10);rule.Area.Cells.Add(20);source.Spawned=true;pawn.carryTracker.CarriedThing=null;
        Check(PausedAreaWorkFilter.DeniedActivityRule(pawn,job)!=null,"direct protected material source is never bypassed");
        rule.Area.Cells.Remove(20);job.playerForced=true;
        Check(PausedAreaWorkFilter.DeniedActivityRule(pawn,job)==null,"explicit forced order retains native control");
    }
    public static int Main(string[] args){try{Run(args.Contains("--previous"));return 0;}catch(Exception e){Console.Error.WriteLine("FAIL: "+e.GetBaseException().Message);return 1;}}
}
