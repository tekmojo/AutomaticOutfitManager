using HarmonyLib;
using UnityEngine;
using Verse;

namespace AutomaticOutfitManager.Core
{
    public sealed class AutomaticOutfitManagerMod : Mod
    {
        public const string HarmonyId = "tekmojo.automaticoutfitmanager";
        public static AutomaticOutfitManagerSettings Settings { get; private set; }

        public AutomaticOutfitManagerMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<AutomaticOutfitManagerSettings>();
            new Harmony(HarmonyId).PatchAll();
            string version = typeof(AutomaticOutfitManagerMod)
                .Assembly.GetName().Version?.ToString(3) ?? "unknown";
            AomLog.Basic($"{version} loaded (logging: {Settings.LoggingLevel}).");
        }

        public override string SettingsCategory() => "Automatic Outfit Manager";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label("Logging");
            listing.GapLine();

            DrawLoggingOption(
                listing,
                AomLoggingLevel.Quiet,
                "Quiet",
                "Warnings and errors only.");
            DrawLoggingOption(
                listing,
                AomLoggingLevel.Basic,
                "Basic (recommended)",
                "Version information and occasional recovery messages, plus warnings and errors.");
            DrawLoggingOption(
                listing,
                AomLoggingLevel.Detailed,
                "Detailed",
                "Records outfit changes, jobs, gear and recovery decisions in the player log. " +
                "Use temporarily when reporting a problem.");

            listing.Gap();
            GameFont previousFont = Text.Font;
            Text.Font = GameFont.Tiny;
            listing.Label(
                "Changes apply immediately. Detailed logging works without RimWorld Developer Mode.");
            Text.Font = previousFont;
            listing.End();
        }

        private static void DrawLoggingOption(
            Listing_Standard listing,
            AomLoggingLevel level,
            string label,
            string description)
        {
            Rect row = listing.GetRect(30f);
            if (Widgets.RadioButtonLabeled(row, label, Settings.LoggingLevel == level))
                SetLoggingLevel(level);
            TooltipHandler.TipRegion(row, description);

            GameFont previousFont = Text.Font;
            Color previousColor = GUI.color;
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            listing.Label(description);
            GUI.color = previousColor;
            Text.Font = previousFont;
            listing.Gap(4f);
        }

        private static void SetLoggingLevel(AomLoggingLevel level)
        {
            if (Settings == null || Settings.LoggingLevel == level)
                return;

            Settings.LoggingLevel = level;
            AomLog.ResetRuntimeCache();
            Settings.Write();
        }
    }
}
