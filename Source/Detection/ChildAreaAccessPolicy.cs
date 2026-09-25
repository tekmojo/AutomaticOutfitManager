using AutomaticOutfitManager.Rules;
using Verse;

namespace AutomaticOutfitManager.Detection
{
    internal static class ChildAreaAccessPolicy
    {
        // Babies' care/carrying and animal/mech life stages are native behavior.
        internal static bool IsChild(Pawn pawn) => pawn?.RaceProps?.Humanlike == true &&
            pawn.DevelopmentalStage == DevelopmentalStage.Child;

        // This per-area pass deliberately leaves native job eligibility alone.
        internal static bool BypassesAdultRules(Pawn pawn, ApparelRule rule) =>
            rule?.Enabled == true && rule.AllowChildren && IsChild(pawn);

        internal static bool Disallows(Pawn pawn, ApparelRule rule) =>
            rule?.Enabled == true && !rule.AllowChildren && IsChild(pawn);
    }
}
