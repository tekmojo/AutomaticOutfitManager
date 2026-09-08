using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

static class BoundaryAdmissionTests
{
    static int passed;
    static void Check(bool ok, string name) { if (!ok) throw new Exception("ASSERT: " + name); passed++; }
    public static Job NewJob(JobDef def = null) => new Job { def = def ?? JobDefOf.HaulToCell, targetA = new Thing() };
    static Pawn Setup(out Job root, out ApparelRule rule)
    {
        ProtectedBoundaryRetryRegistry.ResetForLoadedGame();
        AutomaticOutfitManagerGameComponent.Current = new AutomaticOutfitManagerGameComponent();
        Find.TickManager.TicksGame = 100;
        var p = new Pawn();
        rule = new ApparelRule { Id = "ship", Area = new Area { Map = p.Map }, ChangingArea = new Area { Map = p.Map } };
        rule.Area.Cells.Add(8); rule.ChangingArea.Cells.Add(3);
        AutomaticOutfitManagerGameComponent.Current.Rules.Add(rule);
        root = NewJob(); root.targetQueueA = new List<LocalTargetInfo> { new Thing() }; root.countQueue = new List<int> { 1 };
        ProtectedBoundaryRetryRegistry.Record(p, root, rule);
        return p;
    }
    static Job Pending(Pawn p) => ProtectedBoundaryRetryRegistry.TryGetPendingInterruption(p, out var job, out _) ? job : null;
    static int Main()
    {
        try
        {
            new Harmony("aom.boundary.admission.contracts").PatchAll();
            var p = Setup(out var original, out var rule);
            var retained = Pending(p);
            p.jobs.Mode = "recover";
            try { p.jobs.StartJob(NewJob(JobDefOf.LayDown)); } catch { }
            Check(p.jobs.curJob?.def == JobDefOf.Wait && p.jobs.Attempts == 2,
                "native recovery wait survives instead of recursively restarting the retained haul");
            Check(retained.def == JobDefOf.HaulToCell && retained.targetQueueA.Count == 1,
                "native job cleanup cannot pool or mutate the registry snapshot");
            Check(!BoundaryJobAdmission.IsOpen(p), "guard clears after recovery");
            Check(Pending(p) == null, "failed admission defers retry");
            Find.TickManager.TicksGame += 60;
            Check(Pending(p) != null, "retained root becomes retryable after bounded yield");
            p.jobs.Mode = "normal"; p.jobs.StartJob(NewJob(JobDefOf.LayDown));
            Check(p.jobs.curJob.def == JobDefOf.HaulToCell && !ReferenceEquals(p.jobs.curJob, retained), "later retry admits a separate native job");
            Check(Pending(p) == null, "accepted root retires from registry");

            p = Setup(out original, out rule); retained = Pending(p); p.jobs.Mode = "recover";
            p.jobs.StartJob(retained.Clone());
            Check(p.jobs.curJob.def == JobDefOf.Wait && p.jobs.Attempts == 2,
                "native retry of the same root identity also protects nested recovery");
            p = Setup(out original, out rule); var nativeRetry = Pending(p).Clone();
            p.jobs.StartJob(nativeRetry);
            Check(ReferenceEquals(p.jobs.curJob, nativeRetry) && Pending(p) == null,
                "already detached native root is admitted without replacing its object");

            p = Setup(out original, out rule); retained = Pending(p);
            p.jobs.Mode = "child"; p.jobs.StartJob(NewJob(JobDefOf.LayDown));
            Check(p.jobs.Attempts == 2 && p.jobs.curJob.def == JobDefOf.Child, "optional child is not replaced by retained root");
            Check(p.jobs.jobQueue.Count == 1 && p.jobs.jobQueue[0].job.def == JobDefOf.HaulToCell, "queued native parent remains single-owned");
            Check(!ReferenceEquals(p.jobs.jobQueue[0].job, retained) && Pending(p) == null, "queued admission retires only its snapshot");

            p = Setup(out original, out rule); p.jobs.Mode = "throw";
            try { p.jobs.StartJob(NewJob(JobDefOf.LayDown)); } catch { }
            Check(!BoundaryJobAdmission.IsOpen(p), "Harmony finalizer releases guard on exception");
            Find.TickManager.TicksGame += 60; Check(Pending(p) != null, "exception preserves retry snapshot");

            p = Setup(out original, out rule); retained = Pending(p); p.jobs.Mode = "pool-reuse";
            p.jobs.StartJob(NewJob(JobDefOf.LayDown));
            Check(p.jobs.curJob.def == JobDefOf.Wait && retained.def == JobDefOf.HaulToCell, "pool reuse preserves immutable root");
            Find.TickManager.TicksGame += 60; Check(Pending(p) != null, "reused job object is not mistaken for successful admission");

            p = Setup(out original, out rule); p.jobs.Mode = "record-new-boundary";
            p.jobs.StartJob(NewJob(JobDefOf.LayDown));
            Check(Pending(p) != null, "successful admission preserves a newer boundary observation");

            p = Setup(out original, out rule);
            var state = new PawnApparelState { Transition = ApparelTransition.Active, RecallRequested = true, BufferedTasksCompleted = 1 };
            AutomaticOutfitManagerGameComponent.Current.State = state;
            var sleep = NewJob(JobDefOf.LayDown); p.jobs.StartJob(sleep);
            Check(ReferenceEquals(p.jobs.curJob, sleep), "requested return is not displaced by Prefix promotion");
            var result = PawnJobTracker_StartJob_Patch.TryResumeBoundaryInterruptedJob(p, Pending(p), new[] { rule });
            Check(result == PawnJobTracker_StartJob_Patch.BoundaryResumeResult.RetryLater && p.jobs.Attempts == 1,
                "component helper cannot prepare during requested recall");
            Check(state.BufferedTasksCompleted == 1 && state.RecallRequested, "return priority preserves completed buffer and recall");
            foreach (var phase in new[] { ApparelTransition.Preparing, ApparelTransition.ReturningToChangingArea, ApparelTransition.Restoring })
            { state.Transition = phase; state.RecallRequested = false; Check(!BoundaryJobAdmission.CanResume(state), "existing transition owns its continuation: " + phase); }
            state.Transition = ApparelTransition.Active; Check(BoundaryJobAdmission.CanResume(state), "ordinary active session can resume");

            p = Setup(out original, out rule); retained = Pending(p);
            AutomaticOutfitManagerGameComponent.Current.PlanPreparation = true;
            result = PawnJobTracker_StartJob_Patch.TryResumeBoundaryInterruptedJob(p, retained, new[] { rule });
            state = AutomaticOutfitManagerGameComponent.Current.State;
            Check(result == PawnJobTracker_StartJob_Patch.BoundaryResumeResult.Resumed && p.jobs.curJob.def == JobDefOf.Wear,
                "component helper starts prepared outfit step");
            Check(state.PendingWorkJob?.def == JobDefOf.HaulToCell && !ReferenceEquals(state.PendingWorkJob, retained) && Pending(p) == null,
                "preparation owns one detached saved continuation");
            Check(!BoundaryJobAdmission.IsOpen(p), "helper releases scope after preparation");

            p = Setup(out original, out rule); retained = Pending(p); p.jobs.Mode = "recover";
            result = PawnJobTracker_StartJob_Patch.TryResumeBoundaryInterruptedJob(p, retained, new[] { rule });
            Check(result == PawnJobTracker_StartJob_Patch.BoundaryResumeResult.RetryLater && p.jobs.curJob.def == JobDefOf.Wait,
                "helper verifies failed native admission instead of reporting success");
            Find.TickManager.TicksGame += 60; Check(Pending(p) != null, "helper failure leaves valid detached root for later retry");

            p = Setup(out original, out rule); var forced = NewJob(JobDefOf.LayDown); forced.playerForced = true;
            p.jobs.StartJob(forced); Check(ReferenceEquals(p.jobs.curJob, forced) && Pending(p) == null, "explicit player job retires stale boundary root");
            p = Setup(out original, out rule); p.Drafted = true;
            Check(!BoundaryJobAdmission.TryBegin(p, Pending(p), out _), "drafted control cannot begin boundary admission");
            p.Drafted = false; p.Downed = true;
            Check(!BoundaryJobAdmission.TryBegin(p, Pending(p), out _), "downed control cannot begin boundary admission");

            p = Setup(out original, out rule); retained = Pending(p);
            original.def = null; original.targetQueueA.Clear();
            Check(retained.def == JobDefOf.HaulToCell && retained.targetQueueA.Count == 1, "path interruption captured detached data before native cleanup");
            var component = AutomaticOutfitManagerGameComponent.Current;
            var home = new ApparelRule { Id = "kitchen", Area = new Area { Map = new Map() }, ChangingArea = new Area { Map = new Map() } };
            component.Rules.Add(home);
            state = new PawnApparelState { ActiveRuleId = home.Id, CurrentRuleIds = new List<string> { rule.Id }, BufferedTasksCompleted = 1 };
            component.State = state;
            Check(PawnJobTracker_StartJob_Patch.TryFindRestorationCell(p, component, state, out var cell) && cell == (IntVec3)3,
                "foreign primary can return to its tracked current-map locker");
            p.Position = 3; Check(PawnJobTracker_StartJob_Patch.InLocker(p, component, state), "tracked local locker completes the recorded return");
            state.CurrentRuleIds.Clear(); Check(!PawnJobTracker_StartJob_Patch.TryFindRestorationCell(p, component, state, out cell), "unrelated local locker cannot be borrowed");
            Check(!PawnJobTracker_StartJob_Patch.InLocker(p, component, state), "same position in unrelated locker is not restoration arrival");
            state.CurrentRuleIds.Add(rule.Id); home.ChangingArea.Map = p.Map; home.ChangingArea.Cells.Add(6);
            Check(PawnJobTracker_StartJob_Patch.TryFindRestorationCell(p, component, state, out cell) && cell == (IntVec3)6, "same-map primary locker retains preference");
            rule.ChangingArea.Usable = false; home.ChangingArea.Map = new Map();
            Check(!PawnJobTracker_StartJob_Patch.TryFindRestorationCell(p, component, state, out cell), "unreachable tracked locker remains rejected");
            rule.ChangingArea.Usable = true; HazardousEnvironmentSafety.Unsafe = true;
            Check(!PawnJobTracker_StartJob_Patch.TryFindRestorationCell(p, component, state, out cell), "unsafe local restoration cell remains rejected");
            Check(state.BufferedTasksCompleted == 1, "locker selection never resets buffer progress");
            HazardousEnvironmentSafety.Unsafe = false; home.ChangingArea = null;
            Check(!PawnJobTracker_StartJob_Patch.TryFindRestorationCell(p, component, state, out cell),
                "a primary rule without a locker does not acquire an unrelated fallback policy");

            p = Setup(out original, out rule); retained = Pending(p); rule.WorkAreaPaused = true;
            PausedAreaWorkFilter.PermittedHaul = true;
            AutomaticOutfitManagerGameComponent.Current.PlanPreparation = true;
            result = PawnJobTracker_StartJob_Patch.TryResumeBoundaryInterruptedJob(p, retained, new[] { rule });
            Check(result == PawnJobTracker_StartJob_Patch.BoundaryResumeResult.Resumed && p.jobs.curJob.def == JobDefOf.Wear,
                "permitted paused haul resumes boundary preparation");
            Check(AutomaticOutfitManagerGameComponent.Current.State.PendingWorkJob?.def == JobDefOf.HaulToCell && Pending(p) == null,
                "paused boundary handoff keeps one exact haul continuation");
            p = Setup(out original, out rule); retained = Pending(p); rule.WorkAreaPaused = true;
            PausedAreaWorkFilter.PermittedHaul = false;
            result = PawnJobTracker_StartJob_Patch.TryResumeBoundaryInterruptedJob(p, retained, new[] { rule });
            Check(result == PawnJobTracker_StartJob_Patch.BoundaryResumeResult.Invalid && p.jobs.Attempts == 0,
                "disallowed paused haul cannot start boundary preparation");

            Console.WriteLine(passed + " boundary admission contracts passed (native-shaped fixture; manual game replay still required).");
            return 0;
        }
        catch (Exception e) { Console.WriteLine(e.Message); return 1; }
    }
}

