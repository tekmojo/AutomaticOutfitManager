using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.Storage;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Patches
{
    /// <summary>
    /// Rejects paused-area work while RimWorld is still scanning work givers.
    /// This is deliberately earlier than Pawn_JobTracker.StartJob: replacing an
    /// already-selected job with Wait makes the thinker select the same job over
    /// and over, leaving the pawn standing indefinitely.
    /// </summary>
    internal static class PausedAreaWorkFilter
    {
        private static readonly Dictionary<Type, List<MethodInfo>> scannerMethodsByReturnType =
            new Dictionary<Type, List<MethodInfo>>();

        internal enum ScannerSignature
        {
            StandardThing,
            StandardCell,
            Fallback
        }

        public static IEnumerable<MethodBase> ScannerMethods(
            Type returnType, ScannerSignature signature)
        {
            if (!scannerMethodsByReturnType.TryGetValue(
                    returnType, out List<MethodInfo> scannerMethods))
            {
                string[] names = returnType == typeof(bool)
                    ? new[] { "HasJobOnThing", "HasJobOnCell" }
                    : new[] { "JobOnThing", "JobOnCell" };

                scannerMethods = GenTypes.AllTypes
                    .Where(type => type != null &&
                                   !type.IsAbstract &&
                                   typeof(WorkGiver_Scanner).IsAssignableFrom(type))
                    .SelectMany(type => AccessTools.GetDeclaredMethods(type))
                    .Where(method => names.Contains(method.Name) &&
                                     method.ReturnType == returnType &&
                                     !method.IsAbstract &&
                                     !method.ContainsGenericParameters)
                    .Distinct()
                    .ToList();
                scannerMethodsByReturnType.Add(returnType, scannerMethods);
            }

            return scannerMethods
                .Where(method => ScannerSignatureFor(method) == signature)
                .Cast<MethodBase>()
                .ToList();
        }

        private static ScannerSignature ScannerSignatureFor(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length < 3 ||
                parameters[0].ParameterType != typeof(Pawn) ||
                parameters[2].ParameterType != typeof(bool))
                return ScannerSignature.Fallback;

            if (method.Name.EndsWith("OnThing", StringComparison.Ordinal) &&
                typeof(Thing).IsAssignableFrom(parameters[1].ParameterType))
            {
                return ScannerSignature.StandardThing;
            }

            if (method.Name.EndsWith("OnCell", StringComparison.Ordinal) &&
                parameters[1].ParameterType == typeof(IntVec3))
            {
                return ScannerSignature.StandardCell;
            }

            return ScannerSignature.Fallback;
        }

        public static bool IsPlayerForcedScannerCall(
            MethodBase originalMethod, object[] arguments)
        {
            ParameterInfo[] parameters = originalMethod?.GetParameters();
            if (parameters == null || arguments == null)
                return false;

            int count = Math.Min(parameters.Length, arguments.Length);
            for (int index = 0; index < count; index++)
            {
                ParameterInfo parameter = parameters[index];
                if (parameter.ParameterType == typeof(bool) &&
                    string.Equals(parameter.Name, "forced",
                        StringComparison.OrdinalIgnoreCase) &&
                    arguments[index] is bool forced)
                {
                    return forced;
                }
            }

            return false;
        }

        public static bool ShouldReject(object[] arguments)
        {
            ExtractScannerArguments(arguments, out Pawn pawn, out Thing thing,
                out IntVec3 cellArgument);
            return ShouldReject(pawn, thing, cellArgument);
        }

        public static bool ShouldReject(Pawn pawn, Thing thing) =>
            ShouldReject(pawn, thing, IntVec3.Invalid);

        public static bool ShouldReject(Pawn pawn, IntVec3 cell) =>
            ShouldReject(pawn, null, cell);

        private static bool ShouldReject(Pawn pawn, Thing thing, IntVec3 cellArgument)
        {
            Faction playerFaction = Faction.OfPlayerSilentFail;
            if (pawn?.Map == null || playerFaction == null ||
                pawn.Faction != playerFaction || pawn.Drafted)
                return false;

            // Recall/restoration jobs must remain available so a paused rule can
            // first return the pawn's normal clothing and release intervention.
            if (AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn)?.RecallRequested == true)
                return false;

            IntVec3 targetCell = thing?.PositionHeld ?? cellArgument;
            Map targetMap = thing?.MapHeld ?? pawn.Map;
            if (!targetCell.IsValid || targetMap == null)
                return false;

            return RuleEvaluator.PausedRulesForMap(targetMap).Any(rule =>
                targetCell.InBounds(targetMap) &&
                rule.Area[targetCell]);
        }

        public static bool ShouldRejectScannerTarget(
            WorkGiver_Scanner scanner, object[] arguments)
        {
            ExtractScannerArguments(arguments, out Pawn pawn, out Thing thing,
                out IntVec3 cellArgument);
            return ShouldRejectScannerTarget(scanner, pawn, thing, cellArgument);
        }

        public static bool ShouldRejectScannerTarget(
            WorkGiver_Scanner scanner, Pawn pawn, Thing thing) =>
            ShouldRejectScannerTarget(scanner, pawn, thing, IntVec3.Invalid);

        public static bool ShouldRejectScannerTarget(
            WorkGiver_Scanner scanner, Pawn pawn, IntVec3 cell) =>
            ShouldRejectScannerTarget(scanner, pawn, null, cell);

        private static bool ShouldRejectScannerTarget(
            WorkGiver_Scanner scanner, Pawn pawn, Thing thing, IntVec3 cellArgument)
        {

            if (pawn?.Map == null || pawn.Drafted || !IsManagedPawn(pawn))
                return false;

            IntVec3 targetCell = thing?.PositionHeld ?? cellArgument;
            Map targetMap = thing?.MapHeld ?? pawn.Map;
            if (!targetCell.IsValid || targetMap == null || !targetCell.InBounds(targetMap))
                return false;

            // StartJob can discover a route-dependent access denial that the
            // scanner could not know from the destination alone. Remember that
            // exact concrete candidate briefly so the next native scan skips it
            // and can select unrelated legal work instead of repeating Wait.
            if (UnavailableWorkRegistry.ShouldReject(
                    pawn, targetMap, thing, targetCell))
            {
                return true;
            }

            if (ManagedWorkClaimRegistry.IsClaimedByOther(
                    pawn, targetMap, thing, targetCell))
                return true;

            bool haulingScanner = ScannerUsesHaulingAccess(scanner);
            bool roamingScanner = IsRobotOrMechanoid(pawn) &&
                ContainsIgnoreCase(scanner?.GetType().Name, "Clean");
            return RuleEvaluator.EnabledRulesForMap(targetMap).Any(rule =>
                rule.Area[targetCell] &&
                (roamingScanner
                    ? !WanderingAllowedFor(rule, pawn)
                    : haulingScanner
                        ? !HaulingAllowedFor(rule, pawn)
                        : !WorkAllowedFor(rule, pawn)));
        }

        private static bool ScannerUsesHaulingAccess(WorkGiver_Scanner scanner)
        {
            WorkTypeDef workType = scanner?.def?.workType;
            if (workType != null)
                return workType == WorkTypeDefOf.Hauling;

            // Direct and some modded scanners may not expose a work type. Use
            // the scanner name only as a compatibility fallback. When a work
            // type is present it is authoritative: a constructor delivering
            // resources is Construction work, while a general hauler doing the
            // same delivery is Hauling work. Warden food, hemogen, custody,
            // suppression, and interaction scanners likewise remain Warden
            // work even when their concrete job carries an item.
            string scannerName = scanner?.GetType().Name;
            string defName = scanner?.def?.defName;
            return ContainsIgnoreCase(scannerName, "DeliverResources") ||
                   ContainsIgnoreCase(defName, "DeliverResources");
        }

        private static void ExtractScannerArguments(
            object[] arguments, out Pawn pawn, out Thing thing, out IntVec3 cellArgument)
        {
            pawn = null;
            thing = null;
            cellArgument = IntVec3.Invalid;
            if (arguments == null)
                return;

            foreach (object argument in arguments)
            {
                if (argument is Pawn pawnArgument)
                {
                    if (pawn == null)
                        pawn = pawnArgument;
                    else if (thing == null)
                        thing = pawnArgument;
                }
                else if (thing == null && argument is Thing thingArgument)
                {
                    thing = thingArgument;
                }

                if (!cellArgument.IsValid && argument is IntVec3 cell)
                    cellArgument = cell;
            }
        }

        public static bool TryGetUnassignedAutomaticManagedGear(
            Pawn pawn,
            Job job,
            ThinkNode jobGiver,
            out Thing gear)
        {
            gear = null;
            if (pawn == null || job?.def == null || job.playerForced)
                return false;

            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            PawnApparelState state = component?.StateFor(pawn);
            if (job.def == JobDefOf.Wear &&
                job.targetA.Thing is Apparel apparel &&
                ManagedApparelClassifier.Matches(apparel.def) &&
                !PawnJobTracker_StartJob_Patch.IsAssignedTransitionApparelJob(
                    state, job))
            {
                gear = apparel;
                component?.NotifyRejectedManagedGearJob(pawn);
                return true;
            }

            ThingWithComps weapon = ManagedWeaponTarget(
                job, component, pawn);
            if (weapon == null ||
                PawnJobTracker_StartJob_Patch.IsAssignedTransitionWeaponJob(
                    state, job) ||
                !LooksLikeAutomaticWeaponPickup(job, jobGiver))
            {
                return false;
            }

            gear = weapon;
            component?.NotifyRejectedManagedGearJob(pawn);
            return true;
        }

        private static ThingWithComps ManagedWeaponTarget(
            Job job,
            AutomaticOutfitManagerGameComponent component,
            Pawn pawn)
        {
            if (job == null)
                return null;

            return ManagedWeaponTarget(job.targetA, component, pawn) ??
                   ManagedWeaponTarget(job.targetB, component, pawn) ??
                   ManagedWeaponTarget(job.targetC, component, pawn);
        }

        private static ThingWithComps ManagedWeaponTarget(
            LocalTargetInfo target,
            AutomaticOutfitManagerGameComponent component,
            Pawn pawn)
        {
            if (target.Thing is not ThingWithComps weapon ||
                weapon.def?.IsWeapon != true)
            {
                return null;
            }

            // Exact saved weapons remain owned even when their definition is
            // not selected by an active work rule. Reject automatic sidearm or
            // loadout pickup before another pawn can repeatedly move the exact
            // item into inventory and force the ownership pulse to drop it.
            return ManagedWeaponClassifier.Matches(weapon.def) ||
                   component?.IsSavedWeaponForOtherPawn(weapon, pawn) == true ||
                   component?.IsManagedWeaponAssignedToOtherPawn(weapon, pawn) == true
                ? weapon
                : null;
        }

        private static bool LooksLikeAutomaticWeaponPickup(
            Job job, ThinkNode jobGiver)
        {
            string jobName = job?.def?.defName;
            string driverName = job?.def?.driverClass?.Name;
            string giverName = (jobGiver ?? job?.jobGiver)?.GetType().Name;
            return ContainsWeaponPickupName(jobName) ||
                   ContainsWeaponPickupName(driverName) ||
                   ContainsWeaponPickupName(giverName);
        }

        private static bool ContainsWeaponPickupName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.IndexOf("Equip", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Sidearm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("RetrieveWeapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("PickupWeapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("SwitchWeapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("TakeWeapon", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool ShouldRejectHaulingJob(Pawn pawn, Job job)
        {
            if (pawn?.Map == null || job == null || !IsHaulingJob(job) ||
                !IsManagedPawn(pawn))
            {
                return false;
            }

            return DeniedHaulingRule(pawn, job) != null;
        }

        public static ApparelRule DeniedActivityRule(Pawn pawn, Job job, ThinkNode jobGiver = null)
        {
            if (pawn?.Map == null || job?.def == null || !IsManagedPawn(pawn) ||
                HasNativeActivityOverride(pawn, job) ||
                IsHaulingJob(job) || IsRestrictedRoamingJob(pawn, job, jobGiver ?? job.jobGiver))
                return null;

            List<ApparelRule> restrictedRules =
                AutomaticOutfitManagerGameComponent.Current?.Rules?.Where(rule =>
                rule != null && rule.Enabled && rule.Area?.Map == pawn.Map &&
                !WorkAllowedFor(rule, pawn)).ToList() ?? new List<ApparelRule>();
            if (ShouldAllowEssentialActivityFallback(pawn, job, restrictedRules)) return null;
            ApparelRule directRule = restrictedRules.FirstOrDefault(rule =>
                RuleEvaluator.JobTargetsArea(job, rule.Area) ||
                (pawn.Position.IsValid && rule.Area[pawn.Position]));
            if (directRule != null)
                return directRule;

            List<ApparelRule> crossedRules = restrictedRules.Where(rule =>
                ProtectedPathAvoidance.JobPathCrossesArea(
                    pawn, job, rule.Area)).ToList();
            return crossedRules.Count > 0 &&
                   ProtectedPathAvoidance.RouteRequiresRestrictedArea(
                       pawn, job, crossedRules)
                ? crossedRules[0]
                : null;
        }

        public static ApparelRule DeniedHaulingRule(Pawn pawn, Job job)
        {
            if (pawn?.Map == null || job == null || !IsHaulingJob(job) ||
                !IsManagedPawn(pawn))
                return null;

            List<ApparelRule> restrictedRules =
                AutomaticOutfitManagerGameComponent.Current?.Rules?.Where(rule =>
                rule != null &&
                rule.Enabled &&
                rule.Area?.Map == pawn.Map &&
                !HaulingAllowedFor(rule, pawn)).ToList() ?? new List<ApparelRule>();
            ApparelRule directRule = restrictedRules.FirstOrDefault(rule =>
                RuleEvaluator.JobTargetsArea(job, rule.Area));
            if (directRule != null)
                return directRule;

            List<ApparelRule> crossedRules = restrictedRules.Where(rule =>
                ProtectedPathAvoidance.JobPathCrossesArea(
                    pawn, job, rule.Area)).ToList();
            return crossedRules.Count > 0 &&
                   ProtectedPathAvoidance.RouteRequiresRestrictedArea(
                       pawn, job, crossedRules)
                ? crossedRules[0]
                : null;
        }

        private static bool HaulingPathCrossesArea(Pawn pawn, Job job, Area area)
        {
            if (pawn?.Map == null || area?.Map != pawn.Map)
                return false;

            LocalTargetInfo pickup = job.targetA;
            LocalTargetInfo destination = job.targetB.IsValid
                ? job.targetB
                : job.targetC;

            if (PathCrossesArea(pawn, pawn.Position, pickup, area))
                return true;

            IntVec3 pickupCell = pickup.IsValid ? pickup.Cell : IntVec3.Invalid;
            return pickupCell.IsValid && destination.IsValid &&
                   PathCrossesArea(pawn, pickupCell, destination, area);
        }

        private static bool PathCrossesArea(
            Pawn pawn, IntVec3 start, LocalTargetInfo destination, Area area)
        {
            if (!start.IsValid || !start.InBounds(pawn.Map) || !destination.IsValid)
                return false;

            PathEndMode endMode = destination.HasThing
                ? PathEndMode.Touch
                : PathEndMode.OnCell;
            PawnPath path = null;
            try
            {
                path = pawn.Map.pathFinder.FindPathNow(
                    start, destination, pawn, null, endMode);
                return path != null && path.Found &&
                       path.NodesReversed.Any(cell =>
                           cell.IsValid && cell.InBounds(pawn.Map) && area[cell]);
            }
            finally
            {
                path?.ReleaseToPool();
            }
        }

        public static bool ShouldRejectProtectedAreaJob(Pawn pawn, Job job)
        {
            if (!ChildAreaAccessPolicy.IsChild(pawn) || pawn.Map == null || job == null || !IsManagedPawn(pawn) ||
                ChildActivityHasNativeOverride(pawn, job))
                return false;

            bool isWorkWatching = IsWorkWatchingJob(job);
            return AutomaticOutfitManagerGameComponent.Current?.Rules?.Any(rule =>
                ChildAreaAccessPolicy.Disallows(pawn, rule) &&
                rule.Area?.Map == pawn.Map &&
                ProtectedJobTargetsArea(pawn, job, rule.Area, isWorkWatching) &&
                !IsChildExitJob(pawn, job, rule)) == true;
        }

        private static bool ChildActivityHasNativeOverride(Pawn pawn, Job job,
            bool allowOutfitTransition = true)
        {
            bool transition = IsAutomaticOutfitManagerTransitionJob(pawn, job);
            return pawn.Drafted || pawn.Downed || pawn.InMentalState ||
                (job.playerForced && !transition) ||
                PawnAccessClassifier.IsNativeCustodyEscapeActive(pawn) ||
                PawnJobTracker_StartJob_Patch.IsNativeEmergencySafetyJob(job) ||
                PawnJobTracker_StartJob_Patch.IsMapDepartureJob(job) ||
                (allowOutfitTransition && transition);
        }

        private static bool IsChildExitJob(Pawn pawn, Job job, ApparelRule rule) =>
            (job.def == JobDefOf.Goto || job.def == JobDefOf.GotoWander) &&
            IsRestrictedRoamingEgress(pawn, job, rule);

        private static bool ProtectedJobTargetsArea(
            Pawn child, Job job, Area area, bool isWorkWatching)
        {
            if (RuleEvaluator.JobTargetsArea(job, area) ||
                (child.Position.IsValid && child.Position.InBounds(child.Map) &&
                 area[child.Position]))
            {
                return true;
            }

            if (JobPathCrossesArea(child, job, area))
                return true;
            if (!isWorkWatching)
                return false;

            // Work watching follows an adult dynamically. The child's job can
            // therefore target only the adult, while the adult's workstation
            // or interaction cell is the protected location that matters.
            Pawn watchedPawn = new[] { job.targetA, job.targetB, job.targetC }
                .Where(target => target.IsValid && target.HasThing)
                .Select(target => target.Thing as Pawn)
                .FirstOrDefault(target => target != null);

            return watchedPawn?.Map == area.Map &&
                   ((watchedPawn.Position.IsValid &&
                     watchedPawn.Position.InBounds(watchedPawn.Map) &&
                     area[watchedPawn.Position]) ||
                    RuleEvaluator.JobTargetsArea(watchedPawn.CurJob, area));
        }

        private static bool IsWorkWatchingJob(Job job) =>
            string.Equals(job?.def?.defName, "Workwatching", StringComparison.OrdinalIgnoreCase);

        public static bool TryRedirectProtectedChildJob(Pawn pawn, Job rejectedJob)
        {
            if (!ShouldRejectProtectedAreaJob(pawn, rejectedJob) || pawn?.Map == null)
                return false;

            List<ApparelRule> restrictedRules = ProtectedChildRules(pawn, rejectedJob);
            if (restrictedRules.Count == 0 ||
                !TryFindSafeWanderingCell(pawn, restrictedRules, out IntVec3 safeCell))
            {
                return false;
            }

            rejectedJob.def = JobDefOf.GotoWander;
            rejectedJob.targetA = safeCell;
            rejectedJob.targetB = LocalTargetInfo.Invalid;
            rejectedJob.targetC = LocalTargetInfo.Invalid;
            rejectedJob.expiryInterval = 300;
            rejectedJob.locomotionUrgency = LocomotionUrgency.Jog;
            AccessExitJobs.Mark(pawn, rejectedJob);
            return true;
        }

        public static bool TryMakeProtectedChildExitJob(
            Pawn pawn, Job rejectedJob, out Job exitJob)
        {
            exitJob = null;
            if (!ShouldRejectProtectedAreaJob(pawn, rejectedJob) || pawn?.Map == null)
                return false;

            List<ApparelRule> restrictedRules = ProtectedChildRules(pawn, rejectedJob);
            if (restrictedRules.Count == 0 ||
                !TryFindSafeWanderingCell(pawn, restrictedRules, out IntVec3 safeCell))
            {
                return false;
            }

            exitJob = JobMaker.MakeJob(JobDefOf.Goto, safeCell);
            exitJob.expiryInterval = 300;
            exitJob.locomotionUrgency = LocomotionUrgency.Jog;
            AccessExitJobs.Mark(pawn, exitJob);
            return true;
        }

        private static List<ApparelRule> ProtectedChildRules(Pawn pawn, Job job)
        {
            // Exit destinations must avoid every child-disabled rule on this map,
            // not just the area touched by the rejected job.
            return AutomaticOutfitManagerGameComponent.Current?.Rules?
                .Where(rule => ChildAreaAccessPolicy.Disallows(pawn, rule) &&
                               rule.Area?.Map == pawn.Map)
                .ToList() ?? new List<ApparelRule>();
        }

        public static bool ShouldRejectPausedAreaJob(Pawn pawn, Job job)
        {
            return DeniedPausedAreaRule(pawn, job) != null;
        }

        public static ApparelRule DeniedPausedAreaRule(Pawn pawn, Job job)
        {
            if (pawn?.Map == null || job == null || pawn.Drafted ||
                !IsManagedPawn(pawn) ||
                AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn)?.RecallRequested == true)
                return null;

            if (HasNativeActivityOverride(pawn, job)) return null;
            bool hauling = IsHaulingJob(job);
            bool wandering = IsRestrictedRoamingJob(pawn, job, job.jobGiver);
            if (IsEssentialPersonalJob(job) && !wandering)
                return null;
            List<ApparelRule> restrictedRules =
                AutomaticOutfitManagerGameComponent.Current?.Rules?.Where(rule =>
                rule != null &&
                rule.Enabled &&
                rule.WorkAreaPaused &&
                rule.Area?.Map == pawn.Map &&
                (hauling
                    ? !HaulingAllowedFor(rule, pawn)
                    : wandering
                        ? !WanderingAllowedFor(rule, pawn)
                        : true)).ToList() ?? new List<ApparelRule>();
            ApparelRule directRule = restrictedRules.FirstOrDefault(rule =>
                RuleEvaluator.JobTargetsArea(job, rule.Area));
            if (directRule != null)
                return directRule;

            List<ApparelRule> crossedRules = restrictedRules.Where(rule =>
                ProtectedPathAvoidance.JobPathCrossesArea(
                    pawn, job, rule.Area)).ToList();
            return crossedRules.Count > 0 &&
                   ProtectedPathAvoidance.RouteRequiresRestrictedArea(
                       pawn, job, crossedRules)
                ? crossedRules[0]
                : null;
        }

        public static ApparelRule MatchingPermittedHaulingRule(Pawn pawn, Job job)
        {
            if (pawn?.Map == null || job == null || !IsManagedPawn(pawn) ||
                pawn.Drafted || !IsHaulingJob(job))
            {
                return null;
            }

            List<ApparelRule> relevant = AutomaticOutfitManagerGameComponent.Current?.Rules?
                .Where(rule => rule != null && rule.Enabled && rule.WorkAreaPaused &&
                    rule.Area?.Map == pawn.Map &&
                    (RuleEvaluator.JobTargetsArea(job, rule.Area) ||
                     HaulingPathCrossesArea(pawn, job, rule.Area)))
                .ToList();
            if (relevant == null || relevant.Count == 0 ||
                relevant.Any(rule => !HaulingAllowedFor(rule, pawn)))
                return null;
            return relevant.FirstOrDefault(rule => MatchesPermittedHaulingRule(pawn, job, rule));
        }

        public static bool MatchesPermittedHaulingRule(
            Pawn pawn, Job job, ApparelRule rule)
        {
            if (pawn?.Map == null || job == null || rule == null ||
                !IsManagedPawn(pawn) || pawn.Drafted || !IsHaulingJob(job) ||
                !rule.Enabled || !rule.WorkAreaPaused ||
                rule.Area?.Map != pawn.Map || !HaulingAllowedFor(rule, pawn))
            {
                return false;
            }

            return (!pawn.RaceProps.Humanlike || RuleEvaluator.RuleCanApplyToPawn(pawn, rule)) &&
                   (RuleEvaluator.JobTargetsArea(job, rule.Area) ||
                    HaulingPathCrossesArea(pawn, job, rule.Area));
        }

        public static bool HasPermittedHaulingContext(
            PawnApparelState state, ApparelRule rule)
        {
            Pawn pawn = state?.Pawn;
            if (pawn == null || rule == null || state.RecallRequested)
                return false;

            if (state.Transition == ApparelTransition.Preparing)
            {
                // Outfit transition jobs temporarily replace the haul that
                // requested them. Preserve the permitted haul behind that
                // transition so the paused-area watchdog does not recall the
                // pawn before the outfit is ready.
                return MatchesPermittedHaulingRule(
                    pawn, state.PendingWorkJob, rule);
            }

            return state.Transition == ApparelTransition.Active &&
                   MatchesPermittedHaulingRule(
                       pawn, pawn.jobs?.curJob, rule);
        }

        private static ApparelRule MatchingPermittedWanderingRule(Pawn pawn, Job job)
        {
            if (pawn?.Map == null || job == null || !IsManagedPawn(pawn) ||
                pawn.Drafted || !IsRestrictedRoamingJob(pawn, job, job.jobGiver))
            {
                return null;
            }

            List<ApparelRule> relevant = AutomaticOutfitManagerGameComponent.Current?.Rules?
                .Where(rule => rule != null && rule.Enabled && rule.WorkAreaPaused &&
                    rule.Area?.Map == pawn.Map &&
                    (RuleEvaluator.JobTargetsArea(job, rule.Area) ||
                     WanderingPathCrossesArea(pawn, job, rule.Area)))
                .ToList();
            if (relevant == null || relevant.Count == 0 ||
                relevant.Any(rule => !WanderingAllowedFor(rule, pawn)))
                return null;
            return relevant[0];
        }

        public static bool JobMayEnterPausedRule(Pawn pawn, Job job, ApparelRule rule)
        {
            return MatchesPermittedHaulingRule(pawn, job, rule) ||
                   MatchesPermittedWanderingRule(pawn, job, rule);
        }

        public static bool ActivityAllowedAtRuleBoundary(
            Pawn pawn, Job job, ApparelRule rule)
        {
            if (pawn?.Map == null || job == null || rule == null ||
                !IsManagedPawn(pawn) || pawn.Drafted || !rule.Enabled ||
                rule.Area?.Map != pawn.Map)
            {
                return false;
            }

            if (ChildAreaAccessPolicy.Disallows(pawn, rule))
                // The path guard separately admits a transition's exact owning
                // area. Do not grant it entry through unrelated child-disabled areas.
                return ChildActivityHasNativeOverride(pawn, job, false) || IsChildExitJob(pawn, job, rule);

            if (IsHaulingJob(job))
                return HaulingAllowedFor(rule, pawn);

            // Native/explicit control and exact transitions are handled by
            // callers. Here a transition still needs permission for unrelated
            // areas; only the path guard can admit its actual owning rule.
            if (HasNativeActivityOverride(pawn, job, false)) return true;
            if ((job.def == JobDefOf.Goto || job.def == JobDefOf.GotoWander) &&
                IsRestrictedRoamingEgress(pawn, job, rule)) return true;

            if (IsRestrictedRoamingJob(pawn, job, job.jobGiver))
                return WanderingAllowedFor(rule, pawn);

            if (!WorkAllowedFor(rule, pawn))
            {
                var denied = AutomaticOutfitManagerGameComponent.Current?.Rules?
                    .Where(candidate => candidate?.Enabled == true &&
                        candidate.Area?.Map == pawn.Map && !WorkAllowedFor(candidate, pawn))
                    .ToList() ?? new List<ApparelRule>();
                return rule.Area[pawn.Position] &&
                    ShouldAllowEssentialActivityFallback(pawn, job, denied);
            }
            // Preserve native essential rest during a pause, consistently for
            // colonists, guests, slaves and prisoners.
            return !rule.WorkAreaPaused || IsEssentialPersonalJob(job);
        }

        public static bool IsHaulingOrWanderingActivityForRule(
            Pawn pawn, Job job, ApparelRule rule)
        {
            if (pawn?.Map == null || job == null || rule?.Area?.Map != pawn.Map ||
                !IsManagedPawn(pawn) || pawn.Drafted)
            {
                return false;
            }

            bool relevantJob = IsHaulingJob(job) ||
                               IsRestrictedRoamingJob(pawn, job, job.jobGiver);
            return relevantJob &&
                   (RuleEvaluator.JobTargetsArea(job, rule.Area) ||
                    JobPathCrossesArea(pawn, job, rule.Area));
        }

        public static bool IsHaulingActivityForRule(
            Pawn pawn, Job job, ApparelRule rule)
        {
            return IsActivityForRule(pawn, job, rule, true);
        }

        public static bool IsWanderingActivityForRule(
            Pawn pawn, Job job, ApparelRule rule)
        {
            return IsActivityForRule(pawn, job, rule, false);
        }

        public static bool IsCurrentHaulingActivityForRule(
            Pawn pawn, Job job, ApparelRule rule)
        {
            return IsCurrentActivityForRule(pawn, job, rule, true);
        }

        public static bool IsCurrentWanderingActivityForRule(
            Pawn pawn, Job job, ApparelRule rule)
        {
            return IsCurrentActivityForRule(pawn, job, rule, false);
        }

        public static bool MatchesCurrentProtectedTransitRule(
            Pawn pawn, Job job, ApparelRule rule)
        {
            if (pawn?.Map == null || job == null || rule == null ||
                !PawnAccessClassifier.IsApparelEligibleHuman(pawn) ||
                pawn.Drafted || !rule.Enabled || rule.WorkAreaPaused ||
                rule.Area?.Map != pawn.Map ||
                !RuleEvaluator.RuleCanApplyToPawn(pawn, rule) ||
                !ActivityAllowedAtRuleBoundary(pawn, job, rule))
            {
                return false;
            }

            return !RuleEvaluator.JobTargetsArea(job, rule.Area) &&
                   CurrentPathTouchesArea(pawn, rule.Area);
        }

        private static bool IsCurrentActivityForRule(
            Pawn pawn, Job job, ApparelRule rule, bool hauling)
        {
            if (pawn?.Map == null || job == null || rule?.Area?.Map != pawn.Map ||
                !IsManagedPawn(pawn) || pawn.Drafted)
            {
                return false;
            }

            bool relevantJob = hauling
                ? IsHaulingJob(job)
                : IsObservedRestrictedRoamingJob(pawn, job, job.jobGiver);
            if (!relevantJob)
                return false;

            // The main management tab is display-only. Reuse RimWorld's actual
            // current path instead of synchronously launching a new pathfinder
            // request for every hauling or wandering pawn on each UI refresh.
            // Gameplay enforcement continues to use the full path queries in
            // IsActivityForRule and the protected-path patches.
            return RuleEvaluator.JobTargetsArea(job, rule.Area) ||
                   CurrentPathTouchesArea(pawn, rule.Area);
        }

        private static bool CurrentPathTouchesArea(Pawn pawn, Area area)
        {
            if (pawn?.Map == null || area?.Map != pawn.Map)
                return false;

            if (pawn.Position.IsValid && pawn.Position.InBounds(pawn.Map) &&
                area[pawn.Position])
            {
                return true;
            }

            PawnPath path = pawn.pather?.curPath;
            return path?.Found == true &&
                   path.NodesReversed.Any(cell =>
                       cell.IsValid && cell.InBounds(pawn.Map) && area[cell]);
        }

        private static bool IsActivityForRule(
            Pawn pawn, Job job, ApparelRule rule, bool hauling)
        {
            if (pawn?.Map == null || job == null || rule?.Area?.Map != pawn.Map ||
                !IsManagedPawn(pawn) || pawn.Drafted)
            {
                return false;
            }

            bool relevantJob = hauling
                ? IsHaulingJob(job)
                : IsRestrictedRoamingJob(pawn, job, job.jobGiver);
            return relevantJob &&
                   (RuleEvaluator.JobTargetsArea(job, rule.Area) ||
                    JobPathCrossesArea(pawn, job, rule.Area));
        }

        private static bool MatchesPermittedWanderingRule(Pawn pawn, Job job, ApparelRule rule)
        {
            return pawn?.Map != null && job != null && rule != null &&
                   IsManagedPawn(pawn) && !pawn.Drafted &&
                   IsRestrictedRoamingJob(pawn, job, job.jobGiver) &&
                   rule.Enabled && rule.WorkAreaPaused &&
                   rule.Area?.Map == pawn.Map && WanderingAllowedFor(rule, pawn) &&
                   (RuleEvaluator.JobTargetsArea(job, rule.Area) ||
                    WanderingPathCrossesArea(pawn, job, rule.Area));
        }

        public static ApparelRule MatchingProtectedTransitRule(Pawn pawn, Job job)
            => MatchingProtectedTransitRules(pawn, job).FirstOrDefault();

        public static List<ApparelRule> MatchingProtectedTransitRules(Pawn pawn, Job job)
        {
            if (pawn?.Map == null || job == null ||
                !PawnAccessClassifier.IsApparelEligibleHuman(pawn) || pawn.Drafted)
                return new List<ApparelRule>();

            return AutomaticOutfitManagerGameComponent.Current?.Rules?.Where(rule =>
                       MatchesProtectedTransitRule(pawn, job, rule)).ToList() ??
                   new List<ApparelRule>();
        }

        public static bool MatchesProtectedTransitRule(
            Pawn pawn, Job job, ApparelRule rule)
        {
            if (pawn?.Map == null || job == null || rule == null ||
                !PawnAccessClassifier.IsApparelEligibleHuman(pawn) || pawn.Drafted || !rule.Enabled ||
                rule.WorkAreaPaused || rule.Area?.Map != pawn.Map ||
                !RuleEvaluator.RuleCanApplyToPawn(pawn, rule))
            {
                return false;
            }

            // Leaving an occupied area does not start another outfit session.
            // Other areas on the route still use the ordinary boundary checks.
            if (AccessExitJobs.IsOwned(pawn, job) && IsRestrictedRoamingEgress(pawn, job, rule))
                return false;

            // A disabled activity whose target is outside the rule is routed
            // around the area. It must not trigger an unnecessary outfit swap
            // merely because RimWorld's uncustomized shortest path crosses it.
            if (!ActivityAllowedAtRuleBoundary(pawn, job, rule))
                return false;

            // The actual worksite is handled by RuleEvaluator.MatchesRule.
            // Auxiliary materials inside an area do not make that area own an
            // outside work job. Pre-prepare for transit only when even the
            // strong protected-area avoidance route still crosses the area;
            // the path-cell guard remains the authority for late-bound entry.
            if (RuleEvaluator.JobPreparationTargetsArea(job, rule.Area) ||
                !ProtectedPathAvoidance.JobPathCrossesArea(
                    pawn, job, rule.Area))
            {
                return false;
            }

            bool unavoidable = ProtectedPathAvoidance
                .RouteRequiresRestrictedArea(
                    pawn, job, new[] { rule }, false);
            if (unavoidable && AomLog.DetailedEnabled &&
                AomLog.ShouldLogDetailed(
                    pawn,
                    $"unavoidable-protected-transit:{rule.Id}:" +
                    $"{job.def?.defName}",
                    600))
            {
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                    $"qualified {job.def?.defName ?? "job"} for " +
                    $"'{rule.Name}' because every viable route crosses the " +
                    "protected area.");
            }

            return unavoidable;
        }

        private static bool JobPathCrossesArea(Pawn pawn, Job job, Area area)
        {
            LocalTargetInfo destination = TransitDestinationFor(job);
            return destination.IsValid &&
                   PathCrossesArea(pawn, pawn.Position, destination, area);
        }

        private static LocalTargetInfo TransitDestinationFor(Job job)
        {
            if (job == null)
                return LocalTargetInfo.Invalid;

            return job.targetA.IsValid
                ? job.targetA
                : job.targetB.IsValid ? job.targetB : job.targetC;
        }

        public static bool ShouldRejectWanderingJob(Pawn pawn, Job job, ThinkNode jobGiver = null)
        {
            if (pawn?.Map == null || job == null || pawn.Drafted ||
                !IsRestrictedRoamingJob(pawn, job, jobGiver) ||
                !IsManagedPawn(pawn))
                return false;

            List<ApparelRule> restrictedRules =
                AutomaticOutfitManagerGameComponent.Current?.Rules?.Where(rule =>
                rule != null &&
                rule.Enabled &&
                !WanderingAllowedFor(rule, pawn) &&
                rule.Area?.Map == pawn.Map).ToList() ?? new List<ApparelRule>();
            restrictedRules = restrictedRules.Where(rule =>
                    !IsRestrictedRoamingEgress(pawn, job, rule))
                .ToList();
            if (restrictedRules.Any(rule =>
                    RuleEvaluator.JobTargetsArea(job, rule.Area) ||
                    (IsRestrictedRoamingWaitJob(pawn, job, jobGiver) &&
                     pawn.Position.IsValid && pawn.Position.InBounds(pawn.Map) &&
                     rule.Area[pawn.Position])))
            {
                return true;
            }

            List<ApparelRule> crossedRules = restrictedRules.Where(rule =>
                ProtectedPathAvoidance.JobPathCrossesArea(
                    pawn, job, rule.Area)).ToList();
            return crossedRules.Count > 0 &&
                   ProtectedPathAvoidance.RouteRequiresRestrictedArea(
                       pawn, job, crossedRules);
        }

        private static bool WanderingPathCrossesArea(Pawn pawn, Job job, Area area)
        {
            LocalTargetInfo destination = job.targetA.IsValid
                ? job.targetA
                : job.targetB.IsValid ? job.targetB : job.targetC;
            return destination.IsValid &&
                   PathCrossesArea(pawn, pawn.Position, destination, area);
        }

        public static bool TryRedirectWanderingJob(Pawn pawn, Job job, ThinkNode jobGiver = null)
        {
            if (!ShouldRejectWanderingJob(pawn, job, jobGiver) || pawn?.Map == null)
                return false;

            List<ApparelRule> restrictedRules = AutomaticOutfitManagerGameComponent.Current?.Rules?
                .Where(rule => rule != null &&
                               rule.Enabled &&
                               !WanderingAllowedFor(rule, pawn) &&
                               rule.Area?.Map == pawn.Map)
                .ToList();
            if (restrictedRules == null || restrictedRules.Count == 0)
                return false;

            var blockingCandidates = restrictedRules.Where(rule =>
                !IsRestrictedRoamingEgress(pawn, job, rule)).ToList();
            var blockingRule = blockingCandidates.FirstOrDefault(rule =>
                RuleEvaluator.JobTargetsArea(job, rule.Area) ||
                (IsRestrictedRoamingWaitJob(pawn, job, jobGiver) && rule.Area[pawn.Position])) ??
                blockingCandidates.FirstOrDefault(rule =>
                    ProtectedPathAvoidance.JobPathCrossesArea(pawn, job, rule.Area));
            if (AomLog.DetailedEnabled && blockingRule != null && IsFriendlyGuest(pawn) &&
                AomLog.ShouldLogDetailed(
                    pawn, $"guest-roaming-disabled:{blockingRule.Id}"))
            {
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: redirected " +
                    $"autonomous guest {job.def?.defName ?? "movement"} away from " +
                    $"'{blockingRule.Name}'; guest wandering is disabled.");
            }

            bool startsInside = restrictedRules.Any(rule => rule.Area[pawn.Position]);
            if (!startsInside)
            {
                // The pawn is already safe, but its thinker selected a wander
                // route through the restricted area. Sending it to another
                // GotoWander cell makes autonomous robot thinkers immediately
                // request yet another wander job on arrival, producing a
                // visible wander/no-job loop. Yield briefly in place instead;
                // normal AI can reconsider useful work after the cooldown.
                job.def = JobDefOf.Wait;
                job.targetA = pawn;
                job.targetB = LocalTargetInfo.Invalid;
                job.targetC = LocalTargetInfo.Invalid;
                job.expiryInterval = 180;
                AccessExitJobs.Mark(pawn, job);
                return true;
            }

            if (!TryFindSafeWanderingCell(pawn, restrictedRules, out IntVec3 safeCell))
                return false;

            // An occupant of the restricted area still needs one direct exit
            // movement before the outside cooldown behavior can take over.
            job.def = JobDefOf.GotoWander;
            job.targetA = safeCell;
            job.targetB = LocalTargetInfo.Invalid;
            job.targetC = LocalTargetInfo.Invalid;
            job.expiryInterval = 300;
            AccessExitJobs.Mark(pawn, job);
            return true;
        }

        public static bool TryMakeWanderingExitJob(Pawn pawn, out Job exitJob)
        {
            exitJob = null;
            if (pawn?.Map == null)
                return false;

            List<ApparelRule> restrictedRules = AutomaticOutfitManagerGameComponent.Current?.Rules?
                .Where(rule => rule != null &&
                               rule.Enabled &&
                               !WanderingAllowedFor(rule, pawn) &&
                               rule.Area?.Map == pawn.Map)
                .ToList();
            if (restrictedRules == null ||
                !restrictedRules.Any(rule => rule.Area[pawn.Position]) ||
                !TryFindSafeWanderingCell(pawn, restrictedRules, out IntVec3 safeCell))
            {
                return false;
            }

            exitJob = JobMaker.MakeJob(JobDefOf.Goto, safeCell);
            exitJob.expiryInterval = 300;
            exitJob.locomotionUrgency = LocomotionUrgency.Jog;
            AccessExitJobs.Mark(pawn, exitJob);
            return true;
        }

        private static bool TryFindSafeWanderingCell(
            Pawn pawn, List<ApparelRule> restrictedRules, out IntVec3 safeCell)
        {
            safeCell = IntVec3.Invalid;
            if (pawn?.Map == null || restrictedRules == null || restrictedRules.Count == 0)
                return false;

            bool startsInside = restrictedRules.Any(rule => rule.Area[pawn.Position]);
            IEnumerable<IntVec3> candidates = GenRadial.RadialCellsAround(
                    pawn.Position, 24f, true)
                .Where(cell => cell.IsValid &&
                               cell.InBounds(pawn.Map) &&
                               cell != pawn.Position &&
                               cell.Standable(pawn.Map) &&
                               !cell.IsForbidden(pawn) &&
                               // An area toggle must not walk a prisoner out
                               // through the cell door. Native labor/departure
                               // jobs retain their own authority separately.
                               (!IsPrisoner(pawn) || cell.GetRoom(pawn.Map) == pawn.GetRoom()) &&
                               restrictedRules.All(rule => !rule.Area[cell]) &&
                               HasRestrictedAreaClearance(
                                   pawn.Map, cell, restrictedRules, 3.9f) &&
                               pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some));

            // An occupant must cross its current restricted area to leave, so
            // choose the nearest valid outside cell. Pawns already outside keep
            // the stricter rule that their entire route must avoid the area.
            if (startsInside)
                candidates = candidates.OrderBy(cell => cell.DistanceToSquared(pawn.Position));
            else
                candidates = candidates.InRandomOrder();

            foreach (IntVec3 candidate in candidates)
            {
                if (restrictedRules.All(rule => rule.Area[pawn.Position] ||
                    !PathCrossesArea(pawn, pawn.Position, candidate, rule.Area)))
                {
                    safeCell = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool HasRestrictedAreaClearance(
            Map map, IntVec3 cell, List<ApparelRule> restrictedRules, float radius)
        {
            foreach (IntVec3 nearby in GenRadial.RadialCellsAround(cell, radius, true))
            {
                if (!nearby.IsValid || !nearby.InBounds(map))
                    continue;

                if (restrictedRules.Any(rule => rule.Area[nearby]))
                    return false;
            }

            return true;
        }

        private static bool HasNativeActivityOverride(Pawn pawn, Job job,
            bool allowTransition = true) =>
            pawn == null || job == null || pawn.Drafted || pawn.Downed || pawn.InMentalState ||
            ActivityJobClassifier.IsRobotBaseDuty(pawn, job) ||
            (job.playerForced && !IsAutomaticOutfitManagerTransitionJob(pawn, job)) ||
            PawnAccessClassifier.IsNativeCustodyEscapeActive(pawn) ||
            PawnJobTracker_StartJob_Patch.IsNativeEmergencySafetyJob(job) ||
            PawnJobTracker_StartJob_Patch.IsMapDepartureJob(job) ||
            (allowTransition && IsAutomaticOutfitManagerTransitionJob(pawn, job));

        private static bool IsRestrictedRoamingJob(Pawn pawn, Job job, ThinkNode jobGiver)
        {
            if (HasNativeActivityOverride(pawn, job)) return false;
            return ActivityJobClassifier.IsWandering(pawn, job, IsRobotOrMechanoid(pawn), jobGiver);
        }

        private static bool IsObservedRestrictedRoamingJob(
            Pawn pawn, Job job, ThinkNode jobGiver)
        {
            return !ActivityJobClassifier.IsHauling(job) &&
                   (IsRestrictedRoamingJob(pawn, job, jobGiver) ||
                    ActivityJobClassifier.IsObservedNonHumanIdle(pawn, job));
        }

        private static bool IsAutomaticOutfitManagerTransitionJob(
            Pawn pawn, Job job)
        {
            PawnApparelState state =
                AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn);
            if (state == null || job == null)
                return false;

            return PawnJobTracker_StartJob_Patch
                       .IsAssignedChangingAreaReturnJob(state, job) ||
                   PawnJobTracker_StartJob_Patch
                       .IsAssignedTransitionApparelJob(state, job) ||
                   PawnJobTracker_StartJob_Patch
                       .IsAssignedTransitionWeaponJob(state, job) ||
                   // Idle/stalled Phase 3 recovery uses a same-cell Goto only
                   // to re-enter StartJob and rebuild the exact restoration
                   // queue. Treating that trigger as guest wandering converted
                   // it to Wait before the saved weapon Equip could be issued.
                   (state.Transition == ApparelTransition.Restoring &&
                    job.def == JobDefOf.Goto &&
                    job.targetA.Cell.IsValid &&
                    job.targetA.Cell == pawn.Position);
        }

        // A confined pawn with no reachable safe exterior cell must retain
        // essential bed rest. This exception is identical for all humanlikes;
        // it never admits a new arrival or waives equipment checks.
        private static bool ShouldAllowEssentialActivityFallback(
            Pawn pawn, Job job, List<ApparelRule> restrictedRules)
        {
            return pawn?.RaceProps?.Humanlike == true && IsEssentialPersonalJob(job) &&
                pawn.Map != null && restrictedRules?.Count > 0 && pawn.Position.IsValid &&
                restrictedRules.Any(rule => rule.Area[pawn.Position]) &&
                !TryFindSafeWanderingCell(pawn, restrictedRules, out _);
        }

        public static bool TryMakeAccessExitJob(Pawn pawn, Job rejectedJob, out Job exitJob)
        {
            exitJob = null;
            if (pawn?.Map == null || rejectedJob == null) return false;
            var rules = AutomaticOutfitManagerGameComponent.Current?.Rules?
                .Where(rule => rule?.Enabled == true && rule.Area?.Map == pawn.Map &&
                    !ActivityAllowedAtRuleBoundary(pawn, rejectedJob, rule)).ToList();
            if (rules == null || !rules.Any(rule => rule.Area[pawn.Position]) ||
                !TryFindSafeWanderingCell(pawn, rules, out IntVec3 cell)) return false;
            exitJob = JobMaker.MakeJob(JobDefOf.Goto, cell);
            exitJob.expiryInterval = 300;
            AccessExitJobs.Mark(pawn, exitJob);
            return true;
        }

        private static bool IsRestrictedRoamingWaitJob(
            Pawn pawn, Job job, ThinkNode jobGiver)
        {
            if (!IsRestrictedRoamingJob(pawn, job, jobGiver))
                return false;

            string defName = job?.def?.defName ?? string.Empty;
            return job?.def == JobDefOf.Wait_Wander ||
                   job?.def == JobDefOf.Wait ||
                   defName.StartsWith("Wait", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRestrictedRoamingEgress(
            Pawn pawn, Job job, ApparelRule rule)
        {
            if (pawn?.Map == null || job == null || rule?.Area?.Map != pawn.Map ||
                !pawn.Position.IsValid || !pawn.Position.InBounds(pawn.Map) ||
                !rule.Area[pawn.Position])
            {
                return false;
            }

            LocalTargetInfo destination = TransitDestinationFor(job);
            if (!destination.IsValid ||
                (destination.HasThing && destination.Thing?.MapHeld != pawn.Map))
            {
                return false;
            }

            IntVec3 destinationCell = destination.Cell;
            return destinationCell.IsValid &&
                   destinationCell.InBounds(pawn.Map) &&
                   !rule.Area[destinationCell];
        }

        private static bool ContainsIgnoreCase(string value, string fragment) =>
            !string.IsNullOrEmpty(value) &&
            value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;

        public static bool IsEssentialPersonalJob(Job job)
        {
            if (job?.def == null)
                return false;

            string defName = job.def.defName ?? string.Empty;
            return job.def == JobDefOf.LayDown ||
                   string.Equals(defName, "LayDown", StringComparison.OrdinalIgnoreCase) ||
                   defName.IndexOf("GotoBed", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsHaulingJob(Job job) =>
            ActivityJobClassifier.IsHauling(job);

        public static bool UsesManagedWorkPreparation(Job job)
        {
            if (job?.def == null)
                return false;

            if (!IsHaulingJob(job))
                return true;

            // RimWorld assigns turret rearming to the Hauling work type because
            // the pawn carries fuel, but the destination is a fixed worksite.
            // Treat it as managed-area work for outfit preparation and task-
            // buffer ownership while retaining every native Hauling priority,
            // access, fuel, pathing, and reservation decision.
            string jobDefName = job.def.defName ?? string.Empty;
            string workGiverDefName = job.workGiverDef?.defName ?? string.Empty;
            return jobDefName.Equals(
                       "RearmTurret", StringComparison.OrdinalIgnoreCase) ||
                   jobDefName.Equals(
                       "RearmTurretAtomic", StringComparison.OrdinalIgnoreCase) ||
                   workGiverDefName.Equals(
                       "RearmTurrets", StringComparison.OrdinalIgnoreCase);
        }

        private static AccessGroup PermissionGroup(Pawn pawn) =>
            AreaActivityPermissions.Group(IsPrisoner(pawn), pawn?.IsSlave == true,
                IsRobotOrMechanoid(pawn), pawn?.RaceProps?.Animal == true, IsFriendlyGuest(pawn));

        private static bool HaulingAllowedFor(ApparelRule rule, Pawn pawn) =>
            !ChildAreaAccessPolicy.Disallows(pawn, rule) &&
            AreaActivityPermissions.Allows(rule, PermissionGroup(pawn), AccessActivity.Hauling);

        internal static bool WorkAllowedFor(ApparelRule rule, Pawn pawn) =>
            !ChildAreaAccessPolicy.Disallows(pawn, rule) &&
            AreaActivityPermissions.Allows(rule, PermissionGroup(pawn), AccessActivity.Activities);

        private static bool WanderingAllowedFor(ApparelRule rule, Pawn pawn) =>
            !ChildAreaAccessPolicy.Disallows(pawn, rule) &&
            AreaActivityPermissions.Allows(rule, PermissionGroup(pawn), AccessActivity.Wandering);

        private static bool IsManagedPawn(Pawn pawn)
        {
            Faction playerFaction = Faction.OfPlayerSilentFail;
            return (playerFaction != null && pawn?.Faction == playerFaction) ||
                   IsFriendlyGuest(pawn) || IsPrisoner(pawn) || PawnAccessClassifier.IsFriendlyForeignUnit(pawn);
        }

        private static bool IsPrisoner(Pawn pawn) =>
            PawnAccessClassifier.IsColonyPrisoner(pawn);

        private static bool IsFriendlyGuest(Pawn pawn) =>
            PawnAccessClassifier.IsHostedGuest(pawn);

        private static bool IsRobotOrMechanoid(Pawn pawn)
        {
            RaceProperties race = pawn?.RaceProps;
            if (race == null)
                return false;

            if (race.IsMechanoid || race.FleshType == FleshTypeDefOf.Mechanoid)
                return true;

            // Several robot mods implement utility bots as animal-like pawn
            // races so they can reuse training, zoning, and work AI. They do
            // not report the vanilla mechanoid flesh type and previously fell
            // through to the Animals column. Stable def identifiers let those
            // pawns honor the Mechs permission without targeting any one mod.
            return LooksMechanical(pawn.def?.defName) ||
                   LooksMechanical(pawn.kindDef?.defName) ||
                   LooksMechanical(race.FleshType?.defName);
        }

        private static bool LooksMechanical(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return false;

            return identifier.IndexOf("robot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   identifier.IndexOf("mech", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   identifier.IndexOf("android", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   identifier.IndexOf("droid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   identifier.EndsWith("Bot", StringComparison.OrdinalIgnoreCase);
        }
    }

    [HarmonyPatch]
    internal static class WorkGiverPausedArea_HasJobThing_Patch
    {
        private static bool Prepare() => DeferredWorkScannerPatches.Ready && TargetMethods().Any();

        private static IEnumerable<MethodBase> TargetMethods() =>
            PausedAreaWorkFilter.ScannerMethods(
                typeof(bool), PausedAreaWorkFilter.ScannerSignature.StandardThing);

        private static void Postfix(
            WorkGiver_Scanner __instance, ref bool __result, Pawn __0, Thing __1,
            bool __2)
        {
            // The forced flag is RimWorld's explicit player-order boundary.
            // Preserve that native query so right-click work remains available;
            // StartJob will still prepare required gear before protected entry.
            if (__result && !__2 &&
                (PausedAreaWorkFilter.ShouldReject(__0, __1) ||
                 PausedAreaWorkFilter.ShouldRejectScannerTarget(__instance, __0, __1)))
                __result = false;
        }
    }

    /// <summary>
    /// A forced rearm query searches for reachable fuel before RimWorld creates
    /// its right-click option. That prospective path search must not inherit a
    /// customizer derived from the pawn's unrelated current job (for example,
    /// recreation outside a guest- or wandering-restricted work area). The
    /// eventual rearm job still passes through StartJob preparation and the
    /// live boundary guard, so this bypass cannot admit an unequipped pawn.
    /// </summary>
    [HarmonyPatch]
    internal static class WorkGiverRefuel_ForcedRearmPathQuery_Patch
    {
        private sealed class SuppressionState
        {
            public bool Previous;
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] signature = { typeof(Pawn), typeof(Thing), typeof(bool) };
            MethodInfo hasJob = AccessTools.Method(
                typeof(WorkGiver_Refuel), nameof(WorkGiver_Refuel.HasJobOnThing),
                signature);
            MethodInfo jobOnThing = AccessTools.Method(
                typeof(WorkGiver_Refuel), nameof(WorkGiver_Refuel.JobOnThing),
                signature);
            if (hasJob != null)
                yield return hasJob;
            if (jobOnThing != null)
                yield return jobOnThing;
        }

        private static void Prefix(
            WorkGiver_Refuel __instance, bool __2,
            out SuppressionState __state)
        {
            __state = null;
            if (!__2 || __instance is not WorkGiver_Refuel_Turret)
                return;

            __state = new SuppressionState
            {
                Previous = ProtectedPathAvoidance
                    .BeginAutomaticCustomizerSuppression()
            };
        }

        private static Exception Finalizer(
            Exception __exception, SuppressionState __state)
        {
            if (__state != null)
            {
                ProtectedPathAvoidance.EndAutomaticCustomizerSuppression(
                    __state.Previous);
            }

            return __exception;
        }
    }

    [HarmonyPatch]
    internal static class WorkGiverPausedArea_HasJobCell_Patch
    {
        private static bool Prepare() => DeferredWorkScannerPatches.Ready && TargetMethods().Any();

        private static IEnumerable<MethodBase> TargetMethods() =>
            PausedAreaWorkFilter.ScannerMethods(
                typeof(bool), PausedAreaWorkFilter.ScannerSignature.StandardCell);

        private static void Postfix(
            WorkGiver_Scanner __instance, ref bool __result, Pawn __0, IntVec3 __1,
            bool __2)
        {
            if (__result && !__2 &&
                (PausedAreaWorkFilter.ShouldReject(__0, __1) ||
                 PausedAreaWorkFilter.ShouldRejectScannerTarget(__instance, __0, __1)))
                __result = false;
        }
    }

    [HarmonyPatch]
    internal static class WorkGiverPausedArea_HasJobFallback_Patch
    {
        private static bool Prepare() => DeferredWorkScannerPatches.Ready && TargetMethods().Any();

        private static IEnumerable<MethodBase> TargetMethods() =>
            PausedAreaWorkFilter.ScannerMethods(
                typeof(bool), PausedAreaWorkFilter.ScannerSignature.Fallback);

        private static void Postfix(
            WorkGiver_Scanner __instance, MethodBase __originalMethod,
            ref bool __result, object[] __args)
        {
            if (__result &&
                !PausedAreaWorkFilter.IsPlayerForcedScannerCall(
                    __originalMethod, __args) &&
                (PausedAreaWorkFilter.ShouldReject(__args) ||
                 PausedAreaWorkFilter.ShouldRejectScannerTarget(__instance, __args)))
                __result = false;
        }
    }

    [HarmonyPatch]
    internal static class WorkGiverPausedArea_JobOnThing_Patch
    {
        private static bool Prepare() => DeferredWorkScannerPatches.Ready && TargetMethods().Any();

        private static IEnumerable<MethodBase> TargetMethods() =>
            PausedAreaWorkFilter.ScannerMethods(
                typeof(Job), PausedAreaWorkFilter.ScannerSignature.StandardThing);

        private static void Postfix(
            WorkGiver_Scanner __instance, ref Job __result, Pawn __0, Thing __1,
            bool __2)
        {
            if (__result != null && !__2 &&
                (ManagedWorkCandidateFilter.Rejects(__0, __result) ||
                 PausedAreaWorkFilter.ShouldReject(__0, __1) ||
                 PausedAreaWorkFilter.ShouldRejectScannerTarget(__instance, __0, __1)))
                __result = null;
        }
    }

    [HarmonyPatch]
    internal static class WorkGiverPausedArea_JobOnCell_Patch
    {
        private static bool Prepare() => DeferredWorkScannerPatches.Ready && TargetMethods().Any();

        private static IEnumerable<MethodBase> TargetMethods() =>
            PausedAreaWorkFilter.ScannerMethods(
                typeof(Job), PausedAreaWorkFilter.ScannerSignature.StandardCell);

        private static void Postfix(
            WorkGiver_Scanner __instance, ref Job __result, Pawn __0, IntVec3 __1,
            bool __2)
        {
            if (__result != null && !__2 &&
                (ManagedWorkCandidateFilter.Rejects(__0, __result) ||
                 PausedAreaWorkFilter.ShouldReject(__0, __1) ||
                 PausedAreaWorkFilter.ShouldRejectScannerTarget(__instance, __0, __1)))
                __result = null;
        }
    }

    [HarmonyPatch]
    internal static class WorkGiverPausedArea_JobOnFallback_Patch
    {
        private static bool Prepare() => DeferredWorkScannerPatches.Ready && TargetMethods().Any();

        private static IEnumerable<MethodBase> TargetMethods() =>
            PausedAreaWorkFilter.ScannerMethods(
                typeof(Job), PausedAreaWorkFilter.ScannerSignature.Fallback);

        private static void Postfix(
            WorkGiver_Scanner __instance, MethodBase __originalMethod,
            ref Job __result, object[] __args)
        {
            if (__result != null &&
                !PausedAreaWorkFilter.IsPlayerForcedScannerCall(
                    __originalMethod, __args) &&
                (ManagedWorkCandidateFilter.Rejects(__args.OfType<Pawn>().FirstOrDefault(), __result) ||
                 PausedAreaWorkFilter.ShouldReject(__args) ||
                 PausedAreaWorkFilter.ShouldRejectScannerTarget(__instance, __args)))
                __result = null;
        }
    }

    /// <summary>
    /// Jobs such as child learning, equipment optimization, and some route-
    /// dependent work come from general or specialized job-giver overrides
    /// rather than a shared WorkGiver_Scanner result. Patching every concrete
    /// TryIssueJobPackage implementation rejects those candidates while the
    /// priority tree can still continue to another safe activity.
    /// </summary>
    [HarmonyPatch]
    internal static class ThinkNodeJobGiver_ProtectedArea_Patch
    {
        private static bool Prepare() => DeferredWorkScannerPatches.Ready;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            return GenTypes.AllTypes
                .Where(type => type != null &&
                               typeof(ThinkNode_JobGiver).IsAssignableFrom(type))
                .SelectMany(AccessTools.GetDeclaredMethods)
                .Where(method =>
                    method.Name == "TryIssueJobPackage" &&
                    method.ReturnType == typeof(ThinkResult) &&
                    !method.IsAbstract &&
                    !method.ContainsGenericParameters &&
                    method.GetParameters().Length > 0 &&
                    method.GetParameters()[0].ParameterType == typeof(Pawn))
                .Distinct()
                .Cast<MethodBase>()
                .ToList();
        }

        private static void Postfix(
            ThinkNode_JobGiver __instance, Pawn __0, ref ThinkResult __result)
        {
            if (!__result.IsValid)
                return;

            Pawn pawn = __0;
            if (ManagedWorkCandidateFilter.Rejects(pawn, __result.Job))
            {
                __result = default;
                return;
            }
            if (IngestReservationAdmission.Rejects(pawn, __result.Job))
            {
                TransitionActivityDiagnostics.Rejected(pawn, __result.Job, "meal reservation admission");
                __result = default;
                return;
            }

            if (PausedAreaWorkFilter.TryGetUnassignedAutomaticManagedGear(
                    pawn, __result.Job, __instance, out Thing managedGear))
            {
                PawnJobTracker_StartJob_Patch.LogAutomaticManagedGearRejection(
                    pawn, __result.Job, managedGear, "job selection");
                TransitionActivityDiagnostics.Rejected(pawn, __result.Job, "unassigned managed outfit item");
                __result = default;
                return;
            }

            // An unavailable-gear entry protects real work targets inside the
            // area, but it must not erase every native custody fallback from a
            // prisoner's think tree or the essential bed-rest fallback selected
            // after a real gear search fails. Doing so exhausts the tree into
            // IdleError or repeatedly leaves the pawn awake and Standing. The
            // StartJob and path boundaries still equip reachable gear first;
            // these exemptions exist only during the bounded shortage record.
            bool unavailableGearBlocked =
                UnavailableWorkRegistry.ShouldReject(pawn, __result.Job);
            bool unavailableEssentialFallback = unavailableGearBlocked &&
                PausedAreaWorkFilter.IsEssentialPersonalJob(__result.Job);
            if (!PawnJobTracker_StartJob_Patch
                    .IsNativePrisonerUnavailableGearFallbackJob(
                        pawn, __result.Job) &&
                !unavailableEssentialFallback && unavailableGearBlocked)
            {
                TransitionActivityDiagnostics.Rejected(pawn, __result.Job, "unavailable outfit cooldown");
                __result = default;
                return;
            }

            if (PausedAreaWorkFilter.DeniedActivityRule(pawn, __result.Job, __instance) != null)
            {
                TransitionActivityDiagnostics.Rejected(pawn, __result.Job, "area activity access denied");
                __result = default;
                return;
            }

            if (PausedAreaWorkFilter.TryRedirectWanderingJob(pawn, __result.Job, __instance))
                return;

            if (PausedAreaWorkFilter.TryRedirectProtectedChildJob(pawn, __result.Job))
                return;

            if (PausedAreaWorkFilter.ShouldRejectPausedAreaJob(pawn, __result.Job) ||
                PausedAreaWorkFilter.ShouldRejectHaulingJob(pawn, __result.Job) ||
                PausedAreaWorkFilter.ShouldRejectProtectedAreaJob(pawn, __result.Job) ||
                PausedAreaWorkFilter.ShouldRejectWanderingJob(pawn, __result.Job, __instance))
            {
                TransitionActivityDiagnostics.Rejected(pawn, __result.Job, "paused or protected area access denied");
                __result = default;
            }
        }
    }
}
