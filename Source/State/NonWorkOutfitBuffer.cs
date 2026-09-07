using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.State
{
    // An outfit-retention allowance, not a second owner of personal gear or Jobs.
    public sealed class NonWorkOutfitBuffer : IExposable
    {
        public Pawn Pawn;
        public Map Map;
        public string RuleId;
        public List<Apparel> Apparel = new List<Apparel>();
        public ThingWithComps Weapon;
        public int Completed;
        public int PendingJobId = -1;
        public int LastCompletedJobId = -1;
        public int LastStartedJobId = -1;
        public Job PendingWork;
        public IntVec3 ExitCell = IntVec3.Invalid;
        public int HandoffStartedTick;

        public bool OutfitUnchanged() => Pawn?.apparel != null &&
            Apparel.Count == Pawn.apparel.WornApparel.Count &&
            Apparel.All(item => item != null && !item.Destroyed &&
                Pawn.apparel.WornApparel.Contains(item)) && Pawn.equipment?.Primary == Weapon;

        // Called only for the job actually admitted by the native tracker.
        public bool Start(int jobId, int limit, bool inAreaTask, bool compatible, bool countable)
        {
            if (!compatible || limit <= 0) return false;
            if (LastStartedJobId == jobId) return true;
            LastStartedJobId = jobId;
            PendingJobId = -1;
            if (inAreaTask && countable)
            {
                Completed = 0;
                LastCompletedJobId = -1;
            }
            else if (!inAreaTask && Completed >= limit) return false;
            else if (!inAreaTask && countable && jobId != LastCompletedJobId)
                PendingJobId = jobId;
            return true;
        }

        public bool Complete(int jobId, bool succeeded, int limit)
        {
            if (PendingJobId != jobId) return false;
            PendingJobId = -1;
            if (!succeeded) { LastStartedJobId = -1; return false; }
            if (jobId == LastCompletedJobId || Completed >= limit) return false;
            Completed++;
            LastCompletedJobId = jobId;
            return true;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref Pawn, "pawn");
            Scribe_References.Look(ref Map, "map");
            Scribe_Values.Look(ref RuleId, "ruleId");
            Scribe_Collections.Look(ref Apparel, "apparel", LookMode.Reference);
            Scribe_References.Look(ref Weapon, "weapon");
            Scribe_Values.Look(ref Completed, "completed", 0);
            Scribe_Values.Look(ref PendingJobId, "pendingJobId", -1);
            Scribe_Values.Look(ref LastCompletedJobId, "lastCompletedJobId", -1);
            Scribe_Values.Look(ref LastStartedJobId, "lastStartedJobId", -1);
            Scribe_Deep.Look(ref PendingWork, "pendingWork");
            Scribe_Values.Look(ref ExitCell, "exitCell", IntVec3.Invalid);
            Scribe_Values.Look(ref HandoffStartedTick, "handoffStartedTick", 0);
            Apparel ??= new List<Apparel>();
        }
    }
}
