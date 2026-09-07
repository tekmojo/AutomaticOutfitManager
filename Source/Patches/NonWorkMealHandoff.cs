using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    internal static class NonWorkMealHandoff
    {
        private static AutomaticOutfitManagerGameComponent Component =>
            AutomaticOutfitManagerGameComponent.Current;
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        internal static NonWorkMealTrip For(Pawn pawn) =>
            Component?.NonWorkMealTrips.FirstOrDefault(trip => trip?.Pawn == pawn);

        private static bool NativeOverride(Pawn pawn, Job job, PawnApparelState state) =>
            pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Drafted ||
            pawn.InMentalState || PawnAccessClassifier.IsNativeCustodyEscapeActive(pawn) ||
            PawnJobTracker_StartJob_Patch.IsNativeEmergencySafetyJob(job) ||
            PawnJobTracker_StartJob_Patch.IsMapDepartureJob(job) ||
            (job?.playerForced == true &&
             !PawnPathFollower_ProtectedArea_Patch.IsManagedTransitionJob(pawn, job, state));

        private static bool HasMeal(NonWorkMealTrip trip) => trip.Meal != null &&
            !trip.Meal.Destroyed && trip.Meal.IngestibleNow &&
            (trip.Pawn.carryTracker?.CarriedThing == trip.Meal ||
             trip.Pawn.inventory?.innerContainer.Contains(trip.Meal) == true);

        private static void Remove(NonWorkMealTrip trip) => Component.NonWorkMealTrips.Remove(trip);

        // A pending meal must not override a newly revoked permission or an
        // exact access exit. Cancel only the detour: leave the meal, saved gear
        // and normal recall/restoration ownership intact.
        private static bool CancelDeniedTrip(NonWorkMealTrip trip, Job job)
        {
            var destination = Component.RuleById(trip.DestinationRuleId);
            bool denied = destination?.Enabled == true && destination.Area?.Map == trip.Pawn.Map &&
                !PausedAreaWorkFilter.WorkAllowedFor(destination, trip.Pawn);
            if (!denied && !AccessExitJobs.IsOwned(trip.Pawn, job)) return false;
            Remove(trip);
            Log(trip, "cancelled the dining detour because area access changed; keeping the meal for native activity");
            return true;
        }

        private static List<ApparelRule> Rules(Pawn pawn) =>
            RuleEvaluator.EnabledRulesForMap(pawn.Map).ToList();

        // Before reaching the source locker, only egress from the currently
        // occupied source is permitted. Once outside, it becomes restricted too.
        internal static List<ApparelRule> Restricted(Pawn pawn, NonWorkMealTrip trip)
        {
            bool canEnterDestination = trip.Stage == NonWorkMealStage.Eating && !trip.EatInPlace;
            return Rules(pawn).Where(rule =>
                !(canEnterDestination && rule.Id == trip.DestinationRuleId) &&
                !rule.Area[pawn.Position]).ToList();
        }

        private static List<ApparelRule> AfterLockerRestrictions(Pawn pawn, ApparelRule destination,
            bool dressed) => Rules(pawn).Where(rule => !dressed || rule.Id != destination.Id).ToList();

        private static bool Route(Pawn pawn, IntVec3 start, LocalTargetInfo end,
            List<ApparelRule> restricted, Predicate<IntVec3> unsafeCell = null) =>
            ProtectedPathAvoidance.SegmentAvoidsRules(pawn, start, end, restricted, unsafeCell);

        // Called from native StartPath after PickupIngestible has replaced
        // targetA with the exact carried (possibly split) meal. Never end or
        // replace a job inside a toil's initAction; commit on the component tick.
        internal static bool BeforePath(Pawn pawn, ref LocalTargetInfo destination, PathEndMode mode)
        {
            Job job = pawn?.CurJob;
            if (pawn?.Map == null || job == null) return true;
            var trip = For(pawn);
            var state = Component?.StateFor(pawn);
            if (NativeOverride(pawn, job, state))
            {
                if (trip != null) Remove(trip);
                return true;
            }
            if (trip != null && CancelDeniedTrip(trip, job)) return true;
            if (trip != null)
            {
                if (trip.Stage == NonWorkMealStage.Captured) return false;
                if (trip.Map != pawn.Map) { Remove(trip); return true; }
                if (!Route(pawn, pawn.Position, destination, Restricted(pawn, trip)))
                {
                    RecoverHere(trip, "the route changed or would enter another outfit area");
                    // The currently running native meal can simply chew here.
                    if (job.def == JobDefOf.Ingest && pawn.carryTracker?.CarriedThing == trip.Meal)
                    {
                        destination = pawn.Position;
                        trip.Stage = NonWorkMealStage.Eating;
                        trip.JobLoadId = job.loadID;
                        return true;
                    }
                    return false;
                }
                return true;
            }

            Thing meal = pawn.carryTracker?.CarriedThing;
            if (Component == null || job.def != JobDefOf.Ingest ||
                mode != PathEndMode.OnCell || destination.HasThing || meal == null ||
                job.targetA.Thing != meal || meal.def.ingestible == null ||
                meal.GetStatValue(StatDefOf.Nutrition) <= 0f ||
                meal.def.IsDrug || pawn.inventory == null ||
                !PawnAccessClassifier.IsApparelEligibleHuman(pawn) ||
                state?.Transition != ApparelTransition.Active || state.RecallRequested ||
                Component.RuleById(state.ActiveRuleId)?.IsNonWork != false)
                return true;

            IntVec3 dining = destination.Cell;
            if (!dining.IsValid || !dining.InBounds(pawn.Map)) return true;
            var destinations = Rules(pawn).Where(rule => rule.IsNonWork &&
                rule.Area[dining]).ToList();
            var target = destinations.FirstOrDefault(rule => RuleEvaluator.UsesSavedNonWorkOutfit(pawn, rule))
                ?? destinations.FirstOrDefault();
            if (target == null) return true;

            // Include an already compliant meal too: its late dining cell must
            // be visible to the path customizer even though it is not targetA.
            trip = new NonWorkMealTrip
            {
                Pawn = pawn, Map = pawn.Map, Meal = meal,
                Count = Math.Min(Math.Max(job.count, 1), meal.stackCount),
                SourceRuleId = state.ActiveRuleId, DestinationRuleId = target.Id,
                DiningCell = dining, StartedTick = Now, JobLoadId = job.loadID
            };
            if (!TryPlan(trip, state, target, out string reason))
            {
                // Keep the native meal and outfit; eat where food was picked
                // up instead of alternating return and failed dining paths.
                trip.EatInPlace = true;
                trip.Stage = NonWorkMealStage.Eating;
                destination = pawn.Position;
                Log(trip, "eating at the pickup location; " + reason);
            }
            else if (!RuleEvaluator.HasMissingRequiredGear(pawn, target) &&
                     !NonWorkOutfitPolicy.NeedsStateHandoff(pawn, target))
            {
                trip.Stage = NonWorkMealStage.Eating;
            }
            Component.NonWorkMealTrips.Add(trip);
            if (trip.Stage == NonWorkMealStage.Captured)
            {
                pawn.pather.StopDead();
                return false;
            }
            return true;
        }

        private static bool TryPlan(NonWorkMealTrip trip, PawnApparelState state,
            ApparelRule destination, out string reason)
        {
            Pawn pawn = trip.Pawn;
            reason = "the destination or its outfit is unavailable";
            var all = Rules(pawn);
            var atDestination = all.Where(rule => rule.Area[trip.DiningCell]).ToList();
            // A meal handoff cannot solve a genuinely overlapping Work Area.
            // Do not permit a second non-work target with different semantics.
            if (destination.WorkAreaPaused || atDestination.Count != 1 ||
                !PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(pawn, pawn.CurJob, destination) ||
                !RuleEvaluator.RuleCanApplyToPawn(pawn, destination) ||
                RuleEvaluator.SelectedNonWorkOutfitConflicts(pawn, atDestination) ||
                UnavailableWorkRegistry.HasActiveRuleBlock(pawn, destination)) return false;

            var source = Component.RuleById(trip.SourceRuleId);
            reason = "no safe source locker and outfit route is available";
            if (!PawnJobTracker_StartJob_Patch.TryFindSafeTransitionCell(pawn,
                source?.ChangingArea, all, out IntVec3 locker, state)) return false;
            // A configured source locker is authoritative; do not silently
            // choose the destination locker when that source is inaccessible.
            if (source?.ChangingArea != null && (source.ChangingArea.Map != pawn.Map ||
                !source.ChangingArea[locker])) return false;
            var before = all.Where(rule => !rule.Area[pawn.Position]).ToList();
            if (!Route(pawn, pawn.Position, locker, before)) return false;
            trip.LockerCell = locker;
            trip.UsesFallback = !RuleEvaluator.UsesSavedNonWorkOutfit(pawn, destination);
            trip.ReturnOutfit = Copy(NonWorkOutfitPolicy.Target(pawn, destination));
            trip.DestinationOutfit = Copy(trip.ReturnOutfit);
            if (trip.UsesFallback && !PlanFallback(trip, destination, source?.ChangingArea)) return false;
            var restricted = AfterLockerRestrictions(pawn, destination, false);
            var unsafeAfterRemoval = HazardousEnvironmentSafety.RemovalHazards(pawn, trip.ReturnOutfit.Apparel);
            if (unsafeAfterRemoval?.Invoke(locker) == true) return false;
            IntVec3 last = locker;
            foreach (var item in trip.ReturnOutfit.Apparel.Cast<ThingWithComps>()
                .Concat(new[] { trip.ReturnOutfit.Weapon })
                .Concat(trip.DestinationOutfit.Apparel).Concat(new[] { trip.DestinationOutfit.Weapon })
                .Where(item => item != null).Distinct())
            {
                if (!ItemAvailable(pawn, item)) return false;
                if (Held(pawn, item)) continue;
                IntVec3 cell = item.PositionHeld;
                if (restricted.Any(rule => rule.Area[cell]) ||
                    !Route(pawn, last, cell, restricted, unsafeAfterRemoval)) return false;
                last = cell;
            }
            return Route(pawn, last, trip.DiningCell,
                AfterLockerRestrictions(pawn, destination, true), unsafeAfterRemoval);
        }

        private static SavedNonWorkOutfit Copy(SavedNonWorkOutfit source) => new SavedNonWorkOutfit
        {
            Pawn = source.Pawn, Apparel = source.Apparel.ToList(), Weapon = source.Weapon
        };

        private static bool Held(Pawn pawn, Thing item) =>
            (item is Apparel apparel && pawn.apparel.WornApparel.Contains(apparel)) ||
            pawn.equipment?.Primary == item || pawn.inventory?.innerContainer.Contains(item) == true;

        private static bool ItemAvailable(Pawn pawn, ThingWithComps item)
        {
            if (item.Destroyed || !EquipmentUtility.CanEquip(item, pawn)) return false;
            if (item is Apparel apparel && (Component.IsSavedForOtherPawn(apparel, pawn) ||
                Component.IsManagedApparelAssignedToOtherPawn(apparel, pawn))) return false;
            if (item.def.IsWeapon && (Component.IsSavedWeaponForOtherPawn(item, pawn) ||
                Component.IsManagedWeaponAssignedToOtherPawn(item, pawn))) return false;
            if (Held(pawn, item)) return true;
            // Exact saved items inside ordinary containers are released by the
            // existing restorer; never plan a route into another pawn's gear.
            for (IThingHolder holder = item.ParentHolder; holder != null; holder = holder.ParentHolder)
                if (holder is Pawn other && other != pawn) return false;
            return item.MapHeld == pawn.Map && item.PositionHeld.IsValid &&
                item.PositionHeld.InBounds(pawn.Map) &&
                (!item.Spawned || pawn.CanReserve(item));
        }

        private static bool PlanFallback(NonWorkMealTrip trip, ApparelRule destination, Area sourceLocker)
        {
            var pawn = trip.Pawn;
            var target = trip.DestinationOutfit;
            var restricted = AfterLockerRestrictions(pawn, destination, false);
            bool Accessible(Thing item) => item.PositionHeld.IsValid &&
                !restricted.Any(rule => rule.Area[item.PositionHeld]) &&
                Route(pawn, trip.LockerCell, item.PositionHeld, restricted);
            foreach (var def in destination.RequiredApparel.Where(def => def != null).Distinct())
            {
                if (target.Apparel.Any(item => item.def == def && destination.Allows(item))) continue;
                var candidate = ApparelFinder.FindBest(pawn, def, sourceLocker, null,
                    new[] { destination }, item => Accessible(item));
                if (candidate == null) return false;
                var incompatible = target.Apparel.Where(item => !ApparelUtility.CanWearTogether(
                    candidate.def, item.def, pawn.RaceProps.body)).ToList();
                if (incompatible.Any(item => NonWorkOutfitPolicy.Keep(pawn, destination, item) ||
                    destination.RequiredApparel.Contains(item.def))) return false;
                target.Apparel.RemoveAll(incompatible.Contains);
                target.Apparel.Add(candidate);
            }
            if (Component.StateFor(pawn)?.WeaponRuleOverrideExplicit != true && destination.HasWeaponRequirement)
            {
                if (!RuleEvaluator.TryCombinedWeaponRequirement(new[] { destination }, out var required, pawn)) return false;
                if (!required.Matches(target.Weapon))
                {
                    if (NonWorkOutfitPolicy.Keep(pawn, destination, target.Weapon)) return false;
                    target.Weapon = WeaponFinder.FindBest(pawn, required, sourceLocker, null,
                        item => Accessible(item));
                    if (target.Weapon == null) return false;
                }
            }
            return true;
        }

        internal static void Tick(AutomaticOutfitManagerGameComponent component)
        {
            if (component.NonWorkMealTrips.Count == 0) return;
            foreach (var trip in component.NonWorkMealTrips.ToList())
            {
                Pawn pawn = trip.Pawn;
                var state = component.StateFor(pawn);
                if (pawn == null || trip.Map != pawn.Map || !HasMeal(trip) ||
                    NativeOverride(pawn, pawn.CurJob, state)) { Remove(trip); continue; }
                if (CancelDeniedTrip(trip, pawn.CurJob)) continue;
                if (trip.Stage != NonWorkMealStage.Eating && !trip.EatInPlace &&
                    (trip.ReturnOutfit == null || trip.DestinationOutfit == null))
                { Remove(trip); continue; }
                if (trip.Stage == NonWorkMealStage.Eating)
                {
                    if (pawn.CurJob?.loadID != trip.JobLoadId) Remove(trip);
                    continue;
                }
                var destination = component.RuleById(trip.DestinationRuleId);
                if (destination?.Enabled != true || destination.Area?.Map != pawn.Map ||
                    destination.WorkAreaPaused || !destination.Area[trip.DiningCell] ||
                    Now - trip.StartedTick > 5000)
                    RecoverHere(trip, "the dining rule changed or the changing trip timed out");
                if (trip.Stage == NonWorkMealStage.Captured && !trip.EatInPlace)
                {
                    if (pawn.CurJob?.loadID != trip.JobLoadId ||
                        state?.Transition != ApparelTransition.Active || state.ActiveRuleId != trip.SourceRuleId ||
                        !TryPlan(trip, state, destination, out string _))
                    {
                        RecoverHere(trip, "the meal or outfit plan changed before departure");
                    }
                    else if (StowMeal(trip))
                    {
                        trip.Stage = NonWorkMealStage.Returning;
                        component.BeginNonWorkRestoration(pawn, destination,
                            trip.ReturnOutfit, trip.UsesFallback);
                        state = component.StateFor(pawn);
                        if (state != null) state.RecallInterruptPending = false;
                        ProtectedBoundaryRetryRegistry.Clear(pawn);
                        PreparedIngestRetryRegistry.Clear(pawn);
                        pawn.jobs.ClearQueuedJobs(false);
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, false, true);
                        Log(trip, "carrying the meal to the source locker before the non-work outfit change");
                    }
                    else RecoverHere(trip, "the meal could not be safely stowed");
                }
                if (trip.EatInPlace)
                {
                    if (!StowMeal(trip)) { Remove(trip); continue; }
                    pawn.jobs.ClearQueuedJobs(false);
                    Job eat = MakeMeal(trip);
                    pawn.jobs.StartJob(eat, JobCondition.InterruptForced, null, false, true);
                }
            }
        }

        private static bool StowMeal(NonWorkMealTrip trip)
        {
            if (trip.Pawn.inventory.innerContainer.Contains(trip.Meal)) return true;
            if (trip.Pawn.carryTracker.CarriedThing != trip.Meal) return false;
            return trip.Pawn.carryTracker.innerContainer.TryTransferToContainer(
                trip.Meal, trip.Pawn.inventory.innerContainer, false) &&
                trip.Pawn.inventory.innerContainer.Contains(trip.Meal);
        }

        // Return true only when this controller has fully validated and owns
        // the replacement job. Returning false lets normal restoration run.
        internal static bool BeforeJob(Pawn pawn, ref Job job)
        {
            var trip = For(pawn);
            if (trip == null) return false;
            var state = Component.StateFor(pawn);
            if (trip.Map != pawn.Map || !HasMeal(trip) || NativeOverride(pawn, job, state))
            { Remove(trip); return false; }
            if (CancelDeniedTrip(trip, job)) return false;
            if (trip.Stage == NonWorkMealStage.Eating)
            {
                if (job.loadID == trip.JobLoadId) return true;
                Remove(trip);
                return false;
            }
            if (trip.Stage == NonWorkMealStage.Captured) return false;
            if (trip.Stage == NonWorkMealStage.PreparingFallback && state?.RecallRequested == true)
            { Remove(trip); return false; }
            if (trip.EatInPlace) { Replace(pawn, ref job, MakeMeal(trip)); return true; }
            if (trip.Stage == NonWorkMealStage.Returning && state != null)
            {
                // Recheck immediately before removal, including rule repainting
                // and moved/claimed items since the preflight at the kitchen.
                if (pawn.Position == trip.LockerCell &&
                    (Rules(pawn).Any(rule => rule.Area[pawn.Position]) ||
                     trip.ReturnOutfit.Apparel.Cast<ThingWithComps>()
                        .Concat(new[] { trip.ReturnOutfit.Weapon })
                        .Any(item => item != null && !ItemAvailable(pawn, item))))
                {
                    RecoverHere(trip, "the source locker or saved gear changed before removal");
                    Replace(pawn, ref job, Wait(pawn));
                    return true;
                }
                if (!PawnPathFollower_ProtectedArea_Patch.IsManagedTransitionJob(pawn, job, state))
                    Replace(pawn, ref job, Wait(pawn));
                return false;
            }
            var destination = Component.RuleById(trip.DestinationRuleId);
            if (destination?.Enabled != true || destination.Area?.Map != pawn.Map || destination.WorkAreaPaused)
            { RecoverHere(trip, "the dining rule is no longer active"); Replace(pawn, ref job, Wait(pawn)); return true; }

            if (trip.UsesFallback && trip.Stage == NonWorkMealStage.Returning)
            {
                if (!trip.ReturnOutfit.ApparelSatisfied(pawn) || !trip.ReturnOutfit.WeaponSatisfied(pawn))
                { RecoverHere(trip, "the source outfit return could not finish"); Replace(pawn, ref job, Wait(pawn)); return true; }
                var apparel = trip.DestinationOutfit.Apparel.Except(pawn.apparel.WornApparel).ToList();
                var weapon = pawn.equipment?.Primary == trip.DestinationOutfit.Weapon
                    ? null : trip.DestinationOutfit.Weapon;
                if (apparel.Count > 0 || weapon != null)
                {
                    state = Component.BeginIntervention(pawn, destination, apparel, weapon, true);
                    state.CurrentRuleIds = new List<string> { destination.Id };
                    trip.Stage = NonWorkMealStage.PreparingFallback;
                }
            }
            if (trip.Stage == NonWorkMealStage.PreparingFallback)
            {
                if (state == null) { RecoverHere(trip, "fallback preparation was interrupted"); Replace(pawn, ref job, Wait(pawn)); return true; }
                Job next = null;
                var item = trip.DestinationOutfit.Apparel.FirstOrDefault(a => !pawn.apparel.WornApparel.Contains(a));
                if (item != null && ItemAvailable(pawn, item)) next = JobMaker.MakeJob(JobDefOf.Wear, item);
                else if (item == null && pawn.equipment?.Primary != trip.DestinationOutfit.Weapon &&
                    trip.DestinationOutfit.Weapon != null && ItemAvailable(pawn, trip.DestinationOutfit.Weapon))
                    next = JobMaker.MakeJob(JobDefOf.Equip, trip.DestinationOutfit.Weapon);
                if (next != null && Route(pawn, pawn.Position, next.targetA, Restricted(pawn, trip)))
                {
                    next.playerForced = true;
                    Replace(pawn, ref job, next);
                    return true;
                }
            }
            if (RuleEvaluator.HasMissingRequiredGear(pawn, destination) ||
                !Route(pawn, pawn.Position, trip.DiningCell, AfterLockerRestrictions(pawn, destination, true)))
            {
                RecoverHere(trip, "the complete outfit or final dining route is unavailable");
                Replace(pawn, ref job, Wait(pawn));
                return true;
            }
            if (state != null && trip.Stage == NonWorkMealStage.PreparingFallback)
                state.Transition = ApparelTransition.Active;
            Replace(pawn, ref job, MakeMeal(trip));
            Log(trip, "resuming the same meal in the non-work area; new Work Area entries are blocked");
            return true;
        }

        private static void Replace(Pawn pawn, ref Job job, Job replacement)
        {
            AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(pawn, job);
            job = replacement;
        }

        private static Job Wait(Pawn pawn) => PawnJobTracker_StartJob_Patch.MakeSafeWaitJob(pawn, 30);

        private static Job MakeMeal(NonWorkMealTrip trip)
        {
            Job job = JobMaker.MakeJob(JobDefOf.Ingest, trip.Meal);
            job.count = Math.Min(trip.Count, trip.Meal.stackCount);
            trip.Stage = NonWorkMealStage.Eating;
            trip.JobLoadId = job.loadID;
            return job;
        }

        internal static void RecoverHere(NonWorkMealTrip trip, string reason)
        {
            if (trip.EatInPlace) return;
            trip.EatInPlace = true;
            trip.Stage = NonWorkMealStage.Returning;
            trip.Pawn.pather.StopDead();
            Log(trip, "ending the dining detour and keeping the meal for eating here; " + reason);
        }

        internal static bool BlockEntry(Pawn pawn, IntVec3 next)
        {
            var trip = For(pawn);
            if (trip == null || trip.Stage == NonWorkMealStage.Captured) return false;
            // This runs for each walked cell; avoid constructing a route/list.
            foreach (var rule in RuleEvaluator.EnabledRulesForMap(pawn.Map))
            {
                if (!rule.Area[next] || rule.Area[pawn.Position]) continue;
                if (trip.Stage == NonWorkMealStage.Eating && !trip.EatInPlace &&
                    rule.Id == trip.DestinationRuleId && !rule.WorkAreaPaused &&
                    PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(pawn, pawn.CurJob, rule) &&
                    !RuleEvaluator.HasMissingRequiredGear(pawn, rule)) continue;
                RecoverHere(trip, "the next path cell requires a new outfit area entry");
                return true;
            }
            return false;
        }

        internal static bool GuardMealFromHaul(Pawn pawn, Job proposed)
        {
            var trip = For(pawn);
            if (trip == null || proposed == null || !HasMeal(trip) ||
                NativeOverride(pawn, proposed, Component.StateFor(pawn))) return false;
            if (CancelDeniedTrip(trip, proposed)) return false;
            return trip.Stage == NonWorkMealStage.Eating &&
                pawn.CurJob?.loadID == trip.JobLoadId &&
                proposed.loadID != trip.JobLoadId && PausedAreaWorkFilter.IsHaulingJob(proposed);
        }

        internal static void NotifyEnded(Pawn pawn, Job job)
        {
            var trip = For(pawn);
            if (trip?.Stage == NonWorkMealStage.Eating && trip.JobLoadId == job?.loadID)
                Remove(trip);
        }

        internal static bool DiningSpot(Pawn pawn, Thing meal, ref IntVec3 cell, ref bool result)
        {
            var trip = For(pawn);
            if (trip?.Stage != NonWorkMealStage.Eating || meal != trip.Meal ||
                pawn.CurJob?.loadID != trip.JobLoadId) return false;
            if (CancelDeniedTrip(trip, pawn.CurJob)) return false;
            cell = trip.EatInPlace ? pawn.Position : trip.DiningCell;
            if (!cell.IsValid || !cell.InBounds(pawn.Map) || !pawn.CanReserveSittableOrSpot(cell) ||
                !Route(pawn, pawn.Position, cell, Restricted(pawn, trip)))
            {
                RecoverHere(trip, "the saved dining spot is occupied or unreachable");
                trip.Stage = NonWorkMealStage.Eating;
                cell = pawn.Position;
            }
            result = true;
            return true;
        }

        private static void Log(NonWorkMealTrip trip, string message)
        {
            if (AomLog.DetailedEnabled)
                AomLog.Detailed($"[AutomaticOutfitManager] {trip.Pawn.LabelShortCap}: meal handoff: {message}.");
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    internal static class PawnJobTracker_NonWorkMealGuard_Patch
    {
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Pawn_JobTracker __instance, Job newJob) =>
            !NonWorkMealHandoff.GuardMealFromHaul(PawnField(__instance), newJob);
    }

    [HarmonyPatch(typeof(Toils_Misc), nameof(Toils_Misc.TakeItemFromInventoryToCarrier))]
    internal static class ToilsMisc_NonWorkMealSplit_Patch
    {
        private static void Postfix(Pawn pawn, ref Toil __result)
        {
            var original = __result.initAction;
            __result.initAction = () =>
            {
                var trip = NonWorkMealHandoff.For(pawn);
                bool owns = trip?.Stage == NonWorkMealStage.Eating &&
                    pawn.CurJob?.loadID == trip.JobLoadId && pawn.CurJob.targetA.Thing == trip.Meal;
                original?.Invoke();
                // Native transfer may split the exact inventory stack. Adopt
                // only the carried result of that same owned native operation.
                if (owns && pawn.CurJob?.loadID == trip.JobLoadId &&
                    pawn.carryTracker.CarriedThing != null &&
                    pawn.CurJob.targetA.Thing == pawn.carryTracker.CarriedThing)
                    trip.Meal = pawn.carryTracker.CarriedThing;
            };
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.StartPath))]
    internal static class PawnPathFollower_NonWorkMeal_Patch
    {
        private static readonly AccessTools.FieldRef<Pawn_PathFollower, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_PathFollower, Pawn>("pawn");
        private static bool Prefix(Pawn_PathFollower __instance, ref LocalTargetInfo dest, PathEndMode peMode) =>
            NonWorkMealHandoff.BeforePath(PawnField(__instance), ref dest, peMode);
    }

    [HarmonyPatch(typeof(Toils_Ingest), "TryFindChairOrSpot")]
    internal static class ToilsIngest_NonWorkMeal_Patch
    {
        private static bool Prefix(Pawn pawn, Thing ingestible, ref IntVec3 cell, ref bool __result) =>
            !NonWorkMealHandoff.DiningSpot(pawn, ingestible, ref cell, ref __result);
    }
}
