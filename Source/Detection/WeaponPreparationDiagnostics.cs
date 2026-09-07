using System.Runtime.CompilerServices;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    internal static class WeaponPreparationDiagnostics
    {
        private sealed class EndRecord { internal int LoadId; }
        private static ConditionalWeakTable<Job, EndRecord> endings =
            new ConditionalWeakTable<Job, EndRecord>();

        internal static void ResetForLoadedGame() =>
            endings = new ConditionalWeakTable<Job, EndRecord>();

        private static bool IsPreparation(PawnApparelState state, Job job) =>
            state?.Transition == ApparelTransition.Preparing && job?.def == JobDefOf.Equip &&
            job.targetA.Thing is ThingWithComps weapon && state.IsManagedWeapon(weapon);

        private static bool IsRestoration(PawnApparelState state, Job job) =>
            state?.Transition == ApparelTransition.Restoring && job?.def == JobDefOf.Equip &&
            !job.playerForced && state.WeaponRestorationRequested &&
            job.targetA.Thing != null && job.targetA.Thing == state.OriginalWeapon;

        public static void Proposed(Pawn pawn, Job job)
        {
            if (!AomLog.DetailedEnabled || job == null) return;
            Report(pawn, job, "proposed");
        }

        public static void Admitted(Pawn pawn, Job proposed, Job current)
        {
            if (!AomLog.DetailedEnabled) return;
            var state = AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn);
            if (!(IsPreparation(state, proposed) || IsRestoration(state, proposed))) return;
            // An adjacent Equip can finish synchronously before StartJob's
            // postfix. Its actual ending already reports the result.
            if (endings.TryGetValue(proposed, out EndRecord ending) &&
                ending.LoadId == proposed.loadID) return;
            Report(pawn, proposed, ReferenceEquals(proposed, current)
                ? "admitted" : PreparationJobHandoff.IsQueued(pawn, proposed)
                ? $"queued behind {current?.def?.defName ?? "none"}#{current?.loadID ?? -1}"
                : $"replaced before admission by {current?.def?.defName ?? "none"}#{current?.loadID ?? -1}");
        }

        public static void Ended(Pawn pawn, PawnApparelState state, Job job, JobCondition condition)
        {
            if (!(IsPreparation(state, job) || IsRestoration(state, job))) return;
            endings.Remove(job);
            endings.Add(job, new EndRecord { LoadId = job.loadID });
            if (IsPreparation(state, job) && condition == JobCondition.Succeeded && job.targetA.Thing is ThingWithComps weapon)
                WeaponPreparationRetryRegistry.Equipped(pawn, weapon);
            if (AomLog.DetailedEnabled) Report(pawn, job, "ended " + condition);
        }

        private static void Report(Pawn pawn, Job job, string outcome)
        {
            ThingWithComps target = job.targetA.Thing as ThingWithComps;
            ThingWithComps primary = pawn?.equipment?.Primary;
            string kind = IsRestoration(AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn), job)
                ? "saved weapon" : "work weapon";
            AomLog.Detailed($"[AutomaticOutfitManager] {pawn?.LabelShortCap ?? "Pawn"}: " +
                $"{kind} Equip#{job.loadID} {outcome}; " +
                $"target={target?.LabelCap}#{target?.thingIDNumber ?? -1}, " +
                $"spawned={target?.Spawned == true}, cell={target?.PositionHeld}, " +
                $"canEquip={target != null && EquipmentUtility.CanEquip(target, pawn)}, " +
                $"primary={primary?.LabelCap}#{primary?.thingIDNumber ?? -1}, " +
                $"targetIsPrimary={target != null && primary == target}, " +
                $"tick={Find.TickManager?.TicksGame ?? 0}, " +
                $"queue=[{PreparationJobHandoff.QueueDescription(pawn)}].");
        }
    }
}
