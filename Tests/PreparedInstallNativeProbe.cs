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

internal static class PreparedInstallNativeProbe
{
    public static int Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
                string name=new AssemblyName(e.Name).Name+".dll";
                foreach(string dir in args){string p=Path.Combine(dir,name);if(File.Exists(p))return Assembly.LoadFrom(p);}
                return null;
            };
            Assembly.LoadFrom(Path.Combine(args[0],"Assembly-CSharp.dll"));
            return (int)Assembly.GetExecutingAssembly().GetType("PreparedInstallNativeChecks").GetMethod("Run")
                .Invoke(null,new object[]{args.Contains("--previous")});
        }
        catch(Exception e){Console.Error.WriteLine("FAIL: "+e.GetBaseException());return 1;}
    }
}
internal static class PreparedInstallNativeChecks
{
    static int passed, errors;
    sealed class Identity : IEqualityComparer<Thing>
    {
        public bool Equals(Thing a,Thing b)=>ReferenceEquals(a,b);
        public int GetHashCode(Thing t)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(t);
    }
    static readonly Dictionary<Thing,Map> maps=new Dictionary<Thing,Map>(new Identity());
    static readonly HashSet<Thing> destroyed=new HashSet<Thing>(new Identity());
    static Harmony harmony=new Harmony("aom.tests.prepared.install");
    static object Empty(Type t)=>FormatterServices.GetUninitializedObject(t);
    static void Check(bool b,string message){if(!b)throw new Exception(message);passed++;}
    static void Prefix(MethodBase method,string name)=>harmony.Patch(method,prefix:new HarmonyMethod(typeof(PreparedInstallNativeChecks),name));
    static void SetInstall(Blueprint_Install blueprint,MinifiedThing item)=>AccessTools.Method(typeof(Blueprint_Install),"SetThingToInstallFromMinified").Invoke(blueprint,new object[]{item});
    public static bool Quiet()=>false;
    public static bool Spawned(Thing __instance,ref bool __result){__result=maps.ContainsKey(__instance);return false;}
    public static bool MapValue(Thing __instance,ref Map __result){maps.TryGetValue(__instance,out __result);return false;}
    public static bool Destroyed(Thing __instance,ref bool __result){__result=destroyed.Contains(__instance);return false;}
    public static bool LogError(string text){if(text.StartsWith("Invalid count:"))errors++;return false;}
    public static bool JobText(ref string __result){__result="test installation";return false;}
    static Job Haul(Thing item,Thing destination,int count=0)
    {
        var j=(Job)Empty(typeof(Job));j.def=JobDefOf.HaulToContainer;
        j.targetA=item;j.targetB=destination;j.count=count;return j;
    }
    public static int Run(bool previous)
    {
        var candidate=Assembly.Load("AutomaticOutfitManager");
        var repair=AccessTools.Method(candidate.GetType("AutomaticOutfitManager.Detection.PreparedInstallHaul",true),"RepairPickupCount");
        // Native engine/world properties are shimmed only in this disposable
        // process. The candidate decision and native carry validation body run.
        Prefix(AccessTools.Method(typeof(DefOfHelper),"EnsureInitializedInCtor"),nameof(Quiet));
        Prefix(AccessTools.PropertyGetter(typeof(Thing),"Spawned"),nameof(Spawned));
        Prefix(AccessTools.PropertyGetter(typeof(Thing),"SpawnedOrAnyParentSpawned"),nameof(Spawned));
        Prefix(AccessTools.PropertyGetter(typeof(Thing),"Map"),nameof(MapValue));
        Prefix(AccessTools.PropertyGetter(typeof(Thing),"Destroyed"),nameof(Destroyed));
        Prefix(AccessTools.Method(typeof(Log),"Error",new[]{typeof(string)}),nameof(LogError));
        Prefix(AccessTools.Method(typeof(Job),"ToString",Type.EmptyTypes),nameof(JobText));
        JobDefOf.HaulToContainer=(JobDef)Empty(typeof(JobDef));JobDefOf.HaulToContainer.defName="HaulToContainer";
        JobDefOf.HaulToCell=(JobDef)Empty(typeof(JobDef));JobDefOf.HaulToCell.defName="HaulToCell";
        var map=(Map)Empty(typeof(Map));
        var item=(MinifiedThing)Empty(typeof(MinifiedThing));item.stackCount=1;maps[item]=map;
        var install=(Blueprint_Install)Empty(typeof(Blueprint_Install));maps[install]=map;
        SetInstall(install,item);
        bool Repair(Job j)=>(bool)repair.Invoke(null,new object[]{j});

        // Reproduce the native pickup -> consumed count -> dropped item ->
        // resumed fresh driver ordering captured in the gameplay log.
        var job=Haul(item,install,1);job.count=0;
        var pawn=(Pawn)Empty(typeof(Pawn));pawn.jobs=(Pawn_JobTracker)Empty(typeof(Pawn_JobTracker));pawn.jobs.curJob=job;
        if(!previous) Check(Repair(job),"dropped installation continuation is repaired");
        var native=AccessTools.Method(typeof(Toils_Haul),"ErrorCheckForCarry");
        Check(!(bool)native.Invoke(null,new object[]{pawn,item,false}),"native carry preflight accepts valid installation source");
        Check(errors==0,"resumed installation must not need native zero-count recovery");
        Check(job.count==1,"exact single item pickup preserved");
        Check(!Repair(job),"repair is idempotent");

        job=Haul(item,install,1);Check(!Repair(job)&&job.count==1,"fresh minified installation untouched");
        job.count=4;Check(!Repair(job)&&job.count==4,"positive count never rewritten");
        job=Haul(item,install);job.playerForced=true;Check(!Repair(job)&&job.count==0,"player-forced job untouched");
        job=Haul(item,install);job.def=JobDefOf.HaulToCell;Check(!Repair(job),"storage haul excluded");
        job=Haul(item,install);maps.Remove(item);Check(!Repair(job)&&job.count==0,"still-carried installation excluded");maps[item]=map;
        destroyed.Add(item);Check(!Repair(job),"destroyed source excluded");destroyed.Clear();
        item.stackCount=0;Check(!Repair(job),"empty source excluded");item.stackCount=2;Check(!Repair(job),"multi-item source excluded");item.stackCount=1;
        destroyed.Add(install);Check(!Repair(job),"destroyed blueprint excluded");destroyed.Clear();
        maps.Remove(install);Check(!Repair(job),"unspawned blueprint excluded");maps[install]=(Map)Empty(typeof(Map));Check(!Repair(job),"different map excluded");maps[install]=map;
        var other=(MinifiedThing)Empty(typeof(MinifiedThing));SetInstall(install,other);
        Check(!Repair(job),"different blueprint item excluded");SetInstall(install,item);
        var resource=(Thing)Empty(typeof(Thing));resource.stackCount=50;maps[resource]=map;
        job=Haul(resource,install,17);Check(!Repair(job)&&job.count==17,"partial ordinary resource count preserved");
        job.count=0;Check(!Repair(job)&&job.count==0,"exhausted ordinary resource haul excluded");
        job=Haul(item,(Frame)Empty(typeof(Frame)));Check(!Repair(job),"frame resource delivery excluded");
        job=Haul((Building)Empty(typeof(Building)),install);Check(!Repair(job),"unminified building excluded");
        Check(!Repair(null),"missing continuation excluded");

        var start=candidate.GetType("AutomaticOutfitManager.Patches.PawnJobTracker_StartJob_Patch",true);
        Check(start.GetMethods(AccessTools.all).Any(m=>m.GetMethodBody()!=null && PatchProcessor.GetOriginalInstructions(m).Any(i=>i.Calls(repair))),
            "built prepared-job admission calls production count repair");
        var boundary=AccessTools.Method(candidate.GetType("AutomaticOutfitManager.Detection.BoundaryJobAdmission",true),"TryBegin");
        Check(PatchProcessor.GetOriginalInstructions(boundary).Any(i=>i.Calls(repair)),"built boundary admission repairs direct replay too");
        Console.WriteLine(passed+" native installation continuation checks passed.");return 0;
    }
}