namespace Verse
{
    public class Map { }
    public class TickManager { public int TicksGame; }
    public static class Find { public static TickManager TickManager = new TickManager(); }
    public class JobDef { public string defName; }
    public class Thing { public bool Destroyed; }
    public class ThingCountClass { }
    public struct IntVec3
    {
        public int Value; public static IntVec3 Invalid => -1; public bool IsValid => Value >= 0;
        public bool InBounds(Map map) => map != null && IsValid; public bool Standable(Map map) => true;
        public int DistanceToSquared(IntVec3 other) => (Value-other.Value)*(Value-other.Value);
        public static implicit operator IntVec3(int n) => new IntVec3 { Value = n };
        public static bool operator ==(IntVec3 a, IntVec3 b) => a.Value == b.Value;
        public static bool operator !=(IntVec3 a, IntVec3 b) => !(a == b);
    }
    public struct LocalTargetInfo
    {
        public Thing Thing; public IntVec3 Cell; public bool HasThing => Thing != null;
        public bool IsValid => HasThing || Cell.IsValid;
        public static implicit operator LocalTargetInfo(Thing t) => new LocalTargetInfo { Thing = t, Cell = -1 };
    }
    public class Area { public Map Map; public bool Usable = true; public List<IntVec3> Cells = new List<IntVec3>(); public bool this[IntVec3 c] => Cells.Contains(c); }
    public class Pawn
    {
        public Map Map = new Map(); public bool Spawned = true, Drafted, Downed;
        public IntVec3 Position = 1; public string LabelShortCap = "Ocag"; public Pawn_JobTracker jobs;
        public Pawn() { jobs = new Pawn_JobTracker(this); }
    }
}
namespace Verse.AI
{
    public class ThinkNode { } public class ThinkTreeDef { } public enum JobTag { Misc } public enum JobCondition { InterruptForced }
    public class Job
    {
        static int nextId; public int loadID = ++nextId; public JobDef def; public bool playerForced;
        public ThinkNode jobGiver; public ThinkTreeDef jobGiverThinkTree;
        public LocalTargetInfo targetA = new LocalTargetInfo { Cell = -1 }, targetB = new LocalTargetInfo { Cell = -1 }, targetC = new LocalTargetInfo { Cell = -1 };
        public List<LocalTargetInfo> targetQueueA, targetQueueB; public List<int> countQueue; public List<ThingCountClass> placedThings;
        public Job Clone() => (Job)MemberwiseClone();
        public void Pool() { def = null; loadID = -1; targetQueueA?.Clear(); }
    }
    public class QueuedJob { public Job job; }
    public class Pawn_JobTracker
    {
        public Pawn Pawn; public Job curJob; public List<QueuedJob> jobQueue = new List<QueuedJob>();
        public string Mode = "normal"; public int Attempts;
        public Pawn_JobTracker(Pawn p) { Pawn = p; }
        public void ClearQueuedJobs(bool start) { foreach (var q in jobQueue) q.job.Pool(); jobQueue.Clear(); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void StartJob(Job newJob, JobCondition condition = JobCondition.InterruptForced,
            ThinkNode jobGiver = null, bool fromQueue = false, bool resume = true, ThinkTreeDef thinkTree = null, JobTag? tag = null)
        {
            Attempts++;
            if (Attempts > 12) throw new Exception("native rapid-start safety cap");
            if (Mode == "throw") throw new InvalidOperationException("native admission failure");
            if (Mode == "recover" && newJob.def == JobDefOf.HaulToCell)
            {
                // The native recovery call is nested before outer cleanup.
                JobUtility.TryStartErrorRecoverJob(Pawn); newJob.Pool(); return;
            }
            if (Mode == "pool-reuse") { newJob.Pool(); newJob.def = JobDefOf.Wait; newJob.loadID = 9999; curJob = newJob; return; }
            if (Mode == "child")
            {
                Mode = "normal"; jobQueue.Add(new QueuedJob { job = newJob }); curJob = null;
                StartJob(BoundaryAdmissionTests.NewJob(JobDefOf.Child)); return;
            }
            if (Mode == "record-new-boundary")
                ProtectedBoundaryRetryRegistry.Record(Pawn, newJob, AutomaticOutfitManagerGameComponent.Current.Rules[0]);
            curJob = newJob;
        }
    }
    public static class JobUtility { public static void TryStartErrorRecoverJob(Pawn pawn) => pawn.jobs.StartJob(BoundaryAdmissionTests.NewJob(JobDefOf.Wait)); }
}
namespace RimWorld
{
    public static class JobDefOf
    {
        public static JobDef HaulToCell = new JobDef { defName = "HaulToCell" }, Wait = new JobDef { defName = "Wait" },
            Wait_MaintainPosture = new JobDef { defName = "Wait_MaintainPosture" }, LayDown = new JobDef { defName = "LayDown" },
            Wear = new JobDef { defName = "Wear" }, Child = new JobDef { defName = "OptionalChild" };
    }
}
namespace AutomaticOutfitManager.State
{
    public enum ApparelTransition { Active, Preparing, ReturningToChangingArea, Restoring }
    public class PawnApparelState { public ApparelTransition Transition; public bool RecallRequested; public Job PendingWorkJob; public int BufferedTasksCompleted; public string ActiveRuleId; public List<string> CurrentRuleIds = new List<string>(); }
}
namespace AutomaticOutfitManager.Rules
{
    public class ApparelRule { public string Id; public bool Enabled = true, WorkAreaPaused, Allowed = true; public Area Area, ChangingArea; }
}
namespace AutomaticOutfitManager.Core
{
    public class AutomaticOutfitManagerGameComponent
    {
        public static AutomaticOutfitManagerGameComponent Current = new AutomaticOutfitManagerGameComponent();
        public List<ApparelRule> Rules = new List<ApparelRule>(); public PawnApparelState State; public bool PlanPreparation;
        public PawnApparelState StateFor(Pawn p) => State;
        public ApparelRule RuleById(string id) => Rules.FirstOrDefault(r => r.Id == id);
        public static void ReleaseNativeReservations(Pawn p, Job j) { }
    }
    public static class AomLog { public static bool DetailedEnabled; public static void Detailed(string s) { } public static bool ShouldLogDetailed(Pawn p, string k, int ticks = 0) => false; }
}
namespace AutomaticOutfitManager.Detection
{
    public static class RuleEvaluator { public static bool RuleCanApplyToPawn(Pawn p, ApparelRule r) => true; }
    public static class ManagedWorkClaimRegistry { public static void Release(Pawn p, Job j) { } }
}
namespace AutomaticOutfitManager.Patches
{
    // Full rest/PPE policy is covered by RestAndSupplyTests; this fixture exercises native boundary re-entry.
    internal static class RestActivityPolicy { internal static bool Allowed(Pawn p,Job j,ApparelRule r)=>false; }
    public static class PausedAreaWorkFilter {
        // Access classification is covered by the production paused-haul suite;
        // these fixture inputs exercise native boundary admission/queue ownership.
        public static bool PermittedHaul;
        public static bool IsPermittedHaulForRule(Pawn p,Job j,ApparelRule r)=>PermittedHaul && r.WorkAreaPaused && r.Allowed && j.def==JobDefOf.HaulToCell;
        public static bool ActivityAllowedAtRuleBoundary(Pawn p, Job j, ApparelRule r) => r.Allowed && (!r.WorkAreaPaused || IsPermittedHaulForRule(p,j,r));
    }
    public enum NonWorkMealStage { Returning }
    public class MealTrip { public bool EatInPlace; public NonWorkMealStage Stage; public IntVec3 LockerCell; }
    public static class NonWorkMealHandoff { public static MealTrip For(Pawn p) => null; }
    public static class HazardousEnvironmentSafety { public static bool Unsafe; public static bool MustRetainManagedProtectionAt(Pawn p, PawnApparelState s, IntVec3 c, out string reason) { reason = null; return Unsafe; } }
    public static partial class PawnJobTracker_StartJob_Patch
    {
        public enum BoundaryResumeResult { Resumed, RetryLater, Invalid }
        public static bool InLocker(Pawn p, AutomaticOutfitManagerGameComponent c, PawnApparelState s) => PawnInsideRestorationLocker(p, c, s);
        static bool SameJob(Job a, Job b) => a == b || a?.loadID == b?.loadID;
        static bool PendingWorkJobIsViable(Pawn p, Job j, out string reason, out bool retryable) { reason = null; retryable = false; return j?.def != null; }
        static void ReplaceWithWait(Pawn p, int ticks, ref Job j, ref ThinkNode giver, ref JobTag? tag) { j = BoundaryAdmissionTests.NewJob(JobDefOf.Wait); }
        static bool TryPrepareForMatchingRules(Pawn_JobTracker tracker, Pawn pawn, AutomaticOutfitManagerGameComponent component,
            List<ApparelRule> rules, ref Job job, ref ThinkNode giver, ref JobTag? tag, bool forced)
        {
            if (!component.PlanPreparation) return false;
            component.State = new PawnApparelState { Transition = ApparelTransition.Preparing, PendingWorkJob = job };
            job = BoundaryAdmissionTests.NewJob(JobDefOf.Wear); return true;
        }
        static bool PawnInsideArea(Pawn p, Area a) => p?.Map != null && a?.Map == p.Map && a[p.Position];
        static bool TryFindSafeTransitionCell(Pawn p, Area a, IEnumerable<ApparelRule> rules, out IntVec3 cell, PawnApparelState state = null)
        {
            cell = IntVec3.Invalid;
            if (p?.Map == null || a?.Map != p.Map || !a.Usable || HazardousEnvironmentSafety.Unsafe) return false;
            cell = a.Cells.Where(c => !rules.Any(r => r.Area[c])).DefaultIfEmpty(IntVec3.Invalid).First(); return cell.IsValid;
        }
    }
}
