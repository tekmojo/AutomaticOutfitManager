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
        private const float RuleTypeHeaderHeight = 30f;
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
            public string TooltipText;
            public Color Color;
        }

        private sealed class CachedActivityEntry
        {
            public Pawn Pawn;
            public string Report;
            public bool MissingRequiredGear;
            public bool ObservedOnly;
            public string StagedRepairTip;
            public string ObservedTransitionStatus;
            public State.PawnApparelState ManagedState;
            public int DisplayPriority;
        }

        private sealed class CachedRuleActivity
        {
            public float CreatedAt;
            public Map Map;
            public readonly List<CachedActivityEntry> Participants =
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
                "Set outfits and access for work or non-work areas, with optional locker-room changing.");
            y += 32f;

            Rect newRuleRect = new Rect(inRect.x, y, 180f, 30f);
            if (Widgets.ButtonText(newRuleRect, "Add Work Area Rule"))
            {
                component.Rules.Add(new ApparelRule { Name = "New Work Area Rule" });
                component.InvalidateManagedDefinitionIndexes();
            }
            TooltipHandler.TipRegion(newRuleRect, "Require an outfit before entering an area, such as protective clothing for a freezer or reactor room. Choose the area and gear after adding the rule.");
            Rect newNonWorkRect = new Rect(inRect.x + 190f, y, 215f, 30f);
            if (Widgets.ButtonText(newNonWorkRect, "Add Non-Work Area Rule"))
            {
                component.Rules.Add(new ApparelRule
                {
                    Name = "New Non-Work Area Rule", Kind = AreaRuleKind.NonWork
                });
                component.InvalidateManagedDefinitionIndexes();
            }
            TooltipHandler.TipRegion(newNonWorkRect,
                "Change out of work outfits before entering a dining room, lounge or bedroom. Prefer each pawn's saved personal outfit, or choose an outfit for this area.");
            Rect manageAreasRect = new Rect(inRect.xMax - 150f, y, 150f, 30f);
            if (Widgets.ButtonText(manageAreasRect, "Edit map areas"))
                ShowManageAreas();
            TooltipHandler.TipRegion(manageAreasRect, "Create, rename or paint the map areas used by your rules and locker rooms.");
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

            DrawRuleTypeHeader(rule, new Rect(x, y, width, 24f));
            y += RuleTypeHeaderHeight;

            Rect enabledRect = new Rect(x, y, 90f, 24f);
            bool wasEnabled = rule.Enabled;
            Widgets.CheckboxLabeled(enabledRect, "Enabled", ref rule.Enabled);
            if (rule.Enabled && !wasEnabled)
            {
                string conflict = RuleTypeStyle.ConfigurationConflictTip(rule);
                if (conflict != null)
                {
                    rule.Enabled = wasEnabled;
                    Messages.Message(conflict, MessageTypeDefOf.RejectInput, false);
                }
            }
            if (rule.Enabled != wasEnabled)
            {
                component.RememberManagedStockDefinitions(rule.RequiredApparel);
                component.RememberManagedStockDefinitions(rule.RequiredWeapons);
                component.InvalidateManagedDefinitionIndexes();
                component.NotifyRuleRequirementsChanged(
                    rule.Id, rule.Enabled ? "rule enabled" : "rule disabled");
            }
            TooltipHandler.TipRegion(enabledRect, "Turn this rule on or off. Disabling it keeps its settings and recalls pawns using its outfit.");
            Rect ruleNameRect = new Rect(x + 100f, y, width - 272f, 26f);
            rule.Name = Widgets.TextField(ruleNameRect, rule.Name ?? "");
            TooltipHandler.TipRegion(ruleNameRect, "Name this rule. Its map-area name is separate.");
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
                    : "Show a compact summary while the rule continues to work.");
            Rect deleteRect = new Rect(rect.xMax - 82f, y, 72f, 26f);
            if (Widgets.ButtonText(deleteRect, "Delete"))
            {
                string ruleName = string.IsNullOrWhiteSpace(rule.Name)
                    ? "Unnamed Rule" : RuleTypeStyle.RuleName(rule);
                string ruleType = rule.IsNonWork ? "Non-Work Area" : "Work Area";
                Find.WindowStack.Add(new Dialog_MessageBox(
                    $"Delete {ruleType} rule '{ruleName}'?\n\n" +
                    "Pawns using this rule will return managed items and restore their saved outfits. " +
                    "Its map areas will remain.\n\nThis cannot be undone.",
                    buttonAText: "Delete",
                    buttonAAction: () =>
                    {
                        // The dialog can outlive this draw and its row index.
                        // Delete only the exact rule the user confirmed.
                        if (!component.Rules.Contains(rule)) return;
                        component.RememberManagedStockDefinitions(rule.RequiredApparel);
                        component.RememberManagedStockDefinitions(rule.RequiredWeapons);
                        component.Rules.Remove(rule);
                        component.NotifyRuleRequirementsChanged(rule.Id, "rule deleted");
                    },
                    buttonBText: "Cancel",
                    title: "Delete Rule",
                    buttonADestructive: true,
                    acceptAction: () => { },
                    cancelAction: () => { }));
                return;
            }
            TooltipHandler.TipRegion(deleteRect,
                "Delete this rule after confirmation. Pawns using its outfit are recalled; its map areas remain. Use Forget in the gear selectors to release unused locker stock.");

            if (collapseChanged)
                return;

            if (rule.UiCollapsed)
            {
                CachedRuleReadiness compactReadiness = RuleReadiness(rule, component);
                y += 34f;
                string area = rule.Area?.Label ?? "No area selected";
                Rect compactRecallRect = new Rect(rect.xMax - 120f, y - 1f, 110f, 24f);
                float textStart = x + 100f;
                float textEnd = compactRecallRect.x - 10f;
                float textWidth = Mathf.Max(0f, textEnd - textStart);
                // Let long readiness messages use the available space instead
                // of clipping them to a fixed 190-pixel column. Reserve a clear
                // gap and room for the area name, truncating both independently.
                float statusWidth = Mathf.Min(Text.CalcSize(compactReadiness.Text).x + 8f,
                    Mathf.Max(0f, textWidth - 12f) * 0.7f);
                Rect compactStatusRect = new Rect(textEnd - statusWidth, y, statusWidth, 22f);
                Rect compactAreaRect = new Rect(textStart, y,
                    Mathf.Max(0f, compactStatusRect.x - textStart - 12f), 22f);
                bool previousCompactWordWrap = Text.WordWrap;
                TextAnchor previousCompactAnchor = Text.Anchor;
                Text.WordWrap = false;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(x, y, 100f, 22f), "Area:");
                Widgets.Label(compactAreaRect, area.Truncate(compactAreaRect.width));
                Color compactPreviousColor = GUI.color;
                GUI.color = compactReadiness.Color;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(compactStatusRect,
                    compactReadiness.Text.Truncate(Mathf.Max(0f, compactStatusRect.width - 4f)));
                Text.WordWrap = previousCompactWordWrap;
                Text.Anchor = previousCompactAnchor;
                GUI.color = compactPreviousColor;
                TooltipHandler.TipRegion(new Rect(x, y, Mathf.Max(0f, textEnd - x), 22f),
                    $"{(rule.IsNonWork ? "Non-Work Area" : "Work Area")}: {RuleTypeStyle.AreaName(rule.Area)}\nLocker Room: {(rule.ChangingArea == null ? "None" : RuleTypeStyle.AreaName(rule.ChangingArea))}\n{compactReadiness.AvailabilitySummary}\nReadiness: {compactReadiness.TooltipText}");

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
                        : "Pause work here and recall pawns using this rule. Access and outfit requirements stay active.");
                return;
            }

            y += 34f;
            Rect workLabelRect = new Rect(x, y + 4f, 100f, 24f);
            Widgets.Label(workLabelRect, rule.IsNonWork ? "Non-Work Area:" : "Work Area:");
            string workAreaHelp = rule.IsNonWork
                ? "Pawns return the work outfits selected under Remove Work Outfits before entering or passing through. They then use their saved personal outfit, or this rule's selected outfit according to the settings below. Gear allowed to remain stays on where compatible."
                : "Eligible pawns wear all selected apparel and one selected primary weapon before entering or passing through this area. The outfit stays on for every activity inside. An empty gear category adds no requirement.";
            TooltipHandler.TipRegion(workLabelRect, workAreaHelp);
            string areaLabel = rule.Area?.Label ?? "Choose area...";
            Rect workButtonRect = new Rect(x + 100f, y, 300f, 28f);
            if (Widgets.ButtonText(workButtonRect, areaLabel))
                ShowAreaMenu(rule);
            MarkConfiguredAreaForDraw(rule.Area, workButtonRect);
            TooltipHandler.TipRegion(workButtonRect,
                (rule.IsNonWork ? "Non-Work Area: " : "Work Area: ") +
                (rule.Area == null ? "None selected" : RuleTypeStyle.AreaName(rule.Area)) +
                "\n\n" + workAreaHelp + "\n\nHover to highlight the area; click to choose another.");

            y += 34f;
            const float permissionLabelWidth = 96f;
            const float permissionColumnWidth = 83f;
            string[] permissionHeaders =
                { "All", "Colonists", "Mechs", "Animals", "Guests", "Slaves", "Prisoners" };
            string[] permissionHeaderTips =
            {
                "Change every group in this row. Checked means all groups are allowed.",
                "Player colonists, including children when Allow Children is enabled below.",
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
            Widgets.Label(workAccessLabelRect, "Activities:");
            bool allWork = AllWorkAllowed(rule);
            bool previousAllWork = allWork;
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 0, ref allWork,
                "Allow or block assigned work and purposeful activities for every listed group.");
            if (allWork != previousAllWork)
                SetAllWork(rule, allWork);
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 1, ref rule.AllowColonistWork,
                "Allow colonists to work, eat, rest, learn and recreate in this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 2, ref rule.AllowRobotWork,
                "Allow compatible robots and mechs to perform their assigned work and purposeful activities. Cleaning uses Wandering; hauling is separate.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 3, ref rule.AllowAnimalWork,
                "Allow animals to eat, rest and perform other purposeful activities, including work supplied by mods. Hauling and idle wandering are separate.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 4, ref rule.AllowGuestWork,
                "Allow hosted guests, including Hospitality guests, to work, eat, rest, learn and recreate in this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 5, ref rule.AllowSlaveWork,
                "Allow player-owned slaves to work, eat, rest, learn and recreate in this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 6, ref rule.AllowPrisonerWork,
                "Allow prisoners to eat, rest, learn, recreate and perform work their prison-labor system permits. The same area restrictions apply as for other groups.");
            TooltipHandler.TipRegion(workAccessLabelRect,
                "Allow work, meals, rest, learning and recreation. The same categories apply to colonists, guests, slaves and prisoners. Eligible humanlike pawns must also meet the outfit requirements. Hauling is separate; robot cleaning uses Wandering. A confined pawn with no safe exit may still rest.");

            y += 28f;
            Rect haulingLabelRect = new Rect(x, y + 2f, permissionLabelWidth, 24f);
            Widgets.Label(haulingLabelRect, "Hauling:");
            bool allHauling = AllHaulingAllowed(rule);
            bool previousAllHauling = allHauling;
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 0, ref allHauling,
                "Allow or block hauling for every listed group.");
            if (allHauling != previousAllHauling)
                SetAllHauling(rule, allHauling);
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 1, ref rule.AllowColonistHauling,
                "Allow colonists, including children, to haul into, out of, or through this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 2, ref rule.AllowRobotHauling,
                "Allow player-controlled mechanoids and compatible robots to haul into, out of, or through this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 3, ref rule.AllowAnimalHauling,
                "Allow trained or tamed animals to haul into, out of, or through this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 4, ref rule.AllowGuestHauling,
                "Allow friendly guests to haul into, out of, or through this area when their guest system permits hauling.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 5, ref rule.AllowSlaveHauling,
                "Allow player-owned slaves to haul into, out of, or through this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 6, ref rule.AllowPrisonerHauling,
                "Allow prisoners to haul into, out of, or through this area when vanilla or modded prison labor permits hauling.");
            TooltipHandler.TipRegion(haulingLabelRect,
                "Allow hauling into, out of or through this area. Eligible humanlike haulers must also meet the outfit requirements. This permits hauling; it does not assign hauling work or change work priorities.");

            y += 28f;
            Rect wanderingLabelRect = new Rect(x, y + 2f, permissionLabelWidth, 24f);
            Widgets.Label(wanderingLabelRect, "Wandering:");
            bool allWandering = AllWanderingAllowed(rule);
            bool previousAllWandering = allWandering;
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 0, ref allWandering,
                "Allow or block idle wandering for every listed group.");
            if (allWandering != previousAllWandering)
                SetAllWandering(rule, allWandering);
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 1, ref rule.AllowColonistWandering,
                "Allow colonists, including children, to choose autonomous wandering destinations in this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 2, ref rule.AllowRobotWandering,
                "Allow player-controlled mechanoids and compatible robots to choose autonomous wandering destinations in this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 3, ref rule.AllowAnimalWandering,
                "Allow tamed or player-owned animals to wander through or choose destinations in this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 4, ref rule.AllowGuestWandering,
                "Allow friendly guests to choose autonomous wandering destinations in this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 5, ref rule.AllowSlaveWandering,
                "Allow player-owned slaves to choose autonomous wandering destinations in this area.");
            DrawPermissionCheckbox(x, y, permissionLabelWidth, permissionColumnWidth, 6, ref rule.AllowPrisonerWandering,
                "Allow prisoners to choose autonomous wandering destinations in this area.");
            TooltipHandler.TipRegion(wanderingLabelRect,
                "Allow idle wandering and robot cleaning. Meals, rest, learning and recreation use Activities. Required charging, emergencies and direct player orders retain their normal behavior.");

            y += 28f;
            Rect childWatchingLabelRect = new Rect(x, y + 2f, 100f, 24f);
            Widgets.Label(childWatchingLabelRect, "Children:");
            Rect childWatchingRect = new Rect(x + 100f, y, 230f, 24f);
            bool allowedChildren = rule.AllowChildren;
            DrawLeadingCheckbox(childWatchingRect, "Allow Children", ref rule.AllowChildren);
            if (allowedChildren != rule.AllowChildren) RuleEvaluator.ResetRuntimeCache();
            TooltipHandler.TipRegion(new Rect(x, y, 330f, 26f),
                "Allow children to use or pass through this area. Their group permissions and outfit requirements still apply. " +
                "When off, children already inside leave safely. Babies and carried pawns are unaffected. " +
                "Direct orders, emergencies and necessary outfit returns keep their normal exceptions. Off by default.");

            y += 30f;
            Rect lockerLabelRect = new Rect(x, y + 4f, 100f, 24f);
            Widgets.Label(lockerLabelRect, "Locker Room:");
            string lockerHelp =
                (rule.IsNonWork
                    ? "Prefer this room when collecting or returning the selected Non-Work outfit. Saved personal items are retrieved from wherever they are stored. Borrowed Work outfits return through their own rule's locker. "
                    : "Prefer gear stored here when outfitting. Pawns return here to put away borrowed gear and restore their saved personal outfit. Other reachable map stock can also be used. ") +
                "With no locker selected, pawns change back after reaching a safe cell outside the area.";
            TooltipHandler.TipRegion(lockerLabelRect, lockerHelp);
            string changingAreaLabel = rule.ChangingArea?.Label ?? "No locker room";
            Rect lockerButtonRect = new Rect(x + 100f, y, 300f, 28f);
            if (Widgets.ButtonText(lockerButtonRect, changingAreaLabel))
                ShowChangingAreaMenu(rule);
            MarkConfiguredAreaForDraw(rule.ChangingArea, lockerButtonRect);
            TooltipHandler.TipRegion(lockerButtonRect,
                "Locker Room: " + (rule.ChangingArea == null ? "None selected" : RuleTypeStyle.AreaName(rule.ChangingArea)) +
                "\n\n" + lockerHelp + "\n\nHover to highlight the locker; click to choose another.");

            y += 34f;
            Rect bufferLabelRect = new Rect(x, y + 4f, 100f, 24f);
            Widgets.Label(bufferLabelRect, "Task Buffer:");
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
                (rule.IsNonWork
                    ? "Keep the saved personal or selected outfit, including work items allowed to remain, for up to this many compatible tasks after leaving. " +
                      "If a new task needs a different outfit, the buffer ends early: leave safely, collect the required work outfit, then continue the task if possible. " +
                      "Immediate adds no extra tasks. Ending the buffer does not strip personal clothing; borrowed fallback gear still follows its normal return.\n\n"
                    : "Keep the work outfit for this many compatible tasks after leaving, then return it and restore the saved outfit. " +
                      "Immediate starts the return once no managed area still needs the outfit. New activity for this rule resets its count; overlapping rules keep separate counts.\n\n") +
                "Only successful tasks count; travel, brief waits and interrupted tasks do not. The buffer does not assign extra work. A different required outfit, sleep outside the area, Recall or Pause work can end it early. Unrelated Work Areas are avoided when a route around them exists.");

            CachedRuleReadiness readiness = RuleReadiness(rule, component);

            if (rule.IsNonWork)
            {
                y += 34f;
                const string outfitSourceLabel = "Default to Saved Personal Outfit:";
                float outfitSourceWidth = Mathf.Min(width - 150f,
                    Text.CalcSize(outfitSourceLabel).x + 36f);
                Rect outfitSourceRect = new Rect(x, y, outfitSourceWidth, 28f);
                bool previousSavedPreference = rule.DefaultToSavedPersonalOutfit;
                Widgets.CheckboxLabeled(outfitSourceRect,
                    outfitSourceLabel, ref rule.DefaultToSavedPersonalOutfit);
                if (rule.DefaultToSavedPersonalOutfit != previousSavedPreference)
                {
                    readinessCache.Remove(rule.Id);
                    component.NotifyRuleRequirementsChanged(rule.Id,
                        "saved personal outfit preference changed");
                    foreach (Pawn pawn in rule.Area?.Map?.mapPawns.AllPawnsSpawned ??
                                 Enumerable.Empty<Pawn>())
                        UnavailableWorkRegistry.Clear(pawn, new[] { rule });
                }
                Rect viewOutfitsRect = new Rect(rect.xMax - 155f, y - 2f, 145f, 28f);
                if (Widgets.ButtonText(viewOutfitsRect, "View saved outfits..."))
                    ShowSavedNonWorkOutfits(component);
                TooltipHandler.TipRegion(viewOutfitsRect,
                    "View each pawn's personal outfit saved before Work gear was issued, including whether they were unarmed. Viewing does not save or change an outfit.");
                TooltipHandler.TipRegion(outfitSourceRect,
                    "Checked: use the personal outfit saved before work, including an unarmed weapon slot. The selections below are a fallback only when no outfit has been saved; missing saved items do not activate fallback.\n\nUnchecked: use the selections below for everyone. Empty categories add no requirement. In either mode, Remove Work Outfits controls which borrowed gear is returned. This option does not save a new outfit.");
                y += 34f;
                Widgets.Label(new Rect(x, y + 4f, 150f, 24f), "Remove Work Outfits:");
                string removalLabel = (rule.RemoveAllWorkOutfits ? "All Work Outfits" :
                    rule.WorkOutfitsToRemove.Count == 0 ? "None selected" :
                    rule.WorkOutfitsToRemove.Count + " Work Area rule(s) selected") + "...";
                float removalWidth = Mathf.Min(width - 150f, Text.CalcSize(removalLabel).x + 32f);
                Rect removalRect = new Rect(x + 150f, y, removalWidth, 28f);
                if (Widgets.ButtonText(removalRect, removalLabel))
                    Find.WindowStack.Add(new WorkOutfitRemovalWindow(rule, component, () =>
                    {
                        readinessCache.Remove(rule.Id);
                        component.NotifyRuleRequirementsChanged(rule.Id, "work outfit removal selection changed");
                        foreach (Pawn pawn in rule.Area?.Map?.mapPawns.AllPawnsSpawned ?? Enumerable.Empty<Pawn>())
                            UnavailableWorkRegistry.Clear(pawn, new[] { rule });
                    }));
                TooltipHandler.TipRegion(removalRect,
                    "Choose which Work outfits to return before entry. All Work Outfits is the default. Gear shared by several rules stays on unless every source is selected. Personal clothing fills compatible slots around any work gear you keep.");
            }

            y += 34f;
            float gearLabelWidth = rule.IsNonWork ? 150f : 100f;
            Rect gearLabelRect = new Rect(x, y + 4f, gearLabelWidth, 24f);
            Widgets.Label(gearLabelRect, rule.IsNonWork && rule.DefaultToSavedPersonalOutfit ? "Fallback Apparel:" : "Apparel:");
            TooltipHandler.TipRegion(gearLabelRect, rule.IsNonWork
                ? "All selected apparel must be worn together. With saved-outfit preference on, these choices apply only when no personal outfit has been saved. With it off, they apply to everyone. Empty selections add no clothing requirement."
                : "Require every selected garment before entry and while inside. Choose items that can be worn together, such as a suit and helmet. Empty selections add no clothing requirement.");
            Rect addGearRect = new Rect(x + gearLabelWidth, y, 160f, 28f);
            if (Widgets.ButtonText(addGearRect, "Choose apparel"))
                ShowApparelMenu(rule);
            TooltipHandler.TipRegion(addGearRect, "Choose apparel from the game and your installed mods. All selected items are required together. Hover an item to see its selecting rules or conflicts.");
            Rect clearGearRect = new Rect(x + gearLabelWidth + 168f, y, 110f, 28f);
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
            Widgets.Label(new Rect(x, y, gearLabelWidth, 24f), "Available:");
            Widgets.Label(new Rect(x + gearLabelWidth, y, width - gearLabelWidth, 24f),
                readiness.ApparelAvailability);
            Text.WordWrap = previousApparelWordWrap;
            TooltipHandler.TipRegion(new Rect(x, y, width, 24f),
                "Counts matching unworn apparel on this map, excluding saved personal items. Items may still be reserved or unreachable. For saved-personal outfits, these counts describe fallback stock.\n\n" +
                $"Apparel: {readiness.ApparelAvailability}\n" +
                $"Allowed apparel: {ApparelStandardsSummary(rule)}");

            y += 26f;
            Rect conditionLabelRect = new Rect(x, y + 4f, gearLabelWidth, 24f);
            Widgets.Label(conditionLabelRect, "Condition:");
            FloatRange previousHitPoints = rule.AllowedApparelHitPoints;
            Rect conditionRangeRect = new Rect(x + gearLabelWidth, y - 2f, 360f, 32f);
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
                new Rect(x, y, gearLabelWidth + 370f, 30f),
                "Accept selected apparel within this condition range. Items outside it do not meet the rule. Saved personal apparel ignores this range.");

            y += 34f;
            Rect qualityLabelRect = new Rect(x, y + 4f, gearLabelWidth, 24f);
            Widgets.Label(qualityLabelRect, "Quality:");
            QualityRange previousQuality = rule.AllowedApparelQuality;
            Rect qualityRangeRect = new Rect(x + gearLabelWidth, y - 2f, 360f, 32f);
            Widgets.QualityRange(
                qualityRangeRect, rule.Id.GetHashCode() ^ 0x2D917B,
                ref rule.AllowedApparelQuality);
            if (rule.AllowedApparelQuality != previousQuality)
            {
                QueueRuleStandardChange(
                    rule, "allowed apparel quality changed");
            }
            TooltipHandler.TipRegion(
                new Rect(x, y, gearLabelWidth + 370f, 30f),
                "Accept selected apparel within this quality range. Items without quality are allowed. Saved personal apparel ignores this range.");

            y += 34f;
            Rect weaponLabelRect = new Rect(x, y + 4f, gearLabelWidth, 24f);
            Widgets.Label(weaponLabelRect, rule.IsNonWork && rule.DefaultToSavedPersonalOutfit ? "Fallback Weapons:" : "Weapons:");
            TooltipHandler.TipRegion(weaponLabelRect,
                rule.IsNonWork
                    ? "Choose acceptable primary weapons; each pawn equips one. With saved-outfit preference on, these choices apply only when no personal outfit has been saved. A saved unarmed outfit remains unarmed. Empty selections add no weapon requirement."
                    : "Require one of the selected primary weapons before entry and while inside. Empty selections add no weapon requirement; inventory sidearms do not count.");
            Rect weaponButtonRect = new Rect(x + gearLabelWidth, y, 160f, 28f);
            if (Widgets.ButtonText(weaponButtonRect, "Choose weapons"))
                ShowWeaponMenu(rule);
            TooltipHandler.TipRegion(weaponButtonRect,
                "Choose acceptable primary weapons from the game and your installed mods. Pawns prefer ranged or melee according to their skills when both are available. Direct player choices take priority; automatic sidearm choices do not override this rule.");
            Rect clearWeaponRect = new Rect(x + gearLabelWidth + 168f, y, 110f, 28f);
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
            Widgets.Label(new Rect(x, y, gearLabelWidth, 24f), "Available:");
            Widgets.Label(new Rect(x + gearLabelWidth, y, width - gearLabelWidth, 24f),
                readiness.WeaponAvailability);
            Text.WordWrap = previousWeaponWordWrap;
            TooltipHandler.TipRegion(new Rect(x, y, width, 24f),
                "Counts matching unequipped weapons on this map, excluding saved personal items. Items may still be reserved or unreachable. For saved-personal outfits, these counts describe fallback stock.\n\n" +
                $"Weapons: {readiness.WeaponAvailability}\n" +
                $"Allowed weapons: {WeaponStandardsSummary(rule)}");

            y += 26f;
            Rect weaponConditionLabelRect = new Rect(x, y + 4f, gearLabelWidth, 24f);
            Widgets.Label(weaponConditionLabelRect, "Condition:");
            FloatRange previousWeaponHitPoints = rule.AllowedWeaponHitPoints;
            Rect weaponConditionRangeRect = new Rect(
                x + gearLabelWidth, y - 2f, 360f, 32f);
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
                new Rect(x, y, gearLabelWidth + 370f, 30f),
                "Accept selected weapons within this condition range. Items outside it do not meet the rule. Saved personal weapons ignore this range.");

            y += 34f;
            Rect weaponQualityLabelRect = new Rect(x, y + 4f, gearLabelWidth, 24f);
            Widgets.Label(weaponQualityLabelRect, "Quality:");
            QualityRange previousWeaponQuality = rule.AllowedWeaponQuality;
            Rect weaponQualityRangeRect = new Rect(
                x + gearLabelWidth, y - 2f, 360f, 32f);
            Widgets.QualityRange(
                weaponQualityRangeRect, rule.Id.GetHashCode() ^ 0x176D49,
                ref rule.AllowedWeaponQuality);
            if (rule.AllowedWeaponQuality != previousWeaponQuality)
            {
                QueueRuleStandardChange(
                    rule, "allowed weapon quality changed");
            }
            TooltipHandler.TipRegion(
                new Rect(x, y, gearLabelWidth + 370f, 30f),
                "Accept selected weapons within this quality range. Items without quality are allowed. Saved personal weapons ignore this range.");

            y += 34f;
            CachedRuleActivity activity = RuleActivity(rule);
            int workerCount = activity.Participants.Count;
            Widgets.Label(new Rect(x, y, 100f, 22f), "Readiness:");
            Color previousColor = GUI.color;
            GUI.color = readiness.Color;
            Widgets.Label(new Rect(x + 100f, y, width - 220f, 22f), readiness.Text);
            GUI.color = previousColor;
            TooltipHandler.TipRegion(new Rect(x, y, width, 22f),
                "Readiness: " + readiness.TooltipText + "\n\nShows whether the rule is configured and gear is available. Individual pawns may still be unable to reach or use an item." +
                (rule.IsNonWork && rule.DefaultToSavedPersonalOutfit
                    ? " Saved personal outfits are checked for each pawn; stock counts describe fallback choices."
                    : ""));
            Rect recallRect = new Rect(rect.xMax - 120f, y - 1f, 110f, 24f);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = rule.Area != null;
            if (Widgets.ButtonText(recallRect, rule.WorkAreaPaused ? "Resume work" : "Pause work"))
                PauseOrResumeWork(rule, component);
            GUI.enabled = previousEnabled;
            TooltipHandler.TipRegion(recallRect,
                rule.WorkAreaPaused
                    ? "Resume work here. Pawns meet this rule's outfit requirements before returning, even if earlier recalls are still finishing."
                    : "Pause work here and recall pawns using this rule. They return borrowed outfits and restore personal gear. Access and outfit requirements stay active; direct player orders keep their normal behavior.");

            y += 28f;
            string participantsTip = (rule.IsNonWork ? "Occupants lists" : "Workers lists") +
                " humanlike pawns working, eating, resting or changing outfits for this area. Buffered pawns may remain listed after leaving. Active tasks appear first, followed by outfit changes, buffered tasks and other activity. Check Haulers and Wanderers for those groups. Hover for details; click to select a pawn.";
            Widgets.Label(new Rect(x, y, 100f, 22f), rule.IsNonWork ? "Occupants:" : "Workers:");
            TooltipHandler.TipRegion(new Rect(x, y, 100f, 22f), participantsTip);
            if (workerCount == 0)
            {
                Widgets.Label(new Rect(x + 100f, y, width - 100f, 22f),
                    rule.IsNonWork ? "No active or returning occupants" : "No active or returning workers");
                TooltipHandler.TipRegion(new Rect(x + 100f, y, width - 100f, 22f),
                    participantsTip + "\n\nNo matching pawns are currently listed. Check Haulers and Wanderers below.");
            }
            else
            {
                for (int workerIndex = 0; workerIndex < activity.Participants.Count; workerIndex++)
                {
                    CachedActivityEntry entry = activity.Participants[workerIndex];
                    Pawn worker = entry.Pawn;
                    if (worker == null || worker.Destroyed) continue;
                    State.PawnApparelState state = entry.ManagedState;
                    if (state != null)
                    {
                        string fullStatus = PawnAutomaticOutfitStatus.BuildForRule(state.Pawn, rule) ?? "Automatic Outfit Manager: Active";
                        int detailStart = fullStatus.IndexOf('\n');
                        string headline = detailStart >= 0
                            ? fullStatus.Substring(0, detailStart)
                            : fullStatus;
                        string shortStatus = headline.Replace("Automatic Outfit Manager: ", "");
                        string hoverDetails = detailStart >= 0
                            ? fullStatus.Substring(detailStart + 1)
                            : null;
                        float managedY = y + workerIndex * 22f;
                        Rect managedRect = new Rect(x + 100f, managedY, width - 170f, 22f);
                        Widgets.DrawHighlightIfMouseover(managedRect);
                        Widgets.Label(managedRect, $"{state.Pawn.LabelShortCap} — {shortStatus}".Truncate(managedRect.width));
                        if (Widgets.ButtonInvisible(managedRect))
                            CameraJumper.TryJumpAndSelect(state.Pawn);
                        string jumpHint = $"Click to select and jump to {state.Pawn.LabelShortCap}.";
                        TooltipHandler.TipRegion(managedRect,
                            string.IsNullOrEmpty(hoverDetails)
                                ? jumpHint
                                : $"{hoverDetails}\n\n{jumpHint}");

                        Rect returnWorkerRect = new Rect(rect.xMax - 70f, managedY, 60f, 22f);
                        if (Widgets.ButtonText(returnWorkerRect, "Recall"))
                            ReturnWorker(state);
                        bool ownsManagedGear = state.ApparelInterventionActive ||
                            state.WeaponInterventionActive;
                        TooltipHandler.TipRegion(returnWorkerRect,
                            ownsManagedGear
                                ? $"Recall {state.Pawn.LabelShortCap} to return borrowed outfits and restore saved personal gear. This rule stays active for other pawns."
                                : $"End {state.Pawn.LabelShortCap}'s current task and return to the locker if assigned. Their personal outfit stays on. They can choose new work; this rule stays active.");
                        continue;
                    }

                    float workerY = y + workerIndex * 22f;
                    Rect workerRect = new Rect(
                        x + 100f, workerY, width - 170f, 22f);
                    Widgets.DrawHighlightIfMouseover(workerRect);
                    Color workerColor = GUI.color;
                    if (entry.MissingRequiredGear)
                        GUI.color = Color.red;
                    var retained = Patches.NonWorkBufferTracker.For(worker);
                    string participantStatus = entry.ObservedTransitionStatus ??
                        (retained?.RuleId == rule.Id
                            ? PawnAutomaticOutfitStatus.BuildForRule(worker, rule) : null);
                    int participantDetailStart = participantStatus?.IndexOf('\n') ?? -1;
                    string participantHeadline = participantStatus == null ? entry.Report
                        : (participantDetailStart >= 0 ? participantStatus.Substring(0, participantDetailStart)
                            : participantStatus).Replace("Automatic Outfit Manager: ", "");
                    Widgets.Label(workerRect,
                        $"{worker.LabelShortCap} — {participantHeadline}".Truncate(workerRect.width));
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
                    else if (!entry.ObservedOnly && Widgets.ButtonText(fallbackRecallRect, "Recall"))
                    {
                        State.PawnApparelState tracked =
                            component.TrackCompliantWorkSession(
                                worker,
                                worker.CurJob,
                                RuleEvaluator.MatchingRules(worker, worker.CurJob));
                        activityCache.Remove(rule.Id);
                        ReturnWorker(tracked);
                    }

                    string status = participantStatus != null
                        ? (participantDetailStart >= 0 ? participantStatus.Substring(participantDetailStart + 1) : participantStatus)
                        : entry.StagedRepairTip != null ? entry.StagedRepairTip
                        : entry.ObservedOnly
                        ? $"Rule: {RuleTypeStyle.RuleName(rule)}\nCurrent: {entry.Report}"
                        : entry.MissingRequiredGear
                        ? "This pawn needs the required outfit, but no outfit change has started. Check gear availability and access to the locker. If the pawn remains stuck, enable Detailed logging in mod settings and report the issue."
                        : $"End {worker.LabelShortCap}'s current task and return to the locker if assigned. Their personal outfit stays on, and they can choose new work.";
                    TooltipHandler.TipRegion(workerRect,
                        $"{status}\n\nClick to select and jump to {worker.LabelShortCap}.");
                    if (!entry.ObservedOnly) TooltipHandler.TipRegion(fallbackRecallRect, status);
                }
            }

            y += Mathf.Max(1, workerCount) * 22f + 4f;
            DrawActivityRow("Haulers:", activity.Haulers, x, y, width,
                "Pawns, animals and robots currently hauling in or through this area. Hauling permissions control access.");
            y += Mathf.Max(1, activity.Haulers.Count) * 22f + 4f;
            DrawActivityRow("Wanderers:", activity.Wanderers, x, y, width,
                "Idle movement, robot cleaning and other animal or robot activity appear here. Their current activity is shown. Meals and rest still use Activities permissions; wandering and robot cleaning use Wandering.");
        }

        private static void DrawActivityRow(
            string label, List<CachedActivityEntry> actors, float x, float y, float width, string tooltip)
        {
            Widgets.Label(new Rect(x, y, 100f, 22f), label);
            TooltipHandler.TipRegion(new Rect(x, y, 100f, 22f), tooltip);
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
                    $"Current: {entry.Report}\n\nClick to select and jump to {actor.LabelShortCap}.");
            }
        }

        private static bool TracksRule(State.PawnApparelState state, string ruleId)
        {
            if (state == null || string.IsNullOrEmpty(ruleId))
                return false;

            return state.ActiveRuleId == ruleId ||
                   state.NonWorkRestorationRuleId == ruleId ||
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
                   Patches.PausedAreaWorkFilter.IsCurrentWanderingActivityForRule(state.Pawn, job, rule);
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

        private static void DrawRuleTypeHeader(ApparelRule rule, Rect rect)
        {
            Color accent = RuleTypeStyle.ForRule(rule);
            string typeLabel = rule.IsNonWork ? "Non-Work Area" : "Work Area";
            string ruleName = string.IsNullOrWhiteSpace(rule.Name) ? "Unnamed rule" : rule.Name;
            string badgeLabel = ruleName + " - " + typeLabel;
            float badgeWidth = Mathf.Min(rect.width * 0.55f,
                Mathf.Max(150f, Text.CalcSize(badgeLabel).x + 24f));
            Rect badgeRect = new Rect(rect.x, rect.y, badgeWidth, rect.height);
            string visibleName = ruleName.Truncate(Mathf.Max(0f,
                badgeWidth - 24f - Text.CalcSize(" - " + typeLabel).x));
            MarkConfiguredAreaForDraw(rule.Area, badgeRect);
            if (Widgets.ButtonInvisible(badgeRect))
                CenterConfiguredArea(rule.Area);
            Widgets.DrawBoxSolid(badgeRect,
                new Color(accent.r, accent.g, accent.b, 0.16f));
            Color previousColor = GUI.color;
            TextAnchor previousAnchor = Text.Anchor;
            bool previousWordWrap = Text.WordWrap;
            Text.WordWrap = false;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = accent;
            Widgets.Label(badgeRect, visibleName + " - " + typeLabel);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = previousColor;
            Rect descriptionRect = new Rect(badgeRect.xMax + 12f, rect.y,
                Mathf.Max(0f, rect.xMax - badgeRect.xMax - 12f), rect.height);
            string description = RuleDescriptionWindow.Description(rule);
            Widgets.DrawHighlightIfMouseover(descriptionRect);
            Widgets.Label(descriptionRect, description.Truncate(descriptionRect.width));
            if (Widgets.ButtonInvisible(descriptionRect))
                Find.WindowStack.Add(new RuleDescriptionWindow(rule));
            TooltipHandler.TipRegion(descriptionRect, description + "\n\nClick to edit this description. " +
                "Descriptions are notes; the rule settings control behavior.\nDefault: " +
                RuleDescriptionWindow.DefaultDescription(rule));
            GUI.color = previousColor;
            Text.Anchor = previousAnchor;
            Text.WordWrap = previousWordWrap;
            TooltipHandler.TipRegion(badgeRect, RuleTypeStyle.LocationTip(rule) + "\n\n" + (rule.IsNonWork
                ? "Return the chosen Work outfits and use a personal or selected outfit here. Hover to highlight the area; click to center the map on it."
                : "Wear the required outfit before entry. Hover to highlight the area; click to center the map on it."));
        }

        private static void ShowSavedNonWorkOutfits(
            AutomaticOutfitManagerGameComponent component)
        {
            var options = new List<FloatMenuOption>();
            foreach (Pawn pawn in Find.CurrentMap?.mapPawns.AllPawnsSpawned
                         .Where(PawnAccessClassifier.IsApparelEligibleHuman)
                         .OrderBy(pawn => pawn.LabelShortCap) ?? Enumerable.Empty<Pawn>())
            {
                Pawn selected = pawn;
                bool hasSnapshot = component.NonWorkOutfitFor(selected) != null;
                options.Add(new FloatMenuOption(
                    selected.LabelShortCap + (hasSnapshot
                        ? " — View saved outfit" : " — No saved outfit"),
                    () => ShowSavedNonWorkOutfit(component, selected))
                {
                    tooltip = new TipSignal(hasSnapshot
                        ? "View the personal apparel and primary weapon saved for this pawn."
                        : "No personal outfit has been saved for this pawn. Viewing does not save their current clothes.")
                });
            }
            if (options.Count == 0)
                options.Add(new FloatMenuOption("No eligible pawns on this map", null)
                { tooltip = new TipSignal("No spawned humanlike pawns eligible for automatic outfits are available on the current map.") });
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void ShowSavedNonWorkOutfit(
            AutomaticOutfitManagerGameComponent component, Pawn pawn)
        {
            State.SavedNonWorkOutfit saved = component.NonWorkOutfitFor(pawn);
            if (saved == null)
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    $"No saved non-work outfit for {pawn.LabelShortCap}.\n\n" +
                    "AOM records a pawn's personal outfit automatically before issuing work gear. " +
                    "Until an outfit is saved, Non-Work Area rules use their fallback selections."));
                return;
            }

            List<string> apparelItems = saved.Apparel.Where(item => item != null)
                .Select(item => item.LabelCap.ToString() + (item.Destroyed ? " (destroyed)" : ""))
                .ToList();
            string apparel = apparelItems.Count == 0 ? "None" : string.Join(", ", apparelItems);
            string weapon = saved.Weapon == null ? "None (unarmed)"
                : saved.Weapon.LabelCap.ToString() + (saved.Weapon.Destroyed ? " (destroyed)" : "");
            Find.WindowStack.Add(new Dialog_MessageBox(
                $"Saved non-work outfit for {pawn.LabelShortCap}.\n\n" +
                $"Apparel: {apparel}\nPrimary weapon: {weapon}\n\n" +
                "Saved automatically before Work gear was issued. Items matching enabled Work Area requirements are excluded. " +
                "Non-Work rules prefer this outfit when Default to Saved Personal Outfit is checked."));
        }

        private float RuleHeight(
            ApparelRule rule,
            AutomaticOutfitManagerGameComponent component)
        {
            if (rule?.UiCollapsed == true)
                return 70f + RuleTypeHeaderHeight;

            CachedRuleActivity activity = RuleActivity(rule);
            int workerCount = activity.Participants.Count;
            int haulerCount = activity.Haulers.Count;
            int wandererCount = activity.Wanderers.Count;
            float activityHeight = 8f + Mathf.Max(1, haulerCount) * 22f +
                                   Mathf.Max(1, wandererCount) * 22f;
            return Mathf.Max(590f, 568f + Mathf.Max(1, workerCount) * 22f +
                activityHeight) + (rule.IsNonWork ? 68f : 0f) + RuleTypeHeaderHeight;
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
                // One cached projection owns drawing and height. Do not mutate
                // only one bucket between those calls; refresh all together.
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
                    if (pawn == null)
                        continue;

                    int animalRow = PawnActivityPresentation.AnimalRow(pawn, job);
                    if (animalRow != 0)
                    {
                        if (ObservesActivityInArea(pawn, job, rule))
                        {
                            var animalEntry = new CachedActivityEntry {
                                Pawn = pawn,
                                Report = (job?.GetReport(pawn) ?? job?.def?.label ?? "Idle").CapitalizeFirst()
                            };
                            (animalRow == 1 ? cached.Haulers : cached.Wanderers).Add(animalEntry);
                        }
                        continue;
                    }

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
                        if (pawn.RaceProps?.Humanlike == true && pawn.apparel != null)
                            cached.Participants.Add(new CachedActivityEntry { Pawn = pawn, ManagedState = trackedState });
                        // Preparation, return, and restoration have their own
                        // Worker headline. A preserved or transitional haul job
                        // must not duplicate the pawn in Haulers at the same time.
                        continue;
                    }

                    string observedTransition = ObservedOutfitTransition.Build(pawn, trackedState, job, rule);
                    if (observedTransition != null)
                    {
                        cached.Participants.Add(new CachedActivityEntry
                        {
                            Pawn = pawn,
                            ObservedOnly = true,
                            ObservedTransitionStatus = observedTransition
                        });
                        continue;
                    }

                    Job activityJob = ActivityJobFor(trackedState) ?? job;
                    var repairSources = RepairMaterialStage.StatusSources(pawn, activityJob,
                        trackedState, rule, AutomaticOutfitManagerGameComponent.Current?.Rules);
                    if (repairSources.Count > 0)
                    {
                        bool preparing = trackedState.Transition == State.ApparelTransition.Preparing;
                        cached.Participants.Add(new CachedActivityEntry
                        {
                            Pawn = pawn,
                            Report = preparing ? "Preparing to collect repair material" : "Collecting repair material",
                            ObservedOnly = true,
                            StagedRepairTip = $"Rule: {RuleTypeStyle.RuleName(rule)}\n" +
                                $"Material source: {string.Join(", ", repairSources.Select(RuleTypeStyle.RuleName))}\n" +
                                "Collect the component first, then prepare this rule's outfit before entering to repair."
                        });
                        continue;
                    }
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
                    if (TracksRule(trackedState, rule.Id) && pawn.RaceProps?.Humanlike == true && pawn.apparel != null)
                    {
                        cached.Participants.Add(new CachedActivityEntry { Pawn = pawn, ManagedState = trackedState });
                        continue;
                    }

                    bool managedWorkContext = job != null &&
                        !Patches.PausedAreaWorkFilter.IsHaulingJob(job) &&
                        (job.workGiverDef != null ||
                         job.jobGiver is JobGiver_Work ||
                         job.playerForced);
                    bool qualifyingWork = managedWorkContext && RuleEvaluator.MatchesRule(pawn, job, rule);
                    var retainedBuffer = Patches.NonWorkBufferTracker.For(pawn);
                    bool buffersThisRule = retainedBuffer?.RuleId == rule.Id;
                    if (qualifyingWork || buffersThisRule || ObservesActivityInArea(pawn, job, rule))
                    {
                        string workerReport = job?.GetReport(pawn);
                        if (string.IsNullOrEmpty(workerReport))
                            workerReport = pawn.Downed ? "Downed" : job?.def?.label ?? "Idle";
                        if (pawn.Drafted) workerReport = "Drafted — " + workerReport;
                        cached.Participants.Add(new CachedActivityEntry
                        {
                            Pawn = pawn,
                            Report = workerReport.CapitalizeFirst(),
                            ObservedOnly = buffersThisRule || !qualifyingWork,
                            MissingRequiredGear = !buffersThisRule && qualifyingWork &&
                                RuleEvaluator.HasMissingRequiredGear(pawn, rule)
                        });
                        continue;
                    }

                }
            }

            // A tracked return can be on another map or temporarily despawned
            // during portal travel. Preserve those sessions from the component
            // even though they are absent from this area's spawned-pawn scan.
            var listed = new HashSet<Pawn>(cached.Participants.Select(entry => entry.Pawn));
            var component = AutomaticOutfitManagerGameComponent.Current;
            if (component != null)
            {
                foreach (var state in component.PawnStates)
                {
                    Pawn pawn = state?.Pawn;
                    if (pawn?.RaceProps?.Humanlike != true || pawn.RaceProps.Animal ||
                        pawn.Destroyed || pawn.apparel == null || !TracksRule(state, rule.Id) ||
                        DisplaysAsAccessActivity(state, rule) || !listed.Add(pawn)) continue;
                    cached.Participants.Add(new CachedActivityEntry { Pawn = pawn, ManagedState = state });
                }
            }

            foreach (var entry in cached.Participants)
            {
                var state = entry.ManagedState;
                var progress = RuleBufferProgress.For(rule.Id, rule.ReturnTaskBuffer,
                    state, Patches.NonWorkBufferTracker.For(entry.Pawn));
                bool buffered = progress.Started && !progress.Ended &&
                    (progress.PendingJobId == entry.Pawn.CurJob?.loadID ||
                     Patches.NonWorkBufferTracker.For(entry.Pawn)?.RuleId == rule.Id &&
                     !ObservesActivityInArea(entry.Pawn, entry.Pawn.CurJob, rule));
                entry.DisplayPriority = PawnActivityPresentation.Priority(entry.Pawn.CurJob,
                    entry.ObservedTransitionStatus != null ||
                    state != null && state.Transition != State.ApparelTransition.Active, buffered);
            }
            cached.Participants.Sort((a, b) => {
                int priority = a.DisplayPriority.CompareTo(b.DisplayPriority);
                return priority != 0 ? priority : CompareActivityNames(a, b);
            });
            cached.Haulers.Sort(CompareActivityNames);
            cached.Wanderers.Sort(CompareActivityNames);
            activityCache[key] = cached;
            return cached;
        }

        private static int CompareActivityNames(CachedActivityEntry a, CachedActivityEntry b)
        {
            int name = System.StringComparer.CurrentCultureIgnoreCase.Compare(a.Pawn.LabelShortCap, b.Pawn.LabelShortCap);
            return name != 0 ? name : a.Pawn.thingIDNumber.CompareTo(b.Pawn.thingIDNumber);
        }

        private static bool ObservesActivityInArea(Pawn pawn, Job job, ApparelRule rule)
        {
            Area area = rule?.Area;
            if (pawn?.Map == null || area?.Map != pawn.Map || pawn.Destroyed ||
                !((Faction.OfPlayerSilentFail != null && pawn.Faction == Faction.OfPlayerSilentFail) ||
                  PawnAccessClassifier.IsHostedGuest(pawn) || PawnAccessClassifier.IsColonyPrisoner(pawn)))
                return false;
            if (pawn.Position.IsValid && pawn.Position.InBounds(pawn.Map) && area[pawn.Position]) return true;
            if (job != null && RuleEvaluator.JobTargetsArea(job, area)) return true;
            // Observe the existing route only; UI refreshes must not request paths
            // or create gameplay sessions just to display personal activity.
            var path = pawn.pather?.curPath;
            return pawn.pather?.Moving == true && path?.Found == true &&
                path.NodesReversed.Any(cell => cell.IsValid && cell.InBounds(pawn.Map) && area[cell]);
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

            ConfiguredAreaOnCurrentMap(configuredArea)?.MarkForDraw();
        }

        private static void CenterConfiguredArea(Area configuredArea)
        {
            Area area = ConfiguredAreaOnCurrentMap(configuredArea);
            if (area == null)
                return;

            // Use the center of the full painted extent, including disjoint
            // sections. Only scan on click; empty areas have no camera target.
            int minX = int.MaxValue, minZ = int.MaxValue;
            int maxX = int.MinValue, maxZ = int.MinValue;
            foreach (IntVec3 cell in area.ActiveCells)
            {
                if (!cell.IsValid || !cell.InBounds(area.Map))
                    continue;
                minX = Mathf.Min(minX, cell.x);
                minZ = Mathf.Min(minZ, cell.z);
                maxX = Mathf.Max(maxX, cell.x);
                maxZ = Mathf.Max(maxZ, cell.z);
            }
            if (minX == int.MaxValue)
                return;
            CameraJumper.TryJump(new IntVec3((minX + maxX) / 2, 0, (minZ + maxZ) / 2), area.Map);
            area.MarkForDraw();
        }

        private static Area ConfiguredAreaOnCurrentMap(Area configuredArea)
        {
            Map currentMap = Find.CurrentMap;
            if (configuredArea == null || currentMap == null)
                return null;

            // Gravship placement creates new Area objects. The placement/load
            // remapper normally updates the rule immediately, but UI hover must
            // remain truthful during the small window where an old source-map
            // reference can still be present. Resolve only an exact signature
            // match on the map the player is currently viewing; ordinary maps
            // with merely similar names remain untouched.
            return configuredArea.Map == currentMap
                ? configuredArea
                : Patches.GravshipAreaRemapper.FindSignatureMatch(
                    currentMap, configuredArea);
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
                $"Allowed apparel: {ApparelStandardsSummary(rule)}\n" +
                $"Weapons: {weaponAvailability}\n" +
                $"Allowed weapons: {WeaponStandardsSummary(rule)}";
            string tooltipText = null;
            var removalConflict = NonWorkFallbackPolicy.FirstConflict(rule, component.Rules);
            var affectedNonWork = NonWorkFallbackPolicy.ConflictingDestination(rule, null, component.Rules);
            var nestingConflict = AreaNestingPolicy.Conflict(rule, component.Rules);
            if (nestingConflict != null)
            {
                text = rule.Enabled ? "Conflict — Work Area inside Non-Work Area" :
                    "Disabled — Work Area inside Non-Work Area";
                tooltipText = RuleTypeStyle.AreaNestingTip(rule, nestingConflict);
                color = RuleConflictStyle.Color;
            }
            else if (!rule.Enabled)
            {
                text = "Disabled";
                color = Color.yellow;
            }
            else if (removalConflict != null)
            {
                text = $"Outfit conflict — '{removalConflict.Name}' selected for removal";
                tooltipText = $"Outfit conflict — '{RuleTypeStyle.RuleName(removalConflict)}' selected under Remove Work Outfits";
                color = RuleConflictStyle.Color;
                availabilitySummary = "Choose different gear or change the 'Remove Work Outfits' selection. " +
                    (rule.DefaultToSavedPersonalOutfit
                        ? "Pawns using saved personal outfits are unaffected; fallback entry is blocked until resolved.\n"
                        : "Entry using these requirements is blocked until resolved.\n") + availabilitySummary;
            }
            else if (affectedNonWork != null)
            {
                text = $"Conflicts with '{affectedNonWork.Name}'";
                tooltipText = RuleTypeStyle.ReverseConflictTip(rule, affectedNonWork);
                color = RuleConflictStyle.Color;
                availabilitySummary = "The Non-Work rule cannot use these selections until the conflict is resolved. Pawns using saved personal outfits bypass fallback selections.\n" + availabilitySummary;
            }
            else if (rule.WorkAreaPaused)
            {
                text = "Work paused";
                color = Color.yellow;
            }
            else if (rule.Area == null)
            {
                text = "Missing area";
                color = Color.yellow;
            }
            else if ((!rule.IsNonWork || !rule.DefaultToSavedPersonalOutfit) &&
                     (rule.RequiredApparel == null || rule.RequiredApparel.Count == 0) &&
                     !rule.HasWeaponRequirement)
            {
                text = "No outfit requirements selected";
                color = Color.yellow;
            }
            else if ((!rule.IsNonWork || !rule.DefaultToSavedPersonalOutfit) && apparelConflict != null)
            {
                text = $"Blocked — incompatible apparel: {apparelConflict.Label}";
                tooltipText = $"Blocked — incompatible apparel: {apparelConflict.First.LabelCap} ({RuleTypeStyle.RuleName(apparelConflict.FirstRule)}) conflicts with {apparelConflict.Second.LabelCap} ({RuleTypeStyle.RuleName(apparelConflict.SecondRule)})";
                color = RuleConflictStyle.Color;
            }
            else if ((!rule.IsNonWork || !rule.DefaultToSavedPersonalOutfit) && !compatibleWeaponRequirements)
            {
                text = "Blocked — incompatible overlapping primary-weapon requirements or standards";
                color = RuleConflictStyle.Color;
            }
            else if (workAreaCoveredByPausedOverlaps)
            {
                string names = string.Join(", ", pausedOverlaps.Select(overlap => overlap.Name));
                text = $"Blocked — work area covered by paused: {names}";
                tooltipText = "Blocked — work area covered by paused: " +
                    string.Join(", ", pausedOverlaps.Select(RuleTypeStyle.RuleName));
                color = Color.yellow;
            }
            else if (rule.ChangingArea != null && map != null &&
                     rule.ChangingArea.Map == map &&
                     !rule.ChangingArea.ActiveCells.Any(cell => cell.GetSlotGroup(map) != null))
            {
                text = "Locker room has no storage";
                color = Color.yellow;
            }
            else if (rule.IsNonWork && rule.DefaultToSavedPersonalOutfit)
            {
                text = "Saved outfit; fallback if none saved";
                color = Color.green;
                availabilitySummary = "Saved outfit checked for each pawn. Counts below describe fallback stock only.\n" + availabilitySummary;
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
                        tooltipText = "Active — shared cells paused: " +
                            string.Join(", ", pausedOverlaps.Select(RuleTypeStyle.RuleName));
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
                TooltipText = tooltipText ?? text,
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

        private static FloatMenuOption AreaMenuOption(Area area, System.Action selectArea) =>
            new FloatMenuOption(area.Label.Colorize(RuleTypeStyle.ForArea(area)), selectArea,
                priority: MenuOptionPriority.DisabledOption,
                mouseoverGuiAction: rect => MarkConfiguredAreaForDraw(area, rect))
            {
                tooltip = new TipSignal(RuleTypeStyle.AreaName(area) +
                    "\nHover to highlight this area. Click to select it. The painted area stays as it is.")
            };

        private static void AddGroupedAreaOptions(List<FloatMenuOption> options, Map map,
            System.Action<Area> selectArea)
        {
            var areas = map.areaManager.AllAreas.Where(area => area != null).ToList();
            // Ordinary editable areas use Area_Allowed. Special-purpose native
            // and mod area classes stay in their own group, independent of names.
            foreach (bool custom in new[] { true, false })
            {
                var group = areas.Where(area => (area.GetType() == typeof(Area_Allowed)) == custom)
                    .OrderBy(area => area.Label, System.StringComparer.CurrentCultureIgnoreCase).ToList();
                if (group.Count == 0)
                    continue;
                options.Add(new FloatMenuOption(custom ? "— Custom areas —" : "— Native / mod areas —", null)
                {
                    tooltip = new TipSignal(custom
                        ? "Editable allowed areas, sorted by name."
                        : "Special-purpose native and mod area types, sorted by name. Their painted cells may be controlled by the game or another mod.")
                });
                foreach (Area area in group)
                {
                    Area captured = area;
                    options.Add(AreaMenuOption(captured, () => selectArea(captured)));
                }
            }
            // FloatMenu sorts by priority. Keep selectable entries at the same
            // priority as disabled headings so the grouped order stays intact.
            // An entry is disabled by its null action, not its priority value.
        }

        private static void ShowAreaMenu(ApparelRule rule)
        {
            Map map = Find.CurrentMap;
            if (map == null)
                return;

            var options = new List<FloatMenuOption>();
            AddGroupedAreaOptions(options, map, area =>
            {
                Area previous = rule.Area;
                rule.Area = area;
                string conflict = rule.Enabled ? RuleTypeStyle.ConfigurationConflictTip(rule) : null;
                if (conflict != null)
                {
                    rule.Area = previous;
                    Messages.Message(conflict, MessageTypeDefOf.RejectInput, false);
                    return;
                }
                AutomaticOutfitManagerGameComponent.Current?.NotifyRuleRequirementsChanged(rule.Id, "area assignment changed");
                RuleEvaluator.ResetRuntimeCache();
            });
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
                }, priority: MenuOptionPriority.DisabledOption)
                {
                    tooltip = new TipSignal("Use reachable map stock and change back at a safe cell outside the area. Borrowed gear from other rules still follows its own locker assignment.")
                }
            };
            AddGroupedAreaOptions(options, map, area =>
            {
                rule.ChangingArea = area;
                RuleEvaluator.ResetRuntimeCache();
            });
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
