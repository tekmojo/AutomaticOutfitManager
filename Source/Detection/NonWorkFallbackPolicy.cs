using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Rules;
using Verse;

namespace AutomaticOutfitManager.Detection
{
    public static class NonWorkFallbackPolicy
    {
        public static ApparelRule ConflictingSource(ApparelRule destination, ThingDef item,
            IEnumerable<ApparelRule> rules, bool? removeAll = null, IEnumerable<string> removedIds = null)
        {
            if (destination?.IsNonWork != true || item == null || rules == null) return null;
            return rules.FirstOrDefault(source => source?.Enabled == true && !source.IsNonWork &&
                (destination.Area?.Map == source.Area?.Map) &&
                ((removeAll ?? destination.RemoveAllWorkOutfits) ||
                 (removedIds ?? destination.WorkOutfitsToRemove).Contains(source.Id)) &&
                (item.apparel != null ? source.RequiredApparel.Contains(item) :
                 item.IsWeapon && source.HasWeaponRequirement &&
                 RuleEvaluator.WeaponDefMatchesRequirement(item, source)));
        }

        // Reverse validation: selecting Work gear must not invalidate an existing
        // Non-Work selection which also requests that Work outfit's removal.
        public static ApparelRule ConflictingDestination(ApparelRule source, ThingDef candidate,
            IEnumerable<ApparelRule> rules)
        {
            if (source == null || source.IsNonWork || !source.Enabled || rules == null) return null;
            return rules.FirstOrDefault(destination => destination?.Enabled == true && destination.IsNonWork &&
                destination.Area?.Map == source.Area?.Map &&
                (destination.RemoveAllWorkOutfits || destination.WorkOutfitsToRemove.Contains(source.Id)) &&
                (candidate != null ? GearSelectionPolicy.Selects(destination, candidate) :
                    FirstConflict(destination, new[] { source }) != null));
        }

        public static ApparelRule FirstConflict(ApparelRule destination, IEnumerable<ApparelRule> rules, bool? removeAll = null, IEnumerable<string> removedIds = null)
        {
            if (destination?.IsNonWork != true) return null;
            foreach (var item in destination.RequiredApparel.Concat(destination.RequiredWeapons))
            {
                var source = ConflictingSource(destination, item, rules, removeAll, removedIds);
                if (source != null) return source;
            }
            // Preserve validation for older saves using broad weapon categories.
            if (!destination.UsesExactWeapons && destination.HasWeaponRequirement)
                foreach (var item in DefDatabase<ThingDef>.AllDefsListForReading.Where(item =>
                             item.IsWeapon && RuleEvaluator.WeaponDefMatchesRequirement(item, destination)))
                {
                    var source = ConflictingSource(destination, item, rules, removeAll, removedIds);
                    if (source != null) return source;
                }
            return null;
        }
    }
}
