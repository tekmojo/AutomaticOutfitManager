using System;
using System.Linq;
using AutomaticOutfitManager.Detection;
using Verse;
using Verse.AI;

internal static class RepairComponentClaimTests
{
    static int passed;
    static void Check(bool value, string label)
    { if (!value) throw new Exception(label); passed++; }

    static Thing Pick(Pawn pawn, Predicate<Thing> native, params Thing[] candidates)
        => candidates.FirstOrDefault(t => RepairComponentClaims.WrapValidator(native, pawn)(t));

    public static int Main()
    {
        var map = new Map();
        var owner = new Pawn { Map = map };
        var worker = new Pawn { Map = map };
        var claimed = new Thing { MapHeld = map, PositionHeld = new IntVec3(10) };
        var alternate = new Thing { MapHeld = map, PositionHeld = new IntVec3(20) };
        var job = new Job { def = new JobDef(), targetB = new LocalTargetInfo(claimed) };
        Predicate<Thing> native = t => true;
        ManagedWorkClaimRegistry.ResetForLoadedGame();
        ManagedWorkClaimRegistry.TryClaim(owner, job);
        Check(Pick(worker, native, claimed) == claimed, "unscoped native search is unchanged");
        var outer = RepairComponentClaims.Enter(worker, false);
        try
        {
            Check(Pick(worker, native, claimed) == null, "only claimed material declines early");
            Check(Pick(worker, native, claimed, alternate) == alternate, "search advances past claimed nearest stack");
            Check(Pick(worker, t => t != alternate, claimed, alternate) == null, "native forbidden or reservation rejection remains binding");
            Check(Pick(worker, native, claimed) == null, "unreachable alternate absent from native candidates cannot be invented");
            Check(Pick(owner, native, claimed) == claimed, "unrelated nested pawn does not inherit another pawn context");
            var own = RepairComponentClaims.Enter(owner, false);
            try { Check(Pick(owner, native, claimed) == claimed, "owner may select its claimed stack"); }
            finally { RepairComponentClaims.Exit(own); }
            Check(Pick(worker, native, claimed) == null, "nested pawn exit restores outer filter");
            var forced = RepairComponentClaims.Enter(worker, true);
            try
            {
                Check(Pick(worker, native, claimed) == claimed, "forced native repair is preserved");
                Check(Pick(worker, t => false, claimed) == null, "forced repair preserves native rejection");
                throw new InvalidOperationException("test exception");
            }
            catch (InvalidOperationException) { }
            finally { RepairComponentClaims.Exit(forced); }
            Check(Pick(worker, native, claimed) == null, "exception cleanup restores automatic context");
            foreach (string bypass in new[] { "drafted", "downed", "mental", "unspawned" })
            {
                worker.Drafted = bypass == "drafted"; worker.Downed = bypass == "downed";
                worker.InMentalState = bypass == "mental"; worker.Spawned = bypass != "unspawned";
                Check(ReferenceEquals(RepairComponentClaims.WrapValidator(native, worker), native), bypass + " retains native validator");
            }
            worker.Drafted = worker.Downed = worker.InMentalState = false; worker.Spawned = true;
            Check(Pick(worker, t => t != alternate, claimed, alternate) == null, "native denial never broadened");
            ManagedWorkClaimRegistry.ReleaseAll(owner);
            Check(ReferenceEquals(RepairComponentClaims.WrapValidator(native, worker), native), "no claims keeps native delegate without allocation");
            Check(Pick(worker, native, claimed) == claimed, "release restores availability immediately");
            ManagedWorkClaimRegistry.TryClaim(owner, job);
            Find.TickManager.TicksGame += 15001;
            Check(Pick(worker, native, claimed) == claimed, "expired claim no longer blocks");
            ManagedWorkClaimRegistry.TryClaim(owner, job);
            ManagedWorkClaimRegistry.ResetForLoadedGame();
            Check(Pick(worker, native, claimed) == claimed, "load reset removes old claims");
        }
        finally { RepairComponentClaims.Exit(outer); }
        ManagedWorkClaimRegistry.TryClaim(owner, job);
        Check(Pick(worker, native, claimed) == claimed, "outer exit leaves native searches unfiltered");
        RepairComponentClaims.Exit(default(RepairComponentClaims.Scope));
        Check(Pick(worker, native, claimed) == claimed, "unentered finalizer state is harmless");
        Console.WriteLine("PASS " + passed + " repair-component claim checks");
        return 0;
    }
}
