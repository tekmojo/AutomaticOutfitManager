using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    // A selected, usable Work rule requirement is work stock even when the
    // pawn was already wearing it when its personal snapshot was captured.
    public static class WorkGearSnapshotPolicy
    {
        public static List<ApparelRule> MatchingRules(Pawn pawn, ThingWithComps item,
            IEnumerable<ApparelRule> rules)
        {
            if (pawn?.Map == null || item == null || item.Destroyed)
                return new List<ApparelRule>();
            return (rules ?? Enumerable.Empty<ApparelRule>()).Where(rule =>
                rule?.Enabled == true && !rule.IsNonWork && rule.Area?.Map == pawn.Map &&
                (item is Apparel apparel
                    ? rule.RequiredApparel.Contains(item.def) && rule.Allows(apparel)
                    : item.def?.IsWeapon == true && rule.HasWeaponRequirement &&
                      RuleEvaluator.WeaponMatchesRequirement(item, rule)))
                .GroupBy(rule => rule.Id).Select(group => group.First()).ToList();
        }

        public static List<ThingWithComps> Clean(SavedNonWorkOutfit saved,
            IEnumerable<ApparelRule> rules)
        {
            var removed = new List<ThingWithComps>();
            if (saved?.Pawn == null) return removed;
            foreach (var item in saved.Apparel.Cast<ThingWithComps>()
                .Concat(new[] { saved.Weapon }).Where(item => item != null).Distinct().ToList())
            {
                var matches = MatchingRules(saved.Pawn, item, rules);
                if (matches.Count == 0) continue;
                if (item is Apparel apparel) saved.Apparel.RemoveAll(candidate => candidate == apparel);
                if (saved.Weapon == item) saved.Weapon = null;
                RecordSources(saved, item, matches);
                removed.Add(item);
            }
            return removed;
        }

        public static void RecordSources(SavedNonWorkOutfit saved, ThingWithComps item,
            IEnumerable<ApparelRule> rules)
        {
            if (saved == null || item == null) return;
            var entry = saved.WorkGear.FirstOrDefault(record => record.Item == item);
            if (entry == null)
            {
                entry = new WorkGearSource { Item = item };
                saved.WorkGear.Add(entry);
            }
            foreach (var rule in rules)
                if (!entry.RuleIds.Contains(rule.Id)) entry.RuleIds.Add(rule.Id);
        }

        public static bool IsSnapshotRestoreJob(PawnApparelState state, Job job)
        {
            if (state == null || job == null) return false;
            // AOM deliberately marks its Wear restores playerForced to bypass
            // outfit optimization. Recognize their exact pre-cleanup target,
            // just as the normal transition guards do; that flag alone does
            // not distinguish a native player order from an assigned restore.
            return (job.def == JobDefOf.Wear && job.targetA.Thing is RimWorld.Apparel apparel &&
                    state.OriginalApparel.Contains(apparel)) ||
                (job.def == JobDefOf.Equip && !job.playerForced && state.WeaponRestorationRequested &&
                    job.targetA.Thing != null && state.OriginalWeapon == job.targetA.Thing);
        }

        public static bool ObsoleteSnapshotRestore(PawnApparelState state, Job job,
            ICollection<ThingWithComps> removed)
        {
            if (state == null || job == null ||
                !(job.targetA.Thing is ThingWithComps item) || !removed.Contains(item))
                return false;
            return (job.def == JobDefOf.Wear && item is RimWorld.Apparel apparel &&
                    !state.OriginalApparel.Contains(apparel)) ||
                (job.def == JobDefOf.Equip && !state.WeaponRuleOverrideExplicit &&
                    state.OriginalWeapon != item);
        }

        public static List<ThingWithComps> CleanPersonalState(PawnApparelState state,
            IEnumerable<ApparelRule> rules, SavedNonWorkOutfit history,
            System.Predicate<ThingWithComps> heldByPawn)
        {
            // These are temporary transition targets, not personal snapshots.
            // In particular, a partial Non-Work return intentionally retains
            // unselected work gear; do not put it back on the removal ledger.
            if (state?.Pawn == null || state.NonWorkFallbackActive ||
                !string.IsNullOrEmpty(state.NonWorkRestorationRuleId) ||
                rules?.Any(rule => rule?.Id == state.ActiveRuleId && rule.IsNonWork) == true)
                return new List<ThingWithComps>();
            var originalApparel = (state.ApparelInterventionActive
                ? state.OriginalApparel : state.Pawn.apparel?.WornApparel)?.ToList() ?? new List<Apparel>();
            var snapshot = new SavedNonWorkOutfit
            {
                Pawn = state.Pawn,
                Apparel = originalApparel.ToList(),
                Weapon = state.WeaponInterventionActive ? state.OriginalWeapon :
                    state.WeaponRuleOverrideExplicit ? null : state.Pawn.equipment?.Primary
            };
            var removed = Clean(snapshot, rules);
            foreach (var item in removed)
            {
                RecordSources(history, item, MatchingRules(state.Pawn, item, rules));
                if (item is Apparel apparel)
                {
                    if (!state.ApparelInterventionActive)
                    {
                        state.OriginalApparel = originalApparel.ToList();
                        state.ApparelInterventionActive = true;
                    }
                    bool wasAssigned = state.ReusedOriginalApparel?.Contains(apparel) == true;
                    state.OriginalApparel.RemoveAll(candidate => candidate == apparel);
                    state.ReusedOriginalApparel?.RemoveAll(candidate => candidate == apparel);
                    if (wasAssigned || heldByPawn(apparel))
                    {
                        if (!state.ManagedApparel.Contains(apparel)) state.ManagedApparel.Add(apparel);
                    }
                }
                else
                {
                    state.OriginalWeapon = null;
                    if (!state.WeaponRuleOverrideExplicit && heldByPawn(item))
                    {
                        state.WeaponInterventionActive = true;
                        if (!state.ManagedWeapons.Contains(item)) state.ManagedWeapons.Add(item);
                    }
                    // Keep the intervention/override flags. Normal safe return
                    // owns the removal; an explicit primary choice still wins.
                }
            }
            return removed;
        }
    }
}
