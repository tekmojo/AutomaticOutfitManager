using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;
using Verse.AI;
using System.Runtime.Serialization;

class RitualNativeProbe
{
    static int Main(string[] args)
    {
        // Keep this bootstrap free of references to game-dependent types. A
        // direct call can make the CLR load their fields before Main's handler
        // or catch block is active, leaving a Windows application-error dialog.
        try {
            AppDomain.CurrentDomain.AssemblyResolve += (sender,e) => {
                string name=new AssemblyName(e.Name).Name+".dll";
                foreach(string dir in args) {string path=Path.Combine(dir,name);if(File.Exists(path))return Assembly.LoadFrom(path);}
                return null;
            };
            if(args.Length==0) throw new ArgumentException("The installed game assembly directory is required.");
            Assembly.LoadFrom(Path.Combine(args[0], "Assembly-CSharp.dll"));
            Type checks=Assembly.GetExecutingAssembly().GetType("NativeChecks",true);
            return (int)checks.GetMethod("Run",BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
        }
        catch(Exception e){Console.Error.WriteLine("Native ritual probe failed: "+e.GetBaseException());return 1;}
    }
}
static class NativeChecks
{
    static bool executed;
    static object reservedSeat;
    public static bool CaptureSeat(IntVec3 __1, ref bool __result) { reservedSeat=__1;__result=true;return false; }

    public static bool BlockExecute() { executed=true;return false; }
    sealed class AlwaysTrigger:Trigger { public override bool ActivateOn(Lord l,TriggerSignal s)=>true; }
    sealed class GateFilter:TriggerFilter { public bool Ready;public override bool AllowActivation(Lord l,TriggerSignal s)=>Ready; }
    static void Check(bool b,string s){if(!b)throw new Exception(s);Console.WriteLine("PASS native: "+s);}
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Run()
    {
        var candidate=Assembly.Load("AutomaticOutfitManager");
        Check((int)TargetIndex.A==1 && (int)TargetIndex.B==2,"installed native TargetIndex enum is verified");
        var harmony=new Harmony("aom.tests.native.ritual");
        // Execute the installed native driver's reservation body. Only the map
        // reservation sink is intercepted; Job.GetTarget and argument ordering
        // are real game code, preventing a fixture from swapping A and B again.
        harmony.Patch(AccessTools.Method(typeof(ReservationUtility),"ReserveSittableOrSpot"),
            prefix:new HarmonyMethod(typeof(NativeChecks),nameof(CaptureSeat)));
        var driver=(JobDriver_Spectate)FormatterServices.GetUninitializedObject(typeof(JobDriver_Spectate));
        driver.pawn=(Pawn)FormatterServices.GetUninitializedObject(typeof(Pawn));
        driver.job=(Job)FormatterServices.GetUninitializedObject(typeof(Job));
        driver.job.targetA=new IntVec3(157,0,93);driver.job.targetB=new IntVec3(158,0,99);
        Check(driver.TryMakePreToilReservations(false)&&(IntVec3)reservedSeat==driver.job.targetA.Cell,
            "actual native spectator driver reserves A, not shared ceremony focus B");
        harmony.CreateClassProcessor(candidate.GetType("AutomaticOutfitManager.Patches.RitualSpectatorSeat_Patch",true)).Patch();
        Check(true,"seat admission postfix binds to installed native search API");
        Type patch=candidate.GetType("AutomaticOutfitManager.Patches.RitualStageEnd_OutfitPreparation_Patch",true);
        var methods=((IEnumerable<MethodBase>)patch.GetMethod("TargetMethods",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null)).ToList();
        foreach(Type type in new[]{typeof(StageEndTrigger_RolesArrived),typeof(StageEndTrigger_PawnDeliveredOrNotValid),typeof(StageEndTrigger_DurationPercentage)})
            Check(methods.Any(method=>method.Module==type.Module && method.MetadataToken==AccessTools.Method(type,"MakeTrigger").MetadataToken),type.Name+" inherited trigger factory is covered");
        harmony.CreateClassProcessor(patch).Patch();
        var roles=new StageEndTrigger_RolesArrived();
        Trigger native=roles.MakeTrigger(null,TargetInfo.Invalid,Enumerable.Empty<TargetInfo>(),new RitualStage());
        Check(native.filters!=null&&native.filters.Any(f=>f.GetType().Name=="RitualOutfitReadyFilter"),"real native arrival trigger receives production readiness filter");
        // Run the actual installed Transition.CheckSignal body, intercepting only
        // Execute to avoid needing a running Unity map. Native trigger/filter
        // ordering must reject before any transition preAction/duty change.
        harmony.Patch(AccessTools.Method(typeof(Transition),"Execute"),prefix:new HarmonyMethod(typeof(NativeChecks),nameof(BlockExecute)));
        var gate=new GateFilter();var trigger=new AlwaysTrigger{filters=new List<TriggerFilter>{gate}};
        var transition=new Transition(new LordToil_End(),new LordToil_End());transition.AddTrigger(trigger);
        Check(!transition.CheckSignal(null,default(TriggerSignal))&&!executed,"blocked arrival cannot execute native transition actions");
        gate.Ready=true;
        Check(transition.CheckSignal(null,default(TriggerSignal))&&executed,"ready arrival resumes actual native transition");
        executed=false;var cancellation=new Transition(new LordToil_End(),new LordToil_End());cancellation.AddTrigger(new AlwaysTrigger());
        Check(cancellation.CheckSignal(null,default(TriggerSignal))&&executed,"unfiltered cancellation still executes native transition");
        // Harmony must accept every remaining production prefix/postfix signature
        // against the installed game before this candidate can be deployed.
        foreach(string name in new[]{"RitualTick_OutfitPreparation_Patch","RitualToilTick_OutfitPreparation_Patch","RitualReport_OutfitPreparation_Patch","RitualSave_OutfitPreparation_Patch"}) {
            harmony.CreateClassProcessor(candidate.GetType("AutomaticOutfitManager.Patches."+name,true)).Patch();
            Check(true,name+" binds to installed native method");
        }
        return 0;
    }
}
