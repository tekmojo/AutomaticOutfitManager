using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
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

        public static bool IsHostedGuest(Pawn pawn)
        {
            if (pawn?.guest == null || pawn.guest.IsPrisoner || pawn.IsSlave)
                return false;

            // Hospitality and similar guest systems may temporarily expose a
            // hosted worker as player-faction. HostFaction is the stable signal;
            // retain the friendly foreign-faction fallback for vanilla guests.
            return IsArrivedHospitalityGuest(pawn) ||
                   pawn.HostFaction == Faction.OfPlayer ||
                   (pawn.Faction != null && pawn.Faction != Faction.OfPlayer &&
                    !pawn.Faction.HostileTo(Faction.OfPlayer));
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
            pawn?.guest?.IsPrisoner == true && pawn.IsPrisonerOfColony;

        public static bool IsApparelEligibleHuman(Pawn pawn) =>
            pawn?.RaceProps?.Humanlike == true && pawn.apparel != null &&
            (pawn.IsColonist || pawn.IsSlave || IsHostedGuest(pawn) ||
             IsColonyPrisoner(pawn));
    }
}
