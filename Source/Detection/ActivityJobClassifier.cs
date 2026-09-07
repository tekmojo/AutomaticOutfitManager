using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    internal static class ActivityJobClassifier
    {
        // Shared by access enforcement and the status rows. No faction or
        // custody special case: a guest/prisoner meal is still an activity.
        public static bool IsWandering(Pawn pawn, Job job, bool cleaningRobot,
            ThinkNode jobGiver = null)
        {
            if (job?.def == null || job.playerForced || pawn?.Drafted == true || IsHauling(job))
                return false;
            string name = job.def.defName ?? string.Empty;
            string giver = (jobGiver ?? job.jobGiver)?.GetType().Name ?? string.Empty;
            string driver = job.def.driverClass?.Name ?? string.Empty;
            if (cleaningRobot && (Contains(name, "Clean") || Contains(giver, "Clean") || Contains(driver, "Clean")))
                return true;
            if (job.workGiverDef != null || jobGiver is JobGiver_Work || job.jobGiver is JobGiver_Work)
                return false;
            // A native idle marker or a Wander-named thinker must not turn a
            // purposeful meal/rest/recreation job into idle movement.
            if (job.def.joyKind != null || name == "Ingest" || name == "LayDown" ||
                Contains(name, "GotoBed") || name == "Workwatching" ||
                IsPersonalActivityName(name) || IsPersonalActivityName(driver))
                return false;
            return job.def == JobDefOf.Goto || job.def == JobDefOf.Wait ||
                job.def == JobDefOf.Wait_MaintainPosture || job.def.isIdle ||
                Contains(name, "Wander") || Contains(giver, "Wander") ||
                IsObservedNonHumanIdle(pawn, job);
        }

        public static bool IsRobotBaseDuty(Pawn pawn, Job job) =>
            pawn?.RaceProps != null && !pawn.RaceProps.Humanlike &&
            job?.def != null && job.workGiverDef == null &&
            !(job.jobGiver is JobGiver_Work) &&
            (job.def.defName == "AIRobot_GoRecharge" ||
             job.def.defName == "AIRobot_GoDespawn" ||
             job.def.defName == "AIRobot_GoAndWait");

        private static bool Contains(string value, string part) =>
            value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsPersonalActivityName(string value) =>
            Contains(value, "Ingest") || Contains(value, "Joy") ||
            Contains(value, "Recreation") || Contains(value, "Relax") ||
            Contains(value, "Meditat") || Contains(value, "Watch") ||
            Contains(value, "Play") || Contains(value, "Read") || Contains(value, "Swim");

        public static bool IsHauling(Job job)
        {
            if (job?.def == null)
                return false;

            WorkTypeDef workType = job.workGiverDef?.workType;
            if (workType != null)
            {
                // Complex Jobs moves HaulGeneral/HaulCorpses and other storage
                // work into FSFHauling. Do not infer work type from a carrying
                // job's name: Warden and crafting jobs can use HaulToCell too.
                return workType == WorkTypeDefOf.Hauling ||
                       string.Equals(workType.defName, "FSFHauling",
                           StringComparison.Ordinal);
            }

            // Animal hauling and direct jobs can have no originating work giver.
            if (job.def == JobDefOf.HaulToCell ||
                job.def == JobDefOf.HaulToContainer)
                return true;

            Type driverClass = job.def.driverClass;
            return driverClass != null &&
                   driverClass.Name.IndexOf("Haul", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsObservedNonHumanIdle(Pawn pawn, Job job)
        {
            // Display only: returning to a bot's base or an animal's idle wait
            // is not an outfit/work session. Do not change access or native jobs.
            if (pawn?.RaceProps == null || pawn.RaceProps.Humanlike ||
                pawn.Drafted || job?.def == null || job.playerForced ||
                job.workGiverDef != null || job.jobGiver is JobGiver_Work)
                return false;

            return job.def == JobDefOf.Goto || job.def == JobDefOf.Wait ||
                   job.def == JobDefOf.Wait_MaintainPosture || job.def.isIdle ||
                   // Misc Robots' base/charging travel uses custom jobs with no
                   // isIdle marker. Exact identifiers avoid reclassifying its
                   // repair/deconstruction work or real hauling as wandering.
                   job.def.defName == "AIRobot_GoRecharge" ||
                   job.def.defName == "AIRobot_GoDespawn" ||
                   job.def.defName == "AIRobot_GoAndWait";
        }
    }
}
