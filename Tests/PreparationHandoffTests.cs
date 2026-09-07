// Production queue/ownership policy and Harmony opportunistic patches against
// a native-shaped StartJob -> queue parent -> start finalizer implementation.
// This exercises re-entry with real Harmony; it is not an in-game driver test.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.State;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    // Meal policy is exercised with the production registry in the separate
    // prepared-meal fixture; these contracts cover non-meal/gear continuations.
    internal static class PreparedIngestRetryRegistry
    {
        internal static bool ProtectsRetry(Pawn pawn, Job job) => false;
        internal static void RecordRetryFinalizer(Pawn pawn, Job parent, Job child) { }
    }
}

namespace Verse
{
    public class Map { }
    public class TickManager { public int TicksGame = 100; }
    public static class Find { public static TickManager TickManager = new TickManager(); }
    public class Thing { public bool Destroyed, Spawned = true; public Map Map; }
    public class ThingWithComps : Thing { }
    public class Pawn : Thing
    {
        public bool Drafted, Downed, InMentalState;
        public Pawn_JobTracker jobs;
        public Pawn() { Map = new Map(); jobs = new Pawn_JobTracker(this); }
    }
    public struct LocalTargetInfo
    {
        public Thing Thing;
        public bool IsValid => Thing != null;
        public LocalTargetInfo(Thing t) { Thing = t; }
    }
    public class JobDef { public string defName; public bool allowOpportunisticPrefix; }
}
namespace RimWorld
{
    public class Apparel : ThingWithComps { }
    public static class JobDefOf
    {
        public static JobDef Wear = new JobDef { defName = "Wear", allowOpportunisticPrefix = true };
        public static JobDef Equip = new JobDef { defName = "Equip", allowOpportunisticPrefix = true };
        public static JobDef Wait = new JobDef { defName = "Wait" };
    }
}
namespace Verse.AI
{
    public class Job
    {
        public JobDef def;
        public int loadID;
        public bool playerForced, Viable = true;
        public LocalTargetInfo targetA, targetB, targetC;
        public List<LocalTargetInfo> targetQueueA, targetQueueB;
    }
    public class QueuedJob { public Job job; }
    public class Pawn_JobTracker
    {
        private Pawn pawn;
        public Job curJob, NativeFinalizer;
        public List<QueuedJob> jobQueue = new List<QueuedJob>();
        public int Attempts, Rejections, OptionalHauls, Credits;
        public bool Guards, InjectHaul;
        public Pawn_JobTracker(Pawn p) { pawn = p; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Job TryOpportunisticJob(Job last, Job next) =>
            !next.def.allowOpportunisticPrefix ? null : last ?? PreparationHandoffTests.Job("HaulToCell");
        public void StartJob(Job next)
        {
            var state = AutomaticOutfitManagerGameComponent.Current.StateFor(pawn);
            bool handoff = Guards && (PreparationJobHandoff.PreserveBeforeRetry(pawn, state, next) ||
                PreparationJobHandoff.PreserveRestorationBeforeRetry(pawn, state, next));
            // The former AOM path rejected a missing weapon as soon as a
            // nested child or native wait appeared, clearing the queued root.
            if (!handoff && (state.Transition == ApparelTransition.Preparing || state.Transition == ApparelTransition.Restoring) &&
                next.def != JobDefOf.Equip && next.def != JobDefOf.Wear && Attempts > 0)
            {
                jobQueue.Clear(); Rejections++;
                next = PreparationHandoffTests.Wait();
            }
            if (!handoff && next.def == JobDefOf.Wait && state.PendingWorkJob != null &&
                !(Guards && PreparationJobHandoff.PreservePendingWait(pawn, state, next)))
                state.PendingWorkJob = next;
            if (next.def == JobDefOf.Equip) Attempts++;
            curJob = next;
            Job child = TryOpportunisticJob(NativeFinalizer, next);
            NativeFinalizer = null;
            if (child != null)
            {
                jobQueue.Insert(0, new QueuedJob { job = next });
                curJob = null;
                if (child.def.defName == "HaulToCell") OptionalHauls++;
                StartJob(child);
            }
        }
        public void AdvanceQueue()
        {
            if (jobQueue.Count == 0) return;
            Job job = jobQueue[0].job;
            jobQueue.RemoveAt(0);
            StartJob(job);
        }
        private void TryFindAndStartJob() { AdvanceQueue(); }
    }
}
namespace AutomaticOutfitManager.State
{
    public enum ApparelTransition { Preparing, Active, ReturningToChangingArea, Restoring }
    public class PawnApparelState
    {
        public ApparelTransition Transition = ApparelTransition.Preparing;
        public bool RecallRequested, MapDepartureRequested, WeaponRuleOverrideExplicit;
        public bool WeaponRestorationRequested;
        public ThingWithComps OriginalWeapon;
        public List<Apparel> OriginalApparel = new List<Apparel>();
        public Job PendingWorkJob;
        public HashSet<Apparel> Apparel = new HashSet<Apparel>();
        public HashSet<ThingWithComps> Weapons = new HashSet<ThingWithComps>();
        public bool IsPreparationApparel(Apparel a) => Apparel.Contains(a);
        public bool IsManagedWeapon(ThingWithComps w) => Weapons.Contains(w);
    }
}
namespace AutomaticOutfitManager.Core
{
    public class AutomaticOutfitManagerGameComponent
    {
        public static AutomaticOutfitManagerGameComponent Current = new AutomaticOutfitManagerGameComponent();
        public Dictionary<Pawn, PawnApparelState> States = new Dictionary<Pawn, PawnApparelState>();
        public PawnApparelState StateFor(Pawn p) => p != null && States.TryGetValue(p, out var state) ? state : null;
    }
}
namespace AutomaticOutfitManager.Patches
{
    internal static class PawnJobTracker_StartJob_Patch
    {
        internal static bool PendingWorkJobIsViable(Pawn p, Job j, out string reason)
        { reason = null; return j?.Viable == true; }
        internal static bool IsNativeEmergencySafetyJob(Job j) => j?.def?.defName == "Flee";
    }
}
class PreparationHandoffTests
{
    static int passed, id;
    public static Job Job(string name) => new Job { def = new JobDef { defName = name }, loadID = ++id };
    public static Job Wait() => new Job { def = JobDefOf.Wait, loadID = ++id };
    static Job Step(Pawn pawn, PawnApparelState state, bool apparel = false)
    {
        ThingWithComps target = apparel ? new Apparel() : new ThingWithComps();
        target.Map = pawn.Map;
        if (apparel) state.Apparel.Add((Apparel)target); else state.Weapons.Add(target);
        return new Job { def = apparel ? JobDefOf.Wear : JobDefOf.Equip,
            loadID = ++id, targetA = new LocalTargetInfo(target), playerForced = apparel };
    }
    static void Check(bool ok, string name)
    { if (!ok) throw new Exception(name); passed++; }
    static Pawn Setup(out PawnApparelState state)
    {
        var p = new Pawn(); state = new PawnApparelState { PendingWorkJob = Job("LayDown") };
        AutomaticOutfitManagerGameComponent.Current.States[p] = state;
        return p;
    }
    static void InjectOptional(Pawn_JobTracker __instance, Job __1, ref Job __result)
    { if (__instance.InjectHaul && __1.def.allowOpportunisticPrefix) __result = Job("HaulToCell"); }
    static void Tests()
    {
        var p = Setup(out var s); var bed = s.PendingWorkJob; var equip = Step(p, s);
        p.jobs.StartJob(equip);
        Check(p.jobs.Rejections == 1 && p.jobs.jobQueue.Count == 0,
            "unpatched native nested optional haul reproduces cancelled Equip");
        Check(s.PendingWorkJob != bed, "unpatched wait destroys original activity");

        p = Setup(out s); s.Transition = ApparelTransition.Active;
        Job work = Job("HaulToCell"); work.def.allowOpportunisticPrefix = true;
        p.jobs.StartJob(work);
        Check(p.jobs.curJob != work && p.jobs.jobQueue[0].job == work,
            "native optional haul displaces the prepared root into the queue before the fix");

        var harmony = new Harmony("aom.tests.preparation-handoff");
        harmony.PatchAll(typeof(PreparationJobHandoff).Assembly);
        harmony.Patch(AccessTools.Method(typeof(Pawn_JobTracker), "TryOpportunisticJob"),
            postfix: new HarmonyMethod(typeof(PreparationHandoffTests), nameof(InjectOptional)) { priority = Priority.Normal });

        foreach (string kind in new[] { "HaulToCell", "DoBill", "ConstructDeliverResourcesToBlueprint", "Reading", "LayDown" })
        {
            p = Setup(out s); s.Transition = ApparelTransition.Active; p.jobs.InjectHaul = true;
            work = Job(kind); work.def.allowOpportunisticPrefix = true;
            PreparationJobHandoff.RecordPreparedActivity(p, s, work);
            p.jobs.StartJob(work);
            Check(p.jobs.curJob == work && p.jobs.jobQueue.Count == 0 && p.jobs.OptionalHauls == 0,
                kind + " starts the exact prepared root before native or compatibility optional hauling");
            Check(!PreparationJobHandoff.IsPreparedActivity(p, s, work),
                kind + " drops admission marker once tracker actually owns the job");
            Check(p.jobs.TryOpportunisticJob(null, work) != null,
                kind + " does not disable later ordinary hauling for that job definition");
        }
        p = Setup(out s); s.Transition = ApparelTransition.Active; p.jobs.InjectHaul = true;
        work = Job("HaulToCell"); work.def.allowOpportunisticPrefix = true;
        PreparationJobHandoff.RecordPreparedActivity(p, s, work);
        Job care = Job("ReturnBabyToCrib"); p.jobs.NativeFinalizer = care;
        p.jobs.StartJob(work);
        Check(p.jobs.curJob == care && p.jobs.jobQueue[0].job == work,
            "real native childcare finalizer remains ahead of exact prepared work");
        Check(PreparationJobHandoff.IsPreparedActivity(p, s, work),
            "queued parent retains its marker while care finalizer owns tracker");
        Check(PreparationJobHandoff.PreservePreparedActivity(p, s, care),
            "exact native finalizer cannot spend outer/nested buffer before prepared parent starts");
        Check(PreparationJobHandoff.PreservePreparedActivity(p, s, Wait()),
            "connective wait retains queued prepared parent even with Immediate buffer");
        Check(!PreparationJobHandoff.PreservePreparedActivity(p, s, Job("HaulToCell")),
            "unrelated hauling does not inherit prepared finalizer buffer exemption");
        work.Viable = false;
        Check(!PreparationJobHandoff.PreservePreparedActivity(p, s, care), "invalid parent cannot retain outfit through finalizer");
        work.Viable = true;
        p.jobs.AdvanceQueue();
        Check(p.jobs.curJob == work && p.jobs.OptionalHauls == 0,
            "native finalizer resumes its sole queued parent without an optional haul");

        foreach (string flag in new[] { "forced", "draft", "downed", "mental", "recall", "departure", "map", "state", "pooled", "restoring", "load" })
        {
            p = Setup(out s); s.Transition = ApparelTransition.Active;
            work = Job("DoBill"); PreparationJobHandoff.RecordPreparedActivity(p, s, work);
            work.playerForced = flag == "forced"; p.Drafted = flag == "draft"; p.Downed = flag == "downed";
            p.InMentalState = flag == "mental"; s.RecallRequested = flag == "recall"; s.MapDepartureRequested = flag == "departure";
            if (flag == "map") p.Map = new Map();
            if (flag == "state") s = new PawnApparelState { Transition = ApparelTransition.Active };
            if (flag == "pooled") work.loadID++;
            if (flag == "restoring") s.Transition = ApparelTransition.Restoring;
            if (flag == "load") PreparationJobHandoff.ResetForLoadedGame();
            Check(!PreparationJobHandoff.IsPreparedActivity(p, s, work), flag + " revokes exact prepared admission marker");
        }
        foreach (string kind in new[] { "Ingest", "ModIngestMeal", "Flee" })
        {
            p = Setup(out s); s.Transition = ApparelTransition.Active;
            work = Job(kind); work.def.allowOpportunisticPrefix = true;
            PreparationJobHandoff.RecordPreparedActivity(p, s, work);
            Check(p.jobs.TryOpportunisticJob(null, work) != null,
                kind + " keeps its native or separate meal compatibility policy");
        }
        p = Setup(out s); p.jobs.Guards = true; p.jobs.InjectHaul = true;
        equip = Step(p, s); bed = s.PendingWorkJob;
        p.jobs.StartJob(equip);
        Check(p.jobs.curJob == equip && p.jobs.OptionalHauls == 0 && p.jobs.Rejections == 0,
            "owned Equip bypasses optional native and compatibility hauling");
        Check(!equip.playerForced, "Equip does not gain sidearm preference override");
        Check(s.PendingWorkJob == bed && p.jobs.Credits == 0, "admission keeps bed and does not credit buffer");

        var foreign = Step(p, s); s.Weapons.Remove((ThingWithComps)foreign.targetA.Thing);
        Job optional = p.jobs.TryOpportunisticJob(null, foreign);
        Check(optional?.def?.defName == "HaulToCell", "ordinary unassigned Equip retains native opportunistic behavior");
        var forced = Step(p, s); forced.playerForced = true;
        Check(p.jobs.TryOpportunisticJob(null, forced) != null, "player weapon choice gets no AOM preparation override");
        var restoring = Step(p, s); s.Transition = ApparelTransition.Restoring;
        Check(p.jobs.TryOpportunisticJob(null, restoring) != null, "personal restoration not covered by preparation suppression");

        p = Setup(out s); p.jobs.Guards = true; p.jobs.InjectHaul = true;
        bed = s.PendingWorkJob; equip = Step(p, s);
        var child = Job("PutBabyInCrib"); p.jobs.NativeFinalizer = child;
        p.jobs.StartJob(equip);
        Check(p.jobs.curJob == child && PreparationJobHandoff.IsQueued(p, equip),
            "required finalizer starts and leaves exact Equip queued");
        Check(p.jobs.Rejections == 0 && s.PendingWorkJob == bed, "nested finalizer does not reject weapon or recapture goal");
        Check(PreparationJobHandoff.IsFinalizer(p, s, child), "exact child owns its finalizer exemption");
        p.jobs.AdvanceQueue();
        Check(p.jobs.curJob == equip && p.jobs.Rejections == 0 && p.jobs.jobQueue.Count == 0,
            "parent Equip resumes after real finalizer");

        foreach (string goal in new[] { "LayDown", "HaulToCell", "HaulToContainer", "FixBrokenDownBuilding" })
        {
            p = Setup(out s); p.jobs.Guards = true; s.PendingWorkJob = bed = Job(goal);
            var wear1 = Step(p, s, true); var wear2 = Step(p, s, true); equip = Step(p, s);
            p.jobs.jobQueue.Add(new QueuedJob { job = wear2 });
            p.jobs.jobQueue.Add(new QueuedJob { job = equip });
            p.jobs.StartJob(wear1);
            var wait = Job("Wait_MaintainPosture");
            p.jobs.StartJob(wait);
            Check(s.PendingWorkJob == bed && p.jobs.jobQueue.Count == 2, goal + " retains two queued steps across first wait");
            p.jobs.AdvanceQueue();
            Check(p.jobs.curJob == wear2, goal + " second Wear starts");
            p.jobs.StartJob(Job("Wait_MaintainPosture")); p.jobs.AdvanceQueue();
            Check(p.jobs.curJob == equip && s.PendingWorkJob == bed && p.jobs.Rejections == 0,
                goal + " exact Equip and goal survive second wait");
            Check(p.jobs.Credits == 0, goal + " no preparation buffer credit");
        }

        p = Setup(out s); equip = Step(p, s); p.jobs.jobQueue.Add(new QueuedJob { job = equip });
        Check(PreparationJobHandoff.HasQueuedStep(p, s), "valid assigned queue recognized after load without registry entry");
        var waitJob = Wait();
        Check(PreparationJobHandoff.PreserveBeforeRetry(p, s, waitJob), "queued Equip survives connective wait beyond retry timer");
        Check(!PreparationJobHandoff.PreserveBeforeRetry(p, s, Job("Ingest")), "unrelated activity is not a finalizer");
        Check(!PreparationJobHandoff.PreserveBeforeRetry(p, s, Job("Wait_Downed")), "downed wait excluded");
        waitJob.playerForced = true;
        Check(!PreparationJobHandoff.PreserveBeforeRetry(p, s, waitJob), "player wait excluded");
        waitJob.playerForced = false; waitJob.targetA = new LocalTargetInfo(new Thing());
        Check(!PreparationJobHandoff.IsConnectiveWait(p, waitJob), "targeted wait excluded");
        waitJob.targetA = new LocalTargetInfo(p);
        Check(PreparationJobHandoff.IsConnectiveWait(p, waitJob), "AOM self-target wait recognized");
        waitJob.targetQueueA = new List<LocalTargetInfo> { new LocalTargetInfo(new Thing()) };
        Check(!PreparationJobHandoff.IsConnectiveWait(p, waitJob), "target queue excluded");

        foreach (string flag in new[] { "recall", "departure", "draft", "downed", "mental", "unspawned" })
        {
            s.RecallRequested = flag == "recall"; s.MapDepartureRequested = flag == "departure";
            p.Drafted = flag == "draft"; p.Downed = flag == "downed";
            p.InMentalState = flag == "mental"; p.Spawned = flag != "unspawned";
            Check(!PreparationJobHandoff.HasQueuedStep(p, s), flag + " does not preserve preparation against native control");
        }
        s.RecallRequested = s.MapDepartureRequested = p.Drafted = p.Downed = p.InMentalState = false; p.Spawned = true;
        equip.targetA.Thing.Destroyed = true;
        Check(!PreparationJobHandoff.HasQueuedStep(p, s), "destroyed queued target falls through to bounded recovery");
        equip.targetA.Thing.Destroyed = false; equip.targetA.Thing.Map = new Map();
        Check(!PreparationJobHandoff.HasQueuedStep(p, s), "foreign map target excluded");
        equip.targetA.Thing.Map = p.Map; equip.playerForced = true;
        Check(!PreparationJobHandoff.HasQueuedStep(p, s), "forced external Equip excluded");
        equip.playerForced = false; s.WeaponRuleOverrideExplicit = true;
        Check(!PreparationJobHandoff.HasQueuedStep(p, s), "weapon override excluded");
        s.WeaponRuleOverrideExplicit = false;
        foreach (var transition in new[] { ApparelTransition.Active, ApparelTransition.Restoring, ApparelTransition.ReturningToChangingArea })
        {
            s.Transition = transition;
            Check(!PreparationJobHandoff.HasQueuedStep(p, s), transition + " does not gain preparation ownership");
        }
        s.Transition = ApparelTransition.Preparing; p.jobs.jobQueue.Clear();
        Check(!PreparationJobHandoff.PreserveBeforeRetry(p, s, Wait()), "missing queue permits actual failure retry");
        Check(PreparationJobHandoff.PreservePendingWait(p, s, Wait()), "lost queue cannot turn saved bed task into Standing");
        Check(!PreparationJobHandoff.PreservePendingWait(p, s, Job("Ingest")), "meaningful new task is not a connective wait");
        s.RecallRequested = true;
        Check(!PreparationJobHandoff.PreservePendingWait(p, s, Wait()), "Recall can retire a pending task with lost queue");
        s.RecallRequested = false;

        // Reproduce the saved-weapon queue being displaced by optional hauling.
        p = Setup(out s); p.jobs.Guards = true;
        s.Transition = ApparelTransition.Restoring; s.RecallRequested = true;
        equip = Step(p, s); s.Weapons.Clear();
        s.OriginalWeapon = (ThingWithComps)equip.targetA.Thing; s.WeaponRestorationRequested = true;
        p.jobs.StartJob(equip);
        Check(p.jobs.curJob == equip && p.jobs.OptionalHauls == 0 && p.jobs.Rejections == 0,
            "exact saved Equip cannot be displaced by optional hauling during Recall");

        foreach (bool savedApparel in new[] { false, true })
        {
            p = Setup(out s); p.jobs.Guards = true; s.Transition = ApparelTransition.Restoring;
            s.RecallRequested = s.MapDepartureRequested = true;
            var saved = Step(p, s, savedApparel); s.Apparel.Clear(); s.Weapons.Clear();
            if (savedApparel) s.OriginalApparel.Add((Apparel)saved.targetA.Thing);
            else { s.OriginalWeapon = (ThingWithComps)saved.targetA.Thing; s.WeaponRestorationRequested = true; }
            p.jobs.NativeFinalizer = child = Job("RealChildcareFinalizer");
            p.jobs.StartJob(saved);
            Check(p.jobs.curJob == child && p.jobs.jobQueue.Count == 1 && p.jobs.jobQueue[0].job == saved,
                "real finalizer keeps exact saved parent queued, including Recall and departure");
            Check(p.jobs.OptionalHauls == 0 && p.jobs.Rejections == 0 && p.jobs.Credits == 0,
                "saved restoration finalizer neither retries nor invents activity credit");
            Check(PreparationJobHandoff.PreserveRestorationBeforeRetry(p,s,Wait()),"native wait preserves saved queue");
            Check(!PreparationJobHandoff.PreserveRestorationBeforeRetry(p,s,Job("HaulToCell")),"unrelated haul is not a saved-gear finalizer");
            foreach (string flag in new[] {"draft","downed","mental","unspawned","destroyed","map"})
            {
                p.Drafted=flag=="draft";p.Downed=flag=="downed";p.InMentalState=flag=="mental";p.Spawned=flag!="unspawned";
                saved.targetA.Thing.Destroyed=flag=="destroyed";saved.targetA.Thing.Map=flag=="map"?new Map():p.Map;
                Check(!PreparationJobHandoff.PreserveRestorationBeforeRetry(p,s,Wait()),flag+" revokes saved queue preservation");
            }
            p.Drafted=p.Downed=p.InMentalState=false;p.Spawned=true;saved.targetA.Thing.Destroyed=false;saved.targetA.Thing.Map=p.Map;
            p.jobs.AdvanceQueue();
            Check(p.jobs.curJob==saved && p.jobs.jobQueue.Count==0,"exact saved item advances after native finalizer");
            Check(!PreparationJobHandoff.PreserveRestorationBeforeRetry(p,s,child),"consumed finalizer has no leftover admission");
            var foreignSaved=Step(p,s,savedApparel);
            Check(!PreparationJobHandoff.IsRestorationStep(p,s,foreignSaved),"another item is not part of exact snapshot");
            saved.playerForced=true;
            if(!savedApparel) Check(!PreparationJobHandoff.IsRestorationStep(p,s,saved),"external forced Equip remains native");
        }
        p = Setup(out s); equip = Step(p, s);
        p.jobs.jobQueue.Add(new QueuedJob { job = equip }); child = Job("Finalizer");
        PreparationJobHandoff.MarkFinalizer(p, equip, child);
        Check(PreparationJobHandoff.IsFinalizer(p, s, child), "marked exact finalizer");
        child.loadID++; Check(!PreparationJobHandoff.IsFinalizer(p, s, child), "pooled child identity guarded"); child.loadID--;
        equip.loadID++; Check(!PreparationJobHandoff.IsFinalizer(p, s, child), "pooled parent identity guarded"); equip.loadID--;
        var other = Setup(out var otherState);
        Check(!PreparationJobHandoff.IsFinalizer(other, otherState, child), "other pawn/state cannot inherit admission");
        PreparationJobHandoff.ResetForLoadedGame();
        Check(!PreparationJobHandoff.IsFinalizer(p, s, child) && PreparationJobHandoff.HasQueuedStep(p, s),
            "load clears transient finalizer identity while queued owned gear remains recoverable");

        s.Transition = ApparelTransition.Active; p.jobs.curJob = Wait(); p.jobs.jobQueue.Clear();
        var haul = Job("HaulToContainer"); p.jobs.jobQueue.Add(new QueuedJob { job = haul });
        Check(PreparationJobHandoff.CanResumeQueuedActivity(p, s), "active valid native queue advances before idle recall");
        p.jobs.curJob = null;
        Check(!PreparationJobHandoff.CanResumeQueuedActivity(p, s), "empty tracker recovery is limited to an identified prepared meal");
        p.jobs.curJob = Wait();
        Check(p.jobs.Credits == 0 && p.jobs.curJob.def == JobDefOf.Wait, "policy query cannot execute or credit a job");
        haul.Viable = false;
        Check(!PreparationJobHandoff.CanResumeQueuedActivity(p, s), "invalid queued work does not suppress idle fallback");
        haul.Viable = true; s.RecallRequested = true;
        Check(!PreparationJobHandoff.CanResumeQueuedActivity(p, s), "explicit Recall remains authoritative");
        s.RecallRequested = false; p.jobs.curJob = Job("FleeAndCower");
        Check(!PreparationJobHandoff.CanResumeQueuedActivity(p, s), "active emergency never interrupted to advance queue");
        p.jobs.curJob = Wait(); p.jobs.jobQueue.Clear();
        Check(!PreparationJobHandoff.CanResumeQueuedActivity(p, s), "empty queue cannot invent a new hauling assignment");
        p.jobs.jobQueue.Add(new QueuedJob { job = Wait() });
        Check(!PreparationJobHandoff.CanResumeQueuedActivity(p, s), "only queued waits cannot hold outfit forever");
        p.jobs.jobQueue.Add(new QueuedJob { job = haul });
        Check(PreparationJobHandoff.CanResumeQueuedActivity(p, s), "connective queued wait does not hide next actual task");
        Check(PreparationJobHandoff.QueueDescription(p).Contains("HaulToContainer#"), "queue diagnostics include exact native identity");
        Check(PreparationJobHandoff.TrackerDescription(p).Contains("tick=100, current=Wait#"), "return evidence identifies current job and actual game tick");
    }
    public static int Main()
    {
        try { Tests(); Console.WriteLine($"PASS {passed} preparation handoff checks"); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
