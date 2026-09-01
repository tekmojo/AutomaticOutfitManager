using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Rules;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    public sealed class CombinedWeaponRequirement
    {
        public bool HasRequirement;
        public WeaponRequirement LegacyCategory;
        public HashSet<ThingDef> ExactDefs;
        public readonly List<ApparelRule> Standards = new List<ApparelRule>();

        public bool Matches(ThingWithComps weapon)
        {
            if (!HasRequirement)
                return true;
            if (weapon?.def?.IsWeapon != true)
                return false;
            if (ExactDefs != null && !ExactDefs.Contains(weapon.def))
                return false;
            return RuleEvaluator.WeaponMatchesRequirement(
                       weapon, LegacyCategory) &&
                   Standards.All(rule => rule.AllowsWeapon(weapon));
        }

        public bool Matches(ThingDef def)
        {
            if (!HasRequirement)
                return true;
            if (def?.IsWeapon != true)
                return false;
            if (ExactDefs != null && !ExactDefs.Contains(def))
                return false;
            return RuleEvaluator.WeaponDefMatchesRequirement(
                def, LegacyCategory);
        }
    }

    public static class RuleEvaluator
    {
        private static readonly IReadOnlyList<ApparelRule> EmptyRules =
            Array.Empty<ApparelRule>();
        private static readonly Dictionary<Map, List<ApparelRule>> EnabledRulesByMap =
            new Dictionary<Map, List<ApparelRule>>();
        private static readonly Dictionary<Map, List<ApparelRule>> ActiveRulesByMap =
            new Dictionary<Map, List<ApparelRule>>();
        private static readonly Dictionary<Map, List<ApparelRule>> PausedRulesByMap =
            new Dictionary<Map, List<ApparelRule>>();
        private static AutomaticOutfitManagerGameComponent cachedRuleComponent;
        private static int cachedRuleFrame = int.MinValue;

        public static void ResetRuntimeCache()
        {
            EnabledRulesByMap.Clear();
            ActiveRulesByMap.Clear();
            PausedRulesByMap.Clear();
            cachedRuleComponent = null;
            cachedRuleFrame = int.MinValue;
        }

        public static IReadOnlyList<ApparelRule> EnabledRulesForMap(Map map)
        {
            if (map == null)
                return EmptyRules;

            EnsureRuleMapCache();
            return EnabledRulesByMap.TryGetValue(map, out List<ApparelRule> rules)
                ? rules
                : EmptyRules;
        }

        public static IReadOnlyList<ApparelRule> ActiveRulesForMap(Map map)
        {
            if (map == null)
                return EmptyRules;

            EnsureRuleMapCache();
            return ActiveRulesByMap.TryGetValue(map, out List<ApparelRule> rules)
                ? rules
                : EmptyRules;
        }

        public static IReadOnlyList<ApparelRule> PausedRulesForMap(Map map)
        {
            if (map == null)
                return EmptyRules;

            EnsureRuleMapCache();
            return PausedRulesByMap.TryGetValue(map, out List<ApparelRule> rules)
                ? rules
                : EmptyRules;
        }

        private static void EnsureRuleMapCache()
        {
            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            int frame = Time.frameCount;
            if (cachedRuleComponent == component && cachedRuleFrame == frame)
                return;

            EnabledRulesByMap.Clear();
            ActiveRulesByMap.Clear();
            PausedRulesByMap.Clear();
            cachedRuleComponent = component;
            cachedRuleFrame = frame;

            if (component?.Rules == null)
                return;

            foreach (ApparelRule rule in component.Rules)
            {
                Map map = rule?.Area?.Map;
                if (rule?.Enabled != true || map == null)
                    continue;

                AddRule(EnabledRulesByMap, map, rule);
                AddRule(rule.WorkAreaPaused ? PausedRulesByMap : ActiveRulesByMap,
                    map, rule);
            }
        }

        private static void AddRule(
            Dictionary<Map, List<ApparelRule>> index,
            Map map,
            ApparelRule rule)
        {
            if (!index.TryGetValue(map, out List<ApparelRule> rules))
            {
                rules = new List<ApparelRule>();
                index.Add(map, rules);
            }

            rules.Add(rule);
        }

        public static ApparelRule MatchingRule(Pawn pawn, Job job)
        {
            return MatchingRules(pawn, job).FirstOrDefault();
        }

        public static List<ApparelRule> MatchingRules(Pawn pawn, Job job)
        {
            if (pawn == null || job == null || pawn.Map == null ||
                !PawnAccessClassifier.IsApparelEligibleHuman(pawn) ||
                pawn.Drafted || pawn.Downed)
                return new List<ApparelRule>();

            // A target may be covered by an outer work area and one or more
            // nested areas. All of their safety requirements apply. Preserve
            // rule order so existing saves remain deterministic.
            var matches = new List<ApparelRule>();
            foreach (ApparelRule rule in ActiveRulesForMap(pawn.Map))
            {
                if (JobTargetsArea(job, rule.Area))
                    matches.Add(rule);
            }

            return matches;
        }

        public static bool MatchesRule(Pawn pawn, Job job, ApparelRule rule)
        {
            return pawn != null &&
                   job != null &&
                   pawn.Map != null &&
                   PawnAccessClassifier.IsApparelEligibleHuman(pawn) &&
                   !pawn.Drafted &&
                   !pawn.Downed &&
                   rule != null &&
                   rule.Enabled &&
                   !rule.WorkAreaPaused &&
                   rule.Area != null &&
                   rule.Area.Map == pawn.Map &&
                   JobTargetsArea(job, rule.Area);
        }

        public static List<ApparelRule> MatchingLocationRules(Pawn pawn)
        {
            if (pawn?.Map == null ||
                !PawnAccessClassifier.IsApparelEligibleHuman(pawn) ||
                pawn.Drafted || pawn.Downed)
            {
                return new List<ApparelRule>();
            }

            if (!pawn.Position.IsValid ||
                !pawn.Position.InBounds(pawn.Map))
            {
                return new List<ApparelRule>();
            }

            // Location is an independent safety signal. A pawn who is already
            // inside a live work area still needs its complete requirement when
            // the native thinker switches from work to eating, recreation,
            // waiting, sleep, or another job whose target is elsewhere.
            var matches = new List<ApparelRule>();
            foreach (ApparelRule rule in ActiveRulesForMap(pawn.Map))
            {
                if (rule.Area[pawn.Position])
                    matches.Add(rule);
            }

            return matches;
        }

        public static List<ApparelRule> MatchingRuntimeRules(Pawn pawn, Job job)
        {
            var matches = new List<ApparelRule>();
            if (pawn?.Map == null ||
                !PawnAccessClassifier.IsApparelEligibleHuman(pawn) ||
                pawn.Drafted || pawn.Downed)
            {
                return matches;
            }

            IReadOnlyList<ApparelRule> activeRules = ActiveRulesForMap(pawn.Map);
            if (activeRules.Count == 0)
                return matches;

            if (pawn.Position.IsValid && pawn.Position.InBounds(pawn.Map))
            {
                foreach (ApparelRule rule in activeRules)
                {
                    if (rule.Area[pawn.Position])
                        AddUniqueRule(matches, rule);
                }
            }

            // Occupancy is authoritative while leaving a protected area. Only
            // inspect the running job's targets after the pawn is outside every
            // live area, matching the existing runtime enforcement contract.
            if (matches.Count > 0 || job == null)
                return matches;

            foreach (ApparelRule rule in activeRules)
            {
                if (JobTargetsArea(job, rule.Area))
                    AddUniqueRule(matches, rule);
            }

            return matches;
        }

        private static void AddUniqueRule(
            List<ApparelRule> rules, ApparelRule candidate)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i]?.Id == candidate?.Id)
                    return;
            }

            rules.Add(candidate);
        }

        public static List<ThingDef> MissingRequiredApparel(Pawn pawn, ApparelRule rule)
        {
            if (pawn?.apparel == null || rule?.RequiredApparel == null)
                return new List<ThingDef>();

            return rule.RequiredApparel
                .Where(def => def != null && !pawn.apparel.WornApparel.Any(
                    apparel => apparel?.def == def && rule.Allows(apparel)))
                .Distinct()
                .ToList();
        }

        public static bool HasMissingRequiredApparel(Pawn pawn, ApparelRule rule)
        {
            if (pawn?.apparel == null || rule?.RequiredApparel == null)
                return false;

            foreach (ThingDef required in rule.RequiredApparel)
            {
                if (required == null)
                    continue;

                bool worn = false;
                foreach (Apparel apparel in pawn.apparel.WornApparel)
                {
                    if (apparel?.def == required && rule.Allows(apparel))
                    {
                        worn = true;
                        break;
                    }
                }

                if (!worn)
                    return true;
            }

            return false;
        }

        public static bool HasMissingRequiredGear(Pawn pawn, ApparelRule rule) =>
            HasMissingRequiredApparel(pawn, rule) ||
            (AutomaticOutfitManagerGameComponent.Current?
                 .StateFor(pawn)?.WeaponRuleOverrideExplicit != true &&
             HasMissingRequiredWeapon(pawn, rule));

        public static bool HasMissingRequiredWeapon(Pawn pawn, ApparelRule rule)
        {
            if (rule?.HasWeaponRequirement != true)
                return false;

            return !WeaponMatchesRequirement(pawn?.equipment?.Primary, rule);
        }

        public static bool WeaponMatchesRequirement(
            ThingWithComps weapon, ApparelRule rule)
        {
            if (rule?.HasWeaponRequirement != true)
                return true;
            if (weapon?.def?.IsWeapon != true)
                return false;
            if (!rule.AllowsWeapon(weapon))
                return false;

            if (rule.UsesExactWeapons)
            {
                return rule.RequiredWeapons.Any(def =>
                    def != null && weapon.def == def);
            }

            return WeaponMatchesRequirement(weapon, rule.RequiredWeapon);
        }

        public static bool WeaponDefMatchesRequirement(
            ThingDef def, ApparelRule rule)
        {
            if (rule?.HasWeaponRequirement != true)
                return true;
            if (def?.IsWeapon != true)
                return false;
            if (rule.UsesExactWeapons)
                return rule.RequiredWeapons.Contains(def);
            return WeaponDefMatchesRequirement(def, rule.RequiredWeapon);
        }

        public static bool WeaponMatchesRequirement(
            ThingWithComps weapon, WeaponRequirement requirement)
        {
            if (requirement == WeaponRequirement.None)
                return true;
            if (weapon?.def?.IsWeapon != true)
                return false;

            return WeaponDefMatchesRequirement(weapon.def, requirement);
        }

        public static bool WeaponDefMatchesRequirement(
            ThingDef def, WeaponRequirement requirement)
        {
            if (requirement == WeaponRequirement.None)
                return true;
            if (def?.IsWeapon != true)
                return false;

            switch (requirement)
            {
                case WeaponRequirement.Melee:
                    return def.IsMeleeWeapon;
                case WeaponRequirement.Ranged:
                    return def.IsRangedWeapon;
                case WeaponRequirement.Either:
                    return def.IsMeleeWeapon || def.IsRangedWeapon;
                default:
                    return false;
            }
        }

        public static bool TryCombinedWeaponRequirement(
            IEnumerable<ApparelRule> rules, out CombinedWeaponRequirement requirement)
        {
            requirement = new CombinedWeaponRequirement();

            foreach (ApparelRule rule in rules ?? Enumerable.Empty<ApparelRule>())
            {
                if (rule?.HasWeaponRequirement != true)
                    continue;

                requirement.HasRequirement = true;
                requirement.Standards.Add(rule);
                if (rule.UsesExactWeapons)
                {
                    var exact = new HashSet<ThingDef>(rule.RequiredWeapons
                        .Where(def => def?.IsWeapon == true));
                    if (requirement.ExactDefs == null)
                        requirement.ExactDefs = exact;
                    else
                        requirement.ExactDefs.IntersectWith(exact);
                }
                else
                {
                    if (!TryCombineLegacyCategory(
                            requirement.LegacyCategory,
                            rule.RequiredWeapon,
                            out WeaponRequirement combinedCategory))
                    {
                        return false;
                    }
                    requirement.LegacyCategory = combinedCategory;
                }
            }

            if (requirement.ExactDefs != null)
            {
                WeaponRequirement category = requirement.LegacyCategory;
                requirement.ExactDefs.RemoveWhere(def =>
                    !WeaponDefMatchesRequirement(
                        def, category));
                if (requirement.ExactDefs.Count == 0)
                    return false;
            }

            if (requirement.Standards.Count > 1)
            {
                float minimumHitPoints = requirement.Standards.Max(rule =>
                    rule.AllowedWeaponHitPoints.min);
                float maximumHitPoints = requirement.Standards.Min(rule =>
                    rule.AllowedWeaponHitPoints.max);
                int minimumQuality = requirement.Standards.Max(rule =>
                    (int)rule.AllowedWeaponQuality.min);
                int maximumQuality = requirement.Standards.Min(rule =>
                    (int)rule.AllowedWeaponQuality.max);
                if (minimumHitPoints > maximumHitPoints + 0.0001f)
                {
                    return false;
                }

                // Qualityless weapons remain eligible for every quality range,
                // matching AllowsWeapon. Disjoint quality sliders are therefore
                // incompatible only when every permitted weapon definition has
                // a quality component.
                CombinedWeaponRequirement combinedRequirement = requirement;
                if (minimumQuality > maximumQuality &&
                    !DefDatabase<ThingDef>.AllDefsListForReading.Any(def =>
                        combinedRequirement.Matches(def) &&
                        !def.HasComp(typeof(CompQuality))))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryCombineLegacyCategory(
            WeaponRequirement left,
            WeaponRequirement right,
            out WeaponRequirement combined)
        {
            if (left == WeaponRequirement.None)
            {
                combined = right;
                return true;
            }
            if (right == WeaponRequirement.None || left == right)
            {
                combined = left;
                return true;
            }
            if (left == WeaponRequirement.Either)
            {
                combined = right;
                return true;
            }
            if (right == WeaponRequirement.Either)
            {
                combined = left;
                return true;
            }

            combined = WeaponRequirement.None;
            return false;
        }

        public static bool RuleCanApplyToPawn(Pawn pawn, ApparelRule rule)
        {
            if (pawn == null || pawn.RaceProps?.Humanlike != true || pawn.apparel == null ||
                rule == null ||
                (rule.HasWeaponRequirement && pawn.equipment == null))
                return false;

            foreach (ThingDef def in rule.RequiredApparel ?? Enumerable.Empty<ThingDef>())
            {
                if (def?.apparel == null)
                    continue;
                if (!ApparelUtility.HasPartsToWear(pawn, def) ||
                    (def.apparel.developmentalStageFilter & pawn.DevelopmentalStage) == 0)
                    return false;
            }

            return true;
        }

        public static bool JobTargetsArea(Job job, Area area)
        {
            if (job == null)
                return false;

            return TargetInside(job.targetA, area) ||
                   TargetInside(job.targetB, area) ||
                   TargetInside(job.targetC, area) ||
                   TargetsInside(job.targetQueueA, area) ||
                   TargetsInside(job.targetQueueB, area);
        }

        private static bool TargetsInside(
            IEnumerable<LocalTargetInfo> targets, Area area) =>
            targets != null && targets.Any(target => TargetInside(target, area));

        private static bool TargetInside(LocalTargetInfo target, Area area)
        {
            if (!target.IsValid || area == null)
                return false;

            if (target.HasThing)
            {
                var thing = target.Thing;
                if (thing == null || thing.MapHeld != area.Map)
                    return false;

                // Work-area membership follows the target and its occupied
                // footprint. Do not classify a job by every possible adjacent
                // interaction cell: a frame just outside a nested area can have
                // one candidate standing cell inside it even when RimWorld
                // chooses another, producing false nested-rule status and
                // unnecessary outfitting. Actual entry and transit remain
                // protected by the path-cell safety patch.
                if (CellInside(thing.PositionHeld, area))
                    return true;

                // Held or carried targets can have MapHeld/PositionHeld through
                // their holder while their direct Map is null. RimWorld's
                // InteractionCells calculation requires a spawned thing/map.
                if (!thing.Spawned || thing.Map != area.Map)
                    return false;

                return GenAdj.CellsOccupiedBy(thing)
                    .Any(cell => CellInside(cell, area));
            }

            return CellInside(target.Cell, area);
        }

        private static bool CellInside(IntVec3 cell, Area area) =>
            area?.Map != null && cell.IsValid && cell.InBounds(area.Map) && area[cell];
    }
}
