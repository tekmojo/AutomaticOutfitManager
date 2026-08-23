using AutomaticOutfitManager.Core;
using Verse;

namespace AutomaticOutfitManager.Storage
{
    public static class ManagedWeaponClassifier
    {
        public static bool Matches(ThingDef def)
        {
            if (def?.IsWeapon != true)
                return false;

            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            return component?.IsManagedWeaponDefinition(def) == true;
        }

        public static bool Matches(Thing thing)
        {
            if (thing is not ThingWithComps weapon ||
                weapon.def?.IsWeapon != true)
            {
                return false;
            }

            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            if (component == null)
                return false;

            return component.IsManagedWeaponDefinition(weapon.def) ||
                   component.IsTrackedWeapon(weapon);
        }
    }
}
