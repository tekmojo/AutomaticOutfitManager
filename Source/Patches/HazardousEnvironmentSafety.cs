using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    /// <summary>
    /// Keeps AOM-owned environmental protection on until the pawn can remove it
    /// at a genuinely safe cell. Native pawn safety queries include currently
    /// worn apparel, so they cannot be used directly to decide whether that same
    /// apparel is safe to remove.
    /// </summary>
    internal static class HazardousEnvironmentSafety
    {
        private const float VacuumHazardThreshold = 0.5f;
        private const float ProtectionEpsilon = 0.001f;

        private sealed class ProtectionProfile
        {
            public bool VacuumProtection;
            public bool ToxicProtection;
            public bool ColdProtection;
            public bool HeatProtection;
            public bool ToxicFallout;
            public bool NoxiousHaze;
            public FloatRange UnprotectedTemperatureRange;
        }

        internal static bool JobRequiresManagedProtection(
            Pawn pawn,
            PawnApparelState state,
            Job job,
            out string reason)
        {
            reason = null;
            if (!TryBuildProfile(pawn, state, null, out ProtectionProfile profile))
                return false;

            if (TryHazardAt(pawn, pawn.Position, profile, out reason))
                return true;
            if (job == null)
                return false;

            if (PausedAreaWorkFilter.IsHaulingJob(job))
            {
                LocalTargetInfo pickup = job.targetA;
                LocalTargetInfo destination = job.targetB.IsValid
                    ? job.targetB
                    : job.targetC;
                if (SegmentRequiresProtection(
                        pawn, pawn.Position, pickup, profile, out reason))
                {
                    return true;
                }

                IntVec3 pickupCell = TargetCellOnMap(pawn, pickup);
                if (pickupCell.IsValid && SegmentRequiresProtection(
                        pawn, pickupCell, destination, profile, out reason))
                {
                    return true;
                }


                foreach (LocalTargetInfo queuedTarget in QueuedTargets(job))
                {
                    if (SegmentRequiresProtection(
                            pawn, pawn.Position, queuedTarget, profile, out reason))
                    {
                        return true;
                    }
                }
            }
            else
            {
                foreach (LocalTargetInfo target in DirectTargets(job))
                {
                    if (SegmentRequiresProtection(
                            pawn, pawn.Position, target, profile, out reason))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal static bool MustRetainManagedProtectionAt(
            Pawn pawn,
            PawnApparelState state,
            IntVec3 cell,
            out string reason)
        {
            reason = null;
            return TryBuildProfile(pawn, state, null, out ProtectionProfile profile) &&
                   TryHazardAt(pawn, cell, profile, out reason);
        }

        internal static bool RemovalWouldExposePawn(
            Pawn pawn,
            PawnApparelState state,
            Apparel apparel,
            out string reason)
        {
            reason = null;
            if (pawn?.Map == null || apparel == null ||
                pawn.apparel?.WornApparel.Contains(apparel) != true ||
                state?.ManagedApparel?.Contains(apparel) != true)
            {
                return false;
            }

            return TryBuildProfile(pawn, state, apparel, out ProtectionProfile profile) &&
                   TryHazardAt(pawn, pawn.Position, profile, out reason);
        }

        private static bool TryBuildProfile(
            Pawn pawn,
            PawnApparelState state,
            Apparel onlyRemovedApparel,
            out ProtectionProfile profile)
        {
            profile = null;
            if (pawn?.Map == null || pawn.apparel == null || state == null)
                return false;

            var managed = new HashSet<Apparel>(
                (state.ManagedApparel ?? new List<Apparel>())
                .Where(item => item != null &&
                               pawn.apparel.WornApparel.Contains(item)));
            if (onlyRemovedApparel != null)
            {
                managed.RemoveWhere(item => item != onlyRemovedApparel);
            }
            if (managed.Count == 0)
                return false;

            List<ThingStuffPair> remainingApparel = pawn.apparel.WornApparel
                .Where(item => item != null && !managed.Contains(item))
                .Select(item => new ThingStuffPair(item.def, item.Stuff, 1f))
                .ToList();
            FloatRange currentTemperatureRange =
                GenTemperature.SafeTemperatureRange(pawn);
            FloatRange unprotectedTemperatureRange =
                GenTemperature.SafeTemperatureRange(pawn, remainingApparel);

            profile = new ProtectionProfile
            {
                UnprotectedTemperatureRange = unprotectedTemperatureRange,
                ColdProtection = currentTemperatureRange.min <
                    unprotectedTemperatureRange.min - ProtectionEpsilon,
                HeatProtection = currentTemperatureRange.max >
                    unprotectedTemperatureRange.max + ProtectionEpsilon,
                ToxicFallout = pawn.Map.gameConditionManager?.ConditionIsActive(
                    GameConditionDefOf.ToxicFallout) == true,
                NoxiousHaze = pawn.Map.gameConditionManager?.ConditionIsActive(
                    GameConditionDefOf.NoxiousHaze) == true
            };

            foreach (Apparel item in managed)
            {
                var pair = new ThingStuffPair(item.def, item.Stuff, 1f);
                profile.VacuumProtection |= pair.VacuumResistance > ProtectionEpsilon;
                profile.ToxicProtection |=
                    pair.ToxicEnvironmentResistance > ProtectionEpsilon;
            }

            return profile.VacuumProtection || profile.ToxicProtection ||
                   profile.ColdProtection || profile.HeatProtection;
        }

        private static bool TryHazardAt(
            Pawn pawn,
            IntVec3 cell,
            ProtectionProfile profile,
            out string reason)
        {
            reason = null;
            if (pawn?.Map == null || profile == null || !cell.IsValid ||
                !cell.InBounds(pawn.Map))
            {
                return false;
            }

            if (profile.VacuumProtection && VacuumUtility.CanBeVacuumBurnt(pawn) &&
                VacuumUtility.GetVacuum(cell, pawn.Map) >= VacuumHazardThreshold)
            {
                reason = "vacuum exposure";
                return true;
            }

            float temperature = GenTemperature.GetTemperatureForCell(cell, pawn.Map);
            if (profile.ColdProtection &&
                temperature < profile.UnprotectedTemperatureRange.min)
            {
                reason = "dangerous cold";
                return true;
            }
            if (profile.HeatProtection &&
                temperature > profile.UnprotectedTemperatureRange.max)
            {
                reason = "dangerous heat";
                return true;
            }

            if (profile.ToxicProtection &&
                (GasUtility.AnyGas(cell, pawn.Map, GasType.ToxGas) ||
                 (!cell.Roofed(pawn.Map) &&
                  (profile.ToxicFallout || profile.NoxiousHaze))))
            {
                reason = "toxic exposure";
                return true;
            }

            return false;
        }

        private static bool SegmentRequiresProtection(
            Pawn pawn,
            IntVec3 start,
            LocalTargetInfo destination,
            ProtectionProfile profile,
            out string reason)
        {
            reason = null;
            IntVec3 destinationCell = TargetCellOnMap(pawn, destination);
            if (!destinationCell.IsValid)
                return false;
            if (TryHazardAt(pawn, destinationCell, profile, out reason))
                return true;

            string pathReason = null;
            bool crossesHazard = ProtectedPathAvoidance.SegmentContainsCell(
                pawn, start, destination, cell =>
                    TryHazardAt(pawn, cell, profile, out pathReason));
            if (crossesHazard)
            {
                reason = pathReason ?? "hazardous conditions along the route";
                return true;
            }

            return false;
        }

        private static IntVec3 TargetCellOnMap(
            Pawn pawn, LocalTargetInfo target)
        {
            if (pawn?.Map == null || !target.IsValid ||
                (target.HasThing && target.Thing?.MapHeld != pawn.Map))
            {
                return IntVec3.Invalid;
            }

            IntVec3 cell = target.Cell;
            return cell.IsValid && cell.InBounds(pawn.Map)
                ? cell
                : IntVec3.Invalid;
        }

        private static IEnumerable<LocalTargetInfo> DirectTargets(Job job)
        {
            if (job == null)
                yield break;

            if (job.targetA.IsValid)
                yield return job.targetA;
            if (job.targetB.IsValid)
                yield return job.targetB;
            if (job.targetC.IsValid)
                yield return job.targetC;
            if (job.targetQueueA != null)
            {
                foreach (LocalTargetInfo target in job.targetQueueA)
                    if (target.IsValid)
                        yield return target;
            }
            if (job.targetQueueB != null)
            {
                foreach (LocalTargetInfo target in job.targetQueueB)
                    if (target.IsValid)
                        yield return target;
            }
        }

        private static IEnumerable<LocalTargetInfo> QueuedTargets(Job job)
        {
            if (job?.targetQueueA != null)
            {
                foreach (LocalTargetInfo target in job.targetQueueA)
                    if (target.IsValid)
                        yield return target;
            }
            if (job?.targetQueueB != null)
            {
                foreach (LocalTargetInfo target in job.targetQueueB)
                    if (target.IsValid)
                        yield return target;
            }
        }
    }
}
