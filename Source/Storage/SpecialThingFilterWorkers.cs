using Verse;

namespace AutomaticOutfitManager.Storage
{
    public sealed class SpecialThingFilterWorker_ManagedOutfit : SpecialThingFilterWorker
    {
        public override bool Matches(Thing thing) => ManagedApparelClassifier.Matches(thing);

        public override bool CanEverMatch(ThingDef def) => def?.apparel != null;
    }

    public sealed class SpecialThingFilterWorker_NonManagedOutfit : SpecialThingFilterWorker
    {
        public override bool Matches(Thing thing) =>
            thing?.def?.apparel != null && !ManagedApparelClassifier.Matches(thing);

        public override bool CanEverMatch(ThingDef def) => def?.apparel != null;
    }

    public sealed class SpecialThingFilterWorker_ManagedWeapon : SpecialThingFilterWorker
    {
        public override bool Matches(Thing thing) => ManagedWeaponClassifier.Matches(thing);

        public override bool CanEverMatch(ThingDef def) => def?.IsWeapon == true;
    }

    public sealed class SpecialThingFilterWorker_NonManagedWeapon : SpecialThingFilterWorker
    {
        public override bool Matches(Thing thing) =>
            thing?.def?.IsWeapon == true && !ManagedWeaponClassifier.Matches(thing);

        public override bool CanEverMatch(ThingDef def) => def?.IsWeapon == true;
    }
}
