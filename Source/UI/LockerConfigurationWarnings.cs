using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using UnityEngine;
using Verse;

namespace AutomaticOutfitManager.UI
{
    internal sealed class LockerConfigurationWarnings
    {
        internal sealed class Entry
        {
            internal string Label;
            internal string Tip;
            internal Color Color;
            internal List<IntVec3> Cells;
        }

        private const float RefreshSeconds = 1f;
        private readonly Dictionary<ApparelRule, List<Entry>> entries =
            new Dictionary<ApparelRule, List<Entry>>();
        private readonly Dictionary<ApparelRule, List<IntVec3>> outlines =
            new Dictionary<ApparelRule, List<IntVec3>>();
        private List<IntVec3> allOverlapCells;
        private float refreshedAt = -1f;
        private Map map;
        private AutomaticOutfitManagerGameComponent component;
        private static readonly Color Amber = new Color(1f, 0.75f, 0.25f);
        internal const float RowHeight = 22f;

        // Called once before layout. A stable snapshot is shared by height and
        // drawing passes; realtime also notices edits while game ticks are paused.
        internal void Refresh(Map currentMap, AutomaticOutfitManagerGameComponent currentComponent)
        {
            float now = Time.realtimeSinceStartup;
            if (map != currentMap || component != currentComponent ||
                now < refreshedAt || now - refreshedAt >= RefreshSeconds)
            {
                entries.Clear();
                outlines.Clear();
                allOverlapCells = null;
                map = currentMap;
                component = currentComponent;
                refreshedAt = now;
            }
        }

        internal void Clear()
        {
            entries.Clear();
            outlines.Clear();
            allOverlapCells = null;
            LockerOverlapOverlay.Clear();
            map = null;
            component = null;
            refreshedAt = -1f;
        }

        internal List<Entry> For(ApparelRule rule)
        {
            if (entries.TryGetValue(rule, out var cached)) return cached;
            var result = new List<Entry>();
            entries[rule] = result;
            allOverlapCells = null;
            var locker = rule.ChangingArea;
            if (locker == null || map == null || locker.Map != map) return result;
            var shape = LockerAreaAssessment.Inspect(locker, component?.Rules);
            string names = string.Join(", ", shape.WorkRules.Select(RuleTypeStyle.RuleName));
            string plainNames = string.Join(", ", shape.WorkRules.Select(work => work.Name));
            if (shape.PaintedCount == 0)
                result.Add(new Entry { Label = "Locker area is empty", Color = Color.red,
                    Tip = "Paint cells for this locker room or choose another area." });
            else if (shape.OverlapCells.Count > 0)
                result.Add(new Entry
                {
                    Label = shape.StandableOutsideCount == 0
                        ? "Locker has no changing space outside Work Areas"
                        : "Locker overlaps Work Areas: " + plainNames,
                    Color = shape.StandableOutsideCount == 0 ? Color.red : Amber,
                    Cells = shape.OverlapCells,
                    Tip = "Overlapping Work rules: " + names + "\n\n" +
                        "Gear stored in these cells may be blocked when restoring a personal outfit. " +
                        "Keep changing space and personal-outfit storage outside the Work Areas, or choose another locker. " +
                        "Access, reachability and hazards still affect where each pawn can change.\n\n" +
                        "Hover to highlight only the overlap; click to center the map on it."
                });
            else if (shape.StandableOutsideCount == 0)
                result.Add(new Entry { Label = "Locker has no standable changing space", Color = Color.red,
                    Tip = "Clear some floor space in this locker or choose another area. Pawns also need a reachable, safe place to change." });

            if (shape.NonWorkOverlapCells.Count > 0)
                result.Add(new Entry
                {
                    Label = "Locker overlaps Non-Work Areas: " + string.Join(", ", shape.NonWorkRules.Select(other => other.Name)),
                    Tip = "Overlapping Non-Work rules: " + string.Join(", ", shape.NonWorkRules.Select(RuleTypeStyle.RuleName)) +
                        "\n\nThese cells also follow the Non-Work outfit and access settings. Compatible use can be valid; " +
                        "if changing or retrieval is blocked, move that storage or changing space outside the overlap. " +
                        "Hover to focus the highlight; click to center the map.",
                    Color = new Color(0.5f, 0.8f, 1f),
                    Cells = shape.NonWorkOverlapCells
                });

            if (shape.PaintedCount == 0) return result;
            var saved = new List<Thing>();
            foreach (var state in component.PawnStates)
            {
                if (state?.Pawn?.Map != map || !(state.ActiveRuleId == rule.Id ||
                    state.CurrentRuleIds?.Contains(rule.Id) == true ||
                    state.RestorationSourceRuleIds?.Contains(rule.Id) == true)) continue;
                if (state.OriginalApparel != null) saved.AddRange(state.OriginalApparel.Where(item => item != null));
                if (state.OriginalWeapon != null) saved.Add(state.OriginalWeapon);
            }
            bool personal = rule.IsNonWork && rule.DefaultToSavedPersonalOutfit;
            var storage = LockerStorageAssessment.Inspect(map, shape.OutsideCells,
                personal || rule.RequiredApparel?.Count > 0 || saved.Any(item => item.def?.apparel != null),
                personal || rule.HasWeaponRequirement || saved.Any(item => item.def?.IsWeapon == true), saved);
            string storageLabel = !storage.HasStorage ? "Locker has no storage outside Work Areas" :
                storage.ExcludesManagedApparel && storage.ExcludesManagedWeapons ? "Locker storage excludes automatic outfits" :
                storage.ExcludesManagedApparel ? "Locker storage excludes automatic outfit apparel" :
                storage.ExcludesManagedWeapons ? "Locker storage excludes automatic outfit weapons" :
                storage.RejectedSavedItems.Count > 0 ? "Locker storage rejects saved outfit items" : null;
            if (storageLabel != null)
            {
                string rejected = storage.RejectedSavedItems.Count == 0 ? "" :
                    "\n\nNot accepted here: " + string.Join(", ", storage.RejectedSavedItems.Take(3).Select(item => item.LabelCap.ToString())) +
                    (storage.RejectedSavedItems.Count > 3 ? $" (+{storage.RejectedSavedItems.Count - 3} more)" : "") + ".";
                result.Add(new Entry { Label = storageLabel, Color = Amber,
                    Tip = "Checks this locker's storage outside enabled Work Areas. Automatic outfit filters include saved apparel and weapons. " +
                        "Allow those filters, the item types and their condition/quality in the storage settings. " +
                        "Storage has its own limits, independent of this rule. Use 0-100% and any quality to accept saved outfits of any condition or quality. " +
                        "Other reachable storage may also be used." + rejected });
            }
            return result;
        }

