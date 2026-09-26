using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

internal static class RepairComponentNativeProbe
{
    public static int Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
                foreach (string dir in args.Take(3)) {
                    string path = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll");
                    if (File.Exists(path)) return Assembly.LoadFrom(path);
                }
                return null;
            };
            return (int)Assembly.GetExecutingAssembly().GetType("RepairNativeChecks")
                .GetMethod("Run").Invoke(null, new object[] { args.Contains("--previous") });
        }
        catch (Exception e) { Console.Error.WriteLine(e.GetBaseException()); return 1; }
    }
}

internal static class RepairNativeChecks
{
    static Pawn worker, owner;
    static Map map;
    static Thing claimed, alternate;
    static Thing[] candidates;
    static bool hasClaims = true, throwSearch;
    static Thing forbidden, unreservable;
    static int checks, searchCalls;
    static Assembly candidate;
    static MethodInfo finder;
    static object scanner;
    static readonly Harmony harmony = new Harmony("aom.tests.repair.native");
    static object Empty(Type type) => FormatterServices.GetUninitializedObject(type);
    static void Check(bool value, string label)
    { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS native: " + label); }
    static void Prefix(MethodBase target, string name)
    { if (target == null) throw new Exception("Missing shim target: " + name); harmony.Patch(target, prefix: new HarmonyMethod(typeof(RepairNativeChecks), name)); }
    public static bool MapValue(ref Map __result) { __result = map; return false; }
    public static bool True(ref bool __result) { __result = true; return false; }
    public static bool False(ref bool __result) { __result = false; return false; }
    public static bool Claims(ref bool __result) { __result = hasClaims; return false; }
    public static bool Claim(Pawn __0, Thing __2, ref bool __result)
    { __result = hasClaims && __0 != owner && __2 == claimed; return false; }
    public static bool Forbidden(Thing __0, ref bool __result) { __result = __0 == forbidden; return false; }
    public static bool CanReserve(LocalTargetInfo __1, ref bool __result)
    { __result = __1.Thing != unreservable; return false; }
    public static bool Quiet() => false;
    public static bool Danger(ref Danger __result) { __result = Verse.Danger.Some; return false; }
    public static bool Traverse(Pawn __0, ref TraverseParms __result)
    { __result = new TraverseParms { pawn = __0 }; return false; }
    public static bool Search(Predicate<Thing> __6, ref Thing __result)
    {
        searchCalls++;
        if (throwSearch) throw new InvalidOperationException("search fixture exception");
        __result = candidates.FirstOrDefault(t => __6 == null || __6(t));
        return false;
    }
    // Keep the installed native HasJobOnThing component-search/return body.
    // Building/faction/home/fire checks precede it and are outside this fixture.
    public static IEnumerable<CodeInstruction> EligibleBuilding(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        int call = code.FindIndex(i => i.Calls(finder));
        if (call < 2 || code[call - 2].opcode != OpCodes.Ldarg_0 || code[call - 1].opcode != OpCodes.Ldarg_1)
            throw new Exception("Native HasJob component gate changed");
        return code.Skip(call - 2);
    }
    // Keep native JobOnThing's search and target/count construction, replacing
    // only JobMaker's engine pool with a plain job allocation for this process.
    public static Job MakeJob(JobDef def, LocalTargetInfo a, LocalTargetInfo b)
        => new Job { def = def, targetA = a, targetB = b };
    public static IEnumerable<CodeInstruction> PlainJob(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var i in instructions) {
            if (i.operand is MethodInfo m && m.DeclaringType == typeof(JobMaker) && m.Name == "MakeJob")
                i.operand = AccessTools.Method(typeof(RepairNativeChecks), nameof(MakeJob));
            yield return i;
        }
    }
    static bool Has(Pawn p, bool forced = false) => ((WorkGiver_FixBrokenDownBuilding)scanner).HasJobOnThing(p, alternate, forced);
    static Job Job(Pawn p, bool forced = false) => ((WorkGiver_FixBrokenDownBuilding)scanner).JobOnThing(p, alternate, forced);
    public static int Run(bool previous)
    {
        candidate = Assembly.Load("AutomaticOutfitManager");
        // Engine/world shims stay in this disposable process. Candidate
        // validators, scope prefixes/finalizers and search transpiler are real.
        Prefix(AccessTools.Method(typeof(DefOfHelper), "EnsureInitializedInCtor"), nameof(Quiet));
        Prefix(AccessTools.Method(typeof(JobFailReason), "Is", new[] { typeof(string), typeof(string) }), nameof(Quiet));
        worker = (Pawn)Empty(typeof(Pawn)); owner = (Pawn)Empty(typeof(Pawn)); map = (Map)Empty(typeof(Map));
        claimed = (Thing)Empty(typeof(Thing)); alternate = (Thing)Empty(typeof(Thing));
        ThingDefOf.ComponentIndustrial = (ThingDef)Empty(typeof(ThingDef));
        JobDefOf.FixBrokenDownBuilding = (JobDef)Empty(typeof(JobDef));
        JobDefOf.FixBrokenDownBuilding.defName = "FixBrokenDownBuilding";
        foreach (string name in new[] { "Drafted", "Downed", "InMentalState" })
            Prefix(AccessTools.PropertyGetter(typeof(Pawn), name), nameof(False));
        Prefix(AccessTools.PropertyGetter(typeof(Thing), "Spawned"), nameof(True));
        Prefix(AccessTools.PropertyGetter(typeof(Thing), "Map"), nameof(MapValue));
        Prefix(AccessTools.Method(typeof(ForbidUtility), "IsForbidden", new[] { typeof(Thing), typeof(Pawn) }), nameof(Forbidden));
        Prefix(AccessTools.Method(typeof(ReservationUtility), "CanReserve", new[] { typeof(Pawn), typeof(LocalTargetInfo), typeof(int), typeof(int), typeof(ReservationLayerDef), typeof(bool) }), nameof(CanReserve));
        finder = AccessTools.Method(typeof(WorkGiver_FixBrokenDownBuilding), "FindClosestComponent");
        var danger = PatchProcessor.GetOriginalInstructions(finder).Select(i => i.operand).OfType<MethodInfo>()
            .Single(m => m.Name == "NormalMaxDanger");
        Prefix(danger, nameof(Danger));
        var traverse = typeof(TraverseParms).GetMethods().Single(m => m.Name == "For" && m.GetParameters().Length == 7 && m.GetParameters()[0].ParameterType == typeof(Pawn));
        Prefix(traverse, nameof(Traverse));
        Prefix(AccessTools.Method(typeof(GenClosest), "ClosestThingReachable"), nameof(Search));
        var registry = candidate.GetType("AutomaticOutfitManager.Detection.ManagedWorkClaimRegistry", true);
        Prefix(AccessTools.PropertyGetter(registry, "HasClaims"), nameof(Claims));
        Prefix(AccessTools.Method(registry, "IsClaimedByOther", new[] { typeof(Pawn), typeof(Map), typeof(Thing), typeof(IntVec3) }), nameof(Claim));
        finder = AccessTools.Method(typeof(WorkGiver_FixBrokenDownBuilding), "FindClosestComponent");
        var has = AccessTools.Method(typeof(WorkGiver_FixBrokenDownBuilding), "HasJobOnThing");
        var job = AccessTools.Method(typeof(WorkGiver_FixBrokenDownBuilding), "JobOnThing");
        Check(PatchProcessor.GetOriginalInstructions(has).Count(i => i.Calls(finder)) == 1 &&
              PatchProcessor.GetOriginalInstructions(job).Count(i => i.Calls(finder)) == 1,
            "installed native acceptance and production share the component finder");
        harmony.Patch(has, transpiler: new HarmonyMethod(typeof(RepairNativeChecks), nameof(EligibleBuilding)));
        harmony.Patch(job, transpiler: new HarmonyMethod(typeof(RepairNativeChecks), nameof(PlainJob)));
        // Enable production deferred patch Prepare without installing unrelated scanners.
        AccessTools.Field(candidate.GetType("AutomaticOutfitManager.Patches.DeferredWorkScannerPatches"), "<Ready>k__BackingField").SetValue(null, true);
        harmony.CreateClassProcessor(candidate.GetType("AutomaticOutfitManager.Patches.RepairComponentSelectionScope_Patch", true)).Patch();
        if (!previous)
            harmony.CreateClassProcessor(candidate.GetType("AutomaticOutfitManager.Patches.RepairComponentSearch_Patch", true)).Patch();
        scanner = Empty(typeof(WorkGiver_FixBrokenDownBuilding));
        candidates = new[] { claimed };
        Check(!Has(worker), "claimed-only repair is rejected before native target acceptance");
        candidates = new[] { claimed, alternate };
        Check(Has(worker) && Job(worker).targetB.Thing == alternate, "both native stages choose the unclaimed alternate");
        Check(Job(worker).count == 1, "native repair still requests exactly one component");
        Check(Has(owner) && Job(owner).targetB.Thing == claimed, "owner keeps access to its exact claimed stack");
        Check(Has(worker, true) && Job(worker, true).targetB.Thing == claimed, "forced native calls bypass only the new claim filter");
        forbidden = alternate;
        Check(!Has(worker), "native forbidden alternate stays unavailable");
        forbidden = null; unreservable = alternate;
        Check(!Has(worker), "native reservation rejection stays binding");
        unreservable = null; candidates = new[] { claimed };
        Check(!Has(worker), "unreachable alternate outside native candidates is not selected");
        candidates = new Thing[0];
        Check(!Has(worker), "empty native material search declines the repair");
        candidates = new[] { claimed, alternate }; hasClaims = false;
        Check(Has(worker) && Job(worker).targetB.Thing == claimed, "claim release immediately restores closest native material");
        hasClaims = true; throwSearch = true;
        try { Has(worker); throw new Exception("expected fixture failure"); }
        catch (InvalidOperationException e) { Check(e.Message == "search fixture exception", "native exception remains visible"); }
        throwSearch = false;
        Check(finder.Invoke(scanner, new object[] { worker }) == claimed, "production finalizer clears scope after exception");
        Check(Has(worker), "subsequent automatic call still has an eligible alternate");
        Check(searchCalls > 10, "actual native finder and validator ran repeatedly");
        Console.WriteLine("PASS " + checks + " installed-native repair checks");
        return 0;
    }
}
