using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Rules;
using Verse;

namespace AutomaticOutfitManager.Detection
{
    internal static class AreaNestingPolicy
    {
        // Test actual painted cells, not the enclosing rectangle. An empty area
        // is not nested; equality counts, but containment in the reverse direction does not.
        internal static bool ContainsWorkArea(Area nonWork, Area work)
        {
            if (nonWork?.Map == null || work?.Map != nonWork.Map) return false;
            if (work.TrueCount == 0 || work.TrueCount > nonWork.TrueCount) return false;
            bool any = false;
            foreach (var cell in work.ActiveCells)
            {
                any = true;
                if (!nonWork[cell]) return false;
            }
            return any;
        }

        // The candidate may be disabled while its readiness or proposed enabling
        // is inspected. Only enabled counterparts constrain the candidate.
        internal static ApparelRule Conflict(ApparelRule candidate, IEnumerable<ApparelRule> rules)
        {
            if (candidate?.Area == null || rules == null) return null;
            return rules.FirstOrDefault(other => other != null && other != candidate &&
                other.Enabled && other.IsNonWork != candidate.IsNonWork &&
                (candidate.IsNonWork
                    ? ContainsWorkArea(candidate.Area, other.Area)
                    : ContainsWorkArea(other.Area, candidate.Area)));
        }

        internal static void DisableNestedWorkRules(IEnumerable<ApparelRule> rules,
            Action<ApparelRule, ApparelRule> onDisabled)
        {
            if (rules == null) return;
            foreach (var work in rules)
            {
                if (work?.Enabled != true || work.IsNonWork) continue;
                var nonWork = Conflict(work, rules);
                if (nonWork == null) continue;
                work.Enabled = false;
                onDisabled(work, nonWork);
            }
        }
    }
}
