using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Rules;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.State
{
    public enum ApparelTransition
    {
        Preparing,
        Active,
        ReturningToChangingArea,
        Restoring
    }

    public sealed class PawnApparelState : IExposable
    {
        public Pawn Pawn;
        public string ActiveRuleId;
        public List<string> CurrentRuleIds = new List<string>();
        public List<Apparel> OriginalApparel = new List<Apparel>();
        public List<Apparel> ManagedApparel = new List<Apparel>();
        public List<Apparel> ReusedOriginalApparel = new List<Apparel>();
        public bool ApparelInterventionActive = true;
        public ThingWithComps OriginalWeapon;
        public List<ThingWithComps> ManagedWeapons = new List<ThingWithComps>();
        public bool WeaponInterventionActive;
        public bool WeaponRestorationRequested;
        public bool WeaponPlayerOverride;
        public bool WeaponRuleOverrideExplicit;
        public ApparelTransition Transition = ApparelTransition.Preparing;
        public int StartedTick;
        public int LastRestorationAttemptTick = -1;
        public int LastApparelPreparationAttemptTick = -1;
        public int LastApparelPreparationThingId = -1;
        public int LastWeaponPreparationAttemptTick = -1;
        public int LastWeaponPreparationThingId = -1;
        public int WeaponPreparationRetriesForCurrentCandidate;
        public int RejectedWeaponPreparationThingId = -1;
        public int RejectedWeaponPreparationTick = -1;
        public List<RejectedWeaponPreparation> RejectedWeaponPreparations =
            new List<RejectedWeaponPreparation>();
        public int UnavailableRestorationAttempts;
        public int DepartureRestorationAttempts;
        public int RejectedWeaponRestorationAttempts;
        public bool HostedGuestSession;
        public bool MapDepartureRequested;
        public int MapDepartureRequestedTick = -1;
        public bool RecallRequested;
        public bool AutomaticIdleReturnRequested;
        public List<string> PauseRecallRuleIds = new List<string>();
        public bool RecallInterruptPending;
        public int LastRecallInterruptAttemptTick = -1;
        public int BufferedTasksCompleted;
        public int LastBufferedJobLoadId = -1;
        public int PendingBufferedJobLoadId = -1;
        public string PendingBufferedRuleId;
        public int LastChangingAreaReturnAttemptTick = -1;
        public IntVec3 ChangingAreaReturnCell = IntVec3.Invalid;
        public int NaturalLockerDwellUntilTick = -1;
        public int ActiveIdleTicks;
        public bool DownedTransitionSuspended;
        public bool DraftedTransitionSuspended;
        public bool DraftedLockerReturnRequired;
        public Job PendingWorkJob;
        public bool PendingWorkIsManagedWork;
        public List<string> PendingBoundaryRuleIds = new List<string>();
        public string LastManagedWorkJobDefName;
        public List<NestedRuleBufferState> NestedRuleBuffers = new List<NestedRuleBufferState>();
        public string LastNestedBufferStatus;
        private bool retiredIndividualRecallRequested;

        public static PawnApparelState Capture(
            Pawn pawn, ApparelRule rule, bool captureApparel = true)
        {
            return new PawnApparelState
            {
                Pawn = pawn,
                ActiveRuleId = rule?.Id,
                OriginalApparel = captureApparel
                    ? pawn?.apparel?.WornApparel
                        .Where(apparel => apparel != null)
                        .ToList() ?? new List<Apparel>()
                    : new List<Apparel>(),
                ManagedApparel = new List<Apparel>(),
                ApparelInterventionActive = captureApparel,
                Transition = ApparelTransition.Preparing,
                StartedTick = Find.TickManager?.TicksGame ?? 0
            };
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn");
            Scribe_Values.Look(ref ActiveRuleId, "activeRuleId");
            Scribe_Collections.Look(ref CurrentRuleIds, "currentRuleIds", LookMode.Value);
            Scribe_Collections.Look(ref OriginalApparel, "originalApparel", LookMode.Reference);
            Scribe_Collections.Look(ref ManagedApparel, "managedApparel", LookMode.Reference);
            Scribe_Collections.Look(ref ReusedOriginalApparel,
                "reusedOriginalApparel", LookMode.Reference);
            Scribe_Values.Look(ref ApparelInterventionActive,
                "apparelInterventionActive", true);
            Scribe_References.Look(ref OriginalWeapon, "originalWeapon");
            Scribe_Collections.Look(ref ManagedWeapons, "managedWeapons", LookMode.Reference);
            Scribe_Values.Look(ref WeaponInterventionActive, "weaponInterventionActive", false);
            Scribe_Values.Look(ref WeaponRestorationRequested, "weaponRestorationRequested", false);
            Scribe_Values.Look(ref WeaponPlayerOverride, "weaponPlayerOverride", false);
            Scribe_Values.Look(ref WeaponRuleOverrideExplicit,
                "weaponRuleOverrideExplicit", false);
            Scribe_Values.Look(ref Transition, "transition", ApparelTransition.Preparing);
            Scribe_Values.Look(ref StartedTick, "startedTick");
            Scribe_Values.Look(ref LastRestorationAttemptTick, "lastRestorationAttemptTick", -1);
            Scribe_Values.Look(ref LastApparelPreparationAttemptTick,
                "lastApparelPreparationAttemptTick", -1);
            Scribe_Values.Look(ref LastApparelPreparationThingId,
                "lastApparelPreparationThingId", -1);
            Scribe_Values.Look(ref LastWeaponPreparationAttemptTick,
                "lastWeaponPreparationAttemptTick", -1);
            Scribe_Values.Look(ref LastWeaponPreparationThingId,
                "lastWeaponPreparationThingId", -1);
            Scribe_Values.Look(ref WeaponPreparationRetriesForCurrentCandidate,
                "weaponPreparationRetriesForCurrentCandidate", 0);
            Scribe_Values.Look(ref RejectedWeaponPreparationThingId,
                "rejectedWeaponPreparationThingId", -1);
            Scribe_Values.Look(ref RejectedWeaponPreparationTick,
                "rejectedWeaponPreparationTick", -1);
            Scribe_Collections.Look(ref RejectedWeaponPreparations,
                "rejectedWeaponPreparations", LookMode.Deep);
            Scribe_Values.Look(ref UnavailableRestorationAttempts, "unavailableRestorationAttempts");
            Scribe_Values.Look(ref DepartureRestorationAttempts,
                "departureRestorationAttempts", 0);
            Scribe_Values.Look(ref RejectedWeaponRestorationAttempts,
                // Earlier RCs incremented this field for recovery wakes that
                // never reached a native Equip job. Use a new key so those
                // inflated counters cannot release an exact saved weapon after
                // loading; current saves persist only real failed Equip jobs.
                "actualWeaponRestorationEquipFailures", 0);
            Scribe_Values.Look(ref HostedGuestSession,
                "hostedGuestSession", false);
            Scribe_Values.Look(ref MapDepartureRequested,
                "mapDepartureRequested", false);
            Scribe_Values.Look(ref MapDepartureRequestedTick,
                "mapDepartureRequestedTick", -1);
            Scribe_Values.Look(ref RecallRequested, "recallRequested", false);
            Scribe_Values.Look(ref AutomaticIdleReturnRequested,
                "automaticIdleReturnRequested", false);
            Scribe_Collections.Look(ref PauseRecallRuleIds,
                "pauseRecallRuleIds", LookMode.Value);
            // RC saves briefly persisted a Recall hold that no longer belongs
            // to the Phase 3 contract. Read it only to recover an already-stuck
            // pawn, then discard it rather than preserving the retired state.
            Scribe_Values.Look(ref retiredIndividualRecallRequested,
                "individualRecallRequested", false);
            Scribe_Values.Look(ref RecallInterruptPending, "recallInterruptPending", false);
            Scribe_Values.Look(ref LastRecallInterruptAttemptTick, "lastRecallInterruptAttemptTick", -1);
            Scribe_Values.Look(ref BufferedTasksCompleted, "bufferedTasksCompleted", 0);
            Scribe_Values.Look(ref LastBufferedJobLoadId, "lastBufferedJobLoadId", -1);
            Scribe_Values.Look(ref PendingBufferedJobLoadId,
                "pendingBufferedJobLoadId", -1);
            Scribe_Values.Look(ref PendingBufferedRuleId,
                "pendingBufferedRuleId");
            Scribe_Values.Look(ref LastChangingAreaReturnAttemptTick, "lastChangingAreaReturnAttemptTick", -1);
            Scribe_Values.Look(ref ChangingAreaReturnCell,
                "changingAreaReturnCell", IntVec3.Invalid);
            Scribe_Values.Look(ref NaturalLockerDwellUntilTick,
                "naturalLockerDwellUntilTick", -1);
            Scribe_Values.Look(ref ActiveIdleTicks, "activeIdleTicks", 0);
            Scribe_Values.Look(ref DownedTransitionSuspended,
                "downedTransitionSuspended", false);
            Scribe_Values.Look(ref DraftedTransitionSuspended,
                "draftedTransitionSuspended", false);
            Scribe_Values.Look(ref DraftedLockerReturnRequired,
                "draftedLockerReturnRequired", false);
            Scribe_Deep.Look(ref PendingWorkJob, "pendingWorkJob");
            Scribe_Values.Look(ref PendingWorkIsManagedWork, "pendingWorkIsManagedWork", false);
            Scribe_Collections.Look(ref PendingBoundaryRuleIds,
                "pendingBoundaryRuleIds", LookMode.Value);
            Scribe_Values.Look(ref LastManagedWorkJobDefName, "lastManagedWorkJobDefName");
            Scribe_Collections.Look(ref NestedRuleBuffers, "nestedRuleBuffers", LookMode.Deep);
            Scribe_Values.Look(ref LastNestedBufferStatus, "lastNestedBufferStatus");
            OriginalApparel ??= new List<Apparel>();
            ManagedApparel ??= new List<Apparel>();
            ReusedOriginalApparel ??= new List<Apparel>();
            ManagedWeapons ??= new List<ThingWithComps>();
            RejectedWeaponPreparations ??=
                new List<RejectedWeaponPreparation>();
            CurrentRuleIds ??= new List<string>();
            PauseRecallRuleIds ??= new List<string>();
            PendingBoundaryRuleIds ??= new List<string>();
            NestedRuleBuffers ??= new List<NestedRuleBufferState>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                PauseRecallRuleIds = PauseRecallRuleIds
                    .Where(ruleId => !string.IsNullOrEmpty(ruleId))
                    .Distinct()
                    .ToList();
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                int overlapCount = RemoveOriginalsFromManagedApparel();
                if (overlapCount > 0)
                {
                    AomLog.Warning(
                        $"[AutomaticOutfitManager] {Pawn?.LabelShortCap ?? "Pawn"}: " +
                        $"repaired {overlapCount} saved apparel item(s) recorded as both " +
                        "personal and managed work gear; personal ownership was retained.");
                }

                int releasedWeaponCount =
                    ReleaseRejectedSpawnedWeaponAssignments();
                if (releasedWeaponCount > 0)
                {
                    AomLog.Basic(
                        $"[AutomaticOutfitManager] {Pawn?.LabelShortCap ?? "Pawn"}: " +
                        $"released {releasedWeaponCount} abandoned work weapon " +
                        "candidate(s) left on the map after a rejected Equip attempt.");
                }
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit &&
                retiredIndividualRecallRequested && RecallRequested &&
                Transition != ApparelTransition.ReturningToChangingArea &&
                Transition != ApparelTransition.Restoring)
            {
                RecallInterruptPending = true;
                LastRecallInterruptAttemptTick = -1;
                retiredIndividualRecallRequested = false;
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit &&
                Transition != ApparelTransition.ReturningToChangingArea)
            {
                ChangingAreaReturnCell = IntVec3.Invalid;
            }
        }

        public void AddManagedApparel(IEnumerable<Apparel> apparel)
        {
            if (apparel == null)
                return;

            foreach (Apparel item in apparel.Where(item => item != null))
            {
                // A direct sibling-rule handoff can reuse an exact garment from
                // the original personal outfit as a destination requirement.
                // It remains personal gear even while satisfying work; adding it
                // to the removal ledger makes restoration alternate forever
                // between RemoveApparel and Wear for the same instance.
                if (OriginalApparel?.Contains(item) == true)
                {
                    ReusedOriginalApparel ??= new List<Apparel>();
                    if (!ReusedOriginalApparel.Contains(item))
                        ReusedOriginalApparel.Add(item);
                    continue;
                }
                if (!ManagedApparel.Contains(item))
                    ManagedApparel.Add(item);
            }
        }

        public bool IsPreparationApparel(Apparel apparel) =>
            apparel != null &&
            (ManagedApparel?.Contains(apparel) == true ||
             ReusedOriginalApparel?.Contains(apparel) == true);

        public int RemoveOriginalsFromManagedApparel()
        {
            if (ManagedApparel == null || OriginalApparel == null ||
                ManagedApparel.Count == 0 || OriginalApparel.Count == 0)
            {
                return 0;
            }

            var original = new HashSet<Apparel>(
                OriginalApparel.Where(item => item != null));
            List<Apparel> overlaps = ManagedApparel
                .Where(item => item != null && original.Contains(item))
                .ToList();
            ReusedOriginalApparel ??= new List<Apparel>();
            foreach (Apparel overlap in overlaps)
            {
                if (!ReusedOriginalApparel.Contains(overlap))
                    ReusedOriginalApparel.Add(overlap);
            }
            return ManagedApparel.RemoveAll(overlaps.Contains);
        }

        public void BeginManagedApparelSnapshot(Pawn pawn)
        {
            if (ApparelInterventionActive || pawn?.apparel == null)
                return;

            OriginalApparel = pawn.apparel.WornApparel
                .Where(apparel => apparel != null)
                .ToList();
            ApparelInterventionActive = true;
        }

        public void BeginManagedWeapon(Pawn pawn, ThingWithComps weapon)
        {
            if (pawn == null || weapon == null)
                return;

            if (!WeaponInterventionActive)
            {
                OriginalWeapon = pawn.equipment?.Primary;
                WeaponInterventionActive = true;
                WeaponPlayerOverride = false;
                WeaponRuleOverrideExplicit = false;
                RejectedWeaponPreparations?.Clear();
            }

            WeaponRestorationRequested = false;
            RejectedWeaponRestorationAttempts = 0;
            if (!ManagedWeapons.Contains(weapon))
                ManagedWeapons.Add(weapon);
            AutomaticOutfitManagerGameComponent.Current?
                .InvalidateWeaponStateIndex();
        }

        public bool IsManagedWeapon(ThingWithComps weapon) =>
            weapon != null && ManagedWeapons?.Contains(weapon) == true;

        public void RecordWeaponPreparationAttempt(ThingWithComps weapon)
        {
            int weaponId = weapon?.thingIDNumber ?? -1;
            if (LastWeaponPreparationThingId != weaponId)
                WeaponPreparationRetriesForCurrentCandidate = 0;
            LastWeaponPreparationAttemptTick = Find.TickManager?.TicksGame ?? 0;
            LastWeaponPreparationThingId = weaponId;
        }

        public bool TryUseWeaponPreparationRetry(
            ThingWithComps weapon, int retryLimit = 1)
        {
            if (weapon == null ||
                LastWeaponPreparationThingId != weapon.thingIDNumber ||
                WeaponPreparationRetriesForCurrentCandidate >= retryLimit)
            {
                return false;
            }

            WeaponPreparationRetriesForCurrentCandidate++;
            LastWeaponPreparationAttemptTick = Find.TickManager?.TicksGame ?? 0;
            return true;
        }

        public void RejectLastWeaponPreparationAttempt()
        {
            if (LastWeaponPreparationThingId >= 0)
            {
                RejectedWeaponPreparationThingId = LastWeaponPreparationThingId;
                RejectedWeaponPreparationTick = Find.TickManager?.TicksGame ?? 0;

                ThingWithComps rejectedWeapon = ManagedWeapons?
                    .FirstOrDefault(weapon => weapon != null &&
                        weapon.thingIDNumber == LastWeaponPreparationThingId);
                if (rejectedWeapon != null)
                {
                    RejectedWeaponPreparations ??=
                        new List<RejectedWeaponPreparation>();
                    RejectedWeaponPreparation existing =
                        RejectedWeaponPreparations.FirstOrDefault(record =>
                            record?.Weapon == rejectedWeapon);
                    if (existing == null)
                    {
                        existing = new RejectedWeaponPreparation
                        {
                            Weapon = rejectedWeapon
                        };
                        RejectedWeaponPreparations.Add(existing);
                    }
                    existing.AvailabilitySignature =
                        WeaponAvailabilitySignature(rejectedWeapon, Pawn);
                    ReleaseRejectedSpawnedWeaponAssignment(rejectedWeapon);
                }
            }

            LastWeaponPreparationAttemptTick = -1;
            LastWeaponPreparationThingId = -1;
            WeaponPreparationRetriesForCurrentCandidate = 0;
        }

        private int ReleaseRejectedSpawnedWeaponAssignments()
        {
            if (ManagedWeapons == null || ManagedWeapons.Count == 0 ||
                RejectedWeaponPreparations == null ||
                RejectedWeaponPreparations.Count == 0)
            {
                return 0;
            }

            var rejectedWeapons = new HashSet<ThingWithComps>(
                RejectedWeaponPreparations
                    .Where(record => record?.Weapon != null)
                    .Select(record => record.Weapon));
            int released = ManagedWeapons.RemoveAll(weapon =>
                weapon != null && weapon != OriginalWeapon &&
                weapon.Spawned && rejectedWeapons.Contains(weapon));
            if (released > 0)
            {
                AutomaticOutfitManagerGameComponent.Current?
                    .InvalidateWeaponStateIndex();
            }
            return released;
        }

        private void ReleaseRejectedSpawnedWeaponAssignment(
            ThingWithComps weapon)
        {
            if (weapon == null || weapon == OriginalWeapon || !weapon.Spawned ||
                ManagedWeapons?.Remove(weapon) != true)
            {
                return;
            }

            AutomaticOutfitManagerGameComponent.Current?
                .InvalidateWeaponStateIndex();
            if (AomLog.DetailedEnabled)
            {
                AomLog.Detailed(
                    $"[AutomaticOutfitManager] {Pawn?.LabelShortCap ?? "Pawn"}: " +
                    $"released rejected work weapon candidate {weapon.LabelCap}; " +
                    "it remains shared locker stock for other pawns.");
            }
        }

        public bool IsTemporarilyRejectedWeapon(
            ThingWithComps weapon, int cooldownTicks = 1200)
        {
            if (weapon == null)
                return false;

            RejectedWeaponPreparations ??=
                new List<RejectedWeaponPreparation>();
            RejectedWeaponPreparation rejection =
                RejectedWeaponPreparations.FirstOrDefault(record =>
                    record?.Weapon == weapon);
            if (rejection == null)
                return false;

            string currentSignature = WeaponAvailabilitySignature(weapon, Pawn);
            if (rejection.AvailabilitySignature != currentSignature)
            {
                RejectedWeaponPreparations.Remove(rejection);
                return false;
            }

            return true;
        }

        private static string WeaponAvailabilitySignature(
            ThingWithComps weapon, Pawn pawn)
        {
            if (weapon == null)
                return "null";

            Map map = weapon.MapHeld;
            string holder = weapon.ParentHolder is Thing holderThing
                ? holderThing.GetUniqueLoadID()
                : weapon.ParentHolder?.GetType().FullName ?? "none";
            bool availableOnPawnMap = weapon.Spawned && map == pawn?.Map;
            bool forbidden = availableOnPawnMap && weapon.IsForbidden(pawn);
            bool equippable = pawn != null && EquipmentUtility.CanEquip(weapon, pawn);
            return $"{weapon.Destroyed}|{weapon.Spawned}|{map?.uniqueID ?? -1}|" +
                   $"{weapon.PositionHeld}|{holder}|{forbidden}|{equippable}";
        }

        public void ClearWeaponPreparationRetry()
        {
            LastWeaponPreparationAttemptTick = -1;
            LastWeaponPreparationThingId = -1;
            WeaponPreparationRetriesForCurrentCandidate = 0;
        }

        public void ClearPendingBufferedTask()
        {
            PendingBufferedJobLoadId = -1;
            PendingBufferedRuleId = null;
        }

        public void ClearPendingBufferCandidates()
        {
            ClearPendingBufferedTask();
            foreach (NestedRuleBufferState progress in
                     NestedRuleBuffers ?? new List<NestedRuleBufferState>())
            {
                if (progress != null)
                    progress.PendingJobLoadId = -1;
            }
        }

        public void RequestWeaponRestoration()
        {
            if (WeaponInterventionActive)
                WeaponRestorationRequested = true;
        }

        public void CompleteWeaponRestoration()
        {
            OriginalWeapon = null;
            ManagedWeapons?.Clear();
            WeaponInterventionActive = false;
            WeaponRestorationRequested = false;
            WeaponRuleOverrideExplicit = false;
            RejectedWeaponRestorationAttempts = 0;
            ClearWeaponPreparationRetry();
            RejectedWeaponPreparationThingId = -1;
            RejectedWeaponPreparationTick = -1;
            RejectedWeaponPreparations?.Clear();
            AutomaticOutfitManagerGameComponent.Current?
                .InvalidateWeaponStateIndex();
        }

        public void MarkWeaponPlayerOverride(bool explicitRuleOverride = false)
        {
            WeaponPlayerOverride = true;
            WeaponRuleOverrideExplicit |= explicitRuleOverride;
            WeaponRestorationRequested = false;
        }

        public void AbandonWeaponManagementForOverride(
            bool explicitRuleOverride = false)
        {
            CompleteWeaponRestoration();
            WeaponPlayerOverride = true;
            WeaponRuleOverrideExplicit = explicitRuleOverride;
        }
    }

    public sealed class RejectedWeaponPreparation : IExposable
    {
        public ThingWithComps Weapon;
        public string AvailabilitySignature;

        public void ExposeData()
        {
            Scribe_References.Look(ref Weapon, "weapon");
            Scribe_Values.Look(ref AvailabilitySignature,
                "availabilitySignature");
        }
    }

    public sealed class NestedRuleBufferState : IExposable
    {
        public string RuleId;
        public int Completed;
        public bool Finished;
        public int LastJobLoadId = -1;
        public string LastJobLabel;
        public int PendingJobLoadId = -1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref RuleId, "ruleId");
            Scribe_Values.Look(ref Completed, "completed", 0);
            Scribe_Values.Look(ref Finished, "finished", false);
            Scribe_Values.Look(ref LastJobLoadId, "lastJobLoadId", -1);
            Scribe_Values.Look(ref LastJobLabel, "lastJobLabel");
            Scribe_Values.Look(ref PendingJobLoadId, "pendingJobLoadId", -1);
        }
    }
}
