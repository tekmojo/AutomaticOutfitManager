using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace AutomaticOutfitManager.Detection
{
    public static class PawnAccessClassifier
    {
        private static readonly Type HospitalityGuestCompType =
            AccessTools.TypeByName("Hospitality.CompGuest");
        private static readonly FieldInfo HospitalityArrivedField =
            HospitalityGuestCompType == null
                ? null
                : AccessTools.Field(HospitalityGuestCompType, "arrived");
        private static readonly FieldInfo HospitalitySentAwayField =
            HospitalityGuestCompType == null
                ? null
                : AccessTools.Field(HospitalityGuestCompType, "sentAway");
        private static readonly System.Collections.Generic.Dictionary<Pawn, bool>
            HostedGuestCache =
                new System.Collections.Generic.Dictionary<Pawn, bool>();
        private static readonly System.Collections.Generic.Dictionary<Pawn, bool>
            ApparelEligibilityCache =
                new System.Collections.Generic.Dictionary<Pawn, bool>();
        private static int cachedClassificationFrame = int.MinValue;

        public static void ResetRuntimeCache()
        {
            HostedGuestCache.Clear();
            ApparelEligibilityCache.Clear();
            cachedClassificationFrame = int.MinValue;
        }

        public static bool IsHostedGuest(Pawn pawn)
        {
            if (pawn == null || pawn.guest?.IsPrisoner == true || pawn.IsSlave)
                return false;

            EnsureClassificationCache();
            if (HostedGuestCache.TryGetValue(pawn, out bool cached))
                return cached;

            // Quest lodgers and Hospitality guests may temporarily expose a
            // hosted worker as player-faction. IsQuestLodger is the authoritative
            // vanilla signal; HostFaction remains the compatibility fallback for
            // other hosted workers and friendly foreign-faction visitors.
            Faction playerFaction = Faction.OfPlayerSilentFail;
            bool result = pawn.IsQuestLodger() ||
                          IsArrivedHospitalityGuest(pawn) ||
                          (playerFaction != null &&
                           (pawn.HostFaction == playerFaction ||
                            (pawn.Faction != null && pawn.Faction != playerFaction &&
                             !pawn.Faction.HostileTo(playerFaction))));
            HostedGuestCache[pawn] = result;
            return result;
        }

        private static bool IsArrivedHospitalityGuest(Pawn pawn)
        {
            if (HospitalityGuestCompType == null || pawn?.AllComps == null)
                return false;

            ThingComp comp = null;
            foreach (ThingComp candidate in pawn.AllComps)
            {
                if (candidate != null &&
                    HospitalityGuestCompType.IsInstanceOfType(candidate))
                {
                    comp = candidate;
                    break;
                }
            }
            if (comp == null)
                return false;

            bool arrived = HospitalityArrivedField?.GetValue(comp) is bool value && value;
            bool sentAway = HospitalitySentAwayField?.GetValue(comp) is bool sent && sent;
            return arrived && !sentAway;
        }

        public static bool IsColonyPrisoner(Pawn pawn) =>
            pawn?.guest?.IsPrisoner == true && pawn.IsPrisonerOfColony &&
            !IsNativeCustodyEscapeActive(pawn);

        public static bool IsNativeCustodyEscapeActive(Pawn pawn)
        {
            if (pawn == null ||
                (pawn.guest?.IsPrisoner != true && !pawn.IsSlave))
            {
                return false;
            }

            // Prison breaks are controlled by a native PrisonerEscape duty,
            // not a mental state. IsPrisonerOfColony remains true throughout
            // the breakout, so relying on guest status makes AOM repeatedly
            // cancel Equip and escape movement as if they were cooperative
            // custody wandering. Include the sapper variant and modded duties
            // that retain the same native prefix.
            string dutyDefName = pawn.mindState?.duty?.def?.defName ?? string.Empty;
            if (dutyDefName.StartsWith(
                    "PrisonerEscape", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Ideology slave rebellions use a mental state rather than the
            // prisoner lord/duty. Native combat and escape behavior must retain
            // the same authority if a slave uprising begins while AOM has an
            // outfit snapshot open.
            string mentalStateDefName = pawn.MentalStateDef?.defName ?? string.Empty;
            return pawn.IsSlave &&
                   (mentalStateDefName.Equals(
                        "Rebellion", StringComparison.OrdinalIgnoreCase) ||
                    mentalStateDefName.IndexOf(
                        "SlaveRebellion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    mentalStateDefName.IndexOf(
                        "SlaveEscape", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static bool IsApparelEligibleHuman(Pawn pawn)
        {
            if (pawn?.RaceProps?.Humanlike != true || pawn.apparel == null)
            {
                return false;
            }

            EnsureClassificationCache();
            if (ApparelEligibilityCache.TryGetValue(pawn, out bool cached))
                return cached;

            if (IsNativeCustodyEscapeActive(pawn))
            {
                ApparelEligibilityCache[pawn] = false;
                return false;
            }

            // Use the raw custody status here after the single escape check
            // above. Calling IsColonyPrisoner would repeat the duty/mental-state
            // classification on every path cell for ordinary prisoners.
            bool colonyPrisoner = pawn.guest?.IsPrisoner == true &&
                                  pawn.IsPrisonerOfColony;
            bool eligible = pawn.IsColonist || pawn.IsSlave || IsHostedGuest(pawn) ||
                            colonyPrisoner;
            ApparelEligibilityCache[pawn] = eligible;
            return eligible;
        }

        private static void EnsureClassificationCache()
        {
            int frame = Time.frameCount;
            if (cachedClassificationFrame == frame)
                return;

            HostedGuestCache.Clear();
            ApparelEligibilityCache.Clear();
            cachedClassificationFrame = frame;
        }
    }
}
