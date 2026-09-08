using System;
using System.Collections.Generic;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorld { public static partial class JobDefOf { public static JobDef RemoveApparel = new JobDef { defName = "RemoveApparel" }; } }
namespace AutomaticOutfitManager.Patches { internal static partial class PawnJobTracker_StartJob_Patch { internal static bool IsChangingAreaTravelJob(Job job) => false; } }

partial class PausedHaulTests
{
    static void RestAndSupplyCases()
    {
        foreach (bool nonWork in new[] { false, true })
        {
            var p = Setup(out var s, out var r); r.IsNonWork = nonWork;
            var bed = new Thing { Map = p.Map, Position = 1 };
            var rest = new Job { def = new JobDef { defName = "LayDown" }, loadID = 720,
                targetA = new LocalTargetInfo(bed), Targets = true };
            s.PendingWorkJob = rest;
            Check(!PawnJobTracker_StartJob_Patch.IsBufferableJob(rest), "sleep never consumes a task buffer slot");
            Check(PausedAreaWorkFilter.JobMayEnterPausedRule(p, rest, r), "paused sleep is allowed with area permission");
            Check(RestActivityPolicy.MatchingPausedRules(p, rest).Contains(r), "paused bed requests PPE before entry");
            r.MissingGear = true;
            Check(!PawnJobTracker_StartJob_Patch.ShouldResumePreparedJob(p, AutomaticOutfitManagerGameComponent.Current, s, PreparationHandoffTests.Wait()), "sleep cannot resume with incomplete PPE");
            var suit = Step(p, s); var helmet = Step(p, s); var weapon = Step(p, s, true);
            p.jobs.jobQueue.Add(new QueuedJob { job = helmet });
            p.jobs.jobQueue.Add(new QueuedJob { job = weapon });
            p.jobs.StartJob(suit); Pulse(p, r);
            Check(!s.RecallRequested, "sleep Wear must survive pause watchdog");
            p.Position = 0; rest.Targets = false;
            p.jobs.curJob = PreparationHandoffTests.Wait(); Pulse(p, r);
            Check(!s.RecallRequested && ReferenceEquals(s.PendingWorkJob, rest), "sleep retains exact bed across locker wait");
            p.jobs.AdvanceQueue(); Pulse(p, r);
            p.jobs.AdvanceQueue(); Pulse(p, r);
            Check(!s.RecallRequested && p.jobs.curJob == weapon, "sleep PPE queue reaches weapon without recall");
            s.Transition = ApparelTransition.Active; r.MissingGear = false;
            Check(PawnJobTracker_StartJob_Patch.ShouldResumePreparedJob(p, AutomaticOutfitManagerGameComponent.Current, s, PreparationHandoffTests.Wait()), "active sleep resumes after completed PPE");
            PawnJobTracker_StartJob_Patch.CompletePreparedJobAdmission(p, s, rest);
            p.jobs.jobQueue.Add(new QueuedJob { job = rest }); p.jobs.curJob = PreparationHandoffTests.Wait();
            Pulse(p, r); Check(!s.RecallRequested, "queued prepared sleep survives native connective child");
            p.jobs.AdvanceQueue(); Pulse(p, r);
            Check(p.jobs.curJob == rest && s.PendingWorkJob == null && !s.RecallRequested, "native tracker owns one resting job after preparation");
            for (int i = 0; i < 10; i++) { Find.TickManager.TicksGame += 600; Pulse(p, r); }
            Check(p.jobs.curJob == rest && !s.RecallRequested && s.BufferedTasksCompleted == 0, "long rest stays admitted without buffer credit");
            r.Activities = false;
            Check(!RestActivityPolicy.Allowed(p, rest, r), "rest respects disabled activity category"); r.Activities = true;
            p.Child = true; r.Children = false;
            Check(!RestActivityPolicy.Allowed(p, rest, r), "rest respects child access"); p.Child = false; r.Children = true;
            rest.Targets = false; rest.Crosses = true; rest.Avoidable = true;
            Check(RestActivityPolicy.MatchingPausedRules(p, rest).Count == 0, "rest avoids unrelated protected shortcut");
            rest.Avoidable = false;
            Check(RestActivityPolicy.MatchingPausedRules(p, rest).Contains(r), "unavoidable bed route still requires PPE");
            s.RecallRequested = true;
            Check(!RestActivityPolicy.Preserves(s, r, rest), "explicit recall cannot be cancelled by sleep continuation");
        }

        {
            var p = Setup(out var s, out var r);
            var original = new Blueprint { Map = p.Map, Position = 0 };
            var delivery = Haul("HaulToContainer");
            // Native ResourceDeliverJobFor: C is the original constructible;
            // B may be a different recipient and queueB may hold more sites.
            delivery.workGiverDef = new WorkGiverDef { workType = new WorkTypeDef { defName = "Construction" } };
            delivery.targetA = new LocalTargetInfo(new Thing { Map = p.Map, Position = 1 });
            delivery.targetB = new LocalTargetInfo(new Frame { Map = p.Map, Position = 0 });
            delivery.targetC = new LocalTargetInfo(original);
            delivery.targetQueueB = new List<LocalTargetInfo> { new LocalTargetInfo(new Blueprint { Map = p.Map, Position = 0 }) };
            Check(PausedAreaWorkFilter.IsPermittedMaterialCollection(p, delivery, r), "native original construction target permits exterior delivery");
            Check(PausedAreaWorkFilter.DeniedActivityRule(p, delivery) == null && PausedAreaWorkFilter.DeniedPausedAreaRule(p, delivery) == null, "native component shape passes both activity and pause admission");
            s.PendingWorkJob = delivery; p.jobs.curJob = Step(p, s); Pulse(p, r);
            Check(!s.RecallRequested, "native component shape preserves PPE preparation");
            original.Position = 1;
            Check(!PausedAreaWorkFilter.IsPermittedMaterialCollection(p, delivery, r), "original construction inside pause stays blocked");
            original.Position = 0; original.Footprint = new List<IntVec3> { 0, 1 };
            Check(!PausedAreaWorkFilter.IsPermittedMaterialCollection(p, delivery, r), "original constructible footprint must be outside pause");
            original.Footprint = null; original.Map = new Map();
            Check(!PausedAreaWorkFilter.IsPermittedMaterialCollection(p, delivery, r), "original constructible cannot belong to another map");
            original.Map = p.Map; original.Destroyed = true;
            Check(!PausedAreaWorkFilter.IsPermittedMaterialCollection(p, delivery, r), "destroyed original construction target rejected");
        }

        {
            var p = Setup(out var s, out var r); p.Position = 0;
            s.Transition = ApparelTransition.Restoring; s.PendingWorkJob = null; s.RecallRequested = true;
            var saved = new Apparel { Map = p.Map, Position = 1 }; s.OriginalApparel.Add(saved);
            var rest = new Job { def = new JobDef { defName = "LayDown" }, loadID = 880,
                targetA = new LocalTargetInfo(new Thing { Map = p.Map, Position = 0 }) };
            RestorationPlanProgress.Observe(s, 100, 0, true);
            Check(RestorationPlanProgress.IsEmptyAndBlocked(s) && RestorationNeeds.CanDefer(p, s, rest, 0, true), "blocked snapshot permits safe rest");
            Check(!RestorationNeeds.CanDefer(p, s, rest, 1, true), "available restoration steps go before rest");
            p.jobs.jobQueue.Add(new QueuedJob { job = new Job { def = JobDefOf.Wear, targetA = new LocalTargetInfo(saved) } });
            Check(!RestorationNeeds.CanDefer(p, s, rest, 0, true), "queued exact restoration stays ahead of needs"); p.jobs.jobQueue.Clear();
            p.jobs.StartJob(rest);
            Check(ReferenceEquals(p.jobs.curJob, rest) && s.OriginalApparel.Count == 1 && s.OriginalApparel[0] == saved && s.Transition == ApparelTransition.Restoring, "rest does not replace or clear exact snapshot");
            Check(RestorationNeeds.IsNeed(p.jobs.curJob), "rest is not an outfit stall");
            RestorationPlanProgress.Wake(s);
            Check(!RestorationPlanProgress.IsEmptyAndBlocked(s) && p.jobs.curJob == rest, "recovery wakes probe without interrupting sleep");
            rest.Targets = true; r.MissingGear = true;
            Check(!RestorationNeeds.CanDefer(p, s, rest, 0, true), "blocked personal item does not waive Work PPE");
            r.IsNonWork = true; r.SavedPersonal = true;
            Check(RestorationNeeds.CanDefer(p, s, rest, 0, true), "missing saved piece may wait in Non-Work bed");
            r.MustReturnWork = true;
            Check(!RestorationNeeds.CanDefer(p, s, rest, 0, true), "Non-Work bed still requires selected work outfit removal"); r.MustReturnWork = false;
            var meal = new Job { def = JobDefOf.Ingest, Targets = true };
            Check(!RestorationNeeds.CanDefer(p, s, meal, 0, true), "restoration deferral does not enable meals in paused area");
            Check(!RestorationNeeds.CanDefer(p, s, Haul(), 0, true), "unrelated work cannot hijack blocked restoration");
            s.MapDepartureRequested = true;
            Check(!RestorationNeeds.CanDefer(p, s, rest, 0, true), "departure restoration remains authoritative");
        }
    }
}
