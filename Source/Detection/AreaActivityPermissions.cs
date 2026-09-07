using AutomaticOutfitManager.Rules;

namespace AutomaticOutfitManager.Detection
{
    internal enum AccessGroup { Colonist, Robot, Animal, Guest, Slave, Prisoner }
    internal enum AccessActivity { Activities, Hauling, Wandering }

    internal static class AreaActivityPermissions
    {
        internal static AccessGroup Group(bool prisoner, bool slave, bool robot, bool animal, bool guest) =>
            prisoner ? AccessGroup.Prisoner : slave ? AccessGroup.Slave :
            robot ? AccessGroup.Robot : animal ? AccessGroup.Animal :
            guest ? AccessGroup.Guest : AccessGroup.Colonist;

        internal static bool Allows(ApparelRule rule, AccessGroup group, AccessActivity activity)
        {
            if (rule == null) return false;
            switch (group)
            {
                case AccessGroup.Colonist:
                    return activity == AccessActivity.Hauling ? rule.AllowColonistHauling :
                        activity == AccessActivity.Wandering ? rule.AllowColonistWandering : rule.AllowColonistWork;
                case AccessGroup.Robot:
                    return activity == AccessActivity.Hauling ? rule.AllowRobotHauling :
                        activity == AccessActivity.Wandering ? rule.AllowRobotWandering : rule.AllowRobotWork;
                case AccessGroup.Animal:
                    return activity == AccessActivity.Hauling ? rule.AllowAnimalHauling :
                        activity == AccessActivity.Wandering ? rule.AllowAnimalWandering : rule.AllowAnimalWork;
                case AccessGroup.Guest:
                    return activity == AccessActivity.Hauling ? rule.AllowGuestHauling :
                        activity == AccessActivity.Wandering ? rule.AllowGuestWandering : rule.AllowGuestWork;
                case AccessGroup.Slave:
                    return activity == AccessActivity.Hauling ? rule.AllowSlaveHauling :
                        activity == AccessActivity.Wandering ? rule.AllowSlaveWandering : rule.AllowSlaveWork;
                case AccessGroup.Prisoner:
                    return activity == AccessActivity.Hauling ? rule.AllowPrisonerHauling :
                        activity == AccessActivity.Wandering ? rule.AllowPrisonerWandering : rule.AllowPrisonerWork;
                default: return false;
            }
        }
    }
}
