using System;
using System.Collections.Generic;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using Verse;

namespace Verse
{
    public class Map { }
    public class Area
    {
        public Map Map;
        public HashSet<int> Cells = new HashSet<int>();
        public IEnumerable<int> ActiveCells => Cells;
        public int TrueCount => Cells.Count;
        public bool this[int cell] => Cells.Contains(cell);
    }
}
namespace AutomaticOutfitManager.Rules
{
    public class ApparelRule
    {
        public bool Enabled = true, IsNonWork;
        public Area Area;
    }
}
class AreaNestingPolicyTests
{
    static int checks;
    static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        checks++;
    }
    static Area Area(Map map, params int[] cells) => new Area { Map = map, Cells = new HashSet<int>(cells) };
    static int Main()
    {
        try
        {
            var map = new Map();
            var work = new ApparelRule { Area = Area(map, 2, 3) };
            var nonWork = new ApparelRule { IsNonWork = true, Area = Area(map, 1, 2, 3, 4) };
            var rules = new List<ApparelRule> { work, nonWork };
            Check(AreaNestingPolicy.Conflict(work, rules) == nonWork, "Work assignment rejected");
            Check(AreaNestingPolicy.Conflict(nonWork, rules) == work, "Non-Work assignment rejected");
            Check(AreaNestingPolicy.ContainsWorkArea(work.Area, work.Area), "same area rejected");
            Check(AreaNestingPolicy.ContainsWorkArea(Area(map, 2, 3), work.Area), "equal shapes rejected");
            Check(!AreaNestingPolicy.ContainsWorkArea(work.Area, nonWork.Area), "reverse containment allowed");
            Check(!AreaNestingPolicy.ContainsWorkArea(Area(map, 1, 2, 4), work.Area), "holes respected");
            Check(!AreaNestingPolicy.ContainsWorkArea(Area(map, 1, 2), work.Area), "partial overlap allowed");
            Check(!AreaNestingPolicy.ContainsWorkArea(Area(map, 8, 9), work.Area), "disjoint allowed");
            Check(!AreaNestingPolicy.ContainsWorkArea(nonWork.Area, Area(map)), "empty inner allowed");
            Check(!AreaNestingPolicy.ContainsWorkArea(Area(map), work.Area), "empty outer allowed");
            Check(!AreaNestingPolicy.ContainsWorkArea(nonWork.Area, Area(new Map(), 2, 3)), "map identity matters");
            Check(!AreaNestingPolicy.ContainsWorkArea(Area(null, 2, 3), Area(null, 2, 3)), "missing maps allowed");
            Check(!AreaNestingPolicy.ContainsWorkArea(null, work.Area), "missing outer");
            Check(!AreaNestingPolicy.ContainsWorkArea(nonWork.Area, null), "missing inner");
            nonWork.Enabled = false;
            Check(AreaNestingPolicy.Conflict(work, rules) == null, "disabled counterpart allowed");
            Check(AreaNestingPolicy.Conflict(nonWork, rules) == work, "disabled candidate enable check");
            nonWork.Enabled = true;
            int recalls = 0;
            AreaNestingPolicy.DisableNestedWorkRules(rules, (w, n) => {
                Check(w == work && n == nonWork && !w.Enabled, "correct disable notification");
                recalls++;
            });
            Check(!work.Enabled && nonWork.Enabled && recalls == 1, "only inner Work disabled");
            AreaNestingPolicy.DisableNestedWorkRules(rules, (w, n) => recalls++);
            Check(recalls == 1, "no repeated recall or warning");
            Check(AreaNestingPolicy.Conflict(work, rules) == nonWork, "disabled Work retains conflict explanation");
            work.Area.Cells.Add(9);
            Check(AreaNestingPolicy.Conflict(work, rules) == null, "repaint resolves conflict immediately");
            Check(!work.Enabled, "resolution does not auto-enable");
            work.Enabled = true;
            work.Area.Cells.Remove(9);
            nonWork.IsNonWork = false;
            Check(AreaNestingPolicy.Conflict(work, rules) == null, "Work inside Work allowed");
            work.IsNonWork = true; nonWork.IsNonWork = true;
            Check(AreaNestingPolicy.Conflict(work, rules) == null, "Non-Work inside Non-Work allowed");
            Check(AreaNestingPolicy.Conflict(null, rules) == null, "null candidate");
            Check(AreaNestingPolicy.Conflict(work, null) == null, "null rules");
            Console.WriteLine($"PASS {checks} area nesting checks");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex); return 1; }
    }
}
