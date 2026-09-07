using System;
using AutomaticOutfitManager.Core;
using HarmonyLib;
using Verse;

namespace AutomaticOutfitManager.Patches
{
    // Keep this gate separate from the startup class: reading it from Prepare
    // during constructor-time PatchAll must not trigger installation itself.
    internal static class DeferredWorkScannerPatches
    {
        internal static bool Ready { get; private set; }

        internal static void Install()
        {
            if (Ready) return;
            Ready = true;
            var harmony = new Harmony(AutomaticOutfitManagerMod.HarmonyId);
            foreach (Type patch in new[]
            {
                typeof(WorkGiverPausedArea_HasJobThing_Patch),
                typeof(WorkGiverPausedArea_HasJobCell_Patch),
                typeof(WorkGiverPausedArea_HasJobFallback_Patch),
                typeof(WorkGiverPausedArea_JobOnThing_Patch),
                typeof(WorkGiverPausedArea_JobOnCell_Patch),
                typeof(WorkGiverPausedArea_JobOnFallback_Patch),
                typeof(ThinkNodeJobGiver_ProtectedArea_Patch)
            })
                harmony.CreateClassProcessor(patch).Patch();
        }
    }

    [StaticConstructorOnStartup]
    internal static class WorkScannerPatchStartup
    {
        // RimWorld runs these after language/defs initialize, before gameplay.
        // Detouring mod overrides can run their eager static translation code.
        static WorkScannerPatchStartup() => DeferredWorkScannerPatches.Install();
    }
}
