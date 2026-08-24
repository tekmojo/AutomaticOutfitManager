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
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "Choose apparel");
            Text.Font = GameFont.Small;

            Rect searchRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, 30f);
            searchText = Widgets.TextField(searchRect, searchText ?? "");
            TooltipHandler.TipRegion(searchRect,
                "Search by the apparel's displayed name or technical DefName.");

            List<ThingDef> filtered = FilteredDefs();
            Rect countRect = new Rect(inRect.x, searchRect.yMax + 6f, inRect.width, 24f);
            string selectionHint = rule.RequiredApparel.Count == 0
                ? "any apparel allowed"
                : "all selected apparel is required";
            Widgets.Label(countRect,
                $"{filtered.Count} apparel item(s) — {selectionHint}");
            TooltipHandler.TipRegion(countRect,
                "With no selection, this rule does not change apparel. Every green entry is required and sorts to the top. Cyan entries are retained managed stock: they remain reserved for Automatic Outfit Manager storage until forgotten. Removing a green entry moves it to the cyan group in its normal alphabetical position.");

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
            string label = $"{def.LabelCap} [{def.defName}]";
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
                    "Selected for this rule. Every green apparel entry must be worn simultaneously before entry and throughout every activity or protected route inside the active area.");
            }
            else if (retainedStock)
            {
                TooltipHandler.TipRegion(
                    new Rect(rect.x + 4f, rect.y, rect.width - 188f, rect.height),
                    "Retained managed apparel stock. It remains classified for Automatic Outfit Manager locker storage even though this rule does not currently require it.");
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
                    "Stop requiring this apparel for this rule. Its type remains retained managed stock until Forget is used when no rule or active transition still needs it.");
            }
            else
            {
                Rect addRect = retainedStock
                    ? new Rect(rect.xMax - 176f, rect.y + 2f, 84f, 27f)
                    : buttonRect;
                if (conflict != null)
                {
                    bool previousEnabled = GUI.enabled;
                    GUI.enabled = false;
                    Widgets.ButtonText(addRect, "Conflict");
                    GUI.enabled = previousEnabled;
                    TooltipHandler.TipRegion(addRect,
                        $"Cannot add this apparel because {conflict.Label}. " +
                        "Outer and nested work-area apparel must remain wearable together.");
                }
                else if (Widgets.ButtonText(addRect, "Add"))
                {
                    rule.RequiredApparel.Add(def);
                    component?.RememberManagedStockDefinition(def);
                    component?.InvalidateManagedApparelDefinitionIndex();
                    filteredDefsDirty = true;
                }
                if (conflict == null)
                {
                    TooltipHandler.TipRegion(addRect,
                        retainedStock
                            ? "Require this retained apparel type for this rule again."
                            : "Require every managed worker for this rule to wear this apparel item.");
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
                            ? "Stop classifying spare copies of this apparel type as managed Automatic Outfit Manager stock. Exact saved or currently tracked items remain protected individually."
                            : component?.ManagedStockForgetBlockReason(def) ??
                              "This apparel type is still required by another rule or used by an active outfit transition.");
                }
            }
        }
    }
}
