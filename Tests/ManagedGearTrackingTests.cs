using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.Storage;
using AutomaticOutfitManager.Rules;
using RimWorld;
using Verse;

internal static class ManagedGearTrackingTests
{
    static int passed;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; }
    static Apparel Shield(string id) => new Apparel { Id = id, def = new ThingDef { apparel = new object() } };
    static ThingWithComps Gun(string id) => new ThingWithComps { Id = id, def = new ThingDef { IsWeapon = true } };
    static AutomaticOutfitManagerGameComponent Component() => AutomaticOutfitManagerGameComponent.Current = new AutomaticOutfitManagerGameComponent();
    static void Register(AutomaticOutfitManagerGameComponent c, ThingWithComps a, ThingWithComps w)
    { c.ManagedApparelIds.Add(a.Id); c.ManagedWeaponIds.Add(w.Id); c.IsManagedApparel((Apparel)a); c.IsManagedWeapon(w); }
    static bool Automatic(Thing item) => AutomaticOutfitStorageScope.Matches(item);
    public static int Main()
    {
        try
        {
            var c = Component(); var a = Shield("returned"); var w = Gun("returned-gun"); var p = new Pawn();
            Register(c, a, w);
            c.PawnStates.Add(new PawnApparelState { Pawn = p, ManagedApparel = new List<Apparel>{a}, ManagedWeapons = new List<ThingWithComps>{w} });
            Check(Automatic(a) && Automatic(w), "borrowed gear starts managed");
            c.EndIntervention(p);
            Check(!Automatic(a) && !Automatic(w), "returned gear loses stale tracking after state removal");
            Check(!c.IsManagedApparel(a) && !c.IsManagedWeapon(w), "cached ID lookups invalidate on completion");

            c = Component(); Register(c, a, w); c.ManagedApparelStockDefs.Add(a.def); c.ManagedWeaponStockDefs.Add(w.def);
            c.LoadedGame();
            Check(!c.IsManagedApparel(a) && !c.IsManagedWeapon(w), "load repairs legacy unused IDs");
            Check(Automatic(a) && Automatic(w), "retained stock stays automatic without exact IDs");
            Check(c.ForgetManagedStockDefinition(a.def) && c.ForgetManagedStockDefinition(w.def), "Forget releases retained types");
            Check(!Automatic(a) && !Automatic(w), "forgotten unused stock becomes ordinary");

            c = Component(); Register(c, a, w); c.ManagedApparelStockDefs.Add(a.def); c.ManagedWeaponStockDefs.Add(w.def);
            c.ForgetManagedStockDefinition(a.def); c.ForgetManagedStockDefinition(w.def);
            Check(!Automatic(a) && !Automatic(w), "Forget also repairs existing orphan IDs without reload");
            c = Component(); Register(c, a, w); c.LoadedGame();
            Check(!Automatic(a) && !Automatic(w), "load repairs already-forgotten shield and weapon");

            foreach (ApparelTransition phase in Enum.GetValues(typeof(ApparelTransition)))
            {
                c = Component(); Register(c, a, w);
                var personal = Shield("personal"); var personalGun = Gun("personal-gun");
                Register(c, personal, personalGun);
                c.PawnStates.Add(new PawnApparelState { Pawn=p, Transition=phase, OriginalApparel=new List<Apparel>{personal}, OriginalWeapon=personalGun, ManagedApparel=new List<Apparel>{a}, ManagedWeapons=new List<ThingWithComps>{w} });
                c.LoadedGame();
                Check(Automatic(a) && Automatic(w) && Automatic(personal) && Automatic(personalGun), "load preserves exact active gear in " + phase);
                Check(!c.ForgetManagedStockDefinition(a.def) && !c.ForgetManagedStockDefinition(w.def), "Forget blocked for active gear in " + phase);
                var other = new Pawn(); c.PawnStates.Add(new PawnApparelState {Pawn=other}); c.EndIntervention(other);
                Check(c.IsManagedApparel(a) && c.IsManagedWeapon(w), "another pawn completing does not release active gear in " + phase);
            }

            c = Component(); Register(c, a, w);
            c.SavedNonWorkOutfits.Add(new SavedNonWorkOutfit {Pawn=p, Apparel=new List<Apparel>{a}, Weapon=w});
            c.LoadedGame();
            Check(Automatic(a) && Automatic(w), "inactive personal snapshots remain automatic storage");
            Check(!ManagedApparelClassifier.Matches(a) && !ManagedWeaponClassifier.Matches(w), "inactive preferences do not retain borrowed membership");
            Check(!Automatic(new Apparel {Id="ordinary-copy",def=a.def}), "saved item does not claim other copies");

            c = Component(); Register(c, a, w);
            var saved = new SavedNonWorkOutfit {Pawn=p, WorkGear=new List<WorkGearSource>{new WorkGearSource{Item=a},new WorkGearSource{Item=w}}};
            c.SavedNonWorkOutfits.Add(saved); a.ParentHolder=new Holder {ParentHolder=p}; w.ParentHolder=p;
            c.PawnStates.Add(new PawnApparelState {Pawn=p}); c.EndIntervention(p);
            Check(c.IsManagedApparel(a) && c.IsManagedWeapon(w), "partial return keeps worn and inventory work gear");
            a.ParentHolder=null; w.ParentHolder=null; c.LoadedGame();
            Check(!Automatic(a) && !Automatic(w), "loose issuance history does not retain borrowed status");

            Register(c, a, w); saved.PendingSharedReturns.AddRange(new[]{(ThingWithComps)a,w}); a.ParentHolder=p; w.ParentHolder=p;
            c.LoadedGame(); Check(c.IsManagedApparel(a) && c.IsManagedWeapon(w), "pending held shared returns stay tracked");
            a.ParentHolder=null; w.ParentHolder=null; c.LoadedGame();
            Check(!Automatic(a) && !Automatic(w), "completed pending shared returns release tracking");

            c = Component(); Register(c, a, w);
            c.NonWorkMealTrips.Add(new NonWorkMealTrip {Pawn=p, ReturnOutfit=new SavedNonWorkOutfit{Apparel=new List<Apparel>{a}}, DestinationOutfit=new SavedNonWorkOutfit{Weapon=w}});
            c.LoadedGame(); Check(c.IsManagedApparel(a) && c.IsManagedWeapon(w), "meal handoff snapshots stay tracked");
            c.NonWorkMealTrips.Clear(); c.LoadedGame(); Check(!Automatic(a) && !Automatic(w), "completed meal handoff releases unused IDs");

            c = Component(); c.ManagedApparelStockDefs.Add(a.def); c.Rules.Add(new ApparelRule{Selected=a.def});
            Check(!c.ForgetManagedStockDefinition(a.def), "selected type cannot be forgotten");
            c = Component(); Register(c, a, w); c.ManagedApparelIds.Add(null); c.ManagedWeaponIds.Add("missing");
            c.PawnStates.Add(new PawnApparelState {ManagedApparel=new List<Apparel>{a},ManagedWeapons=new List<ThingWithComps>{w}});
            c.LoadedGame(); Check(c.ManagedApparelIds.Count==0 && c.ManagedWeaponIds.Count==0, "orphan states and missing IDs do not preserve stale tracking");
            c.LoadedGame(); Check(c.ManagedApparelIds.Count==0 && c.ManagedWeaponIds.Count==0, "cleanup is idempotent");
            Console.WriteLine(passed + " managed gear tracking checks passed."); return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine("FAIL: " + ex.Message); return 1; }
    }
}
namespace Verse
{
    public interface IThingHolder { IThingHolder ParentHolder {get;} }
    public class Holder : IThingHolder { public IThingHolder ParentHolder {get;set;} }
    public class Thing : Holder { public string Id; public bool Destroyed; public ThingDef def; public string GetUniqueLoadID()=>Id; }
    public class ThingWithComps : Thing { }
    public class Pawn : Holder { public string LabelShortCap="Pawn"; }
    public class ThingDef { public object apparel; public bool IsWeapon; }
    public class GameComponent { public virtual void LoadedGame(){} }
    public static class DefDatabase<T> { public static List<T> AllDefsListForReading=new List<T>(); }
}
namespace RimWorld { public class Apparel : ThingWithComps {} }
namespace AutomaticOutfitManager.Rules { public class ApparelRule { public ThingDef Selected; } }
namespace AutomaticOutfitManager.State
{
    public enum ApparelTransition {Preparing,Active,ReturningToChangingArea,Restoring}
    public class PawnApparelState
    {
        public Pawn Pawn; public List<Apparel> OriginalApparel=new List<Apparel>(), ManagedApparel=new List<Apparel>();
        public ThingWithComps OriginalWeapon; public List<ThingWithComps> ManagedWeapons=new List<ThingWithComps>();
        public bool NonWorkGearReturnOnly, WeaponRuleOverrideExplicit, MapDepartureRequested, ApparelInterventionActive=true, WeaponInterventionActive;
        public string NonWorkRestorationRuleId; public ApparelTransition Transition; public int LastRestorationAttemptTick, ActiveIdleTicks;
    }
    public class SavedNonWorkOutfit
    {
        public Pawn Pawn; public bool RetiredManualSnapshot; public List<Apparel> Apparel=new List<Apparel>(); public ThingWithComps Weapon;
        public List<WorkGearSource> WorkGear=new List<WorkGearSource>(); public List<ThingWithComps> PendingSharedReturns=new List<ThingWithComps>();
        public bool ApparelSatisfied(Pawn p)=>true; public bool WeaponSatisfied(Pawn p)=>true;
    }
    public class WorkGearSource { public ThingWithComps Item; }
    public class NonWorkMealTrip { public Pawn Pawn; public SavedNonWorkOutfit ReturnOutfit, DestinationOutfit; }
    public static class NonWorkOutfitPolicy { public static SavedNonWorkOutfit Target(Pawn p,ApparelRule r)=>null; }
}
namespace AutomaticOutfitManager.Core
{
    public partial class AutomaticOutfitManagerGameComponent : GameComponent
    {
        public static AutomaticOutfitManagerGameComponent Current;
        public List<PawnApparelState> PawnStates=new List<PawnApparelState>();
        public List<SavedNonWorkOutfit> SavedNonWorkOutfits=new List<SavedNonWorkOutfit>();
        public List<NonWorkMealTrip> NonWorkMealTrips=new List<NonWorkMealTrip>();
        public List<ApparelRule> Rules=new List<ApparelRule>();
        public List<string> ManagedApparelIds=new List<string>(), ManagedWeaponIds=new List<string>();
        public List<ThingDef> ManagedApparelStockDefs=new List<ThingDef>(), ManagedWeaponStockDefs=new List<ThingDef>();
        HashSet<string> managedApparelIdIndex=new HashSet<string>(), managedWeaponIdIndex=new HashSet<string>();
        int indexedManagedApparelCount=-1,indexedManagedWeaponCount=-1,indexedPawnStateCount;
        bool managedApparelDefIndexDirty,managedWeaponDefIndexDirty;
        Dictionary<Pawn,int> restorationProgress=new Dictionary<Pawn,int>(),restorationRecoveryBackoff=new Dictionary<Pawn,int>(),activeWorkProgress=new Dictionary<Pawn,int>(),rejectedManagedGearWakeTicks=new Dictionary<Pawn,int>();
        Dictionary<Pawn,PawnApparelState> pawnStateIndex=new Dictionary<Pawn,PawnApparelState>();
        public PawnApparelState StateFor(Pawn p)=>PawnStates.FirstOrDefault(s=>s.Pawn==p);
        public SavedNonWorkOutfit NonWorkOutfitFor(Pawn p)=>SavedNonWorkOutfits.FirstOrDefault(s=>s.Pawn==p);
        public ApparelRule RuleById(string id)=>null;
        void ClearPendingWork(PawnApparelState s){} void ClearSavedOwner(Apparel a){} void InvalidateWeaponStateIndex(){}
        void RebuildRuntimeIndexes(){} int RepairHostedGuestSessionMarkers()=>0; int RepairAbandonedSnapshots(string reason)=>0;
        bool TryCompleteSatisfiedRestoration(Pawn p,PawnApparelState s,string reason)=>false; int RebuildPendingWorkClaims()=>0;
        public bool IsManagedApparelDefinition(ThingDef d)=>ManagedApparelStockDefs.Contains(d)||Rules.Any(r=>r.Selected==d);
        public bool IsManagedWeaponDefinition(ThingDef d)=>ManagedWeaponStockDefs.Contains(d)||Rules.Any(r=>r.Selected==d);
        public bool IsTrackedWeapon(ThingWithComps w)=>IsManagedWeapon(w)||PawnStates.Any(s=>s.OriginalWeapon==w||s.ManagedWeapons.Contains(w));
    }
    public static class AomLog { public static bool BasicEnabled=true,DetailedEnabled=true; public static void Basic(string s){} public static void Detailed(string s){} public static void ClearPawn(Pawn p){} }
}
namespace AutomaticOutfitManager.Detection
{
    public static class GearSelectionPolicy { public static bool Selects(ApparelRule r,ThingDef d)=>r.Selected==d; }
    public static class ManagedWorkClaimRegistry { public static void ReleaseAll(Pawn p){} }
    public static class UnavailableWorkRegistry { public static void Block(Pawn p,ApparelRule r){} }
    public static class NativeDepartureHandoff { public static void Restored(Pawn p){} }
    public static class TransitionActivityDiagnostics { public static void Cleared(Pawn p,string r){} }
}
namespace AutomaticOutfitManager.Patches
{
    public static class NonWorkBufferTracker { public static void Begin(Pawn p,ApparelRule r){} }
    public static class GravshipAreaRemapper { public static void RepairAfterLoad(AutomaticOutfitManagerGameComponent c){} }
}
