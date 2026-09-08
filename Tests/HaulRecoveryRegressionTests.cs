using System;
using System.Collections.Generic;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using Verse;
using Verse.AI;

namespace Verse { public partial class RaceProperties { } }
namespace Verse.AI { public partial class Job { public int count; } }
namespace AutomaticOutfitManager.State
{
    public partial class PawnApparelState
    {
        public bool AutomaticIdleReturnRequested, RecallInterruptPending;
        public int LastRecallInterruptAttemptTick, LastChangingAreaReturnAttemptTick, NaturalLockerDwellUntilTick, ActiveIdleTicks;
        public IntVec3 ChangingAreaReturnCell;
    }
}
namespace AutomaticOutfitManager.Detection
{
    public static class ApparelCompatibility
    {
        public static string FindConflict(IEnumerable<ApparelRule> rules, object body, Pawn pawn) => null;
    }
}
namespace AutomaticOutfitManager.Patches
{
    internal static partial class PawnJobTracker_StartJob_Patch
    {
        internal static bool TryResumeHaul(Pawn p, PawnApparelState s, List<ApparelRule> rules, Job j) =>
            TryCancelAutomaticIdleReturnForProtectedJob(p, s, rules, j);
    }
}
partial class PausedHaulTests
{
    static void PreparedHaulFinalizerCases()
    {
        foreach (string kind in new[] { "HaulToCell", "HaulToContainer" })
        foreach (string activity in new[] { "washAtCell", "Ingest", "PlayChess" })
        {
            var p = Setup(out var s, out var r); var haul = Haul(kind);
            s.Transition = ApparelTransition.Active; s.PendingWorkJob = haul;
            PreparationJobHandoff.RecordPreparedActivity(p, s, haul);
            var child = PreparationHandoffTests.Job(activity); child.Targets = true;
            p.jobs.NativeFinalizer = child; p.jobs.StartJob(haul); Pulse(p, r);
            Check(p.jobs.curJob == haul && p.jobs.jobQueue.Count == 0 && !s.RecallRequested,
                "denied paused finalizer cannot displace prepared haul");
            Check(s.PendingWorkJob == haul, "finalizer rejection preserves exact pending haul");
        }
        foreach (string allowed in new[] { "outside", "forced", "care", "carried baby", "wait" })
        {
            var p = Setup(out var s, out var r); var haul = Haul();
            s.Transition = ApparelTransition.Active; s.PendingWorkJob = haul;
            PreparationJobHandoff.RecordPreparedActivity(p, s, haul);
            var child = allowed == "wait" ? PreparationHandoffTests.Wait() : PreparationHandoffTests.Job("washAtCell");
            child.Targets = allowed != "outside";
            child.playerForced = allowed == "forced";
            if (allowed == "care") child.targetA = new LocalTargetInfo(new Pawn());
            if (allowed == "carried baby") p.carryTracker.CarriedThing = new Pawn();
            if (allowed == "outside") p.Position = 2;
            p.jobs.NativeFinalizer = child; p.jobs.StartJob(haul);
            Check(p.jobs.curJob == child && p.jobs.jobQueue.Count == 1 && p.jobs.jobQueue[0].job == haul,
                "allowed native finalizer retains exact parent: " + allowed);
        }
    }
    static void HaulRecoveryCases()
    {
        PreparedHaulFinalizerCases();
        {
            var carrier = Setup(out var carryingState, out var pausedRule);
            var delivery = Haul(); delivery.count = 1;
            var savedVest = new ThingWithComps { Map = carrier.Map, Position = 1 };
            delivery.targetA = new LocalTargetInfo(savedVest);
            delivery.targetB = new LocalTargetInfo { Cell = 2 };
            carryingState.Transition = ApparelTransition.Active;
            carryingState.PendingWorkJob = null;
            carrier.jobs.curJob = delivery;
            carrier.carryTracker.CarriedThing = savedVest; savedVest.Spawned = false;
            delivery.count -= 1;
            Pulse(carrier, pausedRule);
            Check(!carryingState.RecallRequested, "native zero-count pickup preserves permitted haul during pause");
            carrier.Position = 2; savedVest.Position = 2; delivery.Targets = delivery.Crosses = false;
            Pulse(carrier, pausedRule);
            Check(!carryingState.RecallRequested && carrier.jobs.curJob == delivery,
                "paused recovery retains PPE and job after leaving protected source");
            carryingState.RecallRequested = true;
            Check(!PausedAreaWorkFilter.HasPermittedHaulingContext(carryingState, pausedRule), "carried recovery cannot override explicit recall");
            carryingState.RecallRequested = false; pausedRule.Hauling = false;
            Pulse(carrier, pausedRule);
            Check(carryingState.RecallRequested, "revoked hauling access remains effective after pickup");
        }
        var p = Setup(out var s, out var ship);
        var kitchen = new ApparelRule { Id = "kitchen", Area = ship.Area, WorkAreaPaused = false };
        AutomaticOutfitManagerGameComponent.Current.Rules.Add(kitchen);
        s.ActiveRuleId = kitchen.Id; s.Transition = ApparelTransition.ReturningToChangingArea;
        s.AutomaticIdleReturnRequested = s.RecallRequested = true;
        ship.MissingGear = true;
        var haul = Haul(); var rules = new List<ApparelRule> { kitchen, ship };
        // Native thinker repeats a rejected proposal while the same return is
        // pending. The return must never reopen with only Kitchen satisfied.
        for (int i = 0; i < 36; i++)
        {
            Check(!PawnJobTracker_StartJob_Patch.TryResumeHaul(p, s, rules, haul), "paused haul missing PPE cannot cancel automatic return");
            Check(s.RecallRequested && s.AutomaticIdleReturnRequested && s.Transition == ApparelTransition.ReturningToChangingArea,
                "rejected cross-area haul keeps original return intact");
        }
        ship.MissingGear = false; ship.Hauling = false;
        Check(!PawnJobTracker_StartJob_Patch.TryResumeHaul(p, s, rules, haul), "hauling revocation keeps return intact");
        ship.Hauling = true;
        Check(PawnJobTracker_StartJob_Patch.TryResumeHaul(p, s, rules, haul), "fully compliant permitted haul can retain outfit");
        Check(!s.RecallRequested && !s.AutomaticIdleReturnRequested && s.Transition == ApparelTransition.Active,
            "successful retention reopens one active session");
        s.Transition = ApparelTransition.ReturningToChangingArea; s.RecallRequested = true;
        Check(!PawnJobTracker_StartJob_Patch.TryResumeHaul(p, s, rules, haul), "explicit recall is not cancelled by a safe haul");

        // Real path node order is destination-to-start. An initial occupied
        // segment may exit once; unrelated entry and re-entry remain blocked.
        var restricted = new List<ApparelRule> { ship };
        Func<int[], int, bool, bool> avoids = (nodes, start, egress) =>
        {
            var cells = new List<IntVec3>(); foreach (int n in nodes) cells.Add(n);
            return ProtectedPathAvoidance.PathAvoidsRules(cells, start, p.Map, restricted, null, egress);
        };
        Check(avoids(new[] { 3, 2, 1 }, 1, true), "locker-bound egress does not request fresh area PPE");
        Check(!avoids(new[] { 3, 2, 1 }, 1, false), "saved gear and hazard routes keep strict area checks");
        Check(!avoids(new[] { 3, 1, 2, 1 }, 1, true), "egress never allows a later protected shortcut");
        Check(!avoids(new[] { 3, 1, 2 }, 2, true), "outside pickup and locker cannot cross protected area");
        Check(!avoids(new[] { 1, 1 }, 1, true), "remaining inside is not egress");
        Check(!avoids(new[] { 1, 2, 1 }, 1, true), "protected destination remains protected after exit");
        Check(avoids(new[] { 3, 2 }, 2, true), "fully outside locker route stays outside");
        Check(!ProtectedPathAvoidance.PathAvoidsRules(new List<IntVec3> { 3, 2, 1 }, 1,
            p.Map, restricted, c => c.X == 2, true), "egress does not bypass unsafe route predicates");
    }
}
