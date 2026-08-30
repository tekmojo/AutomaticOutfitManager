using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace AutomaticOutfitManager.Rules
{
    public enum WeaponRequirement
    {
        None,
        Melee,
        Ranged,
        Either
    }

    public sealed class ApparelRule : IExposable
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "New Outfit Rule";
        public bool Enabled = true;
        public bool UiCollapsed;
        public bool WorkAreaPaused;
        public bool AllowColonistWork = true;
        public bool AllowRobotWork = true;
        public bool AllowAnimalWork = true;
        public bool AllowGuestWork = true;
        public bool AllowSlaveWork = true;
        public bool AllowPrisonerWork;
        public bool AllowColonistHauling = true;
        public bool AllowRobotHauling = true;
        public bool AllowAnimalHauling = true;
        public bool AllowGuestHauling = true;
        public bool AllowSlaveHauling = true;
        public bool AllowPrisonerHauling;
        public bool AllowColonistWandering = true;
        public bool AllowRobotWandering = true;
        public bool AllowAnimalWandering = true;
        public bool AllowGuestWandering = true;
        public bool AllowSlaveWandering = true;
        public bool AllowPrisonerWandering;
        public int ReturnTaskBuffer;
        public bool AllowChildWorkWatching;
        public Area Area;
        public Area ChangingArea;
        public List<ThingDef> RequiredApparel = new List<ThingDef>();
        public FloatRange AllowedApparelHitPoints = FloatRange.ZeroToOne;
        public QualityRange AllowedApparelQuality = QualityRange.All;
        public List<ThingDef> RequiredWeapons = new List<ThingDef>();
        public FloatRange AllowedWeaponHitPoints = FloatRange.ZeroToOne;
        public QualityRange AllowedWeaponQuality = QualityRange.All;
        // Kept for save compatibility with the first 0.3.0 test build. New
        // rules use exact weapon definitions; a loaded category continues to
        // work until the player chooses or clears exact weapons.
        public WeaponRequirement RequiredWeapon;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Id, "id");
            Scribe_Values.Look(ref Name, "name", "New Outfit Rule");
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref UiCollapsed, "uiCollapsed", false);
            Scribe_Values.Look(ref WorkAreaPaused, "workAreaPaused", false);
            Scribe_Values.Look(ref AllowColonistWork, "allowColonistWork", true);
            Scribe_Values.Look(ref AllowRobotWork, "allowRobotWork", true);
            Scribe_Values.Look(ref AllowAnimalWork, "allowAnimalWork", true);
            Scribe_Values.Look(ref AllowGuestWork, "allowGuestWork", true);
            Scribe_Values.Look(ref AllowSlaveWork, "allowSlaveWork", true);
            Scribe_Values.Look(ref AllowPrisonerWork, "allowPrisonerWork", false);
            Scribe_Values.Look(ref AllowColonistHauling, "allowColonistHauling", true);
            Scribe_Values.Look(ref AllowRobotHauling, "allowRobotHauling", true);
            Scribe_Values.Look(ref AllowAnimalHauling, "allowAnimalHauling", true);
            Scribe_Values.Look(ref AllowGuestHauling, "allowGuestHauling", true);
            Scribe_Values.Look(ref AllowSlaveHauling, "allowSlaveHauling", true);
            Scribe_Values.Look(ref AllowPrisonerHauling, "allowPrisonerHauling", false);
            Scribe_Values.Look(ref AllowColonistWandering, "allowColonistWandering", true);
            Scribe_Values.Look(ref AllowRobotWandering, "allowRobotWandering", true);
            Scribe_Values.Look(ref AllowAnimalWandering, "allowAnimalWandering", true);
            Scribe_Values.Look(ref AllowGuestWandering, "allowGuestWandering", true);
            Scribe_Values.Look(ref AllowSlaveWandering, "allowSlaveWandering", true);
            Scribe_Values.Look(ref AllowPrisonerWandering, "allowPrisonerWandering", false);
            Scribe_Values.Look(ref ReturnTaskBuffer, "returnTaskBuffer", 0);
            Scribe_Values.Look(ref AllowChildWorkWatching, "allowChildWorkWatching", false);
            Scribe_References.Look(ref Area, "area");
            Scribe_References.Look(ref ChangingArea, "changingArea");
            Scribe_Collections.Look(ref RequiredApparel, "requiredApparel", LookMode.Def);
            Scribe_Values.Look(
                ref AllowedApparelHitPoints, "allowedApparelHitPoints",
                FloatRange.ZeroToOne);
            Scribe_Values.Look(
                ref AllowedApparelQuality, "allowedApparelQuality",
                QualityRange.All);
            Scribe_Collections.Look(ref RequiredWeapons, "requiredWeapons", LookMode.Def);
            Scribe_Values.Look(
                ref AllowedWeaponHitPoints, "allowedWeaponHitPoints",
                FloatRange.ZeroToOne);
            Scribe_Values.Look(
                ref AllowedWeaponQuality, "allowedWeaponQuality",
                QualityRange.All);
            Scribe_Values.Look(ref RequiredWeapon, "requiredWeapon", WeaponRequirement.None);
            RequiredApparel ??= new List<ThingDef>();
            RequiredWeapons ??= new List<ThingDef>();
            RequiredWeapons.RemoveAll(def => def?.IsWeapon != true);
            ReturnTaskBuffer = System.Math.Max(0, System.Math.Min(20, ReturnTaskBuffer));
            AllowedApparelHitPoints = new FloatRange(
                System.Math.Max(0f, System.Math.Min(1f, AllowedApparelHitPoints.min)),
                System.Math.Max(0f, System.Math.Min(1f, AllowedApparelHitPoints.max)));
            if (AllowedApparelHitPoints.min > AllowedApparelHitPoints.max)
            {
                AllowedApparelHitPoints = new FloatRange(
                    AllowedApparelHitPoints.max, AllowedApparelHitPoints.min);
            }
            AllowedWeaponHitPoints = new FloatRange(
                System.Math.Max(0f, System.Math.Min(1f, AllowedWeaponHitPoints.min)),
                System.Math.Max(0f, System.Math.Min(1f, AllowedWeaponHitPoints.max)));
            if (AllowedWeaponHitPoints.min > AllowedWeaponHitPoints.max)
            {
                AllowedWeaponHitPoints = new FloatRange(
                    AllowedWeaponHitPoints.max, AllowedWeaponHitPoints.min);
            }

            if (string.IsNullOrEmpty(Id))
                Id = Guid.NewGuid().ToString("N");
        }

        public string ApparelSummary => RequiredApparel.Count == 0
            ? "Any apparel"
            : string.Join(", ", RequiredApparel.Where(d => d != null).Select(d => d.LabelCap.ToString()));

        public bool Allows(Apparel apparel)
        {
            if (apparel == null || apparel.Destroyed)
                return false;

            float hitPointPercent = apparel.MaxHitPoints > 0
                ? apparel.HitPoints / (float)apparel.MaxHitPoints
                : 1f;
            if (!AllowedApparelHitPoints.IncludesEpsilon(hitPointPercent))
                return false;

            return !apparel.TryGetQuality(out QualityCategory quality) ||
                   AllowedApparelQuality.Includes(quality);
        }

        public bool HasWeaponRequirement =>
            RequiredWeapons?.Any(def => def?.IsWeapon == true) == true ||
            RequiredWeapon != WeaponRequirement.None;

        public bool AllowsWeapon(ThingWithComps weapon)
        {
            if (weapon?.def?.IsWeapon != true || weapon.Destroyed)
                return false;

            float hitPointPercent = weapon.MaxHitPoints > 0
                ? weapon.HitPoints / (float)weapon.MaxHitPoints
                : 1f;
            if (!AllowedWeaponHitPoints.IncludesEpsilon(hitPointPercent))
                return false;

            return !weapon.TryGetQuality(out QualityCategory quality) ||
                   AllowedWeaponQuality.Includes(quality);
        }

        public bool UsesExactWeapons =>
            RequiredWeapons?.Any(def => def?.IsWeapon == true) == true;

        public string WeaponSummary
        {
            get
            {
                List<ThingDef> exact = (RequiredWeapons ?? new List<ThingDef>())
                    .Where(def => def?.IsWeapon == true)
                    .Distinct()
                    .ToList();
                if (exact.Count > 0)
                {
                    return string.Join(" or ", exact.Select(def => def.LabelCap.ToString()));
                }

                switch (RequiredWeapon)
                {
                    case WeaponRequirement.Melee:
                        return "Legacy: any melee weapon";
                    case WeaponRequirement.Ranged:
                        return "Legacy: any ranged weapon";
                    case WeaponRequirement.Either:
                        return "Legacy: any weapon";
                    default:
                        return "Any weapon";
                }
            }
        }

        public void UseExactWeapons()
        {
            RequiredWeapons ??= new List<ThingDef>();
            RequiredWeapon = WeaponRequirement.None;
        }

        public void ClearWeapons()
        {
            RequiredWeapons?.Clear();
            RequiredWeapon = WeaponRequirement.None;
        }
    }
}
