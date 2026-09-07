using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using RimWorld;
using Verse;

namespace AutomaticOutfitManager.State
{
    // A preference, not a permanent stock reservation. Active transitions still
    // own exact personal items through PawnApparelState and the existing indexes.
    public sealed class SavedNonWorkOutfit : IExposable
    {
        public Pawn Pawn;
        public List<Apparel> Apparel = new List<Apparel>();
        public ThingWithComps Weapon;
        // Issuance history survives a partial return, independently of the
        // temporary restoration state. It never changes the personal snapshot.
        public List<WorkGearSource> WorkGear = new List<WorkGearSource>();
        // Read the retired prototype flag so manual selections cannot masquerade
        // as automatic pre-work snapshots when upgrading a test save.
        public bool RetiredManualSnapshot;

        public static SavedNonWorkOutfit Capture(Pawn pawn, PawnApparelState state = null) =>
            new SavedNonWorkOutfit
            {
                Pawn = pawn,
                Apparel = (state?.ApparelInterventionActive == true
                    ? state.OriginalApparel : pawn.apparel.WornApparel).ToList(),
                Weapon = state?.WeaponInterventionActive == true
                    ? state.OriginalWeapon : pawn.equipment?.Primary
            };

        public static SavedNonWorkOutfit FromExistingWorkSession(
            PawnApparelState state, ApparelRule rule)
        {
            if (state?.Pawn?.apparel == null || state.NonWorkFallbackActive ||
                !string.IsNullOrEmpty(state.NonWorkRestorationRuleId) ||
                rule?.IsNonWork != false ||
                (!state.ApparelInterventionActive && !state.WeaponInterventionActive))
                return null;
            return Capture(state.Pawn, state);
        }

        public void RecordIssuedGear(PawnApparelState state, IEnumerable<Apparel> apparel,
            ThingWithComps weapon, IEnumerable<ApparelRule> sources)
        {
            var work = sources.Where(rule => rule != null && !rule.IsNonWork).ToList();
            foreach (ThingWithComps item in apparel.Cast<ThingWithComps>()
                         .Concat(new[] { weapon }).Where(item => item != null))
            {
                // Reused personal garments are not issued work gear.
                if (item is RimWorld.Apparel garment && !state.ManagedApparel.Contains(garment)) continue;
                if (!(item is RimWorld.Apparel) &&
                    (!state.ManagedWeapons.Contains(item) || item == Weapon)) continue;
                var ids = work.Where(rule => item is RimWorld.Apparel
                    ? rule.RequiredApparel.Contains(item.def)
                    : rule.HasWeaponRequirement && RuleEvaluator.WeaponMatchesRequirement(item, rule))
                    .Select(rule => rule.Id).ToList();
                if (ids.Count == 0) continue;
                var entry = WorkGear.FirstOrDefault(record => record.Item == item);
                if (entry == null)
                {
                    entry = new WorkGearSource { Item = item };
                    WorkGear.Add(entry);
                }
                foreach (string id in ids)
                    if (!entry.RuleIds.Contains(id)) entry.RuleIds.Add(id);
            }
        }

        public bool ApparelSatisfied(Pawn pawn) => pawn?.apparel != null &&
            Apparel.Where(item => item != null && !item.Destroyed)
                .All(pawn.apparel.WornApparel.Contains) &&
            pawn.apparel.WornApparel.All(Apparel.Contains);

