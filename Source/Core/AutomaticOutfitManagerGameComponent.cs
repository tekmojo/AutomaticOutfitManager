using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Core
{
    public sealed class AutomaticOutfitManagerGameComponent : GameComponent
    {
        private const int RestorationNoProgressTimeoutTicks = 600;
        private const int ActiveWorkNoProgressTimeoutTicks = 600;
        private const int ActiveIdleGraceTicks = 240;
        private const int IncompleteTaskBufferIdleGraceTicks = 900;
        private const int PreparingIdleGraceTicks = 120;
        private const int PreparationRetryIntervalTicks = 300;
        private const int ManagedGearWakeCoalesceTicks = 120;
        private const int DepartureUnavailableAttemptLimit = 3;

        private sealed class RestorationProgress
        {
            public int LastProgressTick;
            public int JobLoadId;
            public JobDef JobDef;
            public int ToilIndex;
            public int TicksLeftThisToil;
            public int QueueCount;
            public IntVec3 PawnPosition;
            public int TargetThingId;
            public IntVec3 TargetPosition;
            public bool TargetSpawned;
            public int WornOriginalCount;
            public int WornManagedCount;
            public int PrimaryThingId;
            public int CarriedThingId;
        }

        private sealed class RestorationRecoveryBackoff
        {
            public int LastAttemptTick;
            public int ConsecutiveAttempts;
        }

        public List<ApparelRule> Rules = new List<ApparelRule>();
        public List<PawnApparelState> PawnStates = new List<PawnApparelState>();
        public List<string> ManagedApparelIds = new List<string>();
        public Dictionary<string, string> ManagedApparelOwners = new Dictionary<string, string>();
        public Dictionary<string, string> ManagedApparelOwnerIds = new Dictionary<string, string>();
        public List<string> ManagedWeaponIds = new List<string>();
        public List<ThingDef> ManagedApparelStockDefs = new List<ThingDef>();
        public List<ThingDef> ManagedWeaponStockDefs = new List<ThingDef>();

        private readonly Dictionary<Pawn, PawnApparelState> pawnStateIndex = new Dictionary<Pawn, PawnApparelState>();
        private readonly HashSet<string> managedApparelIdIndex = new HashSet<string>();
        private readonly HashSet<string> managedWeaponIdIndex = new HashSet<string>();
        private readonly HashSet<ThingDef> managedApparelDefIndex = new HashSet<ThingDef>();
        private readonly HashSet<ThingDef> managedWeaponDefIndex = new HashSet<ThingDef>();
        private readonly HashSet<ThingWithComps> stateTrackedWeaponIndex = new HashSet<ThingWithComps>();
        private readonly Dictionary<ThingWithComps, Pawn> savedWeaponOwnerIndex = new Dictionary<ThingWithComps, Pawn>();
        private readonly Dictionary<ThingWithComps, Pawn> managedWeaponAssignmentIndex = new Dictionary<ThingWithComps, Pawn>();
        private readonly Dictionary<string, Pawn> spawnedPawnIdIndex = new Dictionary<string, Pawn>();
        private readonly Dictionary<Pawn, int> jobTransitionFailureTicks = new Dictionary<Pawn, int>();
        private readonly Dictionary<Pawn, int> occupiedGearRecoveryTicks =
            new Dictionary<Pawn, int>();
        private readonly Dictionary<Pawn, RestorationProgress> restorationProgress =
            new Dictionary<Pawn, RestorationProgress>();
        private readonly Dictionary<Pawn, RestorationRecoveryBackoff>
            restorationRecoveryBackoff =
                new Dictionary<Pawn, RestorationRecoveryBackoff>();
        private readonly Dictionary<Pawn, RestorationProgress> activeWorkProgress =
            new Dictionary<Pawn, RestorationProgress>();
        private readonly Dictionary<Pawn, int> rejectedManagedGearWakeTicks =
            new Dictionary<Pawn, int>();
        private int indexedPawnStateCount = -1;
        private int indexedManagedApparelCount = -1;
        private int indexedManagedWeaponCount = -1;
        private bool managedApparelDefIndexDirty = true;
        private bool managedWeaponDefIndexDirty = true;
        private bool weaponStateIndexDirty = true;
        private int spawnedPawnIndexTick = -1;

        public AutomaticOutfitManagerGameComponent(Game game)
        {
            RuleEvaluator.ResetRuntimeCache();
            PawnAccessClassifier.ResetRuntimeCache();
            Patches.ProtectedPathAvoidance.ResetForLoadedGame();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (currentTick % 30 != 0)
                return;

            ProcessPendingRecallInterrupts(currentTick);
            EnforceRuntimePawnRules(currentTick);
            RecoverIdleApparelWorkers(currentTick);
        }

        public void RequestRecall(PawnApparelState state)
        {
            if (state?.Pawn == null)
                return;

            state.PauseRecallRuleIds?.Clear();
            RequestRecallCore(state);
        }

        public void RequestRulePauseRecall(
            PawnApparelState state, ApparelRule pausedRule)
        {
            if (state?.Pawn == null || pausedRule == null)
                return;

            state.PauseRecallRuleIds ??= new List<string>();
            if (!state.PauseRecallRuleIds.Contains(pausedRule.Id))
                state.PauseRecallRuleIds.Add(pausedRule.Id);
            RequestRecallCore(state);
        }

        private static void RequestRecallCore(PawnApparelState state)
        {
            state.RecallRequested = true;
            state.NaturalLockerDwellUntilTick = -1;
            state.ClearPendingBufferCandidates();

            // Returning/restoring is already the recall operation. Re-arming
            // the forced interrupt here can cancel the exact Goto or Wear job
            // that must finish the recall, leaving the pawn in an idle rebuild
            // loop when overlapping rules are paused in quick succession.
            if (state.Transition == ApparelTransition.ReturningToChangingArea ||
                state.Transition == ApparelTransition.Restoring)
            {
                state.RecallInterruptPending = false;
                return;
            }

            state.RecallInterruptPending = true;
        }

        public bool TryCancelRulePauseRecall(
            PawnApparelState state, ApparelRule resumedRule)
        {
            Pawn pawn = state?.Pawn;
            if (pawn == null || resumedRule?.Enabled != true ||
                resumedRule.WorkAreaPaused)
            {
                return false;
            }

            state.PauseRecallRuleIds ??= new List<string>();
            if (!state.PauseRecallRuleIds.Remove(resumedRule.Id))
                return false;

            // Another still-paused rule also requested this return. Reopening
            // just one of the overlapping areas must not cancel that recall.
            if (state.PauseRecallRuleIds.Count > 0 ||
                !state.RecallRequested)
            {
                return false;
            }

            // Once Phase 3 has begun, finish it atomically. Reversing an active
            // locker Goto, managed-item return, or saved-outfit restoration is
            // more disruptive than letting the next qualifying job reacquire
            // the outfit. A pending Active/Preparing recall, however, has not
            // crossed that boundary and can be safely withdrawn.
            if ((state.Transition != ApparelTransition.Active &&
                 state.Transition != ApparelTransition.Preparing) ||
                pawn.Downed || pawn.Drafted ||
                state.DownedTransitionSuspended ||
                state.DraftedTransitionSuspended)
            {
                return false;
            }

            bool tracksResumedRule = state.ActiveRuleId == resumedRule.Id ||
                state.CurrentRuleIds?.Contains(resumedRule.Id) == true ||
                state.NestedRuleBuffers?.Any(progress =>
                    progress?.RuleId == resumedRule.Id) == true;
            if (!tracksResumedRule)
                return false;

            state.RecallRequested = false;
            state.RecallInterruptPending = false;
            state.LastRecallInterruptAttemptTick = -1;
            state.NaturalLockerDwellUntilTick = -1;
            state.ActiveIdleTicks = 0;

            // Resume starts a fresh buffer for the reopened rule. Otherwise a
            // worker whose buffer was already full before Pause can immediately
            // request another return and appear to ignore Resume.
            if (state.ActiveRuleId == resumedRule.Id)
            {
                state.BufferedTasksCompleted = 0;
                state.LastBufferedJobLoadId = -1;
                state.ClearPendingBufferedTask();
            }

            NestedRuleBufferState nestedProgress = state.NestedRuleBuffers?
                .FirstOrDefault(progress => progress?.RuleId == resumedRule.Id);
            if (nestedProgress != null)
            {
                nestedProgress.Completed = 0;
                nestedProgress.Finished = false;
                nestedProgress.LastJobLoadId = -1;
                nestedProgress.LastJobLabel = null;
            }

            activeWorkProgress.Remove(pawn);
            jobTransitionFailureTicks.Remove(pawn);

            if (Prefs.DevMode)
            {
                Log.Message(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: work resumed " +
                    $"for '{resumedRule.Name}' before locker return; cancelled " +
                    "the pause-generated recall and retained the managed outfit.");
            }
            return true;
        }

        public void PrepareForMapDeparture(PawnApparelState state)
        {
            Pawn pawn = state?.Pawn;
            if (pawn == null)
                return;

            // RimWorld can remove quest-lodger/host markers before it assigns
            // the native exit job. Retain every still-live guest signal here,
            // but never replace the session marker with the later false value.
            if (PawnAccessClassifier.IsHostedGuest(pawn))
                state.HostedGuestSession = true;

            if (!state.MapDepartureRequested)
            {
                state.MapDepartureRequested = true;
                state.MapDepartureRequestedTick =
                    Find.TickManager?.TicksGame ?? 0;
                state.DepartureRestorationAttempts = 0;
            }

            // Leaving the map is not another buffered task. A native exit job
            // commonly uses Goto with exitMapOnArrival, and its route naturally
            // begins inside the protected area. Treating that route as work used
            // to equip a departing visitor and then let the visitor take the
            // locker gear off-map. Cancel the old continuation and force the
            // normal Phase 3 locker/restoration path before native AI retries
            // its still-current departure duty.
            ClearPendingWork(state);
            ManagedWorkClaimRegistry.ReleaseAll(pawn);
            RequestRecall(state);
            state.BufferedTasksCompleted = 0;
            state.LastBufferedJobLoadId = -1;
            state.ClearPendingBufferCandidates();
        }

        public void FinalizeInterruptedMapDeparture(Pawn pawn)
        {
            PawnApparelState state = StateFor(pawn);
            if (pawn?.Spawned != true || state == null)
                return;

            int released = DropHeldManagedGear(
                pawn, state, pawn.Map, pawn.Position);
            string reason = released == 1
                ? "map exit bypassed Phase 3; dropped 1 managed work item and released the abandoned saved-outfit claims"
                : $"map exit bypassed Phase 3; dropped {released} managed work items and released the abandoned saved-outfit claims";
            EndIntervention(pawn, reason);
        }

        public void SuspendTransitionWhileDowned(PawnApparelState state)
        {
            Pawn pawn = state?.Pawn;
            if (pawn == null || !pawn.Downed)
                return;

            // Incapacitated pawns must remain entirely under RimWorld's native
            // Wait_Downed, rescue, tending, carrying, and bed-rest behavior. Keep
            // the exact saved-outfit snapshot claimed, but release the work that
            // caused the transition and postpone every movement/equipment step
            // until the pawn can act again.
            bool firstSuspension = !state.DownedTransitionSuspended;
            state.DownedTransitionSuspended = true;
            state.DraftedTransitionSuspended = false;
            state.DraftedLockerReturnRequired = false;
            state.RecallRequested = true;
            state.RecallInterruptPending = false;
            ClearPendingWork(state);
            state.RequestWeaponRestoration();
            state.Transition = ApparelTransition.Restoring;
            state.ChangingAreaReturnCell = IntVec3.Invalid;
            state.LastChangingAreaReturnAttemptTick = -1;
            state.LastRestorationAttemptTick = -1;
            state.LastApparelPreparationAttemptTick = -1;
            state.LastApparelPreparationThingId = -1;
            state.ClearWeaponPreparationRetry();
            state.ActiveIdleTicks = 0;
            state.ClearPendingBufferCandidates();
            ManagedWorkClaimRegistry.ReleaseAll(pawn);
            restorationProgress.Remove(pawn);
            restorationRecoveryBackoff.Remove(pawn);
            activeWorkProgress.Remove(pawn);
            jobTransitionFailureTicks.Remove(pawn);

            if (firstSuspension && Prefs.DevMode)
            {
                Log.Message(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: pawn is downed; " +
                    "suspending locker travel and saved-outfit restoration until recovery.");
            }
        }

        public void RetainManagedProtectionForHazard(
            Pawn pawn, PawnApparelState state)
        {
            if (pawn == null || state == null)
                return;

            state.Transition = ApparelTransition.Active;
            state.ChangingAreaReturnCell = IntVec3.Invalid;
            state.LastChangingAreaReturnAttemptTick = -1;
            state.LastRestorationAttemptTick = -1;
            state.UnavailableRestorationAttempts = 0;
            state.LastApparelPreparationAttemptTick = -1;
            state.LastApparelPreparationThingId = -1;
            state.ClearWeaponPreparationRetry();
            state.ActiveIdleTicks = 0;
            state.ClearPendingBufferCandidates();
            ClearPendingWork(state);
            ManagedWorkClaimRegistry.ReleaseAll(pawn);
            restorationProgress.Remove(pawn);
            restorationRecoveryBackoff.Remove(pawn);
            activeWorkProgress.Remove(pawn);
        }

        public void SuspendTransitionWhileDrafted(PawnApparelState state)
        {
            Pawn pawn = state?.Pawn;
            if (pawn == null || !pawn.Drafted)
                return;

            // Drafting is an explicit player/native combat override. Cancel the
            // civilian work continuation and every AOM-owned transition step,
            // but keep the saved outfit snapshot until the pawn is undrafted.
            // Transition handling ignores drafted pawns and resumes on the first
            // normal component pulse after combat control ends.
            bool firstSuspension = !state.DraftedTransitionSuspended;
            state.DraftedTransitionSuspended = true;
            state.DraftedLockerReturnRequired = true;
            state.RecallRequested = true;
            state.RecallInterruptPending = false;
            ClearPendingWork(state);
            state.RequestWeaponRestoration();
            // Keep the session at the work-outfit side of the locker boundary.
            // Once undrafted, the normal Active + RecallRequested path returns
            // the pawn to the configured changing area before any managed item
            // is removed or returned.
            state.Transition = ApparelTransition.Active;
            state.ChangingAreaReturnCell = IntVec3.Invalid;
            state.LastChangingAreaReturnAttemptTick = -1;
            state.LastRestorationAttemptTick = -1;
            state.LastApparelPreparationAttemptTick = -1;
            state.LastApparelPreparationThingId = -1;
            state.ClearWeaponPreparationRetry();
            state.ActiveIdleTicks = 0;
            state.BufferedTasksCompleted = 0;
            state.LastBufferedJobLoadId = -1;
            state.ClearPendingBufferCandidates();
            ManagedWorkClaimRegistry.ReleaseAll(pawn);
            restorationProgress.Remove(pawn);
            restorationRecoveryBackoff.Remove(pawn);
            activeWorkProgress.Remove(pawn);
            jobTransitionFailureTicks.Remove(pawn);

            if (firstSuspension && Prefs.DevMode)
            {
                Log.Message(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: pawn was drafted; " +
                    "cancelled the civilian continuation and suspended saved-outfit " +
                    "restoration until undrafted.");
            }
        }

        public bool ResumeTransitionAfterDrafted(PawnApparelState state)
        {
            Pawn pawn = state?.Pawn;
            if (pawn == null || pawn.Drafted ||
                !state.DraftedTransitionSuspended)
            {
                return false;
            }

            state.DraftedTransitionSuspended = false;
            // Undrafting resumes the normal active-session contract. It is not
            // an explicit Recall: the configured task buffer starts fresh and
            // keeps the work outfit through those follow-up jobs before locker
            // return. DraftedLockerReturnRequired independently prevents any
            // later restoration from falling back to the current cell when the
            // gravship locker is still on another map.
            state.RecallRequested = false;
            state.RecallInterruptPending = false;
            state.Transition = ApparelTransition.Active;
            state.ChangingAreaReturnCell = IntVec3.Invalid;
            state.LastChangingAreaReturnAttemptTick = -1;
            state.NaturalLockerDwellUntilTick = -1;
            state.ActiveIdleTicks = 0;
            state.LastRestorationAttemptTick = -1;
            restorationProgress.Remove(pawn);
            activeWorkProgress.Remove(pawn);

            if (Prefs.DevMode)
            {
                Log.Message(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: pawn was " +
                    "undrafted; resuming the task buffer with the complete " +
                    "work outfit before locker return.");
            }
            return true;
        }

        public void NotifyRejectedManagedGearJob(Pawn pawn)
        {
            PawnApparelState state = StateFor(pawn);
            if (state?.Transition != ApparelTransition.Restoring)
                return;

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (rejectedManagedGearWakeTicks.TryGetValue(pawn, out int lastWakeTick) &&
                currentTick - lastWakeTick < ManagedGearWakeCoalesceTicks)
            {
                return;
            }
            rejectedManagedGearWakeTicks[pawn] = currentTick;

            // Simple Sidearms and similar thinkers can repeatedly offer a
            // managed work weapon while the pawn is trying to restore its saved
            // outfit. Once that job is rejected, skip the unavailable-item
            // cooldown so the next component pulse can rebuild restoration.
            // Coalescing above prevents the same automatic proposal from
            // continually resetting recovery while a valid transition job is
            // already making progress.
            state.LastRestorationAttemptTick = -1;
            state.ActiveIdleTicks = System.Math.Max(state.ActiveIdleTicks, 240);
        }

        private void ProcessPendingRecallInterrupts(int currentTick)
        {
            foreach (PawnApparelState state in PawnStates.ToList())
            {
                Pawn pawn = state?.Pawn;
                if (state?.RecallInterruptPending != true || pawn?.Spawned != true ||
                    pawn.Drafted || pawn.jobs == null)
                {
                    continue;
                }

                if (state.Transition == ApparelTransition.ReturningToChangingArea ||
                    state.Transition == ApparelTransition.Restoring)
                {
                    state.RecallInterruptPending = false;
                    continue;
                }

                // Do not force-interrupt a wear/remove toil after it has begun.
                // Apparel drivers can temporarily remove conflicting layers
                // before the replacement is committed. Let this exact assigned
                // step finish; the next StartJob call cancels any remaining
                // preparation and enters the normal recall path.
                if (IsAssignedApparelTransitionJob(state, pawn.jobs.curJob) ||
                    IsAssignedWeaponTransitionJob(state, pawn.jobs.curJob))
                    continue;

                // A broken third-party job can throw while RimWorld selects the
                // replacement job. Keep that failure out of the UI and avoid a
                // retry every tick; a later attempt can recover after the stale
                // target or reservation has been cleared.
                if (state.LastRecallInterruptAttemptTick >= 0 &&
                    currentTick - state.LastRecallInterruptAttemptTick < 300)
                {
                    continue;
                }

                state.LastRecallInterruptAttemptTick = currentTick;
                bool clearTrackedOnlySession =
                    !state.ApparelInterventionActive &&
                    !state.WeaponInterventionActive;
                if (pawn.jobs.curJob == null || TryJobTransition(pawn, currentTick, "return request", () =>
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true)))
                {
                    state.RecallInterruptPending = false;
                    if (clearTrackedOnlySession)
                        EndIntervention(pawn);
                }
            }
        }

        private static bool IsAssignedApparelTransitionJob(
            PawnApparelState state, Job job)
        {
            if (state == null || job?.targetA.Thing is not RimWorld.Apparel apparel)
                return false;

            if (job.def == JobDefOf.Wear)
            {
                return state.Transition == ApparelTransition.Restoring
                    ? state.OriginalApparel?.Contains(apparel) == true
                    : state.IsPreparationApparel(apparel);
            }

            if (job.def == JobDefOf.RemoveApparel)
                return state.ManagedApparel?.Contains(apparel) == true;

            return state.Transition == ApparelTransition.Restoring &&
                   (job.def == JobDefOf.HaulToCell ||
                    job.def == JobDefOf.HaulToContainer) &&
                   state.ManagedApparel?.Contains(apparel) == true;
        }

        private static bool IsAssignedWeaponTransitionJob(
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
                       (state.WeaponRestorationRequested && state.OriginalWeapon == weapon);
            }

            return job.def == JobDefOf.DropEquipment &&
                   (state.IsManagedWeapon(weapon) ||
                    (state.WeaponRestorationRequested &&
                     state.WeaponPlayerOverride &&
                     state.Pawn?.equipment?.Primary == weapon));
        }

        private void EnforceRuntimePawnRules(int currentTick)
        {
            bool enforceApparelOwnership = ManagedApparelOwnerIds.Count > 0;
            EnsureWeaponStateIndex();
            bool enforceWeaponOwnership = savedWeaponOwnerIndex.Count > 0;
            bool activeRestoration = HasActiveRestoration();

            foreach (Map map in Find.Maps)
            {
                IReadOnlyList<ApparelRule> mapEnabledRules =
                    RuleEvaluator.EnabledRulesForMap(map);
                IReadOnlyList<ApparelRule> mapActiveRules =
                    RuleEvaluator.ActiveRulesForMap(map);
                IReadOnlyList<ApparelRule> mapPausedRules =
                    RuleEvaluator.PausedRulesForMap(map);
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (pawn?.Downed == true)
                    {
                        PawnApparelState downedState = StateFor(pawn);
                        if (downedState != null)
                            SuspendTransitionWhileDowned(downedState);
                        occupiedGearRecoveryTicks.Remove(pawn);
                        continue;
                    }

                    // A prison break (or slave rebellion) is an explicit native
                    // custody transition. The pawn can seek weapons, fight, and
                    // escape while RimWorld still reports prisoner/slave status;
                    // release any former cooperative session and do not apply
                    // protected-area occupancy or ownership enforcement.
                    if (PawnAccessClassifier.IsNativeCustodyEscapeActive(pawn))
                    {
                        occupiedGearRecoveryTicks.Remove(pawn);
                        UnavailableWorkRegistry.Clear(pawn, Rules);
                        if (StateFor(pawn) != null)
                        {
                            EndIntervention(
                                pawn,
                                "native custody escape took control");
                        }
                        continue;
                    }

                    if (pawn?.Drafted == true)
                    {
                        occupiedGearRecoveryTicks.Remove(pawn);
                        PawnApparelState draftedState = StateFor(pawn);
                        if (draftedState != null)
                        {
                            bool firstDraftedSuspension =
                                !draftedState.DraftedTransitionSuspended;
                            SuspendTransitionWhileDrafted(draftedState);
                            if (firstDraftedSuspension)
                                pawn.jobs?.ClearQueuedJobs(false);
                        }
                        continue;
                    }

                    PawnApparelState resumedDraftedState = StateFor(pawn);
                    if (resumedDraftedState?.DraftedTransitionSuspended == true)
                    {
                        ResumeTransitionAfterDrafted(resumedDraftedState);
                    }

                    if (Patches.PawnJobTracker_StartJob_Patch
                        .IsNativeEmergencySafetyJob(pawn?.jobs?.curJob))
                    {
                        occupiedGearRecoveryTicks.Remove(pawn);
                        continue;
                    }

                    // A haul selected before an outfit snapshot became active can
                    // still reserve or carry the exact apparel/weapon when its
                    // owner starts restoring. Release only automatic jobs for an
                    // actively restoring owner; ordinary locker restocking while
                    // the owner is working remains untouched.
                    if (activeRestoration &&
                        TryReleaseSavedGearNeededForRestoration(pawn, currentTick))
                        continue;

                    if ((enforceApparelOwnership || enforceWeaponOwnership) &&
                        pawn?.RaceProps?.Humanlike == true)
                    {
                        EnforceSavedGearOwnership(
                            pawn, enforceApparelOwnership, enforceWeaponOwnership,
                            currentTick);
                    }

                    Job job = pawn?.jobs?.curJob;
                    PawnApparelState runtimeState = null;
                    if (pawn?.Faction == Faction.OfPlayer && !pawn.Drafted)
                    {
                        runtimeState = StateFor(pawn);
                        // Preserve the established native/player and weapon-mod
                        // override contract before the occupancy safety check
                        // decides whether a required primary is missing.
                        DetectExternalWeaponOverride(pawn, runtimeState, job);
                    }
                    if (mapActiveRules.Count > 0 &&
                        TryEnforceRuntimeProtectedGear(pawn, job, currentTick))
                        continue;
                    if (job == null)
                        continue;

                    // With no live area on this map, only saved-gear ownership
                    // and transition recovery above can apply. Avoid running the
                    // work/hauling/wandering filters for unrelated maps.
                    if (mapEnabledRules.Count == 0)
                        continue;

                    bool handled = false;
                    if (pawn.Faction == Faction.OfPlayer && !pawn.Drafted)
                    {
                        PawnApparelState state = runtimeState;
                        bool managedWorkContext =
                            Patches.PausedAreaWorkFilter
                                .UsesManagedWorkPreparation(job) &&
                            (job.workGiverDef != null ||
                             job.jobGiver is JobGiver_Work ||
                             job.playerForced);
                        if (state == null && managedWorkContext)
                        {
                            state = TrackCompliantWorkSession(
                                pawn, job, RuleEvaluator.MatchingRules(pawn, job));
                        }
                        foreach (ApparelRule rule in mapPausedRules)
                        {
                            bool permittedPausedActivity =
                                Patches.PausedAreaWorkFilter.JobMayEnterPausedRule(
                                    pawn, job, rule);
                            bool preparingPermittedHaul =
                                Patches.PausedAreaWorkFilter.HasPermittedHaulingContext(
                                    state, rule);
                            if (state?.ActiveRuleId == rule.Id && !state.RecallRequested &&
                                !permittedPausedActivity && !preparingPermittedHaul)
                            {
                                RequestRulePauseRecall(state, rule);
                                handled = true;
                                break;
                            }

                            if (state?.RecallRequested != true && job.workGiverDef != null &&
                                RuleEvaluator.JobTargetsArea(job, rule.Area) &&
                                !permittedPausedActivity)
                            {
                                handled = TryJobTransition(pawn, currentTick, "paused-area work", () =>
                                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true));
                                break;
                            }
                        }
                    }

                    if (handled)
                        continue;

                    if (Patches.PausedAreaWorkFilter.ShouldRejectWanderingJob(pawn, job))
                    {
                        if (Patches.PausedAreaWorkFilter.TryMakeWanderingExitJob(pawn, out Job exitJob))
                            TryJobTransition(pawn, currentTick, "wandering exit", () =>
                                pawn.jobs.StartJob(exitJob, JobCondition.InterruptForced));
                        else
                            TryJobTransition(pawn, currentTick, "wandering restriction", () =>
                                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true));
                        continue;
                    }

                    if (Patches.PausedAreaWorkFilter.ShouldRejectProtectedAreaJob(pawn, job))
                    {
                        if (Patches.PausedAreaWorkFilter.TryMakeProtectedChildExitJob(pawn, job, out Job exitJob))
                            TryJobTransition(pawn, currentTick, "protected-child exit", () =>
                                pawn.jobs.StartJob(exitJob, JobCondition.InterruptForced));
                        else
                            TryJobTransition(pawn, currentTick, "protected-child restriction", () =>
                                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true));
                        continue;
                    }

                    if (Patches.PausedAreaWorkFilter.ShouldRejectHaulingJob(pawn, job))
                        TryJobTransition(pawn, currentTick, "hauling restriction", () =>
                            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true));
                }
            }
        }

        private bool TryReleaseSavedGearNeededForRestoration(
            Pawn pawn, int currentTick)
        {
            Job job = pawn?.jobs?.curJob;
            if (job == null || job.playerForced)
                return false;

            Pawn savedOwner = RestoringOwnerForJobTargetUnchecked(
                pawn, job, out Thing savedGear);
            if (savedOwner == null || savedGear == null)
                return false;

            bool carryingSavedGear = pawn.carryTracker?.CarriedThing == savedGear;
            RimWorld.Apparel wornSavedGear = savedGear as RimWorld.Apparel;
            bool wearingSavedGear = wornSavedGear != null &&
                pawn.apparel?.WornApparel.Contains(wornSavedGear) == true;
            bool releasedFromPossession = false;
            bool possessionReleaseFailed = false;
            bool released = TryJobTransition(
                pawn, currentTick, "saved-outfit ownership", () =>
                {
                    if (carryingSavedGear)
                    {
                        if (!pawn.carryTracker.TryDropCarriedThing(
                                pawn.Position, ThingPlaceMode.Near, out Thing dropped))
                        {
                            possessionReleaseFailed = true;
                            return;
                        }

                        releasedFromPossession = true;
                        if (dropped?.Spawned == true &&
                            dropped.IsForbidden(Faction.OfPlayer))
                        {
                            dropped.SetForbidden(false, false);
                        }
                    }
                    else if (wearingSavedGear)
                    {
                        // A managed rule item can also be another pawn's exact
                        // saved garment. Cancelling its RemoveApparel job and
                        // waiting does not make a worn item reservable, leaving
                        // both restorations in an endless Wait/rebuild loop.
                        // Release the exact instance before yielding so personal
                        // Phase 3 ownership wins over the shared locker pool.
                        if (pawn.apparel == null ||
                            !pawn.apparel.TryDrop(
                                wornSavedGear, out RimWorld.Apparel dropped,
                                pawn.Position, false))
                        {
                            possessionReleaseFailed = true;
                            return;
                        }

                        releasedFromPossession = true;
                        if (dropped?.Spawned == true &&
                            dropped.IsForbidden(Faction.OfPlayer))
                        {
                            dropped.SetForbidden(false, false);
                        }
                    }

                    // The haul may have been selected or queued before the
                    // saved owner entered restoration. Remove every native
                    // ownership record for that exact job and discard its
                    // queued continuation before yielding. Ending it with
                    // immediate reselection left the same saved garment
                    // eligible in the current think cycle, so the hauler and
                    // owner could wake each other forever.
                    ReleaseNativeReservations(pawn, job);
                    pawn.jobs.ClearQueuedJobs(false);

                    // Do not leave the contender jobless. A modded hauling or
                    // outfit optimizer can immediately select the same item again
                    // before the saved owner reaches its queued Wear step. Yield
                    // for a few seconds so the owner can rebuild and reserve the
                    // exact item without the two pawns waking each other forever.
                    Job yieldJob = Patches.PawnJobTracker_StartJob_Patch
                        .MakeSafeWaitJob(pawn, 300);
                    pawn.jobs.StartJob(
                        yieldJob, JobCondition.InterruptForced,
                        null, false, true);
                });
            if (!released || possessionReleaseFailed)
                return false;

            MakeReleasedSavedGearAvailable(savedGear, savedOwner);

            if (Prefs.DevMode)
            {
                string action = releasedFromPossession
                    ? "released"
                    : $"yielded {job.def?.defName ?? "automatic job"} targeting";
                Log.Message(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: {action} saved gear " +
                    $"{savedGear.LabelCap} so {savedOwner.LabelShortCap} can finish outfit restoration.");
            }

            return true;
        }

        private static void DetectExternalWeaponOverride(
            Pawn pawn, PawnApparelState state, Job currentJob)
        {
            if (pawn?.equipment == null || state?.WeaponInterventionActive != true ||
                state.Transition != ApparelTransition.Active ||
                state.WeaponRestorationRequested || state.WeaponPlayerOverride ||
                IsAssignedWeaponTransitionJob(state, currentJob))
            {
                return;
            }

            ThingWithComps primary = pawn.equipment.Primary;
            if (state.IsManagedWeapon(primary))
                return;

            state.MarkWeaponPlayerOverride();
            if (Prefs.DevMode)
                Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: external weapon change detected; the current choice is retained until saved-outfit restoration.");
        }

        private void EnforceSavedGearOwnership(
            Pawn pawn, bool enforceApparel, bool enforceWeapons,
            int currentTick)
        {
            if (pawn?.Map == null)
                return;

            if (enforceApparel && pawn.apparel != null)
            {
                List<RimWorld.Apparel> worn = pawn.apparel.WornApparel;
                for (int i = worn.Count - 1; i >= 0; i--)
                {
                    RimWorld.Apparel apparel = worn[i];
                    if (AutomaticOutfitManager.Storage.ManagedApparelClassifier.Matches(apparel.def) ||
                        !IsSavedForOtherPawn(apparel, pawn))
                    {
                        continue;
                    }

                    Pawn owner = SavedPawnFor(apparel);
                    if (pawn.apparel.TryDrop(
                            apparel, out RimWorld.Apparel dropped, pawn.Position, false))
                    {
                        MakeReleasedSavedGearAvailable(dropped, owner);
                    }
                }
            }

            if (enforceWeapons && pawn.equipment?.Primary is ThingWithComps primary &&
                IsSavedWeaponForOtherPawn(primary, pawn))
            {
                Pawn owner = SavedPawnForWeapon(primary);
                if (pawn.equipment.TryDropEquipment(
                        primary, out ThingWithComps dropped, pawn.Position, false))
                {
                    MakeReleasedSavedGearAvailable(dropped, owner);
                }
            }

            // A haul can finish or be replaced after the pawn has picked up an
            // exact saved weapon, leaving it in the carry tracker while the new
            // job no longer targets that item. Never extract it while any native
            // haul still owns the carry tracker: multi-haul drivers can remove
            // the current item from their target queues or use a split reference
            // before the placement toil runs. Emptying the tracker at that point
            // makes RimWorld report "not hauling anything" at the destination.
            // Once the haul ends, the end-job hook or the next component pulse
            // wakes the restoring owner and releases any genuinely stale carry.
            Thing carried = pawn.carryTracker?.CarriedThing;
            if (enforceWeapons && carried is ThingWithComps billedWeapon &&
                billedWeapon.def?.IsWeapon == true &&
                IsSavedWeaponForOtherPawn(billedWeapon, pawn) &&
                pawn.jobs?.curJob is Job billJob &&
                billJob.def == JobDefOf.DoBill && !billJob.playerForced)
            {
                Pawn owner = SavedPawnForWeapon(billedWeapon);
                bool interrupted = TryJobTransition(
                    pawn, currentTick, "saved-weapon bill ownership", () =>
                    {
                        // DoBill uses the carry tracker and a later placement
                        // toil just like hauling. Interrupt the bill before the
                        // ownership sweep releases its ingredient; removing it
                        // while the bill remains active makes RimWorld report
                        // that the pawn is no longer hauling anything.
                        ReleaseNativeReservations(pawn, billJob);
                        pawn.jobs.ClearQueuedJobs(false);
                        Job yieldJob = Patches.PawnJobTracker_StartJob_Patch
                            .MakeSafeWaitJob(pawn, 300);
                        pawn.jobs.StartJob(
                            yieldJob, JobCondition.InterruptForced,
                            null, false, true);
                    });
                if (!interrupted)
                    return;

                if (billedWeapon.Spawned)
                    MakeReleasedSavedGearAvailable(billedWeapon, owner);
                WakeRestoringSavedGearOwner(owner);
                if (Prefs.DevMode)
                {
                    Log.Message(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: interrupted " +
                        $"automatic bill carrying saved weapon {billedWeapon.LabelCap} " +
                        $"for {owner.LabelShortCap} before releasing it.");
                }

                // StartJob may drop the ingredient during the interrupted
                // driver's cleanup. Refresh the tracker before the ordinary
                // ownership release below; if it is still carried, the new
                // Wait job makes that release safe.
                carried = pawn.carryTracker?.CarriedThing;
            }
            if (enforceWeapons && carried is ThingWithComps carriedWeapon &&
                carriedWeapon.def?.IsWeapon == true &&
                IsSavedWeaponForOtherPawn(carriedWeapon, pawn) &&
                !ActiveHaulStillOwnsCarriedThing(pawn, carriedWeapon))
            {
                Pawn owner = SavedPawnForWeapon(carriedWeapon);
                if (pawn.carryTracker.TryDropCarriedThing(
                        pawn.Position, ThingPlaceMode.Near, out Thing dropped))
                {
                    MakeReleasedSavedGearAvailable(dropped, owner);
                    if (Prefs.DevMode)
                    {
                        Log.Message(
                            $"[AutomaticOutfitManager] {pawn.LabelShortCap}: released saved weapon " +
                            $"{dropped.LabelCap} from carry tracker for {owner.LabelShortCap}.");
                    }
                }
            }

            ThingOwner inventory = pawn.inventory?.innerContainer;
            if (inventory == null || inventory.Count == 0)
                return;

            for (int i = inventory.Count - 1; i >= 0; i--)
            {
                Thing item = inventory[i];
                Pawn owner = null;
                if (enforceWeapons && item is ThingWithComps weapon &&
                    weapon.def?.IsWeapon == true &&
                    IsSavedWeaponForOtherPawn(weapon, pawn))
                {
                    owner = SavedPawnForWeapon(weapon);
                }
                else if (enforceApparel && item is RimWorld.Apparel apparel &&
                         !AutomaticOutfitManager.Storage.ManagedApparelClassifier.Matches(apparel.def) &&
                         IsSavedForOtherPawn(apparel, pawn))
                {
                    owner = SavedPawnFor(apparel);
                }

                if (owner == null)
                    continue;

                if (inventory.TryDrop(
                        item, pawn.Position, pawn.Map, ThingPlaceMode.Near,
                        out Thing dropped))
                {
                    MakeReleasedSavedGearAvailable(dropped, owner);
                    if (Prefs.DevMode)
                    {
                        Log.Message(
                            $"[AutomaticOutfitManager] {pawn.LabelShortCap}: released saved gear " +
                            $"{dropped.LabelCap} from inventory for {owner.LabelShortCap}.");
                    }
                }
            }
        }

        private static bool ActiveHaulStillOwnsCarriedThing(
            Pawn pawn, Thing carriedThing)
        {
            Job job = pawn?.jobs?.curJob;
            return carriedThing != null &&
                   pawn?.carryTracker?.CarriedThing == carriedThing &&
                   (job?.def == JobDefOf.HaulToCell ||
                    job?.def == JobDefOf.HaulToContainer);
        }

        internal void NotifySavedWeaponHaulReleased(Pawn hauler, Job endingJob)
        {
            if (hauler == null ||
                (endingJob?.def != JobDefOf.HaulToCell &&
                 endingJob?.def != JobDefOf.HaulToContainer) ||
                endingJob.targetA.Thing is not ThingWithComps weapon ||
                weapon.def?.IsWeapon != true || weapon.Spawned != true ||
                !IsSavedWeaponForOtherPawn(weapon, hauler))
            {
                return;
            }

            Pawn owner = SavedPawnForWeapon(weapon);
            MakeReleasedSavedGearAvailable(weapon, owner);
            if (Prefs.DevMode)
            {
                Log.Message(
                    $"[AutomaticOutfitManager] {hauler.LabelShortCap}: completed " +
                    $"hauling saved weapon {weapon.LabelCap}; waking " +
                    $"{owner.LabelShortCap} to resume outfit restoration.");
            }
        }

        private void MakeReleasedSavedGearAvailable(Thing gear, Pawn owner)
        {
            if (gear?.Spawned == true && gear.IsForbidden(Faction.OfPlayer))
                gear.SetForbidden(false, false);
            WakeRestoringSavedGearOwner(owner);
        }

        internal void WakeRestoringSavedGearOwner(Pawn owner)
        {
            PawnApparelState ownerState = StateFor(owner);
            if (ownerState?.Transition != ApparelTransition.Restoring)
                return;

            // The unavailable-item cooldown exists to contain failed transition
            // loops. This exact item has just become available, so rebuild the
            // owner's restoration immediately instead of waiting ten seconds.
            ownerState.LastRestorationAttemptTick = -1;
            ownerState.ActiveIdleTicks = 240;

            // Availability is a real progress signal. The bounded recovery
            // backoff only applies while nothing changes; retaining it here can
            // make the owner sleep through the brief window after a competing
            // haul releases the item, allowing that haul to win again.
            restorationProgress.Remove(owner);
            restorationRecoveryBackoff.Remove(owner);
        }

        internal void NotifySuccessfulRestorationStep(
            Pawn pawn, PawnApparelState state)
        {
            if (pawn == null || state?.Transition != ApparelTransition.Restoring)
                return;

            // A completed exact Phase 3 job is material progress. Any retry
            // window or exponential watchdog backoff belongs to the state before
            // that change and must not delay the next queued restoration step.
            state.LastRestorationAttemptTick = -1;
            state.UnavailableRestorationAttempts = 0;
            state.ActiveIdleTicks = 0;
            restorationProgress.Remove(pawn);
            restorationRecoveryBackoff.Remove(pawn);
        }

        private bool TryJobTransition(Pawn pawn, int currentTick, string context, System.Action transition)
        {
            if (pawn == null || transition == null)
                return false;
            if (jobTransitionFailureTicks.TryGetValue(pawn, out int failedTick) &&
                currentTick - failedTick < 300)
                return false;

            try
            {
                transition();
                jobTransitionFailureTicks.Remove(pawn);
                return true;
            }
            catch (System.Exception exception)
            {
                jobTransitionFailureTicks[pawn] = currentTick;
                Log.Warning($"[AutomaticOutfitManager] {pawn.LabelShortCap}: {context} job transition failed; retrying later. {exception.GetType().Name}: {exception.Message}");
                return false;
            }
        }

        private void RecoverIdleApparelWorkers(int currentTick)
        {
            foreach (PawnApparelState state in PawnStates.ToList())
            {
                Pawn pawn = state?.Pawn;
                if (TryCompleteSatisfiedRestoration(pawn, state))
                    continue;

                ApparelRule rule = RuleById(state?.ActiveRuleId);
                if (pawn?.Downed == true)
                {
                    bool transitionOwnedQueue =
                        state.Transition == ApparelTransition.Preparing ||
                        state.Transition == ApparelTransition.ReturningToChangingArea ||
                        state.Transition == ApparelTransition.Restoring;
                    bool firstSuspension = !state.DownedTransitionSuspended;
                    SuspendTransitionWhileDowned(state);
                    if (firstSuspension && transitionOwnedQueue)
                        pawn.jobs?.ClearQueuedJobs(false);
                    continue;
                }

                if (state?.DownedTransitionSuspended == true)
                {
                    // The snapshot survived the medical emergency. Resume once
                    // only when the pawn is back on the map and able to accept a
                    // normal restoration job; drafting and caravans keep it
                    // safely suspended until those native states end.
                    if (pawn?.Spawned != true || pawn.Drafted || pawn.jobs == null)
                        continue;

                    state.DownedTransitionSuspended = false;
                    state.ActiveIdleTicks = 0;
                    state.LastRestorationAttemptTick = -1;
                    restorationProgress.Remove(pawn);
                    bool recoveryStarted = StartRestorationRecovery(
                        pawn, state, currentTick, "post-medical restoration recovery");
                    if (recoveryStarted && StateFor(pawn) != null && Prefs.DevMode)
                    {
                        Log.Message(
                            $"[AutomaticOutfitManager] {pawn.LabelShortCap}: pawn can move again; " +
                            "resuming saved-outfit restoration.");
                    }
                    continue;
                }

                if (Patches.PawnJobTracker_StartJob_Patch
                    .IsNativeEmergencySafetyJob(pawn?.jobs?.curJob))
                {
                    state.ActiveIdleTicks = 0;
                    if (pawn != null)
                    {
                        restorationProgress.Remove(pawn);
                        activeWorkProgress.Remove(pawn);
                    }
                    continue;
                }

                Job environmentalTransitionJob = pawn?.jobs?.curJob;
                if (pawn?.Spawned == true &&
                    environmentalTransitionJob?.def == JobDefOf.RemoveApparel &&
                    environmentalTransitionJob.targetA.Thing is RimWorld.Apparel
                        environmentalApparel &&
                    state.ManagedApparel?.Contains(environmentalApparel) == true &&
                    Patches.HazardousEnvironmentSafety.RemovalWouldExposePawn(
                        pawn, state, environmentalApparel,
                        out string environmentalReason))
                {
                    pawn.jobs.ClearQueuedJobs(false);
                    RetainManagedProtectionForHazard(pawn, state);
                    bool interrupted = TryJobTransition(
                        pawn, currentTick, "hazardous apparel restoration", () =>
                            pawn.jobs.EndCurrentJob(
                                JobCondition.InterruptForced, true));
                    if (interrupted && Prefs.DevMode &&
                        Patches.PawnJobTracker_StartJob_Patch
                            .ShouldLogRepeatedDiagnostic(
                                pawn, "environmental-removal-interrupt"))
                    {
                        Log.Message(
                            $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                            $"environment changed to {environmentalReason}; " +
                            "interrupted managed-apparel removal and retained protection.");
                    }
                    continue;
                }

                if (state?.Transition != ApparelTransition.Restoring)
                {
                    if (pawn != null)
                    {
                        restorationProgress.Remove(pawn);
                        restorationRecoveryBackoff.Remove(pawn);
                        rejectedManagedGearWakeTicks.Remove(pawn);
                    }
                }
                if (state?.Transition != ApparelTransition.Active && pawn != null)
                    activeWorkProgress.Remove(pawn);
                if (pawn?.Spawned == true && !pawn.Drafted &&
                    state.Transition == ApparelTransition.Preparing)
                {
                    Job preparationJob = pawn.jobs?.curJob;
                    bool assignedTransition =
                        IsAssignedApparelTransitionJob(state, preparationJob) ||
                        IsAssignedWeaponTransitionJob(state, preparationJob);
                    bool pendingWorkRunning =
                        preparationJob != null && state.PendingWorkJob != null &&
                        (ReferenceEquals(preparationJob, state.PendingWorkJob) ||
                         preparationJob.loadID == state.PendingWorkJob.loadID);

                    // Native Wear reports Standing throughout its timed toil, and
                    // Equip can path or wait on a reservation before completing.
                    // Those exact AOM-owned steps are progress, not an idle
                    // preparation. Player orders and a successfully resumed
                    // pending job remain authoritative as well.
                    if (assignedTransition || pendingWorkRunning ||
                        preparationJob?.playerForced == true ||
                        !IsIdleRecoveryJob(pawn, preparationJob))
                    {
                        state.ActiveIdleTicks = 0;
                        continue;
                    }

                    int apparelRetryElapsed = state.LastApparelPreparationAttemptTick < 0
                        ? PreparationRetryIntervalTicks
                        : currentTick - state.LastApparelPreparationAttemptTick;
                    int weaponRetryElapsed = state.LastWeaponPreparationAttemptTick < 0
                        ? PreparationRetryIntervalTicks
                        : currentTick - state.LastWeaponPreparationAttemptTick;
                    if (apparelRetryElapsed < PreparationRetryIntervalTicks ||
                        weaponRetryElapsed < PreparationRetryIntervalTicks)
                    {
                        // The StartJob boundary deliberately owns this bounded
                        // wait after a failed Wear/Equip. Do not turn that normal
                        // retry delay into a second competing preparation plan.
                        state.ActiveIdleTicks = 0;
                        continue;
                    }

                    state.ActiveIdleTicks += 30;
                    if (state.ActiveIdleTicks < PreparingIdleGraceTicks)
                        continue;

                    state.ActiveIdleTicks = 0;
                    bool preparationRecovered =
                        Patches.PawnJobTracker_StartJob_Patch
                            .TryRecoverIdlePreparation(
                                pawn, state, out string recoveryDescription);
                    if (preparationRecovered && StateFor(pawn) != null &&
                        Prefs.DevMode)
                    {
                        Log.Message(
                            $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                            $"preparation became idle; {recoveryDescription}.");
                    }
                    continue;
                }
                if (pawn?.Spawned == true && !pawn.Drafted &&
                    state.Transition == ApparelTransition.ReturningToChangingArea)
                {
                    Job returnJob = pawn.jobs?.curJob;
                    if (!state.ChangingAreaReturnCell.IsValid &&
                        Patches.PawnJobTracker_StartJob_Patch
                            .IsChangingAreaCellTravelJob(returnJob) &&
                        returnJob.targetA.Cell.IsValid)
                    {
                        // Migrate an in-flight return from an older RC save. The
                        // former state did not persist its AOM-owned destination.
                        state.ChangingAreaReturnCell = returnJob.targetA.Cell;
                    }
                    bool assignedReturnTravel =
                        Patches.PawnJobTracker_StartJob_Patch
                            .IsAssignedChangingAreaReturnJob(state, returnJob);
                    bool activePortalReturn =
                        Patches.PawnJobTracker_StartJob_Patch
                            .IsAssignedCrossMapChangingAreaReturnJob(
                                state, returnJob);
                    bool activeReturnTravel = assignedReturnTravel &&
                        pawn.pather?.Moving == true;
                    bool activeNaturalLockerDwell =
                        state.NaturalLockerDwellUntilTick > currentTick;
                    if (activePortalReturn || activeReturnTravel ||
                        activeNaturalLockerDwell ||
                        returnJob?.playerForced == true)
                    {
                        state.ActiveIdleTicks = 0;
                        continue;
                    }

                    bool reachedRecordedDestination =
                        state.ChangingAreaReturnCell.IsValid &&
                        pawn.Position == state.ChangingAreaReturnCell;
                    state.ActiveIdleTicks = reachedRecordedDestination
                        ? 120
                        : state.ActiveIdleTicks + 30;
                    if (state.ActiveIdleTicks < 120)
                        continue;

                    // RimWorld or a compatibility job can end the locker Goto
                    // without starting another job, leaving the pawn visibly
                    // Standing while the transition remains Returning forever.
                    // Re-enter StartJob through a bounded trigger so the shared
                    // transition logic either rebuilds the locker route or begins
                    // exact restoration when the pawn is already inside.
                    state.ActiveIdleTicks = 0;
                    bool recoveryStarted = StartChangingAreaReturnRecovery(
                        pawn, state, rule, currentTick);
                    if (recoveryStarted && StateFor(pawn) != null && Prefs.DevMode)
                    {
                        Log.Message(
                            $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                            "locker return became idle; rebuilding the return or restoration path.");
                    }
                    continue;
                }
                if (pawn?.Spawned == true && !pawn.Drafted &&
                    state.Transition == ApparelTransition.Restoring)
                {
                    Job restorationJob = pawn.jobs?.curJob;
                    if (!IsIdleRecoveryJob(pawn, restorationJob))
                    {
                        state.ActiveIdleTicks = 0;

                        // A queued Wear or another compatibility job can remain
                        // technically active even though the pawn is visibly
                        // Standing. The wait-family recovery below cannot see
                        // that state. Track real job/toil/target/equipment
                        // progress and rebuild only after every signal has been
                        // unchanged for ten in-game seconds.
                        if (!RestorationHasStalled(pawn, state, currentTick))
                            continue;

                        RestorationPlanner.TryMakeHeldOriginalsAccessible(pawn, state);
                        List<Job> stalledRemainingJobs = RestorationPlanner.BuildJobs(
                            pawn, state, rule, out bool stalledHasUnavailableApparel);
                        bool releasedBlockedWeapon =
                            TryReleasePersistentlyUnavailableSavedWeapon(pawn, state);
                        bool releasedRejectedWeapon =
                            TryReleasePersistentlyRejectedSavedWeapon(pawn, state);
                        if (releasedBlockedWeapon || releasedRejectedWeapon)
                        {
                            stalledRemainingJobs = RestorationPlanner.BuildJobs(
                                pawn, state, rule, out stalledHasUnavailableApparel);
                        }
                        if (stalledRemainingJobs.Count == 0 &&
                            stalledHasUnavailableApparel &&
                            TryCompleteForeignMapDepartureWithUnavailableSavedGear(
                                pawn, state))
                        {
                            continue;
                        }
                        if (stalledRemainingJobs.Count == 0 &&
                            !stalledHasUnavailableApparel)
                        {
                            restorationProgress.Remove(pawn);
                            state.ActiveIdleTicks = 0;
                            EndIntervention(pawn);
                            continue;
                        }

                        string stalledJobDescription =
                            DescribeRestorationProgress(pawn, restorationJob);
                        restorationProgress.Remove(pawn);
                        bool stalledRecoveryStarted = StartRestorationRecovery(
                            pawn, state, currentTick, "stalled restoration recovery");
                        if (stalledRecoveryStarted && StateFor(pawn) != null &&
                            Prefs.DevMode)
                        {
                            Log.Message(
                                $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                                "restoration stopped making progress; rebuilding " +
                                $"saved-apparel and weapon jobs ({stalledJobDescription}; " +
                                $"{stalledRemainingJobs.Count} remaining, " +
                                $"unavailable={stalledHasUnavailableApparel}).");
                        }
                        continue;
                    }

                    restorationProgress.Remove(pawn);

                    RestorationPlanner.TryMakeHeldOriginalsAccessible(pawn, state);
                    List<Job> remainingJobs = RestorationPlanner.BuildJobs(
                        pawn, state, rule, out bool hasUnavailableSavedApparel);
                    if (remainingJobs.Count == 0 && !hasUnavailableSavedApparel)
                    {
                        state.ActiveIdleTicks = 0;
                        EndIntervention(pawn);
                        continue;
                    }
                    if (remainingJobs.Count == 0 &&
                        hasUnavailableSavedApparel &&
                        TryCompleteForeignMapDepartureWithUnavailableSavedGear(
                            pawn, state))
                    {
                        continue;
                    }

                    state.ActiveIdleTicks += 30;
                    int idleGrace = hasUnavailableSavedApparel ? 240 : 120;
                    int retryCooldown = hasUnavailableSavedApparel
                        ? state.MapDepartureRequested ? 600 : 2400
                        : 120;
                    bool retryReady = state.LastRestorationAttemptTick < 0 ||
                        currentTick - state.LastRestorationAttemptTick >= retryCooldown;
                    if (state.ActiveIdleTicks < idleGrace || !retryReady)
                        continue;

                    state.ActiveIdleTicks = 0;
                    bool releasedUnavailableWeapon =
                        TryReleasePersistentlyUnavailableSavedWeapon(pawn, state);
                    bool releasedFailedWeapon =
                        TryReleasePersistentlyRejectedSavedWeapon(pawn, state);
                    if (releasedUnavailableWeapon || releasedFailedWeapon)
                    {
                        remainingJobs = RestorationPlanner.BuildJobs(
                            pawn, state, rule, out hasUnavailableSavedApparel);
                        if (remainingJobs.Count == 0 && !hasUnavailableSavedApparel)
                        {
                            EndIntervention(pawn);
                            continue;
                        }
                    }
                    if (remainingJobs.Count == 0 &&
                        hasUnavailableSavedApparel &&
                        TryCompleteForeignMapDepartureWithUnavailableSavedGear(
                            pawn, state))
                    {
                        continue;
                    }
                    string idleJobDescription = DescribeRestorationProgress(
                        pawn, restorationJob);
                    bool recoveryStarted = StartRestorationRecovery(
                        pawn, state, currentTick, "idle restoration recovery");
                    if (recoveryStarted && StateFor(pawn) != null && Prefs.DevMode)
                    {
                        Log.Message(
                            $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                            "restoration became idle; rebuilding saved-apparel " +
                            $"and weapon jobs ({idleJobDescription}).");
                    }
                    continue;
                }

                // Older RC saves can contain a post-draft return whose buffer
                // already completed on a pocket map. That state has Recall set
                // but no pending interrupt, so waiting for another native job to
                // start can leave a reading or wandering pawn there indefinitely.
                // Recover it proactively, while forced orders and emergency jobs
                // remain entirely under native/player control.
                if (pawn?.Spawned == true && !pawn.Drafted &&
                    state.Transition == ApparelTransition.Active &&
                    state.RecallRequested &&
                    pawn.jobs?.curJob?.playerForced != true &&
                    StartCrossMapChangingAreaReturn(
                        pawn, state, rule, currentTick,
                        "pending cross-map locker return"))
                {
                    continue;
                }

                if (pawn?.Spawned != true || pawn.Drafted ||
                    state.Transition != ApparelTransition.Active ||
                    rule?.Enabled != true || rule.WorkAreaPaused ||
                    state.RecallRequested)
                {
                    if (state != null)
                        state.ActiveIdleTicks = 0;
                    continue;
                }

                // Occupancy remains the safety boundary while a pawn is doing
                // real work, sleeping, wandering, hosted, in custody, or exposed
                // to a hazard. A player worker whose native Wait has remained
                // idle through the full buffer grace on a safe map can still
                // return through the locker; otherwise an active-area cell can
                // suppress the idle watchdog forever and leave the pawn Standing.
                List<ApparelRule> occupiedLocationRules =
                    RuleEvaluator.MatchingLocationRules(pawn);
                if (occupiedLocationRules.Count > 0)
                {
                    activeWorkProgress.Remove(pawn);

                    Job occupiedJob = pawn.jobs?.curJob;
                    bool playerWorker =
                        (pawn.IsColonist || pawn.IsSlave) &&
                        !PawnAccessClassifier.IsHostedGuest(pawn) &&
                        !PawnAccessClassifier.IsColonyPrisoner(pawn);
                    bool safeIdle = playerWorker &&
                        IsIdleRecoveryJob(pawn, occupiedJob) &&
                        !Patches.HazardousEnvironmentSafety
                            .MustRetainManagedProtectionAt(
                                pawn, state, pawn.Position, out _);
                    if (!safeIdle)
                    {
                        state.ActiveIdleTicks = 0;
                        continue;
                    }

                    state.ActiveIdleTicks += 30;
                    int occupiedIdleGrace = HasIncompleteTaskBuffer(state, rule)
                        ? IncompleteTaskBufferIdleGraceTicks
                        : ActiveIdleGraceTicks;
                    if (state.ActiveIdleTicks < occupiedIdleGrace)
                        continue;

                    state.ActiveIdleTicks = 0;
                    RequestRecall(state);
                    if (Prefs.DevMode)
                    {
                        Log.Message(
                            $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                            "remained idle inside a safe active work area; " +
                            "returning through the locker room.");
                    }
                    continue;
                }

                Job job = pawn.jobs?.curJob;
                bool haulingJob = job?.def == JobDefOf.HaulToCell ||
                    job?.def == JobDefOf.HaulToContainer;
                bool managedWorkJob = !haulingJob &&
                    IsActiveManagedWorkJob(state, job);
                // Hauling and ordinary work toils can both report "Standing"
                // while their drivers are actively advancing. Never feed real
                // work through the generic visible-job heuristic; use concrete
                // job, toil, timer, movement, target, queue, and gear progress.
                bool idleRecoveryJob = !haulingJob && !managedWorkJob &&
                    IsIdleRecoveryJob(pawn, job);
                bool stalledHaul = haulingJob &&
                    ActiveHaulHasStalled(pawn, state, currentTick);
                bool stalledManagedWork = managedWorkJob &&
                    ActiveManagedWorkHasStalled(pawn, state, currentTick);
                bool stalledActiveWork = stalledHaul || stalledManagedWork;

                if (!idleRecoveryJob && !stalledActiveWork)
                {
                    state.ActiveIdleTicks = 0;
                    continue;
                }

                if (!stalledActiveWork)
                {
                    state.ActiveIdleTicks += 30;
                    int idleGrace = HasIncompleteTaskBuffer(state, rule)
                        ? IncompleteTaskBufferIdleGraceTicks
                        : ActiveIdleGraceTicks;
                    if (state.ActiveIdleTicks < idleGrace)
                        continue;
                }

                // A true Wait/Standing job gets a short grace period. Real work
                // receives the longer progress-aware timeout above and is never
                // recalled merely because its visible report says "Standing".
                state.ActiveIdleTicks = 0;
                activeWorkProgress.Remove(pawn);
                RequestRecall(state);
                if (Prefs.DevMode)
                {
                    string reason = stalledActiveWork
                        ? $"{job?.def?.defName ?? "managed work"} stopped making progress"
                        : "finished work and became idle";
                    Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: {reason}; returning to locker room.");
                }
            }
        }

        private bool ActiveHaulHasStalled(
            Pawn pawn, PawnApparelState state, int currentTick) =>
            ActiveJobHasStalled(pawn, state, currentTick, true);

        private bool ActiveManagedWorkHasStalled(
            Pawn pawn, PawnApparelState state, int currentTick) =>
            ActiveJobHasStalled(pawn, state, currentTick, false);

        private static bool IsActiveManagedWorkJob(
            PawnApparelState state, Job job)
        {
            if (job?.def == null)
                return false;

            return job.workGiverDef != null ||
                   job.jobGiver is JobGiver_Work ||
                   (!string.IsNullOrEmpty(state?.LastManagedWorkJobDefName) &&
                    string.Equals(
                        job.def.defName, state.LastManagedWorkJobDefName,
                        System.StringComparison.Ordinal));
        }

        private bool HasIncompleteTaskBuffer(
            PawnApparelState state, ApparelRule activeRule)
        {
            if (state == null)
                return false;

            if (activeRule?.Enabled == true &&
                activeRule.ReturnTaskBuffer > state.BufferedTasksCompleted)
            {
                return true;
            }

            return state.NestedRuleBuffers?.Any(progress =>
            {
                if (progress == null || progress.Finished)
                    return false;

                ApparelRule nestedRule = RuleById(progress.RuleId);
                return nestedRule?.Enabled == true &&
                       nestedRule.ReturnTaskBuffer > progress.Completed;
            }) == true;
        }

        private bool ActiveJobHasStalled(
            Pawn pawn, PawnApparelState state, int currentTick, bool hauling)
        {
            Job job = pawn?.jobs?.curJob;
            bool trackedJob = hauling
                ? job != null &&
                  (job.def == JobDefOf.HaulToCell ||
                   job.def == JobDefOf.HaulToContainer) &&
                  pawn.carryTracker?.CarriedThing == null
                : IsActiveManagedWorkJob(state, job);
            if (!trackedJob || pawn?.pather?.Moving == true)
            {
                if (pawn != null)
                    activeWorkProgress.Remove(pawn);
                return false;
            }

            JobDriver driver = pawn.jobs.curDriver;
            Thing target = job.targetA.Thing;
            int jobLoadId = job.loadID;
            int toilIndex = driver?.CurToilIndex ?? -1;
            int ticksLeftThisToil = driver?.ticksLeftThisToil ?? -1;
            int queueCount = pawn.jobs.jobQueue?.Count ?? 0;
            IntVec3 targetPosition = target?.PositionHeld ??
                (job.targetA.IsValid ? job.targetA.Cell : IntVec3.Invalid);
            int wornOriginalCount = CountWornApparel(
                pawn, state?.OriginalApparel);
            int wornManagedCount = CountWornApparel(
                pawn, state?.ManagedApparel);
            int primaryThingId = pawn.equipment?.Primary?.thingIDNumber ?? -1;
            int carriedThingId = pawn.carryTracker?.CarriedThing?.thingIDNumber ?? -1;

            if (!activeWorkProgress.TryGetValue(
                    pawn, out RestorationProgress progress))
            {
                progress = new RestorationProgress();
                activeWorkProgress[pawn] = progress;
                CaptureRestorationProgress(
                    progress, currentTick, jobLoadId, job.def, toilIndex,
                    ticksLeftThisToil, queueCount, pawn.Position, target,
                    targetPosition, wornOriginalCount, wornManagedCount,
                    primaryThingId, carriedThingId);
                return false;
            }

            // Work drivers such as Deconstruct use a negative running counter
            // rather than a positive countdown. Any timer change proves that
            // the toil is executing; requiring non-negative values caused AOM
            // to interrupt healthy native work as visibly idle.
            bool toilTimerAdvanced =
                ticksLeftThisToil != progress.TicksLeftThisToil;
            bool changed = progress.JobLoadId != jobLoadId ||
                progress.JobDef != job.def ||
                progress.ToilIndex != toilIndex ||
                toilTimerAdvanced ||
                progress.QueueCount != queueCount ||
                progress.PawnPosition != pawn.Position ||
                progress.TargetThingId != (target?.thingIDNumber ?? -1) ||
                progress.TargetPosition != targetPosition ||
                progress.TargetSpawned != (target?.Spawned == true) ||
                progress.WornOriginalCount != wornOriginalCount ||
                progress.WornManagedCount != wornManagedCount ||
                progress.PrimaryThingId != primaryThingId ||
                progress.CarriedThingId != carriedThingId;

            if (changed)
            {
                restorationRecoveryBackoff.Remove(pawn);
                CaptureRestorationProgress(
                    progress, currentTick, jobLoadId, job.def, toilIndex,
                    ticksLeftThisToil, queueCount, pawn.Position, target,
                    targetPosition, wornOriginalCount, wornManagedCount,
                    primaryThingId, carriedThingId);
                return false;
            }

            return currentTick - progress.LastProgressTick >=
                   ActiveWorkNoProgressTimeoutTicks;
        }

        private bool RestorationHasStalled(
            Pawn pawn, PawnApparelState state, int currentTick)
        {
            Job job = pawn?.jobs?.curJob;
            bool assignedTransitionJob =
                IsAssignedApparelTransitionJob(state, job) ||
                IsAssignedWeaponTransitionJob(state, job);
            if (pawn == null || state == null || job == null ||
                (job.playerForced && !assignedTransitionJob) ||
                pawn.pather?.Moving == true)
            {
                if (pawn != null)
                    restorationProgress.Remove(pawn);
                return false;
            }

            JobDriver driver = pawn.jobs.curDriver;
            Thing target = job.targetA.Thing;
            int jobLoadId = job.loadID;
            int toilIndex = driver?.CurToilIndex ?? -1;
            int ticksLeftThisToil = driver?.ticksLeftThisToil ?? -1;
            int queueCount = pawn.jobs.jobQueue?.Count ?? 0;
            IntVec3 targetPosition = target?.PositionHeld ??
                (job.targetA.IsValid ? job.targetA.Cell : IntVec3.Invalid);
            int wornOriginalCount = CountWornApparel(
                pawn, state.OriginalApparel);
            int wornManagedCount = CountWornApparel(
                pawn, state.ManagedApparel);
            int primaryThingId = pawn.equipment?.Primary?.thingIDNumber ?? -1;
            int carriedThingId = pawn.carryTracker?.CarriedThing?.thingIDNumber ?? -1;

            if (!restorationProgress.TryGetValue(
                    pawn, out RestorationProgress progress))
            {
                progress = new RestorationProgress();
                restorationProgress[pawn] = progress;
                CaptureRestorationProgress(
                    progress, currentTick, jobLoadId, job.def, toilIndex,
                    ticksLeftThisToil, queueCount, pawn.Position, target,
                    targetPosition, wornOriginalCount, wornManagedCount,
                    primaryThingId, carriedThingId);
                return false;
            }

            bool toilTimerAdvanced = ticksLeftThisToil >= 0 &&
                progress.TicksLeftThisToil >= 0 &&
                ticksLeftThisToil != progress.TicksLeftThisToil;
            bool changed = progress.JobLoadId != jobLoadId ||
                progress.JobDef != job.def ||
                progress.ToilIndex != toilIndex ||
                toilTimerAdvanced ||
                progress.QueueCount != queueCount ||
                progress.PawnPosition != pawn.Position ||
                progress.TargetThingId != (target?.thingIDNumber ?? -1) ||
                progress.TargetPosition != targetPosition ||
                progress.TargetSpawned != (target?.Spawned == true) ||
                progress.WornOriginalCount != wornOriginalCount ||
                progress.WornManagedCount != wornManagedCount ||
                progress.PrimaryThingId != primaryThingId ||
                progress.CarriedThingId != carriedThingId;

            if (changed)
            {
                CaptureRestorationProgress(
                    progress, currentTick, jobLoadId, job.def, toilIndex,
                    ticksLeftThisToil, queueCount, pawn.Position, target,
                    targetPosition, wornOriginalCount, wornManagedCount,
                    primaryThingId, carriedThingId);
                return false;
            }

            return currentTick - progress.LastProgressTick >=
                   RestorationNoProgressTimeoutTicks;
        }

        private static void CaptureRestorationProgress(
            RestorationProgress progress,
            int currentTick,
            int jobLoadId,
            JobDef jobDef,
            int toilIndex,
            int ticksLeftThisToil,
            int queueCount,
            IntVec3 pawnPosition,
            Thing target,
            IntVec3 targetPosition,
            int wornOriginalCount,
            int wornManagedCount,
            int primaryThingId,
            int carriedThingId)
        {
            progress.LastProgressTick = currentTick;
            progress.JobLoadId = jobLoadId;
            progress.JobDef = jobDef;
            progress.ToilIndex = toilIndex;
            progress.TicksLeftThisToil = ticksLeftThisToil;
            progress.QueueCount = queueCount;
            progress.PawnPosition = pawnPosition;
            progress.TargetThingId = target?.thingIDNumber ?? -1;
            progress.TargetPosition = targetPosition;
            progress.TargetSpawned = target?.Spawned == true;
            progress.WornOriginalCount = wornOriginalCount;
            progress.WornManagedCount = wornManagedCount;
            progress.PrimaryThingId = primaryThingId;
            progress.CarriedThingId = carriedThingId;
        }

        private static int CountWornApparel(
            Pawn pawn, List<RimWorld.Apparel> apparel)
        {
            if (pawn?.apparel == null || apparel == null || apparel.Count == 0)
                return 0;

            int count = 0;
            List<RimWorld.Apparel> worn = pawn.apparel.WornApparel;
            for (int i = 0; i < apparel.Count; i++)
            {
                if (apparel[i] != null && worn.Contains(apparel[i]))
                    count++;
            }
            return count;
        }

        private static string DescribeRestorationProgress(Pawn pawn, Job job)
        {
            JobDriver driver = pawn?.jobs?.curDriver;
            Thing target = job?.targetA.Thing;
            string targetDescription = target != null
                ? $"{target.LabelCap} at {target.PositionHeld}"
                : job?.targetA.IsValid == true
                    ? job.targetA.Cell.ToString()
                    : "no target";
            return $"job {job?.def?.defName ?? "none"}, " +
                   $"toil {driver?.CurToilIndex ?? -1}, " +
                   $"ticks left {driver?.ticksLeftThisToil ?? -1}, " +
                   $"target {targetDescription}, " +
                   $"queue {pawn?.jobs?.jobQueue?.Count ?? 0}";
        }

        private bool StartRestorationRecovery(
            Pawn pawn,
            PawnApparelState state,
            int currentTick,
            string context)
        {
            if (!RestorationRecoveryReady(pawn, currentTick))
                return false;

            bool started = TryJobTransition(pawn, currentTick, context, () =>
            {
                // A same-cell Goto is a harmless non-Wait trigger that the
                // StartJob patch replaces with a freshly validated restoration
                // queue before it can execute.
                state.LastRestorationAttemptTick = -1;
                Job recoveryTrigger = JobMaker.MakeJob(JobDefOf.Goto, pawn.Position);
                recoveryTrigger.expiryInterval = 30;
                pawn.jobs.StartJob(
                    recoveryTrigger, JobCondition.InterruptForced, null, false, true);
            });
            if (started)
                RecordRestorationRecovery(pawn, currentTick);
            return started;
        }

        private bool RestorationRecoveryReady(Pawn pawn, int currentTick)
        {
            if (pawn == null ||
                !restorationRecoveryBackoff.TryGetValue(
                    pawn, out RestorationRecoveryBackoff backoff))
            {
                return true;
            }

            int elapsed = currentTick - backoff.LastAttemptTick;
            if (elapsed >= 2400)
            {
                restorationRecoveryBackoff.Remove(pawn);
                return true;
            }

            int shift = System.Math.Min(backoff.ConsecutiveAttempts, 3);
            int cooldown = System.Math.Min(1200, 120 << shift);
            return elapsed >= cooldown;
        }

        private void RecordRestorationRecovery(Pawn pawn, int currentTick)
        {
            if (!restorationRecoveryBackoff.TryGetValue(
                    pawn, out RestorationRecoveryBackoff backoff))
            {
                backoff = new RestorationRecoveryBackoff();
                restorationRecoveryBackoff[pawn] = backoff;
            }

            if (currentTick - backoff.LastAttemptTick >= 2400)
                backoff.ConsecutiveAttempts = 0;
            backoff.ConsecutiveAttempts++;
            backoff.LastAttemptTick = currentTick;
        }

        private bool StartChangingAreaReturnRecovery(
            Pawn pawn,
            PawnApparelState state,
            ApparelRule rule,
            int currentTick)
        {
            bool insideProtectedArea =
                Patches.PawnJobTracker_StartJob_Patch
                    .PawnInsideStateProtectedArea(pawn, this, state);
            Area changingArea = rule?.ChangingArea;
            if (StartCrossMapChangingAreaReturn(
                    pawn, state, rule, currentTick,
                    "cross-map locker-return travel"))
                return true;

            bool outsidePreferredChangingArea = changingArea?.Map == pawn?.Map &&
                !changingArea[pawn.Position];
            if ((insideProtectedArea || outsidePreferredChangingArea) &&
                Patches.PawnJobTracker_StartJob_Patch.TryFindRestorationCell(
                    pawn, this, state, out IntVec3 changingCell))
            {
                return TryJobTransition(pawn, currentTick, "idle locker-return travel", () =>
                {
                    state.LastChangingAreaReturnAttemptTick = currentTick;
                    state.ChangingAreaReturnCell = changingCell;
                    Job returnJob =
                        Patches.PawnJobTracker_StartJob_Patch
                            .MakeChangingAreaTravelJob(changingCell);
                    returnJob.expiryInterval = 2000;
                    returnJob.locomotionUrgency = LocomotionUrgency.Jog;
                    pawn.jobs.StartJob(
                        returnJob, JobCondition.InterruptForced, null, false, true);
                });
            }

            if (insideProtectedArea)
            {
                // Keep the managed outfit intact until a reachable exterior
                // cell exists. Return the state to Active so the next native
                // selection retries the shared safe-exit path.
                state.Transition = ApparelTransition.Active;
                state.ChangingAreaReturnCell = IntVec3.Invalid;
                state.ActiveIdleTicks = 0;
                return TryJobTransition(pawn, currentTick, "safe-area-exit retry", () =>
                {
                    Job wait = JobMaker.MakeJob(JobDefOf.Wait);
                    wait.expiryInterval = 300;
                    pawn.jobs.StartJob(
                        wait, JobCondition.InterruptForced, null, false, true);
                });
            }

            // The pawn has cleared every protected area. If the locker is
            // absent or currently unreachable, restoring here is safe and
            // avoids a repeated Returning/Standing loop.
            state.Transition = ApparelTransition.Restoring;
            state.ChangingAreaReturnCell = IntVec3.Invalid;
            state.LastRestorationAttemptTick = -1;
            return StartRestorationRecovery(
                pawn, state, currentTick, "idle locker-return restoration");
        }

        private bool StartCrossMapChangingAreaReturn(
            Pawn pawn,
            PawnApparelState state,
            ApparelRule rule,
            int currentTick,
            string context)
        {
            Map lockerMap = rule?.ChangingArea?.Map;
            if (pawn?.Map == null || state == null || lockerMap == null ||
                lockerMap == pawn.Map || pawn.jobs == null ||
                !Patches.PawnJobTracker_StartJob_Patch
                    .TryMakeCrossMapChangingAreaReturnJob(
                        pawn, lockerMap,
                        out Job portalReturnJob, out MapPortal returnPortal))
            {
                return false;
            }

            return TryJobTransition(pawn, currentTick, context, () =>
            {
                state.Transition = ApparelTransition.ReturningToChangingArea;
                state.LastChangingAreaReturnAttemptTick = currentTick;
                state.ChangingAreaReturnCell = IntVec3.Invalid;
                pawn.jobs.ClearQueuedJobs(false);
                pawn.jobs.StartJob(
                    portalReturnJob, JobCondition.InterruptForced,
                    null, false, true);

                if (Prefs.DevMode)
                {
                    Log.Message(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                        $"entering {returnPortal.LabelCap} to reach the locker map.");
                }
            });
        }

        private static bool IsIdleRecoveryJob(Pawn pawn, Job job)
        {
            if (job == null)
                return true;

            // RimWorld and several AI mods use specialized Wait jobs for the
            // visible "Standing" activity. Checking only JobDefOf.Wait misses
            // those variants and can leave a finished worker active forever.
            // The movement/carry guards keep connective waits during hauling or
            // travel from being mistaken for completed work.
            string defName = job.def?.defName ?? string.Empty;
            bool waitFamily = job.def == JobDefOf.Wait ||
                job.def == JobDefOf.Wait_Wander ||
                defName.StartsWith("Wait", System.StringComparison.OrdinalIgnoreCase) ||
                defName.IndexOf("Standing", System.StringComparison.OrdinalIgnoreCase) >= 0;

            // Several compatibility jobs retain their own JobDef while their
            // driver has stopped doing work and RimWorld reports the pawn as
            // Standing. Their toil timer may continue changing, which makes the
            // progress watchdog believe the stalled job is active forever. Use
            // the same visible report the player sees, behind movement/carrying
            // guards and the normal four-second idle grace period.
            string report = job.GetReport(pawn);
            bool visiblyStanding = !string.IsNullOrEmpty(report) &&
                report.IndexOf(
                    "Standing", System.StringComparison.OrdinalIgnoreCase) >= 0;

            return (waitFamily || visiblyStanding) &&
                   pawn?.pather?.Moving != true &&
                   pawn?.carryTracker?.CarriedThing == null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                ReleaseUnserializableRestoringSavedWeaponsBeforeSave();
                DiscardUnserializableSnapshotsBeforeSave();
                DiscardInvalidPendingWorkBeforeSave();
            }
            Scribe_Collections.Look(ref Rules, "automaticOutfitManagerRules", LookMode.Deep);
            Scribe_Collections.Look(ref PawnStates, "automaticOutfitManagerPawnStates", LookMode.Deep);
            Scribe_Collections.Look(ref ManagedApparelIds, "automaticOutfitManagerManagedIds", LookMode.Value);
            Scribe_Collections.Look(
                ref ManagedApparelOwners,
                "automaticOutfitManagerManagedOwners",
                LookMode.Value,
                LookMode.Value);
            Scribe_Collections.Look(
                ref ManagedApparelOwnerIds,
                "automaticOutfitManagerManagedOwnerIds",
                LookMode.Value,
                LookMode.Value);
            Scribe_Collections.Look(ref ManagedWeaponIds,
                "automaticOutfitManagerManagedWeaponIds", LookMode.Value);
            Scribe_Collections.Look(ref ManagedApparelStockDefs,
                "automaticOutfitManagerManagedApparelStockDefs", LookMode.Def);
            Scribe_Collections.Look(ref ManagedWeaponStockDefs,
                "automaticOutfitManagerManagedWeaponStockDefs", LookMode.Def);
            Rules ??= new List<ApparelRule>();
            PawnStates ??= new List<PawnApparelState>();
            ManagedApparelIds ??= new List<string>();
            ManagedApparelOwners ??= new Dictionary<string, string>();
            ManagedApparelOwnerIds ??= new Dictionary<string, string>();
            ManagedWeaponIds ??= new List<string>();
            ManagedApparelStockDefs ??= new List<ThingDef>();
            ManagedWeaponStockDefs ??= new List<ThingDef>();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                PawnStates.RemoveAll(state => state?.Pawn == null);
                int repairedManagedJobContexts =
                    RepairManagedCurrentJobThinkTreesAfterLoad();
                int inactiveSavedOwners = PruneInactiveSavedApparelOwnership();
                SeedManagedStockCatalogs();
                RebuildRuntimeIndexes();
                ManagedWorkClaimRegistry.ResetForLoadedGame();
                ProtectedBoundaryRetryRegistry.ResetForLoadedGame();
                UnavailableWorkRegistry.ResetForLoadedGame();
                RuleEvaluator.ResetRuntimeCache();
                PawnAccessClassifier.ResetRuntimeCache();
                Patches.PawnJobTracker_StartJob_Patch.ResetRuntimeCache();
                Patches.PawnPathFollower_ProtectedArea_Patch.ResetRuntimeCache();
                UI.PawnAutomaticOutfitStatus.ResetRuntimeCache();
                Patches.ProtectedPathAvoidance.ResetForLoadedGame();

                if (Prefs.DevMode && inactiveSavedOwners > 0)
                {
                    Log.Message(
                        $"[AutomaticOutfitManager] Released {inactiveSavedOwners} " +
                        "inactive saved-apparel ownership record(s) after load.");
                }
                if (Prefs.DevMode && repairedManagedJobContexts > 0)
                {
                    Log.Message(
                        $"[AutomaticOutfitManager] Repaired thinker context for " +
                        $"{repairedManagedJobContexts} managed current job(s) after load.");
                }
                if (Prefs.DevMode && PawnStates.Count > 0)
                    Log.Message($"[AutomaticOutfitManager] Loaded {PawnStates.Count} pawn outfit snapshot(s).");
            }
        }

        private int RepairManagedCurrentJobThinkTreesAfterLoad()
        {
            ThinkTreeDef humanlikeThinkTree =
                DefDatabase<ThinkTreeDef>.GetNamedSilentFail("Humanlike");
            if (humanlikeThinkTree == null)
                return 0;

            int repaired = 0;
            foreach (PawnApparelState state in PawnStates)
            {
                Pawn pawn = state?.Pawn;
                Job currentJob = pawn?.jobs?.curJob;
                bool activeManagedIntervention =
                    state?.ApparelInterventionActive == true ||
                    state?.WeaponInterventionActive == true;
                bool managedPhase = state?.Transition == ApparelTransition.Preparing ||
                                    state?.Transition == ApparelTransition.Active;
                if (pawn?.RaceProps?.Humanlike != true || currentJob?.def == null ||
                    currentJob.jobGiverThinkTree != null ||
                    !activeManagedIntervention || !managedPhase ||
                    string.IsNullOrEmpty(state.LastManagedWorkJobDefName) ||
                    currentJob.def.defName != state.LastManagedWorkJobDefName)
                {
                    continue;
                }

                // AOM resumes this exact native job after preparing gear. Older
                // candidates allowed StartJob to overwrite its stored thinker
                // tree with null, leaving a stale giver key that Job.ExposeData
                // could not resolve on the next load. Restore only the current,
                // explicitly tracked managed continuation.
                currentJob.jobGiverThinkTree = humanlikeThinkTree;
                repaired++;
            }
            return repaired;
        }

        private int PruneInactiveSavedApparelOwnership()
        {
            var activeSavedIds = new HashSet<string>(PawnStates
                .Where(state => state?.ApparelInterventionActive == true)
                .SelectMany(state => state.OriginalApparel ??
                    Enumerable.Empty<RimWorld.Apparel>())
                .Where(item => item != null)
                .Select(item => item.GetUniqueLoadID()));
            List<string> inactiveIds = ManagedApparelOwners.Keys
                .Concat(ManagedApparelOwnerIds.Keys)
                .Where(id => !string.IsNullOrEmpty(id) &&
                             !activeSavedIds.Contains(id))
                .Distinct()
                .ToList();

            foreach (string itemId in inactiveIds)
            {
                ManagedApparelOwners.Remove(itemId);
                ManagedApparelOwnerIds.Remove(itemId);
                ManagedApparelIds.Remove(itemId);
            }
            return inactiveIds.Count;
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            restorationProgress.Clear();
            restorationRecoveryBackoff.Clear();
            activeWorkProgress.Clear();
            rejectedManagedGearWakeTicks.Clear();
            Patches.GravshipAreaRemapper.RepairAfterLoad(this);
            RebuildRuntimeIndexes();
            int repairedGuestSessions = RepairHostedGuestSessionMarkers();
            int abandonedSnapshots = RepairAbandonedSnapshots("after load");
            if (abandonedSnapshots > 0)
                RebuildRuntimeIndexes();
            foreach (PawnApparelState state in PawnStates.ToList())
            {
                TryCompleteSatisfiedRestoration(
                    state?.Pawn, state,
                    "already-satisfied saved outfit restoration repaired after load");
            }
            foreach (PawnApparelState state in PawnStates)
            {
                if (state?.Transition != ApparelTransition.Restoring)
                    continue;

                // A save can capture the brief Standing/cooldown interval after
                // another weapon mod offered managed gear. Preserve valid
                // restoration jobs, but make an idle loaded pawn eligible for
                // an immediate rebuild on the next component pulse.
                state.LastRestorationAttemptTick = -1;
                state.ActiveIdleTicks = System.Math.Max(state.ActiveIdleTicks, 240);
            }
            int restoredClaims = RebuildPendingWorkClaims();
            if (Prefs.DevMode && restoredClaims > 0)
            {
                Log.Message($"[AutomaticOutfitManager] Restored {restoredClaims} pending work claim(s) after load.");
            }
            if (Prefs.DevMode && repairedGuestSessions > 0)
            {
                Log.Message(
                    $"[AutomaticOutfitManager] Repaired {repairedGuestSessions} " +
                    "hosted-guest outfit session marker(s) after load.");
            }
        }

        private int RepairHostedGuestSessionMarkers()
        {
            int repaired = 0;
            foreach (PawnApparelState state in PawnStates)
            {
                Pawn pawn = state?.Pawn;
                if (pawn == null || state.HostedGuestSession)
                    continue;

                bool liveHostedGuest = PawnAccessClassifier.IsHostedGuest(pawn);
                bool legacyRefugeeDeparture =
                    state.MapDepartureRequested &&
                    state.Transition == ApparelTransition.Restoring &&
                    System.Math.Max(
                        state.UnavailableRestorationAttempts,
                        state.DepartureRestorationAttempts) >=
                        DepartureUnavailableAttemptLimit &&
                    (pawn.kindDef == PawnKindDefOf.Refugee ||
                     pawn.kindDef == PawnKindDefOf.SpaceRefugee);
                if (!liveHostedGuest && !legacyRefugeeDeparture)
                    continue;

                // Older RC saves have no stable session marker. A live hosted
                // guest is authoritative; the pawn-kind fallback is limited to
                // an already-stalled native departure so ordinary player-pawn
                // caravan exits keep the exact Phase 3 contract.
                state.HostedGuestSession = true;
                repaired++;
            }

            return repaired;
        }

        private int RepairAbandonedSnapshots(string stage)
        {
            int repaired = 0;
            foreach (PawnApparelState state in PawnStates.ToList())
            {
                if (state?.Pawn == null)
                {
                    ClearPendingWork(state);
                    PawnStates.Remove(state);
                    repaired++;
                    continue;
                }
                if (!IsAbandonedSnapshot(state))
                    continue;

                int recovered = RecoverDepartedManagedGear(state);
                string recoveredGear = recovered == 1
                    ? "returned 1 managed work item to the locker map and released its abandoned saved-outfit claims"
                    : $"returned {recovered} managed work items to the locker map and released its abandoned saved-outfit claims";
                EndIntervention(
                    state.Pawn,
                    $"repaired a saved-outfit snapshot left behind after the pawn departed the map {stage}; {recoveredGear}");
                repaired++;
            }

            if (repaired > 0)
            {
                indexedPawnStateCount = PawnStates.Count;
                InvalidateWeaponStateIndex();
            }
            return repaired;
        }

        private void DiscardUnserializableSnapshotsBeforeSave()
        {
            foreach (PawnApparelState state in PawnStates
                         .Where(IsUnserializableSnapshot)
                         .ToList())
            {
                Pawn pawn = state?.Pawn;
                if (pawn != null)
                {
                    EndIntervention(
                        pawn,
                        "discarded an unsavable departed-pawn snapshot before save");
                    continue;
                }

                // A discarded world pawn can already have resolved to null by
                // the time the component is scribed. Remove its exact saved
                // apparel records and deep pending Job so neither stale claims
                // nor a reference to the visitor's vanished Lord reach the save.
                foreach (RimWorld.Apparel savedItem in state?.OriginalApparel ??
                             Enumerable.Empty<RimWorld.Apparel>())
                {
                    if (savedItem == null)
                        continue;
                    string itemId = savedItem.GetUniqueLoadID();
                    ManagedApparelOwnerIds.Remove(itemId);
                    ManagedApparelOwners.Remove(itemId);
                }
                ClearPendingWork(state);
                PawnStates.Remove(state);
            }

            indexedPawnStateCount = PawnStates.Count;
            InvalidateWeaponStateIndex();
        }

        private void ReleaseUnserializableRestoringSavedWeaponsBeforeSave()
        {
            bool changed = false;
            foreach (PawnApparelState state in PawnStates)
            {
                ThingWithComps savedWeapon = state?.OriginalWeapon;
                if (state?.Transition != ApparelTransition.Restoring ||
                    state.WeaponInterventionActive != true ||
                    savedWeapon == null || savedWeapon.Destroyed ||
                    savedWeapon.Spawned ||
                    HasSavePersistentHolder(savedWeapon))
                {
                    continue;
                }

                string label = savedWeapon.LabelCap.ToString();
                string holder = RestorationPlanner.HolderDescription(
                    savedWeapon.ParentHolder);
                state.OriginalWeapon = null;
                state.LastRestorationAttemptTick = -1;
                state.ActiveIdleTicks = System.Math.Max(
                    state.ActiveIdleTicks, 240);
                restorationProgress.Remove(state.Pawn);
                restorationRecoveryBackoff.Remove(state.Pawn);
                changed = true;
                Log.Warning(
                    $"[AutomaticOutfitManager] {state.Pawn?.LabelShortCap ?? "Pawn"}: " +
                    $"saved weapon {label} remained inside {holder}, whose holder " +
                    "chain is not save-persistent. AOM released the exact " +
                    "saved-weapon claim before saving so the game will not write " +
                    "an unresolved originalWeapon reference.");
            }

            if (changed)
                InvalidateWeaponStateIndex();
        }

        private static bool HasSavePersistentHolder(Thing gear)
        {
            if (gear?.Spawned == true)
                return true;

            for (IThingHolder holder = gear?.ParentHolder;
                 holder != null;
                 holder = holder.ParentHolder)
            {
                if (holder is Pawn holdingPawn)
                {
                    return !holdingPawn.Destroyed &&
                           (holdingPawn.Spawned ||
                            holdingPawn.ParentHolder != null ||
                            RimWorld.Planet.CaravanUtility.IsInCaravan(
                                holdingPawn) ||
                            Find.WorldPawns?.Contains(holdingPawn) == true);
                }

                if (holder is Thing holdingThing && holdingThing.Spawned)
                    return true;
            }

            return false;
        }

        private static bool IsUnserializableSnapshot(PawnApparelState state)
        {
            Pawn pawn = state?.Pawn;
            if (pawn == null || pawn.Destroyed)
                return true;
            if (pawn.Spawned || pawn.ParentHolder != null ||
                RimWorld.Planet.CaravanUtility.IsInCaravan(pawn))
            {
                return false;
            }

            // A loose pawn must belong to the world-pawn pool to receive a
            // reference load ID. A visitor discarded by world-pawn GC can leave
            // a live C# object behind for the rest of the tick, but serializing
            // that reference writes <pawn>null</pawn> and any saved pending job
            // can then retain an unresolved Lord reference.
            return Find.WorldPawns?.Contains(pawn) != true;
        }

        private static bool IsAbandonedSnapshot(PawnApparelState state)
        {
            Pawn pawn = state?.Pawn;
            if (pawn == null || pawn.Spawned)
                return false;
            if (pawn.Destroyed || pawn.Dead)
                return true;

            // Pawns inside caravans, transport containers, portals, and other
            // holders are still participating in the game and may legitimately
            // resume Phase 3 later. A friendly foreign pawn left loose in the
            // world-pawn pool has finished its map visit; retaining its snapshot
            // can only strand personal gear on the colony map indefinitely.
            if (RimWorld.Planet.CaravanUtility.IsInCaravan(pawn))
                return false;

            Faction playerFaction = Faction.OfPlayerSilentFail;
            return playerFaction != null && pawn.Faction != playerFaction &&
                   Find.WorldPawns?.Contains(pawn) == true;
        }

        private int RecoverDepartedManagedGear(PawnApparelState state)
        {
            Pawn pawn = state?.Pawn;
            if (pawn == null || !TryFindDepartureRecoveryCell(
                    state, out Map map, out IntVec3 cell))
            {
                return 0;
            }

            var managed = new HashSet<Thing>();
            foreach (RimWorld.Apparel apparel in state.ManagedApparel ??
                         Enumerable.Empty<RimWorld.Apparel>())
            {
                if (apparel != null && !apparel.Destroyed)
                    managed.Add(apparel);
            }
            foreach (ThingWithComps weapon in state.ManagedWeapons ??
                         Enumerable.Empty<ThingWithComps>())
            {
                if (weapon != null && !weapon.Destroyed)
                    managed.Add(weapon);
            }

            int recovered = 0;
            foreach (Thing gear in managed.Where(item => HeldByPawn(item, pawn)).ToList())
            {
                if (!TryDetachHeldGear(pawn, gear, out System.Action restore))
                    continue;

                if (GenPlace.TryPlaceThing(
                        gear, cell, map, ThingPlaceMode.Near))
                {
                    MakeDepartureGearAvailable(gear);
                    recovered++;
                }
                else
                {
                    restore?.Invoke();
                }
            }
            return recovered;
        }

        private bool TryFindDepartureRecoveryCell(
            PawnApparelState state,
            out Map map,
            out IntVec3 cell)
        {
            ApparelRule rule = RuleById(state?.ActiveRuleId);
            Area changingArea = rule?.ChangingArea;
            Map candidateMap = changingArea?.Map;
            map = candidateMap;
            cell = changingArea?.ActiveCells
                .Where(candidate => candidate.InBounds(candidateMap) &&
                                    candidate.Standable(candidateMap))
                .DefaultIfEmpty(IntVec3.Invalid)
                .FirstOrDefault() ?? IntVec3.Invalid;
            if (map != null && cell.IsValid)
                return true;

            Thing spawnedOriginal = (state?.OriginalApparel ??
                    new List<RimWorld.Apparel>())
                .FirstOrDefault(item => item?.Spawned == true);
            if (spawnedOriginal == null && state?.OriginalWeapon?.Spawned == true)
                spawnedOriginal = state.OriginalWeapon;
            map = spawnedOriginal?.Map;
            cell = spawnedOriginal?.Position ?? IntVec3.Invalid;
            return map != null && cell.IsValid && cell.InBounds(map);
        }

        private static bool HeldByPawn(Thing gear, Pawn pawn)
        {
            IThingHolder holder = gear?.ParentHolder;
            while (holder != null)
            {
                if (holder == pawn)
                    return true;
                holder = holder.ParentHolder;
            }
            return false;
        }

        private static bool TryDetachHeldGear(
            Pawn pawn,
            Thing gear,
            out System.Action restore)
        {
            restore = null;
            if (pawn?.apparel != null && gear is RimWorld.Apparel apparel &&
                pawn.apparel.WornApparel.Contains(apparel))
            {
                pawn.apparel.Remove(apparel);
                restore = () => pawn.apparel.Wear(apparel, false, false);
                return true;
            }

            if (pawn?.equipment != null && gear is ThingWithComps weapon &&
                pawn.equipment.AllEquipmentListForReading.Contains(weapon))
            {
                pawn.equipment.Remove(weapon);
                restore = () => pawn.equipment.AddEquipment(weapon);
                return true;
            }

            if (gear?.ParentHolder is ThingOwner owner && owner.Remove(gear))
            {
                restore = () => owner.TryAdd(gear);
                return true;
            }

            return false;
        }

        private int RebuildPendingWorkClaims()
        {
            ManagedWorkClaimRegistry.ResetForLoadedGame();
            int restoredClaims = 0;

            // Re-establish saved claims before any pawn thinker can select the
            // same bill, frame, ingredient, or queued target after loading. The
            // oldest intervention wins deterministically if an old save somehow
            // contains conflicting pending jobs; the normal continuation path
            // safely cancels any later state that could not reclaim its target.
            foreach (PawnApparelState state in PawnStates
                         .Where(state =>
                             state?.Pawn?.Spawned == true &&
                             state.PendingWorkJob != null &&
                             !state.RecallRequested &&
                             (state.Transition == ApparelTransition.Preparing ||
                              state.Transition == ApparelTransition.Active))
                         .OrderBy(state => state.StartedTick)
                         .ThenBy(state => state.Pawn.thingIDNumber))
            {
                if (!Patches.PawnJobTracker_StartJob_Patch.PendingWorkJobIsViable(
                        state.Pawn, state.PendingWorkJob, out string invalidReason))
                {
                    CancelPendingWork(state, invalidReason, "after load");
                    continue;
                }

                bool claimed = ManagedWorkClaimRegistry.TryClaim(
                    state.Pawn, state.PendingWorkJob);
                if (claimed && ManagedWorkClaimRegistry.HasActiveClaim(state.Pawn))
                {
                    restoredClaims++;
                    continue;
                }

                CancelPendingWork(
                    state,
                    claimed
                        ? "the saved job no longer has a claimable target"
                        : "another pawn now claims one of its targets",
                    "after load");
            }

            return restoredClaims;
        }

        private void DiscardInvalidPendingWorkBeforeSave()
        {
            foreach (PawnApparelState state in PawnStates.Where(state =>
                         state?.Pawn?.Spawned == true &&
                         state.PendingWorkJob != null).ToList())
            {
                if (!Patches.PawnJobTracker_StartJob_Patch.PendingWorkJobIsViable(
                        state.Pawn, state.PendingWorkJob, out string invalidReason))
                {
                    CancelPendingWork(state, invalidReason, "before save");
                }
            }
        }

        public void NotifyGravshipAreaReferencesRemapped(
            IEnumerable<string> remappedRuleIds,
            Map destinationMap,
            string reason)
        {
            RuleEvaluator.ResetRuntimeCache();
            var ruleIds = new HashSet<string>(
                remappedRuleIds?.Where(id => !string.IsNullOrEmpty(id)) ??
                Enumerable.Empty<string>());
            if (ruleIds.Count == 0 || destinationMap == null)
                return;

            foreach (PawnApparelState state in PawnStates.Where(state =>
                         state?.Pawn?.Spawned == true &&
                         state.Pawn.Map == destinationMap).ToList())
            {
                Pawn pawn = state.Pawn;
                List<ApparelRule> occupiedRules = Rules
                    .Where(rule => rule?.Enabled == true &&
                                   !rule.WorkAreaPaused &&
                                   rule.Area?.Map == destinationMap &&
                                   ruleIds.Contains(rule.Id) &&
                                   pawn.Position.IsValid &&
                                   pawn.Position.InBounds(destinationMap) &&
                                   rule.Area[pawn.Position])
                    .ToList();

                if (occupiedRules.Count == 0)
                    continue;

                if (state.Transition == ApparelTransition.Restoring)
                {
                    Job currentJob = pawn.jobs?.curJob;
                    bool interruptRestoration =
                        IsAssignedApparelTransitionJob(state, currentJob) ||
                        IsAssignedWeaponTransitionJob(state, currentJob);

                    pawn.jobs?.ClearQueuedJobs(false);
                    state.ActiveRuleId = occupiedRules[0].Id;
                    state.CurrentRuleIds = occupiedRules
                        .Select(rule => rule.Id)
                        .Distinct()
                        .ToList();
                    state.RecallRequested = false;
                    state.RecallInterruptPending = false;
                    state.Transition = ApparelTransition.Active;
                    state.ChangingAreaReturnCell = IntVec3.Invalid;
                    state.LastChangingAreaReturnAttemptTick = -1;
                    state.LastRestorationAttemptTick = -1;
                    state.LastApparelPreparationAttemptTick = -1;
                    state.LastApparelPreparationThingId = -1;
                    state.ClearWeaponPreparationRetry();
                    state.WeaponRestorationRequested = false;
                    state.ActiveIdleTicks = 0;
                    state.BufferedTasksCompleted = 0;
                    state.LastBufferedJobLoadId = -1;
                    state.ClearPendingBufferCandidates();
                    ClearPendingWork(state);
                    ManagedWorkClaimRegistry.ReleaseAll(pawn);
                    restorationProgress.Remove(pawn);
                    restorationRecoveryBackoff.Remove(pawn);
                    activeWorkProgress.Remove(pawn);
                    occupiedGearRecoveryTicks.Remove(pawn);

                    if (interruptRestoration && pawn.jobs?.curJob == currentJob)
                    {
                        TryJobTransition(
                            pawn,
                            Find.TickManager?.TicksGame ?? 0,
                            "gravship area remap",
                            () => pawn.jobs.EndCurrentJob(
                                JobCondition.InterruptForced,
                                true));
                    }

                    Log.Warning(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: cancelled " +
                        $"unsafe saved-outfit restoration inside remapped work area " +
                        $"'{occupiedRules[0].Name}' {reason}; required protection " +
                        "remains active.");
                    continue;
                }

                if (state.Transition != ApparelTransition.ReturningToChangingArea ||
                    !state.ChangingAreaReturnCell.IsValid)
                {
                    continue;
                }

                ApparelRule activeRule = occupiedRules.FirstOrDefault(rule =>
                    rule.Id == state.ActiveRuleId) ?? occupiedRules[0];
                if (activeRule.ChangingArea?.Map != destinationMap ||
                    activeRule.ChangingArea[state.ChangingAreaReturnCell])
                {
                    continue;
                }

                Job returnJob = pawn.jobs?.curJob;
                bool interruptReturn =
                    Patches.PawnJobTracker_StartJob_Patch
                        .IsAssignedChangingAreaReturnJob(state, returnJob);
                pawn.jobs?.ClearQueuedJobs(false);
                state.ChangingAreaReturnCell = IntVec3.Invalid;
                state.LastChangingAreaReturnAttemptTick = -1;
                if (interruptReturn && pawn.jobs?.curJob == returnJob)
                {
                    TryJobTransition(
                        pawn,
                        Find.TickManager?.TicksGame ?? 0,
                        "gravship locker-area remap",
                        () => pawn.jobs.EndCurrentJob(
                            JobCondition.InterruptForced,
                            true));
                }
            }
        }

        public bool TryReleaseStrandedRestorationItems(
            Pawn pawn,
            PawnApparelState state)
        {
            const int strandedAttemptLimit = 5;
            if (pawn?.Map == null ||
                state?.Transition != ApparelTransition.Restoring ||
                state.UnavailableRestorationAttempts < strandedAttemptLimit)
            {
                return false;
            }

            List<RimWorld.Apparel> strandedApparel =
                (state.OriginalApparel ?? new List<RimWorld.Apparel>())
                .Where(item => item != null &&
                               !item.Destroyed &&
                               pawn.apparel?.WornApparel.Contains(item) != true &&
                               item.Spawned &&
                               item.Map != pawn.Map)
                .ToList();
            ThingWithComps strandedWeapon =
                state.WeaponInterventionActive &&
                state.OriginalWeapon != null &&
                !state.OriginalWeapon.Destroyed &&
                pawn.equipment?.Primary != state.OriginalWeapon &&
                state.OriginalWeapon.Spawned &&
                state.OriginalWeapon.Map != pawn.Map
                    ? state.OriginalWeapon
                    : null;

            if (strandedApparel.Count == 0 && strandedWeapon == null)
                return false;

            var releasedLabels = new List<string>();
            foreach (RimWorld.Apparel apparel in strandedApparel)
            {
                releasedLabels.Add(apparel.LabelCap.ToString());
                ClearSavedOwner(apparel);
                state.OriginalApparel.Remove(apparel);
                state.ReusedOriginalApparel?.Remove(apparel);
            }

            if (strandedWeapon != null)
            {
                releasedLabels.Add(strandedWeapon.LabelCap.ToString());
                state.OriginalWeapon = null;
                state.RequestWeaponRestoration();
                InvalidateWeaponStateIndex();
            }

            state.UnavailableRestorationAttempts = 0;
            restorationProgress.Remove(pawn);
            restorationRecoveryBackoff.Remove(pawn);
            Log.Warning(
                $"[AutomaticOutfitManager] {pawn.LabelShortCap}: saved gear " +
                $"{string.Join(", ", releasedLabels)} remained on another map " +
                $"after {strandedAttemptLimit} restoration attempts. The gear " +
                "remains there, but AOM released it from this outfit snapshot " +
                "instead of leaving the pawn stuck Standing.");
            return true;
        }

        public bool TryCompleteForeignMapDepartureWithUnavailableSavedGear(
            Pawn pawn,
            PawnApparelState state)
        {
            bool liveHostedGuest = PawnAccessClassifier.IsHostedGuest(pawn);
            int departureAttempts = System.Math.Max(
                state?.DepartureRestorationAttempts ?? 0,
                state?.UnavailableRestorationAttempts ?? 0);
            if (pawn?.Map == null || state?.MapDepartureRequested != true ||
                state.Transition != ApparelTransition.Restoring ||
                (!state.HostedGuestSession && !liveHostedGuest) ||
                departureAttempts < DepartureUnavailableAttemptLimit)
            {
                return false;
            }

            if (liveHostedGuest)
                state.HostedGuestSession = true;

            // Phase 3 normally removes these items through ordinary jobs. If a
            // guest is already trying to leave and its saved personal gear has
            // remained unavailable for the bounded retry window, do the same
            // safe release used by the final map-exit guard. Otherwise a worn,
            // equipped, carried, or inventoried work item makes the fallback
            // reject every retry forever and the guest remains Standing.
            int returnedManagedGear = DropHeldManagedGear(
                pawn, state, pawn.Map, pawn.Position);

            bool managedApparelHeld = (state.ManagedApparel ??
                    new List<RimWorld.Apparel>())
                .Any(item => item != null && !item.Destroyed &&
                             HeldByPawn(item, pawn));
            bool managedWeaponHeld = (state.ManagedWeapons ??
                    new List<ThingWithComps>())
                .Any(item => item != null && !item.Destroyed &&
                             item != state.OriginalWeapon &&
                             HeldByPawn(item, pawn));
            if (managedApparelHeld || managedWeaponHeld)
                return false;

            var unresolvedLabels = new List<string>();
            unresolvedLabels.AddRange((state.OriginalApparel ??
                    new List<RimWorld.Apparel>())
                .Where(item => item != null && !item.Destroyed &&
                               pawn.apparel?.WornApparel.Contains(item) != true)
                .Select(item => item.LabelCap.ToString()));
            if (state.WeaponInterventionActive &&
                state.OriginalWeapon != null &&
                !state.OriginalWeapon.Destroyed &&
                pawn.equipment?.Primary != state.OriginalWeapon)
            {
                unresolvedLabels.Add(state.OriginalWeapon.LabelCap.ToString());
            }

            if (unresolvedLabels.Count == 0)
            {
                // A loaded or interrupted restoration can retain only stale
                // transition bookkeeping after all personal gear is already
                // restored and every managed item has been returned. Treat that
                // as success instead of waiting forever for an unavailable item
                // that no longer exists in the snapshot.
                EndIntervention(
                    pawn,
                    $"native departure restoration was already complete after " +
                    $"returning {returnedManagedGear} lingering managed work item(s)");
                return true;
            }

            string unresolved = string.Join(", ", unresolvedLabels.Distinct());
            EndIntervention(
                pawn,
                $"native departure could not retrieve personal saved gear " +
                $"{unresolved} after {DepartureUnavailableAttemptLimit} " +
                $"restoration attempts; returned {returnedManagedGear} lingering " +
                "managed work item(s), so " +
                "the foreign pawn may leave without taking locker stock");
            return true;
        }

        private bool TryEnforceRuntimeProtectedGear(
            Pawn pawn, Job currentJob, int currentTick)
        {
            if (pawn?.Downed == true)
            {
                occupiedGearRecoveryTicks.Remove(pawn);
                return false;
            }

            PawnApparelState state = StateFor(pawn);
            if (state == null && Patches.PawnJobTracker_StartJob_Patch
                    .IsMapDepartureJob(currentJob))
            {
                // The StartJob boundary already restored and cleared the exact
                // saved outfit before this native exit job was allowed to run.
                // Occupancy remains authoritative for ordinary jobs, but
                // reopening a work session while the exit route crosses the
                // gravship produces an equip/restore loop and prevents leaving.
                occupiedGearRecoveryTicks.Remove(pawn);
                return false;
            }

            // Some native drivers finalize or replace a destination after
            // StartJob. Evaluate current occupancy first and the running job's
            // late-bound targets only when outside every live area. The shared
            // evaluator performs one eligibility check and one map-filtered rule
            // pass instead of constructing and combining two independent lists.
            List<ApparelRule> protectedRules =
                RuleEvaluator.MatchingRuntimeRules(pawn, currentJob);
            if (protectedRules.Count == 0)
            {
                if (pawn != null)
                    occupiedGearRecoveryTicks.Remove(pawn);
                return false;
            }

            List<ApparelRule> missingGearRules = protectedRules
                .Where(rule => RuleEvaluator.HasMissingRequiredGear(pawn, rule))
                .ToList();

            // A failed gear search can deliberately send a pawn from the
            // protected interior to its locker or the nearest safe exterior
            // cell. Do not let the periodic occupancy audit interrupt that
            // one-way egress before it clears every rule with a recorded
            // shortage. The path boundary independently prevents re-entry.
            if (missingGearRules.Count > 0 &&
                missingGearRules.All(rule =>
                    Patches.PawnJobTracker_StartJob_Patch
                        .IsUnavailableGearEgressJob(pawn, currentJob, rule)))
            {
                occupiedGearRecoveryTicks.Remove(pawn);
                return true;
            }

            if (state?.RecallRequested == true)
            {
                // Recall owns the next transition. In particular, a save-time
                // cancellation can leave an assigned Wear/Equip finishing for
                // a few more ticks. Do not let the periodic occupancy audit
                // start a replacement outfit plan in that window; the ordinary
                // StartJob path will route the pawn to the locker and restore
                // the saved outfit as soon as the owned step yields.
                occupiedGearRecoveryTicks.Remove(pawn);
                return true;
            }

            if (state?.Transition == ApparelTransition.Restoring)
            {
                bool assignedRestorationJob =
                    IsAssignedApparelTransitionJob(state, currentJob) ||
                    IsAssignedWeaponTransitionJob(state, currentJob);
                if (assignedRestorationJob || missingGearRules.Count == 0)
                {
                    // The exact restoration queue owns this route. It may need
                    // to cross a locker or another protected area while the pawn
                    // still wears the complete local requirement. Let that
                    // transition finish; the boundary patch independently
                    // protects the first genuinely unsafe cell.
                    occupiedGearRecoveryTicks.Remove(pawn);
                    return true;
                }

                // Native activity took control after the restoration queue lost
                // its exact assigned job while the pawn was missing protection
                // inside another rule. Keep the original personal snapshot, but
                // retire the stale restoration queue and make the occupied rule
                // the single active preparation target.
                state.ActiveRuleId = protectedRules[0].Id;
                state.CurrentRuleIds = protectedRules
                    .Select(rule => rule.Id)
                    .Distinct()
                    .ToList();
                state.RecallRequested = false;
                state.RecallInterruptPending = false;
                state.Transition = ApparelTransition.Active;
                state.ChangingAreaReturnCell = IntVec3.Invalid;
                state.LastChangingAreaReturnAttemptTick = -1;
                state.LastRestorationAttemptTick = -1;
                state.LastApparelPreparationAttemptTick = -1;
                state.LastApparelPreparationThingId = -1;
                state.ClearWeaponPreparationRetry();
                state.WeaponRestorationRequested = false;
                state.ActiveIdleTicks = 0;
                ClearPendingWork(state);
                ManagedWorkClaimRegistry.ReleaseAll(pawn);
                restorationProgress.Remove(pawn);
                activeWorkProgress.Remove(pawn);

                if (Prefs.DevMode)
                {
                    Log.Message(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                        "restoration lost its assigned transition inside an " +
                        "active work area; retargeting complete area protection.");
                }
            }

            if (missingGearRules.Count == 0)
            {
                occupiedGearRecoveryTicks.Remove(pawn);
                return false;
            }

            if (IsAssignedApparelTransitionJob(state, currentJob) ||
                IsAssignedWeaponTransitionJob(state, currentJob))
            {
                return true;
            }

            // A colony prisoner whose cell is also protected may be unable to
            // reach replacement gear stored outside the custody area. The
            // StartJob boundary already attempts preparation whenever native
            // wandering, eating, or bed rest begins. Do not repeatedly interrupt
            // that same custody-safe fallback after preparation fails: it
            // produces RimWorld's IdleError, leaves the prisoner visibly
            // Standing, and can also break food, sleep, or healing. The next
            // native job will recheck the rule and equip gear if it has become
            // reachable in the meantime.
            if (Patches.PawnJobTracker_StartJob_Patch
                .IsNativePrisonerUnavailableGearFallbackJob(pawn, currentJob))
            {
                occupiedGearRecoveryTicks.Remove(pawn);
                return true;
            }

            // A colonist or slave must not be trapped awake forever when their
            // assigned bed is protected but the complete required set is
            // genuinely unavailable. StartJob records a short-lived block only
            // after the normal gear search fails. During that bounded window,
            // preserve the native bed-rest job instead of interrupting it every
            // component pulse. When the block expires, the next pulse retries
            // the complete outfit and resumes strict enforcement if stock has
            // become reachable.
            if (Patches.PausedAreaWorkFilter.IsEssentialPersonalJob(currentJob) &&
                missingGearRules.All(rule =>
                    UnavailableWorkRegistry.HasActiveRuleBlock(pawn, rule)))
            {
                occupiedGearRecoveryTicks.Remove(pawn);
                return true;
            }

            // When two genuinely overlapping rules require mutually exclusive
            // outfits, an already protected worker may finish a short meal under
            // the currently equipped rule instead of oscillating between the two
            // impossible sets. The StartJob boundary records this narrow,
            // time-bounded exception; validate it again at the pawn's current
            // overlap cell so it cannot become a general missing-gear bypass.
            if (missingGearRules.Count > 0 && missingGearRules.All(rule =>
                    Patches.PawnJobTracker_StartJob_Patch
                        .IsManagedIncompatibleIngestFallback(
                            pawn, state, currentJob, rule, pawn.Position)))
            {
                occupiedGearRecoveryTicks.Remove(pawn);
                return true;
            }

            // A missing or newly removed item can leave a sleeping, waiting, or
            // compatibility-controlled pawn inside without another StartJob or
            // path-cell callback. Re-enter the shared job boundary at a bounded
            // cadence; it finds every missing piece, preserves the interrupted
            // native job, and resumes that exact activity after preparation.
            if (occupiedGearRecoveryTicks.TryGetValue(pawn, out int lastTick) &&
                currentTick - lastTick < 300)
            {
                return true;
            }

            occupiedGearRecoveryTicks[pawn] = currentTick;

            bool stalePreparation = state?.Transition ==
                ApparelTransition.Preparing &&
                protectedRules.All(rule => rule.Id != state.ActiveRuleId);
            if (stalePreparation)
            {
                string previousRuleName =
                    RuleById(state.ActiveRuleId)?.Name ?? "previous rule";
                state.ActiveRuleId = protectedRules[0].Id;
                state.CurrentRuleIds = protectedRules
                    .Select(rule => rule.Id)
                    .Distinct()
                    .ToList();
                state.Transition = ApparelTransition.Active;
                state.RecallRequested = false;
                state.RecallInterruptPending = false;
                state.ChangingAreaReturnCell = IntVec3.Invalid;
                state.LastChangingAreaReturnAttemptTick = -1;
                state.LastApparelPreparationAttemptTick = -1;
                state.LastApparelPreparationThingId = -1;
                state.ClearWeaponPreparationRetry();
                state.WeaponRestorationRequested = false;
                state.ActiveIdleTicks = 0;
                ClearPendingWork(state);
                ManagedWorkClaimRegistry.ReleaseAll(pawn);
                restorationProgress.Remove(pawn);
                activeWorkProgress.Remove(pawn);
                if (Prefs.DevMode)
                {
                    Log.Message(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                        $"discarded stale preparation for '{previousRuleName}' " +
                        $"after entering or targeting '{protectedRules[0].Name}'.");
                }
            }

            if (currentJob?.def != null &&
                currentJob.def != JobDefOf.Wait &&
                currentJob.def != JobDefOf.Wait_Wander)
            {
                foreach (ApparelRule rule in missingGearRules)
                {
                    UnavailableWorkRegistry.Block(
                        pawn, rule, currentJob, 300);
                }
            }

            bool transitioned = TryJobTransition(
                pawn, currentTick, "occupied-area gear enforcement", () =>
                {
                    ReleaseNativeReservations(pawn, pawn.jobs?.curJob);
                    pawn.jobs?.ClearQueuedJobs(false);
                    if (!Patches.PawnJobTracker_StartJob_Patch
                            .TryPrepareForOccupiedRules(pawn, protectedRules))
                    {
                        Job trigger = Patches.PawnJobTracker_StartJob_Patch
                            .MakeSafeWaitJob(pawn, 30);
                        pawn.jobs?.StartJob(
                            trigger, JobCondition.InterruptForced,
                            null, false, true);
                    }
                });
            if (transitioned && Prefs.DevMode)
            {
                string names = string.Join(", ", protectedRules
                    .Where(rule => RuleEvaluator.HasMissingRequiredGear(pawn, rule))
                    .Select(rule => $"'{rule.Name}'"));
                Log.Message(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                    $"rechecking complete required gear for {names}.");
            }
            return true;
        }

        private static bool TryReleasePersistentlyRejectedSavedWeapon(
            Pawn pawn, PawnApparelState state)
        {
            if (pawn?.equipment == null || state?.WeaponInterventionActive != true ||
                state.OriginalWeapon == null || state.OriginalWeapon.Destroyed ||
                pawn.equipment.Primary != null ||
                ManagedWeaponIsStillHeldByPawn(pawn, state) ||
                !ApparelRestorationIsComplete(pawn, state))
            {
                if (state != null)
                    state.RejectedWeaponRestorationAttempts = 0;
                return false;
            }

            bool custodyPawn = pawn.IsSlave || PawnAccessClassifier.IsColonyPrisoner(pawn);
            int attemptLimit = custodyPawn ? 5 : 30;
            if (state.RejectedWeaponRestorationAttempts < attemptLimit)
                return false;

            ThingWithComps savedWeapon = state.OriginalWeapon;
            state.CompleteWeaponRestoration();
            Log.Warning(
                $"[AutomaticOutfitManager] {pawn.LabelShortCap}: RimWorld rejected " +
                $"{attemptLimit} actual Equip attempts for saved weapon " +
                $"{savedWeapon.LabelCap}. It remains available on the map; " +
                "releasing the pawn from the locker-room restoration instead " +
                "of leaving them stuck Standing.");
            return true;
        }

        public bool TryReleasePersistentlyUnavailableSavedWeapon(
            Pawn pawn, PawnApparelState state)
        {
            const int unavailableAttemptLimit = 5;
            if (pawn?.equipment == null || state?.WeaponInterventionActive != true ||
                state.OriginalWeapon == null ||
                pawn.equipment.Primary != null ||
                ManagedWeaponIsStillHeldByPawn(pawn, state) ||
                !ApparelRestorationIsComplete(pawn, state) ||
                state.UnavailableRestorationAttempts < unavailableAttemptLimit)
            {
                return false;
            }

            ThingWithComps savedWeapon = state.OriginalWeapon;
            string unavailableReason;
            if (savedWeapon.Destroyed)
            {
                unavailableReason = "the item was destroyed";
            }
            else if (!savedWeapon.Spawned)
            {
                unavailableReason = $"it remained inside " +
                    RestorationPlanner.HolderDescription(
                        savedWeapon.ParentHolder);
            }
            else
            {
                // Another-map references use the dedicated stranded-gear path,
                // which retains the exact item on its source map while clearing
                // only this snapshot after the same bounded attempt count.
                if (savedWeapon.Map != pawn.Map)
                    return false;

                bool canEquip = RestorationPlanner.CanAttemptSavedWeaponEquip(
                    savedWeapon, pawn, out string cantReason);
                bool forbidden = savedWeapon.IsForbidden(pawn);
                bool reservable = !forbidden && pawn.CanReserve(savedWeapon);
                bool reachable = reservable && pawn.CanReach(
                    savedWeapon, PathEndMode.ClosestTouch, Danger.Deadly);
                if (reachable && canEquip)
                    return false;

                unavailableReason = forbidden
                    ? "the item remained forbidden"
                    : !reservable
                        ? "the item remained reserved by another task"
                        : !reachable
                            ? "the item remained unreachable"
                            : string.IsNullOrEmpty(cantReason)
                                ? "RimWorld continued to reject the item"
                                : cantReason;
            }

            state.CompleteWeaponRestoration();
            Log.Warning(
                $"[AutomaticOutfitManager] {pawn.LabelShortCap}: saved weapon " +
                $"{savedWeapon.LabelCap} remained unavailable after " +
                $"{unavailableAttemptLimit} recovery attempts because " +
                $"{unavailableReason}. AOM released the exact saved-weapon " +
                "claim and allowed restoration to finish unarmed instead of " +
                "leaving the pawn stuck Standing.");
            return true;
        }

        private static bool ManagedWeaponIsStillHeldByPawn(
            Pawn pawn, PawnApparelState state)
        {
            foreach (ThingWithComps weapon in state.ManagedWeapons ??
                     new List<ThingWithComps>())
            {
                if (weapon == null || weapon.Destroyed || weapon.Spawned)
                    continue;

                IThingHolder holder = weapon.ParentHolder;
                while (holder != null)
                {
                    if (holder == pawn)
                        return true;
                    holder = holder.ParentHolder;
                }
            }

            return false;
        }

        private static bool ApparelRestorationIsComplete(
            Pawn pawn, PawnApparelState state)
        {
            if (state.ApparelInterventionActive != true)
                return true;
            if (pawn.apparel == null)
                return false;

            bool managedRemoved = (state.ManagedApparel ?? new List<Apparel>())
                .Where(item => item != null && !item.Destroyed)
                .All(item => !pawn.apparel.WornApparel.Contains(item));
            bool originalsRestored = (state.OriginalApparel ?? new List<Apparel>())
                .Where(item => item != null && !item.Destroyed)
                .All(item => pawn.apparel.WornApparel.Contains(item));
            return managedRemoved && originalsRestored;
        }

        public bool TryCompleteSatisfiedRestoration(
            Pawn pawn, PawnApparelState state, string releaseReason = null)
        {
            if (pawn == null || state?.Transition != ApparelTransition.Restoring)
                return false;

            bool managedWeaponHeld = ManagedWeaponIsStillHeldByPawn(pawn, state);
            if (state.WeaponRestorationRequested && !managedWeaponHeld &&
                (pawn.equipment?.Primary == state.OriginalWeapon ||
                 (state.OriginalWeapon == null && pawn.equipment?.Primary == null)))
            {
                state.CompleteWeaponRestoration();
                managedWeaponHeld = false;
            }

            bool managedApparelHeld = (state.ManagedApparel ??
                    new List<RimWorld.Apparel>())
                .Any(item => item != null && !item.Destroyed &&
                             HeldByPawn(item, pawn));
            if (managedApparelHeld || managedWeaponHeld ||
                state.WeaponInterventionActive ||
                !ApparelRestorationIsComplete(pawn, state))
            {
                return false;
            }

            EndIntervention(pawn, releaseReason);
            return true;
        }

        private void CancelPendingWork(
            PawnApparelState state, string reason, string stage)
        {
            Pawn pawn = state?.Pawn;
            if (pawn == null)
                return;

            string jobName = state.PendingWorkJob?.def?.defName ?? "unknown job";
            ManagedWorkClaimRegistry.ReleaseAll(pawn);
            ClearPendingWork(state);
            RequestRecall(state);

            if (Prefs.DevMode)
            {
                Log.Warning(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: discarded " +
                    $"{jobName} continuation {stage} ({reason}); returning saved apparel and weapon.");
            }
        }

        public PawnApparelState StateFor(Pawn pawn)
        {
            EnsureStateIndex();
            return pawn != null && pawnStateIndex.TryGetValue(pawn, out PawnApparelState state)
                ? state
                : null;
        }

        public bool IsTrackedApparel(Pawn pawn, RimWorld.Apparel apparel)
        {
            PawnApparelState state = StateFor(pawn);
            return state != null && apparel != null &&
                   ((state.OriginalApparel?.Contains(apparel) ?? false) ||
                    (state.ManagedApparel?.Contains(apparel) ?? false));
        }

        public bool IsTrackedApparel(RimWorld.Apparel apparel)
        {
            return apparel != null && PawnStates.Any(state => state != null &&
                ((state.OriginalApparel?.Contains(apparel) ?? false) ||
                 (state.ManagedApparel?.Contains(apparel) ?? false)));
        }

        public bool IsManagedApparelAssignedToOtherPawn(
            RimWorld.Apparel apparel, Pawn pawn)
        {
            if (apparel == null || pawn == null)
                return false;

            return PawnStates.Any(state =>
                state?.Pawn != null && state.Pawn != pawn &&
                (state.ManagedApparel?.Contains(apparel) ?? false) &&
                (state.Transition == ApparelTransition.Preparing ||
                 state.Pawn.apparel?.WornApparel.Contains(apparel) == true));
        }

        public bool IsManagedApparel(RimWorld.Apparel apparel)
        {
            EnsureManagedApparelIndex();
            return apparel != null && managedApparelIdIndex.Contains(apparel.GetUniqueLoadID());
        }

        public bool IsManagedApparelDefinition(ThingDef def)
        {
            if (def?.apparel == null)
                return false;

            EnsureManagedApparelDefinitionIndex();
            return managedApparelDefIndex.Contains(def);
        }

        public bool IsManagedWeapon(ThingWithComps weapon)
        {
            EnsureManagedWeaponIndex();
            return weapon?.def?.IsWeapon == true &&
                   managedWeaponIdIndex.Contains(weapon.GetUniqueLoadID());
        }

        public bool IsManagedWeaponDefinition(ThingDef def)
        {
            if (def?.IsWeapon != true)
                return false;

            EnsureManagedWeaponDefinitionIndex();
            return managedWeaponDefIndex.Contains(def);
        }

        public void RememberManagedStockDefinition(ThingDef def)
        {
            if (def?.apparel != null)
            {
                ManagedApparelStockDefs ??= new List<ThingDef>();
                if (!ManagedApparelStockDefs.Contains(def))
                    ManagedApparelStockDefs.Add(def);
                managedApparelDefIndexDirty = true;
                return;
            }

            if (def?.IsWeapon == true)
            {
                ManagedWeaponStockDefs ??= new List<ThingDef>();
                if (!ManagedWeaponStockDefs.Contains(def))
                    ManagedWeaponStockDefs.Add(def);
                managedWeaponDefIndexDirty = true;
            }
        }

        public void RememberManagedStockDefinitions(IEnumerable<ThingDef> defs)
        {
            if (defs == null)
                return;

            foreach (ThingDef def in defs.Where(def => def != null).Distinct())
                RememberManagedStockDefinition(def);
        }

        public bool CanForgetManagedStockDefinition(ThingDef def)
        {
            if (def == null || Rules.Any(rule =>
                    rule?.RequiredApparel?.Contains(def) == true ||
                    rule?.RequiredWeapons?.Contains(def) == true))
            {
                return false;
            }

            return !PawnStates.Any(state =>
                state?.ManagedApparel?.Any(item => item?.def == def) == true ||
                state?.ManagedWeapons?.Any(item => item?.def == def) == true);
        }

        public string ManagedStockForgetBlockReason(ThingDef def)
        {
            if (def == null)
                return "This stock type is unavailable.";

            List<string> ruleNames = Rules
                .Where(rule => rule != null &&
                    (rule.RequiredApparel?.Contains(def) == true ||
                     rule.RequiredWeapons?.Contains(def) == true))
                .Select(rule =>
                {
                    string name = string.IsNullOrWhiteSpace(rule.Name)
                        ? "Unnamed rule"
                        : rule.Name.Trim();
                    return rule.Enabled ? name : $"{name} (inactive)";
                })
                .Distinct()
                .ToList();

            List<string> pawnStatuses = PawnStates
                .Where(state => state?.Pawn != null &&
                    (state.ManagedApparel?.Any(item => item?.def == def) == true ||
                     state.ManagedWeapons?.Any(item => item?.def == def) == true))
                .Select(state =>
                    $"{state.Pawn.LabelShortCap} ({ForgetTransitionLabel(state.Transition)})")
                .Distinct()
                .ToList();

            if (ruleNames.Count == 0 && pawnStatuses.Count == 0)
                return null;

            var blockers = new List<string>();
            if (ruleNames.Count > 0)
                blockers.Add($"required by: {string.Join(", ", ruleNames)}");
            if (pawnStatuses.Count > 0)
                blockers.Add($"in use by: {string.Join(", ", pawnStatuses)}");

            return "Cannot forget this managed stock type. It is " +
                   string.Join("; ", blockers) +
                   ". Forget becomes available after every listed rule requirement is removed " +
                   "and each listed outfit transition finishes.";
        }

        private static string ForgetTransitionLabel(ApparelTransition transition)
        {
            switch (transition)
            {
                case ApparelTransition.Preparing:
                    return "preparing managed outfit";
                case ApparelTransition.Active:
                    return "working";
                case ApparelTransition.ReturningToChangingArea:
                    return "returning to locker room";
                case ApparelTransition.Restoring:
                    return "restoring saved outfit";
                default:
                    return "outfit transition active";
            }
        }

        public bool ForgetManagedStockDefinition(ThingDef def)
        {
            if (!CanForgetManagedStockDefinition(def))
                return false;

            bool removed = false;
            if (def?.apparel != null)
            {
                removed = ManagedApparelStockDefs?.Remove(def) == true;
                managedApparelDefIndexDirty = true;
            }
            else if (def?.IsWeapon == true)
            {
                removed = ManagedWeaponStockDefs?.Remove(def) == true;
                managedWeaponDefIndexDirty = true;
            }

            return removed;
        }

        public void NotifyRuleRequirementsChanged(
            string ruleId, string reason)
        {
            managedApparelDefIndexDirty = true;
            managedWeaponDefIndexDirty = true;
            if (string.IsNullOrEmpty(ruleId))
                return;

            foreach (PawnApparelState state in PawnStates.Where(state =>
                         state?.Pawn != null &&
                         (state.ActiveRuleId == ruleId ||
                          state.CurrentRuleIds?.Contains(ruleId) == true)).ToList())
            {
                ManagedWorkClaimRegistry.ReleaseAll(state.Pawn);
                ClearPendingWork(state);
                RequestRecall(state);
                if (Prefs.DevMode)
                {
                    Log.Message(
                        $"[AutomaticOutfitManager] {state.Pawn.LabelShortCap}: " +
                        $"rule requirements changed ({reason}); returning managed gear " +
                        "before normal work is reconsidered.");
                }
            }
        }

        public bool IsTrackedWeapon(ThingWithComps weapon)
        {
            if (weapon?.def?.IsWeapon != true)
                return false;

            EnsureWeaponStateIndex();
            if (stateTrackedWeaponIndex.Contains(weapon))
                return true;

            EnsureManagedWeaponIndex();
            return managedWeaponIdIndex.Contains(weapon.GetUniqueLoadID());
        }

        public Pawn SavedPawnForWeapon(ThingWithComps weapon)
        {
            if (weapon == null)
                return null;

            EnsureWeaponStateIndex();
            savedWeaponOwnerIndex.TryGetValue(weapon, out Pawn owner);
            return owner;
        }

        public bool IsSavedWeaponForOtherPawn(ThingWithComps weapon, Pawn pawn)
        {
            if (weapon == null || pawn == null)
                return false;

            Pawn owner = SavedPawnForWeapon(weapon);
            return owner != null && owner != pawn;
        }

        public Pawn RestoringOwnerForSavedGear(Thing gear)
        {
            Pawn owner;
            PawnApparelState ownerState;
            if (gear is RimWorld.Apparel apparel)
            {
                owner = SavedPawnFor(apparel);
                ownerState = StateFor(owner);
                return ownerState?.Transition == ApparelTransition.Restoring &&
                       ownerState.OriginalApparel?.Contains(apparel) == true
                    ? owner
                    : null;
            }

            if (gear is ThingWithComps weapon && weapon.def?.IsWeapon == true)
            {
                owner = SavedPawnForWeapon(weapon);
                ownerState = StateFor(owner);
                return ownerState?.Transition == ApparelTransition.Restoring &&
                       ownerState.WeaponInterventionActive &&
                       ownerState.OriginalWeapon == weapon
                    ? owner
                    : null;
            }

            return null;
        }

        public Pawn SavedOwnerForBillTarget(
            Pawn worker, Job job, out Thing savedGear)
        {
            savedGear = null;
            if (worker == null || job?.def != JobDefOf.DoBill)
                return null;

            Pawn owner = SavedOwnerForGear(worker.carryTracker?.CarriedThing,
                out savedGear);
            if (owner != null)
                return owner;

            owner = SavedOwnerForTarget(job.targetA, out savedGear) ??
                    SavedOwnerForTarget(job.targetB, out savedGear) ??
                    SavedOwnerForTarget(job.targetC, out savedGear);
            if (owner != null)
                return owner;

            owner = SavedOwnerInQueue(job.targetQueueA, out savedGear) ??
                    SavedOwnerInQueue(job.targetQueueB, out savedGear);
            return owner;
        }

        private Pawn SavedOwnerForTarget(
            LocalTargetInfo target, out Thing savedGear)
        {
            savedGear = null;
            return target.IsValid && target.HasThing
                ? SavedOwnerForGear(target.Thing, out savedGear)
                : null;
        }

        private Pawn SavedOwnerInQueue(
            List<LocalTargetInfo> targets, out Thing savedGear)
        {
            savedGear = null;
            if (targets == null)
                return null;

            foreach (LocalTargetInfo target in targets)
            {
                Pawn owner = SavedOwnerForTarget(target, out savedGear);
                if (owner != null)
                    return owner;
            }

            return null;
        }

        private Pawn SavedOwnerForGear(Thing gear, out Thing savedGear)
        {
            savedGear = null;
            Pawn owner = gear switch
            {
                RimWorld.Apparel apparel => SavedPawnFor(apparel),
                ThingWithComps weapon when weapon.def?.IsWeapon == true =>
                    SavedPawnForWeapon(weapon),
                _ => null
            };
            if (owner != null)
                savedGear = gear;
            return owner;
        }

        public Pawn RestoringOwnerForJobTarget(
            Pawn otherPawn, Job job, out Thing savedGear)
        {
            savedGear = null;
            if (otherPawn == null || job == null || !HasActiveRestoration())
                return null;

            return RestoringOwnerForJobTargetUnchecked(
                otherPawn, job, out savedGear);
        }

        private Pawn RestoringOwnerForJobTargetUnchecked(
            Pawn otherPawn, Job job, out Thing savedGear)
        {
            savedGear = null;
            if (otherPawn == null || job == null)
                return null;

            Thing carried = otherPawn.carryTracker?.CarriedThing;
            Pawn owner = RestoringOwnerForSavedGear(carried);
            if (owner != null && owner != otherPawn)
            {
                savedGear = carried;
                return owner;
            }

            owner = RestoringOwnerForJobTarget(otherPawn, job.targetA, out savedGear) ??
                    RestoringOwnerForJobTarget(otherPawn, job.targetB, out savedGear) ??
                    RestoringOwnerForJobTarget(otherPawn, job.targetC, out savedGear);
            if (owner != null)
                return owner;

            owner = RestoringOwnerInQueue(
                otherPawn, job.targetQueueA, out savedGear) ??
                    RestoringOwnerInQueue(
                        otherPawn, job.targetQueueB, out savedGear);
            return owner;
        }

        private bool HasActiveRestoration()
        {
            for (int i = 0; i < PawnStates.Count; i++)
            {
                if (PawnStates[i]?.Transition == ApparelTransition.Restoring)
                    return true;
            }

            return false;
        }

        private Pawn RestoringOwnerForJobTarget(
            Pawn otherPawn, LocalTargetInfo target, out Thing savedGear)
        {
            savedGear = null;
            if (!target.IsValid || !target.HasThing)
                return null;

            Pawn owner = RestoringOwnerForSavedGear(target.Thing);
            if (owner == null || owner == otherPawn)
                return null;

            savedGear = target.Thing;
            return owner;
        }

        private Pawn RestoringOwnerInQueue(
            Pawn otherPawn,
            List<LocalTargetInfo> targets,
            out Thing savedGear)
        {
            savedGear = null;
            if (targets == null)
                return null;

            foreach (LocalTargetInfo target in targets)
            {
                Pawn owner = RestoringOwnerForJobTarget(
                    otherPawn, target, out savedGear);
                if (owner != null)
                    return owner;
            }

            return null;
        }

        public bool IsManagedWeaponAssignedToOtherPawn(
            ThingWithComps weapon, Pawn pawn)
        {
            if (weapon == null || pawn == null)
                return false;

            Pawn owner = ManagedPawnForWeapon(weapon);
            return owner != null && owner != pawn;
        }

        public Pawn ManagedPawnForWeapon(ThingWithComps weapon)
        {
            if (weapon == null)
                return null;

            EnsureWeaponStateIndex();
            managedWeaponAssignmentIndex.TryGetValue(weapon, out Pawn owner);
            return owner;
        }

        public void InvalidateManagedApparelDefinitionIndex() =>
            managedApparelDefIndexDirty = true;

        public void InvalidateManagedWeaponDefinitionIndex() =>
            managedWeaponDefIndexDirty = true;

        public void InvalidateManagedDefinitionIndexes()
        {
            managedApparelDefIndexDirty = true;
            managedWeaponDefIndexDirty = true;
            RuleEvaluator.ResetRuntimeCache();
        }

        public void InvalidateWeaponStateIndex() =>
            weaponStateIndexDirty = true;

        public string SavedOwnerFor(RimWorld.Apparel apparel)
        {
            if (apparel == null)
                return null;

            ManagedApparelOwners.TryGetValue(apparel.GetUniqueLoadID(), out string owner);
            return owner;
        }

        public Pawn SavedPawnFor(RimWorld.Apparel apparel)
        {
            if (apparel == null)
                return null;

            string apparelId = apparel.GetUniqueLoadID();
            ManagedApparelOwnerIds.TryGetValue(apparelId, out string pawnId);

            Pawn owner = SpawnedPawnById(pawnId);
            if (owner != null)
                return owner;

            if (!ManagedApparelOwners.TryGetValue(apparelId, out string ownerName))
                return null;

            owner = AllSpawnedPawns().FirstOrDefault(pawn => DisplayNameFor(pawn) == ownerName);
            if (owner != null)
                ManagedApparelOwnerIds[apparelId] = owner.GetUniqueLoadID();
            return owner;
        }

        public bool IsSavedForOtherPawn(RimWorld.Apparel apparel, Pawn pawn)
        {
            if (apparel == null || pawn == null)
                return false;

            string apparelId = apparel.GetUniqueLoadID();
            if (ManagedApparelOwnerIds.TryGetValue(apparelId, out string ownerId))
                return ownerId != pawn.GetUniqueLoadID();

            // Compatibility for items saved by builds that recorded the owner
            // name before stable pawn IDs were introduced.
            if (!ManagedApparelOwners.TryGetValue(apparelId, out string ownerName))
                return false;

            if (DisplayNameFor(pawn) == ownerName)
            {
                ManagedApparelOwnerIds[apparelId] = pawn.GetUniqueLoadID();
                return false;
            }

            Pawn resolvedOwner = SavedPawnFor(apparel);
            return resolvedOwner != null || !string.IsNullOrEmpty(ownerName);
        }

        public void ClearSavedOwner(RimWorld.Apparel apparel)
        {
            ForgetSavedApparel(apparel, preserveReusedAsManaged: false);
        }

        public void ReleaseSavedApparel(RimWorld.Apparel apparel)
        {
            ForgetSavedApparel(apparel, preserveReusedAsManaged: true);
        }

        public void AdoptWornPersonalApparel(
            Pawn pawn, RimWorld.Apparel replacement)
        {
            PawnApparelState state = StateFor(pawn);
            if (state?.ApparelInterventionActive != true ||
                pawn?.apparel?.WornApparel?.Contains(replacement) != true ||
                replacement == null ||
                state.OriginalApparel?.Contains(replacement) == true ||
                state.IsPreparationApparel(replacement) ||
                AutomaticOutfitManager.Storage.ManagedApparelClassifier
                    .Matches(replacement.def) ||
                IsSavedForOtherPawn(replacement, pawn) ||
                IsManagedApparelAssignedToOtherPawn(replacement, pawn))
            {
                return;
            }

            List<RimWorld.Apparel> displaced =
                Patches.SavedApparelReplacementPolicy
                    .ConflictingSavedApparel(pawn, state, replacement);
            foreach (RimWorld.Apparel saved in displaced.ToList())
                ForgetSavedApparel(saved, preserveReusedAsManaged: true);

            state.OriginalApparel ??= new List<RimWorld.Apparel>();
            if (!state.OriginalApparel.Contains(replacement))
                state.OriginalApparel.Add(replacement);
            RegisterManagedApparel(
                new[] { replacement }, pawn);
            WakeRestoringSavedGearOwner(pawn);

            if (Prefs.DevMode)
            {
                string released = displaced.Count == 0
                    ? "an empty personal apparel slot"
                    : string.Join(", ", displaced.Select(item =>
                        item?.LabelCap.ToString() ?? "missing apparel"));
                Log.Message(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: adopted " +
                    $"{replacement.LabelCap} as saved personal apparel and " +
                    $"released {released} from the saved outfit after a " +
                    "successful apparel change.");
            }
        }

        private void ForgetSavedApparel(
            RimWorld.Apparel apparel, bool preserveReusedAsManaged)
        {
            if (apparel == null)
                return;

            string apparelId = apparel.GetUniqueLoadID();
            ManagedApparelOwners.Remove(apparelId);
            ManagedApparelOwnerIds.Remove(apparelId);

            foreach (PawnApparelState state in PawnStates.Where(state => state != null))
            {
                bool removedOriginal = state.OriginalApparel?.Remove(apparel) == true;
                bool removedReused =
                    state.ReusedOriginalApparel?.Remove(apparel) == true;

                // A saved personal item can also be the exact garment reused by
                // the active work outfit. Releasing its saved status means Phase
                // 3 must now return it with the other managed work gear instead
                // of silently leaving it worn or losing it from both ledgers.
                if (preserveReusedAsManaged && removedOriginal && removedReused &&
                    state.ApparelInterventionActive)
                {
                    state.ManagedApparel ??= new List<RimWorld.Apparel>();
                    if (!state.ManagedApparel.Contains(apparel))
                        state.ManagedApparel.Add(apparel);
                }

                if (removedOriginal || removedReused)
                    WakeRestoringSavedGearOwner(state.Pawn);
            }

            if (!IsTrackedApparel(apparel) &&
                !AutomaticOutfitManager.Storage.ManagedApparelClassifier.Matches(apparel.def))
            {
                ManagedApparelIds.Remove(apparelId);
                managedApparelIdIndex.Remove(apparelId);
                indexedManagedApparelCount = ManagedApparelIds.Count;
            }
        }

        public void ReleaseSavedWeapon(ThingWithComps weapon)
        {
            if (weapon?.def?.IsWeapon != true)
                return;

            Pawn owner = SavedPawnForWeapon(weapon);
            PawnApparelState state = StateFor(owner);
            if (state?.OriginalWeapon != weapon)
                return;

            state.OriginalWeapon = null;
            state.RequestWeaponRestoration();
            InvalidateWeaponStateIndex();
            WakeRestoringSavedGearOwner(owner);
        }

        private static string DisplayNameFor(Pawn pawn) =>
            pawn?.Name?.ToStringShort ?? pawn?.LabelShort;

        public ApparelRule RuleById(string ruleId) =>
            Rules.FirstOrDefault(rule => rule != null && rule.Id == ruleId);

        public PawnApparelState TrackCompliantWorkSession(
            Pawn pawn,
            Job job,
            IEnumerable<ApparelRule> matchingRules)
        {
            if (pawn?.RaceProps?.Humanlike != true || pawn.apparel == null ||
                pawn.Drafted || job?.def == null ||
                !Patches.PausedAreaWorkFilter
                    .UsesManagedWorkPreparation(job))
            {
                return null;
            }

            List<ApparelRule> rules = matchingRules?
                .Where(rule => rule?.Enabled == true &&
                               !rule.WorkAreaPaused &&
                               rule.Area?.Map == pawn.Map)
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList() ?? new List<ApparelRule>();
            if (rules.Count == 0 ||
                rules.Any(rule => RuleEvaluator.HasMissingRequiredGear(pawn, rule)))
            {
                return null;
            }

            PawnApparelState state = StateFor(pawn);
            if (state == null)
            {
                // The pawn already owns every requirement. Track the work so it
                // receives the same status and Recall control as an outfitted
                // worker, but capture and claim none of its personal gear.
                state = BeginIntervention(
                    pawn, rules[0], Enumerable.Empty<RimWorld.Apparel>(), null);
            }

            if (state == null || state.RecallRequested ||
                state.Transition == ApparelTransition.ReturningToChangingArea ||
                state.Transition == ApparelTransition.Restoring)
            {
                return state;
            }

            state.CurrentRuleIds = rules.Select(rule => rule.Id).ToList();
            state.Transition = ApparelTransition.Active;
            state.LastManagedWorkJobDefName = job.def.defName;
            state.ActiveIdleTicks = 0;
            return state;
        }

        public PawnApparelState BeginIntervention(
            Pawn pawn,
            ApparelRule rule,
            IEnumerable<RimWorld.Apparel> managedApparel,
            ThingWithComps managedWeapon = null)
        {
            // Access restrictions also cover animals, mechs, and modded robots,
            // but only humanlike pawns with an apparel tracker can participate
            // in outfit transitions. Keep this invariant here as well as at the
            // job boundary so another caller cannot create an empty, looping
            // apparel snapshot for an automated unit.
            if (pawn?.RaceProps?.Humanlike != true || pawn.apparel == null)
            {
                EndIntervention(pawn);
                return null;
            }

            List<RimWorld.Apparel> managedApparelList = managedApparel?
                .Where(item => item != null)
                .ToList() ?? new List<RimWorld.Apparel>();

            PawnApparelState state = StateFor(pawn);
            bool hostedGuestSession = PawnAccessClassifier.IsHostedGuest(pawn);
            if (state != null)
            {
                if (hostedGuestSession)
                    state.HostedGuestSession = true;
                if (managedApparelList.Count > 0)
                {
                    bool captureNeeded = !state.ApparelInterventionActive;
                    state.BeginManagedApparelSnapshot(pawn);
                    if (captureNeeded)
                        RegisterManagedApparel(state.OriginalApparel, pawn);
                }
                state.AddManagedApparel(managedApparelList);
                RegisterManagedApparel(managedApparelList);
                state.BeginManagedWeapon(pawn, managedWeapon);
                RegisterManagedWeapon(managedWeapon);
                state.Transition = ApparelTransition.Preparing;
                state.ActiveIdleTicks = 0;
                return state;
            }

            state = PawnApparelState.Capture(
                pawn, rule, managedApparelList.Count > 0);
            state.HostedGuestSession = hostedGuestSession;
            state.AddManagedApparel(managedApparelList);
            state.BeginManagedWeapon(pawn, managedWeapon);
            if (state.ApparelInterventionActive)
                RegisterManagedApparel(state.OriginalApparel, pawn);
            RegisterManagedApparel(state.ManagedApparel);
            RegisterManagedWeapon(managedWeapon);
            PawnStates.Add(state);
            pawnStateIndex[pawn] = state;
            indexedPawnStateCount = PawnStates.Count;

            if (Prefs.DevMode)
            {
                if (!state.ApparelInterventionActive &&
                    !state.WeaponInterventionActive)
                {
                    Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: started compliant work session for '{rule.Name}'; no personal gear was claimed.");
                }
                else
                {
                    string apparel = state.OriginalApparel.Count == 0
                        ? "none"
                        : string.Join(", ", state.OriginalApparel
                            .Where(item => item != null)
                            .Select(item => item.LabelCap.ToString()));
                    Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: captured apparel snapshot for '{rule.Name}': {apparel}.");
                }
            }

            return state;
        }

        private void RegisterManagedWeapon(ThingWithComps weapon)
        {
            if (weapon?.def?.IsWeapon != true)
                return;

            EnsureManagedWeaponIndex();
            string id = weapon.GetUniqueLoadID();
            if (!ManagedWeaponIds.Contains(id))
                ManagedWeaponIds.Add(id);
            managedWeaponIdIndex.Add(id);
            indexedManagedWeaponCount = ManagedWeaponIds.Count;
        }

        private void RegisterManagedApparel(IEnumerable<RimWorld.Apparel> apparel, Pawn savedOwner = null)
        {
            if (apparel == null)
                return;

            EnsureManagedApparelIndex();

            foreach (RimWorld.Apparel item in apparel.Where(item => item != null))
            {
                string id = item.GetUniqueLoadID();
                if (!ManagedApparelIds.Contains(id))
                    ManagedApparelIds.Add(id);
                managedApparelIdIndex.Add(id);
                indexedManagedApparelCount = ManagedApparelIds.Count;

                if (savedOwner != null)
                {
                    string savedOwnerName = DisplayNameFor(savedOwner);
                    if (!ManagedApparelOwners.TryGetValue(id, out string existingOwnerName))
                    {
                        ManagedApparelOwners[id] = savedOwnerName;
                        ManagedApparelOwnerIds[id] = savedOwner.GetUniqueLoadID();
                    }
                    else if (existingOwnerName == savedOwnerName &&
                             !ManagedApparelOwnerIds.ContainsKey(id))
                    {
                        ManagedApparelOwnerIds[id] = savedOwner.GetUniqueLoadID();
                    }
                }
            }
        }

        public void EndIntervention(Pawn pawn, string releaseReason = null)
        {
            ManagedWorkClaimRegistry.ReleaseAll(pawn);
            restorationProgress.Remove(pawn);
            restorationRecoveryBackoff.Remove(pawn);
            activeWorkProgress.Remove(pawn);
            rejectedManagedGearWakeTicks.Remove(pawn);
            PawnApparelState state = StateFor(pawn);
            if (state == null)
                return;

            ClearPendingWork(state);

            bool trackedOnly = !state.ApparelInterventionActive &&
                !state.WeaponInterventionActive;
            foreach (RimWorld.Apparel savedItem in state.OriginalApparel?
                         .Where(item => item != null).ToList() ??
                     new List<RimWorld.Apparel>())
            {
                // Saved ownership exists only while an exact Phase 3 snapshot
                // is active. Once that snapshot completes or is abandoned, the
                // garment must become ordinary apparel again (unless its type is
                // independently retained as shared managed stock).
                ClearSavedOwner(savedItem);
            }
            PawnStates.Remove(state);
            pawnStateIndex.Remove(pawn);
            indexedPawnStateCount = PawnStates.Count;
            InvalidateWeaponStateIndex();
            if (Prefs.DevMode)
            {
                if (!string.IsNullOrEmpty(releaseReason))
                {
                    Log.Message(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                        $"{releaseReason}; released AOM state without interrupting native behavior.");
                }
                else
                {
                    Log.Message(trackedOnly
                        ? $"[AutomaticOutfitManager] {pawn.LabelShortCap}: compliant work session cleared; personal gear was unchanged."
                        : $"[AutomaticOutfitManager] {pawn.LabelShortCap}: outfit restoration complete; snapshot cleared.");
                }
            }
        }

        private static int DropHeldManagedGear(
            Pawn pawn,
            PawnApparelState state,
            Map map,
            IntVec3 cell)
        {
            if (pawn == null || state == null || map == null || !cell.IsValid)
                return 0;

            var managed = new HashSet<Thing>();
            foreach (RimWorld.Apparel apparel in state.ManagedApparel ??
                         Enumerable.Empty<RimWorld.Apparel>())
            {
                if (apparel != null)
                    managed.Add(apparel);
            }
            foreach (ThingWithComps weapon in state.ManagedWeapons ??
                         Enumerable.Empty<ThingWithComps>())
            {
                if (weapon != null)
                    managed.Add(weapon);
            }

            int released = 0;
            foreach (RimWorld.Apparel apparel in pawn.apparel?.WornApparel
                         .Where(item => item != null && managed.Contains(item))
                         .ToList() ?? new List<RimWorld.Apparel>())
            {
                if (pawn.apparel.TryDrop(
                        apparel, out RimWorld.Apparel dropped, cell, false))
                {
                    MakeDepartureGearAvailable(dropped);
                    released++;
                }
            }

            foreach (ThingWithComps weapon in pawn.equipment?.AllEquipmentListForReading
                         .Where(item => item != null && managed.Contains(item))
                         .ToList() ?? new List<ThingWithComps>())
            {
                if (pawn.equipment.TryDropEquipment(
                        weapon, out ThingWithComps dropped, cell, false))
                {
                    MakeDepartureGearAvailable(dropped);
                    released++;
                }
            }

            Thing carried = pawn.carryTracker?.CarriedThing;
            if (carried != null && managed.Contains(carried) &&
                pawn.carryTracker.TryDropCarriedThing(
                    cell, ThingPlaceMode.Near, out Thing droppedCarried))
            {
                MakeDepartureGearAvailable(droppedCarried);
                released++;
            }

            ThingOwner inventory = pawn.inventory?.innerContainer;
            foreach (Thing item in inventory?
                         .Where(item => item != null && managed.Contains(item))
                         .ToList() ?? new List<Thing>())
            {
                if (inventory.TryDrop(
                        item, cell, map, ThingPlaceMode.Near, out Thing dropped))
                {
                    MakeDepartureGearAvailable(dropped);
                    released++;
                }
            }

            return released;
        }

        private static void MakeDepartureGearAvailable(Thing gear)
        {
            if (gear?.Spawned == true && gear.IsForbidden(Faction.OfPlayer))
                gear.SetForbidden(false, false);
        }

        public static AutomaticOutfitManagerGameComponent Current =>
            Verse.Current.Game?.GetComponent<AutomaticOutfitManagerGameComponent>();

        public static void CapturePendingWork(
            PawnApparelState state, Job job, bool managedWork)
        {
            if (state == null)
                return;

            ClearPendingWork(state);
            state.PendingWorkJob = job;
            state.PendingWorkIsManagedWork = managedWork;

            // Some native job givers reserve a target before StartJob (beds are
            // the important vanilla example). Once AOM owns the exact job as a
            // deep-saved continuation, that pre-start reservation must be
            // released. The managed claim registry protects the work target
            // during the outfit change, and the native driver reserves it again
            // if the continuation is still valid when it actually starts.
            ReleaseNativeReservations(state.Pawn, job);
        }

        public static void ClearPendingWork(PawnApparelState state)
        {
            if (state == null)
                return;

            ReleaseNativeReservations(state.Pawn, state.PendingWorkJob);
            ProtectedBoundaryRetryRegistry.Clear(
                state.Pawn, state.PendingWorkJob);
            state.PendingWorkJob = null;
            state.PendingWorkIsManagedWork = false;
            state.PendingBoundaryRuleIds?.Clear();
        }

        public static void ReleaseNativeReservations(Pawn pawn, Job job)
        {
            if (pawn?.Map == null || job == null)
                return;

            // Pawn.ClearReservationsForJob is RimWorld's complete exact-job
            // cleanup. In addition to the ordinary ReservationManager it clears
            // pawn destinations, attack-target claims, and other native manager
            // records. Calling only reservationManager.ReleaseClaimedBy left
            // interrupted destination/attack jobs referenced but not deep-saved,
            // producing save warnings after a prison-break combat transition.
            // The native helper is idempotent and does not disturb other jobs.
            pawn.ClearReservationsForJob(job);
        }

        private void EnsureStateIndex()
        {
            if (indexedPawnStateCount == PawnStates.Count)
                return;

            pawnStateIndex.Clear();
            foreach (PawnApparelState state in PawnStates.Where(state => state?.Pawn != null))
                pawnStateIndex[state.Pawn] = state;
            indexedPawnStateCount = PawnStates.Count;
        }

        private void EnsureManagedApparelIndex()
        {
            if (indexedManagedApparelCount == ManagedApparelIds.Count)
                return;

            managedApparelIdIndex.Clear();
            foreach (string id in ManagedApparelIds.Where(id => !string.IsNullOrEmpty(id)))
                managedApparelIdIndex.Add(id);
            indexedManagedApparelCount = ManagedApparelIds.Count;
        }

        private void EnsureManagedWeaponIndex()
        {
            if (indexedManagedWeaponCount == ManagedWeaponIds.Count)
                return;

            managedWeaponIdIndex.Clear();
            foreach (string id in ManagedWeaponIds.Where(id => !string.IsNullOrEmpty(id)))
                managedWeaponIdIndex.Add(id);
            indexedManagedWeaponCount = ManagedWeaponIds.Count;
        }

        private void EnsureManagedApparelDefinitionIndex()
        {
            if (!managedApparelDefIndexDirty)
                return;

            managedApparelDefIndex.Clear();
            foreach (ThingDef def in ManagedApparelStockDefs.Where(def =>
                         def?.apparel != null))
            {
                managedApparelDefIndex.Add(def);
            }

            // Defensive synchronization for saves or external rule editors that
            // have not yet passed through the normal selection UI.
            foreach (ApparelRule rule in Rules.Where(rule => rule != null))
            {
                foreach (ThingDef def in rule.RequiredApparel.Where(def =>
                             def?.apparel != null))
                {
                    managedApparelDefIndex.Add(def);
                }
            }

            managedApparelDefIndexDirty = false;
        }

        private void EnsureManagedWeaponDefinitionIndex()
        {
            if (!managedWeaponDefIndexDirty)
                return;

            managedWeaponDefIndex.Clear();
            foreach (ThingDef def in ManagedWeaponStockDefs.Where(def =>
                         def?.IsWeapon == true))
            {
                managedWeaponDefIndex.Add(def);
            }

            // Defensive synchronization for older saves and external editors.
            foreach (ApparelRule rule in Rules.Where(rule =>
                         rule?.RequiredWeapons != null))
            {
                foreach (ThingDef def in rule.RequiredWeapons.Where(def =>
                             def?.IsWeapon == true))
                {
                    managedWeaponDefIndex.Add(def);
                }
            }
            managedWeaponDefIndexDirty = false;
        }

        private void SeedManagedStockCatalogs()
        {
            ManagedApparelStockDefs.RemoveAll(def => def?.apparel == null);
            ManagedWeaponStockDefs.RemoveAll(def => def?.IsWeapon != true);

            foreach (ApparelRule rule in Rules.Where(rule => rule != null))
            {
                RememberManagedStockDefinitions(rule.RequiredApparel);
                RememberManagedStockDefinitions(rule.RequiredWeapons);
            }

            // Migrate in-progress work stock from saves created before the
            // definition catalogs existed. Exact saved personal gear is not
            // included: it remains protected only by its item-level ownership.
            foreach (PawnApparelState state in PawnStates.Where(state => state != null))
            {
                RememberManagedStockDefinitions(
                    state.ManagedApparel?.Select(item => item?.def));
                RememberManagedStockDefinitions(
                    state.ManagedWeapons?.Select(item => item?.def));
            }

            ManagedApparelStockDefs = ManagedApparelStockDefs
                .Where(def => def?.apparel != null)
                .Distinct()
                .ToList();
            ManagedWeaponStockDefs = ManagedWeaponStockDefs
                .Where(def => def?.IsWeapon == true)
                .Distinct()
                .ToList();
            InvalidateManagedDefinitionIndexes();
        }

        private void EnsureWeaponStateIndex()
        {
            if (!weaponStateIndexDirty)
                return;

            stateTrackedWeaponIndex.Clear();
            savedWeaponOwnerIndex.Clear();
            managedWeaponAssignmentIndex.Clear();

            foreach (PawnApparelState state in PawnStates.Where(state =>
                         state?.Pawn != null))
            {
                ThingWithComps original = state.OriginalWeapon;
                if (original?.def?.IsWeapon == true)
                {
                    stateTrackedWeaponIndex.Add(original);
                    if (state.WeaponInterventionActive)
                        savedWeaponOwnerIndex[original] = state.Pawn;
                }

                foreach (ThingWithComps managed in state.ManagedWeapons ??
                             Enumerable.Empty<ThingWithComps>())
                {
                    if (managed?.def?.IsWeapon != true)
                        continue;

                    stateTrackedWeaponIndex.Add(managed);
                    if (state.WeaponInterventionActive)
                        managedWeaponAssignmentIndex[managed] = state.Pawn;
                }
            }

            weaponStateIndexDirty = false;
        }

        private void RebuildRuntimeIndexes()
        {
            indexedPawnStateCount = -1;
            indexedManagedApparelCount = -1;
            indexedManagedWeaponCount = -1;
            managedApparelDefIndexDirty = true;
            managedWeaponDefIndexDirty = true;
            weaponStateIndexDirty = true;
            EnsureStateIndex();
            EnsureManagedApparelIndex();
            EnsureManagedWeaponIndex();
            EnsureManagedApparelDefinitionIndex();
            EnsureManagedWeaponDefinitionIndex();
            EnsureWeaponStateIndex();
            RebuildSpawnedPawnIndex();
        }

        private Pawn SpawnedPawnById(string pawnId)
        {
            if (string.IsNullOrEmpty(pawnId))
                return null;

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (spawnedPawnIndexTick < 0 || currentTick - spawnedPawnIndexTick >= 300)
                RebuildSpawnedPawnIndex();
            if (spawnedPawnIdIndex.TryGetValue(pawnId, out Pawn pawn))
                return pawn;

            RebuildSpawnedPawnIndex();
            spawnedPawnIdIndex.TryGetValue(pawnId, out pawn);
            return pawn;
        }

        private void RebuildSpawnedPawnIndex()
        {
            spawnedPawnIdIndex.Clear();
            foreach (Pawn pawn in AllSpawnedPawns().Where(pawn => pawn != null))
                spawnedPawnIdIndex[pawn.GetUniqueLoadID()] = pawn;
            spawnedPawnIndexTick = Find.TickManager?.TicksGame ?? 0;
        }

        private static IEnumerable<Pawn> AllSpawnedPawns() =>
            Find.Maps.SelectMany(map => map.mapPawns.AllPawnsSpawned);
    }
}
