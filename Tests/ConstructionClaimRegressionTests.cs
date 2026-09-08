using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Patches;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorld
{
    public interface IConstructible { }
    public class Blueprint : Thing, IConstructible { }
    public class Frame : Thing, IConstructible { }
    public static class GenConstruct
    {
        public static readonly Dictionary<Thing, Job> BlockingJobs = new Dictionary<Thing, Job>();
        public static int Creates;
        public static Thing FirstBlockingThing(Thing t, Pawn p) =>
            BlockingJobs.TryGetValue(t, out Job job) ? job.targetA.Thing : null;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Job HandleBlockingThingJob(Thing t, Pawn p, bool forced)
        { Creates++; return BlockingJobs.TryGetValue(t, out Job job) ? job : null; }
    }
    // Native IL: HasJobOnThing and JobOnThing each call this shared generator.
    // Has does not call JobOnThing, which is what exposed the former mismatch.
    public class WorkGiver_ConstructDeliverResources : WorkGiver_Scanner
    {
        public Job Candidate; public int Creates;
        [MethodImpl(MethodImplOptions.NoInlining)]
        protected Job ResourceDeliverJobFor(Pawn p, IConstructible t, bool canRemoveExistingFloor, bool forced)
        { Creates++; return Candidate; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public override bool HasJobOnThing(Pawn p, Thing t, bool forced=false) =>
            GenConstruct.FirstBlockingThing(t,p)!=null ? GenConstruct.HandleBlockingThingJob(t,p,forced)!=null : ResourceDeliverJobFor(p,(IConstructible)t,false,forced)!=null;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public override Job JobOnThing(Pawn p, Thing t, bool forced=false) =>
            GenConstruct.FirstBlockingThing(t,p)!=null ? GenConstruct.HandleBlockingThingJob(t,p,forced) : ResourceDeliverJobFor(p,(IConstructible)t,false,forced);
    }
    public class WorkGiver_ConstructDeliverResourcesToBlueprints : WorkGiver_ConstructDeliverResources { }
    public class WorkGiver_ConstructDeliverResourcesToFrames : WorkGiver_ConstructDeliverResources { }
}
internal static class ConstructionClaimRegressionTests
{
    internal static void Run(Action<bool,string> check)
    {
        var map=new Map(); var hanh=new Pawn {Map=map}; var bowman=new Pawn {Map=map};
        AutomaticOutfitManagerGameComponent.Current=new AutomaticOutfitManagerGameComponent();
        Find.TickManager.TicksGame=100; ManagedWorkClaimRegistry.ResetForLoadedGame();
        // Native frame/blueprint scanners take the early blocker branch before
        // material generation. Exercise actual Harmony patches at both stages.
        foreach(bool frame in new[]{false,true})
        foreach(string kind in new[]{"HaulToCell","CutPlant","Deconstruct","Mine"})
        foreach(bool destinationClaim in kind=="HaulToCell" ? new[]{false,true} : new[]{false})
        {
            WorkGiver_ConstructDeliverResources scanner=frame ? (WorkGiver_ConstructDeliverResources)new WorkGiver_ConstructDeliverResourcesToFrames() : new WorkGiver_ConstructDeliverResourcesToBlueprints();
            Thing target=frame ? (Thing)new Frame {Map=map,Position=new IntVec3(60)} : new Blueprint {Map=map,Position=new IntVec3(60)};
            var blocker=new Thing {Map=map,Position=new IntVec3(60)};
            var job=new Job {def=kind=="HaulToCell" ? JobDefOf.HaulToCell : new JobDef {defName=kind},targetA=blocker,targetB=new IntVec3(70)};
            GenConstruct.BlockingJobs[target]=job;
            var claim=new Job {def=JobDefOf.DoBill,targetA=destinationClaim ? job.targetB : job.targetA};
            check(ManagedWorkClaimRegistry.TryClaim(hanh,claim),"prepared pawn claims construction blocker or destination");
            int before=GenConstruct.Creates;
            check(!scanner.HasJobOnThing(bowman,target),"blocker availability rejects prepared claim before promising a job: "+frame+"/"+kind+"/destination="+destinationClaim);
            check(scanner.JobOnThing(bowman,target)==null,"blocker job stage agrees with availability");
            check(GenConstruct.Creates==before+2 && scanner.Creates==0,"blocker branch runs once per stage and bypasses resource generation");
            check(scanner.HasJobOnThing(hanh,target)&&scanner.JobOnThing(hanh,target)==job,"rightful claimant can clear its blocker");
            check(scanner.HasJobOnThing(bowman,target,true)&&scanner.JobOnThing(bowman,target,true)==job,"forced blocker clearing stays native");
            ManagedWorkClaimRegistry.ReleaseAll(hanh);
            check(scanner.HasJobOnThing(bowman,target)&&scanner.JobOnThing(bowman,target)==job,"released blocker claim becomes available immediately");
            GenConstruct.BlockingJobs.Remove(target);
        }
        foreach(bool frame in new[]{false,true})
        foreach(bool recipient in new[]{false,true})
        {
            WorkGiver_ConstructDeliverResources scanner=frame ? (WorkGiver_ConstructDeliverResources)new WorkGiver_ConstructDeliverResourcesToFrames() : new WorkGiver_ConstructDeliverResourcesToBlueprints();
            Thing target=frame ? (Thing)new Frame {Map=map,Position=new IntVec3(60)} : new Blueprint {Map=map,Position=new IntVec3(60)};
            var steel=new Thing {Map=map,Position=new IntVec3(50)};
            var secondary=new Thing {Map=map,Position=new IntVec3(51)};
            var delivery=new Job {def=JobDefOf.HaulToContainer,targetA=steel,targetB=target};
            if(recipient) delivery.targetQueueB=new List<LocalTargetInfo>{secondary};
            else delivery.targetQueueA=new List<LocalTargetInfo>{secondary};
            scanner.Candidate=delivery;
            check(ManagedWorkClaimRegistry.TryClaim(hanh,new Job {def=JobDefOf.DoBill,targetA=secondary}),"other pawn owns secondary construction target");
            check(!scanner.HasJobOnThing(bowman,target),"construction availability rejects claimed secondary target before selection");
            check(scanner.JobOnThing(bowman,target)==null,"construction job stage agrees with unavailable target");
            check(scanner.Creates==2,"one native generation per scanner stage without extra scans");
            check(scanner.HasJobOnThing(bowman,target,true)&&scanner.JobOnThing(bowman,target,true)==delivery,"forced scanner retains native behavior");
            ManagedWorkClaimRegistry.ReleaseAll(hanh);
            check(scanner.HasJobOnThing(bowman,target)&&scanner.JobOnThing(bowman,target)==delivery,"claim release allows native material delivery immediately");
        }
        var gear=new ThingWithComps {Map=map,def=new ThingDef {IsWeapon=true}};
        var haul=JobMaker.MakeJob(JobDefOf.HaulToCell,gear,new IntVec3(20));
        check(!PawnJobTracker_StartJob_Patch.StorageRejects(bowman,haul),"accepting slot parent allows gear delivery");
        map.Storage.Allows=false;
        check(PawnJobTracker_StartJob_Patch.StorageRejects(bowman,haul),"disallowed slot parent blocks gear delivery");
        map.Storage.Allows=true; map.Storage.Enabled=false;
        check(PawnJobTracker_StartJob_Patch.StorageRejects(bowman,haul),"disabled slot parent blocks gear delivery");
    }
}
