using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.UI
{
    public sealed class MainRulesWindow : MainTabWindow
    {
        private const float ReadinessCacheSeconds = 1f;
        private const float ActivityCacheSeconds = 0.5f;
        private const float RuleStandardChangeQuietSeconds = 1f;
        private Vector2 scrollPosition;
        private readonly List<float> layoutRuleHeights = new List<float>();
        private readonly Dictionary<string, CachedRuleReadiness> readinessCache =
            new Dictionary<string, CachedRuleReadiness>();
        private readonly Dictionary<string, CachedRuleActivity> activityCache =
            new Dictionary<string, CachedRuleActivity>();
        private readonly Dictionary<string, string> pendingRuleStandardChanges =
            new Dictionary<string, string>();
        private readonly Dictionary<string, float> pendingRuleStandardChangeTimes =
            new Dictionary<string, float>();

        private sealed class CachedRuleReadiness
        {
            public float CreatedAt;
            public string Text;
            public string ApparelAvailability;
            public string WeaponAvailability;
            public string AvailabilitySummary;
            public Color Color;
        }

        private sealed class CachedActivityEntry
        {
            public Pawn Pawn;
            public string Report;
            public bool MissingRequiredGear;
        }

        private sealed class CachedRuleActivity
        {
            public float CreatedAt;
            public Map Map;
            public readonly List<CachedActivityEntry> QualifyingWorkers =
                new List<CachedActivityEntry>();
            public readonly List<CachedActivityEntry> Haulers =
                new List<CachedActivityEntry>();
            public readonly List<CachedActivityEntry> Wanderers =
                new List<CachedActivityEntry>();
        }

        public override Vector2 RequestedTabSize => new Vector2(760f, 620f);

        public override void DoWindowContents(Rect inRect)
        {
            var component = AutomaticOutfitManagerGameComponent.Current;
            if (component == null)
            {
                Widgets.Label(inRect, "No active game.");
                return;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 36f), "Automatic Outfit Manager");
            Text.Font = GameFont.Small;

            float y = inRect.y + 42f;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 24f),
                "Require apparel or primary weapons for work areas, with optional locker-room changing.");
            y += 32f;

            Rect newRuleRect = new Rect(inRect.x, y, 130f, 30f);
            if (Widgets.ButtonText(newRuleRect, "Add rule"))
            {
                component.Rules.Add(new ApparelRule());
                component.InvalidateManagedDefinitionIndexes();
            }
            TooltipHandler.TipRegion(newRuleRect, "Create a new automatic outfit rule for this save. Examples: radiation work, freezer clothing, firefighting apparel, cleanroom apparel, uniforms, or guard weapons.");
            Rect manageAreasRect = new Rect(inRect.x + 140f, y, 150f, 30f);
            if (Widgets.ButtonText(manageAreasRect, "Edit map areas"))
                ShowManageAreas();
            TooltipHandler.TipRegion(manageAreasRect, "Create, rename, or edit the areas used by Work area and Locker room. Examples: Reactor Room, Freezer, Hospital Cleanroom, or North Locker Room.");
            y += 40f;

            Rect outRect = new Rect(inRect.x, y, inRect.width, inRect.height - y + inRect.y);
            layoutRuleHeights.Clear();
            float contentHeight = 10f;
            for (int i = 0; i < component.Rules.Count; i++)
            {
                float ruleHeight = RuleHeight(component.Rules[i], component);
                layoutRuleHeights.Add(ruleHeight);
                contentHeight += ruleHeight + 10f;
            }

            float viewHeight = Mathf.Max(outRect.height, contentHeight);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 18f, viewHeight);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float rowY = 0f;
            for (int i = 0; i < component.Rules.Count; i++)
            {
                float ruleHeight = layoutRuleHeights[i];
                Rect ruleRect = new Rect(
                    0f, rowY, viewRect.width, ruleHeight);
                if (ruleRect.yMax >= scrollPosition.y &&
                    ruleRect.y <= scrollPosition.y + outRect.height)
                {
                    DrawRule(
                        component.Rules[i], i, ruleRect, component);
                }
                rowY += ruleHeight + 10f;
            }
            Widgets.EndScrollView();
            FlushPendingRuleStandardChanges(component);
        }

        public override void PostClose()
        {
            FlushPendingRuleStandardChanges(
                AutomaticOutfitManagerGameComponent.Current, true);
            base.PostClose();
        }

        private void DrawRule(ApparelRule rule, int index, Rect rect, AutomaticOutfitManagerGameComponent component)
        {
            Widgets.DrawMenuSection(rect);
            float x = rect.x + 10f;
            float y = rect.y + 8f;
            float width = rect.width - 20f;

            Rect enabledRect = new Rect(x, y, 90f, 24f);
            bool wasEnabled = rule.Enabled;
            Widgets.CheckboxLabeled(enabledRect, "Enabled", ref rule.Enabled);
            if (rule.Enabled != wasEnabled)
            {
                component.RememberManagedStockDefinitions(rule.RequiredApparel);
                component.RememberManagedStockDefinitions(rule.RequiredWeapons);
                component.InvalidateManagedDefinitionIndexes();
                if (!rule.Enabled)
                {
                    component.NotifyRuleRequirementsChanged(
                        rule.Id, "rule disabled");
                }
            }
            TooltipHandler.TipRegion(enabledRect, "Turn this rule on or off without deleting its settings. Pause work does not change this setting. Example: disable a seasonal winter-clothing rule during summer.");
            Rect ruleNameRect = new Rect(x + 100f, y, width - 272f, 26f);
            rule.Name = Widgets.TextField(ruleNameRect, rule.Name ?? "");
            TooltipHandler.TipRegion(ruleNameRect, "Give this rule a recognizable name. Examples: Radiation Lab, Freezer Apparel, Fire Crew, Cleanroom, or Guard Weapons.");
            Rect collapseRect = new Rect(rect.xMax - 164f, y, 76f, 26f);
            bool collapseChanged = false;
            if (Widgets.ButtonText(collapseRect, rule.UiCollapsed ? "Expand" : "Collapse"))
            {
                rule.UiCollapsed = !rule.UiCollapsed;
                collapseChanged = true;
            }
            TooltipHandler.TipRegion(collapseRect,
                rule.UiCollapsed
                    ? "Expand this rule to show and edit all settings and activity."
                    : "Collapse this rule to a compact summary. Its enabled and paused states and all settings are unchanged.");
            Rect deleteRect = new Rect(rect.xMax - 82f, y, 72f, 26f);
            if (Widgets.ButtonText(deleteRect, "Delete"))
            {
                component.RememberManagedStockDefinitions(rule.RequiredApparel);
                component.RememberManagedStockDefinitions(rule.RequiredWeapons);
                component.Rules.RemoveAt(index);
                component.NotifyRuleRequirementsChanged(
                    rule.Id, "rule deleted");
                return;
            }
            TooltipHandler.TipRegion(deleteRect,
                "Permanently remove this rule from the current save. Active workers return managed items and restore their saved outfit. Previously selected stock types remain managed until forgotten in a selector.");

            if (collapseChanged)
                return;

            if (rule.UiCollapsed)
            {
                CachedRuleReadiness compactReadiness = RuleReadiness(rule, component);
                y += 34f;
                string area = rule.Area?.Label ?? "No work area";
                Widgets.Label(new Rect(x, y, 100f, 22f), "Summary:");
                Widgets.Label(new Rect(x + 100f, y, width - 430f, 22f), area);
                Color compactPreviousColor = GUI.color;
                GUI.color = compactReadiness.Color;
                Widgets.Label(new Rect(rect.xMax - 320f, y, 190f, 22f), compactReadiness.Text);
                GUI.color = compactPreviousColor;
                TooltipHandler.TipRegion(new Rect(x, y, width, 22f),
                    $"Work area: {area}\n{compactReadiness.AvailabilitySummary}\nReadiness: {compactReadiness.Text}");

                Rect compactRecallRect = new Rect(rect.xMax - 120f, y - 1f, 110f, 24f);
                bool compactPreviousEnabled = GUI.enabled;
                GUI.enabled = rule.Area != null;
                if (Widgets.ButtonText(compactRecallRect,
                        rule.WorkAreaPaused ? "Resume work" : "Pause work"))
                {
                    PauseOrResumeWork(rule, component);
                }
                GUI.enabled = compactPreviousEnabled;
                TooltipHandler.TipRegion(compactRecallRect,
                    rule.WorkAreaPaused
                        ? "Resume ordinary work in this area."
                        : "Pause ordinary work in this area. Current workers return to the locker room and restore their saved apparel and primary weapon.");
                return;
            }

            y += 34f;
            Rect workLabelRect = new Rect(x, y + 4f, 100f, 24f);
            Widgets.Label(workLabelRect, "Work area:");
            TooltipHandler.TipRegion(workLabelRect, "Every eligible pawn inside, entering, or passing through this active area must wear all selected apparel and one acceptable selected primary weapon, regardless of activity. Empty apparel or weapon categories add no requirement. Examples: reactor rooms, freezers, hospitals, workshops, or defensive positions.");
            string areaLabel = rule.Area?.Label ?? "Choose work area...";
            Rect workButtonRect = new Rect(x + 100f, y, 300f, 28f);
            if (Widgets.ButtonText(workButtonRect, areaLabel))
                ShowAreaMenu(rule);
            MarkConfiguredAreaForDraw(rule.Area, workButtonRect);
            TooltipHandler.TipRegion(workButtonRect, "Select the map area where the configured apparel, personal protective equipment (PPE), or primary weapon is required. For example, select a Freezer area for parkas and warm hats.");

            y += 34f;
            const float permissionLabelWidth = 96f;
            const float permissionColumnWidth = 83f;
            string[] permissionHeaders =
                { "All", "Colonists", "Mechs", "Animals", "Guests", "Slaves", "Prisoners" };
            string[] permissionHeaderTips =
            {
                "Bulk control for every pawn group in this row. It is checked only when every individual group is allowed.",
                "Player colonists, including children. Child work watching is controlled separately below.",
                "Player-controlled mechanoids and compatible robot pawns.",
                "Tamed or player-owned animals.",
                "Friendly visiting pawns who are not members of the colony.",
                "Player-owned slaves.",
                "Prisoners, including compatible prison-labor systems."
            };
            TextAnchor previousAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            for (int column = 0; column < permissionHeaders.Length; column++)
            {
                Rect headerRect = new Rect(
                    x + permissionLabelWidth + column * permissionColumnWidth,
                    y, permissionColumnWidth, 22f);
                Widgets.Label(headerRect, permissionHeaders[column]);
                TooltipHandler.TipRegion(headerRect, permissionHeaderTips[column]);
            }
            Text.Anchor = previousAnchor;

            y += 22f;
            Rect workAccessLabelRect = new Rect(x, y + 2f, permissionLabelWidth, 24f);
            Widgets.Label(workAccessLabelRect, "Work:");
            bool allWork = AllWorkAllowed(rule);
            bool previousAllWork = allWork;
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 0, ref allWork,
                "Allow or block ordinary assigned work for every listed group.");
            if (allWork != previousAllWork)
                SetAllWork(rule, allWork);
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 1, ref rule.AllowColonistWork,
                "Allow colonists to perform ordinary assigned work in this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 2, ref rule.AllowRobotWork,
                "Allow compatible robots and mechs to perform ordinary assigned work in this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 3, ref rule.AllowAnimalWork,
                "Allow modded animals with work jobs to perform ordinary assigned work in this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 4, ref rule.AllowGuestWork,
                "Allow hosted guests, including Hospitality guests, to perform ordinary assigned work in this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 5, ref rule.AllowSlaveWork,
                "Allow player-owned slaves to perform ordinary assigned work in this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 6, ref rule.AllowPrisonerWork,
                "Allow prisoners to perform ordinary assigned work when a prison-labor system assigns it.");
            TooltipHandler.TipRegion(workAccessLabelRect,
                "Controls construction, bills, cleaning, flicking, and other ordinary assigned work. Eligible humanlike workers must satisfy this rule's apparel and primary-weapon requirements. Non-humanlike units follow access permissions but do not change outfits. Hauling and wandering remain independently configurable below.");

            y += 28f;
            Rect haulingLabelRect = new Rect(x, y + 2f, permissionLabelWidth, 24f);
            Widgets.Label(haulingLabelRect, "Hauling:");
            bool allHauling = AllHaulingAllowed(rule);
            bool previousAllHauling = allHauling;
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 0, ref allHauling,
                "Allow or block hauling for every listed group. Enabling this enables every group; disabling it disables every group.");
            if (allHauling != previousAllHauling)
                SetAllHauling(rule, allHauling);
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 1, ref rule.AllowColonistHauling,
                "Allow colonists, including children, to haul into, out of, or through this work area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 2, ref rule.AllowRobotHauling,
                "Allow player-controlled mechanoids and compatible robots to haul into, out of, or through this work area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 3, ref rule.AllowAnimalHauling,
                "Allow trained or tamed animals to haul into, out of, or through this work area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 4, ref rule.AllowGuestHauling,
                "Allow friendly guests to haul into, out of, or through this work area when their guest system permits hauling.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 5, ref rule.AllowSlaveHauling,
                "Allow player-owned slaves to haul into, out of, or through this work area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 6, ref rule.AllowPrisonerHauling,
                "Allow prisoners to haul into, out of, or through this work area when vanilla or modded prison labor permits hauling.");
            TooltipHandler.TipRegion(haulingLabelRect,
                "Choose which groups may haul into, out of, or through this work area. Eligible humanlike haulers must still satisfy configured apparel and primary-weapon requirements; non-humanlike haulers only follow access permissions. Prisoners are supported when vanilla or modded prison labor assigns hauling. Hostiles are never managed.");

            y += 28f;
            Rect wanderingLabelRect = new Rect(x, y + 2f, permissionLabelWidth, 24f);
            Widgets.Label(wanderingLabelRect, "Wandering:");
            bool allWandering = AllWanderingAllowed(rule);
            bool previousAllWandering = allWandering;
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 0, ref allWandering,
                "Allow or block autonomous wandering for every listed group. Enabling this enables every group; disabling it disables every group.");
            if (allWandering != previousAllWandering)
                SetAllWandering(rule, allWandering);
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 1, ref rule.AllowColonistWandering,
                "Allow colonists, including children, to choose autonomous wandering destinations in this work area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 2, ref rule.AllowRobotWandering,
                "Allow player-controlled mechanoids and compatible robots to choose autonomous wandering destinations in this work area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 3, ref rule.AllowAnimalWandering,
                "Allow tamed or player-owned animals to wander through or choose destinations in this work area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 4, ref rule.AllowGuestWandering,
                "Allow friendly guests to choose autonomous wandering destinations in this work area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 5, ref rule.AllowSlaveWandering,
                "Allow player-owned slaves to choose autonomous wandering destinations in this work area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 6, ref rule.AllowPrisonerWandering,
                "Allow prisoners to choose autonomous wandering destinations in this work area.");
            TooltipHandler.TipRegion(wanderingLabelRect,
                "Choose which groups may select autonomous wandering destinations in this area. This does not authorize assigned work, hauling, drafted movement, or direct player orders.");

            y += 28f;
            Rect childWatchingLabelRect = new Rect(x, y + 2f, 100f, 24f);
            Widgets.Label(childWatchingLabelRect, "Children:");
            Rect childWatchingRect = new Rect(x + 100f, y, 230f, 24f);
            DrawLeadingCheckbox(childWatchingRect, "Allow work watching", ref rule.AllowChildWorkWatching);
            TooltipHandler.TipRegion(new Rect(x, y, 330f, 26f),
                "Permit children to enter specifically to watch an adult work for learning. Leave disabled for hazardous areas; enable only for safe workshops or similar spaces. For hauling and wandering, children follow the Colonists column above.");

            y += 30f;
            Rect lockerLabelRect = new Rect(x, y + 4f, 100f, 24f);
            Widgets.Label(lockerLabelRect, "Locker room:");
            TooltipHandler.TipRegion(lockerLabelRect, "Optional staging area where pawns equip before entering and restore after leaving the protected area. Examples: a locker room, airlock, changing bay, or equipment closet.");
            string changingAreaLabel = rule.ChangingArea?.Label ?? "No locker room";
            Rect lockerButtonRect = new Rect(x + 100f, y, 300f, 28f);
            if (Widgets.ButtonText(lockerButtonRect, changingAreaLabel))
                ShowChangingAreaMenu(rule);
            MarkConfiguredAreaForDraw(rule.ChangingArea, lockerButtonRect);
            TooltipHandler.TipRegion(lockerButtonRect, "Selected apparel, PPE, and primary weapons stored here are preferred, with a map-wide fallback. After work, pawns return here, drop temporary weapons, return managed apparel to storage, and restore their saved apparel and primary weapon. Tattered saved apparel may be replaced by a valid better item after it is successfully worn. For example, place radiation suits or guard weapons in lockers beside the work area.");

            y += 34f;
            Rect bufferLabelRect = new Rect(x, y + 4f, 100f, 24f);
            Widgets.Label(bufferLabelRect, "Task buffer:");
            Rect bufferMinusRect = new Rect(x + 100f, y, 32f, 28f);
            if (Widgets.ButtonText(bufferMinusRect, "−"))
                rule.ReturnTaskBuffer = Mathf.Max(0, rule.ReturnTaskBuffer - 1);
            Rect bufferValueRect = new Rect(x + 138f, y + 4f, 110f, 24f);
            Widgets.Label(bufferValueRect, rule.ReturnTaskBuffer == 0
                ? "Immediate"
                : $"{rule.ReturnTaskBuffer} task{(rule.ReturnTaskBuffer == 1 ? "" : "s")}");
            Rect bufferPlusRect = new Rect(x + 254f, y, 32f, 28f);
            bool previousBufferEnabled = GUI.enabled;
            GUI.enabled = rule.ReturnTaskBuffer < 20;
            if (Widgets.ButtonText(bufferPlusRect, "+"))
                rule.ReturnTaskBuffer++;
            GUI.enabled = previousBufferEnabled;
            TooltipHandler.TipRegion(new Rect(bufferLabelRect.x, y, 286f, 28f),
                "Choose how many ordinary follow-up tasks a pawn may complete successfully after leaving managed work before returning to the locker room and restoring the saved outfit. Interrupted or failed tasks do not count. Immediate starts restoration once no active area still applies. Sleep outside every applicable area bypasses the buffer; sleep inside retains the complete requirement. Renewed qualifying work resets the count. Compatible overlapping rules track their own nested buffers. Pause work bypasses remaining buffered tasks.");

            CachedRuleReadiness readiness = RuleReadiness(rule, component);

            y += 34f;
            Rect gearLabelRect = new Rect(x, y + 4f, 100f, 24f);
            Widgets.Label(gearLabelRect, "Apparel:");
            TooltipHandler.TipRegion(gearLabelRect, "All selected apparel and personal protective equipment (PPE) must be worn simultaneously before entry and remain worn for every activity and protected route inside the active area. With nothing selected, this rule has no apparel requirement. Examples: radiation suit and mask, parka and tuque, firefighter suit, armor, or a uniform.");
            Rect addGearRect = new Rect(x + 100f, y, 160f, 28f);
            if (Widgets.ButtonText(addGearRect, "Choose apparel"))
                ShowApparelMenu(rule);
            TooltipHandler.TipRegion(addGearRect, "Search all loaded vanilla and modded apparel. Green entries are required by this rule; cyan entries are retained managed stock that can be added again or forgotten when no longer in use.");
            Rect clearGearRect = new Rect(x + 268f, y, 110f, 28f);
            if (Widgets.ButtonText(clearGearRect, "Clear apparel"))
            {
                component.RememberManagedStockDefinitions(rule.RequiredApparel);
                rule.RequiredApparel.Clear();
                component.NotifyRuleRequirementsChanged(
                    rule.Id, "all apparel requirements cleared");
            }
            TooltipHandler.TipRegion(clearGearRect,
                "Remove all apparel requirements from this rule. Existing stock types remain classified for managed locker storage; open Choose apparel and use Forget to release an unused stock type.");

            y += 34f;
            bool previousApparelWordWrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.Label(new Rect(x, y, 100f, 24f), "Available:");
            Widgets.Label(new Rect(x + 100f, y, width - 100f, 24f),
                readiness.ApparelAvailability);
            Text.WordWrap = previousApparelWordWrap;
            TooltipHandler.TipRegion(new Rect(x, y, width, 24f),
                "Unworn required apparel currently spawned on this map. Personal items saved for a specific pawn are not counted. Availability does not guarantee that every item is reachable or currently unreserved.\n\n" +
                $"Apparel: {readiness.ApparelAvailability}\n" +
                $"Allowed work apparel: {ApparelStandardsSummary(rule)}");

            y += 26f;
            Rect conditionLabelRect = new Rect(x, y + 4f, 100f, 24f);
            Widgets.Label(conditionLabelRect, "Condition:");
            FloatRange previousHitPoints = rule.AllowedApparelHitPoints;
            Rect conditionRangeRect = new Rect(x + 100f, y - 2f, 360f, 32f);
            Widgets.FloatRange(
                conditionRangeRect, rule.Id.GetHashCode() ^ 0x4A6F11,
                ref rule.AllowedApparelHitPoints, 0f, 1f, "HitPoints",
                ToStringStyle.PercentZero, 0f, GameFont.Small, null, 0.01f);
            if (rule.AllowedApparelHitPoints != previousHitPoints)
            {
                QueueRuleStandardChange(
                    rule, "allowed apparel condition changed");
            }
            TooltipHandler.TipRegion(
                new Rect(x, y, 470f, 30f),
                "Set the allowed hit-point range for apparel selected by this work-area rule, like a storage filter. Gear outside the range is not counted as available and will not satisfy the work-area requirement. Saved personal apparel restoration is not filtered here.");

            y += 34f;
            Rect qualityLabelRect = new Rect(x, y + 4f, 100f, 24f);
            Widgets.Label(qualityLabelRect, "Quality:");
            QualityRange previousQuality = rule.AllowedApparelQuality;
            Rect qualityRangeRect = new Rect(x + 100f, y - 2f, 360f, 32f);
            Widgets.QualityRange(
                qualityRangeRect, rule.Id.GetHashCode() ^ 0x2D917B,
                ref rule.AllowedApparelQuality);
            if (rule.AllowedApparelQuality != previousQuality)
            {
                QueueRuleStandardChange(
                    rule, "allowed apparel quality changed");
            }
            TooltipHandler.TipRegion(
                new Rect(x, y, 470f, 30f),
                "Set the allowed quality range for apparel selected by this work-area rule, like a storage filter. Apparel without a quality level remains eligible. Saved personal apparel restoration is not filtered here.");

            y += 34f;
            Rect weaponLabelRect = new Rect(x, y + 4f, 100f, 24f);
            Widgets.Label(weaponLabelRect, "Weapons:");
            TooltipHandler.TipRegion(weaponLabelRect,
                "Optionally select exact primary-weapon alternatives. A pawn equips one acceptable selected weapon before managed work starts. With nothing selected, the pawn may remain unarmed or keep any current weapon.");
            Rect weaponButtonRect = new Rect(x + 100f, y, 160f, 28f);
            if (Widgets.ButtonText(weaponButtonRect, "Choose weapons"))
                ShowWeaponMenu(rule);
            TooltipHandler.TipRegion(weaponButtonRect,
                "Search loaded vanilla and modded primary weapons. Green entries are alternatives selected by this rule; cyan entries are retained managed stock. When both weapon types are selected, higher Shooting prefers ranged and higher Melee prefers melee; the locker and then the map are searched for that type before falling back to the weaker category. Tied pawns are distributed across valid selections. Drafted and player- or mod-controlled choices are retained during work.");
            Rect clearWeaponRect = new Rect(x + 268f, y, 110f, 28f);
            if (Widgets.ButtonText(clearWeaponRect, "Clear weapons"))
            {
                component.RememberManagedStockDefinitions(rule.RequiredWeapons);
                rule.ClearWeapons();
                component.NotifyRuleRequirementsChanged(
                    rule.Id, "all weapon requirements cleared");
            }
            TooltipHandler.TipRegion(clearWeaponRect,
                "Remove every primary-weapon requirement from this rule. Existing stock types remain classified for managed locker storage; open Choose weapons and use Forget to release an unused stock type.");

            y += 34f;
            bool previousWeaponWordWrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.Label(new Rect(x, y, 100f, 24f), "Available:");
            Widgets.Label(new Rect(x + 100f, y, width - 100f, 24f),
                readiness.WeaponAvailability);
            Text.WordWrap = previousWeaponWordWrap;
            TooltipHandler.TipRegion(new Rect(x, y, width, 24f),
                "Unequipped required primary weapons currently spawned on this map. Each exact weapon type is counted separately. Personal items saved for a specific pawn are not counted. Availability does not guarantee that every item is reachable or currently unreserved.\n\n" +
                $"Weapons: {readiness.WeaponAvailability}\n" +
                $"Allowed work weapons: {WeaponStandardsSummary(rule)}");

            y += 26f;
            Rect weaponConditionLabelRect = new Rect(x, y + 4f, 100f, 24f);
            Widgets.Label(weaponConditionLabelRect, "Condition:");
            FloatRange previousWeaponHitPoints = rule.AllowedWeaponHitPoints;
            Rect weaponConditionRangeRect = new Rect(
                x + 100f, y - 2f, 360f, 32f);
            Widgets.FloatRange(
                weaponConditionRangeRect, rule.Id.GetHashCode() ^ 0x61C2B7,
                ref rule.AllowedWeaponHitPoints, 0f, 1f, "HitPoints",
                ToStringStyle.PercentZero, 0f, GameFont.Small, null, 0.01f);
            if (rule.AllowedWeaponHitPoints != previousWeaponHitPoints)
            {
                QueueRuleStandardChange(
                    rule, "allowed weapon condition changed");
            }
            TooltipHandler.TipRegion(
                new Rect(x, y, 470f, 30f),
                "Set the allowed hit-point range for primary weapons selected by this work-area rule, like a storage filter. Weapons outside the range are not counted as available and will not satisfy the work-area requirement. Saved personal weapon restoration is not filtered here.");

            y += 34f;
            Rect weaponQualityLabelRect = new Rect(x, y + 4f, 100f, 24f);
            Widgets.Label(weaponQualityLabelRect, "Quality:");
            QualityRange previousWeaponQuality = rule.AllowedWeaponQuality;
            Rect weaponQualityRangeRect = new Rect(
                x + 100f, y - 2f, 360f, 32f);
            Widgets.QualityRange(
                weaponQualityRangeRect, rule.Id.GetHashCode() ^ 0x176D49,
                ref rule.AllowedWeaponQuality);
            if (rule.AllowedWeaponQuality != previousWeaponQuality)
            {
                QueueRuleStandardChange(
                    rule, "allowed weapon quality changed");
            }
            TooltipHandler.TipRegion(
                new Rect(x, y, 470f, 30f),
                "Set the allowed quality range for primary weapons selected by this work-area rule, like a storage filter. Weapons without a quality level remain eligible. Saved personal weapon restoration is not filtered here.");

            y += 34f;
            List<State.PawnApparelState> managedWorkers = component.PawnStates
                .Where(state => state?.Pawn?.RaceProps?.Humanlike == true &&
                                state.Pawn.apparel != null &&
                                TracksRule(state, rule.Id) &&
                                !DisplaysAsAccessActivity(state, rule))
                .ToList();
            CachedRuleActivity activity = RuleActivity(rule);
            int workerCount = managedWorkers.Count + activity.QualifyingWorkers.Count;
            Widgets.Label(new Rect(x, y, 100f, 22f), "Readiness:");
            Color previousColor = GUI.color;
            GUI.color = readiness.Color;
            Widgets.Label(new Rect(x + 100f, y, width - 220f, 22f), readiness.Text);
            GUI.color = previousColor;
            TooltipHandler.TipRegion(new Rect(x, y, width, 22f),
                "Checks whether this rule currently accepts work, has a work area and at least one apparel or weapon requirement, has storage inside its optional locker room, and has all required apparel plus at least one acceptable primary weapon available or already in use. Work paused applies immediately; Resume work reopens the area even if previous workers are still returning managed items. Active — shared cells paused means work continues outside an overlapping paused area. Blocked — work area covered means paused overlaps cover every work cell. Pawns still obey normal reachability and reservation rules.");
            Rect recallRect = new Rect(rect.xMax - 120f, y - 1f, 110f, 24f);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = rule.Area != null;
            if (Widgets.ButtonText(recallRect, rule.WorkAreaPaused ? "Resume work" : "Pause work"))
                PauseOrResumeWork(rule, component);
            GUI.enabled = previousEnabled;
            TooltipHandler.TipRegion(recallRect,
                rule.WorkAreaPaused
                    ? "Resume ordinary work in this area. The rule remains active and pawns will equip its required apparel and weapons again when qualifying jobs are assigned."
                    : "Pause ordinary work in this area and send all current workers back to the locker room to return managed apparel and weapons, then restore their saved apparel and primary weapon. The rule itself remains active. Drafted orders and paths crossing the area remain under normal RimWorld control.");

            y += 28f;
            Widgets.Label(new Rect(x, y, 100f, 22f), "Workers:");
            if (workerCount == 0)
            {
                Widgets.Label(new Rect(x + 100f, y, width - 100f, 22f), "No active or returning workers");
            }
            else
            {
                for (int workerIndex = 0; workerIndex < managedWorkers.Count; workerIndex++)
                {
                    State.PawnApparelState state = managedWorkers[workerIndex];
                    string fullStatus = PawnAutomaticOutfitStatus.Build(state.Pawn) ?? "Automatic Outfit Manager: Active";
                    int detailStart = fullStatus.IndexOf('\n');
                    string headline = detailStart >= 0
                        ? fullStatus.Substring(0, detailStart)
                        : fullStatus;
                    string shortStatus = headline.Replace("Automatic Outfit Manager: ", "");
                    string hoverDetails = detailStart >= 0
                        ? fullStatus.Substring(detailStart + 1)
                        : null;
                    float workerY = y + workerIndex * 22f;
                    Rect workerRect = new Rect(x + 100f, workerY, width - 170f, 22f);
                    Widgets.DrawHighlightIfMouseover(workerRect);
                    Widgets.Label(workerRect, $"{state.Pawn.LabelShortCap} — {shortStatus}");
                    if (Widgets.ButtonInvisible(workerRect))
                        CameraJumper.TryJumpAndSelect(state.Pawn);
                    string jumpHint = $"Click to select and jump to {state.Pawn.LabelShortCap}.";
                    TooltipHandler.TipRegion(workerRect,
                        string.IsNullOrEmpty(hoverDetails)
                            ? jumpHint
                            : $"{hoverDetails}\n\n{jumpHint}");

                    Rect returnWorkerRect = new Rect(rect.xMax - 70f, workerY, 60f, 22f);
                    if (Widgets.ButtonText(returnWorkerRect, "Recall"))
                        ReturnWorker(state);
                    bool ownsManagedGear = state.ApparelInterventionActive ||
                        state.WeaponInterventionActive;
                    TooltipHandler.TipRegion(returnWorkerRect,
                        ownsManagedGear
                            ? $"Recall only {state.Pawn.LabelShortCap} from managed work. They return to the locker room when one is configured, return managed apparel and weapons, and restore their exact saved apparel and primary weapon. Work remains active for other workers."
                            : $"Recall only {state.Pawn.LabelShortCap} from managed work. They return to the locker room when one is configured, then AOM clears the task and session without changing or claiming their already-compliant personal outfit. Normal RimWorld task selection resumes immediately, and work remains active for other workers.");
                }

                for (int workerIndex = 0;
                     workerIndex < activity.QualifyingWorkers.Count;
                     workerIndex++)
                {
                    CachedActivityEntry entry =
                        activity.QualifyingWorkers[workerIndex];
                    Pawn worker = entry.Pawn;
                    if (worker == null || worker.Destroyed)
                        continue;

                    float workerY = y +
                        (managedWorkers.Count + workerIndex) * 22f;
                    Rect workerRect = new Rect(
                        x + 100f, workerY, width - 170f, 22f);
                    Widgets.DrawHighlightIfMouseover(workerRect);
                    Color workerColor = GUI.color;
                    if (entry.MissingRequiredGear)
                        GUI.color = Color.red;
                    Widgets.Label(workerRect,
                        $"{worker.LabelShortCap} — {entry.Report}");
                    GUI.color = workerColor;
                    if (Widgets.ButtonInvisible(workerRect))
                        CameraJumper.TryJumpAndSelect(worker);

                    Rect fallbackRecallRect = new Rect(
                        rect.xMax - 78f, workerY, 68f, 22f);
                    if (entry.MissingRequiredGear)
                    {
                        Color untrackedColor = GUI.color;
                        GUI.color = Color.red;
                        TextAnchor untrackedPreviousAnchor = Text.Anchor;
                        Text.Anchor = TextAnchor.MiddleCenter;
                        Widgets.Label(fallbackRecallRect, "Untracked");
                        Text.Anchor = untrackedPreviousAnchor;
                        GUI.color = untrackedColor;
                    }
                    else if (Widgets.ButtonText(fallbackRecallRect, "Recall"))
                    {
                        State.PawnApparelState tracked =
                            component.TrackCompliantWorkSession(
                                worker,
                                worker.CurJob,
                                RuleEvaluator.MatchingRules(worker, worker.CurJob));
                        activityCache.Remove(rule.Id);
                        ReturnWorker(tracked);
                    }

                    string status = entry.MissingRequiredGear
                        ? "This qualifying worker is missing required gear but has no active AOM outfit session. This indicates a protection failure and should be reported with the player log."
                        : $"Recall only {worker.LabelShortCap} from managed work. They return to the locker room when one is configured, then AOM clears the task and session without changing or claiming their already-compliant personal outfit. Normal RimWorld task selection resumes immediately.";
                    TooltipHandler.TipRegion(workerRect,
                        $"{status}\n\nClick to select and jump to {worker.LabelShortCap}.");
                    TooltipHandler.TipRegion(fallbackRecallRect, status);
                }
            }

            y += Mathf.Max(1, workerCount) * 22f + 4f;
            DrawActivityRow("Haulers:", activity.Haulers, x, y, width,
                "Actors currently hauling through or into this work area. This is controlled by the Hauling access row.");
            y += Mathf.Max(1, activity.Haulers.Count) * 22f + 4f;
            DrawActivityRow("Wanderers:", activity.Wanderers, x, y, width,
                "Actors currently wandering in or through this work area. This is controlled by the Wandering access row.");
        }

        private static void DrawActivityRow(
            string label, List<CachedActivityEntry> actors, float x, float y, float width, string tooltip)
        {
            Widgets.Label(new Rect(x, y, 100f, 22f), label);
            if (actors.Count == 0)
            {
                Widgets.Label(new Rect(x + 100f, y, width - 100f, 22f), "None");
                TooltipHandler.TipRegion(new Rect(x, y, width, 22f), tooltip);
                return;
            }

            for (int index = 0; index < actors.Count; index++)
            {
                CachedActivityEntry entry = actors[index];
                Pawn actor = entry.Pawn;
                if (actor == null || actor.Destroyed)
                    continue;
                float actorY = y + index * 22f;
                Rect actorRect = new Rect(x + 100f, actorY, width - 100f, 22f);
                Widgets.DrawHighlightIfMouseover(actorRect);
                Widgets.Label(actorRect,
                    $"{actor.LabelShortCap} — {entry.Report}");
                if (Widgets.ButtonInvisible(actorRect))
                    CameraJumper.TryJumpAndSelect(actor);
                TooltipHandler.TipRegion(actorRect,
                    $"{tooltip} The resolved job report identifies the actual item and destination when RimWorld provides them.\n\nClick to select and jump to {actor.LabelShortCap}.");
            }
        }

        private static bool TracksRule(State.PawnApparelState state, string ruleId)
        {
            if (state == null || string.IsNullOrEmpty(ruleId))
                return false;

            return state.ActiveRuleId == ruleId ||
                   state.CurrentRuleIds?.Contains(ruleId) == true ||
                   state.NestedRuleBuffers?.Any(progress =>
                       progress != null && progress.RuleId == ruleId) == true;
        }

        private static bool DisplaysAsAccessActivity(
            State.PawnApparelState state, ApparelRule rule)
        {
            if (state?.Pawn == null || rule == null ||
                state.Transition == State.ApparelTransition.Preparing ||
                state.Transition == State.ApparelTransition.ReturningToChangingArea ||
                state.Transition == State.ApparelTransition.Restoring)
            {
                return false;
            }

            Job job = ActivityJobFor(state);
            return Patches.PausedAreaWorkFilter
                       .IsCurrentHaulingActivityForRule(
                           state.Pawn, job, rule) ||
                   Patches.PausedAreaWorkFilter
                       .IsCurrentWanderingActivityForRule(
                           state.Pawn, job, rule);
        }

        private static Job ActivityJobFor(State.PawnApparelState state)
        {
            if (state?.Transition == State.ApparelTransition.Preparing &&
                state.PendingWorkJob != null)
            {
                return state.PendingWorkJob;
            }

            return state?.Pawn?.CurJob;
        }

        private float RuleHeight(
            ApparelRule rule,
            AutomaticOutfitManagerGameComponent component)
        {
            if (rule?.UiCollapsed == true)
                return 70f;

            int managedWorkerCount = component.PawnStates.Count(state =>
                state?.Pawn?.RaceProps?.Humanlike == true && state.Pawn.apparel != null &&
                TracksRule(state, rule.Id) &&
                !DisplaysAsAccessActivity(state, rule));
            CachedRuleActivity activity = RuleActivity(rule);
            int workerCount = managedWorkerCount + activity.QualifyingWorkers.Count;
            int haulerCount = activity.Haulers.Count;
            int wandererCount = activity.Wanderers.Count;
            float activityHeight = 8f + Mathf.Max(1, haulerCount) * 22f +
                                   Mathf.Max(1, wandererCount) * 22f;
            return Mathf.Max(590f, 568f + Mathf.Max(1, workerCount) * 22f +
                activityHeight);
        }

        private void QueueRuleStandardChange(
            ApparelRule rule, string reason)
        {
            if (rule == null || string.IsNullOrEmpty(rule.Id))
                return;

            readinessCache.Remove(rule.Id);
            pendingRuleStandardChanges[rule.Id] = reason;
            pendingRuleStandardChangeTimes[rule.Id] = Time.realtimeSinceStartup;
        }

        private void FlushPendingRuleStandardChanges(
            AutomaticOutfitManagerGameComponent component, bool force = false)
        {
            // Storage-style range widgets update continuously while a handle is
            // dragged and can emit several distinct mouse releases while their
            // discrete values settle. Commit only after the editor has been
            // quiet long enough to represent one finished adjustment, and force
            // the final value through when the tab closes.
            if (component == null || pendingRuleStandardChanges.Count == 0)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            foreach (KeyValuePair<string, string> change in
                     pendingRuleStandardChanges.ToList())
            {
                pendingRuleStandardChangeTimes.TryGetValue(
                    change.Key, out float changedAt);
                if (!force &&
                    now - changedAt < RuleStandardChangeQuietSeconds)
                {
                    continue;
                }

                component.NotifyRuleRequirementsChanged(
                    change.Key, change.Value);
                pendingRuleStandardChanges.Remove(change.Key);
                pendingRuleStandardChangeTimes.Remove(change.Key);
            }
        }

        private CachedRuleActivity RuleActivity(ApparelRule rule)
        {
            string key = rule?.Id ?? string.Empty;
            Map map = rule?.Area?.Map;
            float now = Time.realtimeSinceStartup;
            if (activityCache.TryGetValue(key, out CachedRuleActivity cached) &&
                cached.Map == map && now - cached.CreatedAt < ActivityCacheSeconds)
            {
                cached.QualifyingWorkers.RemoveAll(entry =>
                    AutomaticOutfitManagerGameComponent.Current?
                        .StateFor(entry?.Pawn) != null);
                return cached;
            }

            cached = new CachedRuleActivity
            {
                CreatedAt = now,
                Map = map
            };

            IReadOnlyList<Pawn> pawns = map?.mapPawns?.AllPawnsSpawned;
            if (pawns != null)
            {
                foreach (Pawn pawn in pawns)
                {
                    var job = pawn?.CurJob;
                    if (job == null)
                        continue;

                    State.PawnApparelState trackedState =
                        AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn);
                    bool trackedTransition = TracksRule(trackedState, rule.Id) &&
                        (trackedState.Transition == State.ApparelTransition.Preparing ||
                         trackedState.Transition ==
                             State.ApparelTransition.ReturningToChangingArea ||
                         trackedState.Transition ==
                             State.ApparelTransition.Restoring);
                    if (trackedTransition)
                    {
                        // Preparation, return, and restoration have their own
                        // Worker headline. A preserved or transitional haul job
                        // must not duplicate the pawn in Haulers at the same time.
                        continue;
                    }

                    Job activityJob = ActivityJobFor(trackedState) ?? job;
                    bool hauling = Patches.PausedAreaWorkFilter
                        .IsCurrentHaulingActivityForRule(
                            pawn, activityJob, rule);
                    bool wandering = !hauling && Patches.PausedAreaWorkFilter
                        .IsCurrentWanderingActivityForRule(
                            pawn, activityJob, rule);
                    if (hauling || wandering)
                    {
                        string activityReport = activityJob.GetReport(pawn);
                        if (string.IsNullOrEmpty(activityReport))
                            activityReport = activityJob.def?.label ?? "Idle";
                        var activityEntry = new CachedActivityEntry
                        {
                            Pawn = pawn,
                            Report = activityReport.CapitalizeFirst()
                        };
                        (hauling ? cached.Haulers : cached.Wanderers)
                            .Add(activityEntry);
                        continue;
                    }

                    // Tracked work states already have a Worker headline.
                    // Hauling and wandering are classified first so retained
                    // outfit sessions appear only in their access-activity row.
                    if (TracksRule(trackedState, rule.Id))
                        continue;

                    bool managedWorkContext =
                        !Patches.PausedAreaWorkFilter.IsHaulingJob(job) &&
                        (job.workGiverDef != null ||
                         job.jobGiver is JobGiver_Work ||
                         job.playerForced);
                    if (managedWorkContext &&
                        RuleEvaluator.MatchesRule(pawn, job, rule))
                    {
                        string workerReport = job.GetReport(pawn);
                        if (string.IsNullOrEmpty(workerReport))
                            workerReport = job.def?.label ?? "Working";
                        cached.QualifyingWorkers.Add(new CachedActivityEntry
                        {
                            Pawn = pawn,
                            Report = workerReport.CapitalizeFirst(),
                            MissingRequiredGear =
                                RuleEvaluator.HasMissingRequiredGear(pawn, rule)
                        });
                        continue;
                    }

                }
            }

            activityCache[key] = cached;
            return cached;
        }

        private static bool IsAutomatedUnit(Pawn pawn)
        {
            if (pawn?.RaceProps == null || pawn.RaceProps.Humanlike)
                return false;

            if (pawn.RaceProps.IsMechanoid)
                return true;

            string defName = pawn.def?.defName ?? string.Empty;
            return defName.IndexOf("bot", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defName.IndexOf("robot", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void DrawLeadingCheckbox(Rect rect, string label, ref bool value)
        {
            Widgets.Checkbox(rect.x, rect.y, ref value, 24f);
            Widgets.Label(new Rect(rect.x + 28f, rect.y, rect.width - 28f, rect.height), label);
        }

        private static void DrawPermissionCheckbox(
            float x, float y, float labelWidth, float columnWidth, int column, ref bool value,
            string tooltip)
        {
            Rect cellRect = new Rect(
                x + labelWidth + column * columnWidth, y, columnWidth, 26f);
            float checkboxX = x + labelWidth + column * columnWidth +
                              (columnWidth - 24f) / 2f;
            Widgets.Checkbox(checkboxX, y, ref value, 24f);
            TooltipHandler.TipRegion(cellRect, tooltip);
        }

        private static bool AllHaulingAllowed(ApparelRule rule) =>
            rule.AllowColonistHauling && rule.AllowRobotHauling &&
            rule.AllowAnimalHauling && rule.AllowGuestHauling &&
            rule.AllowSlaveHauling && rule.AllowPrisonerHauling;

        private static bool AllWorkAllowed(ApparelRule rule) =>
            rule.AllowColonistWork && rule.AllowRobotWork && rule.AllowAnimalWork &&
            rule.AllowGuestWork && rule.AllowSlaveWork && rule.AllowPrisonerWork;

        private static void SetAllWork(ApparelRule rule, bool value)
        {
            rule.AllowColonistWork = value;
            rule.AllowRobotWork = value;
            rule.AllowAnimalWork = value;
            rule.AllowGuestWork = value;
            rule.AllowSlaveWork = value;
            rule.AllowPrisonerWork = value;
        }

        private static void SetAllHauling(ApparelRule rule, bool value)
        {
            rule.AllowColonistHauling = value;
            rule.AllowRobotHauling = value;
            rule.AllowAnimalHauling = value;
            rule.AllowGuestHauling = value;
            rule.AllowSlaveHauling = value;
            rule.AllowPrisonerHauling = value;
        }

        private static bool AllWanderingAllowed(ApparelRule rule) =>
            rule.AllowColonistWandering && rule.AllowRobotWandering &&
            rule.AllowAnimalWandering && rule.AllowGuestWandering &&
            rule.AllowSlaveWandering && rule.AllowPrisonerWandering;

        private static void SetAllWandering(ApparelRule rule, bool value)
        {
            rule.AllowColonistWandering = value;
            rule.AllowRobotWandering = value;
            rule.AllowAnimalWandering = value;
            rule.AllowGuestWandering = value;
            rule.AllowSlaveWandering = value;
            rule.AllowPrisonerWandering = value;
        }

        private static void ToggleWorkPause(
            ApparelRule rule,
            AutomaticOutfitManagerGameComponent component)
        {
            rule.WorkAreaPaused = !rule.WorkAreaPaused;
            RuleEvaluator.ResetRuntimeCache();
            if (rule.Area?.Map == null)
                return;

            List<State.PawnApparelState> areaWorkers = component.PawnStates
                .Where(state => state?.Pawn != null &&
                    TracksRule(state, rule.Id))
                .ToList();

            if (!rule.WorkAreaPaused)
            {
                foreach (State.PawnApparelState state in areaWorkers)
                    component.TryCancelRulePauseRecall(state, rule);
                return;
            }

            foreach (State.PawnApparelState state in areaWorkers)
            {
                // Pause affects ordinary work only. A haul explicitly allowed
                // by this rule must keep its outfit transition and current job;
                // recalling it here makes the same haul restart indefinitely.
                if (!Patches.PausedAreaWorkFilter.HasPermittedHaulingContext(
                        state, rule))
                {
                    component.RequestRulePauseRecall(state, rule);
                }
            }

            // Existing untracked work is enforced by the game component on its
            // next scheduled tick. Do not mutate pawn job trackers from OnGUI:
            // an exception in another mod's job can otherwise abort this window
            // and leave the pause operation only partially applied.
        }

        private static void PauseOrResumeWork(
            ApparelRule rule,
            AutomaticOutfitManagerGameComponent component)
        {
            ToggleWorkPause(rule, component);
        }

        private static void MarkConfiguredAreaForDraw(
            Area configuredArea, Rect buttonRect)
        {
            Event currentEvent = Event.current;
            if (configuredArea == null || currentEvent == null ||
                !buttonRect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            Map currentMap = Find.CurrentMap;
            if (currentMap == null)
                return;

            // Gravship placement creates new Area objects. The placement/load
            // remapper normally updates the rule immediately, but UI hover must
            // remain truthful during the small window where an old source-map
            // reference can still be present. Resolve only an exact signature
            // match on the map the player is currently viewing; ordinary maps
            // with merely similar names remain untouched.
            Area drawArea = configuredArea.Map == currentMap
                ? configuredArea
                : Patches.GravshipAreaRemapper.FindSignatureMatch(
                    currentMap, configuredArea);
            drawArea?.MarkForDraw();
        }

        private CachedRuleReadiness RuleReadiness(
            ApparelRule rule,
            AutomaticOutfitManagerGameComponent component)
        {
            float now = Time.realtimeSinceStartup;
            if (readinessCache.TryGetValue(
                    rule.Id, out CachedRuleReadiness cached) &&
                now - cached.CreatedAt < ReadinessCacheSeconds)
            {
                return cached;
            }

            Map map = Find.CurrentMap;
            List<ApparelRule> overlappingRules =
                ApparelCompatibility.OverlappingRules(rule);
            List<ApparelRule> pausedOverlaps =
                ApparelCompatibility.PausedOverlappingRules(rule);
            bool workAreaCoveredByPausedOverlaps =
                WorkAreaCoveredBy(rule, pausedOverlaps);
            string text;
            Color color;
            Dictionary<ThingDef, int> availableCounts = (rule.RequiredApparel ?? new List<ThingDef>())
                .Where(def => def != null)
                .Distinct()
                .ToDictionary(def => def,
                    def => AvailableGearCount(def, rule, map, component));
            ApparelConflict apparelConflict =
                ApparelCompatibility.FindConflict(overlappingRules);
            bool compatibleWeaponRequirements =
                RuleEvaluator.TryCombinedWeaponRequirement(
                    overlappingRules, out _);
            RuleEvaluator.TryCombinedWeaponRequirement(
                new[] { rule }, out CombinedWeaponRequirement ruleWeaponRequirement);
            List<ThingDef> exactWeaponDefs = (rule.RequiredWeapons ?? new List<ThingDef>())
                .Where(def => def?.IsWeapon == true)
                .Distinct()
                .ToList();
            Dictionary<ThingDef, int> availableWeaponCounts = exactWeaponDefs
                .ToDictionary(def => def,
                    def => AvailableWeaponCount(
                        def, ruleWeaponRequirement, map, component));
            int availableWeaponCount = exactWeaponDefs.Count > 0
                ? availableWeaponCounts.Values.Sum()
                : AvailableWeaponCount(ruleWeaponRequirement, map, component);
            var apparelSummaries = availableCounts.Select(pair =>
                    $"{pair.Key.LabelCap}: {pair.Value} available")
                .ToList();
            string apparelAvailability = apparelSummaries.Count == 0
                ? "Any apparel"
                : string.Join(", ", apparelSummaries);
            string weaponAvailability;
            if (exactWeaponDefs.Count > 0)
            {
                weaponAvailability = string.Join(", ", exactWeaponDefs.Select(def =>
                    $"{def.LabelCap}: {availableWeaponCounts[def]} available"));
            }
            else
            {
                weaponAvailability = rule.HasWeaponRequirement
                    ? $"{rule.WeaponSummary}: {availableWeaponCount} available"
                    : "Any weapon";
            }
            string availabilitySummary =
                $"Apparel: {apparelAvailability}\n" +
                $"Allowed work apparel: {ApparelStandardsSummary(rule)}\n" +
                $"Weapons: {weaponAvailability}\n" +
                $"Allowed work weapons: {WeaponStandardsSummary(rule)}";
            if (!rule.Enabled)
            {
                text = "Disabled";
                color = Color.yellow;
            }
            else if (rule.WorkAreaPaused)
            {
                text = "Work paused";
                color = Color.yellow;
            }
            else if (rule.Area == null)
            {
                text = "Missing work area";
                color = Color.yellow;
            }
            else if ((rule.RequiredApparel == null || rule.RequiredApparel.Count == 0) &&
                     !rule.HasWeaponRequirement)
            {
                text = "No outfit requirements selected";
                color = Color.yellow;
            }
            else if (apparelConflict != null)
            {
                text = $"Blocked — incompatible apparel: {apparelConflict.Label}";
                color = Color.red;
            }
            else if (!compatibleWeaponRequirements)
            {
                text = "Blocked — incompatible overlapping primary-weapon requirements or standards";
                color = Color.red;
            }
            else if (workAreaCoveredByPausedOverlaps)
            {
                string names = string.Join(", ", pausedOverlaps.Select(overlap => overlap.Name));
                text = $"Blocked — work area covered by paused: {names}";
                color = Color.yellow;
            }
            else if (rule.ChangingArea != null && map != null &&
                     rule.ChangingArea.Map == map &&
                     !rule.ChangingArea.ActiveCells.Any(cell => cell.GetSlotGroup(map) != null))
            {
                text = "Locker room has no storage";
                color = Color.yellow;
            }
            else
            {
                List<ThingDef> unavailable = rule.RequiredApparel
                    .Where(def => def != null && availableCounts[def] == 0 &&
                        !RequiredGearInUse(def, rule, map, component))
                    .ToList();
                if (unavailable.Count > 0)
                {
                    text = $"Required apparel unavailable: {string.Join(", ", unavailable.Select(def => def.LabelCap.ToString()))}";
                    color = Color.yellow;
                }
                else if (rule.HasWeaponRequirement &&
                         availableWeaponCount == 0 &&
                         !RequiredWeaponInUse(ruleWeaponRequirement, map))
                {
                    text = $"Required weapon unavailable: {rule.WeaponSummary}";
                    color = Color.yellow;
                }
                else
                {
                    if (pausedOverlaps.Count > 0)
                    {
                        string names = string.Join(", ",
                            pausedOverlaps.Select(overlap => overlap.Name));
                        text = $"Active — shared cells paused: {names}";
                        color = Color.yellow;
                    }
                    else
                    {
                        text = "Active";
                        color = Color.green;
                    }
                }
            }

            cached = new CachedRuleReadiness
            {
                CreatedAt = now,
                Text = text,
                ApparelAvailability = apparelAvailability,
                WeaponAvailability = weaponAvailability,
                AvailabilitySummary = availabilitySummary,
                Color = color
            };
            readinessCache[rule.Id] = cached;
            return cached;
        }

        private static bool WorkAreaCoveredBy(
            ApparelRule rule, List<ApparelRule> coveringRules)
        {
            if (rule?.Area == null || coveringRules == null || coveringRules.Count == 0)
                return false;

            bool hasActiveCell = false;
            foreach (IntVec3 cell in rule.Area.ActiveCells)
            {
                hasActiveCell = true;
                if (!coveringRules.Any(candidate =>
                        candidate?.Area?.Map == rule.Area.Map && candidate.Area[cell]))
                {
                    return false;
                }
            }

            return hasActiveCell;
        }

        private static int AvailableGearCount(
            ThingDef def,
            ApparelRule rule,
            Map map,
            AutomaticOutfitManagerGameComponent component)
        {
            if (map?.listerThings == null)
                return 0;

            return map.listerThings.ThingsOfDef(def).Count(thing =>
                thing is Apparel apparel &&
                apparel.Spawned &&
                !apparel.Destroyed &&
                rule.Allows(apparel) &&
                component.SavedPawnFor(apparel) == null);
        }

        private static bool RequiredGearInUse(
            ThingDef def,
            ApparelRule rule,
            Map map,
            AutomaticOutfitManagerGameComponent component)
        {
            return component.PawnStates.Any(state => state?.Pawn?.Map == map &&
                state.Pawn.apparel?.WornApparel.Any(apparel => apparel?.def == def &&
                    rule.Allows(apparel) &&
                    state.ManagedApparel?.Contains(apparel) == true) == true);
        }

        private static string ApparelStandardsSummary(ApparelRule rule)
        {
            if (rule == null)
                return "any condition and quality";

            return StandardsSummary(
                rule.AllowedApparelHitPoints, rule.AllowedApparelQuality);
        }

        private static string WeaponStandardsSummary(ApparelRule rule)
        {
            if (rule == null)
                return "any condition and quality";

            return StandardsSummary(
                rule.AllowedWeaponHitPoints, rule.AllowedWeaponQuality);
        }

        private static string StandardsSummary(
            FloatRange allowedHitPoints, QualityRange quality)
        {
            string hitPoints =
                $"{Mathf.RoundToInt(allowedHitPoints.min * 100f)}%–" +
                $"{Mathf.RoundToInt(allowedHitPoints.max * 100f)}% hit points";
            string qualityLabel = quality == QualityRange.All
                ? "any quality"
                : quality.min == quality.max
                    ? quality.min.ToString().ToLowerInvariant()
                    : $"{quality.min.ToString().ToLowerInvariant()}–" +
                      quality.max.ToString().ToLowerInvariant();
            return $"{hitPoints}, {qualityLabel}";
        }

        private static int AvailableWeaponCount(
            ThingDef def,
            CombinedWeaponRequirement requirement,
            Map map,
            AutomaticOutfitManagerGameComponent component)
        {
            if (def?.IsWeapon != true || map?.listerThings == null)
                return 0;

            return map.listerThings.ThingsOfDef(def)
                .Count(thing => thing is ThingWithComps weapon &&
                                 weapon.Spawned && !weapon.Destroyed &&
                                 requirement?.Matches(weapon) == true &&
                                 component.SavedPawnForWeapon(weapon) == null);
        }

        private static int AvailableWeaponCount(
            CombinedWeaponRequirement requirement,
            Map map,
            AutomaticOutfitManagerGameComponent component)
        {
            if (requirement?.HasRequirement != true || map?.listerThings == null)
                return 0;

            return map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
                .Count(thing => thing is ThingWithComps weapon &&
                                weapon.Spawned && !weapon.Destroyed &&
                                requirement.Matches(weapon) &&
                                component.SavedPawnForWeapon(weapon) == null);
        }

        private static bool RequiredWeaponInUse(
            CombinedWeaponRequirement requirement, Map map)
        {
            return requirement?.HasRequirement == true &&
                   map?.mapPawns?.AllPawnsSpawned.Any(pawn =>
                       requirement.Matches(pawn?.equipment?.Primary)) == true;
        }

        private static void ReturnWorker(State.PawnApparelState state)
        {
            if (state?.Pawn == null)
                return;

            AutomaticOutfitManagerGameComponent.Current?
                .RequestRecall(state);
        }

        private static void ShowAreaMenu(ApparelRule rule)
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return;

            var options = new List<FloatMenuOption>();
            foreach (Area area in map.areaManager.AllAreas.Where(a => a != null))
            {
                Area captured = area;
                options.Add(new FloatMenuOption(captured.Label, () =>
                {
                    rule.Area = captured;
                    RuleEvaluator.ResetRuntimeCache();
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void ShowChangingAreaMenu(ApparelRule rule)
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return;

            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("No locker room (restore after reaching a safe exterior cell)", () =>
                {
                    rule.ChangingArea = null;
                    RuleEvaluator.ResetRuntimeCache();
                })
            };
            foreach (Area area in map.areaManager.AllAreas.Where(a => a != null))
            {
                Area captured = area;
                options.Add(new FloatMenuOption(captured.Label, () =>
                {
                    rule.ChangingArea = captured;
                    RuleEvaluator.ResetRuntimeCache();
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void ShowManageAreas()
        {
            Map map = Find.CurrentMap;
            if (map != null)
                Find.WindowStack.Add(new Dialog_ManageAreas(map));
        }

        private static void ShowApparelMenu(ApparelRule rule)
        {
            Find.WindowStack.Add(new ApparelSelectionWindow(rule));
        }

        private static void ShowWeaponMenu(ApparelRule rule)
        {
            Find.WindowStack.Add(new WeaponSelectionWindow(rule));
        }
    }
}
