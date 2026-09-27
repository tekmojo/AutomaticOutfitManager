using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using AutomaticOutfitManager.Detection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutomaticOutfitManager.Patches
{
    // Unlike material delivery, floor removal has a separate native eligibility
    // branch that returns true without generating a Job. Its cell claim must
    // agree with the complete-job filter used later by JobOnThing.
    [HarmonyPatch(typeof(WorkGiver_ConstructDeliverResourcesToBlueprints),
        nameof(WorkGiver_ConstructDeliverResourcesToBlueprints.HasJobOnThing))]
    internal static class ConstructionFloorClaims_Patch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var eligibility = AccessTools.Method(typeof(WorkGiver_ConstructDeliverResourcesToBlueprints),
                "CanDoRemoveExistingFloorWork");
            var allow = AccessTools.Method(typeof(ConstructionFloorClaims_Patch), nameof(AllowsFloorRemoval));
            int matches = 0;
            for (int i = 0; i + 3 < code.Count; i++)
            {
                if (!code[i].Calls(eligibility) ||
                    (code[i + 1].opcode != OpCodes.Brfalse && code[i + 1].opcode != OpCodes.Brfalse_S) ||
                    code[i + 2].opcode != OpCodes.Ldc_I4_1 || code[i + 3].opcode != OpCodes.Ret)
                    continue;

                // Only replace the successful floor-prerequisite return. A
                // blocker job and the material/no-cost branches stay native.
                // Read the original Thing argument instead of relying on a
                // compiler-specific local index for the Blueprint.
                var loadPawn = new CodeInstruction(OpCodes.Ldarg_1);
                loadPawn.labels.AddRange(code[i + 2].labels);
                loadPawn.blocks.AddRange(code[i + 2].blocks);
                code.RemoveAt(i + 2);
                code.InsertRange(i + 2, new[] {
                    loadPawn,
                    new CodeInstruction(OpCodes.Ldarg_2),
                    new CodeInstruction(OpCodes.Ldarg_3),
                    new CodeInstruction(OpCodes.Call, allow)
                });
                matches++;
            }
            if (matches != 1)
                throw new InvalidOperationException("AOM: expected one native blueprint floor-removal eligibility return.");
            return code;
        }

        private static bool AllowsFloorRemoval(Pawn pawn, Thing blueprint, bool forced)
        {
            if (!ManagedWorkClaimRegistry.HasClaims || forced || pawn?.Spawned != true ||
                pawn.Drafted || pawn.Downed || pawn.InMentalState)
                return true;

            return !ManagedWorkClaimRegistry.IsClaimedByOther(
                pawn, pawn.Map, null, blueprint.Position);
        }
    }
}
