// Simulates the native StartJob -> opportunistic haul -> denied haul -> exit
// recursion. Harmony patches the simulated tracker using the production patch.
using System;
using System.Runtime.CompilerServices;
using AutomaticOutfitManager.Patches;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace Verse
{
    public class Map { }
    public class Pawn { public Map Map = new Map(); }
    public static class Find { public static TickManager TickManager = new TickManager(); }
    public class TickManager { public int TicksGame; }
    public struct LocalTargetInfo
    {
        public int Cell;
        public static bool operator ==(LocalTargetInfo a, LocalTargetInfo b) => a.Cell == b.Cell;
        public static bool operator !=(LocalTargetInfo a, LocalTargetInfo b) => !(a == b);
        public override bool Equals(object other) => other is LocalTargetInfo b && this == b;
        public override int GetHashCode() => Cell;
    }
}
namespace Verse.AI
{
    public class JobDef { public bool allowOpportunisticPrefix; }
    public class Job { public int loadID; public JobDef def; public LocalTargetInfo targetA; }
    public class Pawn_JobTracker
    {
        private Pawn pawn;
        public int Starts, Hauls;
        public bool RejectHaul, MarkExit, InjectFromCompatibility;
        public Job Current;
        public Pawn_JobTracker(Pawn p) { pawn = p; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Job TryOpportunisticJob(Job lastJob, Job newJob) =>
            newJob.def.allowOpportunisticPrefix ? AccessExitJobTests.Job(false) : null;
        public void StartJob(Job job)
        {
            if (++Starts > 10) throw new InvalidOperationException("10 jobs in one tick");
            if (!job.def.allowOpportunisticPrefix)
            {
                Hauls++;
                if (RejectHaul)
                {
                    job = AccessExitJobTests.Job(true);
                    if (MarkExit) AccessExitJobs.Mark(pawn, job);
                }
            }
            var prefix = TryOpportunisticJob(null, job);
            if (prefix != null) StartJob(prefix);
            else Current = job;
        }
    }
}
class AccessExitJobTests
{
    static int passed, id;
    public static Job Job(bool movement) => new Job
    {
        loadID = ++id, def = new JobDef { allowOpportunisticPrefix = movement },
        targetA = new LocalTargetInfo { Cell = 139 }
    };
    static void Check(bool ok, string name)
    { if (!ok) throw new Exception(name); passed++; }
    static void CompatibilityPostfix(Pawn_JobTracker __instance, ref Job __result)
    { if (__instance.InjectFromCompatibility) __result = Job(false); }
    static void Tests()
    {
        var pawn = new Pawn();
        var tracker = new Pawn_JobTracker(pawn) { RejectHaul = true };
        bool reproduced = false;
        try { tracker.StartJob(Job(true)); }
        catch (InvalidOperationException) { reproduced = true; }
        Check(reproduced && tracker.Starts == 11, "unpatched simulation reproduces native recursion");

        var harmony = new Harmony("aom.tests.access-exits");
        harmony.PatchAll(typeof(AccessExitJobs).Assembly);
        harmony.Patch(AccessTools.Method(typeof(Pawn_JobTracker), "TryOpportunisticJob"),
            postfix: new HarmonyMethod(typeof(AccessExitJobTests), nameof(CompatibilityPostfix)) { priority = Priority.Normal });
        foreach (bool compatibility in new[] { false, true })
        foreach (string reason in new[] { "child", "activities", "wandering" })
        {
            tracker = new Pawn_JobTracker(pawn) { RejectHaul = true, MarkExit = true,
                InjectFromCompatibility = compatibility };
            var exit = Job(true); AccessExitJobs.Mark(pawn, exit);
            tracker.StartJob(exit);
            Check(tracker.Starts == 1 && tracker.Hauls == 0 && tracker.Current == exit,
                reason + " direct exit terminates without nested haul, compatibility=" + compatibility);
        }
        tracker = new Pawn_JobTracker(pawn) { RejectHaul = true, MarkExit = true };
        tracker.StartJob(Job(false));
        Check(tracker.Starts == 1 && tracker.Current.def.allowOpportunisticPrefix,
            "denied haul replaced during admission terminates with one owned exit");
        tracker = new Pawn_JobTracker(pawn);
        tracker.StartJob(Job(true));
        Check(tracker.Starts == 2 && tracker.Hauls == 1, "ordinary movement retains native opportunistic haul");
        var owned = Job(true); AccessExitJobs.Mark(pawn, owned);
        Check(AccessExitJobs.IsOwned(pawn, owned), "exact pawn and exit admitted");
        Check(!AccessExitJobs.IsOwned(new Pawn { Map = pawn.Map }, owned), "different pawn rejected");
        var clone = Job(true); clone.def = owned.def; clone.loadID = owned.loadID;
        Check(!AccessExitJobs.IsOwned(pawn, clone), "same ID and target do not authorize copied job");
        owned.loadID++;
        Check(!AccessExitJobs.IsOwned(pawn, owned), "pooled job reuse rejected");
        AccessExitJobs.Mark(pawn, owned); owned.def = new JobDef();
        Check(!AccessExitJobs.IsOwned(pawn, owned), "rewritten definition rejected");
        AccessExitJobs.Mark(pawn, owned); owned.targetA = new LocalTargetInfo { Cell = 140 };
        Check(!AccessExitJobs.IsOwned(pawn, owned), "rewritten destination rejected");
        AccessExitJobs.Mark(pawn, owned); pawn.Map = new Map();
        Check(!AccessExitJobs.IsOwned(pawn, owned), "map change invalidates exit");
        AccessExitJobs.Mark(pawn, owned); Find.TickManager.TicksGame = 601;
        Check(!AccessExitJobs.IsOwned(pawn, owned), "stale queued exit expires");
        AccessExitJobs.Mark(pawn, owned); Find.TickManager.TicksGame = 600;
        Check(!AccessExitJobs.IsOwned(pawn, owned), "tick rollback invalidates exit");
        AccessExitJobs.Mark(pawn, owned); AccessExitJobs.ResetForLoadedGame();
        Check(!AccessExitJobs.IsOwned(pawn, owned), "loaded game discards transient ownership");
        Check(!AccessExitJobs.IsOwned(null, owned) && !AccessExitJobs.IsOwned(pawn, null), "null input safe");
        Console.WriteLine("PASS " + passed + " access-exit checks with actual Harmony interception; native gameplay requires RimWorld.");
    }
    static int Main() { try { Tests(); return 0; } catch (Exception e) { Console.Error.WriteLine(e); return 1; } }
}
