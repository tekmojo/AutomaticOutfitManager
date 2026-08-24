using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Rules;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    public sealed class CombinedWeaponRequirement
    {
        public bool HasRequirement;
        public WeaponRequirement LegacyCategory;
        public HashSet<ThingDef> ExactDefs;

        public bool Matches(ThingWithComps weapon)
        {
            if (!HasRequirement)
                return true;
            if (weapon?.def?.IsWeapon != true)
                return false;
            if (ExactDefs != null && !ExactDefs.Contains(weapon.def))
                return false;
            return RuleEvaluator.WeaponMatchesRequirement(
                weapon, LegacyCategory);
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
        public static ApparelRule MatchingRule(Pawn pawn, Job job)
        {
            return MatchingRules(pawn, job).FirstOrDefault();
        }

        public static List<ApparelRule> MatchingRules(Pawn pawn, Job job)
        {
            if (pawn == null || job == null || pawn.Map == null ||
                !PawnAccessClassifier.IsApparelEligibleHuman(pawn) || pawn.Drafted)
                return new List<ApparelRule>();

            var component = AutomaticOutfitManagerGameComponent.Current;
            if (component?.Rules == null)
                return new List<ApparelRule>();

            // A target may be covered by an outer work area and one or more
            // nested areas. All of their safety requirements apply. Preserve
            // rule order so existing saves remain deterministic.
            return component.Rules
                .Where(rule => MatchesRule(pawn, job, rule))
                .ToList();
        }

        public static bool MatchesRule(Pawn pawn, Job job, ApparelRule rule)
        {
            return pawn != null &&
                   job != null &&
                   pawn.Map != null &&
                   PawnAccessClassifier.IsApparelEligibleHuman(pawn) &&
                   !pawn.Drafted &&
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
                !PawnAccessClassifier.IsApparelEligibleHuman(pawn) || pawn.Drafted)
            {
                return new List<ApparelRule>();
            }

            var component = AutomaticOutfitManagerGameComponent.Current;
            if (component?.Rules == null || !pawn.Position.IsValid ||
                !pawn.Position.InBounds(pawn.Map))
            {
                return new List<ApparelRule>();
            }

            // Location is an independent safety signal. A pawn who is already
            // inside a live work area still needs its complete requirement when
            // the native thinker switches from work to eating, recreation,
            // waiting, sleep, or another job whose target is elsewhere.
            return component.Rules
                .Where(rule => rule != null && rule.Enabled &&
                               !rule.WorkAreaPaused && rule.Area?.Map == pawn.Map &&
                               rule.Area[pawn.Position])
                .ToList();
        }

        public static List<ThingDef> MissingRequiredApparel(Pawn pawn, ApparelRule rule)
        {
            if (pawn?.apparel == null || rule?.RequiredApparel == null)
                return new List<ThingDef>();

            return rule.RequiredApparel
                .Where(def => def != null && !pawn.apparel.WornApparel.Any(a => a.def == def))
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
                    if (apparel?.def == required)
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
                 .StateFor(pawn)?.WeaponPlayerOverride != true &&
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
