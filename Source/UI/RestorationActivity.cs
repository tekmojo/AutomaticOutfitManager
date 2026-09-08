using System.Linq;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.UI
{
    internal static class RestorationActivity
    {
        internal static string WearLabel(PawnApparelState state, Job job) =>
            job?.targetA.Thing is Apparel apparel && state.OriginalApparel?.Contains(apparel) == true
                ? "Restoring automatic saved apparel" : "Changing personal apparel";

        internal static string Detail(Pawn pawn, PawnApparelState state, Job job)
        {
            if (state?.Transition != ApparelTransition.Restoring || job?.def == null ||
                job.targetA.Thing == null || job.targetA.Thing.Destroyed) return null;
            var target = job.targetA.Thing;
            if (job.def == JobDefOf.Wear && target is Apparel apparel)
            {
                if (state.OriginalApparel?.Contains(apparel) == true)
                    return $"Restoring automatic saved apparel: {apparel.LabelCap}";
                var displaced = SavedApparelReplacementPolicy
                    .ConflictingSavedApparel(pawn, state, apparel);
                if (displaced.Count == 1)
                    return $"Replacement: {apparel.LabelCap}\nReplaces: {displaced[0].LabelCap}";
                // Do not mislabel an unrelated or explicitly ordered Wear as a
                // queued saved item simply because restoration is unfinished.
                return $"Current apparel: {apparel.LabelCap}";
            }
            if (job.def == JobDefOf.Equip && target == state.OriginalWeapon)
                return $"Restoring automatic saved weapons: {target.LabelCap}";
            if (job.def == JobDefOf.RemoveApparel && state.ManagedApparel?.Contains(target) == true)
                return $"Returning automatic outfit apparel: {target.LabelCap}";
            if (job.def == JobDefOf.DropEquipment)
                return $"Returning weapon: {target.LabelCap}";
            return null;
        }
    }
}
