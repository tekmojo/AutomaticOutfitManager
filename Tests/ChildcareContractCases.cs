using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.State;
using Verse;
using Verse.AI;

partial class MealHandoffContractTests
{
 static void ChildcareTests()
 {
  Setup(); ChildcareContinuation.ResetForLoadedGame();
  var baby = new Pawn { DevelopmentalStage = DevelopmentalStage.Baby };
  var safety = new JobDef { defName = "BringBabyToSafety" };
  var job = JobMaker.MakeJob(safety, baby);
  Check(!ChildcareContinuation.Admit(p,job), "safety job does not bypass preparation before picking up a baby");
  p.carryTracker.innerContainer.Clear(); p.carryTracker.innerContainer.Add(baby);
  var state = c.State; state.BufferedTasksCompleted=2; state.RecallRequested=true;
  Check(ChildcareContinuation.Admit(p,job), "carried baby safe placement is admitted with a pending Work outfit return");
  Check(c.State==state && state.RecallRequested && state.BufferedTasksCompleted==2,
   "childcare admission preserves pending return and full buffer count");
  Check(ChildcareContinuation.Admit(p,job), "repeated runtime checks preserve exact continuation");
  p.carryTracker.innerContainer.Clear();
  Check(ChildcareContinuation.Admit(p,job), "placement finalization remains protected after releasing baby");
  Check(ChildcareContinuation.End(p,job,JobCondition.Succeeded), "successful native placement is excluded from another buffer credit");
  Check(!ChildcareContinuation.End(p,job,JobCondition.Succeeded), "completed registration cannot be consumed twice");
  Check(!ChildcareContinuation.Admit(p,job), "finished placement cannot acquire another exception without carrying baby");

  p.carryTracker.innerContainer.Add(baby);
  ChildcareContinuation.Admit(p,job); job.loadID++;
  Check(!ChildcareContinuation.End(p,job,JobCondition.Succeeded), "pooled job identity cannot inherit completion ownership");
  ChildcareContinuation.Admit(p,job); var oldMap=p.Map; p.Map=new Map();
  Check(!ChildcareContinuation.End(p,job,JobCondition.Succeeded), "old map registration cannot complete on another map");
  p.Map=oldMap; ChildcareContinuation.Admit(p,job);
  Check(!ChildcareContinuation.End(new Pawn(),job,JobCondition.Succeeded), "another caregiver cannot consume the registration");
  ChildcareContinuation.ResetForLoadedGame();
  Check(!ChildcareContinuation.End(p,job,JobCondition.Succeeded), "load reset discards transient job ownership");
  Check(ChildcareContinuation.Admit(p,job), "loaded native carried-baby job can recover protection from current state");
  Check(ChildcareContinuation.End(p,job,JobCondition.Incompletable) && p.carryTracker.CarriedThing==baby,
   "failed placement grants no credit and leaves native childcare responsible for the baby");
  p.Drafted=true;
  Check(!ChildcareContinuation.Admit(p,job), "drafted control supersedes childcare deferral");
  p.Drafted=false; p.Downed=true;
  Check(!ChildcareContinuation.Admit(p,job), "downed caregiver cannot acquire childcare deferral");
  p.Downed=false; p.InMentalState=true;
  Check(!ChildcareContinuation.Admit(p,job), "mental state supersedes childcare deferral");
  p.InMentalState=false; baby.DevelopmentalStage=DevelopmentalStage.Child;
  Check(!ChildcareContinuation.Admit(p,job), "carrying an older child is not a baby-placement exception");
  baby.DevelopmentalStage=DevelopmentalStage.Baby; baby.RaceProps.Humanlike=false;
  Check(!ChildcareContinuation.Admit(p,job), "animal carrying does not receive the baby exception");
  baby.RaceProps.Humanlike=true; c.State=null;
  Check(!ChildcareContinuation.Admit(p,job), "unmanaged native childcare remains outside AOM ownership");

  // Real NonWorkBufferTracker: feed succeeds at the limit, then native safe
  // placement must retain that allowance without enrolling a fresh task.
  p.Position=4; dest.ReturnTaskBuffer=2;
  var buffer=new NonWorkOutfitBuffer { Pawn=p, Map=p.Map, RuleId=dest.Id,
   Apparel=new System.Collections.Generic.List<RimWorld.Apparel>(p.apparel.WornApparel), Completed=1 };
  c.NonWorkOutfitBuffers.Add(buffer);
  var feed=JobMaker.MakeJob(new JobDef { defName="Breastfeed" },baby);
  buffer.PendingJobId=feed.loadID; buffer.LastStartedJobId=feed.loadID;
  NonWorkBufferTracker.End(p,feed,JobCondition.Succeeded);
  Check(buffer.Completed==2, "successful feeding still consumes the last Non-Work buffer task");
  job=JobMaker.MakeJob(new JobDef { defName="BringBabyToSafetyUnforced" },baby); p.jobs.curJob=job;
  NonWorkBufferTracker.Refresh(p);
  Check(NonWorkBufferTracker.For(p)==buffer && buffer.Completed==2 && buffer.PendingJobId==-1,
   "native safety follow-up retains full Non-Work allowance without enrolling another task");
  Check(ChildcareContinuation.End(p,job,JobCondition.Succeeded) && buffer.Completed==2,
   "Non-Work placement completion leaves buffer at its original limit");
  p.carryTracker.innerContainer.Clear(); p.jobs.curJob=JobMaker.MakeJob(RimWorld.JobDefOf.HaulToCell,new LocalTargetInfo(6));
  NonWorkBufferTracker.Refresh(p);
  Check(NonWorkBufferTracker.For(p)==null, "ordinary next task expires the full Non-Work allowance after childcare");
 }
}
