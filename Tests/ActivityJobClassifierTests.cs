// Compile the production classifier; API doubles do not simulate pathfinding.
using System;
using AutomaticOutfitManager.Detection;
using RimWorld;
using Verse;
using Verse.AI;

internal static class ActivityJobClassifierTests
{
    private static int passed;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        passed++;
        Console.WriteLine("PASS " + name);
    }

    public static int Main()
    {
        try
        {
            var storage = new Job { def = JobDefOf.HaulToCell };
            Check(ActivityJobClassifier.IsHauling(storage), "animal storage haul without a work giver");
            storage.def = JobDefOf.HaulToContainer;
            Check(ActivityJobClassifier.IsHauling(storage), "direct container haul without a work giver");
            storage.def = JobDefOf.HaulToCell;
            storage.workGiverDef = new WorkGiverDef { defName = "HaulGeneral", workType = WorkTypeDefOf.Hauling };
            Check(ActivityJobClassifier.IsHauling(storage), "vanilla robot HaulGeneral");
            storage.workGiverDef.workType = new WorkTypeDef { defName = "FSFHauling" };
            Check(ActivityJobClassifier.IsHauling(storage), "Complex Jobs robot HaulGeneral");
            storage.workGiverDef.defName = "HaulCorpses";
            Check(ActivityJobClassifier.IsHauling(storage), "Complex Jobs robot HaulCorpses");
            storage.def = new JobDef { defName = "DeliverResources", driverClass = typeof(JobDriver_Deliver) };
            Check(ActivityJobClassifier.IsHauling(storage), "FSF hauling classification does not depend on Haul in the job name");
            storage.def = JobDefOf.HaulToCell;
            storage.workGiverDef = new WorkGiverDef { defName = "WardenHaul", workType = new WorkTypeDef { defName = "Warden" } };
            Check(!ActivityJobClassifier.IsHauling(storage), "Warden carry job remains ordinary work despite hauling names");
            storage.workGiverDef = new WorkGiverDef { defName = "DoBillsMilitarumFabricationBench", workType = new WorkTypeDef { defName = "Crafting" } };
            Check(!ActivityJobClassifier.IsHauling(storage), "saved crafting HaulToCell remains ordinary work");
            storage.workGiverDef.workType = new WorkTypeDef { defName = "UnknownHaulingWork" };
            Check(!ActivityJobClassifier.IsHauling(storage), "unknown work type is not guessed from its name");
            storage.workGiverDef = null;
            storage.def = new JobDef { driverClass = typeof(JobDriver_CustomHaul) };
            Check(ActivityJobClassifier.IsHauling(storage), "untyped mod hauling driver retains compatibility");
            storage.def = new JobDef { driverClass = typeof(JobDriver_Deliver) };
            Check(!ActivityJobClassifier.IsHauling(storage), "untyped non-hauling driver stays ordinary activity");
            Check(!ActivityJobClassifier.IsHauling(null), "missing job is not hauling");
            Check(!ActivityJobClassifier.IsHauling(new Job()), "missing definition is not hauling");

            var animal = new Pawn { RaceProps = new RaceProperties() };
            var robot = new Pawn { RaceProps = new RaceProperties() };
            var human = new Pawn { RaceProps = new RaceProperties { Humanlike = true } };
            var idle = new Job { def = JobDefOf.Goto };
            Check(ActivityJobClassifier.IsObservedNonHumanIdle(robot, idle), "autonomous robot return-to-base movement is an idle activity");
            Check(ActivityJobClassifier.IsObservedNonHumanIdle(animal, idle), "autonomous animal connective movement is an idle activity");
            Check(!ActivityJobClassifier.IsObservedNonHumanIdle(human, idle), "colonist movement is not reclassified");
            idle.def = JobDefOf.Wait;
            Check(ActivityJobClassifier.IsObservedNonHumanIdle(robot, idle), "robot base waiting is an idle activity");
            idle.def = JobDefOf.Wait_MaintainPosture;
            Check(ActivityJobClassifier.IsObservedNonHumanIdle(animal, idle), "animal posture wait is an idle activity");
            idle.def = new JobDef { isIdle = true };
            Check(ActivityJobClassifier.IsObservedNonHumanIdle(robot, idle), "native idle marker supports mod jobs");
            idle.def = JobDefOf.Goto;
            idle.playerForced = true;
            Check(!ActivityJobClassifier.IsObservedNonHumanIdle(robot, idle), "forced robot movement remains explicitly ordered activity");
            idle.playerForced = false;
            robot.Drafted = true;
            Check(!ActivityJobClassifier.IsObservedNonHumanIdle(robot, idle), "drafted robot movement is not called idle");
            robot.Drafted = false;
            idle.workGiverDef = new WorkGiverDef();
            Check(!ActivityJobClassifier.IsObservedNonHumanIdle(robot, idle), "work-giver movement remains work");
            idle.workGiverDef = null;
            idle.jobGiver = new JobGiver_Work();
            Check(!ActivityJobClassifier.IsObservedNonHumanIdle(robot, idle), "work-think-node movement remains work");
            idle.jobGiver = null;
            idle.def = new JobDef { defName = "Ingest" };
            Check(!ActivityJobClassifier.IsObservedNonHumanIdle(animal, idle), "animal eating remains occupant activity");
            idle.def = new JobDef { defName = "LayDown" };
            Check(!ActivityJobClassifier.IsObservedNonHumanIdle(animal, idle), "animal sleep remains occupant activity");
            Check(!ActivityJobClassifier.IsObservedNonHumanIdle(robot, null), "missing job cannot invent an idle activity");
            foreach (string name in new[] { "AIRobot_GoRecharge", "AIRobot_GoDespawn", "AIRobot_GoAndWait" })
            {
                var baseTrip = new Job { def = new JobDef { defName = name } };
                Check(ActivityJobClassifier.IsObservedNonHumanIdle(robot, baseTrip), name + " is observed wandering without a native idle flag");
                Check(!ActivityJobClassifier.IsHauling(baseTrip), name + " is not a stock delivery");
                Check(!ActivityJobClassifier.IsObservedNonHumanIdle(human, baseTrip), name + " does not reclassify a human");
                baseTrip.playerForced = true;
                Check(!ActivityJobClassifier.IsObservedNonHumanIdle(robot, baseTrip), name + " preserves explicit-order classification");
                baseTrip.playerForced = false;
                baseTrip.workGiverDef = new WorkGiverDef { workType = WorkTypeDefOf.Hauling };
                Check(ActivityJobClassifier.IsHauling(baseTrip) && !ActivityJobClassifier.IsObservedNonHumanIdle(robot, baseTrip), name + " gives actual hauling priority");
            }
            Check(!ActivityJobClassifier.IsObservedNonHumanIdle(robot, new Job { def = new JobDef { defName = "AIRobot_RepairStationRobot" } }), "station repair is not base travel");
            Check(!ActivityJobClassifier.IsObservedNonHumanIdle(robot, new Job { def = new JobDef { defName = "AIRobot_DeconstructDamagedRobot" } }), "robot deconstruction is not base travel");
            // Same humanlike classifier for every social/custody group. The
            // production classifier deliberately accepts no faction privilege.
            foreach (string group in new[] { "colonist", "guest", "slave", "prisoner", "child" })
            {
                foreach (string activity in new[] { "Ingest", "LayDown", "GotoBed", "Workwatching", "Play_Roulette", "Meditate", "GoSwimming", "ReadBook", "TakeInventory" })
                {
                    var job = new Job { def = new JobDef { defName = activity } };
                    Check(!ActivityJobClassifier.IsWandering(human, job, false), group + " " + activity + " uses Activities");
                }
                foreach (var definition in new[] { JobDefOf.Goto, JobDefOf.Wait, JobDefOf.Wait_MaintainPosture, new JobDef { defName = "GotoWander" } })
                    Check(ActivityJobClassifier.IsWandering(human, new Job { def = definition }, false), group + " " + definition.defName + " uses Wandering");
            }
            var recreation = new Job { def = new JobDef { defName = "ModdedRecreation", joyKind = new object(), isIdle = true } };
            Check(!ActivityJobClassifier.IsWandering(human, recreation, false), "recreation wins over misleading idle marker");
            var workMove = new Job { def = JobDefOf.Goto, workGiverDef = new WorkGiverDef { workType = new WorkTypeDef { defName = "Construction" } } };
            Check(!ActivityJobClassifier.IsWandering(human, workMove, false), "movement with assigned work context remains Activities");
            workMove.workGiverDef = null;
            Check(!ActivityJobClassifier.IsWandering(human, workMove, false, new JobGiver_Work()), "selection-time work thinker context survives before StartJob");
            workMove.jobGiver = new JobGiver_Work();
            Check(!ActivityJobClassifier.IsWandering(human, workMove, false), "saved work thinker context remains Activities");
            var clean = new Job { def = new JobDef { defName = "Clean" }, workGiverDef = new WorkGiverDef { workType = new WorkTypeDef { defName = "Cleaning" } } };
            Check(ActivityJobClassifier.IsWandering(robot, clean, true), "robot cleaning keeps established Wandering permission");
            Check(!ActivityJobClassifier.IsWandering(human, clean, false), "human cleaning remains Activities");
            clean.workGiverDef.workType = WorkTypeDefOf.Hauling;
            Check(!ActivityJobClassifier.IsWandering(robot, clean, true) && ActivityJobClassifier.IsHauling(clean), "real hauling takes precedence over robot Clean name");
            clean.playerForced = true;
            Check(!ActivityJobClassifier.IsWandering(robot, clean, true), "direct robot order is not idle wandering");
            foreach (string duty in new[] { "AIRobot_GoRecharge", "AIRobot_GoDespawn", "AIRobot_GoAndWait" })
            {
                var job = new Job { def = new JobDef { defName = duty } };
                Check(ActivityJobClassifier.IsRobotBaseDuty(robot, job), duty + " native base service recognized");
                Check(!ActivityJobClassifier.IsRobotBaseDuty(human, job), duty + " cannot grant human exception");
                job.workGiverDef = new WorkGiverDef { workType = WorkTypeDefOf.Hauling };
                Check(!ActivityJobClassifier.IsRobotBaseDuty(robot, job), duty + " cannot bypass actual hauling control");
            }
            Console.WriteLine("Passed " + passed + " activity checks.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL " + exception);
            return 1;
        }
    }
}

