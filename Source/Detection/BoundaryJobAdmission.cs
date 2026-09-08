using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.State;
using Verse;
using Verse.AI;

namespace AutomaticOutfitManager.Detection
{
    // One admission spans the complete native StartJob call, including nested
    // compatibility starts and error recovery. The registry owns a snapshot;
    // only a separate working copy may enter the tracker or preparation state.
    public sealed class BoundaryJobAdmission : IDisposable
    {
        private static readonly Dictionary<Pawn, BoundaryJobAdmission> Active =
            new Dictionary<Pawn, BoundaryJobAdmission>();
        private readonly Pawn pawn;
        private readonly Job snapshot;
        private readonly long revision;
        private readonly JobDef jobDef;
        private readonly int jobLoadId;
        private bool completed;
        private bool disposed;
        public Job Job { get; }

        private BoundaryJobAdmission(Pawn pawn, Job snapshot, Job job)
        {
            this.pawn = pawn;
            this.snapshot = snapshot;
            Job = job;
            jobDef = job.def;
            jobLoadId = job.loadID;
            revision = ProtectedBoundaryRetryRegistry.Revision;
        }

        public static bool IsOpen(Pawn pawn) => pawn != null && Active.ContainsKey(pawn);

        public static bool CanResume(PawnApparelState state) => state == null ||
            (state.Transition == ApparelTransition.Active && !state.RecallRequested);

        public static bool TryBegin(Pawn pawn, Job snapshot, out BoundaryJobAdmission admission,
            Job proposedJob = null)
        {
            admission = null;
            if (pawn?.jobs == null || pawn.Drafted || pawn.Downed || IsOpen(pawn) ||
                snapshot?.def == null ||
                !CanResume(AutomaticOutfitManagerGameComponent.Current?.StateFor(pawn)))
                return false;

            Job job = proposedJob != null && !ReferenceEquals(proposedJob, snapshot)
                ? proposedJob : ProtectedBoundaryRetryRegistry.DetachedClone(snapshot);
            if (job?.def == null) return false;
            admission = new BoundaryJobAdmission(pawn, snapshot, job);
            Active.Add(pawn, admission);
            return true;
        }

        public bool Complete()
        {
            // A StartJob return alone is not acceptance: native code may reject
            // and pool the proposed job or leave a finalizer owning the tracker.
            bool owned = Job.def == jobDef && Job.loadID == jobLoadId &&
                (ReferenceEquals(pawn.jobs?.curJob, Job) ||
                 pawn.jobs?.jobQueue?.Any(queued => ReferenceEquals(queued.job, Job)) == true ||
                 ReferenceEquals(AutomaticOutfitManagerGameComponent.Current?
                     .StateFor(pawn)?.PendingWorkJob, Job));
            if (owned)
                ProtectedBoundaryRetryRegistry.Retire(pawn, snapshot, revision);
            else
                ProtectedBoundaryRetryRegistry.Defer(pawn, snapshot);
            completed = true;
            return owned;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                if (!completed) ProtectedBoundaryRetryRegistry.Defer(pawn, snapshot);
            }
            finally
            {
                if (Active.TryGetValue(pawn, out var admission) && ReferenceEquals(admission, this))
                    Active.Remove(pawn);
            }
        }
    }
}
