using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.Storage;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    public static class RestorationPlanner
    {
        private const float TatteredHitPointThreshold = 0.5f;

        public static bool TryMakeHeldOriginalsAccessible(
            Pawn pawn, PawnApparelState state)
        {
            if (pawn?.Map == null || state == null)
                return false;

            if (state.Transition == ApparelTransition.Restoring)
                state.RequestWeaponRestoration();

            bool droppedAny = false;
            foreach (Apparel item in (state.OriginalApparel ?? new List<Apparel>()).Where(item =>
                         item != null && !item.Destroyed && !item.Spawned &&
                         pawn.apparel?.WornApparel.Contains(item) != true).ToList())
            {
                IThingHolder holder = item.ParentHolder;
                ThingOwner owner = holder?.GetDirectlyHeldThings();
                if (owner == null || !owner.Contains(item) ||
                    item.MapHeld != pawn.Map || !item.PositionHeld.IsValid)
                {
                    continue;
                }

                // Never pull a saved item out of somebody else's inventory.
                // Ownership enforcement will make that pawn release it through
                // its normal safe path instead.
                IThingHolder ancestor = holder;
                bool heldByOtherPawn = false;
                while (ancestor != null)
                {
                    if (ancestor is Pawn holdingPawn && holdingPawn != pawn)
                    {
                        heldByOtherPawn = true;
                        break;
                    }
                    ancestor = ancestor.ParentHolder;
                }
                if (heldByOtherPawn)
                    continue;

                IntVec3 dropCell = item.PositionHeld;
                try
                {
                    if (owner.TryDrop(
                            item, dropCell, pawn.Map, ThingPlaceMode.Near,
                            out Thing dropped) && dropped is Apparel droppedApparel)
                    {
                        if (droppedApparel.IsForbidden(pawn))
                            droppedApparel.SetForbidden(false, false);
                        droppedAny = true;
                        if (Prefs.DevMode)
                            Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: recovered saved apparel {droppedApparel.LabelCap} from an inventory or container.");
                        // Release one exact item at a time. Its Wear job can
                        // reserve it immediately, avoiding a pile of saved gear
                        // that storage or inventory mods may collect again while
                        // the pawn is still restoring earlier layers.
                        break;
                    }
                }
                catch (System.Exception exception)
                {
                    if (Prefs.DevMode)
                        Log.Warning($"[AutomaticOutfitManager] {pawn.LabelShortCap}: could not release saved apparel {item.LabelCap} from its holder. {exception.GetType().Name}: {exception.Message}");
                }
            }

            ThingWithComps originalWeapon = state.OriginalWeapon;
            if (originalWeapon != null && !originalWeapon.Destroyed &&
                pawn.equipment?.Primary != originalWeapon && !originalWeapon.Spawned)
            {
                droppedAny |= TryReleaseHeldWeapon(
                    pawn, originalWeapon, "saved weapon");
            }

            if (state.WeaponRestorationRequested && state.ManagedWeapons != null)
            {
                ThingWithComps currentWeapon = pawn.equipment?.Primary;
                if (currentWeapon != null && currentWeapon != state.OriginalWeapon &&
                    (state.IsManagedWeapon(currentWeapon) ||
                     state.WeaponPlayerOverride))
                {
                    try
                    {
                        // Simple Sidearms transpiles RimWorld's DropEquipment
                        // job and can retain the temporary primary instead of
                        // releasing it. Restoration already owns this exact
                        // tracked weapon, so use the equipment tracker directly
                        // and leave the mod's weapon memories untouched.
                        if (pawn.equipment.TryDropEquipment(
                                currentWeapon, out ThingWithComps droppedWeapon,
                                pawn.Position, false))
                        {
                            if (droppedWeapon?.Spawned == true &&
                                droppedWeapon.IsForbidden(pawn))
                            {
                                droppedWeapon.SetForbidden(false, false);
                            }
                            droppedAny = true;
                            if (Prefs.DevMode)
                            {
                                Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: returned temporary primary weapon {currentWeapon.LabelCap} for saved-outfit restoration.");
                            }
                        }
                    }
                    catch (System.Exception exception)
                    {
                        if (Prefs.DevMode)
                        {
                            Log.Warning($"[AutomaticOutfitManager] {pawn.LabelShortCap}: could not return temporary primary weapon {currentWeapon.LabelCap}; retrying safely. {exception.GetType().Name}: {exception.Message}");
                        }
                    }
                }

                foreach (ThingWithComps managedWeapon in state.ManagedWeapons.Where(weapon =>
                             weapon != null && !weapon.Destroyed && !weapon.Spawned &&
                             pawn.equipment?.Primary != weapon &&
                             IsHeldByPawn(weapon, pawn)).ToList())
                {
                    if (TryReleaseHeldWeapon(pawn, managedWeapon, "temporary managed weapon"))
                    {
                        droppedAny = true;
                        break;
                    }
                }
            }

            return droppedAny;
        }

        private static bool TryReleaseHeldWeapon(
            Pawn pawn, ThingWithComps weapon, string context)
        {
            if (pawn?.Map == null || weapon == null ||
                !TryFindContainingOwner(weapon, out IThingHolder holder,
                    out ThingOwner owner) ||
                !TryResolveHeldDropLocation(
                    pawn, weapon, holder, out Map dropMap,
                    out IntVec3 dropCell))
            {
                return false;
            }

            try
            {
                Thing dropped = null;
                bool released = owner.TryDrop(
                    weapon, dropCell, dropMap, ThingPlaceMode.Near,
                    out dropped);

                // Some inventory and container mods expose a valid ThingOwner
                // but reject its ordinary TryDrop path. This exact saved weapon
                // already belongs to the restoring pawn, so detach it once and
                // place it beside the owning pawn/container. Restore the holder
                // immediately if placement fails; never duplicate or discard it.
                if (!released && owner.Remove(weapon))
                {
                    if (GenPlace.TryPlaceThing(
                            weapon, dropCell, dropMap, ThingPlaceMode.Near))
                    {
                        dropped = weapon;
                        released = true;
                    }
                    else
                    {
                        owner.TryAdd(weapon);
                    }
                }

                if (!released || dropped is not ThingWithComps droppedWeapon)
                    return false;

                if (droppedWeapon.IsForbidden(pawn))
                    droppedWeapon.SetForbidden(false, false);
                if (Prefs.DevMode)
                {
                    Log.Message(
                        $"[AutomaticOutfitManager] {pawn.LabelShortCap}: recovered " +
                        $"{context} {droppedWeapon.LabelCap} from " +
                        $"{HolderDescription(holder)}.");
                }
                return true;
            }
            catch (System.Exception exception)
            {
                if (Prefs.DevMode)
                    Log.Warning($"[AutomaticOutfitManager] {pawn.LabelShortCap}: could not release {context} {weapon.LabelCap} from its holder. {exception.GetType().Name}: {exception.Message}");
            }

            return false;
        }

        private static bool TryFindContainingOwner(
            Thing thing, out IThingHolder containingHolder,
            out ThingOwner containingOwner)
        {
            containingHolder = null;
            containingOwner = null;
            for (IThingHolder holder = thing?.ParentHolder;
                 holder != null;
                 holder = holder.ParentHolder)
            {
                ThingOwner owner = holder.GetDirectlyHeldThings();
                if (owner?.Contains(thing) != true)
                    continue;

                containingHolder = holder;
                containingOwner = owner;
                return true;
            }

            return false;
        }

        private static bool TryResolveHeldDropLocation(
            Pawn pawn, Thing thing, IThingHolder directHolder,
            out Map map, out IntVec3 cell)
        {
            map = null;
            cell = IntVec3.Invalid;
            for (IThingHolder holder = directHolder;
                 holder != null;
                 holder = holder.ParentHolder)
            {
                if (holder is Pawn holdingPawn)
                {
                    if (holdingPawn != pawn)
                        return false;
                    if (pawn.Map != null && pawn.Position.IsValid &&
                        pawn.Position.InBounds(pawn.Map))
                    {
                        map = pawn.Map;
                        cell = pawn.Position;
                        return true;
                    }
                }

                if (holder is Thing holderThing &&
                    holderThing.MapHeld == pawn.Map &&
                    holderThing.PositionHeld.IsValid &&
                    holderThing.PositionHeld.InBounds(pawn.Map))
                {
                    map = pawn.Map;
                    cell = holderThing.PositionHeld;
                    return true;
                }
            }

            if (thing.MapHeld == pawn.Map && thing.PositionHeld.IsValid &&
                thing.PositionHeld.InBounds(pawn.Map))
            {
                map = pawn.Map;
                cell = thing.PositionHeld;
                return true;
            }

            return false;
        }

        internal static string HolderDescription(IThingHolder holder)
        {
            if (holder == null)
                return "an unavailable holder";
            if (holder is Pawn holdingPawn)
                return $"{holdingPawn.LabelShortCap}'s inventory or equipment";
            if (holder is Thing holderThing)
                return holderThing.LabelCap.ToString();

            IThingHolder ancestor = holder.ParentHolder;
            while (ancestor != null)
            {
                if (ancestor is Pawn ancestorPawn)
                    return $"{ancestorPawn.LabelShortCap}'s inventory or equipment";
                if (ancestor is Thing ancestorThing)
                    return ancestorThing.LabelCap.ToString();
                ancestor = ancestor.ParentHolder;
            }

            return holder.GetType().Name;
        }

        private static bool IsHeldByPawn(Thing thing, Pawn pawn)
        {
            IThingHolder holder = thing?.ParentHolder;
            while (holder != null)
            {
                if (holder is Pawn holdingPawn)
                    return holdingPawn == pawn;
                holder = holder.ParentHolder;
            }
            return false;
        }

        public static bool CanAttemptSavedWeaponEquip(
            ThingWithComps weapon,
            Pawn pawn,
            out string cantReason)
        {
            cantReason = null;
            if (weapon?.def?.IsWeapon != true || pawn == null)
            {
                cantReason = "not a valid weapon";
                return false;
            }

            // Use RimWorld's reason-producing overload directly. Some equipment
            // compatibility patches only intercept the two-argument convenience
            // overload and return false without a reason, even for the pawn's
            // exact previously equipped weapon. Vanilla supplies a reason for
            // every hard rejection. If a patch still returns a silent false,
            // let the real Equip job decide; the bounded failed-Equip recovery
            // prevents that attempt from becoming an endless Standing loop.
            bool canEquip = EquipmentUtility.CanEquip(
                weapon, pawn, out cantReason);
            return canEquip || string.IsNullOrEmpty(cantReason);
        }

        public static List<Job> BuildJobs(
            Pawn pawn,
            PawnApparelState state,
            ApparelRule activeRule,
            out bool hasUnavailableOriginal)
        {
            var jobs = new List<Job>();
            hasUnavailableOriginal = false;
            if (pawn?.apparel == null || state == null)
                return jobs;

            var original = new HashSet<Apparel>(state.OriginalApparel.Where(item => item != null));
            var automatic = new HashSet<Apparel>(state.ManagedApparel.Where(item => item != null));

            // Personal ownership always wins if a legacy or interrupted sibling
            // handoff recorded the same exact instance in both ledgers. Without
            // this defensive normalization BuildJobs alternates forever between
            // removing and wearing that one garment.
            automatic.ExceptWith(original);

            // Backward-compatible fallback for snapshots saved before automatic item
            // references were recorded explicitly.
            if (automatic.Count == 0 && activeRule?.RequiredApparel != null)
            {
                foreach (Apparel worn in pawn.apparel.WornApparel)
                {
                    if (!original.Contains(worn) && activeRule.RequiredApparel.Contains(worn.def))
                        automatic.Add(worn);
                }

                // Normalize an older snapshot before its removal jobs reach the
                // shared transition guards. Environmental protection and exact
                // transition ownership both depend on this per-pawn ledger.
                state.AddManagedApparel(automatic);
            }

            // Only apparel explicitly assigned by the intervention is removed.
            // Pawns can legitimately equip utility belts, weapons-as-apparel,
            // ideology items, or other non-work gear while a session is active.
            // Treating every post-snapshot item as automatic stripped those
            // unrelated slots during restoration.

            foreach (Apparel item in pawn.apparel.WornApparel.Where(automatic.Contains).ToList())
            {
                jobs.Add(JobMaker.MakeJob(JobDefOf.RemoveApparel, item));
            }

            var plannedReplacements = new HashSet<Apparel>();
            foreach (Apparel item in state.OriginalApparel)
            {
                if (item == null || item.Destroyed)
                    continue;

                Apparel replacement = FindBetterSavedApparelReplacement(
                    pawn, state, item, plannedReplacements);
                if (replacement != null)
                {
                    Job replacementJob = JobMaker.MakeJob(
                        JobDefOf.Wear, replacement);
                    // This is still Phase 3 ownership restoration. The Wear
                    // callback adopts the replacement only after the native job
                    // succeeds, then releases the one displaced saved item.
                    replacementJob.playerForced = true;
                    jobs.Add(replacementJob);
                    plannedReplacements.Add(replacement);
                    if (Prefs.DevMode)
                    {
                        Log.Message(
                            $"[AutomaticOutfitManager] {pawn.LabelShortCap}: " +
                            $"replacing tattered saved apparel {item.LabelCap} " +
                            $"with better available {replacement.LabelCap} " +
                            "during saved-outfit restoration.");
                    }
                    continue;
                }

                if (pawn.apparel.WornApparel.Contains(item))
                    continue;

                if (item.Spawned && item.IsForbidden(pawn))
                    item.SetForbidden(false, false);

                if (!item.Spawned || item.Map != pawn.Map || item.IsForbidden(pawn) ||
                    !pawn.CanReserve(item) || !pawn.CanReach(item, PathEndMode.ClosestTouch, Danger.Deadly))
                {
                    hasUnavailableOriginal = true;
                    continue;
                }

                Job wearJob = JobMaker.MakeJob(JobDefOf.Wear, item);
                // Restoring the exact captured outfit is an AutomaticOutfitManager
                // transition, not ordinary outfit optimization. Mark it forced
                // for the same reason as required work apparel: apparel policies
                // and compatibility patches must not repeatedly reject an item
                // the pawn was already wearing when the snapshot was taken.
                wearJob.playerForced = true;
                jobs.Add(wearJob);
            }

            List<Job> weaponJobs = BuildWeaponJobs(
                pawn, state, out bool hasUnavailableOriginalWeapon);
            jobs.AddRange(weaponJobs);
            hasUnavailableOriginal |= hasUnavailableOriginalWeapon;

            return jobs;
        }

        private static Apparel FindBetterSavedApparelReplacement(
            Pawn pawn,
            PawnApparelState state,
            Apparel saved,
            ISet<Apparel> excluded)
        {
            if (pawn?.Map?.listerThings == null || state == null ||
                saved == null || saved.Destroyed ||
                HitPointPercent(saved) >= TatteredHitPointThreshold)
            {
                return null;
            }

            float savedScore = SavedApparelReplacementPolicy.NativeScore(
                pawn, saved);
            if (savedScore == float.MinValue)
                return null;

            AutomaticOutfitManager.Core.AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManager.Core.AutomaticOutfitManagerGameComponent.Current;
            Apparel best = null;
            float bestScore = savedScore +
                SavedApparelReplacementPolicy.MinimumScoreGain;
            int bestDistance = int.MaxValue;
            foreach (Thing thing in pawn.Map.listerThings
                         .ThingsInGroup(ThingRequestGroup.Apparel))
            {
                if (thing is not Apparel candidate || candidate == saved ||
                    candidate.Destroyed || !candidate.Spawned ||
                    excluded?.Contains(candidate) == true ||
                    excluded?.Any(planned => planned != null &&
                        !ApparelUtility.CanWearTogether(
                            planned.def, candidate.def,
                            pawn.RaceProps?.body ?? BodyDefOf.Human)) == true ||
                    state.OriginalApparel?.Contains(candidate) == true ||
                    state.ManagedApparel?.Contains(candidate) == true ||
                    HitPointPercent(candidate) < TatteredHitPointThreshold ||
                    candidate.IsForbidden(pawn) || candidate.IsBurning() ||
                    ManagedApparelClassifier.Matches(candidate.def) ||
                    component?.IsSavedForOtherPawn(candidate, pawn) == true ||
                    component?.IsManagedApparelAssignedToOtherPawn(
                        candidate, pawn) == true ||
                    pawn.outfits?.CurrentApparelPolicy?.filter?.Allows(candidate) == false ||
                    !EquipmentUtility.CanEquip(candidate, pawn) ||
                    !ReservationUtility_SavedApparel_Patch
                        .CanReserveForOutfit(pawn, candidate) ||
                    !pawn.CanReach(
                        candidate, PathEndMode.ClosestTouch, Danger.Deadly))
                {
                    continue;
                }

                // A replacement may clear only the exact saved slot it improves.
                // This prevents a coat, armor layer, or modded multi-slot garment
                // from silently deleting several independent personal items.
                List<Apparel> displaced =
                    SavedApparelReplacementPolicy.ConflictingSavedApparel(
                            pawn, state, candidate)
                        .Where(item => item != null && !item.Destroyed)
                        .ToList();
                if (displaced.Count != 1 || displaced[0] != saved)
                    continue;

                float candidateScore =
                    SavedApparelReplacementPolicy.NativeScore(pawn, candidate);
                int distance = pawn.Position.DistanceToSquared(candidate.Position);
                if (candidateScore > bestScore ||
                    (candidateScore == bestScore && distance < bestDistance))
                {
                    best = candidate;
                    bestScore = candidateScore;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static float HitPointPercent(Apparel apparel)
        {
            return apparel?.MaxHitPoints > 0
                ? apparel.HitPoints / (float)apparel.MaxHitPoints
                : 1f;
        }

        public static List<Job> BuildWeaponJobs(
            Pawn pawn, PawnApparelState state, out bool hasUnavailableOriginal)
        {
            var jobs = new List<Job>();
            hasUnavailableOriginal = false;
            if (pawn?.equipment == null || state?.WeaponInterventionActive != true)
                return jobs;

            state.RequestWeaponRestoration();
            ThingWithComps current = pawn.equipment.Primary;
            ThingWithComps original = state.OriginalWeapon;

            bool managedHeldByPawn = (state.ManagedWeapons ?? new List<ThingWithComps>())
                .Any(weapon => weapon != null && !weapon.Destroyed &&
                               weapon != current && IsHeldByPawn(weapon, pawn));

            if ((current == original || (original == null && current == null)) &&
                !managedHeldByPawn)
            {
                state.CompleteWeaponRestoration();
                return jobs;
            }

            if (current != null && current != original &&
                !state.IsManagedWeapon(current) && !state.WeaponPlayerOverride)
            {
                state.AbandonWeaponManagementForOverride();
                return jobs;
            }

            if (managedHeldByPawn)
            {
                hasUnavailableOriginal = true;
                return jobs;
            }

            if (original != null && current != original)
            {
                if (original.Destroyed || !original.Spawned || original.Map != pawn.Map ||
                    original.IsForbidden(pawn) || !pawn.CanReserve(original) ||
                    !pawn.CanReach(original, PathEndMode.ClosestTouch, Danger.Deadly) ||
                    !CanAttemptSavedWeaponEquip(original, pawn, out _))
                {
                    hasUnavailableOriginal = true;
                    return jobs;
                }
            }

            if (current != null && current != original &&
                (state.IsManagedWeapon(current) || state.WeaponPlayerOverride))
                jobs.Add(JobMaker.MakeJob(JobDefOf.DropEquipment, current));

            if (original != null && current != original)
            {
                Job equipOriginal = JobMaker.MakeJob(JobDefOf.Equip, original);
                // Keep this false: Simple Sidearms interprets player-forced
                // equipment as a persistent preference. This is restoration of
                // an exact snapshot, not a new player weapon assignment.
                equipOriginal.playerForced = false;
                jobs.Add(equipOriginal);
            }

            return jobs;
        }
    }
}
