using System.Collections.Generic;
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
    [HarmonyPatch]
    public static class ReservationUtility_SavedApparel_Patch
    {
        [System.ThreadStatic]
        private static Pawn outfitSearchPawn;
        [System.ThreadStatic]
        private static Thing outfitSearchThing;

        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(ReservationUtility)))
            {
                if (method.Name == nameof(ReservationUtility.CanReserve) ||
                    method.Name == nameof(ReservationUtility.CanReserveAndReach))
                {
                    yield return method;
                }
            }
        }

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(object[] __args, ref bool __result)
        {
            if (!__result || __args == null)
                return;

            FindTargets(
                __args, out Pawn pawn, out Apparel apparel,
                out ThingWithComps weapon);
            AutomaticOutfitManagerGameComponent component = AutomaticOutfitManagerGameComponent.Current;
            if (pawn == null || component == null)
                return;

            if (weapon != null)
            {
                if (pawn.Faction != Faction.OfPlayer &&
                    ManagedWeaponClassifier.Matches(weapon.def) &&
                    component.StateFor(pawn)?.IsManagedWeapon(weapon) != true &&
                    !IsOutfitSearchProbe(pawn, weapon))
                {
                    __result = false;
                    return;
                }

                if (component.IsManagedWeaponAssignedToOtherPawn(weapon, pawn))
                {
                    __result = false;
                    return;
                }

                Pawn weaponOwner = component.SavedPawnForWeapon(weapon);
                if (weaponOwner != null && weaponOwner != pawn)
                {
                    PawnApparelState weaponOwnerState = component.StateFor(weaponOwner);
                    if (weaponOwnerState != null &&
                        (weaponOwnerState.Transition == ApparelTransition.ReturningToChangingArea ||
                         weaponOwnerState.Transition == ApparelTransition.Restoring ||
                         weaponOwnerState.WeaponRestorationRequested))
                    {
                        __result = false;
                    }
                }
                return;
            }

            if (apparel == null ||
                !component.IsManagedApparel(apparel))
            {
                return;
            }

            // Guests and other non-colony pawns should never select jobs that
            // reserve managed apparel. The StartJob guard remains a fallback
            // for modded jobs that skip RimWorld's reservation checks.
            if (pawn.Faction != Faction.OfPlayer &&
                !IsAssignedToPawn(component.StateFor(pawn), apparel) &&
                !IsOutfitSearchProbe(pawn, apparel))
            {
                __result = false;
                return;
            }

            // Definition-level work stock is normally shared, but an exact
            // garment already captured in another pawn's personal snapshot
            // temporarily belongs to that pawn while returning/restoring. Give
            // exact saved ownership priority before the generic managed-apparel
            // branch; otherwise a colony hauler can reserve the same formal
            // vest every tick while its owner is trying to put it back on.
            Pawn owner = component.SavedPawnFor(apparel);
            PawnApparelState ownerState = component.StateFor(owner);
            if (owner != null && owner != pawn && ownerState != null &&
                (ownerState.Transition == ApparelTransition.ReturningToChangingArea ||
                 ownerState.Transition == ApparelTransition.Restoring))
            {
                __result = false;
                return;
            }

            // Apparel required by an enabled rule is shared work gear. A
            // particular item can still carry an older saved-owner record
            // after being removed from a pawn. Outside the narrow active return
            // window above, that record must not stop colony haulers from
            // returning the loose item to locker storage.
            if (AutomaticOutfitManager.Storage.ManagedApparelClassifier.Matches(apparel.def))
            {
                if (component.IsManagedApparelAssignedToOtherPawn(apparel, pawn))
                    __result = false;
                return;
            }

            if (owner == null || owner == pawn)
                return;

            if (ownerState != null &&
                (ownerState.Transition == ApparelTransition.ReturningToChangingArea ||
                 ownerState.Transition == ApparelTransition.Restoring))
            {
                __result = false;
            }
        }

        internal static bool CanReserveForOutfit(Pawn pawn, Thing gear)
        {
            if (pawn == null || gear == null)
                return false;

            Pawn previousPawn = outfitSearchPawn;
            Thing previousThing = outfitSearchThing;
            outfitSearchPawn = pawn;
            outfitSearchThing = gear;
            try
            {
                // Preserve every native and third-party reservation decision.
                // The context only prevents this patch's pre-assignment guest
                // guard from rejecting AOM's own synchronous selection probe.
                return pawn.CanReserve(gear);
            }
            finally
            {
                outfitSearchPawn = previousPawn;
                outfitSearchThing = previousThing;
            }
        }

        private static bool IsOutfitSearchProbe(Pawn pawn, Thing gear) =>
            outfitSearchPawn == pawn && outfitSearchThing == gear;

        private static bool IsAssignedToPawn(PawnApparelState state, Apparel apparel)
        {
            if (state == null || apparel == null)
                return false;

            if (state.Transition == ApparelTransition.Preparing ||
                state.Transition == ApparelTransition.Active)
                return state.IsPreparationApparel(apparel);

            if (state.Transition == ApparelTransition.Restoring)
                return state.ManagedApparel?.Contains(apparel) == true ||
                       state.OriginalApparel?.Contains(apparel) == true;

            return false;
        }

        private static void FindTargets(
            IEnumerable<object> arguments,
            out Pawn pawn,
            out Apparel apparel,
            out ThingWithComps weapon)
        {
            pawn = null;
            apparel = null;
            weapon = null;

            foreach (object argument in arguments)
            {
                if (pawn == null && argument is Pawn targetPawn)
                    pawn = targetPawn;

                Thing thing = argument is LocalTargetInfo target
                    ? target.Thing
                    : argument as Thing;
                if (thing is Apparel targetApparel)
                    apparel = targetApparel;
                else if (thing is ThingWithComps targetWeapon &&
                         targetWeapon.def?.IsWeapon == true)
                    weapon = targetWeapon;

                if (pawn != null && (apparel != null || weapon != null))
                    return;
            }
        }
    }
}
