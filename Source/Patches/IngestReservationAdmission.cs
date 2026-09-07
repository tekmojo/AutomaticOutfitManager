using AutomaticOutfitManager.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    internal static class IngestReservationAdmission
    {
        internal static bool Rejects(Pawn pawn, Job job)
        {
            if (RejectsHospitality(pawn, job)) return true;
            // Match the native Ingest pre-toil reservation, including the actual
            // pickup amount. Do not change another driver's reservation contract,
            // explicit orders, carried meals or nutrient paste dispensers.
            if (pawn?.Map == null || pawn.Faction == null || job?.playerForced == true ||
                job?.def != JobDefOf.Ingest || job.def.driverClass != typeof(JobDriver_Ingest) ||
                job.targetA.Thing is not Thing food || !food.Spawned || food.Map != pawn.Map ||
                food.Destroyed || food.def?.ingestible == null || food is Building_NutrientPasteDispenser)
                return false;
            int count = FoodUtility.GetMaxAmountToPickup(food, pawn, job.count);
            if (count <= 0 || pawn.CanReserve(food, 10, count)) return false;
            if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(pawn, $"ingest-reservation:{food.thingIDNumber}", 600))
                AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: skipped contested food {food.LabelCap} " +
                    $"(pickup {count}); native ingestion cannot reserve it. Existing reservations retained.");
            return true;
        }
        private static bool RejectsHospitality(Pawn pawn, Job job)
        {
            // Hospitality 1.6 ScroungeFood reserves target B with maxPawns=1
            // and job.count. Food can still be in its donor's inventory.
            // Never apply the native Ingest pickup calculation to this driver.
            if (pawn?.Map == null || pawn.Faction == null || job?.playerForced == true ||
                job?.def?.defName != "ScroungeFood" ||
                job.def.driverClass?.FullName != "Hospitality.JobDriver_ScroungeFood" ||
                job.targetB.Thing is not Thing food || food.Destroyed ||
                food.MapHeld != pawn.Map || food.def?.ingestible == null || job.count <= 0 ||
                pawn.CanReserve(food, 1, job.count)) return false;
            if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(pawn, $"hospitality-food-reservation:{food.thingIDNumber}", 600))
                AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: skipped contested Hospitality food {food.LabelCap} " +
                    $"(requested {job.count}); existing reservation retained. Native task selection resumes after a brief wait.");
            return true;
        }
    }
}
