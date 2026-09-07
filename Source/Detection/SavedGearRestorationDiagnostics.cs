using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    internal static class SavedGearRestorationDiagnostics
    {
        internal static void Report(Pawn pawn, PawnApparelState state)
        {
            if (!AomLog.DetailedEnabled || pawn?.Map == null || state == null ||
                !AomLog.ShouldLogDetailed(pawn, "unavailable-saved-outfit", 600)) return;
            var items = new List<Thing>();
            if (state.OriginalApparel != null)
                items.AddRange(state.OriginalApparel.Where(item => item != null &&
                    pawn.apparel?.WornApparel.Contains(item) != true));
            if (state.WeaponInterventionActive && state.OriginalWeapon != null &&
                pawn.equipment?.Primary != state.OriginalWeapon)
                items.Add(state.OriginalWeapon);
            // A temporary weapon still in inventory can hold up the entire
            // weapon return, even if the saved primary itself is available.
            if (state.WeaponInterventionActive && state.ManagedWeapons != null)
                items.AddRange(state.ManagedWeapons.Where(item => item != null &&
                    item != pawn.equipment?.Primary && !item.Spawned && item.MapHeld == pawn.Map));
            foreach (Thing item in items.Distinct())
            {
                bool local = !item.Destroyed && item.Spawned && item.Map == pawn.Map;
                bool reserved = local && pawn.CanReserve(item);
                bool nativeReach = local && pawn.CanReach(item, GearRetrievalRoute.EndMode(item), Danger.Deadly);
                // Do not run protected path probes for an unspawned or natively
                // unreachable item. A skipped probe is not a failed probe.
                string protectedReach = nativeReach ? GearRetrievalRoute.CanReach(pawn, item).ToString() : "not checked";
                // Distinguish a blocked owner from an item native hauling never
                // considers (for example, stock already in best-priority storage).
                bool haulCandidate = local && item.Map.listerHaulables
                    .ThingsPotentiallyNeedingHauling().Contains(item);
                Pawn reserver = item.MapHeld?.reservationManager?.FirstRespectedReserver(item, pawn, null);
                string equip = "not applicable";
                if (item == state.OriginalWeapon && item is ThingWithComps weapon)
                    equip = RestorationPlanner.CanAttemptSavedWeaponEquip(weapon, pawn, out string reason)
                        ? "allowed" : reason ?? "denied";
                AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: unresolved restoration item " +
                    $"{item.LabelCap} [{item.ThingID}]; destroyed={item.Destroyed}, spawned={item.Spawned}, " +
                    $"map={item.MapHeld?.GetUniqueLoadID() ?? "none"}, holder={DescribeHolder(item)}, " +
                    $"position={item.PositionHeld}; forbidden={(local ? item.IsForbidden(pawn).ToString() : "not checked")}, " +
                    $"canReserve={(local ? reserved.ToString() : "not checked")}, reserver={reserver?.LabelShortCap ?? "none"}, " +
                    $"nativeReach={(local ? nativeReach.ToString() : "not checked")}, protectedReach={protectedReach}, equip={equip}, nativeHaulCandidate={haulCandidate}. " +
                    (local && protectedReach == "False" ? $"Recovery: {SavedGearRecovery.DescribeLastQuery(pawn, item)}. " : "") +
                    "Saved ownership and restoration order unchanged.");
            }
        }

        private static string DescribeHolder(Thing item)
        {
            var names = new List<string>();
            IThingHolder holder = item.ParentHolder;
            // Diagnostics must remain bounded even with a malformed mod holder chain.
            for (int depth = 0; holder != null && depth < 12; depth++, holder = holder.ParentHolder)
                names.Add(holder is Thing thing ? $"{thing.LabelCap} [{thing.ThingID}]" : holder.GetType().FullName);
            return names.Count == 0 ? "none" : string.Join(" -> ", names);
        }
    }
}
