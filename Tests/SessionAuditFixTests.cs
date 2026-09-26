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

class SessionAuditFixTests
{
    static int passed;
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
    static int Main()
    {
        try {
            AccessOnlyConstruction();
            NonhumanTransit();
            var map = new Map();
            var dining = new ApparelRule { Id="dining", Area=new Area { Map=map } };
            var work = new ApparelRule { Id="work", Area=new Area { Map=map } };
            dining.Area.Cells.Add(8); work.Area.Cells.Add(5);
            RuleEvaluator.Rules = new List<ApparelRule> { dining, work };
            var pawn = new Pawn { Map=map, Position=1, Faction=Faction.OfPlayerSilentFail };
            var food = new Thing { MapHeld=map, Cell=1 };
            var ingest = new Job { def=JobDefOf.Ingest, targetA=food };
            pawn.CurJob=ingest; pawn.carryTracker.CarriedThing=food;
            Func<IReadOnlyList<ApparelRule>> restricted = () => ProtectedPathAvoidance.RestrictedTransitRules(pawn, ingest, new LocalTargetInfo(8));
            Check(restricted().SequenceEqual(new[]{work}), "carried meal exempts permitted dining destination but preserves unrelated work avoidance");
            Check(ProtectedPathAvoidance.RestrictedTransitRules(pawn, ingest).Count==2, "queries without an eating destination retain transit avoidance");
            Check(ProtectedPathAvoidance.RestrictedTransitRules(pawn, ingest, new LocalTargetInfo(9)).Count==2, "outside destination does not exempt dining as a shortcut");
            dining.Allowed=false; Check(restricted().Count==2,"denied eating activity cannot gain a destination exemption");
            dining.Allowed=true; dining.Missing=true; Check(restricted().SequenceEqual(new[]{work}),"unprepared carried meal reaches the destination boundary for outfit preparation without unrelated-area transit"); dining.Missing=false;
            pawn.carryTracker.CarriedThing=new Thing(); Check(restricted().Count==2,"carrying a different thing does not exempt the food job destination");
            pawn.carryTracker.CarriedThing=null; Check(restricted().Count==2,"food pickup phase does not claim an unrelated dining destination");
            pawn.carryTracker.CarriedThing=food; pawn.CurJob=new Job(); Check(restricted().Count==2,"a different job's path query cannot claim current-meal status"); pawn.CurJob=ingest;
            ingest.def=new JobDef{driverClass=typeof(JobDriver_Ingest)}; Check(restricted().Count==2,"another job definition is not treated as native Ingest"); ingest.def=JobDefOf.Ingest;
            JobDefOf.Ingest.driverClass=typeof(CustomIngest); Check(restricted().Count==2,"custom ingestion drivers keep their own destination semantics"); JobDefOf.Ingest.driverClass=typeof(JobDriver_Ingest);
            dining.Area.Map=new Map(); Check(!EatingDestination.IsCurrentMealDestination(pawn,ingest,new LocalTargetInfo(8),dining.Area),"same cell on another map is not the dining destination"); dining.Area.Map=map;
            Check(!EatingDestination.IsCurrentMealDestination(pawn,ingest,new LocalTargetInfo(-1),dining.Area),"invalid destination is rejected");
            food.Cell=8; Check(!EatingDestination.IsCurrentMealDestination(pawn,ingest,(LocalTargetInfo)food,dining.Area),"path to food itself is not a separate dining destination"); food.Cell=1;
            pawn.Faction=new Faction(); Check(restricted().Count==0,"unmanaged faction preserves native routing"); pawn.Faction=Faction.OfPlayerSilentFail;
            pawn.Drafted=true; Check(restricted().Count==0,"drafted movement retains native override"); pawn.Drafted=false;
            pawn.Position=8; dining.Allowed=false; Check(!restricted().Contains(dining),"already inside retains egress even after access is denied"); pawn.Position=1; dining.Allowed=true;
            AutomaticOutfitManagerGameComponent.Current.State=new AutomaticOutfitManager.State.PawnApparelState();
            PawnPathFollower_ProtectedArea_Patch.TransitionJob=true;
            Check(restricted().Count==2,"transition job cannot borrow eating destination exemption");
            PawnPathFollower_ProtectedArea_Patch.Owned=work;
            Check(restricted().SequenceEqual(new[]{dining}),"transition can enter only its exact owned work rule");
            PawnPathFollower_ProtectedArea_Patch.TransitionJob=false; AutomaticOutfitManagerGameComponent.Current.State=null;


            var book = new Book { MapHeld=map, Cell=1 };
            var reading = new Job { def=JobDefOf.Reading, targetA=book };
            pawn.CurJob=reading; pawn.carryTracker.CarriedThing=book;
            var reservation = new DestinationReservation { claimant=pawn, job=reading, target=8 };
            map.pawnDestinationReservationManager.Reservation=reservation;
            Func<IReadOnlyList<ApparelRule>> readRules = () => ProtectedPathAvoidance.RestrictedTransitRules(pawn,reading,new LocalTargetInfo(8));
            Check(readRules().SequenceEqual(new[]{work}),"chosen reading cell is admitted while unrelated Work transit stays blocked");
            dining.Missing=true; Check(readRules().SequenceEqual(new[]{work}),"missing reading outfit can reach the existing boundary preparation check"); dining.Missing=false;
            dining.Allowed=false; Check(readRules().Count==2,"denied reading remains blocked"); dining.Allowed=true;
            reservation.obsolete=true; Check(readRules().Count==2,"obsolete reading reservation cannot exempt a route"); reservation.obsolete=false;
            reservation.job=ingest; Check(readRules().Count==2,"other job reservation cannot claim the reading destination"); reservation.job=reading;
            reservation.claimant=new Pawn(); Check(readRules().Count==2,"another pawn reservation cannot claim reading entry"); reservation.claimant=pawn;
            reservation.target=9; Check(readRules().Count==2,"speculative chair query is not the reserved destination"); reservation.target=8;
            map.pawnDestinationReservationManager.Reservation=null; Check(readRules().Count==2,"reading search before reservation keeps avoidance"); map.pawnDestinationReservationManager.Reservation=reservation;
            pawn.carryTracker.CarriedThing=food; Check(readRules().Count==2,"carrying something other than the book is rejected"); pawn.carryTracker.CarriedThing=book;
            pawn.CurJob=ingest; Check(readRules().Count==2,"stale reading job cannot claim a destination"); pawn.CurJob=reading;
            reading.def=new JobDef{driverClass=typeof(JobDriver_Reading)}; Check(readRules().Count==2,"custom definition cannot borrow native reading semantics"); reading.def=JobDefOf.Reading;
            reading.def.driverClass=typeof(CustomReading); Check(readRules().Count==2,"custom reading driver is excluded"); reading.def.driverClass=typeof(JobDriver_Reading);
            Check(!ReadingDestination.IsCurrentDestination(pawn,reading,null,dining.Area),"no destination cannot exempt reading");
            Check(!ReadingDestination.IsCurrentDestination(pawn,reading,new LocalTargetInfo(101),dining.Area),"out-of-bounds reading is rejected");
            Check(!ReadingDestination.IsCurrentDestination(pawn,reading,(LocalTargetInfo)book,dining.Area),"book target is not a reserved reading cell");
            dining.Area.Map=new Map(); Check(readRules().Count==2,"other map reading cell is excluded"); dining.Area.Map=map;

            pawn.CurJob=ingest; pawn.carryTracker.CarriedThing=food;

            pawn.RaceProps.Animal=true; pawn.CurJob=ingest;
            Check(PawnActivityPresentation.AnimalRow(pawn,ingest)==2,"animal eating is displayed in Wanderers");
            Check(!ActivityJobClassifier.IsWandering(pawn,ingest,false),"animal eating still uses Activities permission");
            var rest=new Job{def=JobDefOf.LayDown};
            Check(PawnActivityPresentation.AnimalRow(pawn,rest)==2 && !ActivityJobClassifier.IsWandering(pawn,rest,false),"animal resting moves display without changing its access category");
            Check(PawnActivityPresentation.AnimalRow(pawn,new Job{def=JobDefOf.HaulToCell})==1,"animal hauling remains in Haulers");
            Check(PawnActivityPresentation.AnimalRow(pawn,null)==2,"idle animal without a job remains outside Workers");
            pawn.RaceProps.Animal=false;
            Check(PawnActivityPresentation.AnimalRow(pawn,new Job{def=new JobDef{defName="Repair"}})==0,"productive robot work is not blanket excluded");
            var cooking=new Job{def=new JobDef{defName="DoBill"}};
            Check(PawnActivityPresentation.Priority(cooking,false,false)==0,"active purposeful work sorts first");
            Check(PawnActivityPresentation.Priority(ingest,false,false)==0,"active meals sort alongside other activities");
            ingest.def.isIdle=true;
            Check(PawnActivityPresentation.Priority(ingest,false,false)==0,"native idle metadata cannot demote a purposeful meal");
            ingest.def.isIdle=false;
            Check(PawnActivityPresentation.Priority(rest,false,false)==3,"sleep is sorted with resting rather than active work");
            Check(PawnActivityPresentation.Priority(cooking,true,true)==1,"outfit transition takes precedence over retained buffer metadata");
            Check(PawnActivityPresentation.Priority(cooking,false,true)==2,"buffered follow-up sorts after transitions");
            Check(PawnActivityPresentation.Priority(null,false,false)==3,"no-job observation sorts last");
            Check(PawnActivityPresentation.Priority(new Job{def=JobDefOf.Wait},false,false)==3,"idle wait sorts last");
            Console.WriteLine(passed+" session audit fix checks passed; in-game routing and layout still need observation."); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    class CustomIngest : JobDriver_Ingest {}
    class CustomReading : JobDriver_Reading {}

    static void NonhumanTransit()
    {
        foreach (bool animal in new[] { false, true })
        foreach (bool nonWork in new[] { false, true })
        foreach (bool clothing in new[] { false, true })
        {
            var map = new Map();
            var rule = new ApparelRule { Id="outfit", IsNonWork=nonWork, Area=new Area { Map=map } };
            rule.Area.Cells.Add(5);
            if (clothing) rule.RequiredApparel.Add(new ThingDef());
            else rule.RequiredWeapon=WeaponRequirement.Ranged;
            var other = new ApparelRule { Id="denied", IsNonWork=nonWork, Allowed=false, Area=new Area { Map=map } };
            other.Area.Cells.Add(6);
            RuleEvaluator.Rules=new List<ApparelRule> { rule, other };
            var pawn=new Pawn { Map=map, Position=1, Faction=Faction.OfPlayerSilentFail };
            pawn.RaceProps.Humanlike=false; pawn.RaceProps.Animal=animal;
            var job=new Job { def=JobDefOf.HaulToCell, targetA=new Thing { MapHeld=map, Cell=2 }, targetB=new Thing { MapHeld=map, Cell=9 } };
            pawn.CurJob=job;
            Func<IReadOnlyList<ApparelRule>> route=()=>ProtectedPathAvoidance.RestrictedTransitRules(pawn,job);
            Check(!route().Contains(rule),"permitted nonhuman transit ignores outfit area");
            Check(route().Contains(other),"nonhuman transit preserves independently denied area");
            rule.Allowed=false;Check(route().Contains(rule),"denied nonhuman transit stays restricted");rule.Allowed=true;
            rule.WorkAreaPaused=true;Check(route().Contains(rule),"disallowed paused nonhuman transit stays restricted");rule.WorkAreaPaused=false;
            pawn.RaceProps.Humanlike=true;
            Check(route().Contains(rule),"humanlike pawn including humanoid robot retains outfit routing");
            pawn.RaceProps.Humanlike=false;
            rule.RequiredApparel.Clear();rule.RequiredWeapon=WeaponRequirement.None;
            Check(!route().Contains(rule),"nonhuman also bypasses empty or saved-personal Non-Work outfit routing");
            pawn.RaceProps.Humanlike=true;
            Check(route().Contains(rule)==nonWork,"human saved-personal Non-Work routing remains distinct from access-only Work");
            pawn.RaceProps.Humanlike=false;
            rule.RequiredApparel.Add(new ThingDef());
            job.targetA=new Thing { MapHeld=map, Cell=5 };
            Check(!route().Contains(rule),"permitted nonhuman direct delivery remains admitted");
            job.targetA=new Thing { MapHeld=map, Cell=2 };pawn.Position=5;rule.Allowed=false;
            Check(!route().Contains(rule),"nonhuman inside denied area retains egress");pawn.Position=1;rule.Allowed=true;
            PawnPathFollower_ProtectedArea_Patch.TransitionJob=true;
            Check(route().Contains(rule),"owned outfit transition does not gain unrelated shortcut");
            PawnPathFollower_ProtectedArea_Patch.TransitionJob=false;
        }
    }

    static void AccessOnlyConstruction()
    {
        var map=new Map();
        var access=new ApparelRule{Id="access",IsNonWork=false};
        access.Area=new Area{Map=map}; access.Area.Cells.Add(8);
        var ppe=new ApparelRule{Id="ppe",IsNonWork=false};
        ppe.RequiredApparel.Add(new ThingDef()); ppe.Area=new Area{Map=map};ppe.Area.Cells.Add(5);
        RuleEvaluator.Rules=new List<ApparelRule>{access,ppe};
        var pawn=new Pawn{Map=map,Position=1,Faction=Faction.OfPlayerSilentFail};
        var frame=new Frame{MapHeld=map,Cell=9};
        var job=new Job{def=JobDefOf.HaulToContainer,targetA=new Thing{MapHeld=map,Cell=1},targetB=frame};
        pawn.CurJob=job;
        Func<IReadOnlyList<ApparelRule>> route=()=>ProtectedPathAvoidance.RestrictedTransitRules(pawn,job,new LocalTargetInfo(8));
        Check(access.IsAccessOnlyWork,"empty Work rule is access-only");
        Check(route().SequenceEqual(new[]{ppe}),"allowed empty Work rule does not block construction or transit");
        access.Allowed=false; Check(route().Contains(access),"denied empty Work rule remains restricted");access.Allowed=true;
        access.WorkAreaPaused=true;Check(route().Contains(access),"paused construction remains restricted");access.WorkAreaPaused=false;
        pawn.Child=true;Check(route().Contains(access),"child denied by checkbox remains restricted");access.AllowChildren=true;
        Check(!route().Contains(access),"allowed child keeps existing bypass");pawn.Child=false;access.AllowChildren=false;
        access.IsNonWork=true;Check(route().Contains(access),"empty Non-Work selections retain saved-outfit obligations");access.IsNonWork=false;
        access.RequiredWeapon=WeaponRequirement.Ranged;Check(!access.IsAccessOnlyWork && route().Contains(access),"legacy weapon category is a real requirement");access.RequiredWeapon=WeaponRequirement.None;
        access.RequiredWeapons.Add(new ThingDef{IsWeapon=true});Check(!access.IsAccessOnlyWork,"exact weapon selection is a real requirement");access.RequiredWeapons.Clear();
        access.RequiredApparel.Add(new ThingDef());Check(!access.IsAccessOnlyWork,"apparel selection is a real requirement");
        pawn.carryTracker.CarriedThing=job.targetA.Thing; pawn.pather.Destination=new LocalTargetInfo(8);
        Check(route().SequenceEqual(new[]{ppe}),"current adjacent delivery destination reaches boundary with PPE rule retained elsewhere");
        access.Missing=true;Check(!route().Contains(access),"missing construction gear reaches boundary preparation rather than failing path");access.Missing=false;
        access.Allowed=false;Check(route().Contains(access),"construction destination cannot override denied activity");access.Allowed=true;
        pawn.pather.Destination=new LocalTargetInfo(7);Check(route().Contains(access),"speculative cell is not the current construction destination");pawn.pather.Destination=new LocalTargetInfo(8);
        pawn.carryTracker.CarriedThing=null;Check(route().Contains(access),"pickup phase cannot claim frame delivery route");pawn.carryTracker.CarriedThing=job.targetA.Thing;
        frame.Cell=20;Check(route().Contains(access),"unrelated distant destination cannot exempt a construction route");frame.Cell=9;
        frame.Spawned=false;Check(route().Contains(access),"destroyed or despawned frame cannot exempt entry");frame.Spawned=true;
        frame.MapHeld=new Map();Check(route().Contains(access),"frame on another map cannot exempt entry");frame.MapHeld=map;
        pawn.CurJob=new Job();Check(route().Contains(access),"stale construction job cannot exempt entry");pawn.CurJob=job;
        job.targetB=new Thing{MapHeld=map,Cell=9};Check(route().Contains(access),"ordinary container is not construction");job.targetB=frame;
        job.targetB=new Blueprint{MapHeld=map,Cell=9};Check(!route().Contains(access),"current blueprint delivery is recognized");
        job.def=JobDefOf.FinishFrame;job.targetA=frame;pawn.carryTracker.CarriedThing=null;
        Check(!route().Contains(access),"actual adjacent FinishFrame work cell is recognized");
        job.targetA=new Blueprint{MapHeld=map,Cell=9};Check(route().Contains(access),"FinishFrame must target a live frame");job.targetA=frame;
        PawnPathFollower_ProtectedArea_Patch.TransitionJob=true;
        Check(route().Contains(access),"owned outfit transitions cannot borrow construction exemption");
        PawnPathFollower_ProtectedArea_Patch.TransitionJob=false;
        Check(!ConstructionDestination.IsCurrentDestination(pawn,job,(LocalTargetInfo)frame,access.Area),"thing query does not masquerade as a chosen work cell");
    }
}
namespace Verse {
    public class Map { public DestinationManager pawnDestinationReservationManager=new DestinationManager(); }
    public class DestinationReservation { public Pawn claimant;public Job job;public IntVec3 target;public bool obsolete; }
    public class DestinationManager { public DestinationReservation Reservation;public DestinationReservation MostRecentReservationFor(Pawn p)=>Reservation; }
    public class Faction { public static Faction OfPlayerSilentFail=new Faction(); }
    public struct IntVec3 {
        public int Value; public bool IsValid=>Value>=0; public bool InBounds(Map m)=>Value>=0 && Value<100;
        public static bool operator ==(IntVec3 a,IntVec3 b)=>a.Value==b.Value; public static bool operator !=(IntVec3 a,IntVec3 b)=>a.Value!=b.Value;
        public static implicit operator IntVec3(int n)=>new IntVec3{Value=n};
    }
    public class Book : Thing {}
    public class ThingDef { public bool IsWeapon; }
    public struct CellRect { public int Center,Radius;public CellRect ExpandedBy(int n)=>new CellRect{Center=Center,Radius=Radius+n};public bool Contains(IntVec3 c)=>Math.Abs(c.Value-Center)<=Radius; }
    public class Thing { public Map MapHeld; public Map Map=>MapHeld;public bool Spawned=true; public IntVec3 Cell;public CellRect OccupiedRect()=>new CellRect{Center=Cell.Value}; }
    public class Pawn_PathFollower { public LocalTargetInfo Destination; }
    public struct LocalTargetInfo {
        public Thing Thing; IntVec3 cell; public LocalTargetInfo(int n){Thing=null;cell=n;}
        public bool HasThing=>Thing!=null; public IntVec3 Cell=>Thing?.Cell??cell; public bool IsValid=>Cell.IsValid;
        public static implicit operator LocalTargetInfo(Thing t)=>new LocalTargetInfo{Thing=t};
    }
    public class Area { public Map Map; public HashSet<int> Cells=new HashSet<int>(); public bool this[IntVec3 c]=>Cells.Contains(c.Value); }
    public class RaceProperties { public bool Animal,Humanlike=true; }
    public class CarryTracker { public Thing CarriedThing; }
    public class Pawn { public bool Child;public Pawn_PathFollower pather=new Pawn_PathFollower(); public Map Map;public Faction Faction;public IntVec3 Position;public Job CurJob;public bool Drafted,Downed,Dead,InMentalState,CustodyEscape;public RaceProperties RaceProps=new RaceProperties();public CarryTracker carryTracker=new CarryTracker(); }
}
namespace Verse.AI {
    public class ThinkNode {}
    public class JobGiver_Work:ThinkNode {}
    public class JobDef { public Type driverClass;public bool isIdle;public string defName="test";public object joyKind; }
    public class Job { public JobDef def;public LocalTargetInfo targetA,targetB,targetC;public bool playerForced;public ThinkNode jobGiver;public WorkGiverDef workGiverDef; }
}
namespace RimWorld {
    public class Frame:Thing {} public class Blueprint:Thing {}
    public class JobDriver_Ingest {}
    public class JobDriver_Reading {}
    public class WorkTypeDef { public string defName; }
    public class WorkGiverDef { public WorkTypeDef workType; }
    public static class WorkTypeDefOf { public static WorkTypeDef Hauling=new WorkTypeDef(); }
    public static class JobDefOf {
        public static JobDef Reading=new JobDef{defName="Reading",driverClass=typeof(JobDriver_Reading)};
        public static JobDef Ingest=new JobDef{defName="Ingest",driverClass=typeof(JobDriver_Ingest)}, LayDown=new JobDef{defName="LayDown"},
            Wait=new JobDef(),Wait_MaintainPosture=new JobDef(),Goto=new JobDef(),HaulToCell=new JobDef(),HaulToContainer=new JobDef(),FinishFrame=new JobDef();
    }
}
namespace AutomaticOutfitManager.State { public enum ApparelTransition{Active,Preparing} public class PawnApparelState { public ApparelTransition Transition;public bool IdleReselectionAttempted,RecallRequested; } }
namespace AutomaticOutfitManager.Rules { public enum WeaponRequirement{None,Ranged} public partial class ApparelRule { public string Id;public bool IsNonWork=true,AllowChildren,WorkAreaPaused;public List<ThingDef> RequiredApparel=new List<ThingDef>(),RequiredWeapons=new List<ThingDef>();public WeaponRequirement RequiredWeapon;public bool Enabled=true,Allowed=true,Missing;public Area Area; } }
namespace AutomaticOutfitManager.Core { public class AutomaticOutfitManagerGameComponent { public static AutomaticOutfitManagerGameComponent Current=new AutomaticOutfitManagerGameComponent();public AutomaticOutfitManager.State.PawnApparelState State;public AutomaticOutfitManager.State.PawnApparelState StateFor(Pawn p)=>State; } }
namespace AutomaticOutfitManager.Detection {
    public static class RuleEvaluator {public static IReadOnlyList<ApparelRule> Rules;public static IReadOnlyList<ApparelRule> EnabledRulesForMap(Map m)=>Rules;public static bool JobTargetsArea(Job j,Area a)=>j.targetA.IsValid && a[j.targetA.Cell];public static bool HasMissingRequiredGear(Pawn p,ApparelRule r)=>r.Missing;}
    public static class PawnAccessClassifier { public static bool IsNativeCustodyEscapeActive(Pawn p)=>p?.CustodyEscape==true; public static bool IsHostedGuest(Pawn p)=>false;public static bool IsColonyPrisoner(Pawn p)=>false; }
}
namespace AutomaticOutfitManager.Patches {
    public static class ChildAreaAccessPolicy {public static bool BypassesAdultRules(Pawn p,ApparelRule r)=>p.Child && r.AllowChildren;}
    public static class PausedAreaWorkFilter { public static bool ActivityAllowedAtRuleBoundary(Pawn p,Job j,ApparelRule r)=>r.Allowed && !r.WorkAreaPaused && (!p.Child || r.AllowChildren); }
    public static class PawnJobTracker_StartJob_Patch { public static bool IsNativeMentalActivity(Pawn p,Job j)=>p?.InMentalState==true; public static bool IsNativeEmergencySafetyJob(Job j)=>false; }
    public static class PawnPathFollower_ProtectedArea_Patch {
        public static bool TransitionJob;public static ApparelRule Owned;
        public static bool IsManagedTransitionJob(Pawn p,Job j,AutomaticOutfitManager.State.PawnApparelState s)=>TransitionJob;
        public static bool ManagedTransitionMayEnterRule(Pawn p,Job j,AutomaticOutfitManager.State.PawnApparelState s,ApparelRule r)=>r==Owned;
    }
}
