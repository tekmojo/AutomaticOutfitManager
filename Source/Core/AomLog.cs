using System;
using System.Collections.Generic;
using AutomaticOutfitManager.Detection;
using Verse;

namespace AutomaticOutfitManager.Core
{
    public static class AomLog
    {
        private const string Prefix = "[AutomaticOutfitManager] ";
        private const int DefaultDetailedInterval = 6000;
        private const int GuestDetailedInterval = 60000;

        private static readonly Dictionary<int, Dictionary<string, int>>
            LastDetailedTickByPawn =
                new Dictionary<int, Dictionary<string, int>>();
        private static readonly Dictionary<int, HashSet<string>>
            LoggedWarningCategoriesByPawn =
                new Dictionary<int, HashSet<string>>();

        public static AomLoggingLevel Level =>
            AutomaticOutfitManagerMod.Settings?.LoggingLevel ??
            AomLoggingLevel.Basic;

        public static bool BasicEnabled => Level >= AomLoggingLevel.Basic;

        public static bool DetailedEnabled => Level >= AomLoggingLevel.Detailed;

        public static void Basic(string message)
        {
            if (BasicEnabled)
                Log.Message(WithPrefix(message));
        }

        public static void Detailed(string message)
        {
            if (DetailedEnabled)
                Log.Message(WithPrefix(message));
        }

        public static void Warning(string message) =>
            Log.Warning(WithPrefix(message));

        public static void Error(string message) =>
            Log.Error(WithPrefix(message));

        public static void WarningOnce(
            Pawn pawn,
            string category,
            Func<string> messageFactory)
        {
            if (pawn == null || string.IsNullOrEmpty(category) ||
                messageFactory == null)
            {
                return;
            }

            int pawnId = pawn.thingIDNumber;
            if (!LoggedWarningCategoriesByPawn.TryGetValue(
                    pawnId,
                    out HashSet<string> categories))
            {
                categories = new HashSet<string>();
                LoggedWarningCategoriesByPawn[pawnId] = categories;
            }

            if (categories.Add(category))
                Warning(messageFactory());
        }

        public static bool ShouldLogDetailed(
            Pawn pawn,
            string category,
            int interval = DefaultDetailedInterval)
        {
            if (!DetailedEnabled || pawn == null || string.IsNullOrEmpty(category))
                return false;

            // Large visiting or custody groups can retry inaccessible jobs for
            // many hours. Keep one useful entry per pawn per in-game day.
            if (PawnAccessClassifier.IsHostedGuest(pawn) ||
                PawnAccessClassifier.IsColonyPrisoner(pawn))
            {
                interval = Math.Max(interval, GuestDetailedInterval);
            }

            int tick = Find.TickManager?.TicksGame ?? 0;
            int pawnId = pawn.thingIDNumber;
            if (!LastDetailedTickByPawn.TryGetValue(
                    pawnId,
                    out Dictionary<string, int> categoryTicks))
            {
                categoryTicks = new Dictionary<string, int>();
                LastDetailedTickByPawn[pawnId] = categoryTicks;
            }

            if (categoryTicks.TryGetValue(category, out int lastTick) &&
                tick - lastTick < interval)
            {
                return false;
            }

            categoryTicks[category] = tick;
            return true;
        }

        public static void ClearPawn(Pawn pawn)
        {
            if (pawn == null)
                return;

            LastDetailedTickByPawn.Remove(pawn.thingIDNumber);
            LoggedWarningCategoriesByPawn.Remove(pawn.thingIDNumber);
        }

        public static void ResetRuntimeCache()
        {
            LastDetailedTickByPawn.Clear();
            LoggedWarningCategoriesByPawn.Clear();
        }

        private static string WithPrefix(string message)
        {
            string safeMessage = message ?? string.Empty;
            return safeMessage.StartsWith(Prefix, StringComparison.Ordinal)
                ? safeMessage
                : Prefix + safeMessage;
        }
    }
}
