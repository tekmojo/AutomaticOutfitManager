using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AutomaticOutfitManager.Core;
using HarmonyLib;
using Verse;

namespace AutomaticOutfitManager.Detection
{
    /// <summary>
    /// Optional, reflection-only Simple Sidearms integration. Automatic Outfit
    /// Manager does not alter sidearm memories or preferences; it only asks the
    /// installed mod whether the current weapon choice is protected.
    /// </summary>
    public static class SimpleSidearmsCompatibility
    {
        private const string PackageId = "petetimessix.simplesidearms";
        private static bool initialized;
        private static bool sidearmsActive;
        private static bool available;
        private static bool failureLogged;
        private static MethodInfo getMemoryForPawn;
        private static MethodInfo isCurrentWeaponForced;
        private static Func<Pawn, bool, object> getMemoryForPawnInvoker;
        private static Func<object, bool, bool> isCurrentWeaponForcedInvoker;

        public static bool ProtectsCurrentWeaponChoice(Pawn pawn)
        {
            if (pawn == null)
                return false;

            EnsureInitialized();
            if (!sidearmsActive)
                return false;
            if (!available)
                return true;

            try
            {
                object memory = getMemoryForPawnInvoker(pawn, false);
                return memory != null &&
                       // Preferred/default weapons are normal loadout choices,
                       // not explicit player overrides. Counting them here
                       // prevents every managed rule weapon from being equipped.
                       isCurrentWeaponForcedInvoker(memory, false);
            }
            catch (Exception exception)
            {
                if (!failureLogged)
                {
                    failureLogged = true;
                    AomLog.Warning($"[AutomaticOutfitManager] Simple Sidearms preference check failed; leaving weapon control to Simple Sidearms. {exception.GetType().Name}: {exception.Message}");
                }
                return true;
            }
        }

        private static void EnsureInitialized()
        {
            if (initialized)
                return;
            initialized = true;

            sidearmsActive = LoadedModManager.RunningModsListForReading.Any(mod =>
                string.Equals(mod.PackageId, PackageId, StringComparison.OrdinalIgnoreCase));
            if (!sidearmsActive)
            {
                return;
            }

            Type memoryType = AccessTools.TypeByName("SimpleSidearms.rimworld.CompSidearmMemory");
            if (memoryType != null)
            {
                getMemoryForPawn = AccessTools.Method(
                    memoryType, "GetMemoryCompForPawn", new[] { typeof(Pawn), typeof(bool) });
                isCurrentWeaponForced = AccessTools.Method(
                    memoryType, "IsCurrentWeaponForced", new[] { typeof(bool) });
            }
            available = memoryType != null && getMemoryForPawn != null &&
                        isCurrentWeaponForced != null;
            if (available)
            {
                try
                {
                    ParameterExpression pawn = Expression.Parameter(typeof(Pawn), "pawn");
                    ParameterExpression onlyManual = Expression.Parameter(typeof(bool), "onlyManual");
                    getMemoryForPawnInvoker = Expression.Lambda<Func<Pawn, bool, object>>(
                        Expression.Convert(
                            Expression.Call(getMemoryForPawn, pawn, onlyManual),
                            typeof(object)),
                        pawn,
                        onlyManual).Compile();

                    ParameterExpression memory = Expression.Parameter(typeof(object), "memory");
                    ParameterExpression includePreferred = Expression.Parameter(typeof(bool), "includePreferred");
                    isCurrentWeaponForcedInvoker = Expression.Lambda<Func<object, bool, bool>>(
                        Expression.Call(
                            Expression.Convert(memory, memoryType),
                            isCurrentWeaponForced,
                            includePreferred),
                        memory,
                        includePreferred).Compile();
                }
                catch
                {
                    getMemoryForPawnInvoker = null;
                    isCurrentWeaponForcedInvoker = null;
                    available = false;
                }
            }

            if (!available)
            {
                AomLog.Warning("[AutomaticOutfitManager] Simple Sidearms is active but its preference API was not found; Automatic Outfit Manager will not replace weapons while that mod is active.");
            }
        }
    }
}
