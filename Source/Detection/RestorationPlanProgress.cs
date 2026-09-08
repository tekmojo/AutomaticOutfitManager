using System.Runtime.CompilerServices;
using AutomaticOutfitManager.State;

namespace AutomaticOutfitManager.Detection
{
    // Runtime-only probe bookkeeping, never a cached Job or a saved continuation.
    // Periodically recheck availability, but restart only when there is a step
    // to execute. A real item release or successful step clears this throttle.
    internal static class RestorationPlanProgress
    {
        private sealed class Probe
        {
            internal int Tick;
            internal bool EmptyAndBlocked;
        }
        private static readonly ConditionalWeakTable<PawnApparelState, Probe> Probes =
            new ConditionalWeakTable<PawnApparelState, Probe>();

        internal static bool CanProbe(PawnApparelState state, int tick) => state != null &&
            (!Probes.TryGetValue(state, out Probe probe) || tick < probe.Tick || tick - probe.Tick >= 120);

        internal static bool Observe(PawnApparelState state, int tick, int jobCount, bool unavailable)
        {
            if (state == null) return false;
            Probe probe = Probes.GetValue(state, _ => new Probe());
            bool becameReady = probe.EmptyAndBlocked && jobCount > 0;
            probe.Tick = tick;
            probe.EmptyAndBlocked = jobCount == 0 && unavailable;
            return becameReady;
        }

        internal static bool IsEmptyAndBlocked(PawnApparelState state) =>
            state != null && Probes.TryGetValue(state, out Probe probe) && probe.EmptyAndBlocked;

        internal static bool ShouldRestart(int jobCount) => jobCount > 0;

        internal static void Wake(PawnApparelState state)
        {
            if (state != null) Probes.Remove(state);
        }
    }
}
