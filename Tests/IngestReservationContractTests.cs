using System;
using AutomaticOutfitManager.Patches;
using RimWorld;
using Verse;
using Verse.AI;

class IngestReservationContractTests
{
 static int passed;
 static void Check(bool ok,string name) { if(!ok)throw new Exception(name); passed++; Console.WriteLine("PASS "+name); }
 static int Main()
 {
  try {
   var pawn=new Pawn { Map=new Map(), Faction=new object() };
   var food=new Thing { Map=pawn.Map };
   var job=new Job { def=JobDefOf.Ingest, targetA=new LocalTargetInfo { Thing=food }, count=51 };
   FoodUtility.Pickup=14; pawn.Reservable=false;
   Check(IngestReservationAdmission.Rejects(pawn,job),"contested native ingestion is rejected before acquiring reservations");
   Check(pawn.LastFood==food && pawn.LastMaxPawns==10 && pawn.LastCount==14,
    "admission matches native maxPawns and computed pickup quantity, not raw job count");
   Check(FoodUtility.Requested==51 && FoodUtility.Food==food && FoodUtility.Pawn==pawn,
    "pickup calculation receives the original native food, pawn and count");
   Check(job.count==51 && job.targetA.Thing==food,"admission does not alter job or food identity");
   FoodUtility.Pickup=15; IngestReservationAdmission.Rejects(pawn,job);
   Check(pawn.LastCount==15,"changed native pickup quantity is re-evaluated on the next attempt");
   pawn.Reservable=true;
   Check(!IngestReservationAdmission.Rejects(pawn,job),"food becomes admissible when native reservation allows it");
   pawn.Reservable=false; job.playerForced=true;
   Check(!IngestReservationAdmission.Rejects(pawn,job),"explicit player order retains native handling");
   job.playerForced=false; food.Spawned=false;
   Check(!IngestReservationAdmission.Rejects(pawn,job),"already held meal is not treated as contested map stock");
   food.Spawned=true; food.Map=new Map();
   Check(!IngestReservationAdmission.Rejects(pawn,job),"off-map target stays outside the reservation guard");
   food.Map=pawn.Map; food.Destroyed=true;
   Check(!IngestReservationAdmission.Rejects(pawn,job),"destroyed target stays with ordinary native validity handling");
   food.Destroyed=false; food.def.ingestible=null;
   Check(!IngestReservationAdmission.Rejects(pawn,job),"non-food target is not given a food reservation policy");
   food.def.ingestible=new object(); pawn.Faction=null;
   Check(!IngestReservationAdmission.Rejects(pawn,job),"factionless pawn follows native no-reservation path");
   pawn.Faction=new object(); job.def=new JobDef { driverClass=typeof(JobDriver_Ingest) };
   Check(!IngestReservationAdmission.Rejects(pawn,job),"other job definitions are not assumed to share Ingest semantics");
   job.def=JobDefOf.Ingest; job.def.driverClass=typeof(CustomIngest);
   Check(!IngestReservationAdmission.Rejects(pawn,job),"custom ingestion driver is left to its own reservation contract");
   job.def.driverClass=typeof(JobDriver_Ingest); FoodUtility.Pickup=0;
   Check(!IngestReservationAdmission.Rejects(pawn,job),"zero pickup does not create a false contested-food rejection");
   FoodUtility.Pickup=14; job.targetA=new LocalTargetInfo { Thing=new Building_NutrientPasteDispenser { Map=pawn.Map } };
   Check(!IngestReservationAdmission.Rejects(pawn,job),"paste dispenser uses native dispenser reservation handling");
   Check(!IngestReservationAdmission.Rejects(pawn,null) && !IngestReservationAdmission.Rejects(null,job),"absent job or pawn cannot trigger the guard");
   food=new Thing{Map=pawn.Map,Spawned=false};
   job=new Job { def=new JobDef{defName="ScroungeFood",driverClass=typeof(Hospitality.JobDriver_ScroungeFood)}, targetB=new LocalTargetInfo{Thing=food},count=19 };
   pawn.Reservable=false; FoodUtility.Pickup=2;
   Check(IngestReservationAdmission.Rejects(pawn,job),"Hospitality contested inventory food is rejected before its reservation fails");
   Check(pawn.LastFood==food && pawn.LastMaxPawns==1 && pawn.LastCount==19,"Hospitality uses target B and exact custom reservation count, not native Ingest calculation");
   Check(job.targetB.Thing==food && job.count==19,"compatibility guard preserves existing job and stack identity");
   pawn.Reservable=true;Check(!IngestReservationAdmission.Rejects(pawn,job),"Hospitality admission resumes when reservation becomes available");pawn.Reservable=false;
   job.playerForced=true;Check(!IngestReservationAdmission.Rejects(pawn,job),"forced Hospitality orders retain native handling");job.playerForced=false;
   food.Map=new Map();Check(!IngestReservationAdmission.Rejects(pawn,job),"off-map Hospitality food is left to native validity checks");food.Map=pawn.Map;
   food.Destroyed=true;Check(!IngestReservationAdmission.Rejects(pawn,job),"destroyed Hospitality target is not a contention wait");food.Destroyed=false;
   job.count=0;Check(!IngestReservationAdmission.Rejects(pawn,job),"zero-count Hospitality job does not invent a reservation");job.count=19;
   job.def.driverClass=typeof(CustomIngest);Check(!IngestReservationAdmission.Rejects(pawn,job),"matching job name with another driver does not activate compatibility logic");job.def.driverClass=typeof(Hospitality.JobDriver_ScroungeFood);
   job.def.defName="SwipeFood";Check(!IngestReservationAdmission.Rejects(pawn,job),"another Hospitality job retains its own reservation contract");
   Console.WriteLine(passed+" ingestion reservation checks passed; native reservations still require a game test."); return 0;
  } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
 }
 class CustomIngest : JobDriver_Ingest {}
}
namespace Verse {
 public class Map {}
 public class ThingDef { public object ingestible=new object(); }
 public class Thing { public bool Spawned=true,Destroyed; public Map Map; public Map MapHeld=>Map; public ThingDef def=new ThingDef(); public int thingIDNumber; public string LabelCap=>"food"; }
 public class Pawn { public Map Map; public object Faction; public string LabelShortCap=>"pawn";
  public bool Reservable; public Thing LastFood; public int LastMaxPawns,LastCount;
  public bool CanReserve(Thing food,int maxPawns,int count) { LastFood=food; LastMaxPawns=maxPawns; LastCount=count; return Reservable; }
 }
 public struct LocalTargetInfo { public Thing Thing; }
}
namespace Verse.AI {
 public class JobDef { public string defName; public Type driverClass; }
 public class Job { public bool playerForced; public JobDef def; public LocalTargetInfo targetA,targetB; public int count; }
}
namespace RimWorld {
 public class JobDriver_Ingest {}
 public class Building_NutrientPasteDispenser : Thing {}
 public static class JobDefOf { public static JobDef Ingest=new JobDef { driverClass=typeof(JobDriver_Ingest) }; }
 public static class FoodUtility { public static int Pickup,Requested; public static Thing Food; public static Pawn Pawn;
  public static int GetMaxAmountToPickup(Thing food,Pawn pawn,int count) { Food=food; Pawn=pawn; Requested=count; return Pickup; }
 }
}
namespace AutomaticOutfitManager.Core {
 public static class AomLog { public static bool DetailedEnabled=>false; public static bool ShouldLogDetailed(Pawn p,string key,int ticks)=>false; public static void Detailed(string s){} }
}

namespace Hospitality { public class JobDriver_ScroungeFood {} }
