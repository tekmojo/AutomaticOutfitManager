using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

internal static class ConstructionFloorNativeProbe
{
    public static int Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
                foreach (string dir in args) {
                    string path=Path.Combine(dir,new AssemblyName(e.Name).Name+".dll");
                    if(File.Exists(path)) return Assembly.LoadFrom(path);
                }
                return null;
            };
            Assembly.LoadFrom(Path.Combine(args[0],"Assembly-CSharp.dll"));
            return (int)Assembly.GetExecutingAssembly().GetType("ConstructionFloorChecks")
                .GetMethod("Run").Invoke(null,new object[]{args.Contains("--previous"),args.Contains("--previous-refresh")});
        }
        catch(Exception e) { Console.Error.WriteLine("FAIL: "+e.GetBaseException()); return 1; }
    }
}

internal static class ConstructionFloorChecks
{
    static readonly Harmony harmony=new Harmony("aom.tests.native.floor.claims");
    static Map map;
    static readonly System.Collections.Generic.List<Thing> things=new System.Collections.Generic.List<Thing>();
    public static bool Things(ref System.Collections.Generic.List<Thing> __result){__result=things;return false;}
    static int passed;
    static bool floor=true, reserve=true, disabled, drafted, downed, mental, spawned=true;
    static Thing blocker;
    static Job blockerJob, resourceJob;
    static System.Collections.Generic.List<ThingDefCountClass> costs;
    static object Empty(Type type)=>FormatterServices.GetUninitializedObject(type);
    static T Empty<T>()=>(T)Empty(typeof(T));
    static MethodInfo Method(Type type,string name,Type[] parameters=null)
    { try { return AccessTools.Method(type,name,parameters); } catch { throw new Exception("Method lookup failed: "+type.FullName+"."+name); } }
    static void Check(bool result,string message) { if(!result)throw new Exception(message);passed++;Console.WriteLine("PASS: "+message); }
    static void Prefix(MethodBase method,string name)=>harmony.Patch(method,prefix:new HarmonyMethod(typeof(ConstructionFloorChecks),name));
    public static bool Quiet()=>false;
    public static bool Costs(ref System.Collections.Generic.List<ThingDefCountClass> __result){__result=costs;return false;}
    public static bool True(ref bool __result){__result=true;return false;}
    public static bool False(ref bool __result){__result=false;return false;}
    public static bool Spawned(ref bool __result){__result=spawned;return false;}
    public static bool Drafted(ref bool __result){__result=drafted;return false;}
    public static bool Downed(ref bool __result){__result=downed;return false;}
    public static bool Mental(ref bool __result){__result=mental;return false;}
    public static bool MapValue(ref Map __result){__result=map;return false;}
    public static bool Tick(ref int __result){__result=100;return false;}
    public static bool Floor(ref bool __result){__result=floor;return false;}
    public static bool Reserve(ref bool __result){__result=reserve;return false;}
    public static bool Disabled(ref bool __result){__result=disabled;return false;}
    public static bool Blocker(ref Thing __result){__result=blocker;return false;}
    public static bool BlockingJob(ref Job __result){__result=blockerJob;return false;}
    public static bool ResourceJob(ref Job __result){__result=resourceJob;return false;}
    public static bool MakeJob(JobDef __0,LocalTargetInfo __1,ref Job __result)
    {__result=Empty<Job>();__result.def=__0;__result.targetA=__1;return false;}
    static Job CellJob(IntVec3 cell) {var job=Empty<Job>();job.def=JobDefOf.RemoveFloor;job.targetA=cell;return job;}
    public static int Run(bool previous, bool previousRefresh)
    {
        var candidate=Assembly.Load("AutomaticOutfitManager");
        Type Registry=candidate.GetType("AutomaticOutfitManager.Detection.ManagedWorkClaimRegistry",true);
        Type Filter=candidate.GetType("AutomaticOutfitManager.Detection.ManagedWorkCandidateFilter",true);
        Type Component=candidate.GetType("AutomaticOutfitManager.Core.AutomaticOutfitManagerGameComponent",true);
        Type Diagnostics=candidate.GetType("AutomaticOutfitManager.Detection.TransitionActivityDiagnostics",true);
        Type Access=candidate.GetType("AutomaticOutfitManager.Patches.PausedAreaWorkFilter",true);
        Prefix(Method(typeof(DefOfHelper),"EnsureInitializedInCtor"),nameof(Quiet));
        Prefix(AccessTools.PropertyGetter(typeof(Thing),"Spawned"),nameof(Spawned));
        Prefix(AccessTools.PropertyGetter(typeof(Thing),"Map"),nameof(MapValue));
        Prefix(AccessTools.PropertyGetter(typeof(Pawn),"Drafted"),nameof(Drafted));
        Prefix(AccessTools.PropertyGetter(typeof(Pawn),"Downed"),nameof(Downed));
        Prefix(AccessTools.PropertyGetter(typeof(Pawn),"InMentalState"),nameof(Mental));
        Prefix(Method(typeof(GenGrid),"InBounds",new[]{typeof(IntVec3),typeof(Map)}),nameof(True));
        Prefix(AccessTools.PropertyGetter(Registry,"CurrentTick"),nameof(Tick));
        // World services are replaced; the actual scanner branches, native
        // floor eligibility/job bodies, claim registry and AOM filters execute.
        map=Empty<Map>();map.terrainGrid=Empty<TerrainGrid>();
        Prefix(Method(typeof(TerrainGrid),"CanRemoveTopLayerAt"),nameof(Floor));
        Prefix(Method(typeof(GenConstruct),"CanTouchTargetFromValidCell"),nameof(True));
        Prefix(Method(typeof(GenConstruct),"FirstBlockingThing"),nameof(Blocker));
        Prefix(Method(typeof(GenConstruct),"HandleBlockingThingJob"),nameof(BlockingJob));
        Prefix(Method(typeof(GenConstruct),"CanConstruct",new[]{typeof(Thing),typeof(Pawn),typeof(WorkTypeDef),typeof(bool),typeof(JobDef)}),nameof(True));
        Prefix(Method(typeof(GenConstruct),"CanGetResources_NewTemp"),nameof(True));
        Prefix(Method(typeof(ReservationUtility),"CanReserve",new[]{typeof(Pawn),typeof(LocalTargetInfo),typeof(int),typeof(int),typeof(ReservationLayerDef),typeof(bool)}),nameof(Reserve));
        Prefix(Method(typeof(Pawn),"WorkTypeIsDisabled"),nameof(Disabled));
        Prefix(Method(typeof(JobMaker),"MakeJob",new[]{typeof(JobDef),typeof(LocalTargetInfo)}),nameof(MakeJob));
        Prefix(Method(typeof(WorkGiver_ConstructDeliverResources),"ResourceDeliverJobFor"),nameof(ResourceJob));
        Prefix(AccessTools.PropertyGetter(Component,"Current"),nameof(Quiet));
        Prefix(Method(Component,"ReleaseNativeReservations"),nameof(Quiet));
        Prefix(Method(Diagnostics,"Rejected"),nameof(Quiet));
        Prefix(Method(Filter,"LogConflict"),nameof(Quiet));
        foreach(var method in Access.GetMethods(AccessTools.all).Where(m=>m.Name=="ShouldRejectPausedScannerTarget"||m.Name=="ShouldRejectScannerTarget")) Prefix(method,nameof(False));

        JobDefOf.RemoveFloor=Empty<JobDef>();JobDefOf.RemoveFloor.defName="RemoveFloor";
        JobDefOf.HaulToContainer=Empty<JobDef>();JobDefOf.HaulToContainer.defName="HaulToContainer";
        JobDefOf.HaulToCell=Empty<JobDef>();JobDefOf.HaulToCell.defName="HaulToCell";
        WorkTypeDefOf.Construction=Empty<WorkTypeDef>();WorkTypeDefOf.Hauling=Empty<WorkTypeDef>();
        WorkGiverDefOf.ConstructRemoveFloors=Empty<WorkGiverDef>();WorkGiverDefOf.ConstructRemoveFloors.workType=WorkTypeDefOf.Construction;
        var scanner=Empty<WorkGiver_ConstructDeliverResourcesToBlueprints>();scanner.def=Empty<WorkGiverDef>();scanner.def.workType=WorkTypeDefOf.Construction;
        var owner=Empty<Pawn>();var other=Empty<Pawn>();
        var blue=Empty<Blueprint_Build>();blue.def=Empty<ThingDef>();blue.def.entityDefToBuild=Empty<TerrainDef>();
        var cell=new IntVec3(193,0,87);AccessTools.Field(typeof(Thing),"positionInt").SetValue(blue,cell);
        costs=new System.Collections.Generic.List<ThingDefCountClass>{new ThingDefCountClass(Empty<ThingDef>(),1)};
        Prefix(Method(typeof(Blueprint_Build),"TotalMaterialCost"),nameof(Costs));
        resourceJob=Empty<Job>();resourceJob.def=JobDefOf.HaulToContainer;
        var has=Method(typeof(WorkGiver_ConstructDeliverResourcesToBlueprints),"HasJobOnThing");
        var get=Method(typeof(WorkGiver_ConstructDeliverResourcesToBlueprints),"JobOnThing");
        // Bind the existing final candidate check too: without the new patch,
        // Has says yes while JobOnThing's production postfix removes the job.
        harmony.Patch(get,postfix:new HarmonyMethod(Method(candidate.GetType("AutomaticOutfitManager.Patches.WorkGiverPausedArea_JobOnThing_Patch",true),"Postfix")));
        if(!previous) harmony.CreateClassProcessor(candidate.GetType("AutomaticOutfitManager.Patches.ConstructionFloorClaims_Patch",true)).Patch();
        var claim=Method(Registry,"TryClaim");var release=Method(Registry,"ReleaseAll");
        Check((bool)claim.Invoke(null,new object[]{owner,CellJob(cell),15000}),"owner claims pending floor job");
        bool eligible=scanner.HasJobOnThing(other,blue);Job generated=scanner.JobOnThing(other,blue);
        Check(generated==null,"production final candidate filter rejects another pawn's claimed floor");
        Check(!eligible,"claimed floor must be rejected during native eligibility");
        Check(scanner.HasJobOnThing(owner,blue)&&scanner.JobOnThing(owner,blue)?.def==JobDefOf.RemoveFloor,"rightful claimant retains native floor work");
        Check(scanner.HasJobOnThing(other,blue,true)&&scanner.JobOnThing(other,blue,true)?.def==JobDefOf.RemoveFloor,"explicit player orders stay native");
        foreach(string control in new[]{"drafted","downed","mental"}) {
            drafted=control=="drafted";downed=control=="downed";mental=control=="mental";
            Check(scanner.HasJobOnThing(other,blue)&&scanner.JobOnThing(other,blue)?.def==JobDefOf.RemoveFloor,"native control bypass agrees at both stages: "+control);
        }
        drafted=downed=mental=false;
        release.Invoke(null,new object[]{owner});
        Check(scanner.HasJobOnThing(other,blue)&&scanner.JobOnThing(other,blue)?.targetA.Cell==cell,"released claim immediately restores floor work");
        claim.Invoke(null,new object[]{owner,CellJob(new IntVec3(194,0,87)),15000});
        Check(scanner.HasJobOnThing(other,blue)&&scanner.JobOnThing(other,blue)?.def==JobDefOf.RemoveFloor,"unrelated cell claim does not suppress construction");
        release.Invoke(null,new object[]{owner});claim.Invoke(null,new object[]{owner,CellJob(cell),15000});
        floor=false;
        Check(scanner.HasJobOnThing(other,blue)&&scanner.JobOnThing(other,blue)==resourceJob,"material delivery without floor prerequisite stays native even at claimed cell");
        floor=true;blocker=Empty<Thing>();blockerJob=Empty<Job>();blockerJob.def=JobDefOf.HaulToCell;
        Check(scanner.HasJobOnThing(other,blue)&&scanner.JobOnThing(other,blue)==blockerJob,"earlier blocking-thing branch unaffected by floor claim");
        blocker=null;
        scanner.def.workType=WorkTypeDefOf.Hauling;
        Check(!scanner.HasJobOnThing(other,blue)&&scanner.JobOnThing(other,blue)==null,"native hauling scanner cannot remove floors");
        scanner.def.workType=WorkTypeDefOf.Construction;
        release.Invoke(null,new object[]{owner});resourceJob=null;
        reserve=false;
        Check(!scanner.HasJobOnThing(other,blue)&&scanner.JobOnThing(other,blue)==null,"native floor reservation denial preserved");
        reserve=true;disabled=true;
        Check(!scanner.HasJobOnThing(other,blue)&&scanner.JobOnThing(other,blue)==null,"disabled floor work remains unavailable");
        disabled=false;
        Prefix(Method(typeof(ThingGrid),"ThingsListAt",new[]{typeof(IntVec3)}),nameof(Things));
        map.thingGrid=Empty<ThingGrid>();
        blue.thingIDNumber=3772288;things.Add(blue);
        AccessTools.Field(typeof(WorkGiverDef),"workerInt").SetValue(scanner.def,scanner);
        Job pending=scanner.JobOnThing(other,blue);
        pending.workGiverDef=scanner.def;pending.loadID=98765;
        Check(pending.def==JobDefOf.RemoveFloor&&pending.ignoreDesignations,"real blueprint scanner generates floor prerequisite without manual designation");
        Type State=candidate.GetType("AutomaticOutfitManager.State.PawnApparelState",true);
        Type FloorWork=candidate.GetType("AutomaticOutfitManager.Detection.PreparedFloorWork",true);
        object state=Activator.CreateInstance(State);
        AccessTools.Field(State,"Pawn").SetValue(state,other);
        Prefix(Method(candidate.GetType("AutomaticOutfitManager.Detection.ProtectedBoundaryRetryRegistry",true),"Clear",new[]{typeof(Pawn),typeof(Job)}),nameof(Quiet));
        var capture=Method(Component,"CapturePendingWork");var clear=Method(Component,"ClearPendingWork");
        var sourceId=AccessTools.Field(State,"PendingFloorBlueprintId");var savedJob=AccessTools.Field(State,"PendingWorkJob");
        capture.Invoke(null,new object[]{state,pending,true});
        Check((int)sourceId.GetValue(state)==blue.thingIDNumber,"production capture retains exact source blueprint identity");
        var refresh=Method(candidate.GetType("AutomaticOutfitManager.Patches.PawnJobTracker_StartJob_Patch",true),"TryRefreshDesignationSensitiveWork");
        bool Refresh(Job job,int id) {
            var args=new object[]{other,job,id,null,null,false};
            bool valid=(bool)refresh.Invoke(null,args);
            if(valid) Check(((Job)args[3]).def==job.def&&((Job)args[3]).targetA==job.targetA,"refresh validates the original floor target");
            return valid;
        }
        bool ready=previousRefresh ? scanner.HasJobOnCell(other,cell) : Refresh(pending,(int)sourceId.GetValue(state));
        Check(ready,"blueprint floor continuation must survive outfit preparation");
        Check(ReferenceEquals(savedJob.GetValue(state),pending),"validation does not replace or alias the saved continuation");
        pending.playerForced=true;
        Check(Refresh(pending,blue.thingIDNumber),"forced prerequisite remains native");pending.playerForced=false;
        claim.Invoke(null,new object[]{owner,CellJob(cell),15000});
        Check(!Refresh(pending,blue.thingIDNumber),"competing floor claim prevents replay");release.Invoke(null,new object[]{owner});
        claim.Invoke(null,new object[]{other,CellJob(cell),15000});
        Check(Refresh(pending,blue.thingIDNumber),"own prepared claim permits refresh");release.Invoke(null,new object[]{other});
        reserve=false;Check(!Refresh(pending,blue.thingIDNumber),"native reservation denial still cancels continuation");reserve=true;
        disabled=true;Check(!Refresh(pending,blue.thingIDNumber),"disabled work still cancels continuation");disabled=false;
        resourceJob=Empty<Job>();resourceJob.def=JobDefOf.HaulToContainer;floor=false;
        Check(!Refresh(pending,blue.thingIDNumber),"now-ready delivery is not permission to replay obsolete floor removal");floor=true;
        blocker=Empty<Thing>();blockerJob=resourceJob;
        Check(!Refresh(pending,blue.thingIDNumber),"changed blocking job cannot validate old floor removal");blocker=null;
        things.Clear();Check(!Refresh(pending,blue.thingIDNumber),"cancelled or moved source blueprint cancels continuation");
        var replacement=Empty<Blueprint_Build>();replacement.def=blue.def;replacement.thingIDNumber=3772289;
        AccessTools.Field(typeof(Thing),"positionInt").SetValue(replacement,cell);things.Add(replacement);
        Check(!Refresh(pending,blue.thingIDNumber),"replacement blueprint at same cell cannot inherit original continuation");
        capture.Invoke(null,new object[]{state,pending,true});
        Check((int)sourceId.GetValue(state)==blue.thingIDNumber,"recapturing same work cannot adopt replacement blueprint");
        things.Add(blue);
        Check((int)Method(FloorWork,"CaptureBlueprintId").Invoke(null,new object[]{other,pending})==-1,"ambiguous source capture is conservative");
        things.Clear();things.Add(blue);
        Check(!Refresh(pending,-1),"legacy pending job without source identity is rejected safely");
        clear.Invoke(null,new object[]{state});
        Check(savedJob.GetValue(state)==null&&(int)sourceId.GetValue(state)==-1,"clearing pending work clears source identity");
        capture.Invoke(null,new object[]{state,pending,true});
        Method(Component,"TransferPendingBoundaryWorkToTracker").Invoke(null,new object[]{state});
        Check(savedJob.GetValue(state)==null&&(int)sourceId.GetValue(state)==-1,"tracker transfer clears saved continuation metadata");
        var expose=PatchProcessor.GetOriginalInstructions(Method(State,"ExposeData"));
        Check(expose.Any(i=>Equals(i.operand,"pendingFloorBlueprintId"))&&expose.Any(i=>Equals(i.operand,sourceId)),"production save schema persists numeric blueprint identity");
        Check((int)sourceId.GetValue(Activator.CreateInstance(State))==-1,"missing legacy save field defaults to unavailable source");
        var ordinary=CellJob(cell);ordinary.workGiverDef=Empty<WorkGiverDef>();var ordinaryScanner=new StandaloneFloorScanner();
        AccessTools.Field(typeof(WorkGiverDef),"workerInt").SetValue(ordinary.workGiverDef,ordinaryScanner);
        Check(Refresh(ordinary,-1),"ordinary designated floor job still uses its native cell scanner");
        Check(!((bool)Method(FloorWork,"UsesBlueprintScanner").Invoke(null,new object[]{ordinary})),"ordinary floor work does not require blueprint context");
        capture.Invoke(null,new object[]{state,ordinary,true});
        Check((int)sourceId.GetValue(state)==-1,"unrelated pending work never inherits blueprint metadata");
        Console.WriteLine(passed+" native blueprint floor selection and continuation checks passed.");return 0;
    }
}

internal sealed class StandaloneFloorScanner : WorkGiver_Scanner
{
    public override bool HasJobOnCell(Pawn pawn,IntVec3 cell,bool forced=false)=>true;
    public override Job JobOnCell(Pawn pawn,IntVec3 cell,bool forced=false) {
        var job=(Job)FormatterServices.GetUninitializedObject(typeof(Job));job.def=JobDefOf.RemoveFloor;job.targetA=cell;return job;
    }
}
