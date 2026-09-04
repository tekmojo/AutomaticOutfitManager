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
            public long RecordSequence;
        }

        private static readonly List<Entry> Entries = new List<Entry>();
        private static long nextRecordSequence;

        public static void ResetForLoadedGame()
        {
            // Entries are runtime observations. Persisted pending continuations
            // retain their discovered rule IDs in PawnApparelState instead.
            Entries.Clear();
            nextRecordSequence = 0;
        }

        public static void Record(Pawn pawn, Job job, ApparelRule rule)
        {
            if (pawn?.Map == null || job?.def == null || rule?.Enabled != true ||
                rule.Area?.Map != pawn.Map)
            {
                return;
            }

            // EndCurrentJob returns the running Job to RimWorld's pool and
            // clears its cached driver, definition, targets, and load ID. Never
            // retain that live object across the boundary interruption. Job.Clone
            // intentionally omits cached/last drivers; copy its mutable queues as
            // well so driver cleanup cannot alter the retained continuation.
            Job interruptedJob = DetachedClone(job);
            if (interruptedJob?.def == null)
                return;

            ExtractTargets(pawn, job, out List<Thing> things,
                out List<IntVec3> cells);
            // Do not broaden a targetless activity solely by job definition.
            // A concrete Thing or cell is required to identify the native retry.
            if (things.Count == 0 && cells.Count == 0)
                return;

            Cleanup();
            Entry root = Entries
                .Where(candidate => candidate.Pawn == pawn &&
                                    candidate.Map == pawn.Map &&
                                    candidate.InterruptedJob?.def != null)
                .OrderBy(candidate => candidate.RecordSequence)
                .FirstOrDefault();
            if (root != null && !SameInterruptedJob(root.InterruptedJob, job))
            {
                // Once a protected boundary interrupts a native job, that job
                // owns the retry handoff until it is consumed or invalidated.
                // RimWorld can immediately start another autonomous job whose
                // path reaches the same boundary; recording that replacement
                // would erase the work job that actually began the sequence.
                if (AomLog.ShouldLogDetailed(
                        pawn, "boundary-retry-root-preserved", 600))
                {
                    AomLog.Detailed(
                        $"{pawn.LabelShortCap}: preserved root " +
                        $"boundary-interrupted {root.JobDef.defName}; " +
                        $"ignored later {job.def.defName} retry record.");
                }
                return;
            }

            Entry entry = Entries.FirstOrDefault(candidate =>
                candidate.Pawn == pawn && candidate.Map == pawn.Map &&
                candidate.RuleId == rule.Id && candidate.JobDef == job.def &&
                SameInterruptedJob(candidate.InterruptedJob, job) &&
                TargetsOverlap(candidate, things, cells));
            int untilTick = CurrentTick + RetryLifetimeTicks;
            if (entry != null)
            {
                entry.InterruptedJob = interruptedJob;
                entry.UntilTick = untilTick;
                return;
            }

            Entries.Add(new Entry
            {
                Pawn = pawn,
                Map = pawn.Map,
                RuleId = rule.Id,
                JobDef = job.def,
                InterruptedJob = interruptedJob,
                Things = things,
                Cells = cells,
                UntilTick = untilTick,
                RecordSequence = ++nextRecordSequence
            });
        }

        private static Job DetachedClone(Job job)
        {
            Job clone = job?.Clone();
            if (clone == null)
                return null;

            clone.targetQueueA = job.targetQueueA == null
                ? null
                : new List<LocalTargetInfo>(job.targetQueueA);
            clone.targetQueueB = job.targetQueueB == null
                ? null
                : new List<LocalTargetInfo>(job.targetQueueB);
            clone.countQueue = job.countQueue == null
                ? null
                : new List<int>(job.countQueue);
            clone.placedThings = job.placedThings == null
                ? null
                : new List<ThingCountClass>(job.placedThings);
            return clone;
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
                // The first interrupted job owns this handoff. Later autonomous
                // jobs must not replace the work job that began the boundary
                // sequence before its exact continuation can be resumed.
                .OrderBy(entry => entry.RecordSequence)
                .FirstOrDefault();
            if (pending == null)
                return false;

            Job pendingJob = pending.InterruptedJob;
            AutomaticOutfitManagerGameComponent component =
                AutomaticOutfitManagerGameComponent.Current;
            rules = Entries.Where(entry =>
                    entry.Pawn == pawn && entry.Map == pawn.Map &&
                    SameInterruptedJob(entry.InterruptedJob, pendingJob))
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

            // A disabled, paused, incompatible, or otherwise inapplicable rule
            // permanently invalidates this handoff. Remove every entry for the
            // same root job so it cannot suppress a later valid boundary event
            // until the registry lifetime expires.
            Entries.RemoveAll(entry =>
                entry.Pawn == pawn && entry.Map == pawn.Map &&
                SameInterruptedJob(entry.InterruptedJob, pendingJob));
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

        private static bool SameInterruptedJob(Job left, Job right) =>
            left != null && right != null &&
            (ReferenceEquals(left, right) || left.loadID == right.loadID);

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
