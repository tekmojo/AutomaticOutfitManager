using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AutomaticOutfitManager.Patches
{
    // Observe participant readiness; never assign ritual duties, waive access,
    // reserve stock, or resurrect a participant that native control has removed.
    internal static class RitualOutfitPreparation
    {
        internal sealed class Gate
        {
            public bool Released;
            internal int CheckedTick = -1;
            internal bool Holding;
            internal string WaitingNames;
        }

        private static readonly ConditionalWeakTable<LordJob_Ritual, Gate> Gates =
            new ConditionalWeakTable<LordJob_Ritual, Gate>();

        internal static Gate StateFor(LordJob_Ritual ritual) => Gates.GetValue(ritual, key => new Gate());

        internal static bool IsHolding(LordJob_Ritual ritual)
        {
            if (ritual?.lord == null || ritual.cancelled || ritual.Ritual == null)
                return false;
            Gate gate = StateFor(ritual);
            if (gate.Released) return false;
            // An existing ceremony loaded after its start is never paused again.
            if (ritual.StageIndex > 0 || ritual.TicksPassedWithProgress > 0f)
            {
                gate.Released = true;
                return false;
            }
            if (ritual.StageIndex != 0)
                return false;
            int tick = Find.TickManager.TicksGame;
            if (gate.CheckedTick >= 0 && tick >= gate.CheckedTick && tick - gate.CheckedTick < 30)
                return gate.Holding;
            gate.CheckedTick = tick;
            var waiting = new List<string>();
            foreach (Pawn pawn in ritual.lord.ownedPawns
                .Concat(ritual.assignments?.Participants ?? new List<Pawn>()).Distinct())
                if (WaitsForPawn(ritual, pawn)) waiting.Add(pawn.LabelShortCap);
            gate.Holding = waiting.Count > 0;
            string names = string.Join(", ", waiting);
            if (gate.WaitingNames != names && AomLog.DetailedEnabled)
                AomLog.Detailed(gate.Holding
                    ? $"[AutomaticOutfitManager] {ritual.RitualLabel}: waiting for participant outfits: {names}."
                    : $"[AutomaticOutfitManager] {ritual.RitualLabel}: outfit preparation hold released; native ceremony continues.");
            gate.WaitingNames = names;
            // A clear scan is not a permanent release: native gathering may still
            // be assigning duties. Latch only once the ceremony actually advances.
            return gate.Holding;
        }

        private static bool Eligible(LordJob_Ritual ritual, Pawn pawn) =>
            ritual?.lord != null && !ritual.cancelled && pawn?.Spawned == true &&
            pawn.Map == ritual.Map && !NativeRuleControl.Suspends(pawn, pawn.CurJob) &&
            (pawn.GetLord() == ritual.lord || pawn.GetLord() == null) &&
            ritual.assignments?.PawnParticipating(pawn) == true &&
            ritual.VoluntaryJoinPriorityFor(pawn) > 0f &&
            (pawn.CurJob?.playerForced != true ||
             PawnPathFollower_ProtectedArea_Patch.IsManagedTransitionJob(pawn, pawn.CurJob,
                 AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn)));

        private static List<ApparelRule> CeremonyRules(LordJob_Ritual ritual, Pawn pawn) =>
            RuleEvaluator.EnabledRulesForMap(pawn.Map).Where(rule =>
                Contains(rule.Area, ritual.selectedTarget.Cell) ||
                Contains(rule.Area, pawn.mindState?.duty?.focus.Cell ?? IntVec3.Invalid) ||
                Contains(rule.Area, pawn.mindState?.duty?.focusSecond.Cell ?? IntVec3.Invalid)).ToList();

        private static bool Allowed(Pawn pawn, List<ApparelRule> rules) =>
            rules.Count > 0 && rules.All(rule =>
                PausedAreaWorkFilter.WorkAllowedFor(rule, pawn) && !rule.WorkAreaPaused &&
                !ChildAreaAccessPolicy.Disallows(pawn, rule) && RuleEvaluator.RuleCanApplyToPawn(pawn, rule));

        // Called only before the ordinary idle watchdog, never to override recall,
        // emergency control, or a real native job. Ready gathering is useful waiting.
        internal static bool RetainReadyOutfit(Pawn pawn, PawnApparelState state)
        {
            var ritual = pawn?.GetLord()?.LordJob as LordJob_Ritual;
            if (state == null || state.Transition != ApparelTransition.Active ||
                state.RecallRequested || state.MapDepartureRequested || state.NativeControlSuspended ||
                state.DraftedTransitionSuspended || state.DownedTransitionSuspended ||
                ritual == null || ritual.StageIndex != 0 || ritual.TicksPassedWithProgress > 0f ||
                !Eligible(ritual, pawn)) return false;
            var rules = CeremonyRules(ritual, pawn);
            return Allowed(pawn, rules) && rules.Any(rule => rule.Id == state.ActiveRuleId ||
                state.CurrentRuleIds?.Contains(rule.Id) == true) &&
                !rules.Any(rule => RuleEvaluator.HasMissingRequiredGear(pawn, rule));
        }

        internal static bool WaitsForPawn(LordJob_Ritual ritual, Pawn pawn)
        {
            if (!Eligible(ritual, pawn)) return false;
            var component = AutomaticOutfitManagerGameComponent.Current;
            PawnApparelState state = component?.StateFor(pawn);
            if (state != null && (state.NativeControlSuspended || state.DownedTransitionSuspended ||
                state.DraftedTransitionSuspended || state.MapDepartureRequested ||
                (state.RecallRequested && string.IsNullOrEmpty(state.NonWorkRestorationRuleId)))) return false;
            var rules = CeremonyRules(ritual, pawn);
            if (!Allowed(pawn, rules) || rules.All(rule =>
                ChildAreaAccessPolicy.BypassesAdultRules(pawn, rule))) return false;
            Job pending = state?.PendingWorkJob;
            if (pending != null &&
                (PausedAreaWorkFilter.DeniedActivityRule(pawn, pending) != null ||
                 PausedAreaWorkFilter.DeniedHaulingRule(pawn, pending) != null ||
                 PausedAreaWorkFilter.DeniedPausedAreaRule(pawn, pending) != null ||
                 PausedAreaWorkFilter.ShouldRejectWanderingJob(pawn, pending))) return false;

            bool needsOutfit = rules.Any(rule => RuleEvaluator.HasMissingRequiredGear(pawn, rule));
            bool transitioning = state != null && state.Transition != ApparelTransition.Active;
            if (!needsOutfit && !transitioning) return false;
            if (!FeasibleOutfit(pawn, rules)) return false;
            // Include assigned participants before StartJob creates an AOM state.
            // This probe neither reserves stock nor starts/duplicates native jobs.
            if (!transitioning) return CanReachCeremony(ritual, pawn, rules);

            var transitionRules = rules.Concat((state.CurrentRuleIds ?? new List<string>())
                .Concat(new[] { state.ActiveRuleId }).Select(component.RuleById)
                .Where(rule => rule?.Enabled == true && rule.Area?.Map == pawn.Map)).Distinct().ToList();
            bool hasStep = false;
            foreach (Job job in TransitionJobs(pawn))
            {
                if (!PawnPathFollower_ProtectedArea_Patch.IsManagedTransitionJob(pawn, job, state))
                    continue;
                if (job.def == JobDefOf.Wear || job.def == JobDefOf.Equip)
                {
                    if (!ReachableGear(pawn, job.targetA.Thing, transitionRules)) return false;
                    hasStep = true;
                }
                else if (job.def == JobDefOf.Goto && job.targetA.IsValid)
                {
                    var restricted = RuleEvaluator.EnabledRulesForMap(pawn.Map).Where(rule =>
                        !PawnPathFollower_ProtectedArea_Patch.ManagedTransitionMayEnterRule(pawn, job, state, rule)).ToList();
                    if (!pawn.CanReach(job.targetA, PathEndMode.OnCell, Danger.Some) ||
                        !ProtectedPathAvoidance.SegmentAvoidsRules(pawn, pawn.Position, job.targetA,
                            restricted, exactEndMode: PathEndMode.OnCell)) return false;
                    hasStep = true;
                }
                else if (job.def == JobDefOf.RemoveApparel || job.def == JobDefOf.HaulToCell ||
                         job.def == JobDefOf.HaulToContainer)
                    hasStep = true;
            }
            if (!hasStep && !needsOutfit) return false;
            if (!hasStep && !CanReachCeremony(ritual, pawn, rules)) return false;

            // A live return job alone is insufficient if its eventual outfit has
            // become unavailable. Check the exact selected/saved targets without
            // invoking a planner, consuming reservations, or finding replacements.
            IEnumerable<Thing> gear = state.Transition == ApparelTransition.Preparing
                ? (state.ManagedApparel ?? new List<Apparel>()).Cast<Thing>()
                    .Concat(state.ManagedWeapons ?? new List<ThingWithComps>())
                : (state.OriginalApparel ?? new List<Apparel>()).Cast<Thing>()
                    .Concat(state.WeaponRestorationRequested && state.OriginalWeapon != null
                        ? new[] { state.OriginalWeapon } : Enumerable.Empty<ThingWithComps>());
            return gear.All(item => HeldByPawn(pawn, item) || ReachableGear(pawn, item, transitionRules));
        }

        private static bool CanReachCeremony(LordJob_Ritual ritual, Pawn pawn, List<ApparelRule> rules)
        {
            LocalTargetInfo target = pawn.mindState?.duty?.focus ?? LocalTargetInfo.Invalid;
            if (!target.IsValid) target = new LocalTargetInfo(ritual.selectedTarget.Cell);
            var restricted = RuleEvaluator.EnabledRulesForMap(pawn.Map).Where(rule =>
                !rules.Contains(rule) && !Contains(rule.Area, pawn.Position)).ToList();
            return target.IsValid && pawn.CanReach(target, PathEndMode.Touch, Danger.Some) &&
                ProtectedPathAvoidance.SegmentAvoidsRules(pawn, pawn.Position, target,
                    restricted, exactEndMode: PathEndMode.Touch);
        }

        private static bool FeasibleOutfit(Pawn pawn, List<ApparelRule> rules)
        {
            if (ApparelCompatibility.FindConflict(rules, pawn.RaceProps?.body, pawn) != null ||
                RuleEvaluator.SelectedNonWorkOutfitConflicts(pawn, rules) ||
                RuleEvaluator.SavedNonWorkOutfitConflicts(pawn, rules) ||
                !RuleEvaluator.TryCombinedWeaponRequirement(rules, out var weapon, pawn)) return false;
            foreach (var rule in rules.Where(rule => RuleEvaluator.UsesSavedNonWorkOutfit(pawn, rule)))
            {
                var saved = NonWorkOutfitPolicy.Target(pawn, rule);
                if (saved.Apparel.Any(item => item != null && !item.Destroyed &&
                    !HeldByPawn(pawn, item) && !ReachableGear(pawn, item, rules))) return false;
                if (saved.Weapon != null && !saved.Weapon.Destroyed &&
                    !HeldByPawn(pawn, saved.Weapon) && !ReachableGear(pawn, saved.Weapon, rules)) return false;
            }
            foreach (var group in rules.SelectMany(rule => RuleEvaluator.RequiredApparelFor(pawn, rule)
                .Where(def => def != null).Select(def => new { Def = def, Rule = rule })).GroupBy(entry => entry.Def))
            {
                var standards = group.Select(entry => entry.Rule).ToList();
                if (pawn.apparel.WornApparel.Any(item => item.def == group.Key && standards.All(rule => rule.Allows(item))))
                    continue;
                if (ApparelFinder.FindBest(pawn, group.Key, standards[0].ChangingArea,
                    standards: standards, candidateAllowed: item => ReachableGear(pawn, item, rules)) == null) return false;
            }
            return AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn)?.WeaponRuleOverrideExplicit == true ||
                weapon.Matches(pawn.equipment?.Primary) ||
                WeaponFinder.FindBest(pawn, weapon, rules.FirstOrDefault(rule => rule.HasWeaponRequirement)?.ChangingArea,
                    candidateAllowed: item => ReachableGear(pawn, item, rules)) != null;
        }

        private static IEnumerable<Job> TransitionJobs(Pawn pawn)
        {
            if (pawn.CurJob != null) yield return pawn.CurJob;
            if (pawn.jobs?.jobQueue != null)
                for (int i = 0; i < pawn.jobs.jobQueue.Count; i++)
                    yield return pawn.jobs.jobQueue[i].job;
        }

        private static bool HeldByPawn(Pawn pawn, Thing item) =>
            (item is Apparel apparel && pawn.apparel?.WornApparel.Contains(apparel) == true) ||
            pawn.equipment?.Primary == item;

        private static bool ReachableGear(Pawn pawn, Thing item, List<ApparelRule> rules) =>
            item != null && !item.Destroyed && item.Spawned && item.Map == pawn.Map &&
            !item.IsForbidden(pawn) && !item.IsBurning() && EquipmentUtility.CanEquip(item, pawn) &&
            ReservationUtility_SavedApparel_Patch.CanReserveForOutfit(pawn, item) &&
            pawn.CanReach(item, GearRetrievalRoute.EndMode(item), Danger.Some) &&
            GearRetrievalRoute.CanReach(pawn, item, rules);

        private static bool Contains(Area area, IntVec3 cell) =>
            area?.Map != null && cell.IsValid && cell.InBounds(area.Map) && area[cell];

        // Reading UI never opens the gate before native duties have been issued.
        internal static string Report(LordJob_Ritual ritual) =>
            ritual != null && Gates.TryGetValue(ritual, out Gate gate) && gate.Holding && !gate.Released
                ? $"Waiting for outfits: {gate.WaitingNames}." : null;
    }

    // Let the native spectator search choose another seat while its first choice
    // is held for a dressing participant. A late null-job filter cannot do that.
    [HarmonyPatch(typeof(ReservationUtility), nameof(ReservationUtility.CanReserveSittableOrSpot),
        new[] { typeof(Pawn), typeof(IntVec3), typeof(bool) })]
    internal static class RitualSpectatorSeat_Patch
    {
        internal static void Postfix(Pawn __0, IntVec3 __1, bool __2, ref bool __result)
        {
            if (!__result || __2 || !(__0?.GetLord()?.LordJob is LordJob_Ritual)) return;
            Building building = __1.GetEdifice(__0.Map);
            Thing seat = building?.def?.building?.multiSittable != true &&
                building?.def?.building?.isSittable == true ? building : null;
            if (ManagedWorkClaimRegistry.IsClaimedByOther(__0, __0.Map, seat, __1)) __result = false;
        }
    }

    // Only successful stage-end triggers receive this filter. Native cancellation,
    // failure, pawn-loss and emergency triggers are left untouched, including for
    // conversion prisoners whose delivery remains a native duty.
    internal sealed class RitualOutfitReadyFilter : TriggerFilter
    {
        public override bool AllowActivation(Lord lord, TriggerSignal signal) =>
            !RitualOutfitPreparation.IsHolding(lord?.LordJob as LordJob_Ritual);
    }

    [HarmonyPatch]
    internal static class RitualStageEnd_OutfitPreparation_Patch
    {
        internal static IEnumerable<MethodBase> TargetMethods() =>
            AccessTools.GetTypesFromAssembly(typeof(StageEndTrigger).Assembly)
                .Where(type => typeof(StageEndTrigger).IsAssignableFrom(type))
                .Select(type => AccessTools.DeclaredMethod(type, "MakeTrigger"))
                .Where(method => method != null && !method.IsAbstract);

        internal static void Postfix(Trigger __result)
        {
            if (__result == null) return;
            if (__result.filters == null) __result.filters = new List<TriggerFilter>();
            __result.filters.Add(new RitualOutfitReadyFilter());
        }
    }

    [HarmonyPatch(typeof(LordJob_Ritual), nameof(LordJob_Ritual.LordJobTick))]
    internal static class RitualTick_OutfitPreparation_Patch
    {
        // Keep stage timers, timed effects and outcome-worker ticks at the start.
        // Lord and pawn ticks still run, including all native abort transitions.
        internal static bool Prefix(LordJob_Ritual __instance) =>
            !RitualOutfitPreparation.IsHolding(__instance);
    }

    [HarmonyPatch(typeof(LordToil_Ritual), nameof(LordToil_Ritual.LordToilTick))]
    internal static class RitualToilTick_OutfitPreparation_Patch
    {
        // Dressing time earns no attendance credit and no ritual progress.
        internal static bool Prefix(LordToil_Ritual __instance) =>
            !RitualOutfitPreparation.IsHolding(__instance.ritual);
    }

    [HarmonyPatch(typeof(LordJob_Ritual), nameof(LordJob_Ritual.GetReport))]
    internal static class RitualReport_OutfitPreparation_Patch
    {
        internal static void Postfix(LordJob_Ritual __instance, ref string __result)
        {
            string report = RitualOutfitPreparation.Report(__instance);
            if (report != null) __result = report;
        }
    }

    [HarmonyPatch(typeof(LordJob_Ritual), nameof(LordJob_Ritual.ExposeData))]
    internal static class RitualSave_OutfitPreparation_Patch
    {
        internal static void Postfix(LordJob_Ritual __instance)
        {
            var state = RitualOutfitPreparation.StateFor(__instance);
            Scribe_Values.Look(ref state.Released, "aomOutfitPreparationReleased", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) state.CheckedTick = -1;
        }
    }
}
