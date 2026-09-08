using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;

namespace AutomaticOutfitManager.Detection
{
    // Only exact items removed from an old personal snapshot enter this queue.
    // A shared type or a locker location alone must never recall a pawn.
    internal static class SnapshotReturnMigration
    {
        internal static void Remember(SavedNonWorkOutfit saved,
            IEnumerable<ThingWithComps> removed, Predicate<ThingWithComps> held)
        {
            if (saved == null) return;
            foreach (var item in removed.Where(item => item != null && !item.Destroyed && held(item)))
                if (!saved.PendingSharedReturns.Contains(item)) saved.PendingSharedReturns.Add(item);
        }

        internal static void Prune(SavedNonWorkOutfit saved, Predicate<ThingWithComps> held)
        {
            saved.PendingSharedReturns.RemoveAll(item => item == null || item.Destroyed || !held(item));
        }

        internal static ApparelRule Source(SavedNonWorkOutfit saved, IEnumerable<ApparelRule> rules)
        {
            var ruleList = (rules ?? Enumerable.Empty<ApparelRule>()).Where(rule => rule != null).ToList();
            var ids = saved.WorkGear.Where(entry => saved.PendingSharedReturns.Contains(entry.Item))
                .SelectMany(entry => entry.RuleIds).ToList();
            return ruleList.Where(rule => (ids.Contains(rule.Id) ||
                    saved.PendingSharedReturns.Any(item => GearSelectionPolicy.Selects(rule, item.def))) &&
                    (rule.Area?.Map == saved.Pawn.Map || rule.ChangingArea?.Map == saved.Pawn.Map))
                .OrderByDescending(rule => rule.ChangingArea?.Map == saved.Pawn.Map)
                .FirstOrDefault();
        }

        internal static PawnApparelState CreateState(SavedNonWorkOutfit saved, ApparelRule source)
        {
            if (saved?.Pawn?.apparel == null || saved.PendingSharedReturns.Count == 0) return null;
            var apparel = saved.PendingSharedReturns.OfType<Apparel>().ToList();
            var weapons = saved.PendingSharedReturns.Where(item => item.def?.IsWeapon == true).ToList();
            if (apparel.Count == 0 && weapons.Count == 0) return null;
            // This is a return session, never a new pre-work capture. Keep the
            // cleaned exact personal outfit and use existing safe-return jobs.
            return new PawnApparelState {
                Pawn = saved.Pawn, ActiveRuleId = source?.Id,
                RestorationSourceRuleIds = source == null ? new List<string>() : new List<string> { source.Id },
                OriginalApparel = saved.Apparel.Where(item => item != null && !item.Destroyed).ToList(),
                ManagedApparel = apparel, ApparelInterventionActive = apparel.Count > 0,
                OriginalWeapon = saved.Pawn.equipment?.Primary != null &&
                    !weapons.Contains(saved.Pawn.equipment.Primary)
                        ? saved.Pawn.equipment.Primary : saved.Weapon,
                ManagedWeapons = weapons, WeaponInterventionActive = weapons.Count > 0,
                Transition = ApparelTransition.Active
            };
        }
    }
}
