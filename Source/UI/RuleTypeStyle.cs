using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using UnityEngine;
using Verse;

namespace AutomaticOutfitManager.UI
{
    internal static class RuleTypeStyle
    {
        internal static Color ForRule(ApparelRule rule) => ForArea(rule?.Area);

        internal static Color ForArea(Area area)
        {
            Color color = area?.Color ?? Color.white;
            color.a = 1f;
            // Preserve the area's hue while keeping very dark area colors legible.
            float brightest = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            return brightest < 0.55f ? Color.Lerp(color, Color.white, 0.35f) : color;
        }

        internal static string AreaName(Area area) => area == null ? "No area" :
            area.Label.Colorize(ForArea(area));

        internal static string RuleName(ApparelRule rule) => rule == null ? "Removed rule" :
            (rule.Name ?? "Unnamed rule").Colorize(ForRule(rule));

        internal static string LocationTip(ApparelRule rule) =>
            RuleName(rule) + " — " + (rule?.IsNonWork == true ? "Non-Work Area: " : "Work Area: ") +
            AreaName(rule?.Area);

        internal static List<ApparelRule> GearSources(ApparelRule rule, ThingDef def) =>
            GearSelectionPolicy.SelectingRules(def, AutomaticOutfitManagerGameComponent.Current?.Rules)
                .Where(source => source.Id != rule?.Id).ToList();

        internal static string GearPolicyTip(ApparelRule rule) => !rule.IsNonWork
            ? "These requirements apply before entry and while inside the Work Area."
            : rule.DefaultToSavedPersonalOutfit
                ? "These choices are used only when no personal outfit has been saved. Missing saved items do not activate fallback. The chosen Work outfits are still returned."
                : "These requirements apply after the chosen Work outfits are returned. Empty categories add no requirement; personal gear may stay on. Saved-outfit preference is off.";

        internal static string ReverseConflictTip(ApparelRule work, ApparelRule destination) =>
            $"{RuleName(destination)} selects this gear and also removes the outfit from {RuleName(work)}. " +
            $"Choose different gear, or change the 'Remove Work Outfits' selection in {RuleName(destination)}.";

        internal static string ConfigurationConflictTip(ApparelRule rule)
        {
            var rules = AutomaticOutfitManagerGameComponent.Current?.Rules;
            var nesting = AreaNestingPolicy.Conflict(rule, rules);
            if (nesting != null) return AreaNestingTip(rule, nesting);
            if (rule.IsNonWork)
            {
                var work = NonWorkFallbackPolicy.FirstConflict(rule, rules);
                return work == null ? null : ReverseConflictTip(work, rule);
            }
            var destination = NonWorkFallbackPolicy.ConflictingDestination(rule, null, rules);
            return destination == null ? null : ReverseConflictTip(rule, destination);
        }

        internal static string AreaNestingTip(ApparelRule rule, ApparelRule other)
        {
            var work = rule.IsNonWork ? other : rule;
            var nonWork = rule.IsNonWork ? rule : other;
            return $"The Work Area for {RuleName(work)} is entirely inside the Non-Work Area for {RuleName(nonWork)}. " +
                "Choose different areas or disable one rule. Identical areas also conflict. " +
                "If an area edit creates this conflict, the Work rule is disabled and its pawns are recalled. Re-enable it after fixing the areas.";
        }

        internal static Color SourceColor(List<ApparelRule> sources, bool muted = true) =>
            muted || sources.Count != 1 ? RuleConflictStyle.Color : ForRule(sources[0]);

        internal static string SourceLabel(List<ApparelRule> sources, float maxWidth)
        {
            if (sources.Count == 0) return "";
            string name = string.IsNullOrWhiteSpace(sources[0].Name) ? "Unnamed rule" : sources[0].Name.Trim();
            string more = sources.Count > 1 ? $" +{sources.Count - 1}" : "";
            float nameWidth = Mathf.Max(0f, maxWidth - Text.CalcSize("[" + more + "] ").x);
            return "[" + name.Truncate(nameWidth) + more + "] ";
        }

        internal static string SourceTip(List<ApparelRule> sources) => sources.Count == 0 ? "" :
            "Selected by: " + string.Join(", ", sources.Select(source =>
                RuleName(source) + " (" + (source.IsNonWork ? (source.DefaultToSavedPersonalOutfit ? "Non-Work fallback" : "Non-Work") : "Work") +
                (!source.Enabled ? ", disabled" : "") +
                (source.Area?.Map != null && source.Area.Map != Find.CurrentMap ? ", another map" : "") + ")")) +
            ".\n\n";

        internal static Rect DrawSourceMarks(Rect label, List<ApparelRule> sources, bool muted)
        {
            if (sources.Count < 2) return label;
            int count = Mathf.Min(3, sources.Count);
            for (int i = 0; i < count; i++)
                Widgets.DrawBoxSolid(new Rect(label.x + i * 8f, label.y + 5f, 5f, 14f),
                    muted ? RuleConflictStyle.Color : ForRule(sources[i]));
            float width = count * 8f + 4f;
            label.x += width;
            label.width -= width;
            return label;
        }
    }
}
