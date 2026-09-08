using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Rules;
using Verse;

namespace AutomaticOutfitManager.Detection
{
    // Configuration advice only. Never changes painted cells, rule enablement,
    // source-rule retrieval exemptions, or a pawn's actual changing-cell search.
    internal sealed class LockerAreaAssessment
    {
        internal readonly List<IntVec3> OverlapCells = new List<IntVec3>();
        internal readonly List<IntVec3> OutsideCells = new List<IntVec3>();
        internal readonly List<ApparelRule> WorkRules = new List<ApparelRule>();
        internal readonly List<ApparelRule> NonWorkRules = new List<ApparelRule>();
        internal readonly List<IntVec3> NonWorkOverlapCells = new List<IntVec3>();
        internal int PaintedCount;
        internal int StandableOutsideCount;

        internal static LockerAreaAssessment Inspect(Area locker, IEnumerable<ApparelRule> rules)
        {
            var result = new LockerAreaAssessment();
            if (locker?.Map == null) return result;
            var work = rules?.Where(rule => rule?.Enabled == true && !rule.IsNonWork &&
                rule.Area?.Map == locker.Map).ToList() ?? new List<ApparelRule>();
            var contributors = new HashSet<ApparelRule>();
            var nonWork = rules?.Where(rule => rule?.Enabled == true && rule.IsNonWork &&
                rule.Area?.Map == locker.Map).ToList() ?? new List<ApparelRule>();
            var nonWorkContributors = new HashSet<ApparelRule>();
            foreach (var cell in locker.ActiveCells)
            {
                if (!cell.IsValid || !cell.InBounds(locker.Map)) continue;
                result.PaintedCount++;
                bool nonWorkOverlap = false;
                foreach (var rule in nonWork)
                {
                    if (!rule.Area[cell]) continue;
                    nonWorkContributors.Add(rule);
                    nonWorkOverlap = true;
                }
                if (nonWorkOverlap) result.NonWorkOverlapCells.Add(cell);
                bool overlaps = false;
                foreach (var rule in work)
                {
                    if (!rule.Area[cell]) continue;
                    contributors.Add(rule);
                    overlaps = true;
                }
                if (overlaps) result.OverlapCells.Add(cell);
                else
                {
                    result.OutsideCells.Add(cell);
                    if (cell.Standable(locker.Map)) result.StandableOutsideCount++;
                }
            }
            result.WorkRules.AddRange(work.Where(contributors.Contains));
            result.NonWorkRules.AddRange(nonWork.Where(nonWorkContributors.Contains));
            return result;
        }
    }
}
