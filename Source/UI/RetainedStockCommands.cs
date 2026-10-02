using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using RimWorld;
using Verse;

namespace AutomaticOutfitManager.UI
{
    internal static class RetainedStockCommands
    {
        internal static Command_Action ClearRetained(ThingWithComps item)
        {
            var component = AutomaticOutfitManagerGameComponent.Current;
            ThingDef def = item?.def;
            if (component == null || def == null || item.Destroyed) return null;

            bool remembered = def.apparel != null
                ? component.ManagedApparelStockDefs?.Contains(def) == true
                : def.IsWeapon && component.ManagedWeaponStockDefs?.Contains(def) == true;
            if (!GearSelectionPolicy.IsRetained(def, remembered, component.Rules)) return null;

            var command = new Command_Action
            {
                defaultLabel = def.apparel != null
                    ? "Forget retained apparel"
                    : "Forget retained weapon",
                defaultDesc = $"Forget retained stock of type {def.LabelCap}, for all items of this type. " +
                    "This is the same as Forget in the apparel or weapon selector. " +
                    "Saved personal items and borrowed gear stay protected; normal storage filters still apply.",
                icon = TexCommand.ForbidOn,
                // Recheck through the same guarded operation used by selectors:
                // a rule or outfit may have claimed this type since the gizmo drew.
                action = () => component.ForgetManagedStockDefinition(def)
            };
            if (!component.CanForgetManagedStockDefinition(def))
                command.Disable(component.ManagedStockForgetBlockReason(def) ??
                    "A rule or current outfit change still uses this type.");
            return command;
        }
    }
}
