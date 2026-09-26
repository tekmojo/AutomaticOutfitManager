using System;
using Verse;

namespace AutomaticOutfitManager.Detection
{
    // Native repair checks and creates its job with the same component search.
    // Filter inside that search so a claimed nearest stack cannot hide an
    // available alternative or produce a null job after target acceptance.
    internal static class RepairComponentClaims
    {
        [ThreadStatic] private static Pawn selectionPawn;
        [ThreadStatic] private static bool automaticSelection;

        internal struct Scope
        {
            internal bool Entered;
            internal Pawn PreviousPawn;
            internal bool PreviousAutomatic;
        }

        internal static Scope Enter(Pawn pawn, bool forced)
        {
            var scope = new Scope
            {
                Entered = true,
                PreviousPawn = selectionPawn,
                PreviousAutomatic = automaticSelection
            };
            selectionPawn = pawn;
            automaticSelection = !forced;
            return scope;
        }

        internal static void Exit(Scope scope)
        {
            if (!scope.Entered) return;
            selectionPawn = scope.PreviousPawn;
            automaticSelection = scope.PreviousAutomatic;
        }

        internal static Predicate<Thing> WrapValidator(Predicate<Thing> native, Pawn pawn)
        {
            if (!automaticSelection || selectionPawn != pawn ||
                pawn?.Spawned != true || pawn.Drafted || pawn.Downed ||
                pawn.InMentalState || !ManagedWorkClaimRegistry.HasClaims)
                return native;

            return WithClaims(native, pawn);
        }

        // Keep the closure allocation out of the no-claims/native fast path.
        private static Predicate<Thing> WithClaims(Predicate<Thing> native, Pawn pawn)
        {
            return thing => (native == null || native(thing)) &&
                (thing == null || !ManagedWorkClaimRegistry.IsClaimedByOther(
                    pawn, pawn.Map, thing, thing.PositionHeld));
        }
    }
}
