using AutomaticOutfitManager.Rules;
using UnityEngine;
using Verse;

namespace AutomaticOutfitManager.UI
{
    internal sealed class RuleDescriptionWindow : Window
    {
        private const int MaxLength = 160;
        private readonly ApparelRule rule;
        private string draft;

        internal RuleDescriptionWindow(ApparelRule rule)
        {
            this.rule = rule;
            draft = Description(rule);
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
        }

        public override Vector2 InitialSize => new Vector2(580f, 270f);

        internal static string Description(ApparelRule rule) =>
            string.IsNullOrWhiteSpace(rule.CustomDescription) ? DefaultDescription(rule) : rule.CustomDescription;

        internal static string DefaultDescription(ApparelRule rule)
        {
            if (!rule.IsNonWork)
                return rule.RequiredApparel.Count > 0 || rule.HasWeaponRequirement
                    ? "Wear the required outfit before entry."
                    : "Control area access without requiring specific gear.";
            string removal = rule.RemoveAllWorkOutfits ? "Return work outfits" :
                rule.WorkOutfitsToRemove.Count > 0 ? "Return selected work outfits" : "Keep work outfits";
            if (rule.DefaultToSavedPersonalOutfit)
                return removal + ", then prefer the saved outfit.";
            return removal + (rule.RequiredApparel.Count > 0 || rule.HasWeaponRequirement
                ? ", then use the selected outfit."
                : ", with no replacement outfit required.");
        }

        public override void DoWindowContents(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, 34f), "Edit Rule Description");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 42f, rect.width, 42f),
                "Add a short note beside the badge. Outfit and access settings still control the rule.");
            Rect inputRect = new Rect(0f, 92f, rect.width, 30f);
            draft = Widgets.TextField(inputRect, draft ?? "").Replace("\r", " ").Replace("\n", " ");
            if (draft.Length > MaxLength) draft = draft.Substring(0, MaxLength);
            TooltipHandler.TipRegion(inputRect,
                "Up to 160 characters. Leave blank to use the automatic description. A custom description stays as written when rule settings change.");
            string defaultText = "Default: " + DefaultDescription(rule);
            Rect defaultRect = new Rect(0f, 130f, rect.width, 24f);
            Widgets.Label(defaultRect, defaultText.Truncate(rect.width));
            TooltipHandler.TipRegion(defaultRect, defaultText);
            Rect resetRect = new Rect(0f, rect.height - 36f, 130f, 32f);
            Rect cancelRect = new Rect(rect.width - 216f, rect.height - 36f, 100f, 32f);
            Rect saveRect = new Rect(rect.width - 106f, rect.height - 36f, 106f, 32f);
            if (Widgets.ButtonText(resetRect, "Use default"))
            {
                rule.CustomDescription = null;
                Close();
            }
            TooltipHandler.TipRegion(resetRect,
                "Use a description that follows this rule's settings, then close.");
            if (Widgets.ButtonText(cancelRect, "Cancel")) Close();
            TooltipHandler.TipRegion(cancelRect, "Close without saving your edits.");
            if (Widgets.ButtonText(saveRect, "Save"))
            {
                string value = draft.Trim();
                rule.CustomDescription = value.Length == 0 || value == DefaultDescription(rule) ? null : value;
                Close();
            }
            TooltipHandler.TipRegion(saveRect, "Save this description with the rule. Blank text restores the automatic description.");
        }
    }
}
