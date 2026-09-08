// Production registry and late Harmony admission guard against a small native
// tracker fixture. No copy of the registry's lifetime/ownership policy here.
using System;
using System.Collections.Generic;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.State;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

class PreparedMealLifetimeTests
{
    static int passed, pawnId;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); passed++; }
    static Pawn Setup(out Job meal, out Job haul, out PawnApparelState state)
    {
        PreparedIngestRetryRegistry.ResetForLoadedGame(); Find.TickManager.TicksGame = 100;
        var pawn = new Pawn { thingIDNumber = ++pawnId };
        meal = new Job { def = JobDefOf.Ingest }; haul = new Job { def = JobDefOf.HaulToCell };
        state = new PawnApparelState { Pawn = pawn, Transition = ApparelTransition.Active, PendingBufferedJobLoadId = haul.loadID };
        state.NestedRuleBuffers.Add(new NestedRuleBufferState { PendingJobLoadId = haul.loadID });
        state.NestedRuleBuffers.Add(new NestedRuleBufferState { PendingJobLoadId = 99999 });
        AutomaticOutfitManagerGameComponent.Current.States[pawn] = state;
        PreparedIngestRetryRegistry.RecordResumed(pawn, meal);
        pawn.jobs.jobQueue.Add(new QueuedJob { job = meal });
        Job proposal = haul;
        Check(PreparedIngestRetryRegistry.TryGuardAutonomousHaul(pawn, null, ref proposal,
            out bool skip, out bool clear, out _) && !skip && !clear && proposal == haul,
            "one native optional haul is permitted before the prepared meal");
        pawn.jobs.curJob = haul;
        PreparedIngestRetryRegistry.ConfirmStarted(pawn, haul);
        return pawn;
    }
    static bool Finish(Pawn pawn, PawnApparelState state, Job haul, JobCondition condition = JobCondition.Succeeded) =>
        PreparedIngestRetryRegistry.TrySuppressCompletedHaulBuffer(pawn, state, haul, condition, out _);
    static Pawn RetrySetup(out Job original, out Job retry, out PawnApparelState state)
    {
        Pawn p = Setup(out original, out Job child, out state);
        Check(Finish(p, state, child), "permitted child completes without credit");
        p.jobs.curJob = null; p.jobs.jobQueue.Clear();
        Check(PreparedIngestRetryRegistry.TryConsume(p, new Job { def = JobDefOf.Wait }, out retry, out _),
            "one bounded retry issued");
        return p;
    }
    static void Tests()
    {
        foreach (int duration in new[] { 100, 600, 601, 820, 3000 })
        {
            Pawn p = Setup(out Job meal, out Job haul, out var state);
            Find.TickManager.TicksGame += duration;
            Job wait = new Job { def = JobDefOf.Wait };
            Check(!PreparedIngestRetryRegistry.TryConsume(p, wait, out _, out _),
                duration + " ticks: running child is neither interrupted nor expired by a new boundary");
            Check(Finish(p, state, haul), duration + " ticks: exact child completion earns no outer/nested buffer credit");
            Check(state.PendingBufferedJobLoadId == -1 && state.NestedRuleBuffers[0].PendingJobLoadId == -1 &&
                state.NestedRuleBuffers[1].PendingJobLoadId == 99999, "only exact child candidates are cleared");
            Check(!Finish(p, state, haul), "duplicate child ending cannot schedule another retry");
            p.jobs.curJob = null;
            Check(PreparedIngestRetryRegistry.TryConsume(p, wait, out Job retry, out _) && retry != null &&
                retry != meal && retry.loadID != meal.loadID && retry.def == meal.def,
                "one independent fresh meal survives the long haul without duplicating the queued object");
            p.jobs.jobQueue.Clear(); p.jobs.curJob = retry;
            PreparedIngestRetryRegistry.ConfirmStarted(p, retry);
            Job duplicate = new Job { def = JobDefOf.HaulToContainer };
            Check(PreparedIngestRetryRegistry.TryGuardAutonomousHaul(p, retry, ref duplicate,
                out bool skip, out _, out _) && skip, "a second optional haul cannot displace the admitted retry");
            PreparedIngestRetryRegistry.NotifyEnded(p, retry, JobCondition.Succeeded);
            Check(!PreparedIngestRetryRegistry.TryConsume(p, wait, out _, out _), "successful meal retires retry");
        }

        foreach (string invalid in new[] { "queue", "other-parent", "invalid-target", "map", "draft", "downed", "mental",
            "forced-child", "food-progress", "other-current", "unspawned", "load", "rollback" })
        {
            Pawn p = Setup(out Job meal, out Job haul, out var state);
            Find.TickManager.TicksGame += 820;
            if (invalid == "queue") p.jobs.jobQueue.Clear();
            if (invalid == "other-parent") p.jobs.jobQueue[0].job = new Job { def = JobDefOf.Ingest };
            if (invalid == "invalid-target") meal.Viable = false;
            if (invalid == "map") p.Map = new Map();
            p.Drafted = invalid == "draft"; p.Downed = invalid == "downed"; p.InMentalState = invalid == "mental";
            if (invalid == "forced-child") haul.playerForced = true;
            if (invalid == "food-progress") p.needs.food.CurLevelPercentage += 0.05f;
            if (invalid == "other-current") p.jobs.curJob = new Job { def = JobDefOf.HaulToCell };
            if (invalid == "unspawned") p.Spawned = false;
            if (invalid == "load") PreparedIngestRetryRegistry.ResetForLoadedGame();
            if (invalid == "rollback") Find.TickManager.TicksGame = 50;
            Check(!Finish(p, state, haul), invalid + " cannot extend an expired meal guard");
        }

        Pawn pawn = Setup(out Job original, out Job child, out var s);
        Find.TickManager.TicksGame += 820;
        Check(!Finish(pawn, s, child, JobCondition.Incompletable), "failed child is not treated as successful buffered work");
        pawn = Setup(out original, out child, out s);
        Find.TickManager.TicksGame += 820; Check(Finish(pawn, s, child), "long haul completes before retry timeout begins");
        pawn.jobs.curJob = null; Find.TickManager.TicksGame += 601;
        Check(!PreparedIngestRetryRegistry.TryConsume(pawn, new Job { def = JobDefOf.Wait }, out _, out _),
            "unconsumed recovery still expires 600 ticks after haul completion");

        // The production late guard must survive a compatibility prefix which
        // rewrites the one-shot retry before native StartJob accepts it.
        var harmony = new Harmony("aom.tests.prepared-meal-lifetime");
        harmony.PatchAll(typeof(PreparedIngestRetryRegistry).Assembly);
        harmony.Patch(AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob)),
            prefix: new HarmonyMethod(typeof(PreparedMealLifetimeTests), nameof(RewriteRetry)) { priority = Priority.Normal });
        pawn = Setup(out original, out child, out s);
        PreparedIngestRetryRegistry.ResetForLoadedGame();
        PreparedIngestRetryRegistry.RecordResumed(pawn, original);
        pawn.jobs.curJob = null; pawn.jobs.jobQueue.Clear(); pawn.jobs.OptionalHauling = true;
        pawn.jobs.StartJob(original);
        Check(pawn.jobs.curJob?.def == JobDefOf.HaulToCell && pawn.jobs.OptionalHauls == 1 &&
            pawn.jobs.jobQueue.Count == 1 && pawn.jobs.jobQueue[0].job == original,
            "initial prepared meal still permits exactly one native optional haul");
        child = pawn.jobs.curJob;
        Find.TickManager.TicksGame += 820;
        Check(Finish(pawn, s, child), "native-created long first haul earns no credit");
        pawn = Setup(out original, out child, out s);
        Check(Finish(pawn, s, child), "native-body fixture permitted haul ends");
        pawn.jobs.curJob = null; pawn.jobs.jobQueue.Clear();
        Check(PreparedIngestRetryRegistry.TryConsume(pawn, new Job { def = JobDefOf.Wait }, out Job bodyRetry, out _),
            "native-body fixture obtains retry");
        pawn.jobs.OptionalHauling = true;
        pawn.jobs.StartJob(bodyRetry);
        Check(pawn.jobs.curJob == bodyRetry && pawn.jobs.jobQueue.Count == 0 && pawn.jobs.OptionalHauls == 0,
            "native optional boundary must not queue the retry and clear current before late child rejection");
        pawn = Setup(out original, out child, out s);
        Find.TickManager.TicksGame += 820; Check(Finish(pawn, s, child), "Harmony fixture long child ends");
        pawn.jobs.curJob = null;
        Check(PreparedIngestRetryRegistry.TryConsume(pawn, new Job { def = JobDefOf.Wait }, out Job fresh, out _),
            "Harmony fixture obtains one retry");
        pawn.jobs.Rewrite = true; pawn.jobs.OptionalHauling = true;
        pawn.jobs.StartJob(fresh);
        Check(pawn.jobs.curJob?.def == JobDefOf.Ingest && pawn.jobs.curJob != fresh && pawn.jobs.jobQueue.Count == 0,
            "late production guard replaces rewritten retry with a fresh sole-owned meal and clears aliases");
        Job actual = pawn.jobs.curJob;
        pawn.jobs.StartJob(new Job { def = JobDefOf.HaulToCell });
        Check(pawn.jobs.curJob == actual, "re-entrant or second optional haul leaves actual meal in tracker");
        Job forced = new Job { def = JobDefOf.HaulToCell, playerForced = true };
        pawn.jobs.StartJob(forced);
        Check(pawn.jobs.curJob == forced, "player orders retain control over the late guard");

        pawn = RetrySetup(out original, out fresh, out s);
        pawn.jobs.OptionalHauling = true;
        pawn.jobs.jobQueue.Add(new QueuedJob { job = original });
        Check(PreparationJobHandoff.CanResumeQueuedActivity(pawn, s), "stranded original alias recognized with empty tracker");
        Check(PreparationJobHandoff.ResumeEmptyTracker(pawn, s) && pawn.jobs.curJob == original &&
            pawn.jobs.jobQueue.Count == 0 && pawn.jobs.OptionalHauls == 0,
            "native queue recovery admits exact original alias once, with no cloned duplicate or optional haul");
        pawn.jobs.StartJob(fresh);
        Check(pawn.jobs.curJob == original && pawn.jobs.jobQueue.Count == 0,
            "stale outer retry alias cannot restart a meal that already owns the tracker");
        PreparedIngestRetryRegistry.NotifyEnded(pawn, original, JobCondition.Succeeded);
        Check(!PreparedIngestRetryRegistry.ProtectsRetry(pawn, original), "actual alias ending clears protection");

        foreach (bool haulFinalizer in new[] { false, true })
        {
            pawn = RetrySetup(out original, out fresh, out s);
            pawn.jobs.OptionalHauling = true;
            Job care = new Job { def = haulFinalizer ? JobDefOf.HaulToCell : new JobDef { defName = "RealCareFinalizer" } };
            pawn.jobs.NativeFinalizer = care;
            pawn.jobs.StartJob(fresh);
            Check(pawn.jobs.curJob == care && pawn.jobs.jobQueue.Count == 1 && pawn.jobs.jobQueue[0].job == fresh,
                "real native care finalizer retains the sole queued meal even when its def is HaulToCell");
            Check(PreparationJobHandoff.PreservePreparedActivity(pawn, s, care), "care finalizer is not buffered extra work");
            Find.TickManager.TicksGame += 3000;
            Check(PreparationJobHandoff.PreservePreparedActivity(pawn, s, care), "exact live care finalizer can finish beyond retry timeout");
            PreparedIngestRetryRegistry.NotifyEnded(pawn, care, JobCondition.Succeeded);
            Check(PreparationJobHandoff.PreservePreparedActivity(pawn, s, care), "finalizer ending still excludes buffer credit");
            pawn.jobs.curJob = null;
            Check(PreparationJobHandoff.ResumeEmptyTracker(pawn, s) && pawn.jobs.curJob == fresh &&
                pawn.jobs.jobQueue.Count == 0, "meal owns tracker after long native care finalizer");
        }

        foreach (string invalid in new[] { "draft", "downed", "mental", "map", "unspawned", "recall", "departure",
            "invalid-target", "forced", "different-meal", "pooled-id", "target-replaced", "progress", "expired", "load", "rollback", "other-current" })
        {
            pawn = RetrySetup(out original, out fresh, out s);
            pawn.jobs.jobQueue.Add(new QueuedJob { job = fresh });
            pawn.Drafted = invalid == "draft"; pawn.Downed = invalid == "downed"; pawn.InMentalState = invalid == "mental";
            if (invalid == "map") pawn.Map = new Map();
            pawn.Spawned = invalid != "unspawned";
            s.RecallRequested = invalid == "recall"; s.MapDepartureRequested = invalid == "departure";
            fresh.Viable = invalid != "invalid-target"; fresh.playerForced = invalid == "forced";
            if (invalid == "different-meal") pawn.jobs.jobQueue[0].job = new Job { def = JobDefOf.Ingest };
            if (invalid == "pooled-id") fresh.loadID++;
            if (invalid == "target-replaced") fresh.targetA = new LocalTargetInfo { Thing = new Thing() };
            if (invalid == "progress") pawn.needs.food.CurLevelPercentage += 0.05f;
            if (invalid == "expired") Find.TickManager.TicksGame += 601;
            if (invalid == "load") PreparedIngestRetryRegistry.ResetForLoadedGame();
            if (invalid == "rollback") Find.TickManager.TicksGame = 50;
            if (invalid == "other-current") pawn.jobs.curJob = new Job { def = new JobDef { defName = "Flee" } };
            Check(!PreparationJobHandoff.ResumeEmptyTracker(pawn, s), invalid + " cannot trigger empty-tracker meal recovery");
        }
        RejectedRetryContracts();
        harmony.UnpatchAll(harmony.Id);
    }
    static void RejectedRetryContracts()
    {
        foreach (string rejection in new[] { "claim", "reservation", "foreign-wait", "exception", "expired-scope" })
        {
            Pawn p = RetrySetup(out _, out Job retry, out var state);
            retry.targetA = new LocalTargetInfo { Thing = new Thing() };
            // Start with this food identity so the exact-match registry can
            // distinguish rejection from an unrelated/pool-reused Job.
            PreparedIngestRetryRegistry.ResetForLoadedGame();
            PreparedIngestRetryRegistry.RecordResumed(p, retry);
            PreparedIngestRetryRegistry.NotifyEnded(p, retry, JobCondition.InterruptForced);
            Check(PreparedIngestRetryRegistry.TryConsume(p, new Job { def = JobDefOf.Wait }, out retry, out _),
                "claimed-food fixture obtains one retry");
            p.jobs.ClaimedFood = rejection == "claim" ? retry.targetA.Thing : null;
            p.jobs.ReservationDenied = rejection == "reservation";
            p.jobs.RejectWithWait = rejection == "foreign-wait";
            p.jobs.ThrowOnMeal = rejection == "exception";
            p.jobs.OptionalHauling = true;
            try { if (rejection != "expired-scope") p.jobs.StartJob(retry); }
            catch (InvalidOperationException) { Check(rejection == "exception", "only injected native exception propagates"); }
            p.jobs.ThrowOnMeal = false;
            Job bill = new Job { def = new JobDef { defName = "DoBill" } };
            // Native assigns the bill, then queues it and clears current before
            // recursively starting its optional haul, in the same game tick.
            p.jobs.StartJob(bill);
            Check(p.jobs.curJob?.def == JobDefOf.HaulToCell && p.jobs.jobQueue.Count == 1 &&
                p.jobs.jobQueue[0].job == bill && p.jobs.OptionalHauls == 1,
                rejection + ": rejected meal must not strand queued cooking by blocking its hauling child");
            Check(!PreparedIngestRetryRegistry.ProtectsRetry(p, retry),
                rejection + ": unrelated haul retires orphan protection without waiting 600 ticks");
            Check(!PreparedIngestRetryRegistry.TrySuppressCompletedHaulBuffer(p, state, p.jobs.curJob,
                JobCondition.Succeeded, out _), "cooking haul is not the old meal's excluded optional child");
        }

        Pawn pawn = RetrySetup(out Job original, out Job fresh, out var s);
        pawn.jobs.jobQueue.Add(new QueuedJob { job = fresh });
        Job blocked = new Job { def = JobDefOf.HaulToCell };
        pawn.jobs.StartJob(blocked);
        Check(pawn.jobs.curJob == null && pawn.jobs.jobQueue[0].job == fresh,
            "exact valid queued meal remains protected during a null-current child admission");
        pawn.jobs.jobQueue.Clear();
        pawn.jobs.StartJob(blocked);
        Check(pawn.jobs.curJob == blocked, "same deferred child may proceed once its meal parent is abandoned");

        pawn = RetrySetup(out original, out fresh, out s);
        pawn.jobs.curJob = fresh;
        PreparedIngestRetryRegistry.ConfirmStarted(pawn, fresh);
        PreparedIngestRetryRegistry.RejectAdmission(pawn, original);
        Check(PreparedIngestRetryRegistry.ProtectsRetry(pawn, fresh),
            "rejected stale alias cannot revoke a different confirmed meal alias");
        PreparedIngestRetryRegistry.RejectAdmission(pawn, new Job { def = JobDefOf.Ingest });
        Check(PreparedIngestRetryRegistry.ProtectsRetry(pawn, fresh), "unrelated meal rejection does not clear actual meal");

        foreach (string mode in new[] { "prefix-rewrite", "prefix-rewrite-without-queue", "synthesized-retry" })
        {
            pawn = RetrySetup(out original, out fresh, out s);
            pawn.jobs.OptionalHauling = true;
            pawn.jobs.Rewrite = true;
            pawn.jobs.SkipRewriteQueue = mode == "prefix-rewrite-without-queue";
            if (mode == "synthesized-retry")
            {
                PreparedIngestRetryRegistry.ResetForLoadedGame();
                PreparedIngestRetryRegistry.RecordResumed(pawn, original);
                PreparedIngestRetryRegistry.NotifyEnded(pawn, original, JobCondition.InterruptForced);
                pawn.jobs.SynthesizeRetry = true;
                fresh = new Job { def = JobDefOf.Wait };
            }
            pawn.jobs.StartJob(fresh);
            Check(pawn.jobs.curJob?.def == JobDefOf.Ingest && pawn.jobs.OptionalHauls == 0 && pawn.jobs.jobQueue.Count == 0,
                mode + ": exact admission survives a compatibility rewrite without permitting a second haul");
        }
    }

    static void RewriteRetry(Pawn_JobTracker __instance, ref Job newJob)
    {
        ThinkNode giver = null; ThinkTreeDef tree = null; JobTag? tag = null;
        // These two bodies are extracted verbatim from the production StartJob
        // rejection boundaries by the runner, not duplicated rejection policy.
        PawnJobTracker_StartJob_Patch.CheckClaim(__instance, ref newJob, ref giver, ref tree, ref tag);
        PawnJobTracker_StartJob_Patch.CheckReservation(__instance, ref newJob, ref giver, ref tree, ref tag);
        if (__instance.SynthesizeRetry)
        {
            __instance.SynthesizeRetry = false;
            if (PreparedIngestRetryRegistry.TryConsume(__instance.PawnForTests, newJob, out Job retry, out _) && retry != null)
                newJob = retry;
        }
        if (__instance.RejectWithWait && newJob.def == JobDefOf.Ingest)
        {
            __instance.RejectWithWait = false;
            newJob = new Job { def = JobDefOf.Wait };
        }
        if (__instance.Rewrite && newJob.def == JobDefOf.Ingest)
        {
            __instance.Rewrite = false;
            if (!__instance.SkipRewriteQueue) __instance.jobQueue.Add(new QueuedJob { job = newJob });
            newJob = new Job { def = JobDefOf.HaulToCell };
        }
    }
    public static int Main()
    {
        try { Tests(); Console.WriteLine("PASS " + passed + " prepared-meal lifetime checks"); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
namespace Verse
{
    public class Map { }
    public class TickManager { public int TicksGame; }
    public static class Find { public static TickManager TickManager = new TickManager(); }
    public class Food { public float CurLevelPercentage = 0.2f; }
    public class Needs { public Food food = new Food(); }
    public class Thing { public bool Destroyed, Spawned = true; public Map Map; }
    public class ThingWithComps : Thing { }
    public struct LocalTargetInfo
    {
        public Thing Thing; public bool IsValid => Thing != null;
    }
    public class Pawn : Thing
    {
        public int thingIDNumber; public bool Drafted, Downed, InMentalState;
        public string LabelShortCap => "pawn"; public Needs needs = new Needs();
        public Pawn_JobTracker jobs; public Pawn() { Map = new Map(); jobs = new Pawn_JobTracker(this); }
    }
    public class JobDef { public string defName; }
}
namespace Verse.AI
{
    public class Job
    {
        static int next; public int loadID = ++next; public JobDef def;
        public bool playerForced, Viable = true;
        public LocalTargetInfo targetA, targetB, targetC;
        public List<LocalTargetInfo> targetQueueA, targetQueueB;
        public ThinkNode jobGiver; public ThinkTreeDef jobGiverThinkTree;
        public Job Clone() => new Job { def = def, playerForced = playerForced, Viable = Viable, targetA = targetA };
    }
    public class ThinkNode { } public class ThinkTreeDef { } public enum JobTag { Misc }
    public enum JobCondition { Succeeded, Incompletable, InterruptForced }
    public class QueuedJob { public Job job; }
    public class Pawn_JobTracker
    {
        private Pawn pawn; public Job curJob; public bool Rewrite, OptionalHauling;
        public Job NativeFinalizer; public int OptionalHauls;
        public Pawn PawnForTests => pawn;
        public Thing ClaimedFood;
        public bool ReservationDenied, RejectWithWait, ThrowOnMeal, SkipRewriteQueue, SynthesizeRetry;
        public List<QueuedJob> jobQueue = new List<QueuedJob>();
        public Pawn_JobTracker(Pawn p) { pawn = p; }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void StartJob(Job newJob, ThinkNode jobGiver = null, ThinkTreeDef thinkTree = null, JobTag? tag = null)
        {
            if (ThrowOnMeal && newJob.def == JobDefOf.Ingest) throw new InvalidOperationException("injected admission failure");
            curJob = newJob;
            Job finalizer = NativeFinalizer; NativeFinalizer = null;
            Job child = TryOpportunisticJob(finalizer, newJob);
            if (child == null) return;
            if (child != finalizer) OptionalHauls++;
            jobQueue.Insert(0, new QueuedJob { job = newJob });
            curJob = null;
            StartJob(child);
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public Job TryOpportunisticJob(Job finalizer, Job newJob) => finalizer ??
            (OptionalHauling && (newJob.def == JobDefOf.Ingest || newJob.def.defName == "DoBill")
                ? new Job { def = JobDefOf.HaulToCell } : null);
        private void TryFindAndStartJob()
        {
            if (curJob != null || jobQueue.Count == 0) return;
            var job = jobQueue[0].job; jobQueue.RemoveAt(0); StartJob(job);
        }
        public void ClearQueuedJobs(bool ignored) { jobQueue.Clear(); }
    }
}
namespace RimWorld
{
    public class Apparel : ThingWithComps { }
    public static class JobDefOf
    {
        public static JobDef Ingest = new JobDef { defName = "Ingest" }, HaulToCell = new JobDef { defName = "HaulToCell" },
            HaulToContainer = new JobDef { defName = "HaulToContainer" }, Wait = new JobDef { defName = "Wait" },
            Wear = new JobDef { defName = "Wear" }, Equip = new JobDef { defName = "Equip" };
    }
}
namespace AutomaticOutfitManager.State
{
    public enum ApparelTransition { Preparing, Active, ReturningToChangingArea, Restoring }
    public class NestedRuleBufferState { public int PendingJobLoadId; }
    public class PawnApparelState
    {
        public int PendingBufferedJobLoadId = -1;
        public Pawn Pawn; public ApparelTransition Transition;
        public bool RecallRequested, MapDepartureRequested, WeaponRuleOverrideExplicit, WeaponRestorationRequested;
        public Job PendingWorkJob; public Thing OriginalWeapon;
        public List<Apparel> OriginalApparel = new List<Apparel>();
        public bool IsPreparationApparel(Apparel a) => false;
        public bool IsManagedWeapon(Thing t) => false;
        public List<NestedRuleBufferState> NestedRuleBuffers = new List<NestedRuleBufferState>();
        public void ClearPendingBufferedTask() { PendingBufferedJobLoadId = -1; }
    }
}
namespace AutomaticOutfitManager.Core
{
    public class AutomaticOutfitManagerGameComponent
    {
        public static AutomaticOutfitManagerGameComponent Current = new AutomaticOutfitManagerGameComponent();
        public Dictionary<Pawn, PawnApparelState> States = new Dictionary<Pawn, PawnApparelState>();
        public PawnApparelState StateFor(Pawn p) => States.TryGetValue(p, out var s) ? s : null;
        public static void ReleaseNativeReservations(Pawn p, Job j) { }
    }
    public static class AomLog
    {
        public static bool DetailedEnabled = false;
        public static bool ShouldLogDetailed(Pawn p, string key, int ticks = 600) => false;
        public static void Detailed(string message) { }
    }
}
namespace AutomaticOutfitManager.Detection
{
    internal static class ManagedWorkClaimRegistry
    {
        internal static bool IsClaimedByOther(Pawn pawn, Job job) =>
            job?.def == JobDefOf.Ingest && pawn.jobs.ClaimedFood != null && pawn.jobs.ClaimedFood == job.targetA.Thing;
    }
    internal static class ManagedWorkCandidateFilter { internal static void LogConflict(Pawn p, Job j, string context) { } }
}
namespace AutomaticOutfitManager.Patches
{
    // This fixture has no paused rules; the paused-haul suite covers this policy.
    internal static class PausedAreaWorkFilter { internal static Job FinalizerForPreparedHaul(Pawn p,Job parent,Job child)=>child; }

    internal static class IngestReservationAdmission
    {
        internal static bool Rejects(Pawn pawn, Job job) => job?.def == JobDefOf.Ingest && pawn.jobs.ReservationDenied;
    }
    public static partial class PawnJobTracker_StartJob_Patch
    {
        private static void ReplaceWithWait(Pawn p, int ticks, ref Job job, ref ThinkNode giver, ref JobTag? tag)
        { job = new Job { def = JobDefOf.Wait }; giver = null; tag = null; }
        private static void ReplaceWithBriefWait(Pawn p, ref Job job, ref ThinkNode giver, ref JobTag? tag) =>
            ReplaceWithWait(p, 30, ref job, ref giver, ref tag);
        public static bool IsNativeEmergencySafetyJob(Job j) => j.def.defName == "Flee";
        public static bool PendingWorkJobIsViable(Pawn p, Job j, out string reason) { reason = null; return j?.Viable == true; }
    }
}
