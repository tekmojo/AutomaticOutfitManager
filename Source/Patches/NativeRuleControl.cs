using AutomaticOutfitManager.Detection;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    // Civilian rule enforcement cannot require an outfit change while native
    // mental, incapacitated or emergency control prevents that change.
    internal static class NativeRuleControl
    {
        internal static bool MentalOrEmergency(Pawn pawn, Job job) =>
            PawnJobTracker_StartJob_Patch.IsNativeMentalActivity(pawn, job) ||
            PawnJobTracker_StartJob_Patch.IsNativeEmergencySafetyJob(job);

        internal static bool Suspends(Pawn pawn, Job job) =>
            pawn == null || pawn.Dead || pawn.Downed || pawn.Drafted ||
            MentalOrEmergency(pawn, job) ||
            PawnAccessClassifier.IsNativeCustodyEscapeActive(pawn);
    }
}