        public bool WeaponSatisfied(Pawn pawn) =>
            pawn?.equipment?.Primary == (Weapon?.Destroyed == true ? null : Weapon);

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn");
            Scribe_Collections.Look(ref Apparel, "apparel", LookMode.Reference);
            Scribe_References.Look(ref Weapon, "weapon");
            Scribe_Collections.Look(ref WorkGear, "workGearSources", LookMode.Deep);
            WorkGear ??= new List<WorkGearSource>();
            Scribe_Values.Look(ref RetiredManualSnapshot, "explicitlySaved", false);
            Apparel ??= new List<Apparel>();
        }
    }
    public sealed class WorkGearSource : IExposable
    {
        public ThingWithComps Item;
        public List<string> RuleIds = new List<string>();
        public void ExposeData()
        {
            Scribe_References.Look(ref Item, "item");
            Scribe_Collections.Look(ref RuleIds, "ruleIds", LookMode.Value);
            RuleIds ??= new List<string>();
        }
    }

    public static class NonWorkOutfitPolicy
    {
        public static bool IsIssued(Pawn pawn, ThingWithComps item)
        {
            if (item == null) return false;
            var component = AutomaticOutfitManagerGameComponent.Current;
            var saved = component?.NonWorkOutfitFor(pawn);
            if (saved != null && (saved.Weapon == item ||
                (item is Apparel original && saved.Apparel.Contains(original)))) return false;
            if (saved?.WorkGear.Any(entry => entry.Item == item) == true) return true;
            var state = component?.StateFor(pawn);
            // Older saves lack issuance history. All still works; a specific
            // selection conservatively keeps unattributed gear.
            return state != null && component.RuleById(state.ActiveRuleId)?.IsNonWork != true &&
                ((item is Apparel apparel && state.ManagedApparel.Contains(apparel)) ||
                 state.ManagedWeapons.Contains(item));
        }

        public static bool ShouldReturn(Pawn pawn, ApparelRule rule, ThingWithComps item)
        {
            if (!IsIssued(pawn, item)) return false;
            if (rule.RemoveAllWorkOutfits) return true;
            var entry = AutomaticOutfitManagerGameComponent.Current.NonWorkOutfitFor(pawn)?
                .WorkGear.FirstOrDefault(source => source.Item == item);
            return entry?.RuleIds.Count > 0 && entry.RuleIds.All(rule.WorkOutfitsToRemove.Contains);
        }

        public static bool Keep(Pawn pawn, ApparelRule rule, ThingWithComps item) =>
            IsIssued(pawn, item) && !ShouldReturn(pawn, rule, item);

        public static bool NeedsStateHandoff(Pawn pawn, ApparelRule rule)
        {
            if (rule.RemoveAllWorkOutfits) return false;
            var component = AutomaticOutfitManagerGameComponent.Current;
            var state = component?.StateFor(pawn);
            return state != null && string.IsNullOrEmpty(state.NonWorkRestorationRuleId) &&
                component.RuleById(state.ActiveRuleId)?.IsNonWork != true &&
                (state.ApparelInterventionActive || state.WeaponInterventionActive) &&
                (pawn.apparel.WornApparel.Any(item => Keep(pawn, rule, item)) ||
                 Keep(pawn, rule, pawn.equipment?.Primary));
        }

        public static SavedNonWorkOutfit Target(Pawn pawn, ApparelRule rule)
        {
            var component = AutomaticOutfitManagerGameComponent.Current;
            var personal = rule.DefaultToSavedPersonalOutfit ? component?.NonWorkOutfitFor(pawn) : null;
            if (personal != null && rule.RemoveAllWorkOutfits) return personal;
            var retained = pawn.apparel.WornApparel.Where(item => Keep(pawn, rule, item)).ToList();
            return new SavedNonWorkOutfit
            {
                Pawn = pawn,
                Apparel = personal == null
                    ? pawn.apparel.WornApparel.Where(item => !ShouldReturn(pawn, rule, item)).ToList()
                    : personal.Apparel.Where(item => item != null && !item.Destroyed &&
                        retained.All(kept => kept == item || ApparelUtility.CanWearTogether(
                            item.def, kept.def, pawn.RaceProps.body)))
                        .Concat(retained).Distinct().ToList(),
                Weapon = Keep(pawn, rule, pawn.equipment?.Primary) ||
                         component?.StateFor(pawn)?.WeaponRuleOverrideExplicit == true
                    ? pawn.equipment?.Primary
                    : personal != null ? personal.Weapon
                    : ShouldReturn(pawn, rule, pawn.equipment?.Primary) ? null : pawn.equipment?.Primary
            };
        }
    }

}
