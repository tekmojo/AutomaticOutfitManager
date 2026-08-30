using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace AutomaticOutfitManager.Patches
{
    /// <summary>
    /// RimWorld copies allowed areas aboard a gravship into new Area objects on
    /// the destination map. AOM rules are independent save references, so they
    /// otherwise continue pointing at the source-map objects after landing.
    /// </summary>
    [HarmonyPatch(typeof(GravshipPlacementUtility), "CopyAreasIntoMap")]
    public static class GravshipPlacementUtility_CopyAreasIntoMap_Patch
    {
        private static void Postfix(Gravship gravship, Map map)
        {
            GravshipAreaRemapper.TryRemapAfterPlacement(
                gravship,
                map,
                "while copying gravship areas");
        }
    }

    /// <summary>
    /// CopyAreasIntoMap is the earliest point at which the destination areas
    /// exist. Repeat the remap after the whole placement has completed because
    /// RimWorld can finish replacing the source Area objects later in the same
    /// placement operation. The second pass is harmless when the first pass
    /// already succeeded because current-map references are ignored.
    /// </summary>
    [HarmonyPatch(typeof(GravshipPlacementUtility), "PlaceGravshipInMap")]
    public static class GravshipPlacementUtility_PlaceGravshipInMap_Patch
    {
        private static void Postfix(Gravship gravship, Map map)
        {
            GravshipAreaRemapper.TryRemapAfterPlacement(
                gravship,
                map,
                "after gravship placement completed");
        }
    }

    internal static class GravshipAreaRemapper
    {
        private const float ColorTolerance = 0.001f;

        private static readonly FieldInfo MoveableAreaIdField =
            AccessTools.Field(typeof(MoveableArea), "id");
        private static readonly FieldInfo MoveableAreaLabelField =
            AccessTools.Field(typeof(MoveableArea), "label");
        private static readonly FieldInfo MoveableAreaRenamableLabelField =
            AccessTools.Field(typeof(MoveableArea), "renamableLabel");
        private static readonly FieldInfo MoveableAreaColorField =
            AccessTools.Field(typeof(MoveableArea), "color");

        internal static void TryRemapAfterPlacement(
            Gravship gravship,
            Map destinationMap,
            string stage)
        {
            try
            {
                RemapAfterPlacement(
                    AutomaticOutfitManagerGameComponent.Current,
                    gravship,
                    destinationMap);
            }
            catch (Exception exception)
            {
                AomLog.Error(
                    "[AutomaticOutfitManager] Failed to remap gravship Work Area " +
                    $"and Locker Area references {stage}: {exception}");
            }
        }

        internal static int RemapAfterPlacement(
            AutomaticOutfitManagerGameComponent component,
            Gravship gravship,
            Map destinationMap)
        {
            if (component?.Rules == null || gravship?.areas?.allowedAreas == null ||
                destinationMap?.areaManager == null)
            {
                return 0;
            }

            var remappedRuleIds = new HashSet<string>();
            foreach (MoveableArea_Allowed movedArea in gravship.areas.allowedAreas)
            {
                if (movedArea == null)
                    continue;

                int sourceId = (int)MoveableAreaIdField.GetValue(movedArea);
                string sourceLabel = MoveableAreaLabelField.GetValue(movedArea) as string;
                string sourceRenamableLabel =
                    MoveableAreaRenamableLabelField.GetValue(movedArea) as string;
                Color sourceColor = (Color)MoveableAreaColorField.GetValue(movedArea);
                if (string.IsNullOrEmpty(sourceLabel))
                    continue;

                Area destinationArea = destinationMap.areaManager.GetLabeled(sourceLabel);
                if (destinationArea?.Map != destinationMap)
                    continue;

                foreach (ApparelRule rule in component.Rules.Where(rule => rule != null))
                {
                    bool changed = false;
                    if (ReferencesCopiedSource(
                            rule.Area,
                            sourceId,
                            sourceLabel,
                            sourceRenamableLabel,
                            sourceColor,
                            destinationMap))
                    {
                        rule.Area = destinationArea;
                        changed = true;
                    }

                    if (ReferencesCopiedSource(
                            rule.ChangingArea,
                            sourceId,
                            sourceLabel,
                            sourceRenamableLabel,
                            sourceColor,
                            destinationMap))
                    {
                        rule.ChangingArea = destinationArea;
                        changed = true;
                    }

                    if (changed && !string.IsNullOrEmpty(rule.Id))
                        remappedRuleIds.Add(rule.Id);
                }
            }

            if (remappedRuleIds.Count == 0)
                return 0;

            component.NotifyGravshipAreaReferencesRemapped(
                remappedRuleIds,
                destinationMap,
                "after gravship placement");

            if (AomLog.DetailedEnabled)
            {
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] Remapped {remappedRuleIds.Count} " +
                    "rule(s) to copied destination-map Work/Locker areas after " +
                    "gravship placement.");
            }

            return remappedRuleIds.Count;
        }

        internal static int RepairAfterLoad(
            AutomaticOutfitManagerGameComponent component)
        {
            if (component?.Rules == null || Find.Maps == null || Find.Maps.Count < 2)
                return 0;

            var repairedByMap = new Dictionary<Map, HashSet<string>>();
            foreach (ApparelRule rule in component.Rules.Where(rule => rule?.Area != null))
            {
                Map destinationMap = ExpectedStateMap(component, rule);
                if (destinationMap != null)
                {
                    if (RepairRuleOnMap(rule, destinationMap))
                        RememberRepair(repairedByMap, destinationMap, rule.Id);
                }

                // A copied Work/Locker pair is intentionally identical on the
                // source and destination maps. Without a spawned tracked pawn
                // to identify which copy is current, moving the rule would be
                // a guess and can invert an already-correct post-flight save.
                // Leave ambiguous inactive rules untouched; live placement or
                // a later corroborated load can remap them safely.
            }

            int repairedCount = 0;
            foreach (KeyValuePair<Map, HashSet<string>> repair in repairedByMap)
            {
                repairedCount += repair.Value.Count;
                component.NotifyGravshipAreaReferencesRemapped(
                    repair.Value,
                    repair.Key,
                    "while repairing a loaded post-flight save");
            }

            if (repairedCount > 0)
            {
                AomLog.Basic(
                    $"[AutomaticOutfitManager] Repaired {repairedCount} stale " +
                    "gravship Work/Locker area rule reference(s) after load.");
            }

            return repairedCount;
        }

        private static bool ReferencesCopiedSource(
            Area area,
            int sourceId,
            string sourceLabel,
            string sourceRenamableLabel,
            Color sourceColor,
            Map destinationMap)
        {
            if (!(area is Area_Allowed) || area.Map == destinationMap ||
                !string.Equals(area.Label, sourceLabel, StringComparison.Ordinal))
            {
                return false;
            }

            // The moved-area ID is normally the source Area ID, but RimWorld
            // can replace or renumber an Area during a gravship transfer. The
            // label/color signature is copied directly into MoveableArea, so it
            // remains a conservative identity fallback for an area that this
            // exact gravship is carrying.
            return area.ID == sourceId ||
                   (string.Equals(
                        area.RenamableLabel,
                        sourceRenamableLabel,
                        StringComparison.Ordinal) &&
                    ColorsMatch(area.Color, sourceColor));
        }

        private static Map ExpectedStateMap(
            AutomaticOutfitManagerGameComponent component,
            ApparelRule rule)
        {
            List<Map> maps = component.PawnStates
                .Where(state => StateReferencesRule(state, rule.Id) &&
                                state.Pawn?.Spawned == true &&
                                state.Pawn.Map != null)
                .Select(state => state.Pawn.Map)
                .Distinct()
                .ToList();

            if (maps.Count != 1)
                return null;

            Map expected = maps[0];
            return rule.Area?.Map == expected ||
                   FindSignatureMatch(expected, rule.Area) != null
                ? expected
                : null;
        }

        private static bool RepairRuleOnMap(
            ApparelRule rule,
            Map destinationMap)
        {
            Area workArea = rule.Area?.Map == destinationMap
                ? rule.Area
                : FindSignatureMatch(destinationMap, rule.Area);
            Area changingArea = rule.ChangingArea == null
                ? null
                : rule.ChangingArea.Map == destinationMap
                    ? rule.ChangingArea
                    : FindSignatureMatch(destinationMap, rule.ChangingArea);

            if (workArea == null)
                return false;

            bool changed = false;
            if (rule.Area != workArea)
            {
                rule.Area = workArea;
                changed = true;
            }

            if (changingArea != null && rule.ChangingArea != changingArea)
            {
                rule.ChangingArea = changingArea;
                changed = true;
            }

            return changed;
        }

        internal static Area FindSignatureMatch(Map map, Area source)
        {
            if (map?.areaManager?.AllAreas == null || source == null || source.Map == map)
                return null;

            List<Area> matches = map.areaManager.AllAreas
                .Where(candidate => candidate != null &&
                                    candidate.GetType() == source.GetType() &&
                                    candidate.TrueCount == source.TrueCount &&
                                    string.Equals(
                                        candidate.Label,
                                        source.Label,
                                        StringComparison.Ordinal) &&
                                    string.Equals(
                                        candidate.RenamableLabel,
                                        source.RenamableLabel,
                                        StringComparison.Ordinal) &&
                                    ColorsMatch(candidate.Color, source.Color))
                .ToList();

            return matches.Count == 1 ? matches[0] : null;
        }

        private static bool ColorsMatch(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) <= ColorTolerance &&
                   Mathf.Abs(left.g - right.g) <= ColorTolerance &&
                   Mathf.Abs(left.b - right.b) <= ColorTolerance &&
                   Mathf.Abs(left.a - right.a) <= ColorTolerance;
        }

        private static bool StateReferencesRule(PawnApparelState state, string ruleId)
        {
            return state?.Pawn != null &&
                   !string.IsNullOrEmpty(ruleId) &&
                   (state.ActiveRuleId == ruleId ||
                    state.CurrentRuleIds?.Contains(ruleId) == true);
        }

        private static void RememberRepair(
            IDictionary<Map, HashSet<string>> repairedByMap,
            Map map,
            string ruleId)
        {
            if (map == null || string.IsNullOrEmpty(ruleId))
                return;

            if (!repairedByMap.TryGetValue(map, out HashSet<string> ruleIds))
            {
                ruleIds = new HashSet<string>();
                repairedByMap.Add(map, ruleIds);
            }
            ruleIds.Add(ruleId);
        }
    }
}
