using System.Collections.Generic;
using AutomaticOutfitManager.Patches;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    /// <summary>
    /// Bridges the native thinker boundary after departure restoration clears
    /// its session. It owns no Job or outfit and does not choose an exit route.
    /// </summary>
    internal static class NativeDepartureHandoff
    {
        private const int HandoffTicks = 600;
        private sealed class Entry
        {
            public Map Map;
            public object Duty;
            public int StartedTick;
        }
        private static readonly Dictionary<Pawn, Entry> Entries = new Dictionary<Pawn, Entry>();
        private static int CurrentTick => Find.TickManager?.TicksGame ?? 0;

        internal static void ResetForLoadedGame() => Entries.Clear();
        internal static void Clear(Pawn pawn)
        {
            if (pawn != null) Entries.Remove(pawn);
        }

        internal static void Restored(Pawn pawn)
        {
            if (pawn?.Spawned != true || pawn.Map == null) return;
            // Remove expired entries without retaining departed pawns/maps for
            // the rest of a long game. Calls are per departure, never per tick.
            var stale = new List<Pawn>();
            foreach (var pair in Entries)
                if (!Valid(pair.Key, pair.Value)) stale.Add(pair.Key);
            foreach (Pawn oldPawn in stale) Entries.Remove(oldPawn);
            Entries[pawn] = new Entry
            {
                Map = pawn.Map, Duty = pawn.mindState?.duty, StartedTick = CurrentTick
            };
        }

        // Called only for an actual new job proposal. Periodic queries must not
        // mistake a trailing Wear/Equip callback for a new activity and cancel.
        internal static bool BeforeJob(Pawn pawn, Job job, bool hasState)
        {
            bool allowed = !hasState && Allows(pawn, job);
            if (!allowed) Clear(pawn);
            else if (PawnJobTracker_StartJob_Patch.IsMapDepartureJob(job))
                Entries[pawn].StartedTick = CurrentTick;
            return allowed;
        }

        internal static bool Allows(Pawn pawn, Job job)
        {
            if (pawn == null || !Entries.TryGetValue(pawn, out Entry entry)) return false;
            if (!Valid(pawn, entry))
            {
                Clear(pawn);
                return false;
            }
            return job == null || (!job.playerForced &&
                (PawnJobTracker_StartJob_Patch.IsMapDepartureJob(job) || IsConnectiveWait(job)));
        }

        private static bool Valid(Pawn pawn, Entry entry) =>
            pawn?.Spawned == true && pawn.Map == entry.Map &&
            !pawn.Drafted && !pawn.Downed && !pawn.InMentalState &&
            ReferenceEquals(pawn.mindState?.duty, entry.Duty) &&
            CurrentTick >= entry.StartedTick && CurrentTick - entry.StartedTick < HandoffTicks;

        private static bool IsConnectiveWait(Job job) =>
            (job.def == JobDefOf.Wait || job.def == JobDefOf.Wait_MaintainPosture) &&
            !job.targetA.IsValid && !job.targetB.IsValid && !job.targetC.IsValid &&
            (job.targetQueueA == null || job.targetQueueA.Count == 0) &&
            (job.targetQueueB == null || job.targetQueueB.Count == 0);
    }
}
