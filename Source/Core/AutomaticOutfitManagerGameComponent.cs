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
        private const int ManagedGearWakeCoalesceTicks = 120;

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
        private readonly Dictionary<Pawn, RestorationProgress> restorationProgress =
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

            state.RecallRequested = true;

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
                if (pawn.jobs.curJob == null || TryJobTransition(pawn, currentTick, "return request", () =>
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true)))
                {
                    state.RecallInterruptPending = false;
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
                    : state.ManagedApparel?.Contains(apparel) == true;
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
            List<ApparelRule> pausedRules = Rules.Where(rule =>
                rule?.Enabled == true && rule.WorkAreaPaused && rule.Area?.Map != null).ToList();
            bool enforceApparelOwnership = ManagedApparelOwnerIds.Count > 0;
            EnsureWeaponStateIndex();
            bool enforceWeaponOwnership = savedWeaponOwnerIndex.Count > 0;
            bool activeRestoration = HasActiveRestoration();

            foreach (Map map in Find.Maps)
            {
                List<ApparelRule> mapPausedRules = pausedRules.Where(rule => rule.Area.Map == map).ToList();
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
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
                            pawn, enforceApparelOwnership, enforceWeaponOwnership);
                    }

                    Job job = pawn?.jobs?.curJob;
                    if (job == null)
                        continue;

                    bool handled = false;
                    if (pawn.Faction == Faction.OfPlayer && !pawn.Drafted)
                    {
                        PawnApparelState state = StateFor(pawn);
                        DetectExternalWeaponOverride(pawn, state, job);
                        foreach (ApparelRule rule in mapPausedRules)
                        {
                            if (state?.ActiveRuleId == rule.Id && !state.RecallRequested &&
                                !Patches.PausedAreaWorkFilter.MatchesPermittedHaulingRule(pawn, job, rule))
                            {
                                RequestRecall(state);
                                handled = true;
                                break;
                            }

                            if (state?.RecallRequested != true && job.workGiverDef != null &&
                                RuleEvaluator.JobTargetsArea(job, rule.Area))
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

            Pawn savedOwner = RestoringOwnerForJobTarget(
                pawn, job, out Thing savedGear);
            if (savedOwner == null || savedGear == null)
                return false;

            bool carryingSavedGear = pawn.carryTracker?.CarriedThing == savedGear;
            bool released = TryJobTransition(
                pawn, currentTick, "saved-outfit ownership", () =>
                {
                    if (carryingSavedGear &&
                        pawn.carryTracker.TryDropCarriedThing(
                            pawn.Position, ThingPlaceMode.Near, out Thing dropped) &&
                        dropped?.Spawned == true && dropped.IsForbidden(Faction.OfPlayer))
                    {
                        dropped.SetForbidden(false, false);
                    }

                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
                });
            if (!released)
                return false;

            WakeRestoringSavedGearOwner(savedOwner);

            if (Prefs.DevMode)
            {
                string action = carryingSavedGear ? "released" : "stopped hauling";
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
                state.WeaponRestorationRequested ||
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
            Pawn pawn, bool enforceApparel, bool enforceWeapons)
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

        private void MakeReleasedSavedGearAvailable(Thing gear, Pawn owner)
        {
            if (gear?.Spawned == true && gear.IsForbidden(Faction.OfPlayer))
                gear.SetForbidden(false, false);
            WakeRestoringSavedGearOwner(owner);
        }

        private void WakeRestoringSavedGearOwner(Pawn owner)
        {
            PawnApparelState ownerState = StateFor(owner);
            if (ownerState?.Transition != ApparelTransition.Restoring)
                return;

            // The unavailable-item cooldown exists to contain failed transition
            // loops. This exact item has just become available, so rebuild the
            // owner's restoration immediately instead of waiting ten seconds.
            ownerState.LastRestorationAttemptTick = -1;
            ownerState.ActiveIdleTicks = 240;
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
                ApparelRule rule = RuleById(state?.ActiveRuleId);
                if (state?.Transition != ApparelTransition.Restoring)
                {
                    if (pawn != null)
                    {
                        restorationProgress.Remove(pawn);
                        rejectedManagedGearWakeTicks.Remove(pawn);
                    }
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

                    state.ActiveIdleTicks += 30;
                    int idleGrace = hasUnavailableSavedApparel ? 240 : 120;
                    int retryCooldown = hasUnavailableSavedApparel ? 600 : 120;
                    bool retryReady = state.LastRestorationAttemptTick < 0 ||
                        currentTick - state.LastRestorationAttemptTick >= retryCooldown;
                    if (state.ActiveIdleTicks < idleGrace || !retryReady)
                        continue;

                    state.ActiveIdleTicks = 0;
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

                if (pawn?.Spawned != true || pawn.Drafted ||
                    state.Transition != ApparelTransition.Active ||
                    rule?.Enabled != true || rule.WorkAreaPaused ||
                    state.RecallRequested)
                {
                    if (state != null)
                        state.ActiveIdleTicks = 0;
                    continue;
                }

                Job job = pawn.jobs?.curJob;
                bool idle = IsIdleRecoveryJob(pawn, job) ||
                    ((job.def == JobDefOf.HaulToCell || job.def == JobDefOf.HaulToContainer) &&
                     pawn.carryTracker?.CarriedThing == null &&
                     pawn.pather?.Moving != true);

                if (!idle)
                {
                    state.ActiveIdleTicks = 0;
                    continue;
                }

                state.ActiveIdleTicks += 30;
                if (state.ActiveIdleTicks < 240)
                    continue;

                // Some haul drivers can finish their final toil without promptly
                // yielding a new StartJob call. That leaves the apparel state
                // active and the pawn visibly standing forever. After a short
                // grace period, request the normal locker-room restoration path.
                state.ActiveIdleTicks = 0;
                RequestRecall(state);
                if (Prefs.DevMode)
                    Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: finished work and became idle; returning to locker room.");
            }
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
            return TryJobTransition(pawn, currentTick, context, () =>
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
                SeedManagedStockCatalogs();
                RebuildRuntimeIndexes();
                ManagedWorkClaimRegistry.ResetForLoadedGame();

                if (Prefs.DevMode && PawnStates.Count > 0)
                    Log.Message($"[AutomaticOutfitManager] Loaded {PawnStates.Count} pawn outfit snapshot(s).");
            }
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            restorationProgress.Clear();
            rejectedManagedGearWakeTicks.Clear();
            RebuildRuntimeIndexes();
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
                    CancelLoadedPendingWork(state, invalidReason);
                    continue;
                }

                bool claimed = ManagedWorkClaimRegistry.TryClaim(
                    state.Pawn, state.PendingWorkJob);
                if (claimed && ManagedWorkClaimRegistry.HasActiveClaim(state.Pawn))
                {
                    restoredClaims++;
                    continue;
                }

                CancelLoadedPendingWork(
                    state,
                    claimed
                        ? "the saved job no longer has a claimable target"
                        : "another pawn now claims one of its targets");
            }

            return restoredClaims;
        }

        private void CancelLoadedPendingWork(
            PawnApparelState state, string reason)
        {
            Pawn pawn = state?.Pawn;
            if (pawn == null)
                return;

            string jobName = state.PendingWorkJob?.def?.defName ?? "unknown job";
            ManagedWorkClaimRegistry.ReleaseAll(pawn);
            state.PendingWorkJob = null;
            state.PendingWorkIsManagedWork = false;
            RequestRecall(state);

            if (Prefs.DevMode)
            {
                Log.Warning(
                    $"[AutomaticOutfitManager] {pawn.LabelShortCap}: discarded saved " +
                    $"{jobName} continuation after load ({reason}); returning saved apparel and weapon.");
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
                state.PendingWorkJob = null;
                state.PendingWorkIsManagedWork = false;
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

        public Pawn RestoringOwnerForJobTarget(
            Pawn otherPawn, Job job, out Thing savedGear)
        {
            savedGear = null;
            if (otherPawn == null || job == null || !HasActiveRestoration())
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

            EnsureWeaponStateIndex();
            return managedWeaponAssignmentIndex.TryGetValue(
                       weapon, out Pawn owner) &&
                   owner != null && owner != pawn;
        }

        public void InvalidateManagedApparelDefinitionIndex() =>
            managedApparelDefIndexDirty = true;

        public void InvalidateManagedWeaponDefinitionIndex() =>
            managedWeaponDefIndexDirty = true;

        public void InvalidateManagedDefinitionIndexes()
        {
            managedApparelDefIndexDirty = true;
            managedWeaponDefIndexDirty = true;
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
            if (apparel == null)
                return;

            string apparelId = apparel.GetUniqueLoadID();
            ManagedApparelOwners.Remove(apparelId);
            ManagedApparelOwnerIds.Remove(apparelId);

            foreach (PawnApparelState state in PawnStates.Where(state => state != null))
            {
                if (state.OriginalApparel?.Remove(apparel) == true)
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
            if (state != null)
            {
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
                string apparel = state.OriginalApparel.Count == 0
                    ? "none"
                    : string.Join(", ", state.OriginalApparel
                        .Where(item => item != null)
                        .Select(item => item.LabelCap.ToString()));
                Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: captured apparel snapshot for '{rule.Name}': {apparel}.");
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

        public void EndIntervention(Pawn pawn)
        {
            ManagedWorkClaimRegistry.ReleaseAll(pawn);
            restorationProgress.Remove(pawn);
            rejectedManagedGearWakeTicks.Remove(pawn);
            PawnApparelState state = StateFor(pawn);
            if (state == null)
                return;

            PawnStates.Remove(state);
            pawnStateIndex.Remove(pawn);
            indexedPawnStateCount = PawnStates.Count;
            InvalidateWeaponStateIndex();
            if (Prefs.DevMode)
                Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: outfit restoration complete; snapshot cleared.");
        }

        public static AutomaticOutfitManagerGameComponent Current =>
            Verse.Current.Game?.GetComponent<AutomaticOutfitManagerGameComponent>();

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
