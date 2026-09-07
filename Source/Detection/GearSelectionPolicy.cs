using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Rules;
using Verse;

namespace AutomaticOutfitManager.Detection
{
    public static class GearSelectionPolicy
    {
        public static bool Selects(ApparelRule rule, ThingDef item) => rule != null && item != null &&
            (item.apparel != null ? rule.RequiredApparel.Contains(item) :
                item.IsWeapon && rule.HasWeaponRequirement && RuleEvaluator.WeaponDefMatchesRequirement(item, rule));

        // Selection identity includes disabled and other-map rules. Their gear
        // is not abandoned stock merely because they do not currently apply here.
        public static List<ApparelRule> SelectingRules(ThingDef item, IEnumerable<ApparelRule> rules) =>
            (rules ?? Enumerable.Empty<ApparelRule>()).Where(rule => Selects(rule, item)).ToList();

        public static bool IsRetained(ThingDef item, bool managed, IEnumerable<ApparelRule> rules) =>
            managed && !SelectingRules(item, rules).Any();
    }
}
