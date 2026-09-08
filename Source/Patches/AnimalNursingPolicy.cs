using AutomaticOutfitManager.Rules;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    // Zoology's mother/young jobs form one feeding activity. Keep both sides
    // consistent at selection, occupancy and path entry when a rule is paused.
    // This is a pause exception, never a general animal-access override.
    internal static class AnimalNursingPolicy
    {
        internal static bool Allowed(Pawn pawn, Job job, ApparelRule rule) =>
            IsNursing(pawn, job) && rule?.Enabled == true && rule.Area?.Map == pawn.Map &&
            PausedAreaWorkFilter.WorkAllowedFor(rule, pawn);

        private static bool IsNursing(Pawn pawn, Job job)
        {
            if (!LiveAnimal(pawn, pawn?.Map) || job?.def == null) return false;
            string name = job.def.defName;
            string driver = job.def.driverClass?.FullName;
            if (name == "Zoology_Breastfeed" && driver == "ZoologyMod.JobDriver_AnimalBreastfeed")
                return job.targetB.Thing == pawn && job.targetA.Thing is Pawn young &&
                    young != pawn && LiveAnimal(young, pawn.Map);

            if (name != "Zoology_YoungSuckle" || driver != "ZoologyMod.JobDriver_YoungSuckle")
                return false;
            // The young's own food giver targets the mother; the mother's
            // driver starts a targetless suckling job once they are together.
            // A targetless job receives no unrelated path-entry exemption.
            return !job.targetA.IsValid || (job.targetA.Thing is Pawn mother &&
                mother != pawn && LiveAnimal(mother, pawn.Map));
        }

        private static bool LiveAnimal(Pawn pawn, Map map) =>
            map != null && pawn?.Spawned == true && !pawn.Dead &&
            pawn.Map == map && pawn.RaceProps?.Animal == true;
    }
}
