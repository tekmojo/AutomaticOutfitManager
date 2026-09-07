// Compile the production admission policy and detached retry registry against
// native-shaped inputs. Pathfinding is controlled; these are not gameplay tests.
using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;
using Verse.AI;

namespace Verse
{
    public class Map { }
    public enum Danger { Deadly }
    public struct IntVec3
    {
        public int x; public IntVec3(int value) { x = value; }
        public bool IsValid => x >= 0;
        public bool InBounds(Map map) => map != null && x >= 0 && x < 1000;
    }
    public class Area
    {
        public Map Map; public HashSet<int> Cells = new HashSet<int>();
        public bool this[IntVec3 cell] => Cells.Contains(cell.x);
    }
    public class Thing
    {
        public bool Spawned = true, Destroyed, Reservable = true, Reachable = true, Forbidden;
        public Map Map; public IntVec3 Position; public int stackCount = 10;
        public bool IsForbidden(Pawn pawn) => Forbidden;
    }
    public class ThingCountClass { }
    public struct LocalTargetInfo
    {
        public Thing Thing; public bool HasThing => Thing != null;
        public bool IsValid => Thing != null;
        public IntVec3 Cell => Thing?.Position ?? new IntVec3(-1);
        public LocalTargetInfo(Thing thing) { Thing = thing; }
        public static implicit operator LocalTargetInfo(Thing thing) => new LocalTargetInfo(thing);
    }
    public class CarryTracker { public Thing CarriedThing; }
    public class Pawn : Thing
    {
        public bool Drafted, Downed, InMentalState, Eligible = true;
        public string LabelShortCap = "Oto"; public Job CurJob;
        public CarryTracker carryTracker = new CarryTracker();
        public bool CanReserve(LocalTargetInfo target, int max, int count, object layer, bool forced)
        {
            if (max != 1 || count != -1 || forced) throw new Exception("wrong native reservation mode");
            return target.Thing?.Reservable == true;
        }
        public bool CanReach(LocalTargetInfo target, PathEndMode mode, Danger danger)
        {
            if (mode != PathEndMode.ClosestTouch) throw new Exception("wrong pickup endpoint mode");
            return target.Thing?.Reachable == true;
        }
    }
    public class JobDef { public string defName = "DoBill"; public Type driverClass = typeof(JobDriver_DoBill); }
    public class TickManager { public int TicksGame; }
    public static class Find { public static TickManager TickManager = new TickManager(); }
}
namespace Verse.AI
{
    public enum PathEndMode { Touch, ClosestTouch }
    public class JobDriver_DoBill { }
    public class Job
    {
        public JobDef def; public bool playerForced; public object workGiverDef = new object(), bill = new object();
        public int loadID = 55; public LocalTargetInfo targetA, targetB, targetC;
        public List<LocalTargetInfo> targetQueueA, targetQueueB;
        public List<int> countQueue; public List<ThingCountClass> placedThings;
        public Job Clone() => (Job)MemberwiseClone();
    }
}
namespace RimWorld { public static class JobDefOf { public static JobDef DoBill = new JobDef(); } }
namespace AutomaticOutfitManager.Rules
{
    public class ApparelRule
    {
        public string Id, Name; public bool IsNonWork, WorkAreaPaused, Enabled = true;
        public bool Applicable = true, Allowed = true, Missing = true, Handoff = true, Blocked;
        public Area Area;
    }
}
namespace AutomaticOutfitManager.State
{
    public enum ApparelTransition { Preparing, Active, ReturningToChangingArea, Restoring }
    public class PawnApparelState { public Pawn Pawn; public ApparelTransition Transition; public bool RecallRequested; public string ActiveRuleId; }
    public static class NonWorkOutfitPolicy { public static bool NeedsStateHandoff(Pawn pawn, ApparelRule rule) => rule.Handoff; }
}
namespace AutomaticOutfitManager.Core
{
    public class AutomaticOutfitManagerGameComponent
    {
        public static AutomaticOutfitManagerGameComponent Current;
        public List<ApparelRule> Rules = new List<ApparelRule>();
        public ApparelRule RuleById(string id) => Rules.FirstOrDefault(r => r.Id == id);
    }
    public static class AomLog
    {
        public static bool DetailedEnabled = true;
        public static bool ShouldLogDetailed(Pawn pawn, string key, int interval = 0) => true;
        public static void Detailed(string value) { }
    }
}
namespace AutomaticOutfitManager.Detection
{
    public static class PawnAccessClassifier { public static bool IsApparelEligibleHuman(Pawn pawn) => pawn.Eligible; }
    public static class UnavailableWorkRegistry { public static bool HasActiveRuleBlock(Pawn p, ApparelRule r) => r.Blocked; }
    public static class RuleEvaluator
    {
        public static IReadOnlyList<ApparelRule> ActiveRulesForMap(Map map) => AutomaticOutfitManagerGameComponent.Current.Rules.Where(r => r.Enabled && !r.WorkAreaPaused && r.Area.Map == map).ToList();
        public static bool RuleCanApplyToPawn(Pawn pawn, ApparelRule rule) => rule.Applicable;
        public static bool HasMissingRequiredGear(Pawn pawn, ApparelRule rule) => rule.Missing;
        public static bool JobTargetsArea(Job job, Area area) => new[] { job.targetA, job.targetB, job.targetC }.Concat(job.targetQueueA ?? new List<LocalTargetInfo>()).Concat(job.targetQueueB ?? new List<LocalTargetInfo>()).Any(t => t.IsValid && area[t.Cell]);
    }
}
namespace AutomaticOutfitManager.Patches
{
    public static class PausedAreaWorkFilter { public static bool ActivityAllowedAtRuleBoundary(Pawn p, Job j, ApparelRule r) => r.Allowed; }
    public static class ProtectedPathAvoidance
    {
        public static bool Avoids; public static int Calls; public static Thing LastTarget;
        public static bool SegmentAvoidsRules(Pawn pawn, IntVec3 start, LocalTargetInfo target,
            List<ApparelRule> rules, Predicate<IntVec3> unsafeCell, PathEndMode? exactEndMode)
        {
            if (exactEndMode != PathEndMode.ClosestTouch || rules.Count != 1 || !rules[0].IsNonWork)
                throw new Exception("wrong pickup route restrictions");
            Calls++; LastTarget = target.Thing; return Avoids;
        }
    }
}
class NonWorkIngredientAdmissionTests
{
    static int checks; static Pawn pawn; static PawnApparelState state; static Job job; static ApparelRule work, dining; static Thing first, second, stove;
    static void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
    static void Setup()
    {
        ProtectedBoundaryRetryRegistry.ResetForLoadedGame(); Find.TickManager.TicksGame = 100;
        ProtectedPathAvoidance.Avoids = false; ProtectedPathAvoidance.Calls = 0; ProtectedPathAvoidance.LastTarget = null;
        var map = new Map(); pawn = new Pawn { Map = map, Position = new IntVec3(10) };
        work = new ApparelRule { Id = "work", Name = "Radiation", Area = new Area { Map = map, Cells = new HashSet<int> { 10 } } };
        dining = new ApparelRule { Id = "dining", Name = "Dining", IsNonWork = true, Area = new Area { Map = map, Cells = new HashSet<int> { 50 } } };
        AutomaticOutfitManagerGameComponent.Current = new AutomaticOutfitManagerGameComponent { Rules = new List<ApparelRule> { work, dining } };
        state = new PawnApparelState { Pawn = pawn, ActiveRuleId = work.Id, Transition = ApparelTransition.Active };
        first = new Thing { Map = map, Position = new IntVec3(50) }; second = new Thing { Map = map, Position = new IntVec3(70) }; stove = new Thing { Map = map, Position = new IntVec3(80) };
        job = new Job { def = JobDefOf.DoBill, targetA = stove, targetQueueB = new List<LocalTargetInfo> { first, second }, countQueue = new List<int> { 3, 4 } };
    }
    static IReadOnlyList<ApparelRule> Rules() => NonWorkIngredientAdmission.RequiredRules(pawn, state, job);
    static void Skip(Action change, string name, bool expectNoPath = true)
    {
        Setup(); change(); Check(Rules().Count == 0, name);
        if (expectNoPath) Check(ProtectedPathAvoidance.Calls == 0, name + " avoids path query");
    }
    static void Main()
    {
        Setup(); var rules = Rules(); Check(rules.Count == 1 && rules[0] == dining, "required first ingredient recognized before departure");
        Check(ProtectedPathAvoidance.LastTarget == first && ProtectedPathAvoidance.Calls == 1, "check the native first pickup only");
        Check(job.targetQueueB.Count == 2 && job.countQueue.SequenceEqual(new[] { 3, 4 }) && !state.RecallRequested, "admission inspection does not mutate job or buffer state");
        Skip(() => ProtectedPathAvoidance.Avoids = true, "outside touching cell avoids unnecessary change", false);
        Skip(() => { first.Position = new IntVec3(70); second.Position = new IntVec3(50); }, "later ingredient does not preempt the first leg");
        Skip(() => { first.Position = new IntVec3(70); job.targetB = new Thing { Map = pawn.Map, Position = new IntVec3(50) }; }, "stale auxiliary target is not first pickup");
        Skip(() => work.Area.Cells.Add(80), "protected worksite keeps sequential handling");
        Skip(() => work.Area.Cells.Add(70), "later Work ingredient keeps sequential handling");
        Skip(() => work.Area.Cells.Add(50), "overlapping Work source is not combined with Non-Work");
        Skip(() => pawn.Drafted = true, "draft override"); Skip(() => pawn.Downed = true, "downed override");
        Skip(() => pawn.InMentalState = true, "mental override"); Skip(() => pawn.Eligible = false, "animals and robots excluded");
        Skip(() => job.playerForced = true, "player order override"); Skip(() => state.RecallRequested = true, "recall owns transition");
        foreach (var transition in new[] { ApparelTransition.Preparing, ApparelTransition.ReturningToChangingArea, ApparelTransition.Restoring })
            Skip(() => state.Transition = transition, "existing transition owns job " + transition);
        Skip(() => state = null, "no active state"); Skip(() => state.Pawn = new Pawn(), "different state owner");
        Skip(() => state.ActiveRuleId = dining.Id, "Non-Work buffer retained");
        Skip(() => job = null, "no job"); Skip(() => job.def = new JobDef { driverClass = typeof(object) }, "custom driver untouched");
        Setup(); JobDefOf.DoBill.driverClass = typeof(object); Check(Rules().Count == 0, "replaced DoBill driver untouched"); JobDefOf.DoBill.driverClass = typeof(JobDriver_DoBill);
        Skip(() => job.workGiverDef = null, "non-workgiver bill untouched"); Skip(() => job.bill = null, "missing bill");
        Skip(() => pawn.CurJob = job, "running driver must not be rewound");
        Skip(() => pawn.carryTracker.CarriedThing = first, "carried material preserved");
        Skip(() => job.placedThings = new List<ThingCountClass> { new ThingCountClass() }, "partially staged bill preserved");
        Skip(() => job.targetQueueB = null, "no ingredient queue"); Skip(() => job.targetQueueB.Clear(), "empty ingredient queue");
        Skip(() => job.countQueue = null, "missing counts"); Skip(() => job.countQueue.RemoveAt(0), "misaligned counts");
        Skip(() => job.countQueue[0] = 0, "zero count"); Skip(() => job.countQueue[0] = 11, "source stack shrank");
        Skip(() => first.Destroyed = true, "destroyed source"); Skip(() => second.Destroyed = true, "invalid later ingredient");
        Skip(() => first.Spawned = false, "held source"); Skip(() => first.Map = new Map(), "cross-map source");
        Skip(() => first.Position = new IntVec3(-1), "invalid source position"); Skip(() => second.Position = new IntVec3(1001), "out-of-bounds later source");
        Skip(() => first.Forbidden = true, "forbidden source");
        Skip(() => stove.Destroyed = true, "destroyed worksite"); Skip(() => stove.Map = new Map(), "cross-map worksite");
        Skip(() => stove.Position = new IntVec3(-1), "invalid worksite position"); Skip(() => stove.Forbidden = true, "forbidden worksite");
        Skip(() => first.Reservable = false, "contested source"); Skip(() => stove.Reservable = false, "contested worksite");
        Skip(() => first.Reachable = false, "unreachable source");
        Skip(() => first.Position = new IntVec3(60), "moved source re-evaluated");
        Skip(() => dining.Enabled = false, "disabled destination"); Skip(() => dining.WorkAreaPaused = true, "paused destination");
        Skip(() => dining.Allowed = false, "activity denied"); Skip(() => dining.Applicable = false, "outfit cannot apply");
        Skip(() => dining.Blocked = true, "unavailable outfit backoff");
        Skip(() => { dining.Missing = false; dining.Handoff = false; }, "already compliant retained outfit");
        Skip(() => dining.Area.Cells.Add(10), "existing occupancy handled normally");
        Skip(() => pawn.Position = new IntVec3(-1), "invalid pawn position");
        Setup(); NonWorkIngredientAdmission.Preserve(pawn, job, Rules());
        Check(ProtectedBoundaryRetryRegistry.TryGetPendingInterruption(pawn, out Job retained, out var retainedRules), "exact task retained");
        Check(retained != job && retained.loadID == job.loadID && retained.targetA.Thing == stove, "detached root retains identity and worksite");
        Check(retained.targetQueueB != job.targetQueueB && retained.countQueue != job.countQueue, "mutable queues single-owned");
        job.targetQueueB.RemoveAt(0); job.countQueue.RemoveAt(0); job.def = null;
        Check(retained.def == JobDefOf.DoBill && retained.targetQueueB[0].Thing == first && retained.countQueue.SequenceEqual(new[] { 3, 4 }), "native queue consumption and pooling do not corrupt continuation");
        Check(retainedRules.Count == 1 && retainedRules[0] == dining, "source rule retained with exact job");
        dining.Allowed = false; Check(!ProtectedBoundaryRetryRegistry.TryGetPendingInterruption(pawn, out _, out _), "changed permission invalidates retry");
        dining.Allowed = true; Check(!ProtectedBoundaryRetryRegistry.TryGetPendingInterruption(pawn, out _, out _), "invalidated task cannot revive");
        Setup(); NonWorkIngredientAdmission.Preserve(pawn, job, Rules()); first.Destroyed = true;
        Check(!ProtectedBoundaryRetryRegistry.TryGetPendingInterruption(pawn, out _, out _), "destroyed retained source cancels retry");
        Setup(); NonWorkIngredientAdmission.Preserve(pawn, job, Rules()); pawn.Map = new Map();
        Check(!ProtectedBoundaryRetryRegistry.TryGetPendingInterruption(pawn, out _, out _), "map change cancels retry");
        Setup(); NonWorkIngredientAdmission.Preserve(pawn, job, Rules()); ProtectedBoundaryRetryRegistry.ResetForLoadedGame();
        Check(!ProtectedBoundaryRetryRegistry.TryGetPendingInterruption(pawn, out _, out _), "transient retry does not survive load");
        Console.WriteLine("Non-Work ingredient admission: " + checks + " checks passed.");
    }
}
