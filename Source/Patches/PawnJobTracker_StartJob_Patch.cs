using System.Collections.Generic;
using System.Linq;
using System;
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
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    public static class PawnJobTracker_StartJob_Patch
    {
        private const int RepeatedDiagnosticInterval = 6000;
        private const int GuestRepeatedDiagnosticInterval = 60000;
        private static readonly Dictionary<string, int> LastRepeatedDiagnosticTick =
            new Dictionary<string, int>();
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

        public static void Prefix(
            Pawn_JobTracker __instance,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref JobTag? tag,
            bool fromQueue)
        {
            if (newJob == null)
                return;

            Pawn pawn = PawnField(__instance);
            if (pawn == null)
                return;

            AutomaticOutfitManagerGameComponent component = AutomaticOutfitManagerGameComponent.Current;
            PawnApparelState state = component?.StateFor(pawn);

            // Locker travel is an exact AOM transition, not an ordinary Goto.
            // Recognize the recorded destination before generic reservation,
            // paused-area, and activity guards can turn it into Wait. Natural
            // task-buffer completion does not set RecallRequested, so using the
            // recall flag as the ownership marker stranded automatic returns.
            if (IsAssignedChangingAreaReturnJob(state, newJob))
                return;

            // A storage classification can change while a hauling job waits in
            // the think-tree or queue. Recheck the concrete destination at the
            // shared job boundary so an explicit stock-type Forget cannot let
            // an already-selected automatic haul deposit gear into storage that
            // now rejects it. Player-forced hauling remains authoritative.
            if (!newJob.playerForced && HaulDestinationRejectsGear(pawn, newJob))
            {
                __instance.ClearQueuedJobs(false);
                ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                return;
            }

            // A hauling or inventory job can be chosen immediately before the
            // saved item's owner enters restoration. Do not let a new automatic
            // job take that exact item away while the owner is waiting for it.
            // Player-forced orders remain authoritative.
            if (!newJob.playerForced &&
                component?.RestoringOwnerForJobTarget(
                    pawn, newJob, out Thing restoringSavedGear) is Pawn restoringOwner)
            {
                if (Prefs.DevMode && ShouldLogRepeatedDiagnostic(
                        pawn,
                        $"saved-gear-restoring:{restoringSavedGear.GetUniqueLoadID()}"))
                {
                    Log.Message(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: ignored automatic " +
                        $"{newJob.def.defName} for {restoringSavedGear.LabelCap}; " +
                        $"{restoringOwner.LabelShortCap} is restoring that exact saved item.");
                }

                __instance.ClearQueuedJobs(false);
                ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                return;
            }

            // A work candidate can be selected just before another pawn claims
            // the same target for an outfit transition. Recheck at the common
            // job boundary so that candidate cannot start a second transition
            // in the small window between scanner and StartJob.
            if (ManagedWorkClaimRegistry.IsClaimedByOther(pawn, newJob))
            {
                __instance.ClearQueuedJobs(false);
                ReplaceWithWait(pawn, 60, ref newJob, ref jobGiver, ref tag);
                return;
            }

            // Some modded robot thinkers start their wander job without passing
            // through the vanilla ThinkNode_JobGiver result patch. At this
            // boundary the originating giver is still available, so redirect
            // the job before it can enter the restricted area. The helper
            // converts it to a safe GotoWander destination and avoids the
            // cancel/reselect loop seen at doorways.
            if (PausedAreaWorkFilter.TryRedirectWanderingJob(pawn, newJob, jobGiver))
            {
                jobGiver = null;
                tag = null;
            }

            ApparelRule deniedWorkRule =
                PausedAreaWorkFilter.DeniedOrdinaryWorkRule(pawn, newJob);
            if (deniedWorkRule != null)
            {
                if (ShouldLogRepeatedDiagnostic(
                        pawn, $"work-disabled:{deniedWorkRule.Id}"))
                {
                    string category = PawnAccessClassifier.IsHostedGuest(pawn) ? "guest work" : "work";
                    Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: blocked from '{deniedWorkRule.Name}'; {category} is disabled.");
                }
                if (state?.Transition == ApparelTransition.Active &&
                    !state.RecallRequested)
                {
                    // A valid managed session must not be recalled merely
                    // because RimWorld's next proposal is a prohibited work
                    // candidate. Skip that exact proposal and let the native
                    // thinker keep the outfit active for other legal work.
                    UnavailableWorkRegistry.Block(pawn, deniedWorkRule, newJob);
                    __instance.ClearQueuedJobs(false);
                    ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                    return;
                }
                if (state != null)
                {
                    component.RequestRecall(state);
                    // Continue through the shared transition path below. Replacing
                    // the denied job with Wait here strands an already-managed
                    // pawn for the whole wait, and repeated denied candidates can
                    // displace every locker-return recovery attempt.
                }
                else
                {
                    UnavailableWorkRegistry.Block(pawn, deniedWorkRule, newJob);
                    __instance.ClearQueuedJobs(false);
                    ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                    return;
                }
            }

            ApparelRule deniedHaulingRule =
                PausedAreaWorkFilter.DeniedHaulingRule(pawn, newJob);
            if (deniedHaulingRule != null)
            {
                if (ShouldLogRepeatedDiagnostic(
                        pawn, $"hauling-disabled:{deniedHaulingRule.Id}"))
                {
                    string category = PawnAccessClassifier.IsHostedGuest(pawn)
                        ? "guest hauling"
                        : "hauling";
                    Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: blocked from '{deniedHaulingRule.Name}'; {category} is disabled.");
                }
                if (state?.Transition == ApparelTransition.Active &&
                    !state.RecallRequested)
                {
                    // A denied haul candidate is not a request to end valid
                    // managed work. Remember only that concrete haul and let
                    // RimWorld select other work without an outfit round-trip.
                    UnavailableWorkRegistry.Block(pawn, deniedHaulingRule, newJob);
                    __instance.ClearQueuedJobs(false);
                    ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                    return;
                }
                if (state != null)
                {
                    component.RequestRecall(state);
                    // Let the active state replace this denied haul with its real
                    // locker/restoration transition instead of a Standing wait.
                }
                else
                {
                    UnavailableWorkRegistry.Block(pawn, deniedHaulingRule, newJob);
                    __instance.ClearQueuedJobs(false);
                    ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                    return;
                }
            }

            // Most autonomous jobs are rejected by the work-scanner and
            // ThinkNode patches before assignment. Some vanilla and modded job
            // givers bypass those shared paths, especially when the job target
            // is outside a paused area but its route crosses the area. Recheck
            // the concrete job here so it cannot walk to the boundary, get
            // cancelled by the path guard, and immediately select the same job
            // again. A bounded wait also prevents a no-job retry storm when no
            // safe alternative work is currently available.
            ApparelRule deniedPausedAreaRule =
                PausedAreaWorkFilter.DeniedPausedAreaRule(pawn, newJob);
            if (deniedPausedAreaRule != null)
            {
                if (Prefs.DevMode && ShouldLogRepeatedDiagnostic(
                        pawn, $"paused-work-start:{newJob.def?.defName}"))
                {
                    Log.Message(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: blocked " +
                        $"{newJob.def?.defName ?? "job"} before it could enter a paused work area.");
                }

                if (state != null)
                {
                    component.RequestRecall(state);
                    // The managed-state block below performs the exact return
                    // and restoration. A state-less pawn still receives the
                    // bounded retry used for ordinary paused-area rejection.
                }
                else
                {
                    UnavailableWorkRegistry.Block(
                        pawn, deniedPausedAreaRule, newJob);
                    __instance.ClearQueuedJobs(false);
                    ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                    return;
                }
            }

            // Keep guests and other non-colony pawns from reserving, hauling,
            // repairing, processing, or wearing managed apparel. Checking the
            // common job boundary makes this work for native and modded jobs,
            // including bills that place their ingredient in a target queue.
            if (pawn.Faction != Faction.OfPlayer && JobTargetsManagedApparel(newJob) &&
                !IsAssignedTransitionApparelJob(state, newJob))
            {
                Job waitJob = JobMaker.MakeJob(JobDefOf.Wait);
                waitJob.expiryInterval = 30;
                newJob = waitJob;
                jobGiver = null;
                tag = null;
                return;
            }

            // Access controls also apply to animals, mechs, and modded robots,
            // but apparel intervention does not. Clear any legacy state created
            // for a non-humanlike unit and leave its real job/status untouched.
            if (pawn.RaceProps?.Humanlike != true || pawn.apparel == null)
            {
                if (state != null)
                    component.EndIntervention(pawn);
                return;
            }

            if (state?.WeaponInterventionActive == true &&
                IsExternalWeaponControlJob(newJob))
            {
                if (state.Transition == ApparelTransition.Restoring)
                    state.AbandonWeaponManagementForOverride();
                else
                    state.MarkWeaponPlayerOverride();
                if (Prefs.DevMode)
                    Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: {newJob.def.defName} is controlling weapons; the current choice is retained and the saved primary remains available for outfit restoration.");
                return;
            }

            if (newJob.def == JobDefOf.Equip &&
                newJob.targetA.Thing is ThingWithComps equipTarget &&
                equipTarget.def?.IsWeapon == true &&
                ((pawn.Faction != Faction.OfPlayer &&
                  (component?.IsManagedWeapon(equipTarget) == true ||
                   ManagedWeaponClassifier.Matches(equipTarget.def)) &&
                  state?.IsManagedWeapon(equipTarget) != true) ||
                 component?.IsSavedWeaponForOtherPawn(equipTarget, pawn) == true ||
                 component?.IsManagedWeaponAssignedToOtherPawn(equipTarget, pawn) == true))
            {
                ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                return;
            }

            if ((newJob.def == JobDefOf.Equip ||
                 newJob.def == JobDefOf.DropEquipment) &&
                newJob.targetA.Thing is ThingWithComps equipmentTarget &&
                equipmentTarget.def?.IsWeapon == true)
            {
                bool assignedTransition =
                    IsAssignedTransitionWeaponJob(state, newJob);
                if (newJob.def == JobDefOf.Equip &&
                    ManagedWeaponClassifier.Matches(equipmentTarget.def) &&
                    !newJob.playerForced && !assignedTransition)
                {
                    LogAutomaticManagedGearRejection(
                        pawn, newJob, equipmentTarget, "job start");
                    component?.NotifyRejectedManagedGearJob(pawn);
                    ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                    return;
                }
                if (state?.WeaponInterventionActive == true && !assignedTransition)
                {
                    if (state.Transition == ApparelTransition.Restoring)
                        state.AbandonWeaponManagementForOverride();
                    else
                        state.MarkWeaponPlayerOverride();
                    if (Prefs.DevMode)
                        Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: {newJob.def.defName} selected by the player or another mod; the choice is retained until saved-outfit restoration.");
                }

                if (assignedTransition && state?.WeaponPlayerOverride == true &&
                    state.Transition == ApparelTransition.Preparing &&
                    newJob.def == JobDefOf.Equip &&
                    state.IsManagedWeapon(equipmentTarget))
                {
                    ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                    return;
                }

                if (assignedTransition && state?.RecallRequested == true &&
                    state.Transition == ApparelTransition.Preparing &&
                    newJob.def == JobDefOf.Equip && state.IsManagedWeapon(equipmentTarget))
                {
                    __instance.ClearQueuedJobs(false);
                    state.PendingWorkJob = null;
                    state.PendingWorkIsManagedWork = false;
                    ManagedWorkClaimRegistry.ReleaseAll(pawn);
                    ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                    return;
                }

                // Weapon jobs are either an exact transition step or an external
                // player/mod decision. Neither should be reinterpreted as work
                // merely because its target lies inside a managed area.
                return;
            }

            if (state?.WeaponRestorationRequested == true &&
                (pawn.equipment?.Primary == state.OriginalWeapon ||
                 (state.OriginalWeapon == null && pawn.equipment?.Primary == null)))
            {
                state.CompleteWeaponRestoration();
            }

            if (state?.WeaponInterventionActive == true &&
                state.Transition == ApparelTransition.Active &&
                !state.IsManagedWeapon(pawn.equipment?.Primary))
            {
                state.MarkWeaponPlayerOverride();
                if (Prefs.DevMode)
                    Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: weapon changed outside Automatic Outfit Manager; the new choice is retained until saved-outfit restoration.");
            }

            if (newJob.def == JobDefOf.Wear &&
                newJob.targetA.Thing is Apparel wearTarget &&
                !ManagedApparelClassifier.Matches(wearTarget.def) &&
                component?.IsSavedForOtherPawn(wearTarget, pawn) == true)
            {
                ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                return;
            }

            if (newJob.def == JobDefOf.Wear &&
                newJob.targetA.Thing is Apparel managedWearTarget &&
                ManagedApparelClassifier.Matches(managedWearTarget.def) &&
                !newJob.playerForced &&
                !IsAssignedTransitionApparelJob(state, newJob))
            {
                LogAutomaticManagedGearRejection(
                    pawn, newJob, managedWearTarget, "job start");
                component?.NotifyRejectedManagedGearJob(pawn);
                ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                return;
            }

            if (newJob.def == JobDefOf.Wear &&
                newJob.targetA.Thing is Apparel assignedWearTarget &&
                component?.IsManagedApparelAssignedToOtherPawn(assignedWearTarget, pawn) == true)
            {
                ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                return;
            }

            if (newJob.def == JobDefOf.Wear &&
                newJob.targetA.Thing is Apparel transitionWearTarget &&
                state != null &&
                !IsAllowedTransitionWear(state, transitionWearTarget))
            {
                ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                return;
            }

            if (state?.RecallRequested == true &&
                state.Transition == ApparelTransition.Preparing &&
                ((newJob.def == JobDefOf.Wear &&
                  newJob.targetA.Thing is Apparel queuedAutomaticOutfitManager &&
                  state.ManagedApparel?.Contains(queuedAutomaticOutfitManager) == true) ||
                 (newJob.def == JobDefOf.Equip &&
                  newJob.targetA.Thing is ThingWithComps queuedManagedWeapon &&
                  state.IsManagedWeapon(queuedManagedWeapon))))
            {
                // The current assigned apparel step was allowed to finish so
                // RimWorld could leave its layers in a consistent state. Drop
                // the rest of the preparation queue now; the brief trigger job
                // will enter the ordinary recall/restoration path on the next
                // selection without ever starting the intercepted work.
                __instance.ClearQueuedJobs(false);
                state.PendingWorkJob = null;
                state.PendingWorkIsManagedWork = false;
                ManagedWorkClaimRegistry.ReleaseAll(pawn);
                ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                return;
            }

            if (newJob.def == JobDefOf.Wear ||
                newJob.def == JobDefOf.RemoveApparel ||
                newJob.def == JobDefOf.Equip ||
                newJob.def == JobDefOf.DropEquipment)
                return;

            // Apparel jobs temporarily displace the work that requested them.
            // Resume the exact intercepted job rather than hoping the think tree
            // can reconstruct a bill, construction, or hauling job from only
            // its target. This also avoids clearing the queued continuation in
            // a sequence of handoff Wait jobs.
            if (state?.Transition == ApparelTransition.Preparing &&
                HasCompletedPreparation(pawn, component, state) &&
                state.PendingWorkJob != null &&
                !SameJob(newJob, state.PendingWorkJob))
            {
                string cancellationReason = PendingWorkCancellationReason(
                    pawn, state, newJob);
                if (cancellationReason != null)
                {
                    if (Prefs.DevMode)
                        Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: pending work continuation was cancelled ({cancellationReason}); returning to normal transition logic.");
                    state.PendingWorkJob = null;
                    state.PendingWorkIsManagedWork = false;
                    ManagedWorkClaimRegistry.ReleaseAll(pawn);
                }
                else
                {
                    Job resumedJob = state.PendingWorkJob;
                    __instance.ClearQueuedJobs(false);
                    newJob = resumedJob;
                    jobGiver = null;
                    tag = null;
                    if (Prefs.DevMode)
                        Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: resuming exact prepared job {newJob.def.defName} for '{ManagedWorkClaimRegistry.DescribeActiveClaim(pawn)}'.");
                }
            }

            // Work in overlapping areas must satisfy the combined equipment
            // requirements before it begins. Previously the active outer rule
            // accepted the job and the path-cell safety check discovered the
            // missing nested gear at the doorway, causing an immediate
            // stop/reselect loop.
            bool hasManagedWorkContext = HasManagedWorkContext(
                newJob, jobGiver, state);
            bool haulingActivity = PausedAreaWorkFilter.IsHaulingJob(newJob);
            List<ApparelRule> protectedJobRules = ProtectedRulesForJob(pawn, newJob);
            List<ApparelRule> matchingWorkRules =
                hasManagedWorkContext && !haulingActivity
                ? RuleEvaluator.MatchingRules(pawn, newJob)
                : new List<ApparelRule>();
            bool canPrepareForMatchingWork = state == null ||
                state.Transition == ApparelTransition.Preparing ||
                state.Transition == ApparelTransition.Active;
            if (canPrepareForMatchingWork && state != null && hasManagedWorkContext &&
                (matchingWorkRules.Count > 0 ||
                 state.Transition != ApparelTransition.Preparing))
            {
                state.CurrentRuleIds = matchingWorkRules
                    .Where(rule => rule != null)
                    .Select(rule => rule.Id)
                    .Distinct()
                    .ToList();
            }
            if (canPrepareForMatchingWork && state != null && matchingWorkRules.Count > 0)
            {
                // Do not create a nested buffer merely because a candidate was
                // intercepted. Contested hauling/construction candidates can
                // disappear while the pawn changes. Record entry only when the
                // combined outfit is complete and the prepared work can start.
                if (HasCompletedPreparation(pawn, component, state))
                    TrackNestedRuleEntries(state, matchingWorkRules);
            }
            // A nested buffer remains active after its work area stops matching.
            // Give it the same semantics as the outer buffer: the next meaningful
            // jobs consume its slots wherever RimWorld sends the pawn. Restricting
            // this call to matching outer-area work made the nested session vanish
            // without ever recording work outside that area.
            if (canPrepareForMatchingWork && state?.NestedRuleBuffers?.Count > 0 &&
                HandleNestedRuleBuffers(
                    __instance, pawn, component, state, protectedJobRules,
                    ref newJob, ref jobGiver, ref tag))
            {
                return;
            }
            if (canPrepareForMatchingWork && matchingWorkRules.Count > 0 &&
                TryPrepareForMatchingRules(
                    __instance, pawn, component, matchingWorkRules,
                    ref newJob, ref jobGiver, ref tag))
            {
                return;
            }
            if (canPrepareForMatchingWork && matchingWorkRules.Count > 0)
            {
                ManagedWorkClaimRegistry.Release(pawn, newJob);
                if (state != null && SameJob(newJob, state.PendingWorkJob))
                {
                    state.PendingWorkJob = null;
                    state.PendingWorkIsManagedWork = false;
                }
            }

            if (state != null)
            {
                // The work target that caused preparation can disappear while
                // the pawn is wearing several apparel items (another pawn may
                // finish it, reserve it, or consume its inputs). Preserve the
                // prepared rule set above and promote the session once every
                // required item is worn. Remaining in Preparing made unrelated
                // thinker jobs bypass both the task buffer and idle recovery,
                // leaving fully equipped pawns standing or returning gear only
                // after another matching job happened to be selected.
                if (state.Transition == ApparelTransition.Preparing &&
                    HasCompletedPreparation(pawn, component, state))
                {
                    state.Transition = ApparelTransition.Active;
                    state.ActiveIdleTicks = 0;
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: preparation complete; equipped rule set is active.");
                    }
                }

                var activeRule = component.RuleById(state.ActiveRuleId);
                if (state.Transition == ApparelTransition.Restoring &&
                    newJob.def == JobDefOf.HaulToCell &&
                    newJob.targetA.Thing is Apparel returnItem &&
                    (state.ManagedApparel?.Contains(returnItem) ?? false))
                {
                    return;
                }

                if (state.Transition == ApparelTransition.Restoring)
                {
                    // A work candidate can be reconsidered while saved apparel
                    // is still being restored. Never let that stale candidate
                    // fall through to the normal missing-work-gear path or the
                    // pawn will alternate forever between the two outfits.
                    int restorationTick = Find.TickManager?.TicksGame ?? 0;
                    if (IsRecoveryWaitJob(newJob))
                        return;

                    if (state.LastRestorationAttemptTick >= 0 &&
                        restorationTick - state.LastRestorationAttemptTick < 600)
                    {
                        // A failed Wear causes RimWorld to start an error-recovery
                        // job immediately. Replanning here used to replace that
                        // recovery with the same Wear hundreds of times in one
                        // tick. Drop stale continuations and yield until the
                        // normal restoration retry window has elapsed.
                        __instance.ClearQueuedJobs(false);
                        ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                        return;
                    }

                    RestorationPlanner.TryMakeHeldOriginalsAccessible(pawn, state);
                    List<Job> pendingRestorationJobs = RestorationPlanner.BuildJobs(
                        pawn, state, activeRule, out bool hasUnavailableSavedApparel);
                    if (pendingRestorationJobs.Count > 0)
                    {
                        state.LastRestorationAttemptTick = restorationTick;
                        state.UnavailableRestorationAttempts = hasUnavailableSavedApparel
                            ? state.UnavailableRestorationAttempts + 1
                            : 0;
                        QueueRestorationJobs(
                            __instance, ref newJob, ref jobGiver, ref tag, pendingRestorationJobs);
                        return;
                    }

                    if (hasUnavailableSavedApparel)
                    {
                        state.LastRestorationAttemptTick = restorationTick;
                        state.UnavailableRestorationAttempts++;
                        __instance.ClearQueuedJobs(false);
                        ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                        return;
                    }

                    __instance.ClearQueuedJobs(false);
                    component.EndIntervention(pawn);
                    ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                    return;
                }

                bool targetsActiveWorkArea = RuleEvaluator.MatchesRule(pawn, newJob, activeRule);
                // Wait/Goto and similar connective jobs can inherit a cell inside
                // the work area after the real task finishes. They still need to
                // be permitted while a pawn is moving through the transition,
                // but they are not fresh work and must not reset or indefinitely
                // hold open the task buffer.
                bool startsMeaningfulWorkInArea = targetsActiveWorkArea &&
                    IsBufferableJob(newJob) && hasManagedWorkContext &&
                    !haulingActivity;
                // Safety follows the area, not the activity label. Any direct
                // destination or protected route for the active rule keeps the
                // full outfit on, including eating, recreation, waiting, and
                // sleeping. Meaningful work still exclusively owns task-buffer
                // resets and worker activity labels below.
                bool matchesActiveRule = protectedJobRules.Any(candidate =>
                    candidate?.Id == activeRule?.Id);
                bool holdsPendingNestedBuffer =
                    state.NestedRuleBuffers?.Any(progress =>
                        progress != null && !progress.Finished) == true &&
                    !state.RecallRequested &&
                    !hasManagedWorkContext &&
                    !RequiresImmediateRestoration(newJob) &&
                    (JobTargetsArea(newJob, activeRule?.Area) ||
                     (IsRecoveryWaitJob(newJob) &&
                      PawnInsideArea(pawn, activeRule?.Area)));

                // The thinker commonly inserts a brief Wait/Goto between the
                // completed nested job and the next meaningful task. Keep the
                // nested outfit through that connective step so the configured
                // buffer can be consumed. The game component still applies its
                // bounded idle timeout; a buffer permits follow-up work but must
                // never make a pawn stand indefinitely waiting for work.
                if (holdsPendingNestedBuffer)
                    return;
                // Only actual work targeting the configured area starts a fresh
                // work session. A connective route that merely crosses the area
                // still requires PPE, but must not erase already-used buffer
                // tasks or a pawn can retain work gear indefinitely.
                if (startsMeaningfulWorkInArea)
                {
                    state.LastManagedWorkJobDefName = newJob.def.defName;
                    if (Prefs.DevMode && state.BufferedTasksCompleted > 0 &&
                        ShouldLogRepeatedDiagnostic(
                            pawn, $"task-buffer-reset:{activeRule?.Id}"))
                        Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: task buffer reset by {newJob.def.defName} in '{activeRule?.Name}'.");
                    state.BufferedTasksCompleted = 0;
                    state.LastBufferedJobLoadId = -1;
                }
                bool shouldLeaveRule = state.RecallRequested || !matchesActiveRule;
                if (shouldLeaveRule &&
                    !state.ApparelInterventionActive &&
                    !state.WeaponInterventionActive)
                {
                    // A tracked-only worker normally needs no locker visit.
                    // Recall is the exception: it is an explicit request to
                    // leave the work area, so let the shared return path below
                    // send the pawn to the configured locker before AOM clears
                    // the session and yields to native job selection.
                    if (!state.RecallRequested)
                    {
                        component.EndIntervention(pawn);
                        return;
                    }
                }

                if (shouldLeaveRule && state.Transition == ApparelTransition.Preparing &&
                    !state.RecallRequested)
                    return;

                if (shouldLeaveRule && !state.RecallRequested &&
                    state.Transition == ApparelTransition.Active &&
                    activeRule != null && activeRule.Enabled && !activeRule.WorkAreaPaused &&
                    activeRule.ReturnTaskBuffer > state.BufferedTasksCompleted &&
                    !RequiresImmediateRestoration(newJob) &&
                    (!hasManagedWorkContext ||
                     RuleEvaluator.MatchingRule(pawn, newJob) == null))
                {
                    // Movement and brief wait jobs are connective AI steps, not
                    // meaningful tasks. Let them pass without consuming the
                    // buffer or causing an outfit swap before the real job starts.
                    if (IsBufferableJob(newJob) &&
                        newJob.loadID != state.LastBufferedJobLoadId)
                    {
                        state.BufferedTasksCompleted++;
                        state.LastBufferedJobLoadId = newJob.loadID;
                        if (Prefs.DevMode)
                            Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: task buffer {state.BufferedTasksCompleted}/{activeRule.ReturnTaskBuffer} used by {newJob.def.defName}.");
                    }
                    return;
                }

                if (shouldLeaveRule)
                {
                    bool insideProtectedArea =
                        PawnInsideStateProtectedArea(pawn, component, state);
                    bool outsidePreferredChangingArea =
                        activeRule?.ChangingArea != null &&
                        !PawnInsideArea(pawn, activeRule.ChangingArea);
                    if ((insideProtectedArea || outsidePreferredChangingArea) &&
                        TryFindRestorationCell(
                            pawn, component, state, out IntVec3 changingCell))
                    {
                        int returnTick = Find.TickManager?.TicksGame ?? 0;
                        if (state.LastChangingAreaReturnAttemptTick >= 0 &&
                            returnTick - state.LastChangingAreaReturnAttemptTick < 30)
                        {
                            // A failed or instantly completed Goto can cause the
                            // interrupted candidate to be reconsidered repeatedly
                            // in one tick. Yield briefly instead of recreating the
                            // same locker-room return until RimWorld's safety cap.
                            __instance.ClearQueuedJobs(false);
                            ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                            return;
                        }

                        state.Transition = ApparelTransition.ReturningToChangingArea;
                        state.LastChangingAreaReturnAttemptTick = returnTick;
                        state.ChangingAreaReturnCell = changingCell;

                        // Recall invalidates the job chosen before the request.
                        // Do not preserve it behind the Goto: if the Goto ends
                        // immediately, that stale job can restart and recursively
                        // create hundreds of identical return jobs in one tick.
                        __instance.ClearQueuedJobs(false);
                        newJob = MakeChangingAreaTravelJob(changingCell);
                        newJob.expiryInterval = 2000;
                        newJob.locomotionUrgency = LocomotionUrgency.Jog;
                        jobGiver = null;
                        tag = null;
                        return;
                    }

                    int currentTick = Find.TickManager?.TicksGame ?? 0;
                    if (state.Transition == ApparelTransition.Restoring &&
                        state.LastRestorationAttemptTick >= 0 &&
                        currentTick - state.LastRestorationAttemptTick < 600)
                    {
                        // A restoration job can fail transiently because an item
                        // is reserved, inside storage, or its path is changing.
                        // Do not convert every newly selected job into Wait while
                        // cooling down; let the pawn perform safe unrelated work
                        // and retry restoration after ten in-game seconds.
                        return;
                    }

                    state.RequestWeaponRestoration();
                    state.ChangingAreaReturnCell = IntVec3.Invalid;
                    RestorationPlanner.TryMakeHeldOriginalsAccessible(pawn, state);
                    List<Job> restorationJobs = RestorationPlanner.BuildJobs(
                        pawn, state, activeRule, out bool hasUnavailableOriginal);
                    if (restorationJobs.Count > 0)
                    {
                        state.Transition = ApparelTransition.Restoring;
                        state.LastRestorationAttemptTick = currentTick;
                        state.UnavailableRestorationAttempts = hasUnavailableOriginal
                            ? state.UnavailableRestorationAttempts + 1
                            : 0;
                        QueueRestorationJobs(__instance, ref newJob, ref jobGiver, ref tag, restorationJobs);

                        if (Prefs.DevMode)
                            Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: restoring saved apparel and primary weapon with {restorationJobs.Count} job(s) before {__instance.curJob?.def?.defName ?? "next job"}.");
                        return;
                    }

                    if (hasUnavailableOriginal)
                    {
                        state.Transition = ApparelTransition.Restoring;
                        state.LastRestorationAttemptTick = currentTick;
                        state.UnavailableRestorationAttempts++;
                        __instance.ClearQueuedJobs(false);
                        ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                        return;
                    }

                    __instance.ClearQueuedJobs(false);
                    bool recalled = state.RecallRequested;
                    component.EndIntervention(pawn);

                    // The candidate was chosen while the recalled work session
                    // still existed. Discard it once, then hand the pawn back to
                    // RimWorld on the next think cycle without any lasting hold.
                    if (recalled)
                    {
                        ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                        return;
                    }

                    if (insideProtectedArea)
                    {
                        // Never remove even one managed layer while the pawn is
                        // still inside a rule that requires it. A temporarily
                        // unreachable exit is safer as a bounded native wait;
                        // the next job selection retries the ordinary return.
                        state.Transition = ApparelTransition.Active;
                        __instance.ClearQueuedJobs(false);
                        ReplaceWithWait(
                            pawn, 300, ref newJob, ref jobGiver, ref tag);
                        return;
                    }

                    // The candidate job was selected before recall/restoration.
                    // Recheck the paused area after clearing the apparel state;
                    // otherwise that stale job can start in the same StartJob
                    // call and bypass the normal work-giver pause filters.
                    if (PausedAreaWorkFilter.ShouldRejectPausedAreaJob(pawn, newJob))
                    {
                        ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                        return;
                    }
                }
            }

            if (newJob.def == JobDefOf.HaulToCell &&
                ManagedApparelClassifier.Matches(newJob.targetA.Thing))
            {
                return;
            }

            // Current occupancy is deliberately included here as a second line
            // of defense. It repairs loaded saves, area edits, forced apparel
            // changes, and gear loss that leave a pawn inside between job starts.
            List<ApparelRule> applicableRules = protectedJobRules
                .Concat(RuleEvaluator.MatchingLocationRules(pawn))
                .Where(candidate => candidate != null)
                .GroupBy(candidate => candidate.Id)
                .Select(group => group.First())
                .ToList();
            if (applicableRules.Count == 0)
                return;

            ApparelRule unwearableRule = applicableRules.FirstOrDefault(candidate =>
                !RuleEvaluator.RuleCanApplyToPawn(pawn, candidate));
            ApparelConflict transitConflict = ApparelCompatibility.FindConflict(
                applicableRules, pawn.RaceProps?.body);
            bool compatibleWeaponRequirements =
                RuleEvaluator.TryCombinedWeaponRequirement(
                    applicableRules, out CombinedWeaponRequirement combinedWeaponRequirement);
            if (unwearableRule != null || transitConflict != null ||
                !compatibleWeaponRequirements)
            {
                foreach (ApparelRule blockedRule in applicableRules)
                    UnavailableWorkRegistry.Block(pawn, blockedRule);
                string reason = unwearableRule != null
                    ? $"required apparel for '{unwearableRule.Name}' cannot be worn"
                    : transitConflict != null
                        ? $"required apparel is incompatible: {transitConflict.Label}"
                        : "overlapping rules require different primary weapons";
                if (ShouldLogRepeatedDiagnostic(
                        pawn, $"unwearable:{string.Join(",", applicableRules.Select(rule => rule.Id))}"))
                {
                    Log.Warning($"[AutomaticOutfitManager] {pawn.LabelShortCap}: delaying {newJob.def.defName}; {reason}.");
                }
                __instance.ClearQueuedJobs(false);
                ReplaceWithWait(pawn, 300, ref newJob, ref jobGiver, ref tag);
                return;
            }

            var requiredByDef = new Dictionary<ThingDef, ApparelRule>();
            foreach (ApparelRule applicableRule in applicableRules)
            {
                foreach (ThingDef def in applicableRule.RequiredApparel ??
                         Enumerable.Empty<ThingDef>())
                {
                    if (def != null && !requiredByDef.ContainsKey(def))
                        requiredByDef.Add(def, applicableRule);
                }
            }
            List<ThingDef> missing = requiredByDef.Keys
                .Where(def => !pawn.apparel.WornApparel.Any(item => item?.def == def))
                .ToList();
            bool missingWeapon = !combinedWeaponRequirement.Matches(
                pawn.equipment?.Primary);
            PawnApparelState weaponState = component?.StateFor(pawn);
            bool weaponChoiceProtected = missingWeapon &&
                (weaponState?.WeaponPlayerOverride == true ||
                 SimpleSidearmsCompatibility.ProtectsCurrentWeaponChoice(pawn));
            if (weaponChoiceProtected)
            {
                missingWeapon = false;
                weaponState ??= component?.BeginIntervention(
                    pawn, applicableRules[0], Enumerable.Empty<Apparel>(), null);
                if (weaponState != null)
                {
                    weaponState.MarkWeaponPlayerOverride();
                    weaponState.CurrentRuleIds = applicableRules
                        .Select(candidate => candidate.Id)
                        .Distinct()
                        .ToList();
                }
                if (ShouldLogRepeatedDiagnostic(
                        pawn, $"weapon-player-control:{string.Join(",", applicableRules.Select(rule => rule.Id))}"))
                {
                    Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: continuing {newJob.def.defName} with the player's current primary weapon; the weapon requirement is skipped while that choice is protected.");
                }
            }
            if (missing.Count == 0 && !missingWeapon)
            {
                PawnApparelState activeState = component?.StateFor(pawn);
                if (activeState == null && matchingWorkRules.Count > 0)
                {
                    activeState = component?.TrackCompliantWorkSession(
                        pawn, newJob, matchingWorkRules);
                }
                if (activeState != null && !activeState.RecallRequested &&
                    (activeState.Transition == ApparelTransition.Preparing ||
                     activeState.Transition == ApparelTransition.Active))
                {
                    activeState.CurrentRuleIds = applicableRules
                        .Select(candidate => candidate.Id)
                        .Distinct()
                        .ToList();
                }
                if (activeState != null &&
                    applicableRules.Any(candidate => candidate.Id == activeState.ActiveRuleId) &&
                    activeState.Transition == ApparelTransition.Preparing &&
                    HasCompletedPreparation(pawn, component, activeState))
                {
                    activeState.Transition = ApparelTransition.Active;
                    if (Prefs.DevMode)
                        Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: preparation complete; equipped rule set is active.");
                }

                // Assigned/player-forced jobs commonly have no workGiverDef and
                // therefore reach this general protection path. Once their exact
                // pending continuation has been restored, release its temporary
                // claim and clear the deep-saved handoff just as the ordinary
                // work-giver path does.
                ManagedWorkClaimRegistry.Release(pawn, newJob);
                if (activeState != null && SameJob(newJob, activeState.PendingWorkJob))
                {
                    activeState.PendingWorkJob = null;
                    activeState.PendingWorkIsManagedWork = false;
                }
                return;
            }

            var transitionJobs = new List<Job>();
            var managedApparel = new List<Apparel>();
            foreach (ThingDef def in missing)
            {
                ApparelRule sourceRule = requiredByDef[def];
                Apparel apparel = ApparelFinder.FindBest(pawn, def, sourceRule.ChangingArea);
                if (apparel == null)
                {
                    UnavailableWorkRegistry.Block(pawn, sourceRule);
                    if (ShouldLogRepeatedDiagnostic(
                            pawn, $"gear-unavailable:{sourceRule.Id}:{def.defName}"))
                    {
                        Log.Warning($"[AutomaticOutfitManager] {pawn.LabelShortCap}: delaying {newJob.def.defName}; no reachable {def.LabelCap} is available for '{sourceRule.Name}'.");
                    }
                    __instance.ClearQueuedJobs(false);
                    ReplaceWithWait(pawn, 300, ref newJob, ref jobGiver, ref tag);
                    return;
                }

                Job wearJob = JobMaker.MakeJob(JobDefOf.Wear, apparel);
                // Rule-required safety apparel must be wearable even when the
                // pawn's ordinary outfit policy does not include it.
                wearJob.playerForced = true;
                transitionJobs.Add(wearJob);
                managedApparel.Add(apparel);
            }

            ThingWithComps managedWeapon = null;
            if (missingWeapon)
            {
                ApparelRule weaponRule = applicableRules.First(candidate =>
                    candidate.HasWeaponRequirement);

                managedWeapon = WeaponFinder.FindBest(
                    pawn, combinedWeaponRequirement, weaponRule.ChangingArea);
                if (managedWeapon == null)
                {
                    UnavailableWorkRegistry.Block(pawn, weaponRule);
                    if (ShouldLogRepeatedDiagnostic(
                            pawn, $"weapon-unavailable:{weaponRule.Id}:{weaponRule.WeaponSummary}"))
                    {
                        Log.Warning($"[AutomaticOutfitManager] {pawn.LabelShortCap}: delaying {newJob.def.defName}; no reachable {weaponRule.WeaponSummary.ToLowerInvariant()} is available for '{weaponRule.Name}'.");
                    }
                    __instance.ClearQueuedJobs(false);
                    ReplaceWithWait(pawn, 300, ref newJob, ref jobGiver, ref tag);
                    return;
                }

                Job equipJob = JobMaker.MakeJob(JobDefOf.Equip, managedWeapon);
                // Do not mark the job player-forced: Simple Sidearms uses that
                // signal for persistent weapon preferences.
                equipJob.playerForced = false;
                transitionJobs.Add(equipJob);
            }

            if (transitionJobs.Count == 0)
                return;

            // Jobs issued directly by the player may not carry a workGiverDef,
            // but their targets still need the same protection while the pawn
            // changes apparel. Claim the complete job before preserving it.
            if (!ManagedWorkClaimRegistry.TryClaim(pawn, newJob))
            {
                __instance.ClearQueuedJobs(false);
                ReplaceWithWait(pawn, 60, ref newJob, ref jobGiver, ref tag);
                return;
            }

            ApparelRule primaryRule = component?.StateFor(pawn) is PawnApparelState existingState
                ? component.RuleById(existingState.ActiveRuleId) ?? applicableRules[0]
                : applicableRules[0];
            UnavailableWorkRegistry.Clear(pawn, applicableRules);
            PawnApparelState preparedState = component?.BeginIntervention(
                pawn, primaryRule, managedApparel, managedWeapon);
            if (preparedState != null)
            {
                if (weaponChoiceProtected)
                    preparedState.MarkWeaponPlayerOverride();
                preparedState.PendingWorkJob = newJob;
                preparedState.PendingWorkIsManagedWork = false;
                preparedState.CurrentRuleIds = applicableRules
                    .Select(candidate => candidate.Id)
                    .Distinct()
                    .ToList();
            }

            if (Prefs.DevMode)
            {
                string ruleNames = string.Join(", ",
                    applicableRules.Select(candidate => $"'{candidate.Name}'"));
                string weaponAssignment = managedWeapon == null
                    ? "no weapon"
                    : $"weapon {managedWeapon.LabelCap} [{managedWeapon.def.defName}]";
                Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: intercepted {newJob.def.defName}; preparing {managedApparel.Count} apparel item(s) and {weaponAssignment} for {ruleNames}.");
            }

            if (preparedState != null)
            {
                // Keep one deep-save owner for the exact interrupted job. The
                // next non-apparel candidate will be replaced with this job once
                // preparation is complete, so recreation or another think-tree
                // choice cannot displace a player assignment.
                StartTransitionJobs(__instance, ref newJob, ref jobGiver, ref tag, transitionJobs);
            }
            else
            {
                // A missing game component is not expected in normal play, but
                // retain RimWorld's queue behavior as a safe compatibility
                // fallback and do not leave a claim with no owning state.
                ManagedWorkClaimRegistry.Release(pawn, newJob);
                QueueBeforeCurrent(__instance, ref newJob, ref jobGiver, ref tag, transitionJobs);
            }
        }

        private static bool PawnInsideArea(Pawn pawn, Area area) =>
            pawn?.Map != null && area?.Map == pawn.Map &&
            pawn.Position.IsValid && pawn.Position.InBounds(pawn.Map) && area[pawn.Position];

        private static bool HasCompletedPreparation(
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            PawnApparelState state)
        {
            if (pawn?.apparel == null || component == null || state == null)
                return false;

            List<ApparelRule> preparedRules = (state.CurrentRuleIds ?? new List<string>())
                .Select(component.RuleById)
                .Where(rule => rule?.Enabled == true)
                .ToList();
            if (preparedRules.Count == 0)
            {
                ApparelRule activeRule = component.RuleById(state.ActiveRuleId);
                if (activeRule?.Enabled == true)
                    preparedRules.Add(activeRule);
            }

            if (preparedRules.Count == 0 ||
                preparedRules.Any(rule => RuleEvaluator.HasMissingRequiredApparel(pawn, rule)) ||
                !RuleEvaluator.TryCombinedWeaponRequirement(
                    preparedRules, out CombinedWeaponRequirement weaponRequirement))
            {
                return false;
            }

            return state.WeaponPlayerOverride ||
                   weaponRequirement.Matches(pawn.equipment?.Primary);
        }

        private static void TrackNestedRuleEntries(
            PawnApparelState state, List<ApparelRule> matchingRules)
        {
            foreach (ApparelRule rule in matchingRules.Where(rule =>
                         rule != null && rule.Id != state.ActiveRuleId))
            {
                NestedRuleBufferState progress = state.NestedRuleBuffers
                    .FirstOrDefault(item => item.RuleId == rule.Id);
                if (progress == null)
                {
                    state.NestedRuleBuffers.Add(new NestedRuleBufferState { RuleId = rule.Id });
                    state.LastNestedBufferStatus =
                        $"{rule.Name}: entered nested work; 0 of {rule.ReturnTaskBuffer} outer tasks used.";
                    if (Prefs.DevMode)
                        Log.Message($"[AutomaticOutfitManager] {state.Pawn?.LabelShortCap}: nested task buffer started for '{rule.Name}' (0/{rule.ReturnTaskBuffer}).");
                }
                else
                {
                    progress.Completed = 0;
                    progress.Finished = false;
                    progress.LastJobLoadId = -1;
                    progress.LastJobLabel = null;
                    state.LastNestedBufferStatus =
                        $"{rule.Name}: nested work restarted; 0 of {rule.ReturnTaskBuffer} outer tasks used.";
                }
            }
        }

        private static bool HandleNestedRuleBuffers(
            Pawn_JobTracker tracker,
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            PawnApparelState state,
            List<ApparelRule> matchingRules,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref JobTag? tag)
        {
            // Mirror the outer task-buffer contract: connective movement and
            // waiting do not count, while the next meaningful jobs do. A nested
            // buffer must not depend on continuing to match the outer work area;
            // otherwise a pawn sent elsewhere loses the nested session before
            // any configured follow-up task can be observed or completed.
            if (!IsBufferableJob(newJob) || RequiresImmediateRestoration(newJob))
                return false;

            var matchingIds = new HashSet<string>(matchingRules.Select(rule => rule.Id));
            foreach (NestedRuleBufferState progress in state.NestedRuleBuffers.ToList())
            {
                if (matchingIds.Contains(progress.RuleId))
                    continue;

                // Keep completed nested sessions for worker-list and tooltip
                // visibility until the pawn's saved outfit is fully restored.
                // Re-entering the nested area resets this flag above.
                if (progress.Finished)
                    continue;

                ApparelRule nestedRule = component.RuleById(progress.RuleId);
                if (nestedRule == null)
                {
                    state.NestedRuleBuffers.Remove(progress);
                    continue;
                }

                if (newJob.loadID == progress.LastJobLoadId)
                    continue;

                if (progress.Completed < nestedRule.ReturnTaskBuffer)
                {
                    progress.Completed++;
                    progress.LastJobLoadId = newJob.loadID;
                    progress.LastJobLabel = newJob.GetReport(pawn);
                    state.LastNestedBufferStatus =
                        $"{nestedRule.Name}: {progress.Completed} of {nestedRule.ReturnTaskBuffer} outer tasks used" +
                        (string.IsNullOrEmpty(progress.LastJobLabel)
                            ? "."
                            : $"; last: {progress.LastJobLabel}.");
                    if (Prefs.DevMode)
                        Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: nested task buffer {progress.Completed}/{nestedRule.ReturnTaskBuffer} used by {newJob.def.defName} after leaving '{nestedRule.Name}'.");
                    continue;
                }

                // The outer outfit remains part of the session even when the
                // buffered follow-up job is outside every managed area. Preserve
                // its shared requirements while removing only nested-only gear.
                ApparelRule activeRule = component.RuleById(state.ActiveRuleId);
                List<ApparelRule> retainedRules = matchingRules
                    .Concat(activeRule == null
                        ? Enumerable.Empty<ApparelRule>()
                        : new[] { activeRule })
                    .Where(rule => rule != null && rule.Id != nestedRule.Id)
                    .Distinct()
                    .ToList();
                var retainedDefs = new HashSet<ThingDef>(retainedRules
                    .SelectMany(rule => rule.RequiredApparel ?? new List<ThingDef>())
                    .Where(def => def != null));
                var nestedOnlyDefs = new HashSet<ThingDef>(
                    (nestedRule.RequiredApparel ?? new List<ThingDef>())
                    .Where(def => def != null && !retainedDefs.Contains(def)));
                List<Job> removalJobs = pawn.apparel.WornApparel
                    .Where(item => item != null && nestedOnlyDefs.Contains(item.def) &&
                                   state.ManagedApparel.Contains(item))
                    .Select(item => JobMaker.MakeJob(JobDefOf.RemoveApparel, item))
                    .ToList();

                if (state.WeaponInterventionActive &&
                    RuleEvaluator.TryCombinedWeaponRequirement(
                        retainedRules, out CombinedWeaponRequirement retainedWeapon) &&
                    (!retainedWeapon.HasRequirement ||
                     !retainedWeapon.Matches(pawn.equipment?.Primary)))
                {
                    state.RequestWeaponRestoration();
                    RestorationPlanner.TryMakeHeldOriginalsAccessible(pawn, state);
                    removalJobs.AddRange(RestorationPlanner.BuildWeaponJobs(
                        pawn, state, out _));
                }
                state.LastNestedBufferStatus =
                    $"{nestedRule.Name}: buffer complete; removing nested-only apparel and weapons before {newJob.GetReport(pawn)}.";
                progress.Completed = nestedRule.ReturnTaskBuffer;
                progress.Finished = true;
                progress.LastJobLoadId = newJob.loadID;
                progress.LastJobLabel = newJob.GetReport(pawn);

                if (removalJobs.Count == 0)
                    continue;

                bool insideNestedArea = PawnInsideArea(pawn, nestedRule.Area);
                bool outsideNestedChangingArea = nestedRule.ChangingArea != null &&
                    !PawnInsideArea(pawn, nestedRule.ChangingArea);
                if ((insideNestedArea || outsideNestedChangingArea) &&
                    TryFindSafeTransitionCell(
                        pawn, nestedRule.ChangingArea, new[] { nestedRule },
                        out IntVec3 changingCell))
                {
                    removalJobs.Insert(0, MakeChangingAreaTravelJob(changingCell));
                }

                else if (insideNestedArea)
                {
                    // Keep every inner requirement on until a safe exterior
                    // cell exists. Do not queue nested-only removal in place.
                    progress.Finished = false;
                    progress.LastJobLoadId = -1;
                    state.LastNestedBufferStatus =
                        $"{nestedRule.Name}: buffer complete; waiting for a safe exit before removing nested-only gear.";
                    ReplaceWithWait(
                        pawn, 300, ref newJob, ref jobGiver, ref tag);
                    return true;
                }

                state.Transition = ApparelTransition.Preparing;
                QueueBeforeCurrent(tracker, ref newJob, ref jobGiver, ref tag, removalJobs);
                if (Prefs.DevMode)
                    Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: nested task buffer complete for '{nestedRule.Name}'; removing {removalJobs.Count} nested transition job(s).");
                return true;
            }

            return false;
        }

        private static bool TryPrepareForMatchingRules(
            Pawn_JobTracker tracker,
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            List<ApparelRule> rules,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref JobTag? tag)
        {
            ApparelRule unwearableRule = rules.FirstOrDefault(rule =>
                !RuleEvaluator.RuleCanApplyToPawn(pawn, rule));
            if (unwearableRule != null)
            {
                UnavailableWorkRegistry.Block(pawn, unwearableRule);
                if (ShouldLogRepeatedDiagnostic(
                        pawn, $"nested-unwearable:{unwearableRule.Id}"))
                {
                    Log.Warning($"[AutomaticOutfitManager] {pawn.LabelShortCap}: blocked from '{unwearableRule.Name}'; its required apparel cannot be worn by this pawn.");
                }
                tracker.ClearQueuedJobs(false);
                ReplaceWithWait(pawn, 300, ref newJob, ref jobGiver, ref tag);
                return true;
            }

            ApparelConflict conflict = ApparelCompatibility.FindConflict(
                rules, pawn.RaceProps?.body);
            bool compatibleWeaponRequirements =
                RuleEvaluator.TryCombinedWeaponRequirement(
                    rules, out CombinedWeaponRequirement combinedWeaponRequirement);
            if (conflict != null || !compatibleWeaponRequirements)
            {
                foreach (ApparelRule rule in rules)
                    UnavailableWorkRegistry.Block(pawn, rule);
                if (ShouldLogRepeatedDiagnostic(
                        pawn, $"nested-conflict:{string.Join(",", rules.Select(rule => rule.Id))}"))
                {
                    string conflictLabel = conflict != null
                        ? conflict.Label
                        : "different primary weapons";
                    Log.Warning($"[AutomaticOutfitManager] {pawn.LabelShortCap}: delaying {newJob.def.defName}; incompatible required apparel: {conflictLabel}.");
                }

                tracker.ClearQueuedJobs(false);
                ReplaceWithWait(pawn, 300, ref newJob, ref jobGiver, ref tag);
                return true;
            }

            var requiredByDef = new Dictionary<ThingDef, ApparelRule>();
            foreach (ApparelRule rule in rules)
            {
                foreach (ThingDef def in rule.RequiredApparel ?? Enumerable.Empty<ThingDef>())
                {
                    if (def != null && !requiredByDef.ContainsKey(def))
                        requiredByDef.Add(def, rule);
                }
            }

            var missing = requiredByDef.Keys
                .Where(def => !pawn.apparel.WornApparel.Any(item => item?.def == def))
                .ToList();
            bool missingWeapon = !combinedWeaponRequirement.Matches(
                pawn.equipment?.Primary);
            PawnApparelState weaponState = component?.StateFor(pawn);
            bool weaponChoiceProtected = missingWeapon &&
                (weaponState?.WeaponPlayerOverride == true ||
                 SimpleSidearmsCompatibility.ProtectsCurrentWeaponChoice(pawn));
            if (weaponChoiceProtected)
            {
                missingWeapon = false;
                weaponState ??= component?.BeginIntervention(
                    pawn, rules[0], Enumerable.Empty<Apparel>(), null);
                if (weaponState != null)
                {
                    weaponState.MarkWeaponPlayerOverride();
                    weaponState.CurrentRuleIds = rules
                        .Where(rule => rule != null)
                        .Select(rule => rule.Id)
                        .Distinct()
                        .ToList();
                }
                if (ShouldLogRepeatedDiagnostic(
                        pawn, $"nested-weapon-player-control:{string.Join(",", rules.Select(rule => rule.Id))}"))
                {
                    Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: continuing {newJob.def.defName} with the player's current primary weapon; the weapon requirement is skipped while that choice is protected.");
                }
            }
            if (missing.Count == 0 && !missingWeapon)
            {
                if (weaponChoiceProtected && weaponState != null)
                {
                    weaponState.Transition = ApparelTransition.Active;
                    weaponState.LastManagedWorkJobDefName = newJob.def.defName;
                }
                return false;
            }

            var transitionJobs = new List<Job>();
            var managedApparel = new List<Apparel>();
            foreach (ThingDef def in missing)
            {
                ApparelRule sourceRule = requiredByDef[def];
                Apparel apparel = ApparelFinder.FindBest(pawn, def, sourceRule.ChangingArea);
                if (apparel == null)
                {
                    UnavailableWorkRegistry.Block(pawn, sourceRule);
                    if (ShouldLogRepeatedDiagnostic(
                            pawn, $"nested-gear-unavailable:{sourceRule.Id}:{def.defName}"))
                    {
                        Log.Warning($"[AutomaticOutfitManager] {pawn.LabelShortCap}: delaying {newJob.def.defName}; no reachable {def.LabelCap} is available for '{sourceRule.Name}'.");
                    }

                    // Discard the stale work candidate and give the normal think
                    // tree time to select other work. It will reconsider after
                    // gear is produced, hauled, or becomes unreserved.
                    tracker.ClearQueuedJobs(false);
                    ReplaceWithWait(pawn, 300, ref newJob, ref jobGiver, ref tag);
                    return true;
                }

                Job wearJob = JobMaker.MakeJob(JobDefOf.Wear, apparel);
                wearJob.playerForced = true;
                transitionJobs.Add(wearJob);
                managedApparel.Add(apparel);
            }

            ThingWithComps managedWeapon = null;
            if (missingWeapon)
            {
                ApparelRule weaponRule = rules.First(rule =>
                    rule.HasWeaponRequirement);

                managedWeapon = WeaponFinder.FindBest(
                    pawn, combinedWeaponRequirement, weaponRule.ChangingArea);
                if (managedWeapon == null)
                {
                    UnavailableWorkRegistry.Block(pawn, weaponRule);
                    if (ShouldLogRepeatedDiagnostic(
                            pawn, $"nested-weapon-unavailable:{weaponRule.Id}:{weaponRule.WeaponSummary}"))
                    {
                        Log.Warning($"[AutomaticOutfitManager] {pawn.LabelShortCap}: delaying {newJob.def.defName}; no reachable {weaponRule.WeaponSummary.ToLowerInvariant()} is available for '{weaponRule.Name}'.");
                    }
                    tracker.ClearQueuedJobs(false);
                    ReplaceWithWait(pawn, 300, ref newJob, ref jobGiver, ref tag);
                    return true;
                }

                Job equipJob = JobMaker.MakeJob(JobDefOf.Equip, managedWeapon);
                equipJob.playerForced = false;
                transitionJobs.Add(equipJob);
            }

            if (!ManagedWorkClaimRegistry.TryClaim(pawn, newJob))
            {
                tracker.ClearQueuedJobs(false);
                ReplaceWithWait(pawn, 60, ref newJob, ref jobGiver, ref tag);
                return true;
            }

            ApparelRule primaryRule = component?.StateFor(pawn) is PawnApparelState existing
                ? component.RuleById(existing.ActiveRuleId) ?? rules[0]
                : rules[0];
            UnavailableWorkRegistry.Clear(pawn, rules);
            PawnApparelState interventionState = component?.BeginIntervention(
                pawn, primaryRule, managedApparel, managedWeapon);
            if (interventionState != null)
            {
                if (weaponChoiceProtected)
                    interventionState.MarkWeaponPlayerOverride();
                interventionState.PendingWorkJob = newJob;
                interventionState.PendingWorkIsManagedWork = true;
                interventionState.LastManagedWorkJobDefName = newJob.def.defName;
                interventionState.CurrentRuleIds = rules
                    .Where(rule => rule != null)
                    .Select(rule => rule.Id)
                    .Distinct()
                    .ToList();
            }

            if (Prefs.DevMode)
            {
                string ruleNames = string.Join(", ", rules.Select(rule => $"'{rule.Name}'"));
                string weaponAssignment = managedWeapon == null
                    ? "no weapon"
                    : $"weapon {managedWeapon.LabelCap} [{managedWeapon.def.defName}]";
                Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: intercepted {newJob.def.defName}; preparing {managedApparel.Count} apparel item(s) and {weaponAssignment} for overlapping rules {ruleNames}.");
            }

            // PendingWorkJob is the sole owner of the interrupted work job while
            // preparation runs. Putting that same Job in RimWorld's queue would
            // make both the queue and PawnApparelState deep-save it, producing
            // duplicate load IDs and unreliable save/load continuations.
            StartTransitionJobs(tracker, ref newJob, ref jobGiver, ref tag, transitionJobs);
            return true;
        }

        private static bool SameJob(Job left, Job right) =>
            left != null && right != null &&
            (ReferenceEquals(left, right) || left.loadID == right.loadID);

        private static bool HasManagedWorkContext(
            Job job, ThinkNode jobGiver, PawnApparelState state)
        {
            if (!IsBufferableJob(job))
                return false;

            // RimWorld normally supplies workGiverDef, but player orders and
            // several modded work givers omit it. Preserve the work context that
            // caused preparation so those jobs reset/hold the managed-area
            // buffer just like ordinary work instead of consuming it.
            return job.workGiverDef != null ||
                   jobGiver is JobGiver_Work ||
                   job.jobGiver is JobGiver_Work ||
                   job.playerForced ||
                   (SameJob(job, state?.PendingWorkJob) &&
                    state.PendingWorkIsManagedWork) ||
                   (!string.IsNullOrEmpty(state?.LastManagedWorkJobDefName) &&
                    string.Equals(job.def.defName, state.LastManagedWorkJobDefName,
                        StringComparison.Ordinal));
        }

        private static string PendingWorkCancellationReason(
            Pawn pawn, PawnApparelState state, Job nextJob)
        {
            if (state?.RecallRequested == true)
                return "work was paused or a return was requested";

            if (RequiresImmediateRestoration(nextJob) &&
                ProtectedRulesForJob(pawn, nextJob).Count == 0)
                return $"{nextJob?.def?.defName ?? "the next job"} requires immediate clothing restoration";

            if (!PendingWorkJobIsViable(pawn, state?.PendingWorkJob, out string reason))
                return reason;

            if (!ManagedWorkClaimRegistry.TryClaim(pawn, state.PendingWorkJob))
                return "another outfitting pawn now claims one of its targets";

            return null;
        }

        private static List<ApparelRule> ProtectedRulesForJob(Pawn pawn, Job job)
        {
            var rules = RuleEvaluator.MatchingRules(pawn, job);
            ApparelRule haulingRule =
                PausedAreaWorkFilter.MatchingPermittedHaulingRule(pawn, job);
            if (haulingRule != null)
                rules.Add(haulingRule);
            rules.AddRange(
                PausedAreaWorkFilter.MatchingProtectedTransitRules(pawn, job));
            return rules
                .Where(rule => rule != null)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
        }

        internal static bool PendingWorkJobIsViable(
            Pawn pawn, Job job, out string reason)
        {
            reason = null;
            if (pawn?.Map == null || job?.def == null)
            {
                reason = "the pawn, map, or saved job is no longer valid";
                return false;
            }

            var targets = new List<LocalTargetInfo>
            {
                job.targetA,
                job.targetB,
                job.targetC
            };
            if (job.targetQueueA != null)
                targets.AddRange(job.targetQueueA);
            if (job.targetQueueB != null)
                targets.AddRange(job.targetQueueB);

            bool hasMeaningfulTarget = targets.Any(target =>
                target.IsValid &&
                (target.HasThing ||
                 (target.Cell.IsValid && target.Cell.InBounds(pawn.Map))));
            if (!hasMeaningfulTarget)
            {
                reason = "the saved job no longer has a valid target";
                return false;
            }

            foreach (LocalTargetInfo target in targets)
            {
                if (target.IsValid && target.HasThing)
                {
                    Thing thing = target.Thing;
                    if (thing == null || thing.Destroyed || thing.MapHeld != pawn.Map)
                    {
                        reason = "one of its targets was destroyed or left the map";
                        return false;
                    }

                    // The claim registry prevents new contenders while apparel
                    // is prepared. Recheck RimWorld's real reservations as well
                    // in case another job already held a secondary ingredient or
                    // queued target before this transition claimed it.
                    int stackCount = thing.def?.stackLimit > 1 ? 1 : -1;
                    if (thing != pawn &&
                        !pawn.CanReserve(target, 1, stackCount, null, false))
                    {
                        reason = $"{thing.LabelCap} is no longer reservable";
                        return false;
                    }
                }
                if (target.IsValid && !target.HasThing &&
                    (!target.Cell.IsValid || !target.Cell.InBounds(pawn.Map)))
                {
                    reason = "one of its target cells is no longer valid";
                    return false;
                }
            }

            // Player-assigned jobs often omit workGiverDef. Re-evaluate the job
            // through every path that can require managed apparel instead of
            // rejecting those valid continuations solely because the tag is
            // absent.
            bool stillApplies = RuleEvaluator.MatchingRules(pawn, job).Count > 0 ||
                                PausedAreaWorkFilter.MatchingPermittedHaulingRule(pawn, job) != null ||
                                PausedAreaWorkFilter.MatchingProtectedTransitRules(pawn, job).Count > 0;
            if (!stillApplies)
                reason = "the job no longer targets an active managed rule";
            return stillApplies;
        }

        internal static bool TryFindChangingCell(Pawn pawn, Area area, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (pawn?.Map == null || area?.Map != pawn.Map)
                return false;

            cell = area.ActiveCells
                .Where(candidate => candidate.Standable(pawn.Map) &&
                                    pawn.CanReach(candidate, PathEndMode.OnCell, Danger.Deadly) &&
                                    ChangingCellIsAvailable(pawn, candidate))
                .OrderBy(candidate => candidate.DistanceToSquared(pawn.Position))
                .FirstOrDefault();
            return cell.IsValid;
        }

        internal static bool TryFindRestorationCell(
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            PawnApparelState state,
            out IntVec3 cell)
        {
            ApparelRule activeRule = component?.RuleById(state?.ActiveRuleId);
            return TryFindSafeTransitionCell(
                pawn,
                activeRule?.ChangingArea,
                StateProtectedRules(component, state, pawn?.Map),
                out cell);
        }

        internal static bool PawnInsideStateProtectedArea(
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            PawnApparelState state)
        {
            return StateProtectedRules(component, state, pawn?.Map)
                .Any(rule => PawnInsideArea(pawn, rule.Area));
        }

        private static List<ApparelRule> StateProtectedRules(
            AutomaticOutfitManagerGameComponent component,
            PawnApparelState state,
            Map map)
        {
            var ruleIds = new List<string>(state?.CurrentRuleIds ??
                Enumerable.Empty<string>());
            if (!string.IsNullOrEmpty(state?.ActiveRuleId))
                ruleIds.Add(state.ActiveRuleId);
            return ruleIds
                .Distinct()
                .Select(ruleId => component?.RuleById(ruleId))
                .Where(rule => rule?.Area?.Map == map)
                .ToList();
        }

        private static bool TryFindSafeTransitionCell(
            Pawn pawn,
            Area preferredArea,
            IEnumerable<ApparelRule> protectedRules,
            out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            if (pawn?.Map == null)
                return false;

            List<ApparelRule> rules = protectedRules?
                .Where(rule => rule?.Area?.Map == pawn.Map)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList() ?? new List<ApparelRule>();
            bool IsSafe(IntVec3 candidate) =>
                candidate.IsValid && candidate.InBounds(pawn.Map) &&
                rules.All(rule => !rule.Area[candidate]);
            bool IsUsable(IntVec3 candidate) =>
                IsSafe(candidate) && candidate.Standable(pawn.Map) &&
                pawn.CanReach(candidate, PathEndMode.OnCell, Danger.Deadly) &&
                ChangingCellIsAvailable(pawn, candidate);

            if (preferredArea?.Map == pawn.Map)
            {
                cell = preferredArea.ActiveCells
                    .Where(IsUsable)
                    .OrderBy(candidate =>
                        candidate.DistanceToSquared(pawn.Position))
                    .FirstOrDefault();
                if (cell.IsValid)
                    return true;
            }

            if (!rules.Any(rule => PawnInsideArea(pawn, rule.Area)))
                return false;

            var boundaryCells = new HashSet<IntVec3>();
            foreach (ApparelRule rule in rules)
            {
                foreach (IntVec3 areaCell in rule.Area.ActiveCells)
                {
                    foreach (IntVec3 candidate in GenRadial.RadialCellsAround(
                                 areaCell, 1.5f, false))
                    {
                        if (IsSafe(candidate))
                            boundaryCells.Add(candidate);
                    }
                }
            }

            cell = boundaryCells
                .Where(IsUsable)
                .OrderBy(candidate =>
                    candidate.DistanceToSquared(pawn.Position))
                .FirstOrDefault();
            return cell.IsValid;
        }

        private static bool ChangingCellIsAvailable(Pawn pawn, IntVec3 candidate)
        {
            if (pawn?.Map == null || !candidate.IsValid)
                return false;

            Pawn occupant = candidate.GetFirstPawn(pawn.Map);
            if (occupant != null && occupant != pawn)
                return false;
            if (!pawn.CanReserve(
                    new LocalTargetInfo(candidate), 1, -1, null, false))
            {
                return false;
            }

            // Goto does not reserve its destination. Exclude cells already
            // assigned to another returning pawn so simultaneous recalls do not
            // repeatedly choose the same otherwise-free locker square.
            return AutomaticOutfitManagerGameComponent.Current?.PawnStates?.All(state =>
                state?.Pawn == null || state.Pawn == pawn ||
                state.Transition != ApparelTransition.ReturningToChangingArea ||
                !IsChangingAreaTravelJob(state.Pawn.jobs?.curJob) ||
                state.Pawn.jobs.curJob.targetA.Cell != candidate) != false;
        }

        private static bool JobTargetsArea(Job job, Area area)
        {
            if (job == null || area == null)
                return false;

            LocalTargetInfo target = job.targetA;
            IntVec3 cell = target.HasThing ? target.Thing?.PositionHeld ?? IntVec3.Invalid : target.Cell;
            return cell.IsValid && cell.InBounds(area.Map) && area[cell];
        }

        private static bool JobTargetsManagedApparel(Job job)
        {
            if (job == null)
                return false;

            if (IsManagedApparel(job.targetA) ||
                IsManagedApparel(job.targetB) ||
                IsManagedApparel(job.targetC))
            {
                return true;
            }

            return (job.targetQueueA?.Any(IsManagedApparel) ?? false) ||
                   (job.targetQueueB?.Any(IsManagedApparel) ?? false);
        }

        private static bool IsManagedApparel(LocalTargetInfo target)
        {
            Apparel apparel = target.Thing as Apparel;
            return apparel != null &&
                   AutomaticOutfitManagerGameComponent.Current?.IsManagedApparel(apparel) == true;
        }

        private static bool IsFriendlyGuest(Pawn pawn)
            => PawnAccessClassifier.IsHostedGuest(pawn);

        private static bool IsAllowedTransitionWear(PawnApparelState state, Apparel apparel)
        {
            if (state == null || apparel == null)
                return false;

            switch (state.Transition)
            {
                case ApparelTransition.Preparing:
                case ApparelTransition.Active:
                    return state.ManagedApparel?.Contains(apparel) == true;
                case ApparelTransition.Restoring:
                    return state.OriginalApparel?.Contains(apparel) == true;
                case ApparelTransition.ReturningToChangingArea:
                default:
                    return false;
            }
        }

        internal static bool IsAssignedTransitionApparelJob(PawnApparelState state, Job job)
        {
            if (state == null || job?.targetA.Thing is not Apparel apparel)
                return false;

            if (job.def == JobDefOf.Wear)
                return IsAllowedTransitionWear(state, apparel);

            if (job.def == JobDefOf.RemoveApparel)
                return state.ManagedApparel?.Contains(apparel) == true;

            // A transition may hand its just-removed automatic item to a haul
            // job. Only the exact items recorded for this pawn are exempted.
            return (job.def == JobDefOf.HaulToCell || job.def == JobDefOf.HaulToContainer) &&
                   state.ManagedApparel?.Contains(apparel) == true;
        }

        private static bool HaulDestinationRejectsGear(Pawn pawn, Job job)
        {
            if (pawn?.Map == null || job?.targetA.Thing is not Thing gear ||
                (gear.def?.apparel == null && gear.def?.IsWeapon != true) ||
                (job.def != JobDefOf.HaulToCell &&
                 job.def != JobDefOf.HaulToContainer))
            {
                return false;
            }

            IHaulDestination destination = null;
            if (job.def == JobDefOf.HaulToCell && job.targetB.Cell.IsValid)
            {
                destination = job.targetB.Cell.GetSlotGroup(pawn.Map) as IHaulDestination;
            }
            else if (job.def == JobDefOf.HaulToContainer)
            {
                destination = job.targetB.Thing as IHaulDestination;
            }

            return destination != null && !destination.Accepts(gear);
        }

        internal static bool IsAssignedTransitionWeaponJob(
            PawnApparelState state, Job job)
        {
            if (state == null || job?.targetA.Thing is not ThingWithComps weapon ||
                weapon.def?.IsWeapon != true || job.playerForced)
            {
                return false;
            }

            if (job.def == JobDefOf.Equip)
            {
                return state.IsManagedWeapon(weapon) ||
                       (state.WeaponRestorationRequested &&
                        state.OriginalWeapon == weapon);
            }

            return job.def == JobDefOf.DropEquipment &&
                   (state.IsManagedWeapon(weapon) ||
                    (state.WeaponRestorationRequested &&
                     state.WeaponPlayerOverride &&
                     state.Pawn?.equipment?.Primary == weapon));
        }

        internal static bool IsAssignedChangingAreaReturnJob(
            PawnApparelState state, Job job)
        {
            if (state?.Transition != ApparelTransition.ReturningToChangingArea ||
                !IsChangingAreaTravelJob(job) || !job.targetA.Cell.IsValid)
            {
                return false;
            }

            return state.ChangingAreaReturnCell.IsValid &&
                   job.targetA.Cell == state.ChangingAreaReturnCell;
        }

        internal static bool IsChangingAreaTravelJob(Job job)
            => job?.def == AutomaticOutfitManagerJobDefOf
                   .AutomaticOutfitManager_LockerReturn ||
               job?.def == JobDefOf.Goto;

        internal static Job MakeChangingAreaTravelJob(IntVec3 cell)
            => JobMaker.MakeJob(
                AutomaticOutfitManagerJobDefOf
                    .AutomaticOutfitManager_LockerReturn,
                cell);

        private static bool IsExternalWeaponControlJob(Job job)
        {
            string defName = job?.def?.defName ?? string.Empty;
            return defName.IndexOf("SwitchWeapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defName.IndexOf("Sidearm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defName.IndexOf("EquipSecondary", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   defName.IndexOf("ReequipSecondary", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsBufferableJob(Job job)
        {
            if (job?.def == null)
                return false;

            string defName = job.def.defName ?? string.Empty;
            return !defName.StartsWith("Wait", StringComparison.OrdinalIgnoreCase) &&
                   !defName.StartsWith("Goto", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(defName, "TakeInventory", StringComparison.OrdinalIgnoreCase) &&
                   job.def != JobDefOf.Wait &&
                   !IsChangingAreaTravelJob(job) &&
                   job.def != JobDefOf.Wear &&
                   job.def != JobDefOf.RemoveApparel;
        }

        private static bool IsRecoveryWaitJob(Job job)
        {
            string defName = job?.def?.defName ?? string.Empty;
            return job?.def == JobDefOf.Wait ||
                   job?.def == JobDefOf.Wait_Wander ||
                   defName.StartsWith("Wait", StringComparison.OrdinalIgnoreCase) ||
                   defName.IndexOf("Standing", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool RequiresImmediateRestoration(Job job)
            // Sleep is a long-lived state rather than an ordinary buffer task.
            // Callers restore immediately only after confirming that neither
            // its destination nor route is protected by an active rule.
            => PausedAreaWorkFilter.IsEssentialPersonalJob(job);

        internal static void LogAutomaticManagedGearRejection(
            Pawn pawn, Job job, Thing gear, string stage)
        {
            if (!Prefs.DevMode || pawn == null || job?.def == null || gear == null ||
                !ShouldLogRepeatedDiagnostic(
                    pawn, $"automatic-managed-gear:{job.def.defName}:{gear.def?.defName}"))
            {
                return;
            }

            string gearKind = gear.def?.IsWeapon == true ? "weapon" : "apparel";
            Log.Message(
                $"[AutomaticOutfitManager] {pawn.LabelShortCap}: ignored automatic " +
                $"{job.def.defName} for managed {gearKind} {gear.LabelCap} at {stage}; " +
                "only an Automatic Outfit Manager transition or explicit player order may use it.");
        }

        internal static bool ShouldLogRepeatedDiagnostic(
            Pawn pawn, string category, int interval = RepeatedDiagnosticInterval)
        {
            if (pawn == null || string.IsNullOrEmpty(category))
                return false;

            // Large visiting groups can retry the same inaccessible transit job
            // for many hours. Keep one useful diagnostic per guest per in-game
            // day while retaining the shorter interval for colony pawns.
            if (PawnAccessClassifier.IsHostedGuest(pawn))
                interval = Math.Max(interval, GuestRepeatedDiagnosticInterval);

            int tick = Find.TickManager?.TicksGame ?? 0;
            string key = $"{pawn.thingIDNumber}:{category}";
            if (LastRepeatedDiagnosticTick.TryGetValue(key, out int lastTick) &&
                tick - lastTick < interval)
            {
                return false;
            }

            LastRepeatedDiagnosticTick[key] = tick;
            return true;
        }

        private static void ReplaceWithBriefWait(
            Pawn pawn,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref JobTag? tag)
        {
            ReplaceWithWait(pawn, 30, ref newJob, ref jobGiver, ref tag);
        }

        private static void ReplaceWithWait(
            Pawn pawn,
            int expiryInterval,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref JobTag? tag)
        {
            // Some StartJob callers immediately inspect targetA while deciding
            // whether to append an opportunistic haul. A targetless replacement
            // can send null into Fogged(Thing); targeting the pawn makes this a
            // complete, harmless wait job for both vanilla and modded callers.
            Job waitJob = pawn != null
                ? JobMaker.MakeJob(JobDefOf.Wait, pawn)
                : JobMaker.MakeJob(JobDefOf.Wait);
            waitJob.expiryInterval = expiryInterval;
            newJob = waitJob;
            jobGiver = null;
            tag = null;
        }

        private static void QueueBeforeCurrent(
            Pawn_JobTracker tracker,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref JobTag? tag,
            List<Job> jobs)
        {
            Job interruptedJob = newJob;
            tracker.jobQueue.EnqueueFirst(interruptedJob, tag);
            for (int i = jobs.Count - 1; i >= 1; i--)
                tracker.jobQueue.EnqueueFirst(jobs[i]);

            newJob = jobs[0];
            jobGiver = null;
            tag = null;
        }

        private static void StartTransitionJobs(
            Pawn_JobTracker tracker,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref JobTag? tag,
            List<Job> jobs)
        {
            // The interrupted work is held exclusively by PendingWorkJob. Queue
            // only the remaining transition steps so every Job has one deep-save
            // owner if the player saves while the pawn is changing apparel.
            for (int i = jobs.Count - 1; i >= 1; i--)
                tracker.jobQueue.EnqueueFirst(jobs[i]);

            newJob = jobs[0];
            jobGiver = null;
            tag = null;
        }

        private static void QueueRestorationJobs(
            Pawn_JobTracker tracker,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref JobTag? tag,
            List<Job> jobs)
        {
            // Restoration does not need to preserve the interrupted job: normal
            // AI will reconsider it after the saved outfit is complete. Clearing
            // the queue also repairs saves affected by the former retry loop,
            // which could accumulate hundreds of duplicate Wear jobs.
            tracker.ClearQueuedJobs(false);
            for (int i = jobs.Count - 1; i >= 1; i--)
                tracker.jobQueue.EnqueueFirst(jobs[i]);

            newJob = jobs[0];
            jobGiver = null;
            tag = null;
        }
    }
}
