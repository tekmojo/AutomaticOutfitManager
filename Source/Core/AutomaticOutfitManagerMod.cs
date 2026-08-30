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
                "Only genuine warnings and errors are written to the player log.");
            DrawLoggingOption(
                listing,
                AomLoggingLevel.Basic,
                "Basic (recommended)",
                "Logs the loaded version and rare repair or compatibility summaries.");
            DrawLoggingOption(
                listing,
                AomLoggingLevel.Detailed,
                "Detailed",
                "Adds rate-limited pawn transitions and troubleshooting decisions. " +
                "Enable this temporarily while reproducing an issue.");

            listing.Gap();
            GameFont previousFont = Text.Font;
            Text.Font = GameFont.Tiny;
            listing.Label(
                "Changes take effect immediately. Detailed AOM logging is " +
                "independent of RimWorld Developer Mode.");
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
