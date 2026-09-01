using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    /// <summary>
    /// Remembers a concrete job whose real, late-bound path reached a protected
    /// boundary even though its StartJob targets did not expose that area.
    /// Ingest is the important vanilla case: food can be carried outside while
    /// the job driver chooses a dining cell inside only after the job starts.
    /// </summary>
    public static class ProtectedBoundaryRetryRegistry
    {
        private const int RetryLifetimeTicks = 15000;

        private sealed class Entry
        {
            public Pawn Pawn;
            public Map Map;
            public string RuleId;
            public JobDef JobDef;
            public Job InterruptedJob;
            public List<Thing> Things;
            public List<IntVec3> Cells;
            public int UntilTick;
        }

        private static readonly List<Entry> Entries = new List<Entry>();

        public static void ResetForLoadedGame()
        {
            // Entries are runtime observations. Persisted pending continuations
            // retain their discovered rule IDs in PawnApparelState instead.
            Entries.Clear();
        }

        public static void Record(Pawn pawn, Job job, ApparelRule rule)
        {
            if (pawn?.Map == null || job?.def == null || rule?.Enabled != true ||
                rule.Area?.Map != pawn.Map)
            {
                return;
            }

            ExtractTargets(pawn, job, out List<Thing> things,
                out List<IntVec3> cells);
            // Do not broaden a targetless activity solely by job definition.
            // A concrete Thing or cell is required to identify the native retry.
            if (things.Count == 0 && cells.Count == 0)
                return;

            Cleanup();
            Entry entry = Entries.FirstOrDefault(candidate =>
                candidate.Pawn == pawn && candidate.Map == pawn.Map &&
                candidate.RuleId == rule.Id && candidate.JobDef == job.def &&
                TargetsOverlap(candidate, things, cells));
            int untilTick = CurrentTick + RetryLifetimeTicks;
            if (entry != null)
            {
                entry.InterruptedJob = job;
                entry.UntilTick = untilTick;
                return;
            }

            Entries.Add(new Entry
            {
                Pawn = pawn,
                Map = pawn.Map,
                RuleId = rule.Id,
                JobDef = job.def,
                InterruptedJob = job,
                Things = things,
                Cells = cells,
                UntilTick = untilTick
            });
        }

        public static bool TryGetPendingInterruption(
            Pawn pawn, out Job job, out List<ApparelRule> rules)
        {
            job = null;
            rules = new List<ApparelRule>();
            if (pawn?.Map == null)
                return false;

            Cleanup();
            Entry pending = Entries
                .Where(entry => entry.Pawn == pawn &&
                                entry.Map == pawn.Map &&
                                entry.InterruptedJob?.def != null)
                .OrderByDescending(entry => entry.UntilTick)
                .FirstOrDefault();
            if (pending == null)
                return false;

            Job pendingJob = pending.InterruptedJob;
            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            rules = Entries.Where(entry =>
                    entry.Pawn == pawn && entry.Map == pawn.Map &&
                    ReferenceEquals(entry.InterruptedJob, pendingJob))
                .Select(entry => component?.RuleById(entry.RuleId))
                .Where(rule => rule?.Enabled == true &&
                               rule.Area?.Map == pawn.Map &&
                               RuleEvaluator.RuleCanApplyToPawn(pawn, rule) &&
                               PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(
                                   pawn, pendingJob, rule))
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
            if (rules.Count > 0)
            {
                job = pendingJob;
                return true;
            }

            job = null;
            return false;
        }

        public static List<ApparelRule> MatchingRules(Pawn pawn, Job job)
        {
            if (pawn?.Map == null || job?.def == null)
                return new List<ApparelRule>();

            Cleanup();
            ExtractTargets(pawn, job, out List<Thing> things,
                out List<IntVec3> cells);
            if (things.Count == 0 && cells.Count == 0)
                return new List<ApparelRule>();

            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            return Entries.Where(entry =>
                    entry.Pawn == pawn && entry.Map == pawn.Map &&
                    entry.JobDef == job.def &&
                    TargetsOverlap(entry, things, cells))
                .Select(entry => component?.RuleById(entry.RuleId))
                .Where(rule => rule?.Enabled == true &&
                               rule.Area?.Map == pawn.Map &&
                               RuleEvaluator.RuleCanApplyToPawn(pawn, rule) &&
                               PausedAreaWorkFilter.ActivityAllowedAtRuleBoundary(
                                   pawn, job, rule))
                .GroupBy(rule => rule.Id)
                .Select(group => group.First())
                .ToList();
        }

        public static void Clear(Pawn pawn, Job job)
        {
            if (pawn == null || job?.def == null || Entries.Count == 0)
                return;

            ExtractTargets(pawn, job, out List<Thing> things,
                out List<IntVec3> cells);
            Entries.RemoveAll(entry =>
                entry.Pawn == pawn && entry.JobDef == job.def &&
                TargetsOverlap(entry, things, cells));
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
                if (cell.IsValid && cell.InBounds(pawn.Map) &&
                    !cells.Contains(cell))
                {
                    cells.Add(cell);
                }
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

        private static bool TargetsOverlap(
            Entry entry, IEnumerable<Thing> things, IEnumerable<IntVec3> cells)
        {
            return entry != null &&
                   (things.Any(thing => entry.Things?.Contains(thing) == true) ||
                    cells.Any(cell => entry.Cells?.Contains(cell) == true));
        }

        private static void Cleanup()
        {
            if (Entries.Count == 0)
                return;

            int now = CurrentTick;
            Entries.RemoveAll(entry =>
                entry == null || entry.UntilTick <= now ||
                entry.Pawn?.Spawned != true || entry.Pawn.Map != entry.Map ||
                entry.JobDef == null ||
                entry.Things?.Any(thing => thing == null || thing.Destroyed) == true);
        }

        private static int CurrentTick => Find.TickManager?.TicksGame ?? 0;
    }
}
