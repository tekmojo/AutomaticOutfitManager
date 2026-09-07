using AutomaticOutfitManager.Detection;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    internal static class OutfitStepAdmissionDiagnostics_Patch
    {
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

        [HarmonyPriority(Priority.Last)]
        private static void Prefix(Pawn_JobTracker __instance, Job newJob, out TransitionActivityDiagnostics.Step __state) =>
            __state = TransitionActivityDiagnostics.CaptureStep(PawnField(__instance), newJob);

        private static void Postfix(Pawn_JobTracker __instance, TransitionActivityDiagnostics.Step __state) =>
            TransitionActivityDiagnostics.AfterStep(PawnField(__instance), __state, "StartJob returned");
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob),
        new[] { typeof(JobCondition), typeof(bool), typeof(bool) })]
    internal static class OutfitStepEndingDiagnostics_Patch
    {
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

        [HarmonyPriority(Priority.First)]
        private static void Prefix(Pawn_JobTracker __instance, out TransitionActivityDiagnostics.Step __state) =>
            __state = TransitionActivityDiagnostics.CaptureStep(PawnField(__instance), __instance.curJob);

        private static void Postfix(Pawn_JobTracker __instance, JobCondition condition, TransitionActivityDiagnostics.Step __state)
        {
            if (__state != null)
                TransitionActivityDiagnostics.AfterStep(PawnField(__instance), __state, "EndCurrentJob returned (" + condition + ")");
        }
    }
}
