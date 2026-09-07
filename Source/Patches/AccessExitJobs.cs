using System.Runtime.CompilerServices;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    // Ownership is exact and transient. Job objects are pooled, so reference
    // identity alone is insufficient; never grant an ordinary Goto this status.
    internal static class AccessExitJobs
    {
        private sealed class Entry
        {
            internal Pawn Pawn;
            internal Map Map;
            internal int LoadId;
            internal JobDef Def;
            internal LocalTargetInfo Target;
            internal int Tick;
        }

        private static ConditionalWeakTable<Job, Entry> entries =
            new ConditionalWeakTable<Job, Entry>();

        internal static void ResetForLoadedGame() =>
            entries = new ConditionalWeakTable<Job, Entry>();

        internal static void Mark(Pawn pawn, Job job)
        {
            if (pawn?.Map == null || job == null) return;
            entries.Remove(job);
            entries.Add(job, new Entry
            {
                Pawn = pawn, Map = pawn.Map, LoadId = job.loadID,
                Def = job.def, Target = job.targetA,
                Tick = Find.TickManager?.TicksGame ?? 0
            });
        }

        internal static bool IsOwned(Pawn pawn, Job job)
        {
            if (pawn == null || job == null || !entries.TryGetValue(job, out Entry entry))
                return false;
            int age = (Find.TickManager?.TicksGame ?? 0) - entry.Tick;
            return ReferenceEquals(entry.Pawn, pawn) && ReferenceEquals(entry.Map, pawn.Map) &&
                entry.LoadId == job.loadID && entry.Def == job.def &&
                entry.Target == job.targetA && age >= 0 && age <= 600;
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), "TryOpportunisticJob")]
    internal static class PawnJobTracker_AccessExitOpportunistic_Patch
    {
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

        // Both native Goto variants allow opportunistic hauling. Such a haul
        // can be denied by the same access rule and replaced with another exit
        // inside StartJob, recursively. An owned exit must go directly out.
        [HarmonyPriority(Priority.First)]
        internal static bool Prefix(Pawn_JobTracker __instance, Job __1, ref Job __result)
        {
            if (!AccessExitJobs.IsOwned(PawnField(__instance), __1)) return true;
            __result = null;
            return false;
        }

        [HarmonyPriority(Priority.Last)]
        internal static void Postfix(Pawn_JobTracker __instance, Job __1, ref Job __result)
        {
            if (AccessExitJobs.IsOwned(PawnField(__instance), __1)) __result = null;
        }
    }
}
