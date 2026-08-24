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
        public bool ApparelInterventionActive = true;
        public ThingWithComps OriginalWeapon;
        public List<ThingWithComps> ManagedWeapons = new List<ThingWithComps>();
        public bool WeaponInterventionActive;
        public bool WeaponRestorationRequested;
        public bool WeaponPlayerOverride;
        public ApparelTransition Transition = ApparelTransition.Preparing;
        public int StartedTick;
        public int LastRestorationAttemptTick = -1;
        public int UnavailableRestorationAttempts;
        public bool RecallRequested;
        public bool RecallInterruptPending;
        public int LastRecallInterruptAttemptTick = -1;
        public int BufferedTasksCompleted;
        public int LastBufferedJobLoadId = -1;
        public int LastChangingAreaReturnAttemptTick = -1;
        public int ActiveIdleTicks;
        public Job PendingWorkJob;
        public bool PendingWorkIsManagedWork;
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
            Scribe_Values.Look(ref ApparelInterventionActive,
                "apparelInterventionActive", true);
            Scribe_References.Look(ref OriginalWeapon, "originalWeapon");
            Scribe_Collections.Look(ref ManagedWeapons, "managedWeapons", LookMode.Reference);
            Scribe_Values.Look(ref WeaponInterventionActive, "weaponInterventionActive", false);
            Scribe_Values.Look(ref WeaponRestorationRequested, "weaponRestorationRequested", false);
            Scribe_Values.Look(ref WeaponPlayerOverride, "weaponPlayerOverride", false);
            Scribe_Values.Look(ref Transition, "transition", ApparelTransition.Preparing);
            Scribe_Values.Look(ref StartedTick, "startedTick");
            Scribe_Values.Look(ref LastRestorationAttemptTick, "lastRestorationAttemptTick", -1);
            Scribe_Values.Look(ref UnavailableRestorationAttempts, "unavailableRestorationAttempts");
            Scribe_Values.Look(ref RecallRequested, "recallRequested", false);
            // RC saves briefly persisted a Recall hold that no longer belongs
            // to the Phase 3 contract. Read it only to recover an already-stuck
            // pawn, then discard it rather than preserving the retired state.
            Scribe_Values.Look(ref retiredIndividualRecallRequested,
                "individualRecallRequested", false);
            Scribe_Values.Look(ref RecallInterruptPending, "recallInterruptPending", false);
            Scribe_Values.Look(ref LastRecallInterruptAttemptTick, "lastRecallInterruptAttemptTick", -1);
            Scribe_Values.Look(ref BufferedTasksCompleted, "bufferedTasksCompleted", 0);
            Scribe_Values.Look(ref LastBufferedJobLoadId, "lastBufferedJobLoadId", -1);
            Scribe_Values.Look(ref LastChangingAreaReturnAttemptTick, "lastChangingAreaReturnAttemptTick", -1);
            Scribe_Values.Look(ref ActiveIdleTicks, "activeIdleTicks", 0);
            Scribe_Deep.Look(ref PendingWorkJob, "pendingWorkJob");
            Scribe_Values.Look(ref PendingWorkIsManagedWork, "pendingWorkIsManagedWork", false);
            Scribe_Values.Look(ref LastManagedWorkJobDefName, "lastManagedWorkJobDefName");
            Scribe_Collections.Look(ref NestedRuleBuffers, "nestedRuleBuffers", LookMode.Deep);
            Scribe_Values.Look(ref LastNestedBufferStatus, "lastNestedBufferStatus");
            OriginalApparel ??= new List<Apparel>();
            ManagedApparel ??= new List<Apparel>();
            ManagedWeapons ??= new List<ThingWithComps>();
            CurrentRuleIds ??= new List<string>();
            NestedRuleBuffers ??= new List<NestedRuleBufferState>();
            if (Scribe.mode == LoadSaveMode.PostLoadInit &&
                retiredIndividualRecallRequested && RecallRequested &&
                Transition != ApparelTransition.ReturningToChangingArea &&
                Transition != ApparelTransition.Restoring)
            {
                RecallInterruptPending = true;
                LastRecallInterruptAttemptTick = -1;
                retiredIndividualRecallRequested = false;
            }
        }

        public void AddManagedApparel(IEnumerable<Apparel> apparel)
        {
            if (apparel == null)
                return;

            foreach (Apparel item in apparel.Where(item => item != null))
            {
                if (!ManagedApparel.Contains(item))
                    ManagedApparel.Add(item);
            }
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
            }

            WeaponRestorationRequested = false;
            if (!ManagedWeapons.Contains(weapon))
                ManagedWeapons.Add(weapon);
            AutomaticOutfitManagerGameComponent.Current?
                .InvalidateWeaponStateIndex();
        }

        public bool IsManagedWeapon(ThingWithComps weapon) =>
            weapon != null && ManagedWeapons?.Contains(weapon) == true;

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
            AutomaticOutfitManagerGameComponent.Current?
                .InvalidateWeaponStateIndex();
        }

        public void MarkWeaponPlayerOverride()
        {
            WeaponPlayerOverride = true;
            WeaponRestorationRequested = false;
        }

        public void AbandonWeaponManagementForOverride()
        {
            CompleteWeaponRestoration();
            WeaponPlayerOverride = true;
        }
    }

    public sealed class NestedRuleBufferState : IExposable
    {
        public string RuleId;
        public int Completed;
        public bool Finished;
        public int LastJobLoadId = -1;
        public string LastJobLabel;

        public void ExposeData()
        {
            Scribe_Values.Look(ref RuleId, "ruleId");
            Scribe_Values.Look(ref Completed, "completed", 0);
            Scribe_Values.Look(ref Finished, "finished", false);
            Scribe_Values.Look(ref LastJobLoadId, "lastJobLoadId", -1);
            Scribe_Values.Look(ref LastJobLabel, "lastJobLabel");
        }
    }
}
