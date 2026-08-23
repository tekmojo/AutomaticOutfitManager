using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    public static class RestorationPlanner
    {
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
            IThingHolder holder = weapon?.ParentHolder;
            ThingOwner owner = holder?.GetDirectlyHeldThings();
            if (owner == null || !owner.Contains(weapon) ||
                weapon.MapHeld != pawn.Map || !weapon.PositionHeld.IsValid)
            {
                return false;
            }

            IThingHolder ancestor = holder;
            while (ancestor != null)
            {
                if (ancestor is Pawn holdingPawn && holdingPawn != pawn)
                    return false;
                ancestor = ancestor.ParentHolder;
            }

            try
            {
                if (owner.TryDrop(
                        weapon, weapon.PositionHeld, pawn.Map, ThingPlaceMode.Near,
                        out Thing dropped) && dropped is ThingWithComps droppedWeapon)
                {
                    if (droppedWeapon.IsForbidden(pawn))
                        droppedWeapon.SetForbidden(false, false);
                    if (Prefs.DevMode)
                        Log.Message($"[AutomaticOutfitManager] {pawn.LabelShortCap}: recovered {context} {droppedWeapon.LabelCap} from an inventory or container.");
                    return true;
                }
            }
            catch (System.Exception exception)
            {
                if (Prefs.DevMode)
                    Log.Warning($"[AutomaticOutfitManager] {pawn.LabelShortCap}: could not release {context} {weapon.LabelCap} from its holder. {exception.GetType().Name}: {exception.Message}");
            }

            return false;
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

            // Backward-compatible fallback for snapshots saved before automatic item
            // references were recorded explicitly.
            if (automatic.Count == 0 && activeRule?.RequiredApparel != null)
            {
                foreach (Apparel worn in pawn.apparel.WornApparel)
                {
                    if (!original.Contains(worn) && activeRule.RequiredApparel.Contains(worn.def))
                        automatic.Add(worn);
                }
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

            foreach (Apparel item in state.OriginalApparel)
            {
                if (item == null || item.Destroyed || pawn.apparel.WornApparel.Contains(item))
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
                    !EquipmentUtility.CanEquip(original, pawn))
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
