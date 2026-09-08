using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AutomaticOutfitManager.UI
{
    // Draw in the game's map update, not IMGUI's repaint. UI requests expire
    // when the tab stops drawing, including when another window replaces it.
    public sealed class LockerOverlapOverlay : GameComponent, ICellBoolGiver
    {
        private Map map;
        private HashSet<IntVec3> cells = new HashSet<IntVec3>();
        private List<IntVec3> edges = new List<IntVec3>();
        private CellBoolDrawer drawer;
        private int requestedFrame = -10;
        private Color color;
        private IEnumerable<IntVec3> source;
        public LockerOverlapOverlay(Game game) { }
        public Color Color => color;
        public bool GetCellBool(int index) => cells.Contains(map.cellIndices.IndexToCell(index));
        public Color GetCellExtraColor(int index) => Color.white;

        internal static void Request(Map map, IEnumerable<IntVec3> cells, Color color) =>
            Current.Game?.GetComponent<LockerOverlapOverlay>()?.Set(map, cells, color);

        internal static void Clear() => Current.Game?.GetComponent<LockerOverlapOverlay>()?.Set(null, null, Color.white);

        private void Set(Map requestedMap, IEnumerable<IntVec3> requestedCells, Color requestedColor)
        {
            requestedFrame = Time.frameCount;
            if (map == requestedMap && ReferenceEquals(source, requestedCells) && color == requestedColor) return;
            source = requestedCells;
            // Enumerated only when the UI requests a changed cached list. The
            // drawer's mesh rebuild is limited to actual geometry/color changes.
            var next = requestedCells == null ? new HashSet<IntVec3>() : new HashSet<IntVec3>(requestedCells);
            bool changedMap = map != requestedMap;
            bool changed = changedMap || !cells.SetEquals(next) || color != requestedColor;
            if (!changed) return;
            map = requestedMap;
            cells = next;
            edges = cells.ToList();
            color = requestedColor;
            if (map == null) { drawer = null; return; }
            if (drawer == null || changedMap) drawer = new CellBoolDrawer(this, map.Size.x, map.Size.z, 0.45f);
            drawer.SetDirty();
        }

        public override void GameComponentUpdate()
        {
            if (map == null || map != Find.CurrentMap || cells.Count == 0 ||
                Time.frameCount - requestedFrame > 1) return;
            drawer.MarkForDraw();
            drawer.CellBoolDrawerUpdate();
            GenDraw.DrawFieldEdges(edges, color);
        }
    }
}
