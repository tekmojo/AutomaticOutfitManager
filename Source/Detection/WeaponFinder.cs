using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Rules;
using RimWorld;
using Verse;
using AutomaticOutfitManager.Patches;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    public static class WeaponFinder
    {
        public static ThingWithComps FindBest(
            Pawn pawn,
            CombinedWeaponRequirement requirement,
            Area changingArea = null,
            ISet<Thing> excludedThings = null)
        {
            if (pawn?.Map == null || requirement?.HasRequirement != true)
                return null;

            ThingDef preferredDef = PreferredExactDefinition(
                pawn, requirement, out bool? preferredRanged);
            if (changingArea != null)
            {
                ThingWithComps preferred = FindClosest(
                    pawn, requirement, changingArea, preferredDef,
                    excludedThings: excludedThings);
                if (preferred != null)
                    return ReportSelection(
                        pawn, preferred, preferredRanged, "locker");

                if (preferredRanged.HasValue)
                {
                    ThingWithComps categoryPreferred = FindClosest(
                        pawn,
                        requirement,
                        changingArea,
                        preferredRanged: preferredRanged,
                        excludedThings: excludedThings);
                    if (categoryPreferred != null)
                    {
                        return ReportSelection(
                            pawn,
                            categoryPreferred,
                            preferredRanged,
                            "locker");
                    }
                }
            }

            if (preferredDef != null)
            {
                ThingWithComps mapPreferred = FindClosest(
                    pawn, requirement, null, preferredDef,
                    excludedThings: excludedThings);
                if (mapPreferred != null)
                    return ReportSelection(
                        pawn, mapPreferred, preferredRanged, "map");
            }

            if (preferredRanged.HasValue)
            {
                ThingWithComps categoryPreferred = FindClosest(
                    pawn,
                    requirement,
                    null,
                    preferredRanged: preferredRanged,
                    excludedThings: excludedThings);
                if (categoryPreferred != null)
                {
                    return ReportSelection(
                        pawn,
                        categoryPreferred,
                        preferredRanged,
                        "map");
                }
            }

            // Only cross to the pawn's weaker combat category after every
            // reachable, equippable selection from the preferred category has
            // been exhausted. Locker proximity must not reverse the skill
            // preference.
            if (changingArea != null)
            {
                ThingWithComps lockerFallback = FindClosest(
                    pawn, requirement, changingArea,
                    excludedThings: excludedThings);
                if (lockerFallback != null)
                {
                    return ReportSelection(
                        pawn, lockerFallback, preferredRanged, "locker fallback");
                }
            }

            ThingWithComps mapFallback = FindClosest(
                pawn, requirement, null, excludedThings: excludedThings);
            return ReportSelection(
                pawn, mapFallback, preferredRanged, "map fallback");
        }

        private static ThingWithComps FindClosest(
            Pawn pawn,
            CombinedWeaponRequirement requirement,
            Area area,
            ThingDef exactDef = null,
            bool? preferredRanged = null,
            ISet<Thing> excludedThings = null)
        {
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Weapon),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                thing => thing is ThingWithComps weapon &&
                         (excludedThings == null || !excludedThings.Contains(weapon)) &&
                         weapon.Spawned &&
                         requirement.Matches(weapon) &&
                         (exactDef == null || weapon.def == exactDef) &&
                         (!preferredRanged.HasValue ||
                          (preferredRanged.Value
                              ? weapon.def.IsRangedWeapon
                              : weapon.def.IsMeleeWeapon)) &&
                         (area == null ||
                          (area.Map == weapon.Map && area[weapon.Position])) &&
                         !weapon.IsForbidden(pawn) &&
                         CanUseWithoutTakingPlayerOwnership(weapon, pawn) &&
                         EquipmentUtility.CanEquip(weapon, pawn) &&
                         AutomaticOutfitManagerGameComponent.Current?
                             .IsSavedWeaponForOtherPawn(weapon, pawn) != true &&
                         AutomaticOutfitManagerGameComponent.Current?
                             .IsManagedWeaponAssignedToOtherPawn(weapon, pawn) != true &&
                         AutomaticOutfitManagerGameComponent.Current?
                             .StateFor(pawn)?.IsTemporarilyRejectedWeapon(weapon) != true &&
                         ReservationUtility_SavedApparel_Patch
                             .CanReserveForOutfit(pawn, weapon)) as ThingWithComps;
        }

        private static ThingDef PreferredExactDefinition(
            Pawn pawn,
            CombinedWeaponRequirement requirement,
            out bool? preferredRanged)
        {
            preferredRanged = null;
            if (pawn == null || requirement?.ExactDefs == null ||
                requirement.ExactDefs.Count <= 1)
            {
                return null;
            }

            // Multiple exact selections are alternatives. Prefer the category
            // matching the pawn's stronger combat skill, then rotate the stable
            // alphabetical order by pawn ID so one storage row cannot monopolize
            // every assignment. If the preferred definition is unavailable,
            // FindBest falls back to any selected weapon.
            var alternatives = requirement.ExactDefs
                .Where(def => def?.IsWeapon == true)
                .OrderBy(def => def.defName)
                .ToList();
            if (alternatives.Count <= 1)
                return alternatives.FirstOrDefault();

            int shooting = CombatSkillLevel(pawn, SkillDefOf.Shooting);
            int melee = CombatSkillLevel(pawn, SkillDefOf.Melee);
            bool hasRanged = alternatives.Any(def => def.IsRangedWeapon);
            bool hasMelee = alternatives.Any(def => def.IsMeleeWeapon);
            if (shooting != melee && hasRanged && hasMelee)
            {
                bool preferRanged = shooting > melee;
                preferredRanged = preferRanged;
                var skillMatched = alternatives.Where(def =>
                        preferRanged ? def.IsRangedWeapon : def.IsMeleeWeapon)
                    .ToList();
                if (skillMatched.Count > 0)
                    alternatives = skillMatched;
            }

            int index = (pawn.thingIDNumber & int.MaxValue) % alternatives.Count;
            return alternatives[index];
        }

        private static ThingWithComps ReportSelection(
            Pawn pawn,
            ThingWithComps weapon,
            bool? preferredRanged,
            string source)
        {
            if (weapon == null || !AomLog.DetailedEnabled)
                return weapon;

            int shooting = CombatSkillLevel(pawn, SkillDefOf.Shooting);
            int melee = CombatSkillLevel(pawn, SkillDefOf.Melee);
            string preference = preferredRanged.HasValue
                ? preferredRanged.Value ? "ranged" : "melee"
                : "stable exact selection";
            bool usedWeakerCategory = preferredRanged.HasValue &&
                (preferredRanged.Value
                    ? !weapon.def.IsRangedWeapon
                    : !weapon.def.IsMeleeWeapon);
            string fallback = usedWeakerCategory
                ? "; preferred category had no reachable, equippable selection"
                : string.Empty;

            AomLog.Detailed(
                $"[AutomaticOutfitManager] {pawn.LabelShortCap}: selected " +
                $"{weapon.LabelCap} [{weapon.def.defName}] from {source}; " +
                $"Shooting {shooting}, Melee {melee}, preference {preference}" +
                $"{fallback}.");
            return weapon;
        }

        private static int CombatSkillLevel(Pawn pawn, SkillDef skillDef)
        {
            SkillRecord skill = pawn?.skills?.GetSkill(skillDef);
            return skill == null || skill.TotallyDisabled ? -1 : skill.Level;
        }

        private static bool CanUseWithoutTakingPlayerOwnership(
            ThingWithComps weapon, Pawn pawn)
        {
            // Never automatically create a persona/bladelink bond. RimWorld
            // 1.6 gives ordinary craftable guns CompBiocodable through
            // BaseHumanMakeableGun, so the mere presence of that component is
            // not an ownership claim. An unbound ordinary weapon is safe; an
            // already biocoded weapon is usable only by its coded pawn.
            if (weapon.TryGetComp<CompBladelinkWeapon>() != null)
                return false;

            CompBiocodable biocodable = weapon.TryGetComp<CompBiocodable>();
            return biocodable == null || !biocodable.Biocoded ||
                   biocodable.CodedPawn == pawn;
        }
    }
}
