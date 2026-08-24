using RimWorld;
using Verse;

namespace AutomaticOutfitManager.Core
{
    [DefOf]
    public static class AutomaticOutfitManagerJobDefOf
    {
        public static JobDef AutomaticOutfitManager_LockerReturn;

        static AutomaticOutfitManagerJobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(
                typeof(AutomaticOutfitManagerJobDefOf));
        }
    }
}
