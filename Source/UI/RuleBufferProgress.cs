using System;
using System.Linq;
using AutomaticOutfitManager.State;

namespace AutomaticOutfitManager.UI
{
    // A rule panel must never borrow the outgoing session's task counter.
    internal sealed class RuleBufferProgress
    {
        public bool Started;
        public bool Ended;
        public int Completed;
        public int Maximum;
        public int PendingJobId = -1;

        internal static RuleBufferProgress For(string ruleId, int maximum,
            PawnApparelState state, NonWorkOutfitBuffer retained)
        {
            var result = new RuleBufferProgress { Maximum = Math.Max(0, maximum) };
            if (retained?.RuleId == ruleId)
            {
                result.Started = true;
                result.Completed = retained.Completed;
                result.PendingJobId = retained.PendingJobId;
                result.Ended = retained.PendingWork != null;
            }
            else if (state?.ActiveRuleId == ruleId)
            {
                result.Started = state.Transition != ApparelTransition.Preparing;
                result.Completed = state.BufferedTasksCompleted;
                result.PendingJobId = state.PendingBufferedJobLoadId;
            }
            else
            {
                var nested = state?.NestedRuleBuffers?.FirstOrDefault(item => item?.RuleId == ruleId);
                if (nested != null)
                {
                    result.Started = true;
                    result.Completed = nested.Completed;
                    result.PendingJobId = nested.PendingJobLoadId;
                    result.Ended = nested.Finished;
                }
            }
            result.Completed = Math.Max(0, Math.Min(result.Completed, result.Maximum));
            return result;
        }

        internal string Summary()
        {
            string progress = Maximum == 0 ? "off (Immediate)"
                : !Started ? "not started"
                : $"{Completed}/{Maximum}" + (Ended ? " — ended" : "");
            return $"Buffer: {progress}";
        }

        internal string Headline(string activity)
        {
            return $"Buffered tasks {Completed} of {Maximum} complete; current: {activity}";
        }
    }
}
