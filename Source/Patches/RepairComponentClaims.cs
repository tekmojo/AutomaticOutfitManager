using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using AutomaticOutfitManager.Detection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AutomaticOutfitManager.Patches
{
    [HarmonyPatch]
    internal static class RepairComponentSelectionScope_Patch
    {
        private static bool Prepare() => DeferredWorkScannerPatches.Ready;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            var signature = new[] { typeof(Pawn), typeof(Thing), typeof(bool) };
            yield return AccessTools.Method(typeof(WorkGiver_FixBrokenDownBuilding),
                nameof(WorkGiver_FixBrokenDownBuilding.HasJobOnThing), signature);
            yield return AccessTools.Method(typeof(WorkGiver_FixBrokenDownBuilding),
                nameof(WorkGiver_FixBrokenDownBuilding.JobOnThing), signature);
        }

        private static void Prefix(Pawn __0, bool __2,
            out RepairComponentClaims.Scope __state)
        {
            __state = RepairComponentClaims.Enter(__0, __2);
        }

        private static void Finalizer(RepairComponentClaims.Scope __state)
        {
            RepairComponentClaims.Exit(__state);
        }
    }

    [HarmonyPatch(typeof(WorkGiver_FixBrokenDownBuilding), "FindClosestComponent")]
    internal static class RepairComponentSearch_Patch
    {
        private static bool Prepare() => DeferredWorkScannerPatches.Ready;

        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var result = new List<CodeInstruction>();
            int wrapped = 0;
            MethodInfo wrap = AccessTools.Method(typeof(RepairComponentClaims),
                nameof(RepairComponentClaims.WrapValidator));
            foreach (CodeInstruction instruction in instructions)
            {
                result.Add(instruction);
                if (instruction.opcode == OpCodes.Newobj &&
                    instruction.operand is ConstructorInfo constructor &&
                    constructor.DeclaringType == typeof(Predicate<Thing>))
                {
                    result.Add(new CodeInstruction(OpCodes.Ldarg_1));
                    result.Add(new CodeInstruction(OpCodes.Call, wrap));
                    wrapped++;
                }
            }
            if (wrapped != 1)
                throw new InvalidOperationException(
                    "AOM repair component search expected one native candidate validator.");
            return result;
        }
    }
}