        internal void ShowMapOverlaps()
        {
            if (allOverlapCells == null)
                allOverlapCells = entries.Values.SelectMany(list => list).Where(entry => entry.Cells != null)
                    .SelectMany(entry => entry.Cells).Distinct().ToList();
            LockerOverlapOverlay.Request(map, allOverlapCells, Amber);
        }

        internal void Highlight(ApparelRule rule, Rect rect)
        {
            if (!Mouse.IsOver(rect)) return;
            if (!outlines.TryGetValue(rule, out var cells))
            {
                cells = For(rule).Where(entry => entry.Cells != null).SelectMany(entry => entry.Cells).Distinct().ToList();
                outlines[rule] = cells;
            }
            LockerOverlapOverlay.Request(map, cells, Amber);
        }

        internal void Draw(ApparelRule rule, float x, ref float y, float width)
        {
            foreach (var entry in For(rule))
            {
                var rect = new Rect(x, y, width, RowHeight);
                var previousColor = GUI.color;
                bool previousWrap = Text.WordWrap;
                Text.WordWrap = false;
                GUI.color = entry.Color;
                Widgets.Label(rect, entry.Label.Truncate(width));
                GUI.color = previousColor;
                Text.WordWrap = previousWrap;
                TooltipHandler.TipRegion(rect, entry.Tip);
                if (entry.Cells?.Count > 0 && map == Find.CurrentMap)
                {
                    Widgets.DrawHighlightIfMouseover(rect);
                    if (Mouse.IsOver(rect) && Event.current.type == EventType.Repaint)
                    {
                        LockerOverlapOverlay.Request(map, entry.Cells, entry.Color);
                    }
                    if (Widgets.ButtonInvisible(rect))
                    {
                        // Pick an actual overlap cell near the extent center,
                        // even when the overlap consists of disjoint islands.
                        float centerX = (entry.Cells.Min(cell => cell.x) + entry.Cells.Max(cell => cell.x)) / 2f;
                        float centerZ = (entry.Cells.Min(cell => cell.z) + entry.Cells.Max(cell => cell.z)) / 2f;
                        var target = entry.Cells.OrderBy(cell => (cell.x - centerX) * (cell.x - centerX) +
                            (cell.z - centerZ) * (cell.z - centerZ)).First();
                        CameraJumper.TryJump(target, map);
                    }
                }
                y += RowHeight;
            }
        }
    }
}
