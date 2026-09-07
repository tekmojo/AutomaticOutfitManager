using UnityEngine;
using Verse;

namespace AutomaticOutfitManager.UI
{
    internal static class RuleConflictStyle
    {
        internal static readonly Color Color = new Color(0.65f, 0.65f, 0.65f);

        internal static void DrawBlockedButton(Rect rect, string reason)
        {
            bool previousEnabled = GUI.enabled;
            Color previousColor = GUI.color;
            GUI.enabled = false;
            GUI.color = Color;
            Widgets.ButtonText(rect, "Conflict");
            GUI.color = previousColor;
            GUI.enabled = previousEnabled;
            TooltipHandler.TipRegion(rect, reason);
        }
    }
}