namespace Verse
{
    public class Pawn { public RaceProperties RaceProps; public bool Drafted; }
    public class RaceProperties { public bool Humanlike; }
    public class JobDef { public string defName; public Type driverClass; public bool isIdle; public object joyKind; }
}
namespace Verse.AI
{
    public class ThinkNode { }
    public class Job
    {
        public JobDef def;
        public WorkGiverDef workGiverDef;
        public ThinkNode jobGiver;
        public bool playerForced;
    }
    public class JobDriver_CustomHaul { }
    public class JobDriver_Deliver { }
}
namespace RimWorld
{
    public class WorkTypeDef { public string defName; }
    public class WorkGiverDef { public string defName; public WorkTypeDef workType; }
    public class JobGiver_Work : ThinkNode { }
    public static class WorkTypeDefOf { public static readonly WorkTypeDef Hauling = new WorkTypeDef { defName = "Hauling" }; }
    public static class JobDefOf
    {
        public static readonly JobDef HaulToCell = new JobDef { defName = "HaulToCell", driverClass = typeof(JobDriver_CustomHaul) };
        public static readonly JobDef HaulToContainer = new JobDef { defName = "HaulToContainer" };
        public static readonly JobDef Goto = new JobDef { defName = "Goto" };
        public static readonly JobDef Wait = new JobDef { defName = "Wait" };
        public static readonly JobDef Wait_MaintainPosture = new JobDef { defName = "Wait_MaintainPosture" };
    }
}
