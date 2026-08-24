using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Rules;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    public static class UnavailableWorkRegistry
    {
        private sealed class Entry
        {
            public string RuleId;
            public int UntilTick;
            public JobDef JobDef;
            public List<Thing> Things;
            public List<IntVec3> Cells;

            public bool ExactJob => JobDef != null;
        }

        private static readonly Dictionary<int, List<Entry>> Entries =
            new Dictionary<int, List<Entry>>();

        public static void Block(Pawn pawn, ApparelRule rule, int ticks = 1200)
        {
            if (pawn == null || rule == null)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (!Entries.TryGetValue(pawn.thingIDNumber, out List<Entry> pawnEntries))
            {
                pawnEntries = new List<Entry>();
                Entries[pawn.thingIDNumber] = pawnEntries;
            }

            Entry entry = pawnEntries.FirstOrDefault(item =>
                item.RuleId == rule.Id && !item.ExactJob);
            if (entry == null)
                pawnEntries.Add(new Entry { RuleId = rule.Id, UntilTick = now + ticks });
            else
                entry.UntilTick = now + ticks;
        }

        public static void Block(
            Pawn pawn, ApparelRule rule, Job rejectedJob, int ticks = 1200)
        {
            if (pawn?.Map == null || rule == null || rejectedJob?.def == null)
                return;

            ExtractTargets(pawn, rejectedJob, out List<Thing> things,
                out List<IntVec3> cells);
            if (things.Count == 0 && cells.Count == 0)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (!Entries.TryGetValue(pawn.thingIDNumber, out List<Entry> pawnEntries))
            {
                pawnEntries = new List<Entry>();
                Entries[pawn.thingIDNumber] = pawnEntries;
            }

            Entry entry = pawnEntries.FirstOrDefault(item =>
                item.RuleId == rule.Id && item.JobDef == rejectedJob.def &&
                TargetsOverlap(item, pawn.Map, things, cells));
            if (entry == null)
            {
                pawnEntries.Add(new Entry
                {
                    RuleId = rule.Id,
                    UntilTick = now + ticks,
                    JobDef = rejectedJob.def,
                    Things = things,
                    Cells = cells
                });
            }
            else
            {
                entry.UntilTick = now + ticks;
            }
        }

        public static void Clear(Pawn pawn, IEnumerable<ApparelRule> rules)
        {
            if (pawn == null || rules == null ||
                !Entries.TryGetValue(pawn.thingIDNumber, out List<Entry> pawnEntries))
                return;

            var ids = new HashSet<string>(rules.Where(rule => rule != null).Select(rule => rule.Id));
            pawnEntries.RemoveAll(entry => ids.Contains(entry.RuleId));
            if (pawnEntries.Count == 0)
                Entries.Remove(pawn.thingIDNumber);
        }

        public static bool ShouldReject(Pawn pawn, Job job)
        {
            if (pawn?.Map == null || job == null ||
                !Entries.TryGetValue(pawn.thingIDNumber, out List<Entry> pawnEntries))
                return false;

            int now = Find.TickManager?.TicksGame ?? 0;
            pawnEntries.RemoveAll(entry => entry.UntilTick <= now);
            if (pawnEntries.Count == 0)
            {
                Entries.Remove(pawn.thingIDNumber);
                return false;
            }

            AutomaticOutfitManagerGameComponent component = AutomaticOutfitManagerGameComponent.Current;
            return pawnEntries.Any(entry =>
            {
                ApparelRule rule = component?.RuleById(entry.RuleId);
                if (rule?.Enabled != true || rule.Area?.Map != pawn.Map)
                    return false;

                return entry.ExactJob
                    ? ExactJobMatches(entry, pawn, job)
                    : RuleEvaluator.JobTargetsArea(job, rule.Area);
            });
        }

        public static bool ShouldReject(
            Pawn pawn, Map map, Thing thing, IntVec3 cell)
        {
            if (pawn?.Map == null || map == null ||
                !Entries.TryGetValue(pawn.thingIDNumber, out List<Entry> pawnEntries))
            {
                return false;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            pawnEntries.RemoveAll(entry => entry.UntilTick <= now);
            if (pawnEntries.Count == 0)
            {
                Entries.Remove(pawn.thingIDNumber);
                return false;
            }

            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            return pawnEntries.Any(entry =>
            {
                ApparelRule rule = component?.RuleById(entry.RuleId);
                if (rule?.Enabled != true || rule.Area?.Map != map)
                    return false;

                if (entry.ExactJob)
                    return TargetMatches(entry, map, thing, cell);
                return cell.IsValid && cell.InBounds(map) && rule.Area[cell];
            });
        }

        private static bool ExactJobMatches(Entry entry, Pawn pawn, Job job)
        {
            if (entry?.JobDef != job?.def || pawn?.Map == null)
                return false;

            ExtractTargets(pawn, job, out List<Thing> things,
                out List<IntVec3> cells);
            return TargetsOverlap(entry, pawn.Map, things, cells);
        }

        private static bool TargetsOverlap(
            Entry entry, Map map, IEnumerable<Thing> things,
            IEnumerable<IntVec3> cells)
        {
            return things.Any(thing => TargetMatches(
                       entry, thing?.MapHeld ?? map, thing,
                       thing?.PositionHeld ?? IntVec3.Invalid)) ||
                   cells.Any(cell => TargetMatches(entry, map, null, cell));
        }

        private static bool TargetMatches(
            Entry entry, Map map, Thing thing, IntVec3 cell)
        {
            if (entry == null || map == null)
                return false;
            if (thing != null)
                return entry.Things?.Contains(thing) == true;
            return cell.IsValid && entry.Cells?.Contains(cell) == true;
        }

        private static void ExtractTargets(
            Pawn pawn, Job job, out List<Thing> things, out List<IntVec3> cells)
        {
            things = new List<Thing>();
            cells = new List<IntVec3>();
            if (pawn?.Map == null || job == null)
                return;

            foreach (LocalTargetInfo target in EnumerateTargets(job))
            {
                if (!target.IsValid)
                    continue;

                if (target.HasThing && target.Thing != null)
                {
                    Thing thing = target.Thing;
                    if (!thing.Destroyed && !things.Contains(thing))
                        things.Add(thing);
                    continue;
                }

                IntVec3 cell = target.Cell;
                if (cell.IsValid && cell.InBounds(pawn.Map) && !cells.Contains(cell))
                    cells.Add(cell);
            }
        }

        private static IEnumerable<LocalTargetInfo> EnumerateTargets(Job job)
        {
            yield return job.targetA;
            yield return job.targetB;
            yield return job.targetC;

            if (job.targetQueueA != null)
            {
                foreach (LocalTargetInfo target in job.targetQueueA)
                    yield return target;
            }

            if (job.targetQueueB != null)
            {
                foreach (LocalTargetInfo target in job.targetQueueB)
                    yield return target;
            }
        }
    }
}
