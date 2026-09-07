using System;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;

internal static class AreaActivityPermissionsTests
{
    static int passed;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); passed++; }
    public static int Main()
    {
        try
        {
            foreach (bool nonWork in new[] { false, true })
            foreach (AccessGroup group in Enum.GetValues(typeof(AccessGroup)))
            for (int mask = 0; mask < 8; mask++)
            {
                var rule = new ApparelRule { IsNonWork = nonWork };
                // Other groups deliberately receive opposite values so a
                // fallback to Colonists/Guests cannot accidentally pass.
                foreach (AccessGroup other in Enum.GetValues(typeof(AccessGroup)))
                {
                    bool own = other == group;
                    typeof(ApparelRule).GetField("Allow" + other + "Work").SetValue(rule, own == ((mask & 1) != 0));
                    typeof(ApparelRule).GetField("Allow" + other + "Hauling").SetValue(rule, own == ((mask & 2) != 0));
                    typeof(ApparelRule).GetField("Allow" + other + "Wandering").SetValue(rule, own == ((mask & 4) != 0));
                }
                Check(AreaActivityPermissions.Allows(rule, group, AccessActivity.Activities) == ((mask & 1) != 0), group + " Activities independent");
                Check(AreaActivityPermissions.Allows(rule, group, AccessActivity.Hauling) == ((mask & 2) != 0), group + " Hauling independent");
                Check(AreaActivityPermissions.Allows(rule, group, AccessActivity.Wandering) == ((mask & 4) != 0), group + " Wandering independent");
            }
            Check(AreaActivityPermissions.Group(false, false, false, false, true) == AccessGroup.Guest, "human guest");
            Check(AreaActivityPermissions.Group(false, true, false, false, true) == AccessGroup.Slave, "slave beats stale guest flag");
            Check(AreaActivityPermissions.Group(true, false, false, false, true) == AccessGroup.Prisoner, "prisoner beats stale guest flag");
            Check(AreaActivityPermissions.Group(false, false, true, true, true) == AccessGroup.Robot, "animal-like visiting robot uses Robots");
            Check(AreaActivityPermissions.Group(false, false, false, true, true) == AccessGroup.Animal, "visiting pack animal uses Animals");
            Check(AreaActivityPermissions.Group(false, false, false, false, false) == AccessGroup.Colonist, "ordinary colonist");
            Check(!AreaActivityPermissions.Allows(null, AccessGroup.Guest, AccessActivity.Activities), "missing rule grants nothing");
            Console.WriteLine("Passed " + passed + " area permission checks.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
namespace AutomaticOutfitManager.Rules
{
    public class ApparelRule
    {
        public bool IsNonWork;
        public bool AllowColonistWork;
        public bool AllowColonistHauling;
        public bool AllowColonistWandering;
        public bool AllowRobotWork;
        public bool AllowRobotHauling;
        public bool AllowRobotWandering;
        public bool AllowAnimalWork;
        public bool AllowAnimalHauling;
        public bool AllowAnimalWandering;
        public bool AllowGuestWork;
        public bool AllowGuestHauling;
        public bool AllowGuestWandering;
        public bool AllowSlaveWork;
        public bool AllowSlaveHauling;
        public bool AllowSlaveWandering;
        public bool AllowPrisonerWork;
        public bool AllowPrisonerHauling;
        public bool AllowPrisonerWandering;
    }
}
