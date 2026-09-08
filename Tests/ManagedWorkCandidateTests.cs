using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;
using Verse.AI;

class ManagedWorkCandidateTests
{
    static int passed;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); passed++; }
    static Thing Item(Map map, int cell, string name) => new Thing { MapHeld = map, PositionHeld = new IntVec3(cell), LabelCap = name };
    static Job Work(Thing site, Thing material) => new Job { def = new JobDef { defName = "ConstructDeliverResourcesToBlueprint" },
        targetA = new LocalTargetInfo(site), targetQueueB = new List<LocalTargetInfo> { new LocalTargetInfo(material) } };
    static void TargetlessTests(bool queueFirst)
    {
        var map = new Map(); var owner = new Pawn { Map = map, LabelShortCap = "Mizo" };
        var contender = new Pawn { Map = map, LabelShortCap = "Lumi" };
        var wait = new Job { def = new JobDef { defName = "Wait_MaintainPosture" } };
        ManagedWorkClaimRegistry.ResetForLoadedGame();
        Check(default(LocalTargetInfo).IsValid && default(LocalTargetInfo).Cell == new IntVec3(0),
            "native default LocalTargetInfo is valid at the origin");
        Check(!wait.targetA.IsValid && !wait.targetB.IsValid && !wait.targetC.IsValid,
            "native Job fields initialize to Invalid, not default(LocalTargetInfo)");
        ManagedWorkClaimRegistry.TryClaim(owner, wait);
        if (!queueFirst)
            Check(!ManagedWorkClaimRegistry.HasActiveClaim(owner), "targetless preparation creates no origin claim");

        // Native/compatibility flow: Wear completes, next saved Wear remains
        // queued, then an optional connective Wait enters StartJob. Execute the
        // actual production claim fallback before admitting the native child.
        var tracker = new QueueTracker();
        var savedWear = new Job { def = new JobDef { defName = "Wear" }, targetA = new LocalTargetInfo(Item(map, 25, "Saved coat")) };
        tracker.Queue.Enqueue(savedWear);
        Job next = wait;
        ClaimAdmissionFixture.Start(tracker, contender, ref next);
        Check(tracker.Queue.Count == 1 && tracker.Queue.Peek() == savedWear,
            "connective wait preserves queued saved Wear after native outfit completion");
        Check(next == wait, "targetless native wait is admitted without a replacement");
        Job resumed = tracker.Queue.Dequeue();
        ClaimAdmissionFixture.Start(tracker, contender, ref resumed);
        Check(resumed == savedWear, "native queue resumes the exact saved garment");
        Check(!ManagedWorkClaimRegistry.IsClaimedByOther(contender, wait), "cleared restoration can wait without false contention");

        Job origin = new Job { def = new JobDef { defName = "Clean" }, targetA = new LocalTargetInfo(new IntVec3(0)) };
        Check(ManagedWorkClaimRegistry.TryClaim(owner, origin), "explicit origin job is claimable");
        Check(ManagedWorkClaimRegistry.IsClaimedByOther(contender, origin), "real origin claims remain exclusive");
        Check(!ManagedWorkClaimRegistry.IsClaimedByOther(contender, wait), "targetless wait does not contend with a real origin job");
        ManagedWorkClaimRegistry.Release(owner, wait);
        Check(ManagedWorkClaimRegistry.HasActiveClaim(owner), "targetless release cannot release a real origin reservation");
        ManagedWorkClaimRegistry.Release(owner, origin);
        Check(!ManagedWorkClaimRegistry.HasActiveClaim(owner), "explicit origin release removes that claim");
        var originHaul = new Job { def = JobDefOf.HaulToCell, targetA = new LocalTargetInfo(Item(map, 33, "Steel")), targetB = origin.targetA };
        ManagedWorkClaimRegistry.TryClaim(owner, originHaul);
        Check(ManagedWorkClaimRegistry.IsClaimedByOther(contender, origin), "hauling to the origin retains its destination reservation");
        foreach (string field in new[] { "B", "C", "QueueA", "QueueB" })
        {
            ManagedWorkClaimRegistry.ResetForLoadedGame();
            var queuedCell = new Job { def = origin.def };
            if (field == "B") queuedCell.targetB = origin.targetA;
            if (field == "C") queuedCell.targetC = origin.targetA;
            if (field == "QueueA") queuedCell.targetQueueA = new List<LocalTargetInfo> { LocalTargetInfo.Invalid, origin.targetA };
            if (field == "QueueB") queuedCell.targetQueueB = new List<LocalTargetInfo> { LocalTargetInfo.Invalid, origin.targetA };
            ManagedWorkClaimRegistry.TryClaim(owner, queuedCell);
            Check(ManagedWorkClaimRegistry.IsClaimedByOther(contender, origin), field + " retains an explicit cell after invalid targets");
        }
        ManagedWorkClaimRegistry.ResetForLoadedGame();
    }
    static void Tests()
    {
        var map = new Map(); var owner = new Pawn { Map = map, LabelShortCap = "Bowman" };
        var contender = new Pawn { Map = map, LabelShortCap = "Lumi" };
        Thing steel = Item(map, 10, "Steel"), frame = Item(map, 20, "Cooler"), lamp = Item(map, 30, "Lamp");
        Job prepared = Work(frame, steel), competing = Work(lamp, steel);
        Find.TickManager.TicksGame = 100; ManagedWorkClaimRegistry.ResetForLoadedGame();
        Check(ManagedWorkClaimRegistry.TryClaim(owner, prepared), "prepared native work claims exact frame and queued material atomically");
        Check(!ManagedWorkClaimRegistry.IsClaimedByOther(contender, map, lamp, lamp.PositionHeld),
            "primary target-only scanner check misses the competing queued steel");
        Check(ManagedWorkCandidateFilter.Rejects(contender, competing), "complete job candidate rejects claimed queued steel before selection commits");
        Check(AutomaticOutfitManagerGameComponent.ReleasedPawn == contender && AutomaticOutfitManagerGameComponent.ReleasedJob == competing,
            "only rejected contender's exact native job reservations are released");
        Check(ManagedWorkClaimRegistry.HasActiveClaim(owner), "rejection retains rightful owner's prepared claim");
        Check(ManagedWorkClaimRegistry.DescribeConflict(contender, competing).Contains("Bowman holds Steel"), "contention diagnostic names owner and actual material");
        Check(!ManagedWorkCandidateFilter.Rejects(owner, prepared), "owner can resume its exact prepared job");
        Job unrelated = Work(Item(map, 40, "Other frame"), Item(map, 50, "Wood"));
        Job selected = new[] { competing, unrelated }.FirstOrDefault(j => !ManagedWorkCandidateFilter.Rejects(contender, j));
        Check(selected == unrelated, "native candidate iteration can choose unrelated legal work instead of committing to a Wait");

        foreach (string field in new[] { "A", "B", "C", "QueueA", "QueueB" })
        {
            Job j = new Job { def = new JobDef { defName = "DoBill" } };
            var target = new LocalTargetInfo(steel);
            if (field == "A") j.targetA = target; if (field == "B") j.targetB = target; if (field == "C") j.targetC = target;
            if (field == "QueueA") j.targetQueueA = new List<LocalTargetInfo> { target };
            if (field == "QueueB") j.targetQueueB = new List<LocalTargetInfo> { target };
            Check(ManagedWorkCandidateFilter.Rejects(contender, j), field + " is checked for exact claim conflict");
        }
        foreach (string flag in new[] { "forced", "draft", "downed", "mental", "emergency", "unspawned", "saved-apparel", "saved-weapon" })
        {
            competing.playerForced = flag == "forced"; contender.Drafted = flag == "draft"; contender.Downed = flag == "downed";
            contender.InMentalState = flag == "mental"; contender.Spawned = flag != "unspawned";
            competing.def.defName = flag == "emergency" ? "Flee" : "ConstructDeliverResourcesToBlueprint";
            var state = new PawnApparelState { Transition = ApparelTransition.Restoring };
            if (flag == "saved-apparel") state.Apparel = competing;
            if (flag == "saved-weapon") state.Weapon = competing;
            AutomaticOutfitManagerGameComponent.Current.States[contender] = state;
            Check(!ManagedWorkCandidateFilter.Rejects(contender, competing), flag + " keeps native or exact saved-restoration handling");
        }
        contender.Spawned = true; contender.Drafted = contender.Downed = contender.InMentalState = false;
        competing.playerForced = false; AutomaticOutfitManagerGameComponent.Current.States.Clear();
        var otherMap = new Map(); contender.Map = otherMap;
        Job sameCellElsewhere = Work(Item(otherMap, 20, "Other cooler"), Item(otherMap, 10, "Other steel"));
        Check(!ManagedWorkCandidateFilter.Rejects(contender, sameCellElsewhere), "same coordinates on another map do not conflict");
        contender.Map = map;
        ManagedWorkClaimRegistry.Release(owner, prepared);
        Check(!ManagedWorkCandidateFilter.Rejects(contender, competing), "real claim release immediately makes material available without another cooldown");
        Check(ManagedWorkClaimRegistry.TryClaim(owner, prepared, 10), "short test claim established");
        Find.TickManager.TicksGame += 10;
        Check(!ManagedWorkCandidateFilter.Rejects(contender, competing), "expired claim cannot deny native work");
        ManagedWorkClaimRegistry.TryClaim(owner, prepared); owner.Map = otherMap;
        Check(!ManagedWorkCandidateFilter.Rejects(contender, competing), "owner leaving map releases stale preparation claim");
        owner.Map = map; ManagedWorkClaimRegistry.TryClaim(owner, prepared); steel.Destroyed = true;
        Check(!ManagedWorkCandidateFilter.Rejects(contender, competing), "destroyed material cannot retain a claim");
        steel.Destroyed = false; ManagedWorkClaimRegistry.TryClaim(owner, prepared);
        ManagedWorkClaimRegistry.ResetForLoadedGame();
        Check(!ManagedWorkCandidateFilter.Rejects(contender, competing), "load reset does not keep owners from another game");

        Job haul = new Job { def = JobDefOf.HaulToCell, targetA = new LocalTargetInfo(steel), targetB = new LocalTargetInfo(new IntVec3(80)) };
        ManagedWorkClaimRegistry.TryClaim(owner, haul);
        Job otherHaul = new Job { def = JobDefOf.HaulToCell, targetA = new LocalTargetInfo(Item(map, 70, "Other material")), targetB = haul.targetB };
        Check(ManagedWorkCandidateFilter.Rejects(contender, otherHaul), "exact haul destination cell remains exclusive during preparation");
        Check(!ManagedWorkClaimRegistry.TryClaim(contender, otherHaul), "failed all-target claim does not steal destination");
        Check(ManagedWorkClaimRegistry.HasActiveClaim(owner) && !ManagedWorkClaimRegistry.HasActiveClaim(contender), "failed contender leaves no partial claim");
    }
    public static int Main(string[] args)
    {
        try { TargetlessTests(args.Contains("--queue-first")); Tests(); Console.WriteLine("PASS " + passed + " prepared-work candidate checks"); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
namespace Verse
{
    public class Map { }
    public struct IntVec3
    {
        int value; public IntVec3(int v) { value = v; } public bool IsValid => value != -1000;
        public static IntVec3 Invalid => new IntVec3(-1000);
        public bool InBounds(Map m) => m != null && value >= 0 && value < 1000;
        public static bool operator ==(IntVec3 a, IntVec3 b) => a.value == b.value;
        public static bool operator !=(IntVec3 a, IntVec3 b) => !(a == b);
        public override bool Equals(object o) => o is IntVec3 c && c == this;
        public override int GetHashCode() => value; public override string ToString() => value.ToString();
    }
    public class Thing { public Map MapHeld; public IntVec3 PositionHeld; public string LabelCap; public bool Destroyed; }
    public class Pawn { public Map Map; public bool Spawned = true, Drafted, Downed, InMentalState; public string LabelShortCap; }
    public struct LocalTargetInfo
    {
        public Thing Thing; public IntVec3 Cell;
        public bool HasThing => Thing != null; public bool IsValid => HasThing || Cell.IsValid;
        public static LocalTargetInfo Invalid => new LocalTargetInfo(IntVec3.Invalid);
        public LocalTargetInfo(Thing t) { Thing = t; Cell = t.PositionHeld; }
        public LocalTargetInfo(IntVec3 c) { Thing = null; Cell = c; }
    }
    public static class Find { public static TickManager TickManager = new TickManager(); }
    public class TickManager { public int TicksGame; }
}
namespace Verse.AI
{
    public class JobDef { public string defName; }
    public class Job { public JobDef def; public bool playerForced; public LocalTargetInfo targetA = LocalTargetInfo.Invalid, targetB = LocalTargetInfo.Invalid, targetC = LocalTargetInfo.Invalid; public List<LocalTargetInfo> targetQueueA, targetQueueB; }
    public class ThinkNode { }
    public enum JobTag { Misc }
}
class QueueTracker
{
    public Queue<Job> Queue = new Queue<Job>();
    public void ClearQueuedJobs(bool unused) => Queue.Clear();
}
namespace RimWorld { public static class JobDefOf { public static JobDef HaulToCell = new JobDef { defName = "HaulToCell" }; } }
namespace AutomaticOutfitManager.State
{
    public enum ApparelTransition { Preparing, Active, Restoring }
    public class PawnApparelState { public ApparelTransition Transition; public Job Apparel, Weapon; }
}
namespace AutomaticOutfitManager.Core
{
    public class AutomaticOutfitManagerGameComponent
    {
        public static AutomaticOutfitManagerGameComponent Current = new AutomaticOutfitManagerGameComponent();
        public Dictionary<Pawn, PawnApparelState> States = new Dictionary<Pawn, PawnApparelState>();
        public PawnApparelState StateFor(Pawn p) => States.TryGetValue(p, out var s) ? s : null;
        public static Pawn ReleasedPawn; public static Job ReleasedJob;
        public static void ReleaseNativeReservations(Pawn p, Job j) { ReleasedPawn = p; ReleasedJob = j; }
    }
    public static class AomLog
    {
        public static bool DetailedEnabled = false; public static bool ShouldLogDetailed(Pawn p, string key, int ticks) => false;
        public static void Detailed(string message) { }
    }
}
namespace AutomaticOutfitManager.Patches
{
    public static class PawnJobTracker_StartJob_Patch
    {
        public static bool IsNativeEmergencySafetyJob(Job j) => j.def.defName == "Flee";
        public static bool IsAssignedTransitionApparelJob(PawnApparelState s, Job j) => s.Apparel == j;
        public static bool IsAssignedTransitionWeaponJob(PawnApparelState s, Job j) => s.Weapon == j;
    }
}
namespace AutomaticOutfitManager.Detection
{
    public static class TransitionActivityDiagnostics { public static void Rejected(Pawn p, Job j, string reason) { } }
    // The prepared-meal suite executes the real registry and this same extracted
    // admission boundary; this fixture isolates claim ownership and queue safety.
    internal static class PreparedIngestRetryRegistry { internal static void RejectAdmission(Pawn p, Job j) { } }
}
