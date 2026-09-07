using System;
using System.Linq;
using System.Collections.Generic;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using RimWorld;
using AutomaticOutfitManager.Rules;
using UnityEngine;
using Verse;

namespace AutomaticOutfitManager.UI
{
    public sealed class WorkOutfitRemovalWindow : Window
    {
        private readonly ApparelRule rule;
        private readonly AutomaticOutfitManagerGameComponent component;
        private readonly Action changed;
        private Vector2 scroll;
        private bool removeAll;
        private readonly List<string> selectedIds;
        public WorkOutfitRemovalWindow(ApparelRule rule,
            AutomaticOutfitManagerGameComponent component, Action changed)
        {
            this.rule = rule;
            this.component = component;
            this.changed = changed;
            removeAll = rule.RemoveAllWorkOutfits;
            selectedIds = rule.WorkOutfitsToRemove.ToList();
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }
        public override void PreClose()
        {
            base.PreClose();
            if (rule.RemoveAllWorkOutfits == removeAll &&
                rule.WorkOutfitsToRemove.SequenceEqual(selectedIds)) return;
            rule.RemoveAllWorkOutfits = removeAll;
            rule.WorkOutfitsToRemove = selectedIds.ToList();
            changed();
        }
        private bool RejectNewConflict(bool proposedAll, IEnumerable<string> proposedIds)
        {
            foreach (var source in component.Rules.Where(item => item?.Enabled == true && !item.IsNonWork))
            {
                var onlySource = new[] { source };
                if (NonWorkFallbackPolicy.FirstConflict(rule, onlySource, proposedAll, proposedIds) != null &&
                    NonWorkFallbackPolicy.FirstConflict(rule, onlySource, removeAll, selectedIds) == null)
                {
                    Messages.Message(RuleTypeStyle.ReverseConflictTip(source, rule), MessageTypeDefOf.RejectInput, false);
                    return true;
                }
            }
            return false;
        }
        public override Vector2 InitialSize => new Vector2(620f, 540f);
        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 32f), "Work Outfits to Remove");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 40f, rect.width, 70f),
                "Return work outfits issued by the selected Work Area rules. Shared gear stays on if any source rule is unselected. Changes apply together when this window closes.");
            bool all = removeAll;
            Widgets.CheckboxLabeled(new Rect(0f, 116f, rect.width, 28f),
                "All Work Outfits (default)", ref all);
            TooltipHandler.TipRegion(new Rect(0f, 116f, rect.width, 28f),
                "Return every Work outfit, including outfits from rules added later. Turn this off to clear the selection and choose rules individually. Changes apply when the window closes.");
            if (all != removeAll && RejectNewConflict(all, all ? selectedIds : new List<string>()))
                all = removeAll;
            if (removeAll && !all)
                selectedIds.Clear();
            removeAll = all;
            Widgets.Label(new Rect(0f, 150f, rect.width, 50f), all
                ? "All Work Areas are selected and locked, including rules added later. Turn this off to clear the selections and choose individually."
                : "Choose one or more rules below. No selection keeps all issued work outfits.");
            var work = component.Rules.Where(item => item != null && !item.IsNonWork)
                .OrderBy(item => item.Name).ToList();
            var missing = selectedIds.Where(id => !work.Any(item => item.Id == id)).ToList();
            Rect outer = new Rect(0f, 208f, rect.width, rect.height - 260f);
            Rect inner = new Rect(0f, 0f, outer.width - 18f,
                Mathf.Max(32f, (work.Count + missing.Count) * 32f));
            Widgets.BeginScrollView(outer, ref scroll, inner);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !all;
            float y = 0f;
            foreach (var source in work)
            {
                bool selected = all || selectedIds.Contains(source.Id);
                bool before = selected;
                Rect row = new Rect(0f, y, inner.width, 28f);
                Color previousColor = GUI.color;
                Color labelColor = all || !source.Enabled ? RuleConflictStyle.Color : RuleTypeStyle.ForRule(source);
                string label = source.Name + " (" + (source.Area?.Label ?? "no area") + ")";
                // Tint only the text. Native checkbox textures keep their red X
                // and green check colors, including normal disabled rendering.
                GUI.color = Color.white;
                Widgets.CheckboxLabeled(row, label.Colorize(labelColor), ref selected);
                GUI.color = previousColor;
                if (!all && selected != before)
                {
                    var proposed = selectedIds.ToList();
                    if (selected) proposed.Add(source.Id);
                    else proposed.Remove(source.Id);
                    if (RejectNewConflict(false, proposed)) selected = before;
                    else
                    {
                        selectedIds.Clear();
                        selectedIds.AddRange(proposed);
                    }
                }
                TooltipHandler.TipRegion(row,
                    "Work Rule: " + RuleTypeStyle.RuleName(source) + "\n\n" +
                    (all ? "Selected and locked by All Work Outfits. Turn All off to choose sources individually. " :
                        selected ? "Selected: gear issued by this source is eligible for return. " :
                        "Unselected: gear issued by this source is retained. ") +
                    "Shared gear stays on if any source is unselected. Disabled rules can still have issued gear to return. Changes apply when the window closes.");
                y += 32f;
            }
            foreach (string id in missing)
            {
                bool selected = true;
                Widgets.CheckboxLabeled(new Rect(0f, y, inner.width, 28f),
                    "Deleted Work Area rule", ref selected);
                TooltipHandler.TipRegion(new Rect(0f, y, inner.width, 28f),
                    "This Work rule was deleted. Turn All Work Outfits off to uncheck this old selection.");
                if (!all && !selected) selectedIds.Remove(id);
                y += 32f;
            }
            if (work.Count + missing.Count == 0)
            {
                Widgets.Label(new Rect(0f, 0f, inner.width, 28f), "No Work Area rules available.");
                TooltipHandler.TipRegion(new Rect(0f, 0f, inner.width, 28f),
                    "No Work rules are available. All Work Outfits will include any you add later.");
            }
            GUI.enabled = previousEnabled;
            Widgets.EndScrollView();
        }
    }
}
