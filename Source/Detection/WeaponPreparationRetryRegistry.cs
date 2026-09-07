using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Patches;
using RimWorld;
using Verse;

namespace AutomaticOutfitManager.Detection
{
    // Pawn-specific acquisition failures outlive the outfit snapshot. This is
    // availability history, not ownership: other pawns can still use the stock.
    internal static class WeaponPreparationRetryRegistry
    {
        private const int InitialDelay = 2500;
        private const int MaximumDelay = 15000;

        private sealed class Candidate
        {
            public string Signature;
            public int UntilTick;
        }

        private sealed class History
        {
            public readonly Dictionary<int, Candidate> Candidates = new Dictionary<int, Candidate>();
            public int ExhaustedCycles;
        }

        private static readonly Dictionary<int, History> Histories = new Dictionary<int, History>();

        public static void ResetForLoadedGame() => Histories.Clear();

        private static History For(Pawn pawn)
        {
            if (!Histories.TryGetValue(pawn.thingIDNumber, out History history))
                Histories.Add(pawn.thingIDNumber, history = new History());
            return history;
        }

        public static void Reject(Pawn pawn, ThingWithComps weapon)
        {
            if (pawn == null || weapon == null) return;
            Defer(For(pawn), pawn, weapon, InitialDelay);
        }

        // Once a whole preparation has exhausted its budget, do not immediately
        // change clothes again for another unchanged item in the same stock pool.
        // New stock or a material change can reopen a candidate; otherwise the
        // bounded delay increases across failed cycles, up to six in-game hours.
        public static int DeferUnchangedStock(Pawn pawn, CombinedWeaponRequirement requirement)
        {
            if (pawn?.Map == null || requirement?.HasRequirement != true) return InitialDelay;
            History history = For(pawn);
            history.ExhaustedCycles = Math.Min(4, history.ExhaustedCycles + 1);
            int delay = Math.Min(MaximumDelay, InitialDelay << (history.ExhaustedCycles - 1));
            foreach (ThingWithComps weapon in pawn.Map.listerThings
                         .ThingsInGroup(ThingRequestGroup.Weapon).OfType<ThingWithComps>())
            {
                if (weapon.Spawned && requirement.Matches(weapon))
                    Defer(history, pawn, weapon, delay);
            }
            return delay;
        }

        private static void Defer(History history, Pawn pawn, ThingWithComps weapon, int delay)
        {
            history.Candidates[weapon.thingIDNumber] = new Candidate
            {
                Signature = AvailabilitySignature(weapon, pawn),
                UntilTick = (Find.TickManager?.TicksGame ?? 0) + delay
            };
        }

        public static bool IsDeferred(Pawn pawn, ThingWithComps weapon)
        {
            if (pawn == null || weapon == null ||
                !Histories.TryGetValue(pawn.thingIDNumber, out History history) ||
                !history.Candidates.TryGetValue(weapon.thingIDNumber, out Candidate candidate))
                return false;
            if ((Find.TickManager?.TicksGame ?? 0) >= candidate.UntilTick ||
                candidate.Signature != AvailabilitySignature(weapon, pawn))
            {
                history.Candidates.Remove(weapon.thingIDNumber);
                return false;
            }
            return true;
        }

        public static void Equipped(Pawn pawn, ThingWithComps weapon)
        {
            if (pawn == null || weapon == null || pawn.equipment?.Primary != weapon ||
                !Histories.TryGetValue(pawn.thingIDNumber, out History history)) return;
            history.ExhaustedCycles = 0;
            history.Candidates.Remove(weapon.thingIDNumber);
            if (history.Candidates.Count == 0) Histories.Remove(pawn.thingIDNumber);
        }

        internal static string AvailabilitySignature(ThingWithComps weapon, Pawn pawn)
        {
            if (weapon == null) return "null";
            Map map = weapon.MapHeld;
            string holder = weapon.ParentHolder is Thing holderThing
                ? holderThing.GetUniqueLoadID()
                : weapon.ParentHolder?.GetType().FullName ?? "none";
            bool onMap = weapon.Spawned && map == pawn?.Map;
            AutomaticOutfitManagerGameComponent component = AutomaticOutfitManagerGameComponent.Current;
            return $"{weapon.Destroyed}|{weapon.Spawned}|{map?.uniqueID ?? -1}|" +
                   $"{pawn?.Map?.uniqueID ?? -1}|{weapon.PositionHeld}|{holder}|" +
                   $"{onMap && weapon.IsForbidden(pawn)}|{pawn != null && EquipmentUtility.CanEquip(weapon, pawn)}|" +
                   $"{component?.IsSavedWeaponForOtherPawn(weapon, pawn) == true}|" +
                   $"{component?.IsManagedWeaponAssignedToOtherPawn(weapon, pawn) == true}|" +
                   $"{onMap && ReservationUtility_SavedApparel_Patch.CanReserveForOutfit(pawn, weapon)}";
        }
    }
}
