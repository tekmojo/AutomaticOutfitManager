using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.State;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    internal static class ManagedWorkCandidateFilter
    {
        // A scanner's primary frame/workbench can be free while a queued steel
        // or component stack is held for someone else's outfit preparation.
        // Reject the complete candidate while native selection can still try
        // other work, rather than selecting it and replacing it with Wait.
        internal static bool Rejects(Pawn pawn, Job job)
        {
            if (!ManagedWorkClaimRegistry.HasClaims) return false;
            if (pawn?.Spawned != true || pawn.Drafted || pawn.Downed || pawn.InMentalState ||
                job?.def == null || job.playerForced ||
                PawnJobTracker_StartJob_Patch.IsNativeEmergencySafetyJob(job)) return false;
            var state = AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn);
            if (state?.Transition == ApparelTransition.Restoring &&
                (PawnJobTracker_StartJob_Patch.IsAssignedTransitionApparelJob(state, job) ||
                 PawnJobTracker_StartJob_Patch.IsAssignedTransitionWeaponJob(state, job))) return false;
            if (!ManagedWorkClaimRegistry.IsClaimedByOther(pawn, job)) return false;

            AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(pawn, job);
            TransitionActivityDiagnostics.Rejected(pawn, job, "prepared work target claimed by another pawn");
            LogConflict(pawn, job, "native work selection");
            return true;
        }

        internal static void LogConflict(Pawn pawn, Job job, string boundary)
        {
            if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(pawn,
                    $"prepared-work-contention:{job?.def?.defName}:{job?.targetA}", 600))
                AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                    $"deferred {job.def.defName} at {boundary}; " +
                    ManagedWorkClaimRegistry.DescribeConflict(pawn, job) + ".");
        }
    }
}
