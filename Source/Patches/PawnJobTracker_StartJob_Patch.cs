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
        internal enum BoundaryResumeResult
        {
            Resumed,
            RetryLater,
            Invalid
        }

        private const int ApparelPreparationRetryInterval = 300;
        private const int WeaponPreparationRetryInterval = 300;
        private const int SubsequentWeaponPreparationSettleInterval = 30;
        private const int WeaponPreparationAttemptLimit = 6;
        private const int WeaponPreparationTimeLimit = 1200;
        private const int WeaponPreparationFailureCooldown = 1200;
        private const int EssentialPersonalFallbackRetryInterval = 2500;
        // Preserve a clean native job boundary at the locker without adding a
        // visible five-second pause before restoration begins.
        private const int NaturalLockerDwellTicks = 30;
        // An automatic idle return gets one additional thinker window at the
        // locker. This is long enough for newly available protected work to
        // retain the already-equipped set without recreating the old extended
        // Standing pause.
        private const int AutomaticIdleLockerDwellTicks = 120;
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");
        // StartJob compatibility patches can synchronously admit a rewritten
        // copy while this patch is admitting the exact retained boundary job.
        // Keep the retained entry available until the outer admission reports
        // success, but do not promote it again from inside that same call stack.
        private static readonly HashSet<Pawn> BoundaryResumeAdmissions =
            new HashSet<Pawn>();

        public static void Prefix(
            Pawn_JobTracker __instance,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref ThinkTreeDef thinkTree,
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

            // Some guards below replace the proposed job and return before the
            // ordinary restoration-completion check. Normalize and clear an
            // already satisfied restoration first so an expired Wait or
            // repeated departure proposal cannot preserve stale bookkeeping.
            if (component?.TryCompleteSatisfiedRestoration(pawn, state) == true)
                state = null;

            // A different StartJob call means a previously pending buffered job
            // was replaced without reporting successful completion. Do not let
            // interrupted or invalidated work consume a buffer slot.
            if (state != null)
                DiscardReplacedBufferCandidates(state, newJob.loadID);

            // StartJob normally records these arguments on the accepted Job.
            // An AOM apparel interception replaces that Job before the original
            // method runs, so preserve its native origin on the pending object
            // first. Designation jobs in particular need this exact context
            // when they are resumed from RimWorld's queue after preparation.
            if (newJob.jobGiver == null && jobGiver != null)
                newJob.jobGiver = jobGiver;
            if (newJob.jobGiverThinkTree == null && thinkTree != null)
                newJob.jobGiverThinkTree = thinkTree;

            // Preserved jobs re-enter StartJob from AOM state or RimWorld's
            // queue without the original call arguments. Feed the context saved
            // on the Job back into vanilla StartJob so it does not overwrite the
            // thinker references with null immediately before the next save.
            if (jobGiver == null && newJob.jobGiver != null)
                jobGiver = newJob.jobGiver;
            if (thinkTree == null && newJob.jobGiverThinkTree != null)
                thinkTree = newJob.jobGiverThinkTree;

            if (pawn.Downed)
            {
                if (state != null)
                {
                    bool assignedTransitionJob =
                        IsAssignedChangingAreaReturnJob(state, newJob) ||
                        IsAssignedTransitionApparelJob(state, newJob) ||
                        IsAssignedTransitionWeaponJob(state, newJob) ||
                        (state.Transition == ApparelTransition.Restoring &&
                         newJob.def == JobDefOf.Goto &&
                         newJob.targetA.Cell.IsValid &&
                         newJob.targetA.Cell == pawn.Position);

                    component.SuspendTransitionWhileDowned(state);

                    // A transition job may already have reached StartJob in the
                    // same tick that health made the pawn downed. Remove the
                    // remaining AOM queue and replace only that owned job with
                    // RimWorld's native incapacitated wait.
                    if (assignedTransitionJob)
                    {
                        __instance.ClearQueuedJobs(false);
                        Job downedWait = JobMaker.MakeJob(JobDefOf.Wait_Downed, pawn);
                        downedWait.expiryInterval = 30;
                        AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                            pawn, newJob);
                        newJob = downedWait;
                        jobGiver = null;
                        tag = null;
                    }
                }

                // This guard deliberately applies even when AOM has no state for
                // the pawn. A newly captured or visiting prisoner can be downed
                // inside a live work area; native Wait_Downed, LayDown, rescue,
                // tending, carrying, and modded medical jobs must pass through
                // without an outfit requirement or periodic interruption.
                return;
            }

            // Prison breaks and slave rebellions remain native even though the
            // pawn still has colony prisoner/slave status. Do this before access,
            // managed-stock, and weapon guards so native Equip, combat, and escape
            // jobs cannot be converted into AOM waits.
            if (PawnAccessClassifier.IsNativeCustodyEscapeActive(pawn))
            {
                UnavailableWorkRegistry.Clear(pawn, component?.Rules);
                if (state != null)
                {
                    component.EndIntervention(
                        pawn,
                        "native custody escape took control");
                }
                return;
            }

            // Drafting is a player/native combat override, not another buffered
            // civilian task. Preserve the current combat job, cancel the former
            // work continuation, and postpone saved-outfit restoration until the
            // pawn is undrafted.
            if (pawn.Drafted)
            {
                if (state != null)
                {
                    bool assignedTransitionJob =
                        IsAssignedChangingAreaReturnJob(state, newJob) ||
                        IsAssignedTransitionApparelJob(state, newJob) ||
                        IsAssignedTransitionWeaponJob(state, newJob);
                    bool firstDraftedSuspension =
                        !state.DraftedTransitionSuspended;
                    component.SuspendTransitionWhileDrafted(state);
                    if (firstDraftedSuspension)
                        __instance.ClearQueuedJobs(false);

                    // A transition job can already have been dequeued in the
                    // same tick that the player drafts the pawn. Letting that
                    // exact RemoveApparel/DropEquipment/Goto proceed starts the
                    // locker restoration while combat control is active. Cancel
                    // only the AOM-owned job and leave every drafted/native job
                    // untouched.
                    if (assignedTransitionJob)
                    {
                        __instance.ClearQueuedJobs(false);
                        ReplaceWithBriefWait(
                            pawn, ref newJob, ref jobGiver, ref tag);
                    }
                }
                return;
            }

            // Pawn_DraftController immediately asks for a civilian job inside
            // set_Drafted(false), before GameComponentTick can observe the
            // undraft. Resume the active buffer here as well so that very first
            // job counts normally instead of inheriting the draft-time Recall.
            if (state?.DraftedTransitionSuspended == true)
                component.ResumeTransitionAfterDrafted(state);

            // Urgent native safety behavior must run immediately even when its
            // route crosses a protected area or an outfit transition is open.
            // Keep the snapshot intact and let the next ordinary job resume the
            // normal preparation/restoration boundary after danger passes.
            if (IsNativeEmergencySafetyJob(newJob))
                return;

            bool mapDepartureJob = IsMapDepartureJob(newJob);
            if (mapDepartureJob)
            {
                // Native visitor departure is normally a Goto whose final toil
                // exits the map. Never interpret its route out of a work area as
                // fresh protected work. An active session must finish Phase 3;
                // a pawn without a session should simply keep leaving.
                if (state == null)
                    return;

                component.PrepareForMapDeparture(state);
                if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                        pawn, "map-departure-restoration"))
                {
                    AomLog.Detailed(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                        "native map departure requested; returning managed gear " +
                        "and restoring the saved outfit before leaving.");
                }
            }

            // The path-cell guard can discover a protected route only after a
            // native job has started. It records and ends that exact job before
            // the first protected cell, but RimWorld may propose an unrelated
            // activity on the next tracker tick—well before the component's
            // 30-tick recovery pulse. Promote the retained job at this shared
            // StartJob boundary so recreation or another thinker choice cannot
            // take ownership of the outfit transition first. Explicit player
            // orders remain authoritative and retire the stale retry instead.
            if (!mapDepartureJob)
            {
                PreferBoundaryInterruptedJob(
                    pawn, state, ref newJob, ref jobGiver,
                    ref thinkTree, ref tag);
            }

            bool assignedRecoveryTransition = state != null &&
                (IsAssignedChangingAreaReturnJob(state, newJob) ||
                 IsAssignedTransitionApparelJob(state, newJob) ||
                 IsAssignedTransitionWeaponJob(state, newJob));
            bool retainedBoundaryRepair = false;
            if (!mapDepartureJob && !assignedRecoveryTransition)
            {
                retainedBoundaryRepair =
                    PreferPendingBoundaryRepairDuringPreparation(
                    __instance, pawn, state, ref newJob,
                    ref jobGiver, ref thinkTree, ref tag);
            }
            if (!mapDepartureJob && !assignedRecoveryTransition &&
                !retainedBoundaryRepair &&
                PreparedIngestRetryRegistry.TryConsume(
                    pawn, newJob, out Job ingestRetry,
                    out string ingestRecoveryDescription))
            {
                AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                    pawn, newJob);
                if (ingestRetry != null)
                {
                    __instance.ClearQueuedJobs(false);
                    newJob = ingestRetry;
                    jobGiver = ingestRetry.jobGiver;
                    thinkTree = ingestRetry.jobGiverThinkTree;
                    tag = null;
                }
                else
                {
                    ReplaceWithBriefWait(
                        pawn, ref newJob, ref jobGiver, ref tag);
                    thinkTree = null;
                }

                if (AomLog.DetailedEnabled)
                {
                    AomLog.Detailed(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                        $"{ingestRecoveryDescription}.");
                }

                if (ingestRetry == null)
                    return;
            }

            if (AomLog.DetailedEnabled && IsDesignationSensitiveWork(newJob) &&
                state?.Transition == ApparelTransition.Preparing &&
                state.PendingWorkJob == null)
            {
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: starting preserved " +
                    $"native {newJob.def.defName} after preparation " +
                    $"(fromQueue={fromQueue}, giver=" +
                    $"{newJob.jobGiver?.GetType().Name ?? "none"}, tree=" +
                    $"{newJob.jobGiverThinkTree?.defName ?? "none"}).");
            }

            // Locker travel is an exact AOM transition, not an ordinary Goto.
            // Recognize the recorded destination before generic reservation,
            // paused-area, and activity guards can turn it into Wait. Natural
            // task-buffer completion does not set RecallRequested, so using the
            // recall flag as the ownership marker stranded automatic returns.
            if (IsAssignedChangingAreaReturnJob(state, newJob))
                return;

            bool assignedOutfitTransitionJob =
                IsAssignedTransitionApparelJob(state, newJob) ||
                IsAssignedTransitionWeaponJob(state, newJob);
            bool assignedSavedRestorationJob =
                state?.Transition == ApparelTransition.Restoring &&
                assignedOutfitTransitionJob;
            bool savedApparelReplacementJob =
                SavedApparelReplacementPolicy.CanStart(
                    pawn, state, component, __instance, newJob, jobGiver);

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
                if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                        pawn,
                        $"saved-gear-restoring:{restoringSavedGear.GetUniqueLoadID()}"))
                {
                    AomLog.Detailed(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: ignored automatic " +
                        $"{newJob.def.defName} for {restoringSavedGear.LabelCap}; " +
                        $"{restoringOwner.LabelShortCap} is restoring that exact saved item.");
                }

                __instance.ClearQueuedJobs(false);
                component.WakeRestoringSavedGearOwner(restoringOwner);

                // A 30-tick retry can beat an owner that still has one or more
                // managed layers to remove before its saved Wear job begins.
                // Keep this contender out of the race long enough for the owner
                // to rebuild and reserve the exact saved item.
                ReplaceWithWait(pawn, 300, ref newJob, ref jobGiver, ref tag);
                return;
            }

            // Bills can carry their ingredient through the same placement toil
            // used by hauling, and destructive bills can consume an exact saved
            // apparel item or primary weapon. Reject the automatic bill before
            // pickup even when its owner is not currently restoring. A
            // player-forced bill remains authoritative.
            if (!newJob.playerForced &&
                component?.SavedOwnerForBillTarget(
                    pawn, newJob, out Thing billedSavedGear) is Pawn billOwner)
            {
                if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                        pawn,
                        $"saved-gear-bill:{billedSavedGear.GetUniqueLoadID()}"))
                {
                    AomLog.Detailed(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: ignored automatic " +
                        $"{newJob.def.defName} for saved personal gear " +
                        $"{billedSavedGear.LabelCap} owned by {billOwner.LabelShortCap}.");
                }

                AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                    pawn, newJob);
                __instance.ClearQueuedJobs(false);
                component.WakeRestoringSavedGearOwner(billOwner);
                ReplaceWithWait(pawn, 300, ref newJob, ref jobGiver, ref tag);
                return;
            }

            // A work candidate can be selected just before another pawn claims
            // the same target for an outfit transition. Recheck at the common
            // job boundary so that candidate cannot start a second transition
            // in the small window between scanner and StartJob.
            // A short-lived work claim protects a preserved bill/haul target
            // while its worker changes into managed gear. It must not outrank
            // the exact personal item once that item's saved owner reaches
            // Phase 3. Native reservations still arbitrate the real Wear/Equip
            // job; this exception only prevents the runtime-only work claim
            // from replacing a valid restoration job with Wait forever.
            if (!assignedSavedRestorationJob &&
                ManagedWorkClaimRegistry.IsClaimedByOther(pawn, newJob))
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
            if (!assignedOutfitTransitionJob &&
                PausedAreaWorkFilter.TryRedirectWanderingJob(
                    pawn, newJob, jobGiver))
            {
                jobGiver = null;
                tag = null;
            }

            ApparelRule deniedWorkRule =
                assignedOutfitTransitionJob
                    ? null
                    : PausedAreaWorkFilter.DeniedOrdinaryWorkRule(pawn, newJob);
            if (deniedWorkRule != null)
            {
                if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                        pawn, $"work-disabled:{deniedWorkRule.Id}"))
                {
                    string category = PawnAccessClassifier.IsHostedGuest(pawn) ? "guest work" : "work";
                    AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: blocked from '{deniedWorkRule.Name}'; {category} is disabled.");
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
                assignedOutfitTransitionJob
                    ? null
                    : PausedAreaWorkFilter.DeniedHaulingRule(pawn, newJob);
            if (deniedHaulingRule != null)
            {
                if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                        pawn, $"hauling-disabled:{deniedHaulingRule.Id}"))
                {
                    string category = PawnAccessClassifier.IsHostedGuest(pawn)
                        ? "guest hauling"
                        : "hauling";
                    AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: blocked from '{deniedHaulingRule.Name}'; {category} is disabled.");
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
                assignedOutfitTransitionJob
                    ? null
                    : PausedAreaWorkFilter.DeniedPausedAreaRule(pawn, newJob);
            if (deniedPausedAreaRule != null)
            {
                if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                        pawn, $"paused-work-start:{newJob.def?.defName}"))
                {
                    AomLog.Detailed(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: blocked " +
                        $"{newJob.def?.defName ?? "job"} before it could enter a paused work area.");
                }

                if (state != null)
                {
                    component.RequestRulePauseRecall(
                        state, deniedPausedAreaRule);
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
                ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
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
                    state.AbandonWeaponManagementForOverride(
                        newJob.playerForced);
                else
                    state.MarkWeaponPlayerOverride(newJob.playerForced);
                if (AomLog.DetailedEnabled)
                    AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: {newJob.def.defName} is controlling weapons; the current choice is retained and the saved primary remains available for outfit restoration.");
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
                        state.AbandonWeaponManagementForOverride(
                            newJob.playerForced);
                    else
                        state.MarkWeaponPlayerOverride(newJob.playerForced);
                    if (AomLog.DetailedEnabled)
                        AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: {newJob.def.defName} selected by the player or another mod; the choice is retained until saved-outfit restoration.");
                }

                if (assignedTransition &&
                    state?.WeaponRuleOverrideExplicit == true &&
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
                    AutomaticOutfitManagerGameComponent.ClearPendingWork(state);
                    ManagedWorkClaimRegistry.ReleaseAll(pawn);
                    ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                    return;
                }

                if (assignedTransition &&
                    state?.Transition == ApparelTransition.Preparing &&
                    newJob.def == JobDefOf.Equip &&
                    state.IsManagedWeapon(equipmentTarget))
                {
                    // PendingWorkJob owns the interrupted task while this exact
                    // Equip owns preparation. Remove any stale duplicate steps
                    // left by an older save or a failed same-tick replan before
                    // RimWorld starts the single assigned Equip.
                    state.RecordWeaponPreparationAttempt(equipmentTarget);
                    __instance.ClearQueuedJobs(false);
                }

                // Weapon jobs are either an exact transition step or an external
                // player/mod decision. Neither should be reinterpreted as work
                // merely because its target lies inside a managed area.
                return;
            }

            if (state?.WeaponInterventionActive == true &&
                state.Transition == ApparelTransition.Active &&
                !state.WeaponPlayerOverride &&
                !state.IsManagedWeapon(pawn.equipment?.Primary))
            {
                state.MarkWeaponPlayerOverride();
                if (AomLog.DetailedEnabled)
                    AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: weapon changed outside Automatic Outfit Manager; the new choice is retained until saved-outfit restoration.");
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
                !savedApparelReplacementJob &&
                !IsAllowedTransitionWear(state, transitionWearTarget))
            {
                ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                return;
            }

            if (newJob.def == JobDefOf.Wear &&
                newJob.targetA.Thing is Apparel assignedPreparationApparel &&
                state?.Transition == ApparelTransition.Preparing &&
                IsAssignedTransitionApparelJob(state, newJob))
            {
                // Remember the exact preparation attempt before RimWorld runs
                // its native Wear driver. Fire, a transient reservation, or a
                // compatibility rejection can end that driver immediately. A
                // later non-apparel proposal then yields instead of rebuilding
                // the same Wear job repeatedly in one tick.
                state.LastApparelPreparationAttemptTick =
                    Find.TickManager?.TicksGame ?? 0;
                state.LastApparelPreparationThingId =
                    assignedPreparationApparel.thingIDNumber;
            }

            if (state?.RecallRequested == true &&
                state.Transition == ApparelTransition.Preparing &&
                ((newJob.def == JobDefOf.Wear &&
                  newJob.targetA.Thing is Apparel queuedAutomaticOutfitManager &&
                  state.IsPreparationApparel(queuedAutomaticOutfitManager)) ||
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
                AutomaticOutfitManagerGameComponent.ClearPendingWork(state);
                ManagedWorkClaimRegistry.ReleaseAll(pawn);
                ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                return;
            }

            if (newJob.def == JobDefOf.RemoveApparel &&
                newJob.targetA.Thing is Apparel environmentalRemovalTarget &&
                IsAssignedTransitionApparelJob(state, newJob) &&
                HazardousEnvironmentSafety.RemovalWouldExposePawn(
                    pawn, state, environmentalRemovalTarget,
                    out string removalHazardReason))
            {
                component?.RetainManagedProtectionForHazard(pawn, state);
                __instance.ClearQueuedJobs(false);
                AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                    pawn, newJob);
                ReplaceWithWait(
                    pawn, 300, ref newJob, ref jobGiver, ref tag);
                LogHazardProtectionHold(
                    pawn, removalHazardReason, "managed-apparel removal");
                return;
            }

            if (newJob.def == JobDefOf.Wear ||
                newJob.def == JobDefOf.RemoveApparel ||
                newJob.def == JobDefOf.Equip ||
                newJob.def == JobDefOf.DropEquipment)
                return;

            // A targetless autonomous wait inside a protected area is not a
            // work continuation worth outfitting for. If the pawn is safely
            // idle and missing required gear, leave through the existing safe
            // egress path before the preparation retry logic can cycle through
            // otherwise-valid weapon candidates merely to resume Standing.
            if (TryRedirectIdleMissingGearWaitWithEgress(
                    __instance, pawn, component, state,
                    ref newJob, ref jobGiver, ref tag))
            {
                return;
            }

            // Apparel jobs temporarily displace the work that requested them.
            // Prefer a fresh, structurally equivalent job selected by RimWorld
            // after preparation. If the immediate post-Wear proposal is not the
            // saved designation job, ask its original WorkGiver_Scanner to
            // recreate that exact target now. Designation jobs may carry driver
            // state that cannot safely be replayed from the intercepted object.
            // Other jobs keep their captured continuation so bills,
            // construction, and hauling retain their concrete targets.
            if (state?.Transition == ApparelTransition.Preparing &&
                HasCompletedPreparation(pawn, component, state) &&
                state.PendingWorkJob != null &&
                !SameJob(newJob, state.PendingWorkJob))
            {
                Job pendingWork = state.PendingWorkJob;
                string cancellationReason = PendingWorkCancellationReason(
                    pawn, state, newJob);
                if (cancellationReason != null)
                {
                    if (AomLog.DetailedEnabled)
                        AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: pending work continuation was cancelled ({cancellationReason}); returning to normal transition logic.");
                    AutomaticOutfitManagerGameComponent.ClearPendingWork(state);
                    ManagedWorkClaimRegistry.ReleaseAll(pawn);
                }
                else if (StructurallyEquivalentWorkJob(newJob, pendingWork))
                {
                    if (AomLog.DetailedEnabled)
                        AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: using RimWorld's fresh {newJob.def.defName} job for the prepared target instead of replaying the captured continuation.");
                    AutomaticOutfitManagerGameComponent.ClearPendingWork(state);
                    ManagedWorkClaimRegistry.ReleaseAll(pawn);
                }
                else if (IsDesignationSensitiveWork(pendingWork))
                {
                    if (TryRefreshDesignationSensitiveWork(
                            pawn, pendingWork, out Job refreshedWork,
                            out string refreshReason,
                            out bool targetRejected))
                    {
                        string proposedJobName =
                            newJob?.def?.defName ?? "no job";
                        __instance.ClearQueuedJobs(false);
                        __instance.jobQueue.EnqueueFirst(pendingWork);
                        AutomaticOutfitManagerGameComponent.ClearPendingWork(state);
                        string preservedJobName = pendingWork.def.defName;
                        string refreshedReport = refreshedWork.GetReport(pawn);
                        ReplaceWithBriefWait(
                            pawn, ref newJob, ref jobGiver, ref tag);
                        if (AomLog.DetailedEnabled)
                        {
                            AomLog.Detailed(
                                $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                                $"native {preservedJobName} remains valid for " +
                                $"'{refreshedReport}' after preparation; queued the " +
                                $"original job with its native context after proposed " +
                                $"{proposedJobName}.");
                        }
                        // Replacing the post-Wear proposal directly starts the
                        // designation driver inside the old Wear completion
                        // callback. A reconstructed Job also loses context that
                        // StartJob normally copies from its originating thinker.
                        // The preserved original now owns that context, while a
                        // one-toil wait gives RimWorld a clean stack before the
                        // queue starts it. Keep the concrete claim until the
                        // queued job reaches the normal matching-work path.
                        return;
                    }
                    else
                    {
                        if (targetRejected)
                            BlockPendingWorkRetry(pawn, pendingWork);
                        if (AomLog.DetailedEnabled)
                        {
                            AomLog.Detailed(
                                $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                                $"native refresh rejected {pendingWork.def.defName} " +
                                $"after preparation ({refreshReason}); " +
                                (targetRejected
                                    ? "delaying only that target before normal work selection resumes."
                                    : "releasing it for normal native work selection."));
                        }
                        AutomaticOutfitManagerGameComponent.ClearPendingWork(state);
                        ManagedWorkClaimRegistry.ReleaseAll(pawn);
                    }
                }
                else
                {
                    Job resumedJob = state.PendingWorkJob;
                    __instance.ClearQueuedJobs(false);
                    AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                        pawn, newJob);
                    newJob = resumedJob;
                    jobGiver = resumedJob.jobGiver;
                    thinkTree = resumedJob.jobGiverThinkTree;
                    tag = null;
                    if (AomLog.DetailedEnabled)
                        AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: resuming exact prepared job {newJob.def.defName} for '{ManagedWorkClaimRegistry.DescribeActiveClaim(pawn)}'.");
                    PreparedIngestRetryRegistry.RecordResumed(pawn, newJob);
                }
            }

            if (state?.Transition == ApparelTransition.Preparing &&
                state.LastApparelPreparationAttemptTick >= 0 &&
                !HasCompletedPreparation(pawn, component, state))
            {
                bool attemptedItemIsWorn = pawn.apparel?.WornApparel.Any(item =>
                    item?.thingIDNumber == state.LastApparelPreparationThingId) == true;
                int preparationTick = Find.TickManager?.TicksGame ?? 0;
                int elapsed = preparationTick -
                    state.LastApparelPreparationAttemptTick;
                if (attemptedItemIsWorn ||
                    elapsed >= ApparelPreparationRetryInterval)
                {
                    state.LastApparelPreparationAttemptTick = -1;
                    state.LastApparelPreparationThingId = -1;
                }
                else
                {
                    // The assigned Wear ended without putting its target on the
                    // pawn. Let fire, reservations, and mod-controlled apparel
                    // state settle before one bounded retry. Clearing the queue
                    // also removes any stale remaining transition steps.
                    __instance.ClearQueuedJobs(false);
                    int remaining = Math.Max(
                        30, ApparelPreparationRetryInterval - elapsed);
                    ReplaceWithWait(
                        pawn, remaining, ref newJob, ref jobGiver, ref tag);
                    if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                            pawn, "apparel-preparation-retry"))
                    {
                        AomLog.Detailed(
                            $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                            "required apparel Wear did not complete; yielding " +
                            "before one bounded preparation retry.");
                    }
                    return;
                }
            }

            if (state?.Transition == ApparelTransition.Preparing &&
                state.WeaponInterventionActive &&
                state.LastWeaponPreparationAttemptTick >= 0 &&
                !PreparationWeaponRequirementMatches(pawn, component, state))
            {
                int preparationTick = Find.TickManager?.TicksGame ?? 0;
                int elapsed = preparationTick -
                    state.LastWeaponPreparationAttemptTick;
                if (state.WeaponPreparationBudgetExceeded(
                        preparationTick, WeaponPreparationAttemptLimit,
                        WeaponPreparationTimeLimit))
                {
                    state.RejectLastWeaponPreparationAttempt();
                    if (TryAbortExhaustedWeaponPreparation(
                            __instance, pawn, component, state, null,
                            ref newJob, ref jobGiver, ref tag))
                    {
                        return;
                    }
                }

                int retryInterval = state.WeaponPreparationRetriesThisTransition == 0
                    ? WeaponPreparationRetryInterval
                    : SubsequentWeaponPreparationSettleInterval;
                if (elapsed < retryInterval)
                {
                    // A failed Equip makes RimWorld immediately reconsider the
                    // preserved work. The first candidate receives one real
                    // bounded retry. Later candidates only receive a short
                    // settlement window so a large locker inventory cannot turn
                    // per-item backoff into a long Standing sequence.
                    __instance.ClearQueuedJobs(false);
                    int remaining = Math.Max(
                        30, retryInterval - elapsed);
                    ReplaceWithWait(
                        pawn, remaining, ref newJob, ref jobGiver, ref tag);
                    if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                            pawn, "weapon-preparation-retry"))
                    {
                        AomLog.Detailed(
                            $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                            "required weapon Equip did not complete; yielding " +
                            "before one bounded preparation retry.");
                    }
                    return;
                }

                // The former retry window only waited and then rejected the
                // candidate; it never issued the promised second native Equip.
                // Give the exact still-available floor weapon one real retry.
                // Only after that concrete retry fails do we cool the instance
                // down and let the next planner pass choose an alternative.
                if (TryRetryWeaponPreparationCandidate(
                        __instance, pawn, component, state,
                        ref newJob, ref jobGiver, ref tag))
                {
                    return;
                }

                state.RejectLastWeaponPreparationAttempt();
                if (TryAbortExhaustedWeaponPreparation(
                        __instance, pawn, component, state, null,
                        ref newJob, ref jobGiver, ref tag))
                {
                    return;
                }
            }

            // Work in overlapping areas must satisfy the combined equipment
            // requirements before it begins. Previously the active outer rule
            // accepted the job and the path-cell safety check discovered the
            // missing nested gear at the doorway, causing an immediate
            // stop/reselect loop.
            bool hasManagedWorkContext = HasManagedWorkContext(
                newJob, jobGiver, state);
            bool managedWorkPreparation =
                PausedAreaWorkFilter.UsesManagedWorkPreparation(newJob);
            bool haulingActivity =
                PausedAreaWorkFilter.IsHaulingJob(newJob) &&
                !managedWorkPreparation;
            List<ApparelRule> protectedJobRules = ProtectedRulesForJob(pawn, newJob);
            List<ApparelRule> occupiedRules =
                RuleEvaluator.MatchingLocationRules(pawn);
            List<ApparelRule> matchingWorkRules =
                hasManagedWorkContext && managedWorkPreparation
                ? RuleEvaluator.MatchingRules(pawn, newJob)
                : new List<ApparelRule>();
            bool stagedBoundaryTransit = TrySelectBoundaryTransitStage(
                pawn, newJob, matchingWorkRules, occupiedRules,
                out List<ApparelRule> boundaryTransitRules);
            if (stagedBoundaryTransit)
            {
                // The actual path-cell interruption identifies the next rule
                // that must be crossed. When its outfit conflicts with the
                // eventual worksite outfit, prepare only that immediate stage.
                // The exact job remains pending and its destination rule will
                // be prepared after the pawn clears this protected crossing.
                protectedJobRules = boundaryTransitRules;
            }
            TryCancelAutomaticIdleReturnForProtectedJob(
                pawn, state, protectedJobRules, newJob);
            bool canPrepareForMatchingWork = state?.RecallRequested != true &&
                (state == null ||
                 state.Transition == ApparelTransition.Preparing ||
                 state.Transition == ApparelTransition.Active);
            if (canPrepareForMatchingWork && state != null && hasManagedWorkContext &&
                (matchingWorkRules.Count > 0 ||
                 state.Transition != ApparelTransition.Preparing))
            {
                state.CurrentRuleIds = (stagedBoundaryTransit
                        ? boundaryTransitRules.Concat(occupiedRules)
                        : matchingWorkRules)
                    .Where(rule => rule != null)
                    .Select(rule => rule.Id)
                    .Distinct()
                    .ToList();
            }
            if (TryAllowIncompatibleIngestWithCurrentOutfit(
                    pawn, component, state, newJob,
                    protectedJobRules, occupiedRules))
            {
                return;
            }
            if (TryBeginSequentialRuleHandoff(
                    __instance, pawn, component, state,
                    protectedJobRules, occupiedRules,
                    ref newJob, ref jobGiver, ref tag))
            {
                return;
            }
            TryBeginDirectRuleHandoff(
                pawn, component, state, matchingWorkRules,
                protectedJobRules, occupiedRules);
            if (!stagedBoundaryTransit && canPrepareForMatchingWork &&
                state != null && matchingWorkRules.Count > 0)
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
            if (!stagedBoundaryTransit && canPrepareForMatchingWork &&
                matchingWorkRules.Count > 0 &&
                TryPrepareForMatchingRules(
                    __instance, pawn, component, matchingWorkRules,
                    ref newJob, ref jobGiver, ref tag))
            {
                return;
            }
            if (!stagedBoundaryTransit && canPrepareForMatchingWork &&
                matchingWorkRules.Count > 0)
            {
                ManagedWorkClaimRegistry.Release(pawn, newJob);
                if (state != null && SameJob(newJob, state.PendingWorkJob))
                {
                    AutomaticOutfitManagerGameComponent.ClearPendingWork(state);
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
                    state.LastApparelPreparationAttemptTick = -1;
                    state.LastApparelPreparationThingId = -1;
                    state.ClearWeaponPreparationRetry();
                    state.ActiveIdleTicks = 0;
                    if (AomLog.DetailedEnabled)
                    {
                        AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: preparation complete; equipped rule set is active.");
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
                    if (HazardousEnvironmentSafety.MustRetainManagedProtectionAt(
                            pawn, state, pawn.Position,
                            out string restorationHazardReason))
                    {
                        component.RetainManagedProtectionForHazard(pawn, state);
                        __instance.ClearQueuedJobs(false);
                        AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                            pawn, newJob);
                        ReplaceWithWait(
                            pawn, 300, ref newJob, ref jobGiver, ref tag);
                        LogHazardProtectionHold(
                            pawn, restorationHazardReason,
                            "saved-outfit restoration");
                        return;
                    }

                    // QueueRestorationJobs owns every exact Phase 3 job. The
                    // previous retry guard treated the second queued job as a
                    // stale native proposal, cleared the remainder of the queue,
                    // and forced the component watchdog to rebuild after every
                    // individual garment. Let the validated queue advance
                    // directly; the recovery window below is only for unrelated
                    // jobs selected after the owned queue actually disappears.
                    if (assignedSavedRestorationJob)
                        return;

                    // A work candidate can be reconsidered while saved apparel
                    // is still being restored. Never let that stale candidate
                    // fall through to the normal missing-work-gear path or the
                    // pawn will alternate forever between the two outfits.
                    int restorationTick = Find.TickManager?.TicksGame ?? 0;
                    if (IsRecoveryWaitJob(newJob))
                        return;

                    int restorationRetryWindow =
                        state.UnavailableRestorationAttempts > 0 &&
                        !state.MapDepartureRequested
                            ? 2400
                            : 600;
                    if (state.LastRestorationAttemptTick >= 0 &&
                        restorationTick - state.LastRestorationAttemptTick <
                            restorationRetryWindow)
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
                    bool releasedUnavailableItem = false;
                    if (hasUnavailableSavedApparel)
                    {
                        releasedUnavailableItem =
                            component.TryReleaseStrandedRestorationItems(pawn, state);
                        releasedUnavailableItem |=
                            component.TryReleasePersistentlyUnavailableSavedWeapon(
                                pawn, state);
                    }
                    if (releasedUnavailableItem)
                    {
                        pendingRestorationJobs = RestorationPlanner.BuildJobs(
                            pawn, state, activeRule, out hasUnavailableSavedApparel);
                    }
                    if (state.MapDepartureRequested)
                    {
                        state.DepartureRestorationAttempts++;
                        if (component
                            .TryCompleteForeignMapDepartureWithUnavailableSavedGear(
                                pawn, state))
                        {
                            if (!mapDepartureJob)
                            {
                                __instance.ClearQueuedJobs(false);
                                ReplaceWithBriefWait(
                                    pawn, ref newJob, ref jobGiver, ref tag);
                            }
                            return;
                        }
                    }
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
                        if (component
                            .TryCompleteForeignMapDepartureWithUnavailableSavedGear(
                                pawn, state))
                        {
                            if (!mapDepartureJob)
                            {
                                __instance.ClearQueuedJobs(false);
                                ReplaceWithBriefWait(
                                    pawn, ref newJob, ref jobGiver, ref tag);
                            }
                            return;
                        }
                        __instance.ClearQueuedJobs(false);
                        ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);
                        return;
                    }

                    bool safeFollowupAfterRestoration =
                        protectedJobRules.Count == 0 &&
                        RuleEvaluator.MatchingLocationRules(pawn).Count == 0 &&
                        !PausedAreaWorkFilter.ShouldRejectPausedAreaJob(
                            pawn, newJob);
                    __instance.ClearQueuedJobs(false);
                    component.EndIntervention(pawn);

                    // Once the exact personal outfit is complete, keep a safe
                    // native proposal instead of replacing it with Wait. The
                    // forced reselection could immediately choose another job
                    // in the area and produce a visible restore/re-equip round
                    // trip. A protected or paused-area proposal still yields so
                    // the next clean StartJob pass performs ordinary preparation.
                    if (!safeFollowupAfterRestoration)
                        ReplaceWithBriefWait(
                            pawn, ref newJob, ref jobGiver, ref tag);
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
                // A targetless native/recovery Wait does not describe the cell
                // it protects. Once occupancy has activated a rule, keep its
                // complete outfit through that bounded Wait while the pawn is
                // still physically inside. Treating the same Wait as departure
                // made restoration enter the area, occupancy reactivate it, and
                // StartJob immediately restore again in an endless gear storm.
                bool waitsInsideActiveRule =
                    !state.RecallRequested &&
                    IsRecoveryWaitJob(newJob) &&
                    PawnInsideArea(pawn, activeRule?.Area);
                bool hazardousRouteRequiresProtection =
                    HazardousEnvironmentSafety.JobRequiresManagedProtection(
                        pawn, state, newJob, out string routeHazardReason);
                bool matchesActiveRule = waitsInsideActiveRule ||
                    hazardousRouteRequiresProtection ||
                    protectedJobRules.Any(candidate =>
                        candidate?.Id == activeRule?.Id);
                if (matchesActiveRule && !state.RecallRequested &&
                    state.Transition == ApparelTransition.ReturningToChangingArea)
                {
                    // A protected job became available while a natural return
                    // was settling at the locker. Keep the already-complete work
                    // outfit and reopen the session instead of finishing an
                    // expensive personal-outfit swap only to reverse it.
                    state.Transition = ApparelTransition.Active;
                    state.ChangingAreaReturnCell = IntVec3.Invalid;
                    state.LastChangingAreaReturnAttemptTick = -1;
                    state.NaturalLockerDwellUntilTick = -1;
                    state.ActiveIdleTicks = 0;
                    if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                            pawn, "natural-locker-dwell-retained"))
                    {
                        AomLog.Detailed(
                            $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                            "new protected activity became available during the " +
                            "locker pause; retaining the managed outfit.");
                    }
                }
                if (hazardousRouteRequiresProtection &&
                    !waitsInsideActiveRule &&
                    !protectedJobRules.Any(candidate =>
                        candidate?.Id == activeRule?.Id))
                {
                    LogHazardProtectionHold(
                        pawn, routeHazardReason,
                        $"route for {newJob.def?.defName ?? "job"}");
                }
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
                // A thinker proposal selected while recall is already committed
                // will be replaced by the locker return below. Do not erase
                // completed buffer progress for work that never takes control.
                if (startsMeaningfulWorkInArea && !state.RecallRequested)
                {
                    state.LastManagedWorkJobDefName = newJob.def.defName;
                    if (AomLog.DetailedEnabled && state.BufferedTasksCompleted > 0 &&
                        AomLog.ShouldLogDetailed(
                            pawn, $"task-buffer-reset:{activeRule?.Id}"))
                        AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: task buffer reset by {newJob.def.defName} in '{activeRule?.Name}'.");
                    state.BufferedTasksCompleted = 0;
                    state.LastBufferedJobLoadId = -1;
                    state.ClearPendingBufferedTask();
                    state.NaturalLockerDwellUntilTick = -1;
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
                        newJob.loadID != state.LastBufferedJobLoadId &&
                        newJob.loadID != state.PendingBufferedJobLoadId)
                    {
                        state.PendingBufferedJobLoadId = newJob.loadID;
                        state.PendingBufferedRuleId = activeRule.Id;
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
                    bool reachedRecordedRestorationCell =
                        state.Transition ==
                            ApparelTransition.ReturningToChangingArea &&
                        state.ChangingAreaReturnCell.IsValid &&
                        pawn.Position == state.ChangingAreaReturnCell &&
                        activeRule?.Area?.Map == pawn.Map &&
                        !insideProtectedArea;
                    if (!reachedRecordedRestorationCell &&
                        activeRule?.ChangingArea?.Map != null &&
                        activeRule.ChangingArea.Map != pawn.Map &&
                        TryMakeCrossMapChangingAreaReturnJob(
                            pawn, activeRule.ChangingArea.Map,
                            out Job portalReturnJob, out MapPortal returnPortal))
                    {
                        int returnTick = Find.TickManager?.TicksGame ?? 0;
                        if (state.LastChangingAreaReturnAttemptTick >= 0 &&
                            returnTick - state.LastChangingAreaReturnAttemptTick < 30)
                        {
                            __instance.ClearQueuedJobs(false);
                            ReplaceWithBriefWait(
                                pawn, ref newJob, ref jobGiver, ref tag);
                            return;
                        }

                        // Pocket and underground maps are separate Map instances.
                        // A same-map Goto can never reach the gravship locker from
                        // there, so use RimWorld's own portal job for the map
                        // transfer and keep the complete work outfit throughout.
                        // The next native job selected on the destination map
                        // re-enters this shared path and continues to the locker.
                        state.Transition =
                            ApparelTransition.ReturningToChangingArea;
                        state.LastChangingAreaReturnAttemptTick = returnTick;
                        state.ChangingAreaReturnCell = IntVec3.Invalid;
                        __instance.ClearQueuedJobs(false);
                        AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                            pawn, newJob);
                        newJob = portalReturnJob;
                        jobGiver = null;
                        tag = null;

                        if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                                pawn, "cross-map-locker-return"))
                        {
                            AomLog.Detailed(
                                $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                                $"task buffer complete; entering {returnPortal.LabelCap} " +
                                "to return to the locker map.");
                        }
                        return;
                    }

                    if (!reachedRecordedRestorationCell &&
                        (insideProtectedArea || outsidePreferredChangingArea) &&
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
                        AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                            pawn, newJob);
                        newJob = MakeChangingAreaTravelJob(changingCell);
                        newJob.expiryInterval = 2000;
                        newJob.locomotionUrgency = LocomotionUrgency.Jog;
                        jobGiver = null;
                        tag = null;
                        return;
                    }

                    if (HazardousEnvironmentSafety.MustRetainManagedProtectionAt(
                            pawn, state, pawn.Position,
                            out string departureHazardReason))
                    {
                        component.RetainManagedProtectionForHazard(pawn, state);
                        __instance.ClearQueuedJobs(false);
                        AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                            pawn, newJob);
                        ReplaceWithWait(
                            pawn, 300, ref newJob, ref jobGiver, ref tag);
                        LogHazardProtectionHold(
                            pawn, departureHazardReason,
                            "work-area departure");
                        return;
                    }

                    // RimWorld starts the first civilian job synchronously from
                    // Pawn_DraftController.set_Drafted(false), before the next
                    // game-component pulse can finish the undraft handoff. A
                    // gravship worker may also be undrafted on another map where
                    // the configured locker cannot be reached. In both cases,
                    // retain the complete managed outfit until the pawn reaches
                    // the safe locker/exterior cell chosen by AOM. Ordinary
                    // native activity may continue while that locker is on a
                    // different map, but restoration must not begin in place.
                    if (state.DraftedLockerReturnRequired)
                    {
                        bool alreadyInSafeLocker =
                            activeRule?.ChangingArea?.Map == pawn.Map &&
                            PawnInsideArea(pawn, activeRule.ChangingArea);
                        bool safeToRestore =
                            (reachedRecordedRestorationCell ||
                             alreadyInSafeLocker) &&
                            !PawnInsideStateProtectedArea(
                                pawn, component, state) &&
                            !HazardousEnvironmentSafety.MustRetainManagedProtectionAt(
                                pawn, state, pawn.Position, out _);

                        if (!safeToRestore)
                        {
                            state.Transition = ApparelTransition.Active;
                            state.RecallRequested = true;
                            state.AutomaticIdleReturnRequested = false;
                            state.RecallInterruptPending = false;
                            state.ChangingAreaReturnCell = IntVec3.Invalid;
                            state.LastChangingAreaReturnAttemptTick = -1;
                            state.LastRestorationAttemptTick = -1;

                            if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                                    pawn, "post-draft-locker-return"))
                            {
                                AomLog.Detailed(
                                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                                    "undrafted away from a safe locker return " +
                                    "cell; retaining the complete work outfit.");
                            }

                            // If the locker is on this map, yield briefly so a
                            // cell occupied by another simultaneously undrafted
                            // pawn can become available. When it is on another
                            // map, preserve the native local job and recheck on
                            // every later StartJob after the pawn changes maps.
                            if (activeRule?.ChangingArea?.Map == pawn.Map)
                            {
                                __instance.ClearQueuedJobs(false);
                                AutomaticOutfitManagerGameComponent
                                    .ReleaseNativeReservations(pawn, newJob);
                                ReplaceWithBriefWait(
                                    pawn, ref newJob, ref jobGiver, ref tag);
                            }
                            return;
                        }

                        state.DraftedLockerReturnRequired = false;
                    }

                    int currentTick = Find.TickManager?.TicksGame ?? 0;
                    bool naturalReturnAtLocker =
                        (!state.RecallRequested ||
                         state.AutomaticIdleReturnRequested) &&
                        state.Transition != ApparelTransition.Restoring &&
                        activeRule?.ChangingArea?.Map == pawn.Map &&
                        PawnInsideArea(pawn, activeRule.ChangingArea) &&
                        !insideProtectedArea &&
                        !RequiresImmediateRestoration(newJob);
                    if (naturalReturnAtLocker &&
                        state.Transition == ApparelTransition.Active &&
                        !state.AutomaticIdleReturnRequested &&
                        activeRule.ReturnTaskBuffer > state.BufferedTasksCompleted)
                    {
                        // Passing through or naturally using the locker is not
                        // itself completion of the configured task buffer. Keep
                        // the managed outfit active and let the native job run;
                        // the progress-aware idle fallback will still recall the
                        // pawn if no further qualifying work becomes available.
                        state.NaturalLockerDwellUntilTick = -1;
                        return;
                    }
                    if (naturalReturnAtLocker)
                    {
                        if (state.NaturalLockerDwellUntilTick < 0)
                        {
                            int lockerDwellTicks =
                                state.AutomaticIdleReturnRequested
                                    ? AutomaticIdleLockerDwellTicks
                                    : NaturalLockerDwellTicks;
                            state.Transition =
                                ApparelTransition.ReturningToChangingArea;
                            state.ChangingAreaReturnCell = pawn.Position;
                            state.LastChangingAreaReturnAttemptTick = currentTick;
                            state.NaturalLockerDwellUntilTick =
                                currentTick + lockerDwellTicks;
                            __instance.ClearQueuedJobs(false);
                            AutomaticOutfitManagerGameComponent
                                .ReleaseNativeReservations(pawn, newJob);
                            ReplaceWithWait(
                                pawn, lockerDwellTicks,
                                ref newJob, ref jobGiver, ref tag);
                            if (AomLog.DetailedEnabled)
                            {
                                string lockerReason =
                                    state.AutomaticIdleReturnRequested
                                        ? "automatic idle return reached the locker"
                                        : activeRule.ReturnTaskBuffer <=
                                          state.BufferedTasksCompleted
                                            ? "task buffer complete"
                                            : "outfit handoff reached the locker";
                                AomLog.Detailed(
                                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                                    $"{lockerReason}; pausing briefly in the " +
                                    "locker before saved-outfit restoration.");
                            }
                            return;
                        }

                        if (currentTick < state.NaturalLockerDwellUntilTick)
                        {
                            __instance.ClearQueuedJobs(false);
                            AutomaticOutfitManagerGameComponent
                                .ReleaseNativeReservations(pawn, newJob);
                            ReplaceWithWait(
                                pawn,
                                Math.Max(
                                    30,
                                    state.NaturalLockerDwellUntilTick - currentTick),
                                ref newJob, ref jobGiver, ref tag);
                            return;
                        }

                        state.NaturalLockerDwellUntilTick = -1;
                        state.AutomaticIdleReturnRequested = false;
                    }

                    int restorationRetryWindow =
                        state.UnavailableRestorationAttempts > 0 &&
                        !state.MapDepartureRequested
                            ? 2400
                            : 600;
                    if (state.Transition == ApparelTransition.Restoring &&
                        state.LastRestorationAttemptTick >= 0 &&
                        currentTick - state.LastRestorationAttemptTick <
                            restorationRetryWindow)
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
                    bool releasedUnavailableItem = false;
                    if (hasUnavailableOriginal)
                    {
                        releasedUnavailableItem =
                            component.TryReleaseStrandedRestorationItems(pawn, state);
                        releasedUnavailableItem |=
                            component.TryReleasePersistentlyUnavailableSavedWeapon(
                                pawn, state);
                    }
                    if (releasedUnavailableItem)
                    {
                        restorationJobs = RestorationPlanner.BuildJobs(
                            pawn, state, activeRule, out hasUnavailableOriginal);
                    }
                    if (state.MapDepartureRequested)
                    {
                        state.Transition = ApparelTransition.Restoring;
                        state.DepartureRestorationAttempts++;
                        if (component
                            .TryCompleteForeignMapDepartureWithUnavailableSavedGear(
                                pawn, state))
                        {
                            if (!mapDepartureJob)
                            {
                                __instance.ClearQueuedJobs(false);
                                ReplaceWithBriefWait(
                                    pawn, ref newJob, ref jobGiver, ref tag);
                            }
                            return;
                        }
                    }
                    if (restorationJobs.Count > 0)
                    {
                        state.Transition = ApparelTransition.Restoring;
                        state.LastRestorationAttemptTick = currentTick;
                        state.UnavailableRestorationAttempts = hasUnavailableOriginal
                            ? state.UnavailableRestorationAttempts + 1
                            : 0;
                        QueueRestorationJobs(__instance, ref newJob, ref jobGiver, ref tag, restorationJobs);

                        if (AomLog.DetailedEnabled)
                            AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: restoring saved apparel and primary weapon with {restorationJobs.Count} job(s) before {__instance.curJob?.def?.defName ?? "next job"}.");
                        return;
                    }

                    if (hasUnavailableOriginal)
                    {
                        state.Transition = ApparelTransition.Restoring;
                        state.LastRestorationAttemptTick = currentTick;
                        state.UnavailableRestorationAttempts++;
                        if (component
                            .TryCompleteForeignMapDepartureWithUnavailableSavedGear(
                                pawn, state))
                        {
                            if (!mapDepartureJob)
                            {
                                __instance.ClearQueuedJobs(false);
                                ReplaceWithBriefWait(
                                    pawn, ref newJob, ref jobGiver, ref tag);
                            }
                            return;
                        }
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
                ManagedApparelClassifier.Matches(newJob.targetA.Thing) &&
                protectedJobRules.Count == 0 && occupiedRules.Count == 0)
            {
                // Locker restocking outside every protected area remains an
                // ordinary haul. If its destination or actual route enters an
                // active rule, continue into the general protection path so the
                // pawn prepares once and resumes the same haul as a Hauler.
                return;
            }

            // Current occupancy is deliberately included here as a second line
            // of defense. It repairs loaded saves, area edits, forced apparel
            // changes, and gear loss that leave a pawn inside between job starts.
            List<ApparelRule> applicableRules = protectedJobRules
                .Concat(occupiedRules)
                .Where(candidate => candidate != null)
                .GroupBy(candidate => candidate.Id)
                .Select(group => group.First())
                .ToList();
            if (applicableRules.Count == 0)
                return;

            bool nativePrisonerFallback =
                IsNativePrisonerUnavailableGearFallbackJob(pawn, newJob);
            bool essentialPersonalFallback =
                PausedAreaWorkFilter.IsEssentialPersonalJob(newJob);
            bool essentialPersonalFallbackMayRemainInside =
                essentialPersonalFallback &&
                protectedJobRules.All(rule =>
                    PawnInsideArea(pawn, rule?.Area));
            bool unavailableGearFallback =
                nativePrisonerFallback ||
                essentialPersonalFallbackMayRemainInside;
            string nativePrisonerFallbackAction = newJob.def == JobDefOf.Wait_Wander
                ? "allowing native cell wandering"
                : $"allowing native prisoner {newJob.def.defName}";
            string unavailableGearFallbackAction = nativePrisonerFallback
                ? nativePrisonerFallbackAction
                : essentialPersonalFallback
                    ? $"allowing essential {newJob.def.defName}"
                    : $"delaying {newJob.def.defName}";
            int unavailableBlockTicks = essentialPersonalFallback &&
                !nativePrisonerFallback
                ? EssentialPersonalFallbackRetryInterval
                : 1200;

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
                    UnavailableWorkRegistry.Block(
                        pawn, blockedRule, unavailableBlockTicks);
                string reason = unwearableRule != null
                    ? $"required apparel for '{unwearableRule.Name}' cannot be worn"
                    : transitConflict != null
                        ? $"required apparel is incompatible: {transitConflict.Label}"
                        : "overlapping rules require different primary weapons";
                if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                        pawn, $"unwearable:{string.Join(",", applicableRules.Select(rule => rule.Id))}"))
                {
                    string action = unavailableGearFallback
                        ? $"{unavailableGearFallbackAction}; {reason}"
                        : $"delaying {newJob.def.defName}; {reason}";
                    AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: {action}.");
                }
                if (unavailableGearFallback)
                    return;
                if (TryReplaceUnavailableGearWaitWithEgress(
                        __instance, pawn, ref newJob, ref jobGiver, ref tag))
                {
                    return;
                }
                __instance.ClearQueuedJobs(false);
                ReplaceWithWait(pawn, 300, ref newJob, ref jobGiver, ref tag);
                return;
            }

            var requiredByDef = new Dictionary<ThingDef, ApparelRule>();
            var standardsByDef = new Dictionary<ThingDef, List<ApparelRule>>();
            foreach (ApparelRule applicableRule in applicableRules)
            {
                foreach (ThingDef def in applicableRule.RequiredApparel ??
                         Enumerable.Empty<ThingDef>())
                {
                    if (def == null)
                        continue;
                    if (!requiredByDef.ContainsKey(def))
                        requiredByDef.Add(def, applicableRule);
                    if (!standardsByDef.TryGetValue(
                            def, out List<ApparelRule> standards))
                    {
                        standards = new List<ApparelRule>();
                        standardsByDef.Add(def, standards);
                    }
                    standards.Add(applicableRule);
                }
            }
            List<ThingDef> missing = requiredByDef.Keys
                .Where(def => !pawn.apparel.WornApparel.Any(item =>
                    item?.def == def &&
                    standardsByDef[def].All(rule => rule.Allows(item))))
                .ToList();
            bool missingWeapon = !combinedWeaponRequirement.Matches(
                pawn.equipment?.Primary);
            PawnApparelState weaponState = component?.StateFor(pawn);
            bool weaponChoiceProtected = missingWeapon &&
                weaponState?.WeaponRuleOverrideExplicit == true;
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
                if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                        pawn, $"weapon-player-control:{string.Join(",", applicableRules.Select(rule => rule.Id))}"))
                {
                    AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: continuing {newJob.def.defName} with the player's current primary weapon; the weapon requirement is skipped while that choice is protected.");
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
                    activeState.ClearWeaponPreparationRetry();
                    if (AomLog.DetailedEnabled)
                        AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: preparation complete; equipped rule set is active.");
                }

                // Assigned/player-forced jobs commonly have no workGiverDef and
                // therefore reach this general protection path. Once their exact
                // pending continuation has been restored, release its temporary
                // claim and clear the deep-saved handoff just as the ordinary
                // work-giver path does.
                ManagedWorkClaimRegistry.Release(pawn, newJob);
                if (activeState != null && SameJob(newJob, activeState.PendingWorkJob))
                {
                    if (stagedBoundaryTransit &&
                        activeState.PendingBoundaryRuleIds?.Count > 0)
                    {
                        AutomaticOutfitManagerGameComponent
                            .TransferPendingBoundaryWorkToTracker(activeState);
                    }
                    else
                    {
                        AutomaticOutfitManagerGameComponent.ClearPendingWork(activeState);
                    }
                }
                else
                {
                    ProtectedBoundaryRetryRegistry.Clear(pawn, newJob);
                }
                return;
            }

            var transitionJobs = new List<Job>();
            var managedApparel = new List<Apparel>();
            HashSet<Thing> pendingJobTargets = JobThingTargets(newJob);
            foreach (ThingDef def in missing)
            {
                ApparelRule sourceRule = requiredByDef[def];
                Apparel apparel = ApparelFinder.FindBest(
                    pawn, def, sourceRule.ChangingArea, pendingJobTargets,
                    standardsByDef[def]);
                if (apparel == null)
                {
                    if (essentialPersonalFallback)
                    {
                        foreach (ApparelRule blockedRule in applicableRules)
                        {
                            UnavailableWorkRegistry.Block(
                                pawn, blockedRule, unavailableBlockTicks);
                        }
                    }
                    else
                    {
                        UnavailableWorkRegistry.Block(
                            pawn, sourceRule, unavailableBlockTicks);
                    }
                    if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                            pawn, $"gear-unavailable:{sourceRule.Id}:{def.defName}"))
                    {
                        string action = unavailableGearFallback
                            ? unavailableGearFallbackAction
                            : $"delaying {newJob.def.defName}";
                        AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: {action}; no reachable {def.LabelCap} is available for '{sourceRule.Name}'.");
                    }
                    if (unavailableGearFallback)
                        return;
                    if (TryReplaceUnavailableGearWaitWithEgress(
                            __instance, pawn, ref newJob, ref jobGiver, ref tag))
                    {
                        return;
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
                    pawn, combinedWeaponRequirement, weaponRule.ChangingArea,
                    pendingJobTargets);
                if (managedWeapon == null)
                {
                    if (essentialPersonalFallback)
                    {
                        foreach (ApparelRule blockedRule in applicableRules)
                        {
                            UnavailableWorkRegistry.Block(
                                pawn, blockedRule, unavailableBlockTicks);
                        }
                    }
                    else
                    {
                        UnavailableWorkRegistry.Block(
                            pawn, weaponRule, unavailableBlockTicks);
                    }
                    if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                            pawn, $"weapon-unavailable:{weaponRule.Id}:{weaponRule.WeaponSummary}"))
                    {
                        string action = unavailableGearFallback
                            ? unavailableGearFallbackAction
                            : $"delaying {newJob.def.defName}";
                        AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: {action}; no reachable {weaponRule.WeaponSummary.ToLowerInvariant()} is available for '{weaponRule.Name}'.");
                    }
                    if (unavailableGearFallback)
                        return;
                    if (TryReplaceUnavailableGearWaitWithEgress(
                            __instance, pawn, ref newJob, ref jobGiver, ref tag))
                    {
                        return;
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
                if (managedWeapon != null &&
                    transitionJobs[0].def == JobDefOf.Equip)
                {
                    // Only the first transition step bypasses this Prefix when
                    // it replaces the intercepted job directly. A queued Equip
                    // records its attempt when it actually reaches StartJob;
                    // starting its retry clock while earlier Wear jobs run can
                    // reject the weapon before the pawn ever tries to equip it.
                    preparedState.RecordWeaponPreparationAttempt(managedWeapon);
                }
                AutomaticOutfitManagerGameComponent.CapturePendingWork(
                    preparedState, newJob, false);
                preparedState.PendingBoundaryRuleIds =
                    ProtectedBoundaryRetryRegistry.MatchingRules(pawn, newJob)
                        .Select(candidate => candidate.Id)
                        .Distinct()
                        .ToList();
                preparedState.PendingBoundaryWorkJobLoadId =
                    preparedState.PendingBoundaryRuleIds.Count > 0
                        ? newJob.loadID
                        : -1;
                preparedState.CurrentRuleIds = applicableRules
                    .Select(candidate => candidate.Id)
                    .Distinct()
                    .ToList();
            }

            if (AomLog.DetailedEnabled)
            {
                string ruleNames = string.Join(", ",
                    applicableRules.Select(candidate => $"'{candidate.Name}'"));
                string weaponAssignment = managedWeapon == null
                    ? "no weapon"
                    : $"weapon {managedWeapon.LabelCap} [{managedWeapon.def.defName}]";
                AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: intercepted {newJob.def.defName}; preparing {managedApparel.Count} apparel item(s) and {weaponAssignment} for {ruleNames}.");
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

            return state.WeaponRuleOverrideExplicit ||
                   weaponRequirement.Matches(pawn.equipment?.Primary);
        }

        private static bool PreparationWeaponRequirementMatches(
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            PawnApparelState state)
        {
            if (pawn?.equipment == null || component == null || state == null)
                return false;
            if (state.WeaponRuleOverrideExplicit)
                return true;

            List<ApparelRule> preparedRules = (state.CurrentRuleIds ??
                    new List<string>())
                .Select(component.RuleById)
                .Where(rule => rule?.Enabled == true)
                .ToList();
            if (preparedRules.Count == 0)
            {
                ApparelRule activeRule = component.RuleById(state.ActiveRuleId);
                if (activeRule?.Enabled == true)
                    preparedRules.Add(activeRule);
            }

            return preparedRules.Count > 0 &&
                   RuleEvaluator.TryCombinedWeaponRequirement(
                       preparedRules, out CombinedWeaponRequirement requirement) &&
                   requirement.Matches(pawn.equipment.Primary);
        }

        private static bool TryRetryWeaponPreparationCandidate(
            Pawn_JobTracker tracker,
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            PawnApparelState state,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref JobTag? tag)
        {
            ThingWithComps candidate = state?.ManagedWeapons?
                .FirstOrDefault(weapon => weapon != null &&
                    weapon.thingIDNumber == state.LastWeaponPreparationThingId);
            if (candidate == null || candidate.Destroyed || !candidate.Spawned ||
                candidate.Map != pawn?.Map || candidate.IsForbidden(pawn) ||
                !EquipmentUtility.CanEquip(candidate, pawn) ||
                component?.IsSavedWeaponForOtherPawn(candidate, pawn) == true ||
                component?.IsManagedWeaponAssignedToOtherPawn(candidate, pawn) == true ||
                !ReservationUtility_SavedApparel_Patch
                    .CanReserveForOutfit(pawn, candidate) ||
                !pawn.CanReach(
                    candidate, PathEndMode.ClosestTouch, Danger.Deadly) ||
                !state.TryUseWeaponPreparationRetry(candidate))
            {
                return false;
            }

            AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                pawn, newJob);
            tracker.ClearQueuedJobs(false);
            Job retry = JobMaker.MakeJob(JobDefOf.Equip, candidate);
            retry.playerForced = false;
            newJob = retry;
            jobGiver = null;
            tag = null;

            if (AomLog.DetailedEnabled)
            {
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: retrying " +
                    $"the exact required work weapon {candidate.LabelCap} once " +
                    "before considering another locker candidate.");
            }
            return true;
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
                    if (AomLog.DetailedEnabled)
                        AomLog.Detailed($"[AutomaticOutfitManager] {state.Pawn?.LabelShortCap}: nested task buffer started for '{rule.Name}' (0/{rule.ReturnTaskBuffer}).");
                }
                else
                {
                    progress.Completed = 0;
                    progress.Finished = false;
                    progress.LastJobLoadId = -1;
                    progress.LastJobLabel = null;
                    progress.PendingJobLoadId = -1;
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

                if (newJob.loadID == progress.LastJobLoadId ||
                    newJob.loadID == progress.PendingJobLoadId)
                    continue;

                if (progress.Completed < nestedRule.ReturnTaskBuffer)
                {
                    progress.PendingJobLoadId = newJob.loadID;
                    state.LastNestedBufferStatus =
                        $"{nestedRule.Name}: task {progress.Completed + 1} of {nestedRule.ReturnTaskBuffer} in progress" +
                        (string.IsNullOrEmpty(newJob.GetReport(pawn))
                            ? "."
                            : $"; current: {newJob.GetReport(pawn)}.");
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
                if (AomLog.DetailedEnabled)
                    AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: nested task buffer complete for '{nestedRule.Name}'; removing {removalJobs.Count} nested transition job(s).");
                return true;
            }

            return false;
        }

        private static void TryBeginDirectRuleHandoff(
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            PawnApparelState state,
            List<ApparelRule> matchingWorkRules,
            List<ApparelRule> protectedJobRules,
            List<ApparelRule> occupiedRules)
        {
            if (pawn == null || component == null || state == null ||
                state.Transition != ApparelTransition.Active ||
                state.RecallRequested)
            {
                return;
            }

            List<ApparelRule> destinationRules =
                (protectedJobRules ?? new List<ApparelRule>())
                .Concat(occupiedRules ?? new List<ApparelRule>())
                .Where(rule => rule?.Enabled == true &&
                               !rule.WorkAreaPaused &&
                               rule.Area?.Map == pawn.Map)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
            if (destinationRules.Count == 0 ||
                destinationRules.Any(rule => rule.Id == state.ActiveRuleId))
            {
                return;
            }

            // Prefer the rule that owns the real work, then the protected route,
            // and finally current occupancy for a passive Wait. This is a sibling
            // handoff, not a nested entry: retain the one true personal-outfit
            // snapshot and the complete managed-item ledger, but retire all
            // buffers and preparation throttles owned by the previous rule.
            ApparelRule destination = matchingWorkRules?
                .FirstOrDefault(rule => destinationRules.Any(
                    candidate => candidate.Id == rule?.Id)) ??
                protectedJobRules?
                    .FirstOrDefault(rule => destinationRules.Any(
                        candidate => candidate.Id == rule?.Id)) ??
                destinationRules[0];
            string previousRuleName =
                component.RuleById(state.ActiveRuleId)?.Name ?? "previous rule";

            state.ActiveRuleId = destination.Id;
            state.CurrentRuleIds = destinationRules
                .Select(rule => rule.Id)
                .Distinct()
                .ToList();
            state.BufferedTasksCompleted = 0;
            state.LastBufferedJobLoadId = -1;
            state.ClearPendingBufferedTask();
            state.NestedRuleBuffers?.Clear();
            state.LastNestedBufferStatus = null;
            state.LastApparelPreparationAttemptTick = -1;
            state.LastApparelPreparationThingId = -1;
            state.ClearWeaponPreparationRetry();
            state.ActiveIdleTicks = 0;
            ManagedWorkClaimRegistry.ReleaseAll(pawn);

            if (AomLog.DetailedEnabled)
            {
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: handing off " +
                    $"directly from '{previousRuleName}' to '{destination.Name}'; " +
                    "retaining the original personal-outfit snapshot.");
            }
        }

        private static void DiscardReplacedBufferCandidates(
            PawnApparelState state, int incomingJobLoadId)
        {
            if (state.PendingBufferedJobLoadId >= 0 &&
                state.PendingBufferedJobLoadId != incomingJobLoadId)
            {
                state.ClearPendingBufferedTask();
            }

            foreach (NestedRuleBufferState progress in
                     state.NestedRuleBuffers ?? new List<NestedRuleBufferState>())
            {
                if (progress != null && progress.PendingJobLoadId >= 0 &&
                    progress.PendingJobLoadId != incomingJobLoadId)
                {
                    progress.PendingJobLoadId = -1;
                }
            }
        }

        private static bool TryCancelAutomaticIdleReturnForProtectedJob(
            Pawn pawn,
            PawnApparelState state,
            IEnumerable<ApparelRule> protectedJobRules,
            Job newJob)
        {
            if (pawn == null || state?.AutomaticIdleReturnRequested != true ||
                state.Transition == ApparelTransition.Restoring ||
                !IsBufferableJob(newJob))
            {
                return false;
            }

            List<ApparelRule> rules = (protectedJobRules ??
                    Enumerable.Empty<ApparelRule>())
                .Where(rule => rule?.Enabled == true && !rule.WorkAreaPaused &&
                               rule.Area?.Map == pawn.Map)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
            if (!rules.Any(rule => rule.Id == state.ActiveRuleId) ||
                UnavailableWorkRegistry.ShouldReject(pawn, newJob))
            {
                return false;
            }

            // Reopen the session only when the complete destination rule set is
            // already safe under the outfit being retained. Checking merely for
            // the active rule allowed an essential job spanning incompatible
            // areas (for example Kitchen + ship LayDown) to cancel its return,
            // get rejected for the second rule, and repeat forever.
            if (rules.Any(rule =>
                    !RuleEvaluator.RuleCanApplyToPawn(pawn, rule) ||
                    RuleEvaluator.HasMissingRequiredApparel(pawn, rule)) ||
                ApparelCompatibility.FindConflict(
                    rules, pawn.RaceProps?.body) != null ||
                !RuleEvaluator.TryCombinedWeaponRequirement(
                    rules, out CombinedWeaponRequirement weaponRequirement) ||
                (!state.WeaponRuleOverrideExplicit &&
                 !weaponRequirement.Matches(pawn.equipment?.Primary)))
            {
                return false;
            }

            state.RecallRequested = false;
            state.AutomaticIdleReturnRequested = false;
            state.RecallInterruptPending = false;
            state.LastRecallInterruptAttemptTick = -1;
            state.Transition = ApparelTransition.Active;
            state.ChangingAreaReturnCell = IntVec3.Invalid;
            state.LastChangingAreaReturnAttemptTick = -1;
            state.NaturalLockerDwellUntilTick = -1;
            state.ActiveIdleTicks = 0;

            if (AomLog.DetailedEnabled)
            {
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                    $"protected {newJob.def.defName} became available during " +
                    "the automatic idle return; retained the managed outfit.");
            }
            return true;
        }

        private static bool TryBeginSequentialRuleHandoff(
            Pawn_JobTracker tracker,
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            PawnApparelState state,
            List<ApparelRule> protectedJobRules,
            List<ApparelRule> occupiedRules,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref JobTag? tag)
        {
            if (tracker == null || pawn?.Map == null || component == null ||
                state?.Transition != ApparelTransition.Active ||
                state.RecallRequested || newJob?.def == null)
            {
                return false;
            }

            List<ApparelRule> sourceRules = (occupiedRules ??
                    new List<ApparelRule>())
                .Where(rule => rule?.Enabled == true && !rule.WorkAreaPaused &&
                               rule.Area?.Map == pawn.Map)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
            var sourceIds = new HashSet<string>(sourceRules.Select(rule => rule.Id));
            List<ApparelRule> destinationRules = (protectedJobRules ??
                    new List<ApparelRule>())
                .Where(rule => rule?.Enabled == true && !rule.WorkAreaPaused &&
                               rule.Area?.Map == pawn.Map &&
                               !sourceIds.Contains(rule.Id))
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
            if (sourceRules.Count == 0 || destinationRules.Count == 0 ||
                !sourceIds.Contains(state.ActiveRuleId))
            {
                return false;
            }

            List<ApparelRule> combined = sourceRules
                .Concat(destinationRules)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
            bool sourceCompatible = RequirementsAreCompatible(sourceRules, pawn);
            bool destinationCompatible =
                RequirementsAreCompatible(destinationRules, pawn);
            bool combinedCompatible = RequirementsAreCompatible(combined, pawn);
            if (!sourceCompatible || !destinationCompatible || combinedCompatible)
                return false;

            // A static overlap elsewhere on the map does not make this handoff
            // impossible. What matters before leaving is whether the pawn's
            // current cell genuinely requires both incompatible sets. When it
            // does not, move to a neutral changing cell and re-evaluate the
            // destination from there. If the destination itself is an impossible
            // overlap, the next clean StartJob pass will reject it without ever
            // granting boundary entry.
            if (destinationRules.Any(destination =>
                    PawnInsideArea(pawn, destination.Area)))
            {
                return false;
            }

            ApparelRule activeRule = component.RuleById(state.ActiveRuleId);
            Area preferredChangingArea = activeRule?.ChangingArea?.Map == pawn.Map
                ? activeRule.ChangingArea
                : sourceRules.Select(rule => rule.ChangingArea)
                    .FirstOrDefault(area => area?.Map == pawn.Map);
            if (!TryFindSafeTransitionCell(
                    pawn, preferredChangingArea, combined, out IntVec3 safeCell))
            {
                return false;
            }

            Job interruptedJob = newJob;
            tracker.ClearQueuedJobs(false);
            ManagedWorkClaimRegistry.Release(pawn, interruptedJob);
            AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                pawn, interruptedJob);
            AutomaticOutfitManagerGameComponent.ClearPendingWork(state);

            state.CurrentRuleIds = sourceRules
                .Select(rule => rule.Id)
                .Distinct()
                .ToList();
            state.Transition = ApparelTransition.ReturningToChangingArea;
            state.ChangingAreaReturnCell = safeCell;
            state.LastChangingAreaReturnAttemptTick =
                Find.TickManager?.TicksGame ?? 0;
            state.LastApparelPreparationAttemptTick = -1;
            state.LastApparelPreparationThingId = -1;
            state.ClearWeaponPreparationRetry();
            state.ActiveIdleTicks = 0;
            state.NestedRuleBuffers?.Clear();
            state.LastNestedBufferStatus = null;

            newJob = MakeChangingAreaTravelJob(safeCell);
            newJob.expiryInterval = 2000;
            newJob.locomotionUrgency = LocomotionUrgency.Jog;
            jobGiver = null;
            tag = null;

            if (AomLog.DetailedEnabled)
            {
                string sourceNames = string.Join(", ",
                    sourceRules.Select(rule => $"'{rule.Name}'"));
                string destinationNames = string.Join(", ",
                    destinationRules.Select(rule => $"'{rule.Name}'"));
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                    $"leaving {sourceNames} through neutral changing cell " +
                    $"{safeCell} before preparing incompatible sequential " +
                    $"destination {destinationNames}; reconsidering " +
                    $"{interruptedJob.def.defName} afterward.");
            }
            return true;
        }

        private static bool RequirementsAreCompatible(
            List<ApparelRule> rules, Pawn pawn)
        {
            return ApparelCompatibility.FindConflict(
                       rules, pawn?.RaceProps?.body) == null &&
                   RuleEvaluator.TryCombinedWeaponRequirement(
                       rules, out CombinedWeaponRequirement _);
        }

        private static bool TryAllowIncompatibleIngestWithCurrentOutfit(
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            PawnApparelState state,
            Job job,
            IEnumerable<ApparelRule> protectedJobRules,
            IEnumerable<ApparelRule> occupiedRules)
        {
            // This helper sits on the shared StartJob boundary. Nearly every
            // native job reaches it, while the exception is intentionally
            // limited to eating. Avoid the Concat/GroupBy allocations for all
            // unrelated work, hauling, recreation, wandering, and sleep jobs.
            if (!IsIngestJob(job))
                return false;

            List<ApparelRule> applicableRules =
                (protectedJobRules ?? Enumerable.Empty<ApparelRule>())
                .Concat(occupiedRules ?? Enumerable.Empty<ApparelRule>())
                .Where(rule => rule?.Enabled == true && !rule.WorkAreaPaused &&
                               rule.Area?.Map == pawn?.Map)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
            if (!TryGetManagedIncompatibleIngestFallbackRules(
                    pawn, component, state, job, applicableRules,
                    out ApparelRule retainedRule,
                    out List<ApparelRule> bypassedRules))
            {
                return false;
            }

            foreach (ApparelRule bypassedRule in bypassedRules)
            {
                UnavailableWorkRegistry.Block(
                    pawn, bypassedRule,
                    EssentialPersonalFallbackRetryInterval);
            }

            state.ActiveRuleId = retainedRule.Id;
            state.CurrentRuleIds = new List<string> { retainedRule.Id };
            state.LastManagedWorkJobDefName = job.def.defName;
            state.ActiveIdleTicks = 0;
            ManagedWorkClaimRegistry.Release(pawn, job);
            if (SameJob(job, state.PendingWorkJob))
                AutomaticOutfitManagerGameComponent.ClearPendingWork(state);
            else
                ProtectedBoundaryRetryRegistry.Clear(pawn, job);

            if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                    pawn,
                    $"incompatible-ingest-fallback:{retainedRule.Id}:" +
                    string.Join(",", bypassedRules.Select(rule => rule.Id))))
            {
                string bypassedNames = string.Join(", ",
                    bypassedRules.Select(rule => $"'{rule.Name}'"));
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: allowing " +
                    $"essential {job.def.defName} under the currently equipped " +
                    $"'{retainedRule.Name}' outfit; overlapping {bypassedNames} " +
                    "cannot be equipped simultaneously.");
            }
            return true;
        }

        private static bool TryGetManagedIncompatibleIngestFallbackRules(
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            PawnApparelState state,
            Job job,
            IEnumerable<ApparelRule> rules,
            out ApparelRule retainedRule,
            out List<ApparelRule> bypassedRules)
        {
            retainedRule = null;
            bypassedRules = new List<ApparelRule>();
            if (!IsIngestJob(job) || pawn?.Map == null || component == null ||
                state == null || state.RecallRequested ||
                (state.Transition != ApparelTransition.Active &&
                 state.Transition != ApparelTransition.Preparing) ||
                (!state.ApparelInterventionActive &&
                 !state.WeaponInterventionActive))
            {
                return false;
            }

            List<ApparelRule> candidates = rules?
                .Where(rule => rule?.Enabled == true && !rule.WorkAreaPaused &&
                               rule.Area?.Map == pawn.Map)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList() ?? new List<ApparelRule>();
            ApparelRule activeRule = component.RuleById(state.ActiveRuleId);
            retainedRule = candidates.FirstOrDefault(rule =>
                    rule.Id == activeRule?.Id &&
                    PawnInsideArea(pawn, rule.Area) &&
                    !RuleEvaluator.HasMissingRequiredGear(pawn, rule)) ??
                candidates.FirstOrDefault(rule =>
                    PawnInsideArea(pawn, rule.Area) &&
                    !RuleEvaluator.HasMissingRequiredGear(pawn, rule));
            if (retainedRule == null)
                return false;

            ApparelRule retained = retainedRule;
            bypassedRules = candidates.Where(rule =>
                    rule.Id != retained.Id &&
                    RuleEvaluator.HasMissingRequiredGear(pawn, rule) &&
                    RuleAreasOverlap(retained, rule) &&
                    !RequirementsAreCompatible(
                        new List<ApparelRule> { retained, rule }, pawn) &&
                    ((PawnInsideArea(pawn, retained.Area) &&
                      PawnInsideArea(pawn, rule.Area)) ||
                     (RuleEvaluator.JobTargetsArea(job, retained.Area) &&
                      RuleEvaluator.JobTargetsArea(job, rule.Area))))
                .ToList();
            return bypassedRules.Count > 0;
        }

        internal static bool IsManagedIncompatibleIngestFallback(
            Pawn pawn,
            PawnApparelState state,
            Job job,
            ApparelRule missingRule,
            IntVec3 cell)
        {
            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            ApparelRule retainedRule = component?.RuleById(state?.ActiveRuleId);
            if (!IsIngestJob(job) || pawn?.Map == null ||
                state?.Transition != ApparelTransition.Active ||
                state.RecallRequested ||
                (!state.ApparelInterventionActive &&
                 !state.WeaponInterventionActive) ||
                retainedRule?.Enabled != true || retainedRule.WorkAreaPaused ||
                retainedRule.Area?.Map != pawn.Map || missingRule?.Enabled != true ||
                missingRule.WorkAreaPaused || missingRule.Area?.Map != pawn.Map ||
                retainedRule.Id == missingRule.Id || !cell.IsValid ||
                !cell.InBounds(pawn.Map) || !retainedRule.Area[cell] ||
                !missingRule.Area[cell] ||
                RuleEvaluator.HasMissingRequiredGear(pawn, retainedRule) ||
                !RuleEvaluator.HasMissingRequiredGear(pawn, missingRule) ||
                !UnavailableWorkRegistry.HasActiveRuleBlock(pawn, missingRule))
            {
                return false;
            }

            return RuleAreasOverlap(retainedRule, missingRule) &&
                   !RequirementsAreCompatible(
                       new List<ApparelRule> { retainedRule, missingRule }, pawn);
        }

        private static bool IsIngestJob(Job job)
        {
            string defName = job?.def?.defName ?? string.Empty;
            return job?.def == JobDefOf.Ingest ||
                   defName.IndexOf(
                       "Ingest", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool RuleAreasOverlap(
            ApparelRule left, ApparelRule right)
        {
            if (left?.Area?.Map == null || right?.Area?.Map != left.Area.Map)
                return false;

            Area smaller = left.Area.ActiveCells.Count() <=
                           right.Area.ActiveCells.Count()
                ? left.Area
                : right.Area;
            Area larger = smaller == left.Area ? right.Area : left.Area;
            return smaller.ActiveCells.Any(cell => larger[cell]);
        }

        internal static bool TryPrepareForOccupiedRules(
            Pawn pawn, IEnumerable<ApparelRule> rules)
        {
            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            List<ApparelRule> occupiedRules = rules?
                .Where(rule => rule?.Enabled == true &&
                               rule.Area?.Map == pawn?.Map)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList() ?? new List<ApparelRule>();
            if (pawn?.jobs == null || component == null ||
                occupiedRules.Count == 0)
            {
                return false;
            }

            // Occupancy recovery has no native work continuation to preserve:
            // its only purpose is to restore the complete requirement before
            // RimWorld chooses another activity. Reuse the ordinary combined
            // requirement planner, but let the bounded self-wait be the trigger
            // rather than making the rejected concrete job own a second retry.
            Job trigger = MakeSafeWaitJob(pawn, 30);
            ThinkNode jobGiver = null;
            JobTag? tag = null;
            if (!TryPrepareForMatchingRules(
                    pawn.jobs, pawn, component, occupiedRules,
                    ref trigger, ref jobGiver, ref tag, false))
            {
                return false;
            }

            pawn.jobs.StartJob(
                trigger, JobCondition.InterruptForced,
                null, false, true);
            return true;
        }

        internal static BoundaryResumeResult TryResumeBoundaryInterruptedJob(
            Pawn pawn, Job interruptedJob, IEnumerable<ApparelRule> rules)
        {
            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            List<ApparelRule> boundaryRules = rules?
                .Where(rule => rule?.Enabled == true &&
                               !rule.WorkAreaPaused &&
                               rule.Area?.Map == pawn?.Map)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList() ?? new List<ApparelRule>();
            if (pawn?.jobs == null || component == null ||
                interruptedJob?.def == null || boundaryRules.Count == 0)
            {
                return BoundaryResumeResult.Invalid;
            }

            if (!PendingWorkJobIsViable(
                    pawn, interruptedJob, out string viabilityReason,
                    out bool retryableFailure))
            {
                if (retryableFailure)
                {
                    if (AomLog.ShouldLogDetailed(
                            pawn, "boundary-retry-temporarily-blocked", 600))
                    {
                        AomLog.Detailed(
                            $"{pawn.LabelShortCap}: retained boundary-interrupted " +
                            $"{interruptedJob.def.defName}; " +
                            $"{viabilityReason ?? "its target is temporarily unavailable"}.");
                    }
                    return BoundaryResumeResult.RetryLater;
                }

                return BoundaryResumeResult.Invalid;
            }

            // The path-cell guard has already ended the native job. Promote its
            // detached, driver-free snapshot through the ordinary preparation
            // planner before another thinker job can erase the late-bound
            // destination that exposed the protected boundary.
            Job currentJob = pawn.jobs.curJob;
            Job resumedJob = interruptedJob;
            ThinkNode originalJobGiver = interruptedJob.jobGiver;
            ThinkTreeDef originalThinkTree = interruptedJob.jobGiverThinkTree;
            ThinkNode resumedJobGiver = originalJobGiver;
            JobTag? tag = null;
            bool plannedTransition = TryPrepareForMatchingRules(
                pawn.jobs, pawn, component, boundaryRules,
                ref resumedJob, ref resumedJobGiver, ref tag, true);

            AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                pawn, currentJob);
            if (!plannedTransition)
                pawn.jobs.ClearQueuedJobs(false);

            if (!BoundaryResumeAdmissions.Add(pawn))
                return BoundaryResumeResult.RetryLater;

            try
            {
                pawn.jobs.StartJob(
                    resumedJob, JobCondition.InterruptForced,
                    plannedTransition ? resumedJobGiver : originalJobGiver,
                    false, true,
                    plannedTransition ? null : originalThinkTree,
                    tag);
            }
            finally
            {
                BoundaryResumeAdmissions.Remove(pawn);
            }
            return BoundaryResumeResult.Resumed;
        }

        private static bool PreferBoundaryInterruptedJob(
            Pawn pawn,
            PawnApparelState state,
            ref Job proposedJob,
            ref ThinkNode jobGiver,
            ref ThinkTreeDef thinkTree,
            ref JobTag? tag)
        {
            if (pawn?.jobs == null || proposedJob?.def == null ||
                BoundaryResumeAdmissions.Contains(pawn) ||
                (state != null && state.Transition != ApparelTransition.Active) ||
                !ProtectedBoundaryRetryRegistry.TryGetPendingInterruption(
                    pawn, out Job interruptedJob,
                    out List<ApparelRule> boundaryRules))
            {
                return false;
            }

            if (SameJob(proposedJob, interruptedJob))
                return false;

            if (proposedJob.playerForced)
            {
                ProtectedBoundaryRetryRegistry.Clear(pawn, interruptedJob);
                return false;
            }

            string invalidReason = null;
            bool retryableFailure = false;
            if (boundaryRules.Count == 0 ||
                !PendingWorkJobIsViable(
                    pawn, interruptedJob, out invalidReason,
                    out retryableFailure))
            {
                if (retryableFailure)
                {
                    Job deferredReplacementJob = proposedJob;
                    ReplaceWithWait(
                        pawn, 60, ref proposedJob, ref jobGiver, ref tag);
                    thinkTree = null;
                    if (AomLog.ShouldLogDetailed(
                            pawn, "boundary-retry-temporarily-blocked", 600))
                    {
                        AomLog.Detailed(
                            $"{pawn.LabelShortCap}: retained boundary-interrupted " +
                            $"{interruptedJob.def.defName} while " +
                            $"{invalidReason ?? "its target is temporarily unavailable"}; " +
                            $"holding outside before replacement " +
                            $"{deferredReplacementJob.def.defName} can take control.");
                    }
                    return true;
                }

                ProtectedBoundaryRetryRegistry.Clear(pawn, interruptedJob);
                string releasedJobName =
                    interruptedJob?.def?.defName ?? "job";
                if (AomLog.ShouldLogDetailed(
                        pawn, $"boundary-retry-released:{releasedJobName}"))
                {
                    AomLog.Detailed(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                        $"released boundary-interrupted " +
                        $"{releasedJobName}; " +
                        $"{invalidReason ?? "its protected rule is no longer active"}.");
                }
                return false;
            }

            Job displacedJob = proposedJob;
            ManagedWorkClaimRegistry.Release(pawn, displacedJob);
            AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                pawn, displacedJob);

            proposedJob = interruptedJob;
            jobGiver = interruptedJob.jobGiver;
            thinkTree = interruptedJob.jobGiverThinkTree;
            tag = null;

            if (AomLog.DetailedEnabled)
            {
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: resuming " +
                    $"exact boundary-interrupted {interruptedJob.def.defName} " +
                    $"before replacement {displacedJob.def.defName} can take control.");
            }
            return true;
        }

        private static bool PreferPendingBoundaryRepairDuringPreparation(
            Pawn_JobTracker tracker,
            Pawn pawn,
            PawnApparelState state,
            ref Job proposedJob,
            ref ThinkNode jobGiver,
            ref ThinkTreeDef thinkTree,
            ref JobTag? tag)
        {
            Job pendingRepair = state?.PendingWorkJob;
            if (tracker == null || pawn == null || proposedJob?.def == null ||
                state?.Transition != ApparelTransition.Preparing ||
                pendingRepair?.def == null ||
                state.PendingBoundaryRuleIds?.Count <= 0 ||
                SameJob(proposedJob, pendingRepair) || proposedJob.playerForced ||
                !string.Equals(pendingRepair.def.defName,
                    "FixBrokenDownBuilding", StringComparison.OrdinalIgnoreCase) ||
                !PendingWorkJobIsViable(pawn, pendingRepair, out _))
            {
                return false;
            }

            Job displacedJob = proposedJob;
            tracker.ClearQueuedJobs(false);
            ManagedWorkClaimRegistry.Release(pawn, displacedJob);
            AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                pawn, displacedJob);
            proposedJob = pendingRepair;
            jobGiver = pendingRepair.jobGiver;
            thinkTree = pendingRepair.jobGiverThinkTree;
            tag = null;
            state.PendingBoundaryWorkJobLoadId = pendingRepair.loadID;

            if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                    pawn, $"pending-boundary-repair:{pendingRepair.loadID}", 600))
            {
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: retained " +
                    $"the staged {pendingRepair.def.defName} while source-area " +
                    $"preparation continues; deferred autonomous " +
                    $"{displacedJob.def.defName}.");
            }
            return true;
        }

        internal static bool TryRecoverIdlePreparation(
            Pawn pawn, PawnApparelState state, out string description)
        {
            description = null;
            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            if (pawn?.Spawned != true || pawn.Drafted || pawn.jobs == null ||
                component == null ||
                state?.Transition != ApparelTransition.Preparing)
            {
                return false;
            }

            Job pendingWork = state.PendingWorkJob;
            if (!PendingWorkJobIsViable(pawn, pendingWork, out string invalidReason))
            {
                ManagedWorkClaimRegistry.ReleaseAll(pawn);
                AutomaticOutfitManagerGameComponent.ClearPendingWork(state);
                component.RequestRecall(state);
                description =
                    $"cancelled the invalid saved continuation ({invalidReason}) " +
                    "and requested saved-outfit restoration";
                return true;
            }

            List<ApparelRule> recoveryRules = ProtectedRulesForJob(pawn, pendingWork)
                .Where(rule => rule?.Enabled == true && !rule.WorkAreaPaused &&
                               rule.Area?.Map == pawn.Map)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
            List<ApparelRule> recoveryDestinationRules =
                RuleEvaluator.MatchingRules(pawn, pendingWork);
            List<ApparelRule> recoveryOccupiedRules =
                RuleEvaluator.MatchingLocationRules(pawn);
            if (TrySelectBoundaryTransitStage(
                    pawn, pendingWork, recoveryDestinationRules,
                    recoveryOccupiedRules,
                    out List<ApparelRule> recoveryBoundaryRules))
            {
                // Idle recovery must preserve the same immediate-stage choice
                // made at StartJob. Recombining the eventual worksite here would
                // recreate the incompatible transit/destination loop whenever a
                // queued Wear step briefly yielded to the thinker.
                recoveryRules = recoveryBoundaryRules
                    .Concat(recoveryOccupiedRules)
                    .Where(rule => rule != null)
                    .GroupBy(rule => rule.Id)
                    .Select(group => group.First())
                    .ToList();
            }
            if (recoveryRules.Count == 0)
            {
                ApparelRule activeRule = component.RuleById(state.ActiveRuleId);
                if (activeRule?.Enabled == true && !activeRule.WorkAreaPaused &&
                    activeRule.Area?.Map == pawn.Map)
                {
                    recoveryRules.Add(activeRule);
                }
            }

            if (recoveryRules.Count == 0)
            {
                ManagedWorkClaimRegistry.ReleaseAll(pawn);
                AutomaticOutfitManagerGameComponent.ClearPendingWork(state);
                component.RequestRecall(state);
                description =
                    "released a continuation whose protected rule is no longer " +
                    "active and requested saved-outfit restoration";
                return true;
            }

            ApparelRule unwearableRecoveryRule = recoveryRules.FirstOrDefault(rule =>
                !RuleEvaluator.RuleCanApplyToPawn(pawn, rule));
            ApparelConflict recoveryConflict = ApparelCompatibility.FindConflict(
                recoveryRules, pawn.RaceProps?.body);
            bool compatibleRecoveryWeapons =
                RuleEvaluator.TryCombinedWeaponRequirement(
                    recoveryRules, out CombinedWeaponRequirement _);
            bool managedIngestFallback =
                TryGetManagedIncompatibleIngestFallbackRules(
                    pawn, component, state, pendingWork, recoveryRules,
                    out ApparelRule retainedIngestRule,
                    out List<ApparelRule> bypassedIngestRules);
            if ((PausedAreaWorkFilter.IsEssentialPersonalJob(pendingWork) ||
                 managedIngestFallback) &&
                (unwearableRecoveryRule != null || recoveryConflict != null ||
                 !compatibleRecoveryWeapons))
            {
                // The top-level StartJob path deliberately allows essential
                // personal jobs under the currently safe outfit when overlapping
                // rules cannot be satisfied together. Idle recovery must make
                // the same decision; rebuilding the impossible outfit here made
                // Ingest/LayDown alternate between Wait and preparation forever.
                IEnumerable<ApparelRule> rulesToBlock = managedIngestFallback
                    ? bypassedIngestRules
                    : recoveryRules;
                foreach (ApparelRule rule in rulesToBlock)
                {
                    UnavailableWorkRegistry.Block(
                        pawn, rule, EssentialPersonalFallbackRetryInterval);
                }

                Job essentialJob = pendingWork;
                ThinkNode essentialGiver = pendingWork.jobGiver;
                ThinkTreeDef essentialThinkTree = pendingWork.jobGiverThinkTree;
                string incompatibility = unwearableRecoveryRule != null
                    ? $"required apparel for '{unwearableRecoveryRule.Name}' cannot be worn"
                    : recoveryConflict != null
                        ? $"required apparel is incompatible: {recoveryConflict.Label}"
                        : "overlapping rules require different primary weapons";

                if (managedIngestFallback)
                {
                    state.ActiveRuleId = retainedIngestRule.Id;
                    state.CurrentRuleIds = new List<string>
                    {
                        retainedIngestRule.Id
                    };
                }
                state.Transition = ApparelTransition.Active;
                state.LastManagedWorkJobDefName = essentialJob.def.defName;
                ManagedWorkClaimRegistry.ReleaseAll(pawn);
                AutomaticOutfitManagerGameComponent.ClearPendingWork(state);
                pawn.jobs.StartJob(
                    essentialJob, JobCondition.InterruptForced,
                    essentialGiver, false, true,
                    essentialThinkTree, null);
                description =
                    $"resumed essential {essentialJob.def.defName} under the " +
                    $"currently safe outfit because {incompatibility}";
                return true;
            }

            Job recoveryJob = pendingWork;
            ThinkNode recoveryGiver = pendingWork.jobGiver;
            JobTag? recoveryTag = null;
            bool plannedTransition = TryPrepareForMatchingRules(
                pawn.jobs, pawn, component, recoveryRules,
                ref recoveryJob, ref recoveryGiver, ref recoveryTag,
                true, true, out bool essentialGearUnavailable);
            if (essentialGearUnavailable)
            {
                // The exact sleep/rest continuation already performed a real
                // preparation attempt and the retry planner has now confirmed
                // that the complete set is still unavailable. Keep the one
                // personal-outfit snapshot open and allow this exact essential
                // job during the bounded shortage window. Recalling here clears
                // PendingWorkJob, restores the personal outfit, and lets the
                // thinker select the same protected bed again forever.
                foreach (ApparelRule rule in recoveryRules)
                {
                    UnavailableWorkRegistry.Block(
                        pawn, rule, pendingWork,
                        EssentialPersonalFallbackRetryInterval);
                }

                state.CurrentRuleIds = recoveryRules
                    .Select(rule => rule.Id)
                    .Distinct()
                    .ToList();
                state.Transition = ApparelTransition.Active;
                state.RecallRequested = false;
                state.AutomaticIdleReturnRequested = false;
                state.RecallInterruptPending = false;
                state.LastManagedWorkJobDefName = pendingWork.def.defName;
                state.LastApparelPreparationAttemptTick = -1;
                state.LastApparelPreparationThingId = -1;
                state.ClearWeaponPreparationRetry();
                state.ActiveIdleTicks = 0;
                pawn.jobs.ClearQueuedJobs(false);
                ManagedWorkClaimRegistry.ReleaseAll(pawn);
                AutomaticOutfitManagerGameComponent.ClearPendingWork(state);
                pawn.jobs.StartJob(
                    pendingWork, JobCondition.InterruptForced,
                    pendingWork.jobGiver, false, true,
                    pendingWork.jobGiverThinkTree, null);
                description =
                    $"resumed essential {pendingWork.def.defName} under the " +
                    "bounded unavailable-gear fallback without restoring and " +
                    "recapturing the personal outfit";
                return true;
            }
            if (!plannedTransition &&
                !HasCompletedPreparation(pawn, component, state))
            {
                return false;
            }

            pawn.jobs.StartJob(
                recoveryJob, JobCondition.InterruptForced,
                recoveryGiver, false, true,
                null, recoveryTag);
            description = plannedTransition
                ? $"rebuilt required gear for the saved {pendingWork.def.defName} job"
                : $"resumed the fully prepared saved {pendingWork.def.defName} job";
            return true;
        }

        private static bool TryPrepareForMatchingRules(
            Pawn_JobTracker tracker,
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            List<ApparelRule> rules,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref JobTag? tag,
            bool preservePendingWork = true)
        {
            return TryPrepareForMatchingRules(
                tracker, pawn, component, rules,
                ref newJob, ref jobGiver, ref tag,
                preservePendingWork, false, out _);
        }

        private static bool TryPrepareForMatchingRules(
            Pawn_JobTracker tracker,
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            List<ApparelRule> rules,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref JobTag? tag,
            bool preservePendingWork,
            bool allowEssentialUnavailableFallback,
            out bool essentialGearUnavailable)
        {
            essentialGearUnavailable = false;
            PawnApparelState pendingState = component?.StateFor(pawn);
            bool mayUseEssentialUnavailableFallback =
                allowEssentialUnavailableFallback && preservePendingWork &&
                pendingState?.Transition == ApparelTransition.Preparing &&
                SameJob(newJob, pendingState.PendingWorkJob) &&
                PausedAreaWorkFilter.IsEssentialPersonalJob(newJob);

            ApparelRule unwearableRule = rules.FirstOrDefault(rule =>
                !RuleEvaluator.RuleCanApplyToPawn(pawn, rule));
            if (unwearableRule != null)
            {
                UnavailableWorkRegistry.Block(pawn, unwearableRule);
                if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                        pawn, $"nested-unwearable:{unwearableRule.Id}"))
                {
                    AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: blocked from '{unwearableRule.Name}'; its required apparel cannot be worn by this pawn.");
                }
                if (TryReplaceUnavailableGearWaitWithEgress(
                        tracker, pawn, ref newJob, ref jobGiver, ref tag))
                {
                    return true;
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
                if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                        pawn, $"nested-conflict:{string.Join(",", rules.Select(rule => rule.Id))}"))
                {
                    string conflictLabel = conflict != null
                        ? conflict.Label
                        : "different primary weapons";
                    AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: delaying {newJob.def.defName}; incompatible required apparel: {conflictLabel}.");
                }

                if (TryReplaceUnavailableGearWaitWithEgress(
                        tracker, pawn, ref newJob, ref jobGiver, ref tag))
                {
                    return true;
                }
                tracker.ClearQueuedJobs(false);
                ReplaceWithWait(pawn, 300, ref newJob, ref jobGiver, ref tag);
                return true;
            }

            var requiredByDef = new Dictionary<ThingDef, ApparelRule>();
            var standardsByDef = new Dictionary<ThingDef, List<ApparelRule>>();
            foreach (ApparelRule rule in rules)
            {
                foreach (ThingDef def in rule.RequiredApparel ?? Enumerable.Empty<ThingDef>())
                {
                    if (def == null)
                        continue;
                    if (!requiredByDef.ContainsKey(def))
                        requiredByDef.Add(def, rule);
                    if (!standardsByDef.TryGetValue(
                            def, out List<ApparelRule> standards))
                    {
                        standards = new List<ApparelRule>();
                        standardsByDef.Add(def, standards);
                    }
                    standards.Add(rule);
                }
            }

            var missing = requiredByDef.Keys
                .Where(def => !pawn.apparel.WornApparel.Any(item =>
                    item?.def == def &&
                    standardsByDef[def].All(rule => rule.Allows(item))))
                .ToList();
            bool missingWeapon = !combinedWeaponRequirement.Matches(
                pawn.equipment?.Primary);
            PawnApparelState weaponState = component?.StateFor(pawn);
            bool weaponChoiceProtected = missingWeapon &&
                weaponState?.WeaponRuleOverrideExplicit == true;
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
                if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                        pawn, $"nested-weapon-player-control:{string.Join(",", rules.Select(rule => rule.Id))}"))
                {
                    AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: continuing {newJob.def.defName} with the player's current primary weapon; the weapon requirement is skipped while that choice is protected.");
                }
            }
            if (missingWeapon && weaponState?.Transition ==
                    ApparelTransition.Preparing &&
                weaponState.WeaponInterventionActive &&
                weaponState.LastWeaponPreparationAttemptTick >= 0 &&
                !PreparationWeaponRequirementMatches(
                    pawn, component, weaponState))
            {
                int preparationTick = Find.TickManager?.TicksGame ?? 0;
                int elapsed = preparationTick -
                    weaponState.LastWeaponPreparationAttemptTick;
                if (weaponState.WeaponPreparationBudgetExceeded(
                        preparationTick, WeaponPreparationAttemptLimit,
                        WeaponPreparationTimeLimit))
                {
                    weaponState.RejectLastWeaponPreparationAttempt();
                    if (TryAbortExhaustedWeaponPreparation(
                            tracker, pawn, component, weaponState, rules,
                            ref newJob, ref jobGiver, ref tag))
                    {
                        return true;
                    }
                }

                int retryInterval =
                    weaponState.WeaponPreparationRetriesThisTransition == 0
                        ? WeaponPreparationRetryInterval
                        : SubsequentWeaponPreparationSettleInterval;
                if (elapsed < retryInterval)
                {
                    // Occupancy recovery calls this planner directly instead of
                    // re-entering the top-level StartJob prefix. Preserve the
                    // outstanding attempt's settlement window here as well, or
                    // every component pulse selects the same failed floor weapon
                    // and resets its timer before it can ever be rejected.
                    tracker.ClearQueuedJobs(false);
                    int remaining = Math.Max(
                        30, retryInterval - elapsed);
                    ReplaceWithWait(
                        pawn, remaining, ref newJob, ref jobGiver, ref tag);
                    return true;
                }

                if (TryRetryWeaponPreparationCandidate(
                        tracker, pawn, component, weaponState,
                        ref newJob, ref jobGiver, ref tag))
                {
                    return true;
                }

                weaponState.RejectLastWeaponPreparationAttempt();
                if (TryAbortExhaustedWeaponPreparation(
                        tracker, pawn, component, weaponState, rules,
                        ref newJob, ref jobGiver, ref tag))
                {
                    return true;
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
            HashSet<Thing> pendingJobTargets = JobThingTargets(newJob);
            foreach (ThingDef def in missing)
            {
                ApparelRule sourceRule = requiredByDef[def];
                Apparel apparel = ApparelFinder.FindBest(
                    pawn, def, sourceRule.ChangingArea, pendingJobTargets,
                    standardsByDef[def]);
                if (apparel == null)
                {
                    if (mayUseEssentialUnavailableFallback)
                    {
                        essentialGearUnavailable = true;
                        return false;
                    }

                    UnavailableWorkRegistry.Block(pawn, sourceRule);
                    if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                            pawn, $"nested-gear-unavailable:{sourceRule.Id}:{def.defName}"))
                    {
                        AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: delaying {newJob.def.defName}; no reachable {def.LabelCap} is available for '{sourceRule.Name}'.");
                    }

                    // Discard the stale work candidate and give the normal think
                    // tree time to select other work. It will reconsider after
                    // gear is produced, hauled, or becomes unreserved.
                    if (TryReplaceUnavailableGearWaitWithEgress(
                            tracker, pawn, ref newJob, ref jobGiver, ref tag))
                    {
                        return true;
                    }
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
                    pawn, combinedWeaponRequirement, weaponRule.ChangingArea,
                    pendingJobTargets);
                if (managedWeapon == null)
                {
                    if (mayUseEssentialUnavailableFallback)
                    {
                        essentialGearUnavailable = true;
                        return false;
                    }

                    UnavailableWorkRegistry.Block(pawn, weaponRule);
                    if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                            pawn, $"nested-weapon-unavailable:{weaponRule.Id}:{weaponRule.WeaponSummary}"))
                    {
                        AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: delaying {newJob.def.defName}; no reachable {weaponRule.WeaponSummary.ToLowerInvariant()} is available for '{weaponRule.Name}'.");
                    }
                    if (TryReplaceUnavailableGearWaitWithEgress(
                            tracker, pawn, ref newJob, ref jobGiver, ref tag))
                    {
                        return true;
                    }
                    tracker.ClearQueuedJobs(false);
                    ReplaceWithWait(pawn, 300, ref newJob, ref jobGiver, ref tag);
                    return true;
                }

                Job equipJob = JobMaker.MakeJob(JobDefOf.Equip, managedWeapon);
                equipJob.playerForced = false;
                transitionJobs.Add(equipJob);
            }

            if (preservePendingWork &&
                !ManagedWorkClaimRegistry.TryClaim(pawn, newJob))
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
                if (managedWeapon != null &&
                    transitionJobs[0].def == JobDefOf.Equip)
                {
                    interventionState.RecordWeaponPreparationAttempt(managedWeapon);
                }
                if (preservePendingWork)
                {
                    AutomaticOutfitManagerGameComponent.CapturePendingWork(
                        interventionState, newJob, true);
                    interventionState.LastManagedWorkJobDefName =
                        newJob.def.defName;
                }
                else
                {
                    AutomaticOutfitManagerGameComponent.ClearPendingWork(
                        interventionState);
                }
                interventionState.CurrentRuleIds = rules
                    .Where(rule => rule != null)
                    .Select(rule => rule.Id)
                    .Distinct()
                    .ToList();
            }

            if (AomLog.DetailedEnabled)
            {
                string ruleNames = string.Join(", ", rules.Select(rule => $"'{rule.Name}'"));
                string weaponAssignment = managedWeapon == null
                    ? "no weapon"
                    : $"weapon {managedWeapon.LabelCap} [{managedWeapon.def.defName}]";
                string preparationContext = preservePendingWork
                    ? $"intercepted {newJob.def.defName}"
                    : "recovering complete occupied-area protection";
                AomLog.Detailed($"[AutomaticOutfitManager] {pawn.LabelShortCap}: {preparationContext}; preparing {managedApparel.Count} apparel item(s) and {weaponAssignment} for overlapping rules {ruleNames}.");
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

        private static HashSet<Thing> JobThingTargets(Job job)
        {
            var targets = new HashSet<Thing>();
            if (job == null)
                return targets;

            AddThingTarget(targets, job.targetA);
            AddThingTarget(targets, job.targetB);
            AddThingTarget(targets, job.targetC);
            if (job.targetQueueA != null)
            {
                foreach (LocalTargetInfo target in job.targetQueueA)
                    AddThingTarget(targets, target);
            }
            if (job.targetQueueB != null)
            {
                foreach (LocalTargetInfo target in job.targetQueueB)
                    AddThingTarget(targets, target);
            }
            return targets;
        }

        private static void AddThingTarget(
            HashSet<Thing> targets, LocalTargetInfo target)
        {
            if (target.HasThing && target.Thing != null)
                targets.Add(target.Thing);
        }

        internal static bool StructurallyEquivalentWorkJob(Job candidate, Job pending)
        {
            if (candidate?.def == null || pending?.def == null ||
                candidate.def != pending.def)
                return false;

            return SameTarget(candidate.targetA, pending.targetA) &&
                   SameTarget(candidate.targetB, pending.targetB) &&
                   SameTarget(candidate.targetC, pending.targetC) &&
                   SameTargetQueue(candidate.targetQueueA, pending.targetQueueA) &&
                   SameTargetQueue(candidate.targetQueueB, pending.targetQueueB);
        }

        private static bool SameTarget(LocalTargetInfo left, LocalTargetInfo right)
        {
            if (left.IsValid != right.IsValid)
                return false;
            if (!left.IsValid)
                return true;
            if (left.HasThing || right.HasThing)
            {
                return left.HasThing && right.HasThing &&
                       ReferenceEquals(left.Thing, right.Thing);
            }
            return left.Cell == right.Cell;
        }

        private static bool SameTargetQueue(
            List<LocalTargetInfo> left, List<LocalTargetInfo> right)
        {
            int leftCount = left?.Count ?? 0;
            int rightCount = right?.Count ?? 0;
            if (leftCount != rightCount)
                return false;

            for (int index = 0; index < leftCount; index++)
            {
                if (!SameTarget(left[index], right[index]))
                    return false;
            }
            return true;
        }

        internal static bool IsDesignationSensitiveWork(Job job)
        {
            string defName = job?.def?.defName;
            return string.Equals(defName, "Deconstruct", StringComparison.Ordinal) ||
                   string.Equals(defName, "Uninstall", StringComparison.Ordinal) ||
                   string.Equals(defName, "RemoveFloor", StringComparison.Ordinal);
        }

        private static bool TryRefreshDesignationSensitiveWork(
            Pawn pawn, Job pendingWork, out Job refreshedWork, out string reason,
            out bool targetRejected)
        {
            refreshedWork = null;
            reason = null;
            targetRejected = false;
            if (pawn?.Map == null || pendingWork?.def == null)
            {
                reason = "the pawn, map, or pending job is no longer valid";
                return false;
            }

            WorkGiver_Scanner scanner = pendingWork.workGiverDef?.Worker as
                WorkGiver_Scanner;
            if (scanner == null)
            {
                reason = "the original native work giver is unavailable";
                return false;
            }

            bool forced = pendingWork.playerForced;
            try
            {
                if (pendingWork.targetA.HasThing)
                {
                    Thing target = pendingWork.targetA.Thing;
                    if (target == null || target.Destroyed || target.MapHeld != pawn.Map)
                    {
                        reason = "the designated target was destroyed or left the map";
                        return false;
                    }
                    if (!scanner.HasJobOnThing(pawn, target, forced))
                    {
                        targetRejected = true;
                        reason = "the native work giver no longer accepts the target";
                        return false;
                    }
                    refreshedWork = scanner.JobOnThing(pawn, target, forced);
                }
                else if (pendingWork.targetA.Cell.IsValid &&
                         pendingWork.targetA.Cell.InBounds(pawn.Map))
                {
                    IntVec3 target = pendingWork.targetA.Cell;
                    if (!scanner.HasJobOnCell(pawn, target, forced))
                    {
                        targetRejected = true;
                        reason = "the native work giver no longer accepts the target cell";
                        return false;
                    }
                    refreshedWork = scanner.JobOnCell(pawn, target, forced);
                }
                else
                {
                    reason = "the pending job has no refreshable primary target";
                    return false;
                }
            }
            catch (Exception exception)
            {
                reason = $"the native work giver threw {exception.GetType().Name}";
                return false;
            }

            if (refreshedWork?.def == null)
            {
                targetRejected = true;
                reason = "the native work giver returned no job";
                refreshedWork = null;
                return false;
            }

            refreshedWork.workGiverDef = pendingWork.workGiverDef;
            refreshedWork.jobGiver = pendingWork.jobGiver;
            refreshedWork.jobGiverThinkTree = pendingWork.jobGiverThinkTree;
            refreshedWork.playerForced = pendingWork.playerForced;
            refreshedWork.ignoreForbidden = pendingWork.ignoreForbidden;
            refreshedWork.ignoreDesignations = pendingWork.ignoreDesignations;
            return true;
        }

        private static void BlockPendingWorkRetry(Pawn pawn, Job pendingWork)
        {
            foreach (ApparelRule rule in ProtectedRulesForJob(pawn, pendingWork))
                UnavailableWorkRegistry.Block(pawn, rule, pendingWork, 300);
        }

        private static bool TryAbortExhaustedWeaponPreparation(
            Pawn_JobTracker tracker,
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            PawnApparelState state,
            IEnumerable<ApparelRule> knownRules,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref JobTag? tag)
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (tracker == null || pawn == null || component == null ||
                state?.Transition != ApparelTransition.Preparing ||
                !state.WeaponPreparationBudgetExceeded(
                    currentTick, WeaponPreparationAttemptLimit,
                    WeaponPreparationTimeLimit))
            {
                return false;
            }

            Job pendingWork = state.PendingWorkJob;
            var blockedRules = (knownRules ?? Enumerable.Empty<ApparelRule>())
                .Concat(ProtectedRulesForJob(pawn, pendingWork))
                .Concat((state.CurrentRuleIds ?? new List<string>())
                    .Select(component.RuleById))
                .Where(rule => rule?.Enabled == true &&
                               rule.Area?.Map == pawn.Map)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
            foreach (ApparelRule rule in blockedRules)
            {
                UnavailableWorkRegistry.Block(
                    pawn, rule, WeaponPreparationFailureCooldown);
            }

            int attempts = state.WeaponPreparationAttemptsThisTransition;
            int elapsed = state.WeaponPreparationStartedTick < 0
                ? 0
                : Math.Max(0, currentTick - state.WeaponPreparationStartedTick);
            string pendingJobName = pendingWork?.def?.defName ??
                newJob?.def?.defName ?? "work";
            string ruleNames = blockedRules.Count == 0
                ? "the managed area"
                : string.Join(", ", blockedRules.Select(rule => $"'{rule.Name}'"));

            tracker.ClearQueuedJobs(false);
            AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                pawn, newJob);
            ManagedWorkClaimRegistry.ReleaseAll(pawn);
            AutomaticOutfitManagerGameComponent.ClearPendingWork(state);
            component.RequestRecall(state);
            state.ClearWeaponPreparationRetry();
            ReplaceWithBriefWait(pawn, ref newJob, ref jobGiver, ref tag);

            if (AomLog.DetailedEnabled)
            {
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: stopped " +
                    $"preparing {pendingJobName} after {attempts} failed weapon " +
                    $"Equip attempt(s) over {elapsed} tick(s); briefly blocked " +
                    $"{ruleNames} and requested saved-outfit restoration instead " +
                    "of cycling through more locker stock.");
            }
            return true;
        }

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
            rules.AddRange(
                ProtectedBoundaryRetryRegistry.MatchingRules(pawn, job));
            rules.AddRange(PersistedBoundaryRulesForJob(pawn, job));
            return rules
                .Where(rule => rule != null)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
        }

        private static bool TrySelectBoundaryTransitStage(
            Pawn pawn,
            Job job,
            List<ApparelRule> destinationRules,
            List<ApparelRule> occupiedRules,
            out List<ApparelRule> boundaryTransitRules)
        {
            boundaryTransitRules = new List<ApparelRule>();
            if (pawn?.Map == null || job?.def == null ||
                destinationRules == null || destinationRules.Count == 0)
            {
                return false;
            }

            var destinationIds = new HashSet<string>(
                destinationRules.Where(rule => rule != null)
                    .Select(rule => rule.Id));
            boundaryTransitRules = ProtectedBoundaryRetryRegistry
                .MatchingRules(pawn, job)
                .Concat(PersistedBoundaryRulesForJob(pawn, job))
                .Where(rule => rule?.Enabled == true &&
                               !rule.WorkAreaPaused &&
                               rule.Area?.Map == pawn.Map &&
                               !destinationIds.Contains(rule.Id) &&
                               !RuleEvaluator.JobPreparationTargetsArea(
                                   job, rule.Area))
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
            if (boundaryTransitRules.Count == 0)
                return false;

            List<ApparelRule> occupied = (occupiedRules ??
                    new List<ApparelRule>())
                .Where(rule => rule?.Enabled == true &&
                               !rule.WorkAreaPaused &&
                               rule.Area?.Map == pawn.Map)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
            if (occupied.Any(rule => destinationIds.Contains(rule.Id)))
                return false;

            List<ApparelRule> immediateRules = occupied
                .Concat(boundaryTransitRules)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
            List<ApparelRule> combinedRules = immediateRules
                .Concat(destinationRules)
                .Where(rule => rule != null)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
            if (!RequirementsAreCompatible(immediateRules, pawn) ||
                !RequirementsAreCompatible(destinationRules, pawn) ||
                RequirementsAreCompatible(combinedRules, pawn))
            {
                return false;
            }

            if (AomLog.DetailedEnabled && AomLog.ShouldLogDetailed(
                    pawn,
                    $"staged-boundary-transit:{job.def.defName}:" +
                    string.Join(",", boundaryTransitRules.Select(rule => rule.Id)),
                    600))
            {
                string transitNames = string.Join(", ",
                    boundaryTransitRules.Select(rule => $"'{rule.Name}'"));
                string destinationNames = string.Join(", ",
                    destinationRules.Select(rule => $"'{rule.Name}'"));
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: staging " +
                    $"{job.def.defName} through {transitNames} before preparing " +
                    $"incompatible destination {destinationNames}; retained the " +
                    "exact job for sequential continuation.");
            }

            return true;
        }

        internal static bool PendingWorkJobIsViable(
            Pawn pawn, Job job, out string reason)
        {
            return PendingWorkJobIsViable(
                pawn, job, out reason, out bool _);
        }

        internal static bool PendingWorkJobIsViable(
            Pawn pawn, Job job, out string reason,
            out bool retryableFailure)
        {
            reason = null;
            retryableFailure = false;
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
            bool targetlessRecoveryWait =
                !hasMeaningfulTarget && IsRecoveryWaitJob(job);
            if (!hasMeaningfulTarget && !targetlessRecoveryWait)
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
                        retryableFailure = true;
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

            // HaulToCell reserves targetB exclusively in
            // JobDriver_HaulToCell.TryMakePreToilReservations. The source thing
            // can remain valid while another pawn takes that destination during
            // the outfit transition, so recheck the cell with the same vanilla
            // reservation cardinality immediately before replaying the job.
            if (job.def == JobDefOf.HaulToCell &&
                job.targetB.IsValid && !job.targetB.HasThing &&
                !pawn.CanReserve(job.targetB, 1, -1, null, false))
            {
                reason = $"haul destination {job.targetB.Cell} is no longer reservable";
                retryableFailure = true;
                return false;
            }

            // Player-assigned jobs often omit workGiverDef. Re-evaluate the job
            // through every path that can require managed apparel instead of
            // rejecting those valid continuations solely because the tag is
            // absent.
            // Preparing at the locker can move the pawn to the far side of a
            // protected route. A captured bed job may therefore stop crossing
            // the rule even though its bed and reservation are still valid.
            // Preserve that essential continuation: the active-state path will
            // restore personal gear once if the route is now clear, then queue
            // this exact LayDown instead of letting native selection recreate an
            // equip/cancel/restore cycle for the same bed.
            bool essentialPersonalContinuation =
                PausedAreaWorkFilter.IsEssentialPersonalJob(job);
            bool stillApplies = essentialPersonalContinuation ||
                                RuleEvaluator.MatchingRules(pawn, job).Count > 0 ||
                                PausedAreaWorkFilter.MatchingPermittedHaulingRule(pawn, job) != null ||
                                PausedAreaWorkFilter.MatchingProtectedTransitRules(pawn, job).Count > 0 ||
                                ProtectedBoundaryRetryRegistry.MatchingRules(pawn, job).Count > 0 ||
                                PersistedBoundaryRulesForJob(pawn, job).Count > 0 ||
                                (targetlessRecoveryWait &&
                                 RuleEvaluator.MatchingLocationRules(pawn).Count > 0);
            if (!stillApplies)
                reason = "the job no longer targets an active managed rule";
            return stillApplies;
        }

        private static List<ApparelRule> PersistedBoundaryRulesForJob(
            Pawn pawn, Job job)
        {
            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            PawnApparelState state = component?.StateFor(pawn);
            bool pendingContinuation = state?.PendingWorkJob != null &&
                SameJob(job, state.PendingWorkJob);
            bool activatedContinuation = state?.PendingWorkJob == null &&
                state?.PendingBoundaryWorkJobLoadId == job?.loadID;
            if (pawn?.Map == null || job?.def == null ||
                (!pendingContinuation && !activatedContinuation) ||
                state.PendingBoundaryRuleIds?.Count <= 0)
            {
                return new List<ApparelRule>();
            }

            return state.PendingBoundaryRuleIds
                .Select(component.RuleById)
                .Where(rule => rule?.Enabled == true &&
                               rule.Area?.Map == pawn.Map &&
                               RuleEvaluator.RuleCanApplyToPawn(pawn, rule) &&
                               PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(
                                   pawn, job, rule))
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
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
                out cell,
                state);
        }

        private static bool TryReplaceUnavailableGearWaitWithEgress(
            Pawn_JobTracker tracker,
            Pawn pawn,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref JobTag? tag)
        {
            if (tracker == null || pawn?.Map == null || newJob?.def == null)
                return false;

            // Waiting is safe when the pawn has not entered the protected area:
            // the unavailable-work registry makes the native thinker choose a
            // different target on its next pass. Waiting while already inside
            // without the complete set is different. Runtime occupancy
            // enforcement intercepts that wait again, so the pawn cannot even
            // accept an unrelated job that would take it outside. Move only to
            // the nearest locker/exterior cell and discard the blocked work.
            List<ApparelRule> occupiedRules =
                AutomaticOutfitManagerGameComponent.Current?.Rules?
                    .Where(rule => rule?.Enabled == true &&
                                   rule.Area?.Map == pawn.Map &&
                                   pawn.Position.IsValid &&
                                   pawn.Position.InBounds(pawn.Map) &&
                                   rule.Area[pawn.Position])
                    .GroupBy(rule => rule.Id)
                    .Select(group => group.First())
                    .ToList() ?? new List<ApparelRule>();
            List<ApparelRule> missingOccupiedRules = occupiedRules
                .Where(rule => !rule.WorkAreaPaused &&
                               RuleEvaluator.HasMissingRequiredGear(pawn, rule))
                .ToList();
            if (missingOccupiedRules.Count == 0)
                return false;

            Area preferredArea = missingOccupiedRules
                .Select(rule => rule.ChangingArea)
                .FirstOrDefault(area => area?.Map == pawn.Map);
            if (!TryFindSafeTransitionCell(
                    pawn, preferredArea, occupiedRules,
                    out IntVec3 safeCell))
            {
                return false;
            }

            // Mark every rule being exited, including a paused or already
            // satisfied overlap, so the path boundary recognizes this exact
            // internal job all the way to a cell outside the whole occupied
            // rule stack.
            foreach (ApparelRule rule in occupiedRules)
                UnavailableWorkRegistry.Block(pawn, rule);

            Job interruptedJob = newJob;
            tracker.ClearQueuedJobs(false);
            ManagedWorkClaimRegistry.Release(pawn, interruptedJob);
            AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                pawn, interruptedJob);

            newJob = MakeChangingAreaTravelJob(safeCell);
            newJob.expiryInterval = 2000;
            newJob.locomotionUrgency = LocomotionUrgency.Jog;
            jobGiver = null;
            tag = null;

            if (AomLog.DetailedEnabled)
            {
                string ruleNames = string.Join(
                    ", ", missingOccupiedRules.Select(rule => $"'{rule.Name}'"));
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: complete gear " +
                    $"is unavailable inside {ruleNames}; leaving for safe cell " +
                    $"{safeCell} before reconsidering {interruptedJob.def.defName}.");
            }
            return true;
        }

        private static bool TryRedirectIdleMissingGearWaitWithEgress(
            Pawn_JobTracker tracker,
            Pawn pawn,
            AutomaticOutfitManagerGameComponent component,
            PawnApparelState state,
            ref Job newJob,
            ref ThinkNode jobGiver,
            ref JobTag? tag)
        {
            bool playerWorker = pawn != null &&
                (pawn.IsColonist || pawn.IsSlave) &&
                !PawnAccessClassifier.IsHostedGuest(pawn) &&
                !PawnAccessClassifier.IsColonyPrisoner(pawn);
            if (!playerWorker || newJob?.playerForced == true ||
                !IsTargetlessRecoveryWaitJob(newJob) ||
                pawn.pather?.Moving == true ||
                pawn.carryTracker?.CarriedThing != null ||
                state?.Transition == ApparelTransition.ReturningToChangingArea ||
                state?.Transition == ApparelTransition.Restoring)
            {
                return false;
            }

            // A targetless wait selected while an exact native continuation is
            // still preparing belongs to that transition, not to autonomous
            // idle behavior. Preserve PendingWorkJob so the bounded preparation
            // retry can either find a new gear candidate or invoke the narrow
            // essential-personal fallback after confirming a real shortage.
            if (state?.Transition == ApparelTransition.Preparing &&
                state.PendingWorkJob != null)
            {
                return false;
            }

            // A pawn already relying on managed apparel for a live environmental
            // hazard must keep that protection. The normal occupancy planner can
            // finish preparing them instead of redirecting the wait.
            if (state != null &&
                HazardousEnvironmentSafety.MustRetainManagedProtectionAt(
                    pawn, state, pawn.Position, out _))
            {
                return false;
            }

            string waitName = newJob.def?.defName ?? "Wait";
            if (!TryReplaceUnavailableGearWaitWithEgress(
                    tracker, pawn, ref newJob, ref jobGiver, ref tag))
            {
                return false;
            }

            if (state != null)
            {
                component?.RequestRecall(state);
                state.RecallInterruptPending = false;
                state.Transition = ApparelTransition.Active;
                state.LastApparelPreparationAttemptTick = -1;
                state.LastApparelPreparationThingId = -1;
                state.ClearWeaponPreparationRetry();
                state.ActiveIdleTicks = 0;
                AutomaticOutfitManagerGameComponent.ClearPendingWork(state);
                ManagedWorkClaimRegistry.ReleaseAll(pawn);
            }

            if (AomLog.DetailedEnabled)
            {
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: safely idle " +
                    $"{waitName} needs no work outfit; leaving the protected area " +
                    "instead of starting gear preparation.");
            }
            return true;
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
            out IntVec3 cell,
            PawnApparelState restorationState = null)
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
                ChangingCellIsAvailable(pawn, candidate) &&
                (restorationState == null ||
                 !HazardousEnvironmentSafety.MustRetainManagedProtectionAt(
                     pawn, restorationState, candidate, out _));

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
                    return state.IsPreparationApparel(apparel);
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
            if (state?.Transition != ApparelTransition.ReturningToChangingArea)
            {
                return false;
            }

            if (IsAssignedCrossMapChangingAreaReturnJob(state, job))
                return true;

            if (!IsChangingAreaCellTravelJob(job) ||
                !job.targetA.Cell.IsValid)
            {
                return false;
            }

            return state.ChangingAreaReturnCell.IsValid &&
                   job.targetA.Cell == state.ChangingAreaReturnCell;
        }

        internal static bool IsChangingAreaTravelJob(Job job)
            => IsChangingAreaCellTravelJob(job) ||
               job?.def == JobDefOf.EnterPortal;

        internal static bool IsChangingAreaCellTravelJob(Job job)
            => job?.def == AutomaticOutfitManagerJobDefOf
                   .AutomaticOutfitManager_LockerReturn ||
               job?.def == JobDefOf.Goto;

        internal static bool IsAssignedCrossMapChangingAreaReturnJob(
            PawnApparelState state, Job job)
        {
            if (state?.Transition != ApparelTransition.ReturningToChangingArea ||
                job?.def != JobDefOf.EnterPortal ||
                job.targetA.Thing is not MapPortal portal)
            {
                return false;
            }

            Map destinationMap = AutomaticOutfitManagerGameComponent.Current?
                .RuleById(state.ActiveRuleId)?.ChangingArea?.Map;
            return destinationMap != null &&
                   PortalLeadsToMap(portal, destinationMap);
        }

        internal static bool TryMakeCrossMapChangingAreaReturnJob(
            Pawn pawn,
            Map destinationMap,
            out Job returnJob,
            out MapPortal returnPortal)
        {
            returnJob = null;
            returnPortal = null;
            if (pawn?.Map == null || destinationMap == null ||
                destinationMap == pawn.Map || JobDefOf.EnterPortal == null)
            {
                return false;
            }

            returnPortal = pawn.Map.listerThings.AllThings
                .OfType<MapPortal>()
                .Where(portal => portal?.Spawned == true &&
                                 PortalLeadsToMap(portal, destinationMap) &&
                                 PortalIsEnterable(portal) &&
                                 pawn.CanReach(
                                     portal, PathEndMode.Touch, Danger.Deadly))
                .OrderBy(portal =>
                    portal.Position.DistanceToSquared(pawn.Position))
                .FirstOrDefault();
            if (returnPortal == null)
                return false;

            returnJob = JobMaker.MakeJob(JobDefOf.EnterPortal, returnPortal);
            returnJob.expiryInterval = 2000;
            returnJob.locomotionUrgency = LocomotionUrgency.Jog;
            return true;
        }

        private static bool PortalLeadsToMap(
            MapPortal portal, Map destinationMap)
        {
            if (portal == null || destinationMap == null)
                return false;

            try
            {
                return portal.GetOtherMap() == destinationMap;
            }
            catch
            {
                // A portal can be tearing down with its pocket map. Treat it as
                // unavailable and let native selection or a later retry proceed.
                return false;
            }
        }

        private static bool PortalIsEnterable(MapPortal portal)
        {
            try
            {
                return portal?.IsEnterable(out _) == true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsUnavailableGearEgressJob(
            Pawn pawn, Job job, ApparelRule rule)
        {
            if (pawn?.Map == null || rule?.Area?.Map != pawn.Map ||
                job?.def != AutomaticOutfitManagerJobDefOf
                    .AutomaticOutfitManager_LockerReturn ||
                !pawn.Position.IsValid || !pawn.Position.InBounds(pawn.Map) ||
                !job.targetA.Cell.IsValid || !job.targetA.Cell.InBounds(pawn.Map))
            {
                return false;
            }

            // The exception is directional: a pawn may finish leaving the rule
            // whose shortage was recorded, but once outside it cannot use the
            // same internal job to enter again without the complete gear set.
            return rule.Area[pawn.Position] && !rule.Area[job.targetA.Cell] &&
                   UnavailableWorkRegistry.HasActiveRuleBlock(pawn, rule);
        }

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
            return !IsNativeEmergencySafetyJob(job) &&
                   !defName.StartsWith("Wait", StringComparison.OrdinalIgnoreCase) &&
                   !defName.StartsWith("Goto", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(defName, "TakeInventory", StringComparison.OrdinalIgnoreCase) &&
                   job.def != JobDefOf.Wait &&
                   !IsChangingAreaTravelJob(job) &&
                   job.def != JobDefOf.Wear &&
                   job.def != JobDefOf.RemoveApparel;
        }

        internal static bool IsNativeEmergencySafetyJob(Job job)
        {
            string defName = job?.def?.defName ?? string.Empty;
            return defName.StartsWith("Flee", StringComparison.OrdinalIgnoreCase) ||
                   defName.IndexOf("Cower", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool IsMapDepartureJob(Job job)
        {
            if (job?.def == null)
                return false;

            if (job.exitMapOnArrival)
                return true;

            // Flying pawns use a dedicated exit job instead of the ordinary
            // Goto flag. Keep this narrow: hostile Steal/Kidnap drivers also end
            // at the map edge but remain emergency/native behavior.
            string defName = job.def.defName ?? string.Empty;
            return defName.Equals(
                       "ExitMapFlying", StringComparison.OrdinalIgnoreCase) ||
                   defName.StartsWith(
                       "ExitMap", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRecoveryWaitJob(Job job)
        {
            string defName = job?.def?.defName ?? string.Empty;
            return job?.def == JobDefOf.Wait ||
                   job?.def == JobDefOf.Wait_Wander ||
                   defName.StartsWith("Wait", StringComparison.OrdinalIgnoreCase) ||
                   defName.IndexOf("Standing", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsTargetlessRecoveryWaitJob(Job job)
        {
            if (!IsRecoveryWaitJob(job))
                return false;

            return !job.targetA.IsValid &&
                   !job.targetB.IsValid &&
                   !job.targetC.IsValid &&
                   (job.targetQueueA == null ||
                    !job.targetQueueA.Any(target => target.IsValid)) &&
                   (job.targetQueueB == null ||
                    !job.targetQueueB.Any(target => target.IsValid));
        }

        internal static bool IsNativePrisonerUnavailableGearFallbackJob(
            Pawn pawn, Job job)
        {
            return IsNativePrisonerUnavailableGearFallbackJobFamily(pawn, job) &&
                   RuleEvaluator.MatchingLocationRules(pawn).Count > 0;
        }

        internal static bool IsNativePrisonerUnavailableGearFallbackJobFamily(
            Pawn pawn, Job job)
        {
            if (!PawnAccessClassifier.IsColonyPrisoner(pawn) || job?.def == null)
                return false;

            string defName = job.def.defName ?? string.Empty;
            bool nativeWait = job.def == JobDefOf.Wait ||
                              job.def == JobDefOf.Wait_Wander ||
                              defName.StartsWith("Wait", StringComparison.OrdinalIgnoreCase);
            bool nativeWander = job.def == JobDefOf.GotoWander ||
                                defName.IndexOf(
                                    "Wander", StringComparison.OrdinalIgnoreCase) >= 0;
            bool nativeEating = job.def == JobDefOf.Ingest ||
                                defName.IndexOf(
                                    "Ingest", StringComparison.OrdinalIgnoreCase) >= 0;
            return nativeWait || nativeWander || nativeEating ||
                   PausedAreaWorkFilter.IsEssentialPersonalJob(job);
        }

        private static bool RequiresImmediateRestoration(Job job)
            // Sleep is a long-lived state rather than an ordinary buffer task.
            // Callers restore immediately only after confirming that neither
            // its destination nor route is protected by an active rule.
            => PausedAreaWorkFilter.IsEssentialPersonalJob(job);

        internal static void LogAutomaticManagedGearRejection(
            Pawn pawn, Job job, Thing gear, string stage)
        {
            if (!AomLog.DetailedEnabled || pawn == null || job?.def == null || gear == null ||
                !AomLog.ShouldLogDetailed(
                    pawn, $"automatic-managed-gear:{gear.thingIDNumber}", 60000))
            {
                return;
            }

            string gearKind = gear.def?.IsWeapon == true ? "weapon" : "apparel";
            AomLog.Detailed(
                $"[AutomaticOutfitManager] {pawn.LabelShortCap}: ignored automatic " +
                $"{job.def.defName} for managed {gearKind} {gear.LabelCap} at {stage}; " +
                "only an Automatic Outfit Manager transition or explicit player order may use it.");
        }

        private static void LogHazardProtectionHold(
            Pawn pawn, string reason, string context)
        {
            if (!AomLog.DetailedEnabled || pawn == null ||
                !AomLog.ShouldLogDetailed(
                    pawn, $"environmental-protection:{reason}:{context}"))
            {
                return;
            }

            AomLog.Detailed(
                $"[AutomaticOutfitManager] {pawn.LabelShortCap}: retaining " +
                $"managed protection during {context} because of " +
                $"{reason ?? "hazardous conditions"}.");
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
            Job waitJob = MakeSafeWaitJob(pawn, expiryInterval);
            AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                pawn, newJob);
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
            // Clearing first also repairs stale or duplicated preparation steps
            // from an older save before this transition becomes the sole owner.
            tracker.ClearQueuedJobs(false);
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
            AutomaticOutfitManagerGameComponent.ReleaseNativeReservations(
                PawnField(tracker), newJob);
            tracker.ClearQueuedJobs(false);
            for (int i = jobs.Count - 1; i >= 1; i--)
                tracker.jobQueue.EnqueueFirst(jobs[i]);

            newJob = jobs[0];
            jobGiver = null;
            tag = null;
        }

        internal static Job MakeSafeWaitJob(Pawn pawn, int expiryInterval)
        {
            // JobDriver_Wait checks colonist drafting/auto-attack state that a
            // colony prisoner does not own. Assigning it to a prisoner throws in
            // CheckForAutoAttack and can leave the pawn jobless. Wait_Wander is
            // RimWorld's native prisoner-safe idle family and still gives AOM a
            // bounded retry without taking control of the prisoner's next task.
            Job waitJob;
            if (PawnAccessClassifier.IsColonyPrisoner(pawn) ||
                (pawn?.RaceProps?.Humanlike == true && pawn.drafter == null))
            {
                waitJob = JobMaker.MakeJob(JobDefOf.Wait_Wander);
            }
            else
            {
                // Some StartJob callers immediately inspect targetA while
                // deciding whether to append an opportunistic haul. Preserve the
                // complete self-targeted wait used for those callers.
                waitJob = pawn != null
                    ? JobMaker.MakeJob(JobDefOf.Wait, pawn)
                    : JobMaker.MakeJob(JobDefOf.Wait);
            }
            waitJob.expiryInterval = expiryInterval;
            return waitJob;
        }

    }
}
