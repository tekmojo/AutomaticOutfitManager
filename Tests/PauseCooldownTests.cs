// Real registry plus the production pause button, with minimal native inputs.
using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.UI;
using Verse;
using Verse.AI;

class PauseCooldownTests
{
    static int passed;
    static void Check(bool ok,string name) {if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
    static int Main() {
        try {
            foreach(bool nonWork in new[]{false,true}) {
                UnavailableWorkRegistry.ResetForLoadedGame();Find.TickManager.TicksGame=100;
                var map=new Map();var c=AutomaticOutfitManagerGameComponent.Current=new AutomaticOutfitManagerGameComponent();
                var rule=new ApparelRule {Id="Dining",Area=new Area{Map=map},WorkAreaPaused=true,IsNonWork=nonWork};c.Rules.Add(rule);
                var other=new ApparelRule {Id="Kitchen",Area=rule.Area,WorkAreaPaused=true};c.Rules.Add(other);
                var pawn=new Pawn{thingIDNumber=1,Map=map};var second=new Pawn{thingIDNumber=2,Map=map};
                var job=new Job{def=new JobDef(),targetA=new LocalTargetInfo(new Thing{MapHeld=map})};
                UnavailableWorkRegistry.BlockDeniedActivity(pawn,rule,job);
                UnavailableWorkRegistry.BlockDeniedActivity(second,rule,job);
                Check(UnavailableWorkRegistry.ShouldReject(pawn,job),"paused exact job rejected");
                Check(UnavailableWorkRegistry.ShouldReject(pawn,map,job.targetA.Thing,IntVec3.Invalid),"paused scanner target rejected");
                Check(UnavailableWorkRegistry.HasActiveRuleBlock(pawn,rule),"paused retry registered");
                // Actual UI Resume must clear every pawn, including no-session
                // pawns, even when it is paused again before the next lookup.
                MainRulesWindow.Toggle(rule,c);MainRulesWindow.Toggle(rule,c);
                Check(!UnavailableWorkRegistry.ShouldReject(pawn,job)&&!UnavailableWorkRegistry.ShouldReject(second,job),"resume clears prior pause entries before rapid repause");
                Check(c.PawnStates.Count==0&&pawn.jobs.curJob==null,"resume needs no session or forced native job");
                foreach(bool gearFirst in new[]{false,true}) {
                    UnavailableWorkRegistry.Clear(pawn,c.Rules);
                    if(gearFirst)UnavailableWorkRegistry.Block(pawn,rule,job,2000);
                    UnavailableWorkRegistry.BlockDeniedActivity(pawn,rule,job);
                    if(!gearFirst)UnavailableWorkRegistry.Block(pawn,rule,job,2000);
                    MainRulesWindow.Toggle(rule,c);
                    Check(UnavailableWorkRegistry.ShouldReject(pawn,job),"same-target real gear cooldown survives resume regardless of insertion order");
                    Find.TickManager.TicksGame+=1201;
                    Check(UnavailableWorkRegistry.ShouldReject(pawn,job),"pause retry cannot shorten real gear cooldown");
                    MainRulesWindow.Toggle(rule,c);
                }
                UnavailableWorkRegistry.Clear(pawn,c.Rules);
                UnavailableWorkRegistry.BlockDeniedActivity(pawn,rule,job);UnavailableWorkRegistry.BlockDeniedActivity(pawn,other,job);
                MainRulesWindow.Toggle(rule,c);
                Check(!UnavailableWorkRegistry.HasActiveRuleBlock(pawn,rule)&&UnavailableWorkRegistry.HasActiveRuleBlock(pawn,other),"resume is scoped to one rule");
                Check(UnavailableWorkRegistry.ShouldReject(pawn,job),"other paused area remains denied");
                UnavailableWorkRegistry.Clear(pawn,c.Rules);
                UnavailableWorkRegistry.Block(pawn,rule);
                UnavailableWorkRegistry.ClearPauseBlocks(rule);
                Check(UnavailableWorkRegistry.ShouldReject(pawn,job),"broad unavailable outfit cooldown survives resume");
                UnavailableWorkRegistry.Clear(pawn,c.Rules);
                UnavailableWorkRegistry.BlockDeniedActivity(pawn,rule,job);
                UnavailableWorkRegistry.ClearPauseBlocks(rule);
                Check(UnavailableWorkRegistry.ShouldReject(pawn,job),"unpaused category denial retains its cooldown");
                foreach(int query in new[]{0,1,2}) {
                    UnavailableWorkRegistry.Clear(pawn,c.Rules);rule.WorkAreaPaused=true;
                    UnavailableWorkRegistry.BlockDeniedActivity(pawn,rule,job);rule.WorkAreaPaused=false;
                    bool denied=query==0?UnavailableWorkRegistry.ShouldReject(pawn,job):query==1?UnavailableWorkRegistry.ShouldReject(pawn,map,job.targetA.Thing,IntVec3.Invalid):UnavailableWorkRegistry.HasActiveRuleBlock(pawn,rule);
                    Check(!denied,"direct resume invalidates each retry query: "+query);
                }
                rule.WorkAreaPaused=true;UnavailableWorkRegistry.BlockDeniedActivity(pawn,rule,job);
                pawn.Map=new Map();Check(!UnavailableWorkRegistry.ShouldReject(pawn,job),"pause retry cannot affect another map");pawn.Map=map;
                Find.TickManager.TicksGame+=1200;Check(!UnavailableWorkRegistry.ShouldReject(pawn,job),"pause retry expires normally");
                UnavailableWorkRegistry.BlockDeniedActivity(pawn,rule,job);UnavailableWorkRegistry.ResetForLoadedGame();
                Check(!UnavailableWorkRegistry.ShouldReject(pawn,job),"load reset clears transient pause retry");
            }
            Console.WriteLine(passed+" pause cooldown checks passed.");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
namespace Verse {
 public class Map {}
 public struct IntVec3 {public int x;public bool IsValid=>x>=0;public bool InBounds(Map m)=>IsValid;public static IntVec3 Invalid=>new IntVec3{x=-1};}
 public class Thing {public Map MapHeld;public IntVec3 PositionHeld;public bool Destroyed;}
 public struct LocalTargetInfo {public Thing Thing;public IntVec3 Cell;public bool HasThing=>Thing!=null;public bool IsValid=>HasThing||Cell.IsValid;public LocalTargetInfo(Thing t){Thing=t;Cell=IntVec3.Invalid;}}
 public class Pawn {public int thingIDNumber;public Map Map;public Pawn_JobTracker jobs=new Pawn_JobTracker();}
 public class Area {public Map Map;public bool this[IntVec3 cell]=>true;}
 public class JobDef {}
 public class TickManager {public int TicksGame;}
 public static class Find {public static TickManager TickManager=new TickManager();}
}
namespace Verse.AI {
 public class Job {public JobDef def;public LocalTargetInfo targetA,targetB=new LocalTargetInfo(null),targetC=new LocalTargetInfo(null);public List<LocalTargetInfo> targetQueueA,targetQueueB;}
 public class Pawn_JobTracker {public Job curJob;}
}
namespace AutomaticOutfitManager.Rules {public class ApparelRule {public string Id;public Area Area;public bool Enabled=true,WorkAreaPaused,IsNonWork;}}
namespace AutomaticOutfitManager.State {public class PawnApparelState {public Pawn Pawn;public bool RecallRequested;public List<string> PauseRecallRuleIds=new List<string>();}}
namespace AutomaticOutfitManager.Core {
 public class AutomaticOutfitManagerGameComponent {
  public static AutomaticOutfitManagerGameComponent Current;public List<ApparelRule> Rules=new List<ApparelRule>();
  public List<State.PawnApparelState> PawnStates=new List<State.PawnApparelState>();public ApparelRule RuleById(string id)=>Rules.Find(r=>r.Id==id);
  public bool TryCancelRulePauseRecall(State.PawnApparelState s,ApparelRule r)=>false;
  public void RequestRulePauseRecall(State.PawnApparelState s,ApparelRule r){}
 }
}
namespace AutomaticOutfitManager.Detection {
 public static class WeaponPreparationRetryRegistry {public static void ResetForLoadedGame(){}}
 public static class RuleEvaluator {public static bool JobTargetsArea(Job j,Area a)=>true;public static void ResetRuntimeCache(){}}
}
namespace AutomaticOutfitManager.Patches {
 public static class PausedAreaWorkFilter {public static bool HasPermittedHaulingContext(State.PawnApparelState s,ApparelRule r)=>false;}
 public static class RestActivityPolicy {public static bool Preserves(State.PawnApparelState s,ApparelRule r,Job j)=>false;}
}
namespace AutomaticOutfitManager.UI {
 public static partial class MainRulesWindow {
  public static void Toggle(ApparelRule r,AutomaticOutfitManagerGameComponent c)=>ToggleWorkPause(r,c);
  private static bool TracksRule(State.PawnApparelState s,string id)=>false;
 }
}
