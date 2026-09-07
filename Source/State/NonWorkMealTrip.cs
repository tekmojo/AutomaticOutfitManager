using Verse;

namespace AutomaticOutfitManager.State
{
    public enum NonWorkMealStage { Captured, Returning, PreparingFallback, Eating }

    // Only references are saved here. The native tracker owns every running or
    // queued job, and the pawn's carry tracker/inventory owns the actual meal.
    public sealed class NonWorkMealTrip : IExposable
    {
        public Pawn Pawn;
        public Map Map;
        public Thing Meal;
        public int Count;
        public string SourceRuleId;
        public string DestinationRuleId;
        public IntVec3 DiningCell = IntVec3.Invalid;
        public IntVec3 LockerCell = IntVec3.Invalid;
        public NonWorkMealStage Stage;
        public int JobLoadId = -1;
        public int StartedTick;
        public bool EatInPlace;
        public bool UsesFallback;
        public SavedNonWorkOutfit ReturnOutfit;
        public SavedNonWorkOutfit DestinationOutfit;

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn");
            Scribe_References.Look(ref Map, "map");
            Scribe_References.Look(ref Meal, "meal");
            Scribe_Values.Look(ref Count, "count");
            Scribe_Values.Look(ref SourceRuleId, "sourceRuleId");
            Scribe_Values.Look(ref DestinationRuleId, "destinationRuleId");
            Scribe_Values.Look(ref DiningCell, "diningCell", IntVec3.Invalid);
            Scribe_Values.Look(ref LockerCell, "lockerCell", IntVec3.Invalid);
            Scribe_Values.Look(ref Stage, "stage");
            Scribe_Values.Look(ref JobLoadId, "jobLoadId", -1);
            Scribe_Values.Look(ref StartedTick, "startedTick");
            Scribe_Values.Look(ref EatInPlace, "eatInPlace");
            Scribe_Values.Look(ref UsesFallback, "usesFallback");
            Scribe_Deep.Look(ref ReturnOutfit, "returnOutfit");
            Scribe_Deep.Look(ref DestinationOutfit, "destinationOutfit");
        }
    }
}
