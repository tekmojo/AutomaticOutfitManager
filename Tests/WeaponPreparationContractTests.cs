using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.UI;
using RimWorld;
using Verse;
using Verse.AI;

internal static class WeaponPreparationContractTests
{
    private static int passed;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; }
    private static ThingWithComps Stock(Map map, int id, ThingDef def = null)
    {
        var item = new ThingWithComps { thingIDNumber = id, Map = map, def = def ?? Gun };
        map.listerThings.Items.Add(item);
        return item;
    }
    private static readonly ThingDef Gun = new ThingDef { IsRangedWeapon = true };
    private static Job Equip(ThingWithComps weapon, int id = 1) => new Job { def = JobDefOf.Equip, targetA = new LocalTargetInfo(weapon), loadID = id };
    private static PawnApparelState Begin(Pawn pawn, ThingWithComps weapon)
    {
        var state = new PawnApparelState { Pawn = pawn, Transition = ApparelTransition.Preparing };
        AutomaticOutfitManagerGameComponent.Current.States[pawn] = state;
        state.BeginManagedWeapon(pawn, weapon);
        return state;
    }
    private static ThingWithComps FindWeapon(Pawn pawn, CombinedWeaponRequirement requirement) =>
        WeaponFinder.FindClosest(pawn, requirement, null);
    public static int Main()
    {
        try
        {
            var component = AutomaticOutfitManagerGameComponent.Current = new AutomaticOutfitManagerGameComponent();
            var map = new Map { uniqueID = 1 };
            var pawn = new Pawn { thingIDNumber = 1, Map = map };
            var other = new Pawn { thingIDNumber = 2, Map = map };
            var requirement = new CombinedWeaponRequirement { Def = Gun };
            var first = Stock(map, 101);
            var second = Stock(map, 102);
            var third = Stock(map, 103);
            var unrelated = Stock(map, 104, new ThingDef());
            var rule = new ApparelRule { Id = "ship", Enabled = true, Area = new Area { Map = map }, Requirement = requirement };
            component.Rules.Add(rule);
            Find.TickManager.TicksGame = 0;
            WeaponPreparationRetryRegistry.ResetForLoadedGame();
            Check(FindWeapon(pawn, requirement) == first, "initial stock selection");

            var state = Begin(pawn, first);
            state.RecordWeaponPreparationAttempt(first, Equip(first));
            state.RecordWeaponPreparationAttempt(first, Equip(first));
            Check(state.WeaponPreparationAttemptsThisTransition == 1, "duplicate admission does not spend another candidate attempt");
            Check(state.TryUseWeaponPreparationRetry(first), "one exact retry is available");
            Check(!state.TryUseWeaponPreparationRetry(first), "same-candidate retry is bounded");
            Check(state.WeaponPreparationAttemptsThisTransition == 2, "the actual extra proposal spends one attempt");
            state.RejectLastWeaponPreparationAttempt();
            Check(!state.ManagedWeapons.Contains(first), "rejected spawned stock releases assignment");
            Check(FindWeapon(pawn, requirement) == second, "next candidate after actual unresolved proposal");
            state.CompleteWeaponRestoration();
            component.States.Remove(pawn);
            Check(FindWeapon(pawn, requirement) == second, "exact rejection survives completed restoration and deleted state");
            Check(FindWeapon(other, requirement) == first, "another pawn can acquire the shared rejected candidate");

            // Production abort captures the last candidate before recall clears
            // the snapshot, and defers the unchanged matching stock pool.
            state = Begin(pawn, second);
            state.PendingWorkJob = new Job { def = JobDefOf.LayDown };
            state.CurrentRuleIds.Add(rule.Id);
            state.RecordWeaponPreparationAttempt(second, Equip(second));
            state.WeaponPreparationAttemptsThisTransition = 6;
            Job next = state.PendingWorkJob;
            ThinkNode giver = null;
            JobTag? tag = null;
            var tracker = new Pawn_JobTracker();
            Check(PawnJobTracker_StartJob_Patch.TryAbortExhaustedWeaponPreparation(
                tracker, pawn, component, state, new[] { rule }, ref next, ref giver, ref tag), "real exhausted-cycle abort");
            Check(state.RecallRequested && state.PendingWorkJob == null, "abort requests recall and releases pending work");
            Check(next.def == JobDefOf.Wait && tracker.Cleared == 1, "abort leaves one bounded wait");
            Check(!state.ManagedWeapons.Contains(second), "last candidate released even at the budget boundary");
            Check(state.RejectedWeaponPreparations.Any(r => r.Weapon == second), "last proposal remains in rejection history");
            Check(state.WeaponPreparationAttemptsThisTransition == 0, "local preparation counter can reset independently");
            state.CompleteWeaponRestoration();
            component.States.Remove(pawn);
            Check(FindWeapon(pawn, requirement) == null, "essential rest replanning cannot restart same full outfit cycle");
            Check(FindWeapon(other, requirement) == first, "whole-pool backoff remains pawn-specific");
            Check(!WeaponPreparationRetryRegistry.IsDeferred(pawn, unrelated), "unrelated weapon requirement unaffected");
            state = Begin(pawn, third);
            Check(FindWeapon(pawn, requirement) == null, "fresh BeginManagedWeapon does not clear cross-session backoff");
            state.CompleteWeaponRestoration();
            component.States.Remove(pawn);
            Find.TickManager.TicksGame = 2499;
            Check(FindWeapon(pawn, requirement) == null, "entire first cooldown honored across native proposals");
            Find.TickManager.TicksGame = 2500;
            Check(FindWeapon(pawn, requirement) == first, "bounded retry reopens without a permanent blacklist");
            state = Begin(pawn, first);
            state.RecordWeaponPreparationAttempt(first, Equip(first));
            state.WeaponPreparationAttemptsThisTransition = 6;
            state.PendingWorkJob = next = new Job { def = JobDefOf.LayDown };
            state.CurrentRuleIds.Add(rule.Id);
            Check(PawnJobTracker_StartJob_Patch.TryAbortExhaustedWeaponPreparation(
                tracker, pawn, component, state, new[] { rule }, ref next, ref giver, ref tag), "second actual full-cycle abort");
            state.CompleteWeaponRestoration();
            component.States.Remove(pawn);
            Find.TickManager.TicksGame = 7499;
            Check(FindWeapon(pawn, requirement) == null, "second exhausted cycle retains five-thousand-tick backoff after restoration");
            Find.TickManager.TicksGame = 7500;
            Check(FindWeapon(pawn, requirement) == first, "second whole-cycle backoff expires exactly once");
            Check(WeaponPreparationRetryRegistry.DeferUnchangedStock(pawn, requirement) == 10000, "third exhausted cycle waits longer");
            Check(WeaponPreparationRetryRegistry.DeferUnchangedStock(pawn, requirement) == 15000, "backoff has an upper bound");
            Check(WeaponPreparationRetryRegistry.DeferUnchangedStock(pawn, requirement) == 15000, "backoff cannot overflow or grow indefinitely");

            var fresh = Stock(map, 105);
            Check(FindWeapon(pawn, requirement) == fresh, "new stock reopens immediately");
            WeaponPreparationRetryRegistry.Reject(pawn, fresh);
            fresh.PositionHeld = new IntVec3 { Value = 1 };
            Check(!WeaponPreparationRetryRegistry.IsDeferred(pawn, fresh), "moved stock reopens");
            WeaponPreparationRetryRegistry.Reject(pawn, fresh);
            fresh.Forbidden = true;
            Check(!WeaponPreparationRetryRegistry.IsDeferred(pawn, fresh), "forbid state change invalidates old availability");
            Check(FindWeapon(pawn, requirement) == null, "forbidden stock still rejected by production finder");
            fresh.Forbidden = false;
            fresh.Reservable = false;
            WeaponPreparationRetryRegistry.Reject(pawn, fresh);
            fresh.Reservable = true;
            Check(!WeaponPreparationRetryRegistry.IsDeferred(pawn, fresh), "released native reservation reopens candidate");
            fresh.Equippable = false;
            WeaponPreparationRetryRegistry.Reject(pawn, fresh);
            fresh.Equippable = true;
            Check(!WeaponPreparationRetryRegistry.IsDeferred(pawn, fresh), "changed equip capability reopens candidate");
            fresh.SavedOther = true;
            WeaponPreparationRetryRegistry.Reject(pawn, fresh);
            Check(FindWeapon(pawn, requirement) == null, "saved owner cannot be bypassed");
            fresh.SavedOther = false;
            Check(FindWeapon(pawn, requirement) == fresh, "released saved ownership is material progress");
            fresh.ManagedOther = true;
            WeaponPreparationRetryRegistry.Reject(pawn, fresh);
            Check(FindWeapon(pawn, requirement) == null, "other managed assignment cannot be bypassed");
            fresh.ManagedOther = false;
            Check(FindWeapon(pawn, requirement) == fresh, "released managed assignment is material progress");
            Check(WeaponFinder.FindClosest(pawn, requirement, null, excludedThings: new HashSet<Thing> { fresh }) == null,
                "preserved native targets remain excluded");
            Check(WeaponFinder.FindClosest(pawn, requirement, null, candidateAllowed: w => false) == null,
                "foreign-area route filter remains mandatory");

            // Native Equip diagnostics distinguish admission and actual end
            // condition. Only successful exact equipment resets cycle escalation.
            state = Begin(pawn, fresh);
            var equip = Equip(fresh, 900);
            AomLog.DetailedEnabled = true;
            state.RecordWeaponPreparationAttempt(fresh, equip);
            WeaponPreparationDiagnostics.Admitted(pawn, equip, equip);
            WeaponPreparationDiagnostics.Admitted(pawn, equip, new Job { def = JobDefOf.Wait, loadID = 901 });
            WeaponPreparationDiagnostics.Ended(pawn, state, equip, JobCondition.InterruptForced);
            Check(AomLog.Messages.Any(s => s.Contains("Equip#900 proposed")), "proposal diagnostic has exact job identity");
            Check(AomLog.Messages.Any(s => s.Contains("Equip#900 admitted")), "actual admission diagnostic");
            Check(AomLog.Messages.Any(s => s.Contains("replaced before admission by Wait#901")), "replacement is not called a native equip failure");
            Check(AomLog.Messages.Any(s => s.Contains("ended InterruptForced") && s.Contains("targetIsPrimary=False")), "interruption reports actual equipment state");
            WeaponPreparationDiagnostics.Ended(pawn, state, equip, JobCondition.Incompletable);
            Check(AomLog.Messages.Any(s => s.Contains("ended Incompletable")), "actual native failure condition preserved");
            WeaponPreparationDiagnostics.Ended(pawn, state, equip, JobCondition.Succeeded);
            Check(WeaponPreparationRetryRegistry.DeferUnchangedStock(pawn, requirement) == 15000,
                "nominal success without target equipped cannot reset backoff");
            pawn.equipment.Primary = fresh;
            WeaponPreparationDiagnostics.Ended(pawn, state, equip, JobCondition.Succeeded);
            Check(!WeaponPreparationRetryRegistry.IsDeferred(pawn, fresh), "actual exact equip clears its own deferred state");
            Check(WeaponPreparationRetryRegistry.IsDeferred(pawn, first), "success does not erase other rejected candidates");
            Check(WeaponPreparationRetryRegistry.DeferUnchangedStock(pawn, requirement) == 2500,
                "actual successful preparation resets cycle escalation");
            state.Transition = ApparelTransition.Restoring;
            WeaponPreparationDiagnostics.Ended(pawn, state, equip, JobCondition.Succeeded);
            Check(WeaponPreparationRetryRegistry.DeferUnchangedStock(pawn, requirement) == 5000,
                "personal restoration does not masquerade as successful work preparation");
            state.Transition = ApparelTransition.Preparing;
            var queuedEquip = Equip(fresh, 902);
            PreparationJobHandoff.Queued.Add(queuedEquip);
            int beforeQueued = AomLog.Messages.Count;
            WeaponPreparationDiagnostics.Admitted(pawn, queuedEquip, new Job { def = JobDefOf.Wait, loadID = 903 });
            Check(AomLog.Messages.Count == beforeQueued + 1 && AomLog.Messages.Last().Contains("queued behind Wait#903"),
                "queued root is distinct from cancelled admission");
            PreparationJobHandoff.Queued.Clear();
            WeaponPreparationDiagnostics.Ended(pawn, state, queuedEquip, JobCondition.Succeeded);
            int afterSynchronousEnd = AomLog.Messages.Count;
            WeaponPreparationDiagnostics.Admitted(pawn, queuedEquip, new Job { def = JobDefOf.Wait });
            Check(AomLog.Messages.Count == afterSynchronousEnd, "synchronous successful Equip is not reported as replacement");
            queuedEquip.loadID++;
            WeaponPreparationDiagnostics.Admitted(pawn, queuedEquip, new Job { def = JobDefOf.Wait });
            Check(AomLog.Messages.Count == afterSynchronousEnd + 1, "recycled job object does not inherit old ending");
            WeaponPreparationDiagnostics.Ended(pawn, state, queuedEquip, JobCondition.Incompletable);
            WeaponPreparationDiagnostics.ResetForLoadedGame();
            int beforeLoaded = AomLog.Messages.Count;
            WeaponPreparationDiagnostics.Admitted(pawn, queuedEquip, queuedEquip);
            Check(AomLog.Messages.Count == beforeLoaded + 1 && AomLog.Messages.Last().Contains("admitted"),
                "new loaded game clears old admission diagnostics");
            state.Transition=ApparelTransition.Restoring;state.OriginalWeapon=fresh;state.WeaponRestorationRequested=true;
            var savedEquip=Equip(fresh,999);int beforeSaved=AomLog.Messages.Count;
            WeaponPreparationDiagnostics.Admitted(pawn,savedEquip,savedEquip);
            WeaponPreparationDiagnostics.Ended(pawn,state,savedEquip,JobCondition.Incompletable);
            Check(AomLog.Messages.Count==beforeSaved+2 && AomLog.Messages.Last().Contains("saved weapon Equip#999 ended Incompletable"),
                "saved Equip admission and failure are visible without counting work preparation");
            state.WeaponRestorationRequested=false;beforeSaved=AomLog.Messages.Count;
            WeaponPreparationDiagnostics.Admitted(pawn,savedEquip,savedEquip);
            Check(AomLog.Messages.Count==beforeSaved,"ended saved-weapon management has no diagnostic ownership");
            AomLog.DetailedEnabled = false;
            int messages = AomLog.Messages.Count;
            WeaponPreparationDiagnostics.Proposed(pawn, equip);
            Check(AomLog.Messages.Count == messages, "diagnostics respect detailed-log setting");

            state.LastManagedWorkJobDefName = "LayDown";
            var rest = new Job { def = JobDefOf.LayDown };
            Check(!PawnAutomaticOutfitStatus.IsManagedWorkStatusJob(rest, state), "compliant tracked resting is Active, not Working");
            Check(!PawnAutomaticOutfitStatus.IsManagedWorkStatusJob(rest, new PawnApparelState()), "prepared resting uses same Active classification");
            state.PendingWorkJob = rest;
            state.PendingWorkIsManagedWork = true;
            Check(!PawnAutomaticOutfitStatus.IsManagedWorkStatusJob(rest, state), "pending rest continuation remains personal activity");
            rest.playerForced = true;
            Check(!PawnAutomaticOutfitStatus.IsManagedWorkStatusJob(rest, state), "ordered rest is still resting");
            var meal = new Job { def = JobDefOf.Ingest };
            state.LastManagedWorkJobDefName = "Ingest";
            Check(!PawnAutomaticOutfitStatus.IsManagedWorkStatusJob(meal, state), "tracked meal is Active");
            var joy = new Job { def = new JobDef { defName = "PlayChess", joyKind = new object() } };
            state.LastManagedWorkJobDefName = "PlayChess";
            Check(!PawnAutomaticOutfitStatus.IsManagedWorkStatusJob(joy, state), "tracked recreation is Active");
            var repair = new Job { def = new JobDef { defName = "RepairElectric" } };
            state.LastManagedWorkJobDefName = "RepairElectric";
            Check(PawnAutomaticOutfitStatus.IsManagedWorkStatusJob(repair, state), "repair with missing work-giver keeps recovered Working label");
            var work = new Job { def = new JobDef { defName = "DoBill" }, workGiverDef = new WorkGiverDef() };
            Check(PawnAutomaticOutfitStatus.IsManagedWorkStatusJob(work, state), "native work-giver still labels work");
            WeaponPreparationRetryRegistry.ResetForLoadedGame();
            Check(!WeaponPreparationRetryRegistry.IsDeferred(pawn, first), "new/load game drops transient availability observations");
            Check(WeaponPreparationRetryRegistry.DeferUnchangedStock(pawn, requirement) == 2500, "load starts with a fresh bounded cycle");
            Console.WriteLine("Passed " + passed + " weapon preparation and activity checks.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}

namespace AutomaticOutfitManager.State
{
    public enum ApparelTransition { Preparing, Active, ReturningToChangingArea, Restoring }
    public class RejectedWeaponPreparation { public ThingWithComps Weapon; public string AvailabilitySignature; }
    public partial class PawnApparelState
    {
        public Pawn Pawn;
        public ThingWithComps OriginalWeapon;
        public List<ThingWithComps> ManagedWeapons = new List<ThingWithComps>();
        public List<RejectedWeaponPreparation> RejectedWeaponPreparations = new List<RejectedWeaponPreparation>();
        public bool WeaponInterventionActive, WeaponPlayerOverride, WeaponRuleOverrideExplicit, WeaponRestorationRequested, RecallRequested, PendingWorkIsManagedWork;
        public int RejectedWeaponRestorationAttempts, WeaponPreparationRetriesForCurrentCandidate, WeaponPreparationAttemptsThisTransition, WeaponPreparationRetriesThisTransition;
        public int LastWeaponPreparationAttemptTick = -1, LastWeaponPreparationThingId = -1, WeaponPreparationStartedTick = -1, RejectedWeaponPreparationThingId = -1, RejectedWeaponPreparationTick = -1;
        public ApparelTransition Transition;
        public Job PendingWorkJob;
        public List<string> CurrentRuleIds = new List<string>();
        public string LastManagedWorkJobDefName;
    }
}
namespace AutomaticOutfitManager.Core
{
    public class AutomaticOutfitManagerGameComponent
    {
        public static AutomaticOutfitManagerGameComponent Current;
        public Dictionary<Pawn, PawnApparelState> States = new Dictionary<Pawn, PawnApparelState>();
        public List<ApparelRule> Rules = new List<ApparelRule>();
        public PawnApparelState StateFor(Pawn p) => p != null && States.TryGetValue(p, out var state) ? state : null;
        public bool IsSavedWeaponForOtherPawn(ThingWithComps w, Pawn p) => w.SavedOther;
        public bool IsManagedWeaponAssignedToOtherPawn(ThingWithComps w, Pawn p) => w.ManagedOther;
        public void InvalidateWeaponStateIndex() { }
        public ApparelRule RuleById(string id) => Rules.FirstOrDefault(r => r.Id == id);
        public void RequestRecall(PawnApparelState state) { state.RecallRequested = true; state.Transition = ApparelTransition.ReturningToChangingArea; }
        public static void ClearPendingWork(PawnApparelState state) => state.PendingWorkJob = null;
        public static void ReleaseNativeReservations(Pawn pawn, Job job) { }
    }
    public static class AomLog { public static bool DetailedEnabled; public static List<string> Messages = new List<string>(); public static void Detailed(string message) => Messages.Add(message); }
}
namespace AutomaticOutfitManager.Rules { public class ApparelRule { public string Id, Name; public bool Enabled; public Area Area; public CombinedWeaponRequirement Requirement; } }
namespace AutomaticOutfitManager.Detection
{
    public class CombinedWeaponRequirement { public bool HasRequirement = true; public ThingDef Def; public bool Matches(ThingWithComps w) => w != null && w.def == Def; }
    public partial class WeaponFinder { public static bool CanUseWithoutTakingPlayerOwnership(ThingWithComps w, Pawn p) => true; }
    public static class RuleEvaluator { public static bool TryCombinedWeaponRequirement(IEnumerable<ApparelRule> rules, out CombinedWeaponRequirement r, Pawn p) { r = rules.FirstOrDefault()?.Requirement; return r != null; } }
    public static class UnavailableWorkRegistry { public static void Block(Pawn p, ApparelRule r, int ticks) { } }
    public static class ManagedWorkClaimRegistry { public static void ReleaseAll(Pawn p) { } }
}
namespace AutomaticOutfitManager.Patches
{
    public static class PreparationJobHandoff
    {
        public static HashSet<Job> Queued = new HashSet<Job>();
        public static bool IsQueued(Pawn pawn, Job job) => Queued.Contains(job);
        public static string QueueDescription(Pawn pawn) => "empty";
    }
    public static class ReservationUtility_SavedApparel_Patch { public static bool CanReserveForOutfit(Pawn p, Thing t) => t.Reservable; }
    public partial class PausedAreaWorkFilter { public static bool IsHaulingJob(Job j) => j?.def?.defName == "HaulToCell"; }
    public partial class PawnJobTracker_StartJob_Patch
    {
        private const int WeaponPreparationAttemptLimit = 6, WeaponPreparationTimeLimit = 1200, WeaponPreparationFailureCooldown = 1200;
        public static IEnumerable<ApparelRule> ProtectedRulesForJob(Pawn p, Job j) => AutomaticOutfitManagerGameComponent.Current.Rules;
        public static void ReplaceWithBriefWait(Pawn p, ref Job j, ref ThinkNode giver, ref JobTag? tag) { j = new Job { def = JobDefOf.Wait }; giver = null; tag = null; }
    }
}
namespace AutomaticOutfitManager.UI { public partial class PawnAutomaticOutfitStatus { public static bool IsMeaningfulActivity(Job job) => job?.def != null; } }
namespace Verse
{
    public class Thing
    {
        public int thingIDNumber; public bool Spawned = true, Destroyed, Reservable = true, Forbidden;
        public Map Map; public Map MapHeld => Map; public IntVec3 PositionHeld; public IntVec3 Position => PositionHeld;
        public object ParentHolder; public string LabelCap => "weapon"; public string LabelShortCap => "pawn";
        public string GetUniqueLoadID() => thingIDNumber.ToString(); public bool IsForbidden(Pawn p) => Forbidden;
    }
    public class ThingWithComps : Thing { public ThingDef def; public bool Equippable = true, SavedOther, ManagedOther; }
    public class ThingDef { public bool IsRangedWeapon, IsMeleeWeapon; }
    public class Pawn : Thing { public Equipment equipment = new Equipment(); }
    public class Equipment { public ThingWithComps Primary; }
    public struct IntVec3 { public int Value; public override string ToString() => Value.ToString(); }
    public class Map { public int uniqueID; public Lister listerThings = new Lister(); }
    public class Lister { public List<Thing> Items = new List<Thing>(); public List<Thing> ThingsInGroup(ThingRequestGroup g) => Items; }
    public class Area { public Map Map; public bool this[IntVec3 c] => true; }
    public static class Find { public static TickManager TickManager = new TickManager(); }
    public class TickManager { public int TicksGame; }
    public enum ThingRequestGroup { Weapon }
    public class ThingRequest { public static ThingRequest ForGroup(ThingRequestGroup g) => new ThingRequest(); }
    public class TraverseParms { public static TraverseParms For(Pawn p) => new TraverseParms(); }
    public static class GenClosest { public static Thing ClosestThingReachable(IntVec3 p, Map map, ThingRequest r, PathEndMode m, TraverseParms t, float distance, Predicate<Thing> validator) => map.listerThings.Items.FirstOrDefault(w => validator(w)); }
    public class JobDef { public string defName; public object joyKind; }
    public struct LocalTargetInfo { public Thing Thing; public LocalTargetInfo(Thing t) { Thing = t; } }
}
namespace Verse.AI
{
    public enum PathEndMode { ClosestTouch }
    public enum JobCondition { Succeeded, Incompletable, InterruptForced }
    public enum JobTag { Misc }
    public class ThinkNode { }
    public class Job { public JobDef def; public int loadID; public LocalTargetInfo targetA; public bool playerForced; public WorkGiverDef workGiverDef; public ThinkNode jobGiver; }
    public class Pawn_JobTracker { public int Cleared; public void ClearQueuedJobs(bool b) => Cleared++; }
}
namespace RimWorld
{
    public class WorkGiverDef { }
    public class JobGiver_Work : ThinkNode { }
    public static class EquipmentUtility { public static bool CanEquip(ThingWithComps w, Pawn p) => w.Equippable; }
    public static class JobDefOf
    {
        public static JobDef Equip = new JobDef { defName = "Equip" }, Wait = new JobDef { defName = "Wait" }, LayDown = new JobDef { defName = "LayDown" }, Ingest = new JobDef { defName = "Ingest" };
    }
}
