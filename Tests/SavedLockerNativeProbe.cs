using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.Storage;

internal static class SavedLockerNativeProbe
{
    public static int Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
                if (new AssemblyName(e.Name).Name == "0Harmony") return Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,"0Harmony.dll"));
                foreach (string dir in args) {
                    string path=Path.Combine(dir,new AssemblyName(e.Name).Name+".dll");
                    if(File.Exists(path)) return Assembly.LoadFrom(path);
                }
                return null;
            };
            return (int)typeof(SavedLockerNativeProbe).Assembly.GetType("SavedLockerChecks")
                .GetMethod("Run").Invoke(null,new object[]{args.Contains("--previous")});
        }
        catch(Exception e) { Console.Error.WriteLine("FAIL: "+e.GetBaseException()); return 1; }
    }
}

internal static class SavedLockerChecks
{
    static readonly Harmony harmony=new Harmony("aom.tests.saved.locker");
    static readonly Dictionary<Area,List<IntVec3>> areas=new Dictionary<Area,List<IntVec3>>();
    static readonly Dictionary<IntVec3,SlotGroup> slots=new Dictionary<IntVec3,SlotGroup>();
    static readonly Dictionary<SlotGroup,List<IntVec3>> groupCells=new Dictionary<SlotGroup,List<IntVec3>>();
    static readonly Dictionary<SlotGroup,StorageSettings> settings=new Dictionary<SlotGroup,StorageSettings>();
    static readonly Dictionary<StorageSettings,StoragePriority> priorities=new Dictionary<StorageSettings,StoragePriority>();
    static readonly HashSet<IntVec3> rejected=new HashSet<IntVec3>();
    static readonly List<Thing> emptyThings=new List<Thing>();
    static readonly List<Thing> shelfThings=new List<Thing>();
    static readonly List<SlotGroup> orderedGroups=new List<SlotGroup>();
    static Map map; static Pawn owner,hauler; static Apparel gear; static ThingWithComps weapon;
    static PawnApparelState state; static AutomaticOutfitManagerGameComponent component;
    static Area home,remote; static SlotGroup localGroup,remoteGroup; static Type policy;
    static bool reachable=true,stored,accept=true,eligible=true,claim,held,storageEnabled=true,throwStorage;
    static Job forcedCurrentJob;
    static int passed;
    static readonly IntVec3 floor=new IntVec3(10,0,10),local=new IntVec3(11,0,10),far=new IntVec3(30,0,30);
    static T Empty<T>() { if(typeof(T).IsAbstract) throw new Exception("Abstract fixture type: "+typeof(T)); return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    static MethodInfo M(Type t,string n,params Type[] p) => AccessTools.Method(t,n,p.Length==0?null:p) ?? throw new Exception("Missing method: "+t+"."+n);
    static void Prefix(MethodBase m,string n) { if(m==null) throw new Exception("Missing fixture target for "+n); try { harmony.Patch(m,prefix:new HarmonyMethod(typeof(SavedLockerChecks),n)); } catch(Exception e) { throw new Exception("Patching "+m+" via "+n,e); } }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);passed++;Console.WriteLine("PASS: "+message);}
    public static bool Quiet()=>false;
    public static bool LogError(string text){throw new Exception("Native Scribe error: "+text);}
    public static bool ResolveType(string __0,ref Type __result)
    { __result=typeof(PawnApparelState).Assembly.GetType(__0)??typeof(Thing).Assembly.GetType(__0)??Type.GetType(__0);return __result==null; }
    public static bool Yes(ref bool __result){__result=true;return false;}
    public static bool No(ref bool __result){__result=false;return false;}
    public static bool MapValue(ref Map __result){__result=map;return false;}
    public static bool Spawned(Thing __instance,ref bool __result){__result=!held||__instance!=gear;return false;}
    public static bool Player(ref Faction __result){__result=Faction.OfPlayer;return false;}
    public static bool Component(ref AutomaticOutfitManagerGameComponent __result){__result=component;return false;}
    public static bool Owner(Thing __0,ref Pawn __result)
    {__result=state!=null&&(__0==state.OriginalWeapon||(__0 is Apparel a&&state.OriginalApparel.Contains(a)))?owner:null;return false;}
    public static bool WorkWeaponDefinition(ThingDef __0,ref bool __result)
    {__result=component.Rules.Any(rule=>rule.RequiredWeapons.Contains(__0));return false;}
    public static bool State(Pawn __0,ref PawnApparelState __result){__result=__0==owner?state:null;return false;}
    public static bool ActiveCells(Area __instance,ref IEnumerable<IntVec3> __result){__result=areas[__instance];return false;}
    public static bool Contains(Area __instance,IntVec3 __0,ref bool __result){__result=__0.IsValid&&areas[__instance].Contains(__0);return false;}
    public static bool Slot(IntVec3 __0,ref SlotGroup __result){slots.TryGetValue(__0,out __result);return false;}
    public static bool Settings(SlotGroup __instance,ref StorageSettings __result){__result=settings[__instance];return false;}
    public static bool Cells(SlotGroup __instance,ref List<IntVec3> __result){__result=groupCells[__instance];return false;}
    public static bool Priority(StorageSettings __instance,ref StoragePriority __result){__result=priorities[__instance];return false;}
    public static bool Accept(ref bool __result){__result=accept;return false;}
    public static bool StorageEnabled(ref bool __result){__result=storageEnabled;return false;}
    public static bool CurrentJob(Pawn __instance,ref Job __result){__result=__instance==hauler?forcedCurrentJob:null;return false;}
    public static bool NativeCell(IntVec3 c,ref bool __result){__result=accept&&!rejected.Contains(c);return false;}
    public static bool CurrentDestination(ref IHaulDestination __result){__result=stored?localGroup.parent:null;return false;}
    public static bool Reach(ref bool __result){__result=reachable;return false;}
    public static bool Eligible(ref bool __result){__result=eligible;return false;}
    public static bool Claim(ref bool __result){__result=claim;return false;}
    public static bool Things(ref List<Thing> __result){__result=emptyThings;return false;}
    public static bool CellThings(IntVec3 __0,ref List<Thing> __result)
    {__result=slots.ContainsKey(__0)?shelfThings:emptyThings;return false;}
    public static bool Groups(ref List<SlotGroup> __result){__result=orderedGroups;return false;}
    public static bool MakeJob(JobDef __0,LocalTargetInfo __1,LocalTargetInfo __2,ref Job __result)
    {__result=Empty<Job>();__result.def=__0;__result.targetA=__1;__result.targetB=__2;return false;}
    public static bool NativeStorage(Thing t,Pawn carrier,Map map,StoragePriority currentPriority,Faction faction,
        ref IntVec3 foundCell,ref IHaulDestination haulDestination,ref bool __result)
    {if(throwStorage)throw new InvalidOperationException("fixture native storage failure");foundCell=far;haulDestination=remoteGroup.parent;__result=true;return false;}
    public static bool NativeStorageJob(Pawn p,Thing t,IntVec3 storeCell,ref Job __result)
    {__result=JobMaker.MakeJob(JobDefOf.HaulToCell,t,storeCell);return false;}
    public static bool HeldPosition(Thing __instance,ref IntVec3 __result){__result=__instance.Position;return false;}
    static bool Home(Thing item,out Area result)
    {var args=new object[]{item,null,null};bool ok=(bool)M(policy,"TryGetHome").Invoke(null,args);result=(Area)args[2];return ok;}
    static bool Plan(Thing item,out Job job)
    {var args=new object[]{hauler,item,false,null};bool handled=(bool)M(policy,"TryMakeJob").Invoke(null,args);job=(Job)args[3];return handled;}
    static SlotGroup Group(IntVec3 cell,StoragePriority priority)
    {
        var group=Empty<SlotGroup>();group.parent=Empty<Building_Storage>();
        groupCells[group]=new List<IntVec3>{cell}; settings[group]=Empty<StorageSettings>();
        priorities[settings[group]]=priority;slots[cell]=group;return group;
    }
    public static int Run(bool previous)
    {
        var assembly=typeof(PawnApparelState).Assembly;
        policy=assembly.GetType("AutomaticOutfitManager.Storage.SavedGearLockerPolicy",true);
        Prefix(M(typeof(DefOfHelper),"EnsureInitializedInCtor"),nameof(Quiet));
        Prefix(M(typeof(Log),"Error",typeof(string)),nameof(LogError));
        Prefix(AccessTools.PropertyGetter(typeof(Prefs),"LogVerbose"),nameof(No));
        Prefix(M(typeof(GenTypes),"GetTypeInAnyAssembly",typeof(string),typeof(string)),nameof(ResolveType));
        Prefix(AccessTools.PropertyGetter(typeof(Faction),"OfPlayer"),nameof(PlayerFaction));
        Prefix(AccessTools.PropertyGetter(typeof(Thing),"Faction"),nameof(Player));
        Prefix(AccessTools.PropertyGetter(typeof(Thing),"Map"),nameof(MapValue));
        Prefix(AccessTools.PropertyGetter(typeof(Thing),"MapHeld"),nameof(MapValue));
        Prefix(AccessTools.PropertyGetter(typeof(Thing),"Spawned"),nameof(Spawned));
        Prefix(AccessTools.PropertyGetter(typeof(Thing),"SpawnedOrAnyParentSpawned"),nameof(Yes));
        Prefix(AccessTools.PropertyGetter(typeof(Thing),"PositionHeld"),nameof(HeldPosition));
        Prefix(AccessTools.PropertyGetter(typeof(Pawn),"CurJob"),nameof(CurrentJob));
        Prefix(AccessTools.PropertyGetter(typeof(Area),"Map"),nameof(MapValue));
        Prefix(AccessTools.PropertyGetter(typeof(Area),"ActiveCells"),nameof(ActiveCells));
        Prefix(M(typeof(Area),"get_Item",typeof(IntVec3)),nameof(Contains));
        Prefix(AccessTools.PropertyGetter(typeof(AutomaticOutfitManagerGameComponent),"Current"),nameof(Component));
        Prefix(M(typeof(AutomaticOutfitManagerGameComponent),"SavedPawnFor"),nameof(Owner));
        Prefix(M(typeof(AutomaticOutfitManagerGameComponent),"SavedPawnForWeapon"),nameof(Owner));
        Prefix(M(typeof(AutomaticOutfitManagerGameComponent),"StateFor"),nameof(State));
        Prefix(M(typeof(AutomaticOutfitManagerGameComponent),"IsManagedWeaponDefinition"),nameof(WorkWeaponDefinition));
        Prefix(M(typeof(GenGrid),"InBounds",typeof(IntVec3),typeof(Map)),nameof(Yes));
        // Execute native Standable: a walkable shelf is PassThroughOnly and
        // therefore NOT standable. Storage must not reuse the floor-drop test.
        Prefix(M(typeof(GenGrid),"Walkable",typeof(IntVec3),typeof(Map)),nameof(Yes));
        Prefix(M(typeof(ThingGrid),"ThingsListAt",typeof(IntVec3)),nameof(CellThings));
        Prefix(M(typeof(GridsUtility),"Fogged",typeof(IntVec3),typeof(Map)),nameof(No));
        Prefix(typeof(Thing).Assembly.GetTypes().SelectMany(t=>t.GetMethods(BindingFlags.Public|BindingFlags.Static)).Single(m=>m.Name=="ContainsStaticFire"),nameof(No));
        Prefix(M(typeof(StoreUtility),"GetSlotGroup",typeof(IntVec3),typeof(Map)),nameof(Slot));
        Prefix(M(typeof(GridsUtility),"GetThingList",typeof(IntVec3),typeof(Map)),nameof(Things));
        Prefix(M(typeof(ForbidUtility),"IsForbidden",typeof(IntVec3),typeof(Pawn)),nameof(No));
        Prefix(M(typeof(ForbidUtility),"IsForbidden",typeof(Thing),typeof(Pawn)),nameof(No));
        Prefix(M(typeof(ReachabilityUtility),"CanReach",typeof(Pawn),typeof(LocalTargetInfo),typeof(PathEndMode),typeof(Danger),typeof(bool),typeof(bool),typeof(TraverseMode)),nameof(Reach));
        Prefix(M(typeof(ReservationUtility),"CanReserve",typeof(Pawn),typeof(LocalTargetInfo),typeof(int),typeof(int),typeof(ReservationLayerDef),typeof(bool)),nameof(Yes));
        Prefix(M(assembly.GetType("AutomaticOutfitManager.Detection.GearRetrievalRoute"),"CanReachStorageCell"),nameof(Reach));
        Prefix(M(assembly.GetType("AutomaticOutfitManager.Detection.ManagedWorkCandidateFilter"),"Rejects"),nameof(No));
        Prefix(M(assembly.GetType("AutomaticOutfitManager.Detection.ManagedWorkClaimRegistry"),"IsClaimedByOther",typeof(Pawn),typeof(Map),typeof(Thing),typeof(IntVec3)),nameof(Claim));
        Prefix(AccessTools.PropertyGetter(typeof(SlotGroup),"Settings"),nameof(Settings));
        Prefix(AccessTools.PropertyGetter(typeof(SlotGroup),"CellsList"),nameof(Cells));
        Prefix(AccessTools.PropertyGetter(typeof(StorageSettings),"Priority"),nameof(Priority));
        Prefix(M(typeof(StorageSettings),"AllowedToAccept",typeof(Thing)),nameof(Accept));
        Prefix(M(typeof(Building_Storage),"Accepts"),nameof(Accept));
        Prefix(AccessTools.PropertyGetter(typeof(Building_Storage),"HaulDestinationEnabled"),nameof(StorageEnabled));
        Prefix(M(typeof(StoreUtility),"CurrentHaulDestinationOf"),nameof(CurrentDestination));
        Prefix(M(typeof(StoreUtility),"IsGoodStoreCell"),nameof(NativeCell));
        Prefix(M(typeof(StoreUtility),"TryFindBestBetterStorageFor"),nameof(NativeStorage));
        Prefix(M(typeof(StoreUtility),"CurrentStoragePriorityOf"),nameof(Unstored));
        Prefix(M(typeof(HaulAIUtility),"PawnCanAutomaticallyHaulFast"),nameof(Eligible));
        Prefix(M(typeof(HaulAIUtility),"HaulToCellStorageJob"),nameof(NativeStorageJob));
        Prefix(M(typeof(JobMaker),"MakeJob",typeof(JobDef),typeof(LocalTargetInfo),typeof(LocalTargetInfo)),nameof(MakeJob));
        Prefix(AccessTools.PropertyGetter(typeof(HaulDestinationManager),"AllGroupsListInPriorityOrder"),nameof(Groups));
        foreach(string n in new[]{"LockerHaulDestination_Patch","SavedGearLocker_HaulToStorage_Patch","SavedGearLocker_Search_Patch","SavedGearLocker_Cell_Patch","SavedGearLocker_Container_Patch"})
            if(!previous||n=="LockerHaulDestination_Patch") harmony.CreateClassProcessor(assembly.GetType("AutomaticOutfitManager.Storage."+n,true)).Patch();
        map=Empty<Map>();map.haulDestinationManager=Empty<HaulDestinationManager>();map.thingGrid=Empty<ThingGrid>();
        var shelf=Empty<Building_Storage>();shelf.def=Empty<ThingDef>();shelf.def.passability=Traversability.PassThroughOnly;
        shelfThings.Add(shelf);
        owner=Empty<Pawn>();hauler=Empty<Pawn>();
        home=Empty<Area_Allowed>();remote=Empty<Area_Allowed>();
        areas[home]=new List<IntVec3>{floor,local};areas[remote]=new List<IntVec3>{far};
        localGroup=Group(local,StoragePriority.Preferred);remoteGroup=Group(far,StoragePriority.Critical);
        orderedGroups.AddRange(new[]{remoteGroup,localGroup});
        gear=Empty<Apparel>();gear.def=Empty<ThingDef>();gear.def.defName="PersonalCoat";gear.def.apparel=new ApparelProperties();gear.def.stackLimit=1;gear.stackCount=1;gear.thingIDNumber=12;
        AccessTools.Field(typeof(Thing),"positionInt").SetValue(gear,floor);
        weapon=Empty<ThingWithComps>();weapon.def=Empty<ThingDef>();weapon.def.defName="PersonalGun";weapon.def.equipmentType=EquipmentType.Primary;weapon.def.category=ThingCategory.Item;AccessTools.Field(typeof(ThingDef),"verbs").SetValue(weapon.def,new List<VerbProperties>{new VerbProperties()});weapon.def.stackLimit=1;weapon.thingIDNumber=13;weapon.stackCount=1;
        AccessTools.Field(typeof(Thing),"positionInt").SetValue(weapon,floor);
        state=new PawnApparelState{Pawn=owner,ActiveRuleId="anomaly",Transition=ApparelTransition.Active,OriginalApparel=new List<Apparel>{gear},OriginalWeapon=weapon};
        state.RestorationSourceRuleIds.Add("anomaly");
        component=Empty<AutomaticOutfitManagerGameComponent>();component.Rules=new List<ApparelRule>{new ApparelRule{Id="anomaly",ChangingArea=home}};component.PawnStates=new List<PawnApparelState>{state};
        JobDefOf.HaulToCell=Empty<JobDef>();JobDefOf.HaulToCell.defName="HaulToCell";
        // Run the native scanner and its native HaulToStorageJob entry point.
        // The previous decision selects the remote critical shelf fixture.
        var scanner=(WorkGiver_Haul)FormatterServices.GetUninitializedObject(typeof(WorkGiver_Haul).Assembly.GetTypes().First(t=>t.IsSubclassOf(typeof(WorkGiver_Haul))&&!t.IsAbstract));
        Check(!local.Standable(map)&&floor.Standable(map),"native pass-through locker is valid furniture but not standable floor");
        Job job=scanner.JobOnThing(hauler,gear,false);
        Check(job?.targetB.Cell==local,"native hauling keeps saved apparel in its originating locker despite remote higher priority");
        Check(job.count==1&&!job.haulOpportunisticDuplicates&&job.haulMode==HaulMode.ToCellStorage,"local storage haul owns one exact item");
        Check(state.SavedGearLockerRuleIds[gear.ThingID]=="anomaly","item-to-rule tracking is captured");
        // Actual native best-cell search and worker execute under AOM patches.
        Check(StoreUtility.TryFindBestBetterStoreCellFor(gear,hauler,map,StoragePriority.Unstored,Faction.OfPlayer,out IntVec3 cell,false)&&cell==local,"native mid-haul search ignores remote priority and finds local shelf");
        stored=true;AccessTools.Field(typeof(Thing),"positionInt").SetValue(gear,local);
        Check(scanner.JobOnThing(hauler,gear,false)==null,"correctly stored saved item is not shuffled");
        stored=false;AccessTools.Field(typeof(Thing),"positionInt").SetValue(gear,floor);
        rejected.Add(local);Check(scanner.JobOnThing(hauler,gear,false)==null,"full local shelf leaves saved gear on local ground");
        rejected.Clear();accept=false;Check(scanner.JobOnThing(hauler,gear,false)==null,"rejecting local filters leave saved gear on local ground");
        accept=true;
        storageEnabled=false;Check(scanner.JobOnThing(hauler,gear,false)==null,"disabled local storage leaves saved gear on its room floor");storageEnabled=true;
        Check(scanner.JobOnThing(hauler,gear,true)?.targetB.Cell==far,"explicit forced haul retains native destination");
        forcedCurrentJob=JobMaker.MakeJob(JobDefOf.HaulToCell,gear,far);forcedCurrentJob.playerForced=true;
        Check(StoreUtility.IsGoodStoreCell(far,map,gear,hauler,Faction.OfPlayer),"forced current haul keeps native destination validation after pickup");forcedCurrentJob=null;
        throwStorage=true;
        try { scanner.JobOnThing(hauler,gear,true);throw new Exception("expected native fixture failure"); }
        catch(InvalidOperationException) { }
        finally { throwStorage=false; }
        Check(scanner.JobOnThing(hauler,gear,false)?.targetB.Cell==local,"forced-search scope is restored after native call");
        eligible=false;Check(scanner.JobOnThing(hauler,gear,false)==null,"native hauler eligibility remains authoritative");eligible=true;
        claim=true;Check(scanner.JobOnThing(hauler,gear,false)==null,"prepared source claim blocks competing haul");claim=false;
        AccessTools.Field(typeof(Thing),"positionInt").SetValue(gear,far);
        groupCells[localGroup].Insert(0,far);
        Check(Plan(gear,out job)&&job?.targetB.Cell==local,"linked storage group cannot select its nearer cell outside the locker");
        groupCells[localGroup].Remove(far);
        Check(Plan(gear,out job)&&job?.targetB.Cell==local,"displaced saved item returns to its recorded locker");
        accept=false;Check(Plan(gear,out job)&&job?.targetB.Cell==floor&&job.haulMode==HaulMode.ToCellNonStorage,"displaced item uses local floor when storage rejects it");accept=true;
        emptyThings.Add(weapon);rejected.Add(local);
        Check(!Plan(gear,out job),"no free local ground or storage releases ordinary fallback");
        emptyThings.Clear();rejected.Clear();
        held=true;Check(!StoreUtility.IsGoodStoreCell(far,map,gear,hauler,Faction.OfPlayer),"carried gear retains locker restriction during destination refresh");held=false;
        reachable=false;Check(scanner.JobOnThing(hauler,gear,false)?.targetB.Cell==far,"inaccessible locker releases ordinary recovery fallback");reachable=true;
        state.Transition=ApparelTransition.Restoring;Check(scanner.JobOnThing(hauler,gear,false)?.targetB.Cell==far,"active restoration leaves saved-item recovery authoritative");state.Transition=ApparelTransition.Active;
        state.MapDepartureRequested=true;Check(!Home(gear,out _),"departure releases locality constraint");state.MapDepartureRequested=false;
        component.Rules[0].ChangingArea=null;Check(scanner.JobOnThing(hauler,gear,false)?.targetB.Cell==far,"removing locker preserves ordinary native hauling");component.Rules[0].ChangingArea=home;
        Check(Home(weapon,out Area weaponHome)&&weaponHome==home,"saved primary weapon uses the same locker policy");
        Check(Plan(weapon,out job)&&job?.targetB.Cell==local,"saved weapon can be put in local storage");
        state.WeaponRestorationRequested=true;Check(!Home(weapon,out _),"active primary-weapon restoration releases locality restriction");state.WeaponRestorationRequested=false;
        state.SavedGearLockerRuleIds.Clear();state.ActiveRuleId=null;state.RestorationSourceRuleIds.Clear();
        Check(!Home(gear,out _),"session without a locker retains native hauling");
        state.RestorationSourceRuleIds.AddRange(new[]{"anomaly","radiation"});
        component.Rules.Add(new ApparelRule{Id="radiation",ChangingArea=remote});
        AccessTools.Field(typeof(Thing),"positionInt").SetValue(gear,new IntVec3(50,0,50));
        Check(!Home(gear,out _),"ambiguous legacy gear outside multiple lockers is not assigned by guesswork");
        AccessTools.Field(typeof(Thing),"positionInt").SetValue(gear,floor);
        Check(Home(gear,out Area origin)&&origin==home,"multi-area session records the locker actually containing its saved item");
        state.ActiveRuleId="anomaly";state.RestorationSourceRuleIds.Remove("radiation");component.Rules.RemoveAt(1);
        var restored=RoundTrip(state.SavedGearLockerRuleIds);
        state=new PawnApparelState{Pawn=owner,ActiveRuleId="anomaly",Transition=ApparelTransition.Active,OriginalApparel=new List<Apparel>{gear},SavedGearLockerRuleIds=restored};
        Check(Home(gear,out Area restoredHome)&&restoredHome==home,"restored item/rule association resolves without an area object in the record");
        component.Rules[0].ChangingArea=remote;Check(Home(gear,out restoredHome)&&restoredHome==remote,"rule area remapping redirects persisted locker association");
        component.Rules.Clear();Check(!Home(gear,out _),"deleted rule releases saved-item locker association");
        // Shared work stock must still use the ordinary restock scanners with
        // the saved-personal policy and native storage callbacks installed.
        state.OriginalApparel.Clear();state.OriginalWeapon=null;
        var sharedRule=new ApparelRule {Id="anomaly",ChangingArea=home};
        sharedRule.RequiredApparel.Add(gear.def);sharedRule.RequiredWeapons.Add(weapon.def);
        component.Rules.Add(sharedRule);
        foreach(var pair in new[]{
            Tuple.Create<WorkGiver_Scanner,Thing>(new WorkGiver_LockerRestock(),gear),
            Tuple.Create<WorkGiver_Scanner,Thing>(new WorkGiver_WeaponLockerRestock(),weapon)})
        {
            string kind=pair.Item2==gear?"apparel":"weapon";
            AccessTools.Field(typeof(Thing),"positionInt").SetValue(pair.Item2,far);
            Check(!Home(pair.Item2,out _),"shared work "+kind+" has no saved-personal locker restriction");
            Check(pair.Item1.HasJobOnThing(hauler,pair.Item2)&&pair.Item1.JobOnThing(hauler,pair.Item2)?.targetB.Cell==local,
                "shared work "+kind+" is offered for cleanup into its selecting rule's locker");
            accept=false;
            Check(!pair.Item1.HasJobOnThing(hauler,pair.Item2)&&pair.Item1.JobOnThing(hauler,pair.Item2)==null,
                "shared work "+kind+" cleanup respects rejecting storage filters");accept=true;
            sharedRule.Enabled=false;
            Check(!pair.Item1.HasJobOnThing(hauler,pair.Item2),"disabled rule does not request shared "+kind+" restock");sharedRule.Enabled=true;
            sharedRule.ChangingArea=null;
            Check(!pair.Item1.HasJobOnThing(hauler,pair.Item2),"no-locker rule does not invent shared "+kind+" restock");sharedRule.ChangingArea=home;
            AccessTools.Field(typeof(Thing),"positionInt").SetValue(pair.Item2,local);stored=true;
            Check(!pair.Item1.HasJobOnThing(hauler,pair.Item2),"shared work "+kind+" already in matching locker is left alone");stored=false;
        }
        Console.WriteLine("Passed "+passed+" native saved-locker checks.");return 0;
    }
    static Faction faction=Empty<Faction>();
    public static bool PlayerFaction(ref Faction __result){__result=faction;return false;}
    public static bool Unstored(ref StoragePriority __result){__result=StoragePriority.Unstored;return false;}
    static Dictionary<string,string> RoundTrip(Dictionary<string,string> associations)
    {
        string path=Path.Combine(AppContext.BaseDirectory,"state.xml");
        try
        {
            var saved=new PawnApparelState { SavedGearLockerRuleIds=associations };
            Scribe.saver.InitSaving(path,"state");
            Scribe_Deep.Look(ref saved,"outfit");
            Scribe.saver.FinalizeSaving();
            var loaded=new PawnApparelState();
            Scribe.loader.InitLoading(path);
            Scribe_Deep.Look(ref loaded,"outfit");
            Scribe.loader.FinalizeLoading();
            Check(loaded.SavedGearLockerRuleIds.Count==associations.Count &&
                associations.All(pair=>loaded.SavedGearLockerRuleIds[pair.Key]==pair.Value),
                "production ExposeData round-trips item-to-locker associations through native XML Scribe");
            File.WriteAllText(path,"<state><outfit /></state>");
            var legacy=new PawnApparelState();
            Scribe.loader.InitLoading(path);
            Scribe_Deep.Look(ref legacy,"outfit");
            Scribe.loader.FinalizeLoading();
            Check(legacy.SavedGearLockerRuleIds!=null&&legacy.SavedGearLockerRuleIds.Count==0,
                "legacy save without locker associations loads an empty record");
            return loaded.SavedGearLockerRuleIds;
        }
        finally { Scribe.ForceStop(); if(File.Exists(path))File.Delete(path); }
    }
}












