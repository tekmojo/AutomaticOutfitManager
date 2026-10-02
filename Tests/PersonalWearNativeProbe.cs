using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using RimWorld;
using Verse;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;

internal static class PersonalWearNativeProbe
{
    public static int Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
                if(new AssemblyName(e.Name).Name=="0Harmony") return Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,"0Harmony.dll"));
                foreach(string dir in args) {
                    string path=Path.Combine(dir,new AssemblyName(e.Name).Name+".dll");
                    if(File.Exists(path)) return Assembly.LoadFrom(path);
                }
                return null;
            };
            return (int)typeof(PersonalWearNativeProbe).Assembly.GetType("PersonalWearChecks")
                .GetMethod("Run").Invoke(null,new object[]{args.Contains("--inspect")});
        }
        catch(Exception e) { Console.Error.WriteLine("FAIL: "+e.GetBaseException()); return 1; }
    }
}

internal static class PersonalWearChecks
{
    static readonly Harmony harmony=new Harmony("aom.tests.personal.wear");
    static AutomaticOutfitManagerGameComponent component;
    static Pawn pawn;
    static int passed;
    static bool canWear=true;
    static readonly ApparelLayerDef skin=new ApparelLayerDef{defName="OnSkin"};
    static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    static void Prefix(MethodBase m,string n) => harmony.Patch(m,prefix:new HarmonyMethod(typeof(PersonalWearChecks),n));
    static void Check(bool ok,string msg) {if(!ok)throw new Exception(msg);passed++;Console.WriteLine("PASS: "+msg);}
    public static bool Current(ref AutomaticOutfitManagerGameComponent __result) {__result=component;return false;}
    public static bool Quiet()=>false;
    public static bool No(ref bool __result) {__result=false;return false;}
    public static bool Parts(ref bool __result) {__result=canWear;return false;}
    public static bool BodyGroups(ApparelProperties __instance,ref BodyPartGroupDef[] __result)
    {__result=__instance.bodyPartGroups.ToArray();return false;}
    static SavedNonWorkOutfit Reset(bool preference=true)
    {
        component=Empty<AutomaticOutfitManagerGameComponent>();
        foreach(var field in AccessTools.GetDeclaredFields(component.GetType())) {
            if(field.IsStatic)continue;
            if(field.FieldType.Namespace=="System.Collections.Generic")
                field.SetValue(component,Activator.CreateInstance(field.FieldType));
            if(field.FieldType==typeof(int)&&field.Name.StartsWith("indexed"))field.SetValue(component,-1);
            if(field.FieldType==typeof(bool)&&field.Name.EndsWith("IndexDirty"))field.SetValue(component,true);
        }
        pawn=Empty<Pawn>();pawn.def=Empty<ThingDef>();pawn.def.defName="Human";pawn.def.race=new RaceProperties{body=new BodyDef()};
        pawn.thingIDNumber=1;pawn.Name=new NameSingle("Moto");pawn.apparel=new Pawn_ApparelTracker(pawn);
        var saved=new SavedNonWorkOutfit{Pawn=pawn};
        if(preference)component.SavedNonWorkOutfits.Add(saved);
        canWear=true;return saved;
    }
    static Apparel Garment(int id=2,ThingDef def=null)
    {
        var garment=Empty<Apparel>();garment.def=def??Empty<ThingDef>();
        if(def==null) {garment.def.defName="Pants"+id;garment.def.apparel=new ApparelProperties{
            layers=new List<ApparelLayerDef>{skin},bodyPartGroups=new List<BodyPartGroupDef>{new BodyPartGroupDef{defName="Slot"+id}}};}
        garment.thingIDNumber=id;garment.stackCount=1;return garment;
    }
    public static int Run(bool inspect)
    {
        var wear=AccessTools.Method(typeof(Pawn_ApparelTracker),"Wear",new[]{typeof(Apparel),typeof(bool),typeof(bool)});
        if(inspect) {
            foreach(var ins in PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(ApparelUtility),"CanWearTogether"))) Console.WriteLine(ins);
            return 0;
        }
        Prefix(AccessTools.PropertyGetter(typeof(AutomaticOutfitManagerGameComponent),"Current"),nameof(Current));
        Prefix(AccessTools.PropertyGetter(typeof(AutomaticOutfitManager.Core.AomLog),"DetailedEnabled"),nameof(No));
        Prefix(AccessTools.Method(typeof(ApparelUtility),"HasPartsToWear"),nameof(Parts));
        Prefix(AccessTools.Method(typeof(Apparel),"PawnCanWear"),nameof(Parts));
        Prefix(AccessTools.Method(typeof(ApparelProperties),"GetInterferingBodyPartGroups"),nameof(BodyGroups));
        Prefix(AccessTools.Method(typeof(Log),"Warning",new[]{typeof(string)}),nameof(Quiet));
        // Omit world/render notifications after container mutation. Native Wear
        // and ThingOwner.TryAdd still own membership and callback ordering.
        Prefix(AccessTools.Method(typeof(ThingOwner<Apparel>),"NotifyAdded"),nameof(Quiet));
        Prefix(AccessTools.Method(typeof(ThingOwner<Apparel>),"NotifyRemoved"),nameof(Quiet));
        // Skip the game's constructor-time global registry reset; initialize
        // only instance collections, leaving the production methods intact.
        var saved=Reset();
        harmony.CreateClassProcessor(typeof(PawnApparelTracker_Wear_SavedApparel_Patch)).Patch();
        var pants=Garment();
        pawn.apparel.Wear(pants,false,false);
        Check(pawn.apparel.WornApparel.Contains(pants),"native Wear actually adds the garment before callback");
        Check(saved.Apparel.Contains(pants),"ordinary native Wear updates the inactive outfit");
        Check(saved.ApparelSatisfied(pawn),"dining saved outfit accepts the newly worn pants");
        Check(component.ManagedApparelIds.Contains(pants.GetUniqueLoadID())&&component.ManagedApparelOwnerIds.Count==0,"inactive preference tracks exact storage membership without a new pawn reservation");
        Check(component.ManagedApparelStockDefs.Count==0,"ordinary personal Wear does not retain the type");
        component.AdoptWornPersonalApparel(pawn,pants);
        Check(saved.Apparel.Count==1,"duplicate callback cannot duplicate saved items");

        var replacement=Garment(3,pants.def);
        pawn.apparel.Wear(replacement,false,false);
        Check(saved.Apparel.SequenceEqual(new[]{replacement}),"native replacement removes the displaced item from inactive preference");
        Check(!component.ManagedApparelOwnerIds.ContainsKey(pants.GetUniqueLoadID())&&!component.ManagedApparelIds.Contains(pants.GetUniqueLoadID()),"replacement releases old exact ownership and tracking");
        Check(saved.ApparelSatisfied(pawn),"dining accepts a successful ordinary replacement");

        saved=Reset();pants=Garment();canWear=false;
        pawn.apparel.Wear(pants,false,false);
        Check(saved.Apparel.Count==0&&pawn.apparel.WornApparel.Count==0,"native rejected Wear cannot alter the saved outfit");

        saved=Reset(false);pants=Garment();pawn.apparel.Wear(pants,false,false);
        Check(component.SavedNonWorkOutfits.Count==0&&component.ManagedApparelIds.Count==0,"ordinary Wear does not invent a saved preference");

        saved=Reset();pants=Garment();component.ManagedApparelStockDefs.Add(pants.def);
        pawn.apparel.Wear(pants,false,false);
        Check(pawn.apparel.WornApparel.Contains(pants)&&saved.Apparel.Count==0,"retained stock worn by explicit order is not personal");

        saved=Reset();pants=Garment();component.Rules.Add(new ApparelRule{Enabled=false,RequiredApparel=new List<ThingDef>{pants.def}});
        pawn.apparel.Wear(pants,false,false);
        Check(saved.Apparel.Count==0,"disabled rule selection stays outside personal preference");

        saved=Reset();pants=Garment();saved.WorkGear.Add(new WorkGearSource{Item=pants});
        pawn.apparel.Wear(pants,false,false);
        Check(saved.Apparel.Count==0,"borrowed exact gear remains excluded after its type is unselected");

        saved=Reset();pants=Garment();saved.PendingSharedReturns.Add(pants);
        pawn.apparel.Wear(pants,false,false);
        Check(saved.Apparel.Count==0,"pending shared return cannot become personal");

        saved=Reset();pants=Garment();var other=Empty<Pawn>();other.thingIDNumber=99;other.def=pawn.def;
        component.SavedNonWorkOutfits.Add(new SavedNonWorkOutfit{Pawn=other,Apparel=new List<Apparel>{pants}});
        component.ManagedApparelOwnerIds[pants.GetUniqueLoadID()]=other.GetUniqueLoadID();
        pawn.apparel.Wear(pants,false,false);
        Check(pawn.apparel.WornApparel.Count==0&&saved.Apparel.Count==0,"another pawn's saved item remains protected by native Wear prefix");

        saved=Reset();pants=Garment();component.PawnStates.Add(new PawnApparelState{Pawn=pawn,ApparelInterventionActive=false});
        pawn.apparel.Wear(pants,false,false);
        Check(saved.Apparel.Count==0,"non-apparel transition is not reinterpreted as an inactive personal change");

        saved=Reset();pants=Garment();var state=new PawnApparelState{Pawn=pawn,Transition=ApparelTransition.Active};component.PawnStates.Add(state);
        pawn.apparel.Wear(pants,false,false);
        Check(state.OriginalApparel.Contains(pants)&&saved.Apparel.Contains(pants),"existing active personal adoption remains intact");
        Check(component.ManagedApparelOwnerIds[pants.GetUniqueLoadID()]==pawn.GetUniqueLoadID(),"active adoption still reserves the exact personal garment");
        replacement=Garment(3,pants.def);pawn.apparel.Wear(replacement,false,false);
        Check(state.OriginalApparel.SequenceEqual(new[]{replacement})&&saved.Apparel.SequenceEqual(new[]{replacement}),"active replacement updates both personal snapshots");
        Check(!component.ManagedApparelOwnerIds.ContainsKey(pants.GetUniqueLoadID()),"active replacement clears displaced saved owner");

        saved=Reset();var absentShirt=Garment(10);saved.Apparel.Add(absentShirt);pants=Garment();
        pawn.apparel.Wear(pants,false,false);
        Check(saved.Apparel.Contains(absentShirt)&&saved.Apparel.Contains(pants),"ordinary pants Wear preserves unrelated saved garments even when not worn");

        saved=Reset();pants=Garment();state=new PawnApparelState{Pawn=pawn};state.ManagedApparel.Add(pants);component.PawnStates.Add(state);
        pawn.apparel.Wear(pants,false,false);
        Check(saved.Apparel.Count==0&&state.OriginalApparel.Count==0,"AOM preparation Wear cannot contaminate the saved outfit");
        Console.WriteLine("Personal wear native checks: "+passed);return 0;
    }
}

