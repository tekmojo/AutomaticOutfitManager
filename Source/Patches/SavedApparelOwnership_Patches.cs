using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.Storage;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), "ApparelScoreGain")]
    [HarmonyPriority(Priority.Last)]
    public static class JobGiverOptimizeApparel_ScoreGain_SavedOwner_Patch
    {
        public static void Postfix(Pawn __0, Apparel __1, ref float __result)
        {
            Pawn pawn = __0;
            Apparel apparel = __1;
            if (apparel == null || pawn == null)
                return;

            // Rule apparel belongs to the shared locker pool. It must only be
            // worn by an AutomaticOutfitManager transition (or an explicit player
            // order), never selected as a pawn's ordinary optimized outfit.
            if (ManagedApparelClassifier.Matches(apparel.def))
            {
                __result = float.MinValue;
                return;
            }

            if (AutomaticOutfitManagerGameComponent.Current?.IsSavedForOtherPawn(apparel, pawn) == true)
                __result = float.MinValue;
        }
    }

    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), "ApparelScoreRaw")]
    [HarmonyPriority(Priority.Last)]
    public static class JobGiverOptimizeApparel_ScoreRaw_SavedOwner_Patch
    {
        public static void Postfix(Pawn __0, Apparel __1, ref float __result)
        {
            Pawn pawn = __0;
            Apparel apparel = __1;
            if (apparel == null || pawn == null)
                return;

            if (ManagedApparelClassifier.Matches(apparel.def))
            {
                __result = float.MinValue;
                return;
            }

            if (AutomaticOutfitManagerGameComponent.Current?.IsSavedForOtherPawn(apparel, pawn) == true)
                __result = float.MinValue;
        }
    }

    [HarmonyPatch]
    public static class EquipmentUtility_CanEquip_SavedApparel_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(EquipmentUtility)))
            {
                if (method.Name == nameof(EquipmentUtility.CanEquip))
                    yield return method;
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(
            Thing __0, Pawn __1, object[] __args, ref bool __result)
        {
            if (!__result || __1 == null)
                return;

            if (__0 is ThingWithComps weapon && weapon.def?.IsWeapon == true)
            {
                AutomaticOutfitManagerGameComponent component =
                    AutomaticOutfitManagerGameComponent.Current;
                Pawn owner = component?.SavedPawnForWeapon(weapon);
                if (owner != null && owner != __1)
                {
                    __result = false;
                    SetCantReason(
                        __args,
                        $"Saved by Automatic Outfit Manager as " +
                        $"{owner.LabelShortCap}'s personal primary weapon");
                    return;
                }

                owner = component?.ManagedPawnForWeapon(weapon);
                if (owner != null && owner != __1)
                {
                    __result = false;
                    SetCantReason(
                        __args,
                        $"Reserved by Automatic Outfit Manager for " +
                        $"{owner.LabelShortCap}'s active work outfit");
                }
                return;
            }

            if (!(__0 is Apparel apparel))
                return;

            // Required work apparel is shared. Ownership only protects saved
            // personal apparel that is not itself assigned to a rule.
            if (ManagedApparelClassifier.Matches(apparel.def))
                return;

            AutomaticOutfitManagerGameComponent current =
                AutomaticOutfitManagerGameComponent.Current;
            Pawn apparelOwner = current?.SavedPawnFor(apparel);
            if (apparelOwner != null && apparelOwner != __1)
            {
                __result = false;
                SetCantReason(
                    __args,
                    $"Saved by Automatic Outfit Manager as " +
                    $"{apparelOwner.LabelShortCap}'s personal apparel");
            }
        }

        private static void SetCantReason(object[] arguments, string reason)
        {
            // RimWorld's reason-producing CanEquip overload passes its out
            // string as argument 2. Harmony writes changes to __args back to
            // ref/out arguments, while the two-argument convenience overload
            // simply has no reason slot to update.
            if (arguments?.Length > 2)
                arguments[2] = reason;
        }
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker),
        nameof(Pawn_EquipmentTracker.AddEquipment))]
    [HarmonyPriority(Priority.First)]
    public static class PawnEquipmentTracker_AddEquipment_SavedWeapon_Patch
    {
        public static bool Prefix(
            Pawn_EquipmentTracker __instance, ThingWithComps newEq)
        {
            if (newEq?.def?.IsWeapon != true)
                return true;

            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            Faction playerFaction = Faction.OfPlayerSilentFail;
            if (pawn != null && playerFaction != null &&
                pawn.Faction != playerFaction &&
                (component?.IsManagedWeapon(newEq) == true ||
                 ManagedWeaponClassifier.Matches(newEq.def)) &&
                component.StateFor(pawn)?.IsManagedWeapon(newEq) != true)
            {
                return false;
            }
            return component?.IsSavedWeaponForOtherPawn(newEq, pawn) != true &&
                   component?.IsManagedWeaponAssignedToOtherPawn(newEq, pawn) != true;
        }
    }

    [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear),
        typeof(Apparel), typeof(bool), typeof(bool))]
    [HarmonyPriority(Priority.First)]
    public static class PawnApparelTracker_Wear_SavedApparel_Patch
    {
        public static bool Prefix(Pawn_ApparelTracker __instance, Apparel newApparel)
        {
            if (newApparel == null || ManagedApparelClassifier.Matches(newApparel.def))
                return true;

            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            return AutomaticOutfitManagerGameComponent.Current?.IsSavedForOtherPawn(newApparel, pawn) != true;
        }

        public static void Postfix(
            Pawn_ApparelTracker __instance, Apparel newApparel)
        {
            if (newApparel == null ||
                __instance?.WornApparel?.Contains(newApparel) != true)
            {
                return;
            }

            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            AutomaticOutfitManagerGameComponent.Current?
                .AdoptWornPersonalApparel(pawn, newApparel);
        }
    }

    internal static class SavedApparelReplacementPolicy
    {
        internal const float MinimumScoreGain = 0.05f;
        private static readonly MethodInfo ApparelScoreRawMethod =
            AccessTools.Method(typeof(JobGiver_OptimizeApparel), "ApparelScoreRaw");

        internal static bool CanStart(
            Pawn pawn,
            PawnApparelState state,
            AutomaticOutfitManagerGameComponent component,
            Pawn_JobTracker tracker,
            Job job,
            ThinkNode jobGiver)
        {
            if (pawn == null || state?.ApparelInterventionActive != true ||
                job?.def != JobDefOf.Wear ||
                job.targetA.Thing is not Apparel replacement ||
                state.OriginalApparel?.Contains(replacement) == true ||
                state.IsPreparationApparel(replacement) ||
                ManagedApparelClassifier.Matches(replacement.def) ||
                component?.IsSavedForOtherPawn(replacement, pawn) == true ||
                component?.IsManagedApparelAssignedToOtherPawn(
                    replacement, pawn) == true)
            {
                return false;
            }

            // Explicit apparel orders remain authoritative. The successful Wear
            // callback below updates the saved snapshot only after RimWorld has
            // actually accepted and worn the item.
            if (job.playerForced)
                return true;

            ThinkNode origin = jobGiver ?? job.jobGiver;
            if (origin is not JobGiver_OptimizeApparel ||
                (state.Transition != ApparelTransition.Active &&
                 state.Transition != ApparelTransition.Restoring))
            {
                return false;
            }

            // A queued Phase 3 plan owns its exact order. Only let the native
            // optimizer fill an otherwise idle restoration boundary; otherwise
            // a stale queued Wear could immediately put the displaced item back.
            if (state.Transition == ApparelTransition.Restoring &&
                tracker?.jobQueue?.Count > 0)
            {
                return false;
            }

            List<Apparel> displaced = ConflictingSavedApparel(
                pawn, state, replacement);
            if (displaced.Count == 0)
                return false;

            // Ordinary optimization may improve a compatible personal layer
            // during active work, but it must never displace the exact PPE that
            // currently satisfies the managed rule.
            if (state.Transition == ApparelTransition.Active &&
                ConflictsWithPreparationApparel(pawn, state, replacement))
            {
                return false;
            }

            return NativeScore(pawn, replacement) >
                   displaced.Sum(item => NativeScore(pawn, item)) +
                   MinimumScoreGain;
        }

        internal static List<Apparel> ConflictingSavedApparel(
            Pawn pawn, PawnApparelState state, Apparel replacement)
        {
            var result = new List<Apparel>();
            BodyDef body = pawn?.RaceProps?.body ?? BodyDefOf.Human;
            foreach (Apparel saved in state?.OriginalApparel ??
                         Enumerable.Empty<Apparel>())
            {
                if (saved != null && saved != replacement &&
                    !ApparelUtility.CanWearTogether(
                        saved.def, replacement.def, body))
                {
                    result.Add(saved);
                }
            }
            return result;
        }

        private static bool ConflictsWithPreparationApparel(
            Pawn pawn, PawnApparelState state, Apparel replacement)
        {
            BodyDef body = pawn?.RaceProps?.body ?? BodyDefOf.Human;
            IEnumerable<Apparel> protection =
                (state.ManagedApparel ?? new List<Apparel>())
                .Concat(state.ReusedOriginalApparel ?? new List<Apparel>())
                .Where(item => item != null && item != replacement)
                .Distinct();
            return protection.Any(item => !ApparelUtility.CanWearTogether(
                item.def, replacement.def, body));
        }

        internal static float NativeScore(Pawn pawn, Apparel apparel)
        {
            if (ApparelScoreRawMethod == null || pawn == null || apparel == null)
                return float.MinValue;

            try
            {
                object value = ApparelScoreRawMethod.Invoke(
                    null, new object[] { pawn, apparel });
                return value is float score ? score : float.MinValue;
            }
            catch
            {
                // If another mod makes the native score unavailable, keep the
                // exact snapshot instead of guessing that an item is an upgrade.
                return float.MinValue;
            }
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetGizmos))]
    public static class Apparel_GetGizmos_SavedOwner_Patch
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, ThingWithComps __instance)
        {
            foreach (Gizmo gizmo in __result)
                yield return gizmo;

            AutomaticOutfitManagerGameComponent component = AutomaticOutfitManagerGameComponent.Current;
            if (__instance?.def?.IsWeapon == true && component != null)
            {
                Pawn weaponOwner = component.SavedPawnForWeapon(__instance);
                if (weaponOwner != null)
                {
                    if (weaponOwner.Spawned)
                    {
                        yield return new Command_Action
                        {
                            defaultLabel = $"Jump to {weaponOwner.LabelShortCap}",
                            defaultDesc = $"Select and center the camera on {weaponOwner.LabelShortCap}, the owner of this exact saved primary weapon.",
                            icon = TexButton.ShowImportantLocations,
                            action = () => CameraJumper.TryJumpAndSelect(weaponOwner)
                        };

                        yield return new Command_Action
                        {
                            defaultLabel = "Recall owner",
                            defaultDesc = $"Recall {weaponOwner.LabelShortCap} from managed work. They return to the locker room when configured, return managed items, and restore their exact saved apparel and primary weapon.",
                            icon = TexCommand.ClearPrioritizedWork,
                            action = () => component.RequestRecall(
                                component.StateFor(weaponOwner))
                        };
                    }

                    yield return ReleaseItemCommand(
                        __instance,
                        weaponOwner.LabelShortCap,
                        () => component.ReleaseSavedWeapon(__instance));
                }
                yield break;
            }

            Apparel apparel = __instance as Apparel;
            if (apparel == null || component == null)
            {
                yield break;
            }

            string ownerName = component.SavedOwnerFor(apparel);
            if (string.IsNullOrEmpty(ownerName))
                yield break;

            Pawn owner = component.SavedPawnFor(apparel);
            if (owner?.Spawned == true)
            {
                yield return new Command_Action
                {
                    defaultLabel = $"Jump to {owner.LabelShortCap}",
                    defaultDesc = $"Select and center the camera on {owner.LabelShortCap}, the owner of this exact saved apparel item.",
                    icon = TexButton.ShowImportantLocations,
                    action = () => CameraJumper.TryJumpAndSelect(owner)
                };

                yield return new Command_Action
                {
                    defaultLabel = "Recall owner",
                    defaultDesc = $"Recall {owner.LabelShortCap} from managed work. They return to the locker room when configured, return managed items, and restore their exact saved apparel and primary weapon.",
                    icon = TexCommand.ClearPrioritizedWork,
                    action = () => component.RequestRecall(
                        component.StateFor(owner))
                };
            }

            yield return ReleaseItemCommand(
                apparel,
                ownerName,
                () => component.ReleaseSavedApparel(apparel));
        }

        private static Command_Action ReleaseItemCommand(
            ThingWithComps item, string ownerName, System.Action release)
        {
            bool weapon = item?.def?.IsWeapon == true;
            string itemKind = weapon ? "saved primary weapon" : "saved apparel";
            string consequence = weapon
                ? $"{ownerName} will no longer restore this exact primary weapon. Automatic Outfit Manager will not choose a replacement saved weapon, so the pawn may finish restoration unarmed. The item becomes available to other pawns."
                : $"{ownerName} will no longer restore this exact apparel item. It becomes ordinary apparel and may be worn by another pawn.";

            return new Command_Action
            {
                defaultLabel = "Release item",
                defaultDesc = $"Permanently remove this exact {itemKind} from {ownerName}'s saved outfit and release it for normal use. A confirmation is required.",
                icon = TexCommand.ForbidOn,
                action = () => Find.WindowStack.Add(
                    Dialog_MessageBox.CreateConfirmation(
                        $"Release {item.LabelCap} from {ownerName}'s saved outfit?\n\n{consequence}",
                        release,
                        true))
            };
        }
    }
}
