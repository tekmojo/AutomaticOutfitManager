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
    public sealed class ApparelSelectionWindow : Window
    {
        private const float RowHeight = 32f;

        private readonly ApparelRule rule;
        private readonly List<ThingDef> apparelDefs;
        private readonly List<ApparelRule> overlappingRules;
        private readonly List<ThingDef> filteredDefs = new List<ThingDef>();
        private Vector2 scrollPosition;
        private string searchText = "";
        private string filteredSearchText;
        private bool filteredDefsDirty = true;

        public ApparelSelectionWindow(ApparelRule rule)
        {
            this.rule = rule;
            overlappingRules = ApparelCompatibility.OverlappingRules(rule);
            apparelDefs = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def?.apparel != null)
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
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "Choose Apparel");
            Text.Font = GameFont.Small;

            Rect searchRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, 30f);
            searchText = Widgets.TextField(searchRect, searchText ?? "");
            TooltipHandler.TipRegion(searchRect,
                "Search apparel by name.");

            List<ThingDef> filtered = FilteredDefs();
            Rect countRect = new Rect(inRect.x, searchRect.yMax + 6f, inRect.width, 24f);
            string selectionHint = rule.RequiredApparel.Count == 0
                ? "any apparel allowed"
                : "all selected apparel is required";
            Widgets.Label(countRect,
                $"{filtered.Count} apparel item(s) — {selectionHint}");
            TooltipHandler.TipRegion(countRect,
                RuleTypeStyle.GearPolicyTip(rule) + "\n\n" + "Every selected garment must be worn together. An empty selection adds no clothing requirement. Selected items appear first. [Rule Name] shows who else selects an item; +number means more rules. [Retained] is remembered locker stock, not a current requirement or an availability count. Grey entries have a conflict; hover to see why.");

            Rect outRect = new Rect(inRect.x, countRect.yMax + 4f, inRect.width, inRect.yMax - countRect.yMax - 4f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 18f, filtered.Count * RowHeight);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            for (int i = 0; i < filtered.Count; i++)
                DrawApparelRow(filtered[i], new Rect(0f, i * RowHeight, viewRect.width, RowHeight));
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
            foreach (ThingDef def in apparelDefs)
            {
                bool isSelected = rule.RequiredApparel.Contains(def);
                if (isSelected != selected)
                    continue;
                if (!selected &&
                    (component?.IsManagedApparelDefinition(def) == true) != retainedStock)
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

        private string ApparelWithSelectedSources(ThingDef def)
        {
            // FindConflictIfAdded temporarily assigns the candidate to this rule.
            // That proposed assignment is not its existing source for display.
            var sources = RuleTypeStyle.GearSources(rule, def);
            if (rule.RequiredApparel.Contains(def))
                sources.Insert(0, rule);
            string sourceNames = sources.Count == 0 ? "not currently selected" :
                string.Join(", ", sources.Select(RuleTypeStyle.RuleName));
            return $"{def.LabelCap} ({sourceNames})";
        }

        private void DrawApparelRow(ThingDef def, Rect rect)
        {
            if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);

            bool selected = rule.RequiredApparel.Contains(def);
            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            bool retainedStock = !selected &&
                component?.IsManagedApparelDefinition(def) == true;
            ApparelConflict conflict = selected
                ? null
                : ApparelCompatibility.FindConflictIfAdded(rule, def, overlappingRules);
            var removalSource = NonWorkFallbackPolicy.ConflictingSource(rule, def, component?.Rules);
            var affectedNonWork = NonWorkFallbackPolicy.ConflictingDestination(rule, def, component?.Rules);
            string conflictReason = removalSource != null
                ? $"This item is part of {RuleTypeStyle.RuleName(removalSource)}, an outfit selected under Remove Work Outfits. " +
                  "Choose different gear or change the 'Remove Work Outfits' selection."
                : affectedNonWork != null ? RuleTypeStyle.ReverseConflictTip(rule, affectedNonWork)
                : conflict != null ? $"Cannot add this apparel to {RuleTypeStyle.RuleName(rule)} because " +
                    $"{ApparelWithSelectedSources(conflict.First)} conflicts with {ApparelWithSelectedSources(conflict.Second)}. " +
                    "Choose garments that can be worn together across the overlapping areas." : null;
            var sources = RuleTypeStyle.GearSources(rule, def);
            string sourceHeading = RuleTypeStyle.SourceTip(selected
                ? new[] { rule }.Concat(sources).ToList() : sources);
            Color conflictColor = RuleConflictStyle.Color;
            string label = def.LabelCap.ToString();
            bool retainedOnly = GearSelectionPolicy.IsRetained(def, retainedStock, sources);
            Color previousColor = GUI.color;
            if (conflictReason != null)
                GUI.color = conflictColor;
            else if (selected)
                GUI.color = RuleTypeStyle.ForRule(rule);
            else if (sources.Count > 0)
                GUI.color = RuleTypeStyle.SourceColor(sources, muted: false);

            float reservedButtonWidth = retainedStock ? 188f : 100f;
            Rect labelRect = new Rect(
                rect.x + 4f, rect.y + 5f,
                rect.width - reservedButtonWidth, 24f);
            if (conflictReason == null)
                labelRect = RuleTypeStyle.DrawSourceMarks(labelRect, sources, muted: false);
            if (retainedStock || (!selected && sources.Count > 0))
                label = (retainedOnly ? "[Retained] " : RuleTypeStyle.SourceLabel(sources, labelRect.width * 0.45f)) + label;
            string suffix = conflictReason != null ? " — Conflict" : "";
            Widgets.Label(labelRect, label.Truncate(Mathf.Max(0f, labelRect.width - Text.CalcSize(suffix).x)) + suffix);
            GUI.color = previousColor;
            if (conflictReason != null)
                TooltipHandler.TipRegion(labelRect,
                    sourceHeading + conflictReason + "\n\n" + RuleTypeStyle.GearPolicyTip(rule));
            else if (selected)
            {
                TooltipHandler.TipRegion(
                    new Rect(rect.x + 4f, rect.y, rect.width - 100f, rect.height),
                    sourceHeading + $"Selected for this rule ({RuleTypeStyle.RuleName(rule)}). Wear all selected garments together." + "\n\n" + RuleTypeStyle.GearPolicyTip(rule));
            }
            else if (retainedStock)
            {
                TooltipHandler.TipRegion(
                    new Rect(rect.x + 4f, rect.y, rect.width - 188f, rect.height),
                    sourceHeading + (sources.Count > 0
                        ? "Not selected for this rule. Use Add to select it here. Remove it from the rules above before using Forget."
                        : "Remembered as locker stock, but not selected by a rule. Add makes it a requirement here. Forget returns unused stock to ordinary storage.") + "\n\n" + RuleTypeStyle.GearPolicyTip(rule));
            }

            else
            {
                TooltipHandler.TipRegion(labelRect,
                    sourceHeading + $"{def.LabelCap} is not selected for {RuleTypeStyle.RuleName(rule)}. Use Add to select this type." + "\n\n" + RuleTypeStyle.GearPolicyTip(rule));
            }

            Rect buttonRect = new Rect(rect.xMax - 88f, rect.y + 2f, 84f, 27f);
            if (selected)
            {
                if (Widgets.ButtonText(buttonRect, "Remove"))
                {
                    component?.RememberManagedStockDefinition(def);
                    rule.RequiredApparel.Remove(def);
                    filteredDefsDirty = true;
                    component?.NotifyRuleRequirementsChanged(
                        rule.Id, $"removed apparel {def.LabelCap}");
                }
                TooltipHandler.TipRegion(buttonRect,
                    "Remove this selection. Its type remains automatic outfit stock until you use Forget; other rules can still select it.");
            }
            else
            {
                Rect addRect = retainedStock
                    ? new Rect(rect.xMax - 176f, rect.y + 2f, 84f, 27f)
                    : buttonRect;
                if (conflictReason != null)
                {
                    RuleConflictStyle.DrawBlockedButton(addRect, conflictReason);
                }
                else if (Widgets.ButtonText(addRect, "Add"))
                {
                    rule.RequiredApparel.Add(def);
                    component?.RememberManagedStockDefinition(def);
                    component?.InvalidateManagedApparelDefinitionIndex();
                    component?.NotifyRuleRequirementsChanged(rule.Id, "outfit selection added");
                    filteredDefsDirty = true;
                }
                if (conflictReason == null)
                {
                    TooltipHandler.TipRegion(addRect,
                        RuleTypeStyle.GearPolicyTip(rule) + "\n\n" + (retainedStock
                            ? "Select this apparel type for this rule."
                            : "Add this apparel type to the selected outfit requirements."));
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
                            ? "Return unused stock of this type to ordinary storage. Individual saved or borrowed items stay protected."
                            : component?.ManagedStockForgetBlockReason(def) ??
                              "A rule or current outfit change still uses this type.");
                }
            }
        }
    }
}
