using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using UnityEngine;
using Verse;

namespace AutomaticOutfitManager.UI
{
    public sealed class WeaponSelectionWindow : Window
    {
        private const float RowHeight = 32f;

        private readonly ApparelRule rule;
        private readonly List<ThingDef> weaponDefs;
        private readonly List<ApparelRule> overlappingRules;
        private readonly List<ThingDef> filteredDefs = new List<ThingDef>();
        private Vector2 scrollPosition;
        private string searchText = "";
        private string filteredSearchText;
        private bool filteredDefsDirty = true;

        public WeaponSelectionWindow(ApparelRule rule)
        {
            this.rule = rule;
            overlappingRules = ApparelCompatibility.OverlappingRules(rule)
                .Where(candidate => !ReferenceEquals(candidate, rule))
                .ToList();
            weaponDefs = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def?.IsWeapon == true &&
                              (def.IsMeleeWeapon || def.IsRangedWeapon))
                .OrderBy(def => def.LabelCap.ToString())
                .ToList();

            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(640f, 700f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f),
                "Choose primary weapons");
            Text.Font = GameFont.Small;

            Rect searchRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, 30f);
            searchText = Widgets.TextField(searchRect, searchText ?? "");
            TooltipHandler.TipRegion(searchRect,
                "Search by the weapon's displayed name or technical DefName.");

            List<ThingDef> filtered = FilteredDefs();
            Rect countRect = new Rect(inRect.x, searchRect.yMax + 6f, inRect.width, 24f);
            string selectionHint = rule.HasWeaponRequirement
                ? "selected weapons are alternatives"
                : "any weapon allowed";
            Widgets.Label(countRect, $"{filtered.Count} weapon(s) — {selectionHint}");
            TooltipHandler.TipRegion(countRect,
                "A pawn equips one green primary-weapon alternative before work. When both types are selected, higher Shooting prefers ranged and higher Melee prefers melee. The locker and then the map are searched for that type before falling back to the weaker category; tied pawns are distributed across valid selections. Cyan entries are retained managed stock. With nothing selected, there is no primary-weapon requirement: a pawn may remain unarmed or keep any current weapon. Inventory sidearms do not count.");

            Rect outRect = new Rect(
                inRect.x, countRect.yMax + 4f,
                inRect.width, inRect.yMax - countRect.yMax - 4f);
            Rect viewRect = new Rect(
                0f, 0f, outRect.width - 18f, filtered.Count * RowHeight);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            for (int i = 0; i < filtered.Count; i++)
                DrawWeaponRow(filtered[i],
                    new Rect(0f, i * RowHeight, viewRect.width, RowHeight));
            Widgets.EndScrollView();
        }

        private List<ThingDef> FilteredDefs()
        {
            string query = (searchText ?? "").Trim();
            if (!filteredDefsDirty &&
                string.Equals(query, filteredSearchText, StringComparison.Ordinal))
            {
                return filteredDefs;
            }

            filteredDefs.Clear();
            AppendFilteredDefs(query, selected: true, retainedStock: false);
            AppendFilteredDefs(query, selected: false, retainedStock: true);
            AppendFilteredDefs(query, selected: false, retainedStock: false);
            filteredSearchText = query;
            filteredDefsDirty = false;
            return filteredDefs;
        }

        private void AppendFilteredDefs(
            string query, bool selected, bool retainedStock)
        {
            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            foreach (ThingDef def in weaponDefs)
            {
                bool isSelected = rule.RequiredWeapons?.Contains(def) == true;
                if (isSelected != selected)
                    continue;
                if (!selected &&
                    (component?.IsManagedWeaponDefinition(def) == true) != retainedStock)
                    continue;

                if (query.Length == 0 ||
                    def.LabelCap.ToString().IndexOf(
                        query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    def.defName.IndexOf(
                        query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    filteredDefs.Add(def);
                }
            }
        }

        private void DrawWeaponRow(ThingDef def, Rect rect)
        {
            if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);

            bool selected = rule.RequiredWeapons?.Contains(def) == true;
            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            bool retainedStock = !selected &&
                component?.IsManagedWeaponDefinition(def) == true;
            bool conflict = !selected && ConflictsIfAdded(def);
            string type = def.IsMeleeWeapon ? "melee" : "ranged";
            string label = $"{def.LabelCap} [{def.defName}] — {type}";
            Color previousColor = GUI.color;
            if (selected)
                GUI.color = Color.green;
            else if (retainedStock)
                GUI.color = Color.cyan;
            float reservedButtonWidth = retainedStock ? 188f : 100f;
            Rect labelRect = new Rect(
                rect.x + 4f, rect.y + 5f,
                rect.width - reservedButtonWidth, 24f);
            Widgets.Label(labelRect, label);
            GUI.color = previousColor;
            if (selected)
            {
                TooltipHandler.TipRegion(
                    new Rect(rect.x + 4f, rect.y, rect.width - 100f, rect.height),
                    "Selected for this rule. Green weapons are alternatives; a managed worker equips one acceptable primary weapon, not every selected weapon.");
            }
            else if (retainedStock)
            {
                TooltipHandler.TipRegion(
                    new Rect(rect.x + 4f, rect.y, rect.width - 188f, rect.height),
                    "Retained managed weapon stock. It remains classified for Automatic Outfit Manager locker storage even though this rule does not currently require it.");
            }

            Rect buttonRect = new Rect(rect.xMax - 88f, rect.y + 2f, 84f, 27f);
            if (selected)
            {
                if (Widgets.ButtonText(buttonRect, "Remove"))
                {
                    component?.RememberManagedStockDefinition(def);
                    rule.RequiredWeapons.Remove(def);
                    filteredDefsDirty = true;
                    component?.NotifyRuleRequirementsChanged(
                        rule.Id, $"removed weapon {def.LabelCap}");
                }
                TooltipHandler.TipRegion(buttonRect,
                    "Stop accepting this weapon for this rule. Its type remains retained managed stock until Forget is used when no rule or active transition still needs it.");
            }
            else
            {
                Rect addRect = retainedStock
                    ? new Rect(rect.xMax - 176f, rect.y + 2f, 84f, 27f)
                    : buttonRect;
                if (conflict)
                {
                    bool previousEnabled = GUI.enabled;
                    GUI.enabled = false;
                    Widgets.ButtonText(addRect, "Conflict");
                    GUI.enabled = previousEnabled;
                    TooltipHandler.TipRegion(addRect,
                        "This weapon cannot satisfy the exact primary-weapon requirements of the overlapping work areas.");
                }
                else if (Widgets.ButtonText(addRect, "Add"))
                {
                    rule.UseExactWeapons();
                    if (!rule.RequiredWeapons.Contains(def))
                    {
                        rule.RequiredWeapons.Add(def);
                        component?.RememberManagedStockDefinition(def);
                        component?.InvalidateManagedWeaponDefinitionIndex();
                        filteredDefsDirty = true;
                    }
                }
                if (!conflict)
                {
                    TooltipHandler.TipRegion(addRect,
                        retainedStock
                            ? "Add this retained weapon type back as an acceptable primary-weapon alternative."
                            : "Add this exact weapon type as an acceptable primary-weapon alternative for this rule.");
                }

                if (retainedStock)
                {
                    bool canForget = component?.CanForgetManagedStockDefinition(def) == true;
                    bool previousEnabled = GUI.enabled;
                    GUI.enabled = canForget;
                    if (Widgets.ButtonText(buttonRect, "Forget"))
                    {
                        component?.ForgetManagedStockDefinition(def);
                        filteredDefsDirty = true;
                    }
                    GUI.enabled = previousEnabled;
                    TooltipHandler.TipRegion(buttonRect,
                        canForget
                            ? "Stop classifying spare copies of this weapon type as managed Automatic Outfit Manager stock. Exact saved or currently tracked weapons remain protected individually."
                            : component?.ManagedStockForgetBlockReason(def) ??
                              "This weapon type is still required by another rule or used by an active outfit transition.");
                }
            }
        }

        private bool ConflictsIfAdded(ThingDef candidate)
        {
            List<ApparelRule> restrictedOverlaps = overlappingRules
                .Where(other => other?.HasWeaponRequirement == true)
                .ToList();
            if (restrictedOverlaps.Count == 0)
                return false;

            IEnumerable<ThingDef> candidates =
                (rule.RequiredWeapons ?? new List<ThingDef>())
                .Where(def => def?.IsWeapon == true)
                .Concat(new[] { candidate })
                .Distinct();
            return !candidates.Any(def => restrictedOverlaps.All(other =>
                RuleEvaluator.WeaponDefMatchesRequirement(def, other)));
        }
    }
}
