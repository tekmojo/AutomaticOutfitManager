using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using RimWorld;
using Verse;

internal static class ConstructionChildNativeProbe
{
    public static int Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
                string name=new AssemblyName(e.Name).Name+".dll";
                foreach(string dir in args){string path=Path.Combine(dir,name);if(File.Exists(path))return Assembly.LoadFrom(path);}
                return null;
            };
            Assembly.LoadFrom(Path.Combine(args[0],"Assembly-CSharp.dll"));
            return (int)Assembly.GetExecutingAssembly().GetType("ConstructionNativeChecks")
                .GetMethod("Run").Invoke(null,null);
        }
        catch(Exception e){Console.Error.WriteLine("Native construction probe failed: "+e.GetBaseException());return 1;}
    }
}
internal static class ConstructionNativeChecks
{
    static int enumerations;
    public static bool EmptyAdjacent(ref IEnumerable<IntVec3> __result)
    { enumerations++;__result=Enumerable.Empty<IntVec3>();return false; }
    static void Check(bool result,string message){if(!result)throw new Exception(message);Console.WriteLine("PASS native: "+message);}
    public static int Run()
    {
        Assembly candidate=Assembly.Load("AutomaticOutfitManager");
        var adjacent=AccessTools.Method(typeof(GenAdj),nameof(GenAdj.CellsAdjacent8Way),new[]{typeof(Thing)});
        var picker=AccessTools.Method(typeof(RCellFinder),nameof(RCellFinder.TryFindGoodAdjacentSpotToTouch));
        Check(PatchProcessor.GetOriginalInstructions(picker).Count(i=>i.Calls(adjacent))==2,
            "installed native picker has two adjacent-cell candidate passes");
        var patch=candidate.GetType("AutomaticOutfitManager.Patches.ConstructionChildDestination_Patch",true);
        var transpiler=AccessTools.Method(patch,"Transpiler");
        var rewritten=((IEnumerable<CodeInstruction>)transpiler.Invoke(null,new object[]{PatchProcessor.GetOriginalInstructions(picker)})).ToList();
        Check(rewritten.Count(i=>i.operand is MethodInfo m && m.Name=="FilterCells")==2,
            "production filter is inserted in both installed native passes");
        var harmony=new Harmony("aom.tests.native.child.construction");
        harmony.CreateClassProcessor(patch).Patch();
        Check(true,"destination transpiler binds to installed native method");
        harmony.CreateClassProcessor(candidate.GetType("AutomaticOutfitManager.Patches.ConstructionChildDelivery_Patch",true)).Patch();
        Check(true,"delivery postfix binds to installed shared resource generator");
        // Execute the real picker, replacing only its map-dependent enumeration.
        // Empty first and fallback passes must still return the target footprint.
        harmony.Patch(adjacent,prefix:new HarmonyMethod(typeof(ConstructionNativeChecks),nameof(EmptyAdjacent)));
        var target=(Frame)FormatterServices.GetUninitializedObject(typeof(Frame));
        AccessTools.Field(typeof(Thing),"positionInt").SetValue(target,new IntVec3(50,0,60));
        Check(!RCellFinder.TryFindGoodAdjacentSpotToTouch(null,target,out IntVec3 cell) &&
            cell==new IntVec3(50,0,60) && enumerations==2,
            "actual native body executes both filtered passes and preserves footprint fallback");
        return 0;
    }
}
