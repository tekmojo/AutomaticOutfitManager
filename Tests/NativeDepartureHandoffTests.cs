using System;
using System.Collections.Generic;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using RimWorld;
using Verse;
using Verse.AI;

class NativeDepartureHandoffTests
{
    static int passed;
    static void Check(bool value, string message) { if (!value) throw new Exception("ASSERT: " + message); passed++; }
    static Pawn Pawn() => new Pawn { Map = new Map(), mindState = new MindState { duty = new object() } };
    static Job Wait() => new Job { def = JobDefOf.Wait_MaintainPosture };
    static Job Exit() => new Job { def = JobDefOf.Goto, targetA = new LocalTargetInfo(0), exitMapOnArrival = true };
    static void Reset() { NativeDepartureHandoff.ResetForLoadedGame(); Find.TickManager.TicksGame = 100; AutomaticOutfitManagerGameComponent.Current = new AutomaticOutfitManagerGameComponent(); }
    static void Complete(Pawn pawn, bool departure = true)
    {
        var component = AutomaticOutfitManagerGameComponent.Current;
        component.PawnStates.Add(new PawnApparelState { Pawn = pawn, MapDepartureRequested = departure, Complete = true });
    }
    static void Tests()
    {
        Reset(); Pawn pawn = Pawn(); var component = AutomaticOutfitManagerGameComponent.Current;
        Complete(pawn);
        // Native final Wear completion leads synchronously into StartJob with
        // a connective Wait. Production normalization clears the satisfied
        // session before testing the new proposal. Then the native body owns it.
        Job wait = Wait(); Job current = null;
        PawnJobTracker_StartJob_Patch.Admit(pawn, ref wait);
        current = wait;
        Check(component.StateFor(pawn) == null, "completed departure snapshot clears before the native follow-up");
        Check(component.Preparations == 0, "restored departure wait must not re-equip work outfits");
        Check(DepartureRuntimeFixture.Allows(pawn, current), "periodic occupancy admits the same post-restoration wait");
        Check(NativeDepartureHandoff.Allows(pawn, null), "empty native tracker keeps the short departure handoff");
        Find.TickManager.TicksGame += 60;
        Job exit = Exit(); PawnJobTracker_StartJob_Patch.Admit(pawn, ref exit); current = exit;
        Check(current.exitMapOnArrival && component.Preparations == 0, "native thinker retries its own exit route without another outfit change");
        Check(DepartureRuntimeFixture.Allows(pawn, current), "native exit remains exempt from occupancy after state clears");
        NativeDepartureHandoff.Clear(pawn);
        Check(!NativeDepartureHandoff.Allows(pawn, Wait()), "completed exit cannot leave a stale wait exemption");

        Reset(); pawn = Pawn(); component = AutomaticOutfitManagerGameComponent.Current;
        Complete(pawn, false); wait = Wait(); PawnJobTracker_StartJob_Patch.Admit(pawn, ref wait);
        Check(component.Preparations == 1, "ordinary restoration keeps normal protected-area checks");
        Reset(); pawn = Pawn(); component = AutomaticOutfitManagerGameComponent.Current;
        component.PawnStates.Add(new PawnApparelState { Pawn = pawn, MapDepartureRequested = true, Complete = false });
        wait = Wait(); PawnJobTracker_StartJob_Patch.Admit(pawn, ref wait);
        Check(component.StateFor(pawn) != null && !NativeDepartureHandoff.Allows(pawn, wait), "incomplete restoration still owns and must return its gear");

        foreach (string kind in new[] { "work", "meal", "wander", "goto", "forced", "targetA", "targetB", "targetC", "queueA", "queueB" })
        {
            Reset(); pawn = Pawn(); NativeDepartureHandoff.Restored(pawn); Job job = Wait();
            if (kind == "work") job.def = new JobDef { defName = "Repair" };
            if (kind == "meal") job.def = new JobDef { defName = "Ingest" };
            if (kind == "wander") job.def = new JobDef { defName = "Wait_Wander" };
            if (kind == "goto") job.def = JobDefOf.Goto;
            if (kind == "forced") job.playerForced = true;
            if (kind == "targetA") job.targetA = new LocalTargetInfo(0);
            if (kind == "targetB") job.targetB = new LocalTargetInfo(4);
            if (kind == "targetC") job.targetC = new LocalTargetInfo(5);
            if (kind == "queueA") job.targetQueueA = new List<LocalTargetInfo> { new LocalTargetInfo(6) };
            if (kind == "queueB") job.targetQueueB = new List<LocalTargetInfo> { new LocalTargetInfo(7) };
            Check(!NativeDepartureHandoff.BeforeJob(pawn, job, false), kind + " receives ordinary admission checks");
            Check(!NativeDepartureHandoff.Allows(pawn, Wait()), kind + " cancels departure handoff for later waits");
        }
        foreach (string change in new[] { "duty", "map", "draft", "downed", "mental", "despawn", "rewind", "timeout", "load", "state" })
        {
            Reset(); pawn = Pawn(); NativeDepartureHandoff.Restored(pawn);
            if (change == "duty") pawn.mindState.duty = new object();
            if (change == "map") pawn.Map = new Map();
            if (change == "draft") pawn.Drafted = true;
            if (change == "downed") pawn.Downed = true;
            if (change == "mental") pawn.InMentalState = true;
            if (change == "despawn") pawn.Spawned = false;
            if (change == "rewind") Find.TickManager.TicksGame = 99;
            if (change == "timeout") Find.TickManager.TicksGame += 600;
            if (change == "load") NativeDepartureHandoff.ResetForLoadedGame();
            Check(!NativeDepartureHandoff.BeforeJob(pawn, Wait(), change == "state"), change + " ends the temporary handoff");
        }
        Reset(); pawn = Pawn(); pawn.mindState.duty = null; NativeDepartureHandoff.Restored(pawn);
        Check(NativeDepartureHandoff.BeforeJob(pawn, new Job { def = JobDefOf.Wait }, false), "modded departure without a duty gets the bounded native wait handoff");
        Find.TickManager.TicksGame += 599;
        Check(NativeDepartureHandoff.BeforeJob(pawn, Wait(), false), "wait is admitted within the original handoff window");
        Find.TickManager.TicksGame++;
        Check(!NativeDepartureHandoff.Allows(pawn, Wait()), "repeated waits cannot renew the handoff indefinitely");
        Reset(); pawn = Pawn(); NativeDepartureHandoff.Restored(pawn);
        Find.TickManager.TicksGame += 590;
        Check(NativeDepartureHandoff.BeforeJob(pawn, Exit(), false), "confirmed native exit retry refreshes the short window");
        Find.TickManager.TicksGame += 20;
        Check(NativeDepartureHandoff.Allows(pawn, Wait()), "brief native recovery after a confirmed exit remains protected");
        Check(!NativeDepartureHandoff.Allows(Pawn(), Wait()), "another pawn cannot borrow departure intent");
        Check(!NativeDepartureHandoff.Allows(pawn, new Job { def = JobDefOf.Wear }), "trailing outfit callback is not admitted as new departure work");
        Check(NativeDepartureHandoff.Allows(pawn, Wait()), "passive callback query cannot cancel the next native wait");
    }
    public static int Main()
    {
        try { Tests(); Console.WriteLine("PASS " + passed + " departure handoff checks"); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
namespace Verse
{
    public class Map { }
    public class MindState { public object duty; }
    public class Pawn { public Map Map; public bool Spawned = true, Drafted, Downed, InMentalState; public MindState mindState; }
    public struct LocalTargetInfo
    {
        int cell; public LocalTargetInfo(int value) { cell = value; }
        public bool IsValid => cell != -1000;
        public static LocalTargetInfo Invalid => new LocalTargetInfo(-1000);
    }
    public static class Find { public static TickManager TickManager = new TickManager(); }
    public class TickManager { public int TicksGame; }
}
namespace Verse.AI
{
    public class JobDef { public string defName; }
    public class Job
    {
        public JobDef def;
        public bool playerForced, exitMapOnArrival;
        public LocalTargetInfo targetA = LocalTargetInfo.Invalid, targetB = LocalTargetInfo.Invalid, targetC = LocalTargetInfo.Invalid;
        public List<LocalTargetInfo> targetQueueA, targetQueueB;
    }
}
namespace RimWorld
{
    public static class JobDefOf
    {
        public static JobDef Wait = new JobDef { defName = "Wait" }, Wait_MaintainPosture = new JobDef { defName = "Wait_MaintainPosture" },
            Goto = new JobDef { defName = "Goto" }, Wear = new JobDef { defName = "Wear" };
    }
}
namespace AutomaticOutfitManager.Core
{
    public class PawnApparelState { public Pawn Pawn; public bool MapDepartureRequested, Complete; }
    public partial class AutomaticOutfitManagerGameComponent
    {
        public static AutomaticOutfitManagerGameComponent Current = new AutomaticOutfitManagerGameComponent();
        public List<PawnApparelState> PawnStates = new List<PawnApparelState>();
        public int Preparations;
        public PawnApparelState StateFor(Pawn pawn) => PawnStates.Find(state => state.Pawn == pawn);
        public bool TryCompleteSatisfiedRestoration(Pawn pawn, PawnApparelState state)
        {
            if (state?.Complete != true) return false;
            EndIntervention(pawn, state);
            return true;
        }
    }
}
