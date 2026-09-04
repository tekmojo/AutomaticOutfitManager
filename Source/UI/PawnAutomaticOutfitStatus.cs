using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.UI
{
    public static class PawnAutomaticOutfitStatus
    {
        private const float CacheSeconds = 0.5f;
        private static readonly Dictionary<Pawn, CachedStatus> StatusCache =
            new Dictionary<Pawn, CachedStatus>();

        private sealed class CachedStatus
        {
            public float CreatedAt;
            public ApparelTransition Transition;
            public string RuleId;
            public int OriginalCount;
            public int AutomaticCount;
            public int WornCount;
            public int BufferedTasksCompleted;
            public int ReturnTaskBuffer;
            public int PendingBufferedJobLoadId;
            public int CurrentJobLoadId;
            public int PendingWorkLoadId;
            public bool RecallInterruptPending;
            public bool Drafted;
            public string Text;
        }

        internal static void ResetRuntimeCache() => StatusCache.Clear();

        public static string Build(Pawn pawn)
        {
            AutomaticOutfitManagerGameComponent component = AutomaticOutfitManagerGameComponent.Current;
            PawnApparelState state = component?.StateFor(pawn);
            if (state == null)
            {
                if (pawn != null)
                    StatusCache.Remove(pawn);
                return null;
            }

            ApparelRule rule = component.RuleById(state.ActiveRuleId);
            int originalCount = state.OriginalApparel?.Count ?? 0;
            int automaticCount = state.ManagedApparel?.Count ?? 0;
            int wornCount = pawn.apparel?.WornApparelCount ?? 0;
            int returnTaskBuffer = rule?.ReturnTaskBuffer ?? 0;
            int currentJobLoadId = pawn.CurJob?.loadID ?? -1;
            int pendingWorkLoadId = state.PendingWorkJob?.loadID ?? -1;
            if (StatusCache.TryGetValue(pawn, out CachedStatus cached) &&
                Time.realtimeSinceStartup - cached.CreatedAt < CacheSeconds &&
                cached.Transition == state.Transition &&
                cached.RuleId == state.ActiveRuleId &&
                cached.OriginalCount == originalCount &&
                cached.AutomaticCount == automaticCount &&
                cached.WornCount == wornCount &&
                cached.BufferedTasksCompleted == state.BufferedTasksCompleted &&
                cached.ReturnTaskBuffer == returnTaskBuffer &&
                cached.PendingBufferedJobLoadId == state.PendingBufferedJobLoadId &&
                cached.CurrentJobLoadId == currentJobLoadId &&
                cached.PendingWorkLoadId == pendingWorkLoadId &&
                cached.RecallInterruptPending == state.RecallInterruptPending &&
                cached.Drafted == pawn.Drafted)
            {
                return cached.Text;
            }

            List<ApparelRule> currentRules = (state.CurrentRuleIds ?? new List<string>())
                .Select(component.RuleById)
                .Where(current => current != null)
                .ToList();
            List<ApparelRule> requiredSessionRules = currentRules
                .Concat(rule == null
                    ? Enumerable.Empty<ApparelRule>()
                    : new[] { rule })
                .Concat((state.NestedRuleBuffers ?? new List<NestedRuleBufferState>())
                    .Where(progress => progress != null && !progress.Finished)
                    .Select(progress => component.RuleById(progress.RuleId))
                    .Where(candidate => candidate != null))
                .GroupBy(candidate => candidate.Id)
                .Select(group => group.First())
                .ToList();
            string transition = state.RecallInterruptPending
                ? "Recall pending"
                : TransitionLabel(
                    pawn, state, requiredSessionRules, returnTaskBuffer);
            string text = $"Automatic Outfit Manager: {transition}";
            if (currentRules.Count > 1)
                text += $"\nRules: {string.Join(" → ", currentRules.Select(current => current.Name))}";
            else if (currentRules.Count == 1)
                text += $"\nRule: {currentRules[0].Name}";
            else if (rule != null)
                text += $"\nRule: {rule.Name}";

            // Show nested buffers as soon as an overlapping rule is selected,
            // including while the pawn is still outfitting. The persisted
            // buffer state is intentionally created only when prepared work can
            // really start, but the hover should still expose the configured
            // nested buffer at 0/N instead of appearing to omit it.
            var nestedProgressByRule = (state.NestedRuleBuffers ??
                    new List<NestedRuleBufferState>())
                .Where(nested => nested != null && !string.IsNullOrEmpty(nested.RuleId))
                .GroupBy(nested => nested.RuleId)
                .ToDictionary(group => group.Key, group => group.First());
            List<string> nestedRuleIds = currentRules
                .Where(current => current.Id != state.ActiveRuleId)
                .Select(current => current.Id)
                .Concat(nestedProgressByRule.Keys)
                .Distinct()
                .ToList();
            var bufferStatuses = new List<string>();
            int completedOuterBufferCount = state.BufferedTasksCompleted;
            if (rule != null)
            {
                bufferStatuses.Add(BufferStatus(
                    rule.Name, completedOuterBufferCount,
                    returnTaskBuffer, false));
            }
            else
            {
                bufferStatuses.Add(BufferStatus(
                    null, completedOuterBufferCount,
                    returnTaskBuffer, false));
            }
            foreach (string nestedRuleId in nestedRuleIds)
            {
                ApparelRule nestedRule = component.RuleById(nestedRuleId);
                if (nestedRule != null)
                {
                    bool hasProgress = nestedProgressByRule.TryGetValue(
                        nestedRuleId, out NestedRuleBufferState nested);
                    int completedNestedBufferCount =
                        hasProgress ? nested.Completed : 0;
                    bufferStatuses.Add(BufferStatus(
                        nestedRule.Name,
                        completedNestedBufferCount,
                        nestedRule.ReturnTaskBuffer,
                        hasProgress && nested.Finished));
                }
            }
            text += $"\nBuffers: {string.Join(" · ", bufferStatuses)}";

            string detail = DetailFor(
                pawn, state, rule, requiredSessionRules);
            if (!string.IsNullOrEmpty(detail))
                text += $"\n{detail}";

            StatusCache[pawn] = new CachedStatus
            {
                CreatedAt = Time.realtimeSinceStartup,
                Transition = state.Transition,
                RuleId = state.ActiveRuleId,
                OriginalCount = originalCount,
                AutomaticCount = automaticCount,
                WornCount = wornCount,
                BufferedTasksCompleted = state.BufferedTasksCompleted,
                ReturnTaskBuffer = returnTaskBuffer,
                PendingBufferedJobLoadId = state.PendingBufferedJobLoadId,
                CurrentJobLoadId = currentJobLoadId,
                PendingWorkLoadId = pendingWorkLoadId,
                RecallInterruptPending = state.RecallInterruptPending,
                Drafted = pawn.Drafted,
                Text = text
            };
            return text;
        }

        private static string TransitionLabel(
            Pawn pawn,
            PawnApparelState state,
            List<ApparelRule> requiredSessionRules,
            int returnTaskBuffer)
        {
            ApparelTransition transition = state.Transition;
            Job currentJob = pawn?.CurJob;
            if (transition == ApparelTransition.Restoring)
            {
                if (pawn?.Drafted == true)
                    return "Restoration paused — drafted";

                if (currentJob?.def == JobDefOf.LayDown)
                    return "Restoration paused — sleeping or resting";

                if (currentJob?.playerForced == true &&
                    currentJob.def != JobDefOf.Wear &&
                    currentJob.def != JobDefOf.RemoveApparel &&
                    currentJob.def != JobDefOf.Equip &&
                    currentJob.def != JobDefOf.DropEquipment)
                {
                    return "Restoration paused — forced order";
                }
            }

            if (transition == ApparelTransition.Active &&
                returnTaskBuffer > 0 &&
                currentJob != null &&
                currentJob.loadID == state.PendingBufferedJobLoadId)
            {
                int completed = System.Math.Max(
                    0, System.Math.Min(
                        state.BufferedTasksCompleted, returnTaskBuffer));
                return $"Buffered tasks {completed} of {returnTaskBuffer} complete; " +
                       $"current: {JobActivity(pawn, currentJob)}";
            }

            switch (transition)
            {
                case ApparelTransition.Preparing:
                    string pendingActivity = state.PendingWorkJob == null
                        ? null
                        : JobActivity(pawn, state.PendingWorkJob);
                    if (state.WeaponRestorationRequested &&
                        (currentJob?.def == JobDefOf.Equip ||
                         currentJob?.def == JobDefOf.DropEquipment))
                    {
                        return "Returning nested work weapon";
                    }
                    if (state.NestedRuleBuffers?.Any(progress =>
                            progress?.Finished == true) == true &&
                        (currentJob?.def == JobDefOf.RemoveApparel ||
                         PawnJobTracker_StartJob_Patch
                             .IsChangingAreaTravelJob(currentJob)))
                    {
                        return "Returning nested work apparel";
                    }
                    if (currentJob?.def == JobDefOf.Equip)
                        return pendingActivity == null
                            ? $"Equipping required weapon: {JobActivity(pawn, currentJob)}"
                            : $"Equipping required weapon for: {pendingActivity}";
                    if (currentJob?.def == JobDefOf.Wear)
                    {
                        return pendingActivity == null
                            ? $"Equipping required apparel: {JobActivity(pawn, currentJob)}"
                            : $"Equipping required apparel for: {pendingActivity}";
                    }
                    if (pendingActivity != null)
                        return $"Preparing for: {pendingActivity}";
                    return PreparingRequirementsLabel(requiredSessionRules);
                case ApparelTransition.Active:
                    if (requiredSessionRules.Any(candidate =>
                            RuleEvaluator.HasMissingRequiredApparel(pawn, candidate)) ||
                        (!state.WeaponRuleOverrideExplicit &&
                         requiredSessionRules.Any(candidate =>
                             RuleEvaluator.HasMissingRequiredWeapon(pawn, candidate))))
                    {
                        return "Required outfit item missing";
                    }
                    if (IsIdleJob(pawn, currentJob))
                        return "Waiting with work outfit";
                    NestedRuleBufferState currentNestedBuffer = currentJob == null
                        ? null
                        : state.NestedRuleBuffers?.FirstOrDefault(progress =>
                            progress != null && !progress.Finished &&
                            progress.PendingJobLoadId == currentJob.loadID);
                    if (currentNestedBuffer != null)
                    {
                        ApparelRule nestedRule = AutomaticOutfitManagerGameComponent.Current?
                            .RuleById(currentNestedBuffer.RuleId);
                        int maximum = System.Math.Max(
                            0, nestedRule?.ReturnTaskBuffer ?? 0);
                        int completed = System.Math.Max(
                            0, System.Math.Min(
                                currentNestedBuffer.Completed, maximum));
                        return $"Buffered tasks {completed} of {maximum} complete; " +
                               $"current: {JobActivity(pawn, currentJob)}";
                    }
                    if (PausedAreaWorkFilter.IsHaulingJob(currentJob))
                        return $"Hauling: {JobActivity(pawn, currentJob)}";
                    if (IsManagedWorkStatusJob(currentJob, state) &&
                        RuleEvaluator.MatchingRules(pawn, currentJob).Count > 0)
                    {
                        return $"Working: {JobActivity(pawn, currentJob)}";
                    }
                    if (currentJob != null && requiredSessionRules.Any(candidate =>
                            PausedAreaWorkFilter.MatchesCurrentProtectedTransitRule(
                                pawn, currentJob, candidate)))
                    {
                        return $"Protected transit: {JobActivity(pawn, currentJob)}";
                    }
                    if (PawnJobTracker_StartJob_Patch
                            .IsChangingAreaTravelJob(currentJob) ||
                        pawn?.pather?.Moving == true)
                    {
                        return $"Traveling: {JobActivity(pawn, currentJob)}";
                    }
                    if (currentJob != null && IsMeaningfulActivity(currentJob))
                        return $"Active: {JobActivity(pawn, currentJob)}";
                    return "Outfit requirements met";
                case ApparelTransition.ReturningToChangingArea:
                    return "Returning to locker room";
                case ApparelTransition.Restoring:
                    if (currentJob?.def == JobDefOf.DropEquipment)
                        return "Returning temporary work weapon";
                    if (currentJob?.def == JobDefOf.Equip)
                        return $"Restoring saved weapon: {JobActivity(pawn, currentJob)}";
                    if (currentJob?.def == JobDefOf.RemoveApparel)
                        return "Returning managed apparel";
                    if (IsIdleJob(pawn, currentJob))
                        return "Waiting for saved outfit item";
                    return currentJob?.def == JobDefOf.Wear
                        ? $"Restoring saved apparel: {JobActivity(pawn, currentJob)}"
                        : "Restoring saved outfit";
                default:
                    return transition.ToString();
            }
        }

        private static string DetailFor(
            Pawn pawn,
            PawnApparelState state,
            ApparelRule rule,
            List<ApparelRule> requiredSessionRules)
        {
            if (state.Transition == ApparelTransition.Preparing)
            {
                if (state.NestedRuleBuffers?.Any(progress =>
                        progress?.Finished == true) == true &&
                    (pawn?.CurJob?.def == JobDefOf.RemoveApparel ||
                     PawnJobTracker_StartJob_Patch
                         .IsChangingAreaTravelJob(pawn?.CurJob)))
                {
                    // The transition title and compact buffer summary already
                    // describe this state; repeating it made the hover grow by
                    // another wrapped line without adding actionable detail.
                    return null;
                }

                List<ThingDef> missing = requiredSessionRules
                    .SelectMany(candidate => RuleEvaluator.MissingRequiredApparel(pawn, candidate))
                    .Where(def => def != null)
                    .Distinct()
                    .ToList();
                bool missingWeapon = requiredSessionRules.Any(candidate =>
                    RuleEvaluator.HasMissingRequiredWeapon(pawn, candidate));
                if (missingWeapon && state.WeaponRuleOverrideExplicit)
                    return "Player weapon override retained; required primary weapon is not equipped.";
                var missingLabels = missing.Select(def => def.LabelCap.ToString()).ToList();
                if (missingWeapon)
                    missingLabels.Add("required primary weapon");
                return missingLabels.Count == 0
                    ? "Waiting for the work job to resume."
                    : $"Still needed: {string.Join(", ", missingLabels)}";
            }

            if (state.Transition == ApparelTransition.ReturningToChangingArea)
                return rule?.ChangingArea == null ? null : $"Destination: {rule.ChangingArea.Label}";

            if (state.Transition == ApparelTransition.Active)
            {
                List<ThingDef> missing = requiredSessionRules
                    .SelectMany(candidate => RuleEvaluator.MissingRequiredApparel(pawn, candidate))
                    .Where(def => def != null)
                    .Distinct()
                    .ToList();
                bool missingWeapon = requiredSessionRules.Any(candidate =>
                    RuleEvaluator.HasMissingRequiredWeapon(pawn, candidate));
                if (missingWeapon && state.WeaponRuleOverrideExplicit)
                    return "Player weapon override retained; required primary weapon is not equipped.";
                var missingLabels = missing.Select(def => def.LabelCap.ToString()).ToList();
                if (missingWeapon)
                    missingLabels.Add("required primary weapon");
                return missingLabels.Count == 0
                    ? null
                    : $"Still needed: {string.Join(", ", missingLabels)}";
            }

            if (state.Transition != ApparelTransition.Restoring)
                return null;

            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            foreach (Apparel worn in pawn.apparel?.WornApparel ??
                         new List<Apparel>())
            {
                if (state.ManagedApparel?.Contains(worn) != true)
                    continue;

                Pawn savedOwner = component?.RestoringOwnerForSavedGear(worn);
                if (savedOwner != null && savedOwner != pawn)
                {
                    return $"Releasing saved apparel: {worn.LabelCap} for " +
                           $"{savedOwner.LabelShortCap}.";
                }
            }

            Apparel missingItem = state.OriginalApparel.FirstOrDefault(item =>
                item != null && !item.Destroyed &&
                pawn.apparel?.WornApparel.Contains(item) != true);
            if (missingItem == null && state.WeaponInterventionActive &&
                state.OriginalWeapon != null &&
                pawn.equipment?.Primary != state.OriginalWeapon)
            {
                return $"Waiting for saved weapon: {state.OriginalWeapon.LabelCap} — " +
                       UnavailableWeaponReason(pawn, state.OriginalWeapon);
            }

            if (missingItem == null)
                return "Finishing the outfit change.";

            return $"Waiting for saved apparel: {missingItem.LabelCap} — {UnavailableReason(pawn, missingItem)}";
        }

        private static string JobActivity(Pawn pawn, Job job)
        {
            // A Job returned to RimWorld's pool can remain briefly observable
            // during error recovery with its def already cleared. Status text
            // must not ask such a Job to construct a driver/report.
            if (job?.def == null)
                return "Task";

            string activity = job.GetReport(pawn);
            if (string.IsNullOrEmpty(activity))
                activity = job.def.label ?? "Task";
            return activity.CapitalizeFirst();
        }

        private static bool IsIdleJob(Pawn pawn, Job job)
        {
            if (job == null)
                return true;

            string defName = job.def?.defName ?? string.Empty;
            bool waitFamily = job.def == JobDefOf.Wait ||
                              job.def == JobDefOf.Wait_Wander ||
                              defName.StartsWith("Wait", System.StringComparison.OrdinalIgnoreCase) ||
                              defName.IndexOf("Standing", System.StringComparison.OrdinalIgnoreCase) >= 0;
            return waitFamily &&
                   pawn?.pather?.Moving != true &&
                   pawn?.carryTracker?.CarriedThing == null;
        }

        private static bool IsMeaningfulActivity(Job job)
        {
            if (job?.def == null)
                return false;

            string defName = job.def.defName ?? string.Empty;
            return !PawnJobTracker_StartJob_Patch
                       .IsChangingAreaTravelJob(job) &&
                   job.def != JobDefOf.Wait &&
                   job.def != JobDefOf.Wait_Wander &&
                   !defName.StartsWith("Goto", System.StringComparison.OrdinalIgnoreCase) &&
                   !defName.StartsWith("Wait", System.StringComparison.OrdinalIgnoreCase) &&
                   defName.IndexOf("Standing", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsManagedWorkStatusJob(
            Job job, PawnApparelState state)
        {
            if (job?.def == null || !IsMeaningfulActivity(job) ||
                PausedAreaWorkFilter.IsHaulingJob(job))
                return false;

            bool isPendingContinuation = state?.PendingWorkJob != null &&
                (ReferenceEquals(job, state.PendingWorkJob) ||
                 job.loadID == state.PendingWorkJob.loadID) &&
                state.PendingWorkIsManagedWork;
            return job.workGiverDef != null ||
                   job.jobGiver is JobGiver_Work ||
                   job.playerForced ||
                   isPendingContinuation ||
                   (!string.IsNullOrEmpty(state?.LastManagedWorkJobDefName) &&
                    string.Equals(job.def.defName, state.LastManagedWorkJobDefName,
                        System.StringComparison.Ordinal));
        }

        private static string BufferStatus(
            string name, int completed, int maximum, bool finished)
        {
            maximum = System.Math.Max(0, maximum);
            completed = System.Math.Max(0, System.Math.Min(completed, maximum));
            string status = $"{completed}/{maximum}";
            if (finished)
                status += " complete";
            return string.IsNullOrEmpty(name) ? status : $"{name} {status}";
        }

        private static string UnavailableReason(Pawn pawn, Apparel apparel)
        {
            Pawn wearer = Find.Maps
                .SelectMany(map => map.mapPawns.AllPawnsSpawned)
                .FirstOrDefault(candidate => candidate != pawn &&
                    candidate.apparel?.WornApparel.Contains(apparel) == true);
            if (wearer != null)
                return $"currently worn by {wearer.LabelShortCap}";

            if (!apparel.Spawned)
                return "inside an inventory or container";
            if (apparel.Map != pawn.Map)
                return "on another map";
            if (apparel.IsForbidden(pawn))
                return "forbidden";
            if (!pawn.CanReserve(apparel))
                return "reserved by another task";
            if (!pawn.CanReach(apparel, PathEndMode.ClosestTouch, Danger.Deadly))
                return "unreachable";
            return "ready to retrieve";
        }

        private static string PreparingRequirementsLabel(
            List<ApparelRule> requiredSessionRules)
        {
            bool apparel = requiredSessionRules?.Any(candidate =>
                candidate?.RequiredApparel?.Any(def => def?.apparel != null) == true) == true;
            bool weapon = requiredSessionRules?.Any(candidate =>
                candidate?.HasWeaponRequirement == true) == true;

            if (apparel && weapon)
                return "Preparing required apparel and primary weapon";
            if (weapon)
                return "Preparing required primary weapon";
            if (apparel)
                return "Preparing required apparel";
            return "Preparing managed outfit";
        }

        private static string UnavailableWeaponReason(
            Pawn pawn, ThingWithComps weapon)
        {
            Pawn user = Find.Maps
                .SelectMany(map => map.mapPawns.AllPawnsSpawned)
                .FirstOrDefault(candidate => candidate != pawn &&
                    candidate.equipment?.Primary == weapon);
            if (user != null)
                return $"currently equipped by {user.LabelShortCap}";

            Pawn carrier = HoldingPawnFor(weapon);
            if (carrier != null && carrier != pawn)
                return $"carried by {carrier.LabelShortCap}";

            if (!weapon.Spawned)
                return "inside an inventory or container";
            if (weapon.Map != pawn.Map)
                return "on another map";
            if (weapon.IsForbidden(pawn))
                return "forbidden";
            if (!pawn.CanReserve(weapon))
                return "reserved by another task";
            if (!pawn.CanReach(weapon, PathEndMode.ClosestTouch, Danger.Deadly))
                return "unreachable";
            if (!RestorationPlanner.CanAttemptSavedWeaponEquip(
                    weapon, pawn, out string cantReason))
            {
                return string.IsNullOrEmpty(cantReason)
                    ? "cannot be equipped"
                    : cantReason;
            }
            return "ready to retrieve";
        }

        private static Pawn HoldingPawnFor(Thing thing)
        {
            IThingHolder holder = thing?.ParentHolder;
            while (holder != null)
            {
                if (holder is Pawn pawn)
                    return pawn;
                holder = holder.ParentHolder;
            }

            return null;
        }
    }
}
