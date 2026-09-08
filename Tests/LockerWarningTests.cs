using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.UI;
using RimWorld;
using UnityEngine;
using Verse;

// Native world and drawing effects are recorded here. Geometry, storage
// assessment, warning cache, and hover/click handling are production code.
namespace UnityEngine
{
    public struct Color { public float r,g,b; public Color(float r,float g,float b) {this.r=r;this.g=g;this.b=b;} public static Color red => new Color(1,0,0); public static Color white=>new Color(1,1,1); public static bool operator ==(Color a,Color b)=>a.Equals(b); public static bool operator !=(Color a,Color b)=>!a.Equals(b); }
    public struct Rect { public Rect(float x,float y,float w,float h) {} }
    public enum EventType { Repaint, Layout }
    public class Event { public static Event current = new Event(); public EventType type; }
    public static class Time { public static float realtimeSinceStartup; public static int frameCount; }
    public static class GUI { public static Color color; }
}
namespace Verse
{
    public struct IntVec3
    {
        public int x,z; public IntVec3(int x,int z=0) {this.x=x;this.z=z;}
        public bool IsValid => x>=0; public bool InBounds(Map map) => x<100 && z<100;
        public bool Standable(Map map) => !map.Walls.Contains(this);
        public ISlotGroup GetSlotGroup(Map map) => map.Storage.TryGetValue(this,out var group)?group:null;
    }
    public class Map { public IntVec3 Size=new IntVec3(100,100); public CellIndices cellIndices=new CellIndices(); public HashSet<IntVec3> Walls=new HashSet<IntVec3>(); public Dictionary<IntVec3,ISlotGroup> Storage=new Dictionary<IntVec3,ISlotGroup>(); }
    public class CellIndices { public IntVec3 IndexToCell(int index)=>new IntVec3(index%100,index/100); }
    public abstract class GameComponent { public virtual void GameComponentUpdate() {} }
    public class Game { public LockerOverlapOverlay Overlay; public T GetComponent<T>() where T:class=>Overlay as T; }
    public static class Current {public static Game Game;}
    public interface ICellBoolGiver {Color Color{get;} bool GetCellBool(int index); Color GetCellExtraColor(int index);}
    public class CellBoolDrawer
    {
        ICellBoolGiver giver; public CellBoolDrawer(ICellBoolGiver giver,int x,int z,float opacity){this.giver=giver;}
        public void SetDirty(){} public void MarkForDraw(){}
        public void CellBoolDrawerUpdate(){GenDraw.Stripes=Enumerable.Range(0,10000).Where(giver.GetCellBool).Select(i=>new IntVec3(i%100,i/100)).ToList();}
    }
    public class Area
    {
        public Map Map; public HashSet<IntVec3> Cells=new HashSet<IntVec3>();
        public IEnumerable<IntVec3> ActiveCells => Cells;
        public bool this[IntVec3 cell] => Cells.Contains(cell);
    }
    public class ThingDef { public object apparel; public bool IsWeapon; }
    public class Thing { public bool Destroyed; public ThingDef def; public string LabelCap="saved item"; }
    public class Pawn { public Map Map; }
    public class SpecialThingFilterDef { }
    public static class Find { public static Map CurrentMap; }
    public static class Text { public static bool WordWrap=true; }
    public static class StringExtensions { public static string Truncate(this string value,float width) => value; }
    public static class Widgets
    {
        public static bool Click;
        public static List<string> Labels=new List<string>();
        public static void Label(Rect rect,string label) => Labels.Add(label);
        public static void DrawHighlightIfMouseover(Rect rect) { }
        public static bool ButtonInvisible(Rect rect) { bool result=Click; Click=false; return result; }
    }
    public static class TooltipHandler { public static void TipRegion(Rect rect,string tip) { } }
    public static class Mouse { public static bool Hover; public static bool IsOver(Rect rect)=>Hover; }
    public static class GenDraw
    {
        public static List<IntVec3> Edges,Stripes;
        public static void DrawFieldEdges(List<IntVec3> cells,Color color)=>Edges=new List<IntVec3>(cells);
        public static void DrawDiagonalStripes(List<IntVec3> cells,Color color)=>Stripes=new List<IntVec3>(cells);
    }
    public static class CameraJumper { public static IntVec3 Target; public static Map Map; public static void TryJump(IntVec3 target,Map map) {Target=target;Map=map;} }
}
namespace RimWorld
{
    public interface ISlotGroup { StorageSettings Settings { get; } }
    public class SlotGroup : ISlotGroup { public StorageSettings Settings {get;}=new StorageSettings(); }
    public class ThingFilter { public HashSet<SpecialThingFilterDef> Flags=new HashSet<SpecialThingFilterDef>(); public bool Allows(SpecialThingFilterDef flag)=>Flags.Contains(flag); }
    public class StorageSettings
    {
        public ThingFilter filter=new ThingFilter();
        public Func<Thing,bool> Accept=item=>true;
        public int Calls;
        public bool AllowedToAccept(Thing item) {Calls++; return Accept(item);}
    }
}
namespace AutomaticOutfitManager.Patches
{
    public static class ManagedGearStorageFilterDefs
    {
        public static SpecialThingFilterDef Apparel=new SpecialThingFilterDef(),Weapon=new SpecialThingFilterDef();
        public static SpecialThingFilterDef For(bool weapon,bool managed)=>weapon?Weapon:Apparel;
    }
}
namespace AutomaticOutfitManager.Rules
{
    public class ApparelRule
    {
        public string Id,Name;
        public bool Enabled=true,IsNonWork,DefaultToSavedPersonalOutfit,HasWeaponRequirement,WorkAreaPaused;
        public Area Area,ChangingArea;
        public List<ThingDef> RequiredApparel=new List<ThingDef>();
    }
}
namespace AutomaticOutfitManager.State
{
    public class PawnApparelState
    {
        public Pawn Pawn; public string ActiveRuleId;
        public List<string> CurrentRuleIds=new List<string>(),RestorationSourceRuleIds=new List<string>();
        public List<Thing> OriginalApparel=new List<Thing>(); public Thing OriginalWeapon;
    }
}
namespace AutomaticOutfitManager.Core
{
    public class AutomaticOutfitManagerGameComponent { public List<ApparelRule> Rules=new List<ApparelRule>(); public List<PawnApparelState> PawnStates=new List<PawnApparelState>(); }
}
namespace AutomaticOutfitManager.UI { internal static class RuleTypeStyle {internal static string RuleName(ApparelRule rule)=>rule.Name;} }

internal static class LockerWarningTests
{
    private static int checks;
    private static void Check(bool value,string name) { if(!value) throw new Exception(name); checks++; }
    private static Area Area(Map map,params int[] cells)=>new Area {Map=map,Cells=new HashSet<IntVec3>(cells.Select(x=>new IntVec3(x)))};
    private static void SameCells(IEnumerable<IntVec3> actual, params int[] cells)=>Check(
        new HashSet<IntVec3>(actual).SetEquals(cells.Select(x=>new IntVec3(x))),"exact painted cells");
    private static int Main()
    {
        try
        {
            var map=new Map(); Find.CurrentMap=map; Current.Game=new Game(); Current.Game.Overlay=new LockerOverlapOverlay(Current.Game);
            var otherMap=new Map();
            var locker=Area(map,1,2,5,6);
            var work=new ApparelRule {Id="work",Name="Ship",Area=Area(map,2,3,4,5),ChangingArea=locker};
            var second=new ApparelRule {Id="second",Name="Kitchen",Area=Area(map,5,6)};
            var disabled=new ApparelRule {Enabled=false,Area=Area(map,1)};
            var nonWork=new ApparelRule {IsNonWork=true,Area=Area(map,1)};
            var remote=new ApparelRule {Area=Area(otherMap,1)};
            var rules=new List<ApparelRule>{work,second,disabled,nonWork,remote};
            var shape=LockerAreaAssessment.Inspect(locker,rules);
            SameCells(shape.OverlapCells,2,5,6); SameCells(shape.OutsideCells,1);
            Check(shape.WorkRules.SequenceEqual(new[]{work,second}),"only contributing Work rules in order");
            Check(shape.PaintedCount==4 && shape.StandableOutsideCount==1,"union avoids double-counting");
            work.WorkAreaPaused=true;
            SameCells(LockerAreaAssessment.Inspect(locker,rules).OverlapCells,2,5,6);
            map.Walls.Add(new IntVec3(1));
            Check(LockerAreaAssessment.Inspect(locker,rules).StandableOutsideCount==0,"walls are not changing space");
            map.Walls.Clear();
            SameCells(LockerAreaAssessment.Inspect(locker,new[]{work}).OverlapCells,2,5);
            Check(LockerAreaAssessment.Inspect(null,rules).PaintedCount==0,"no locker");
            Check(LockerAreaAssessment.Inspect(Area(map),rules).PaintedCount==0,"empty paint");
            Check(LockerAreaAssessment.Inspect(Area(null,1),rules).PaintedCount==0,"unloaded map");

            Check(shape.NonWorkOverlapCells.Count==1 && shape.NonWorkRules.Single()==nonWork,"Non-Work overlap is tracked separately from Work safety");
            nonWork.Enabled=false;
            var group=new SlotGroup(); map.Storage[new IntVec3(1)]=group;
            var apparelFlag=AutomaticOutfitManager.Patches.ManagedGearStorageFilterDefs.Apparel;
            var weaponFlag=AutomaticOutfitManager.Patches.ManagedGearStorageFilterDefs.Weapon;
            var jacket=new Thing {def=new ThingDef{apparel=new object()},LabelCap="Flak jacket (good 90%)"};
            var weapon=new Thing {def=new ThingDef{IsWeapon=true}};
            var storage=LockerStorageAssessment.Inspect(map,shape.OutsideCells,true,true,new[]{jacket,weapon});
            Check(storage.HasStorage && storage.ExcludesManagedApparel && storage.ExcludesManagedWeapons,"managed flags off");
            group.Settings.filter.Flags.Add(apparelFlag);
            group.Settings.Accept=item=>item!=jacket;
            storage=LockerStorageAssessment.Inspect(map,shape.OutsideCells,true,true,new[]{jacket,jacket,weapon});
            Check(!storage.ExcludesManagedApparel && storage.ExcludesManagedWeapons,"category flags independent");
            Check(storage.RejectedSavedItems.SequenceEqual(new[]{jacket}),"native exact-item acceptance includes quality/condition filters");
            int calls=group.Settings.Calls;
            LockerStorageAssessment.Inspect(map,new[]{new IntVec3(1),new IntVec3(1)},true,false,new[]{jacket,jacket});
            Check(group.Settings.Calls==calls+1,"deduplicate native acceptance per item and group");
            var secondGroup=new SlotGroup(); secondGroup.Settings.filter.Flags.Add(weaponFlag);
            map.Storage[new IntVec3(9)]=secondGroup;
            storage=LockerStorageAssessment.Inspect(map,new[]{new IntVec3(1),new IntVec3(9)},true,true,new[]{jacket,weapon});
            Check(!storage.ExcludesManagedWeapons && storage.RejectedSavedItems.Count==0,"another accepting shelf resolves rejection");
            storage=LockerStorageAssessment.Inspect(map,new[]{new IntVec3(3)},true,true,new[]{jacket});
            Check(!storage.HasStorage && !storage.ExcludesManagedApparel,"no storage is distinct from rejecting filters");

            var component=new AutomaticOutfitManagerGameComponent {Rules=rules};
            var state=new PawnApparelState {Pawn=new Pawn{Map=map},ActiveRuleId=work.Id,OriginalApparel=new List<Thing>{jacket}};
            component.PawnStates.Add(state);
            var cache=new LockerConfigurationWarnings(); cache.Refresh(map,component);
            var warnings=cache.For(work);
            Check(warnings.Count==2 && warnings[0].Cells.Count==3,"overlap and saved-item filter warning separate");
            Check(warnings[0].Color.g>0 && warnings[1].Tip.Contains(jacket.LabelCap),"partial amber and exact saved item explained");
            float y=10; Mouse.Hover=true; Widgets.Click=true; Event.current.type=EventType.Repaint;
            var beforeColor=GUI.color; bool beforeWrap=Text.WordWrap;
            cache.Draw(work,0,ref y,500); Current.Game.Overlay.GameComponentUpdate();
            Check(y==54 && GUI.color.Equals(beforeColor) && Text.WordWrap==beforeWrap,"two reserved rows, GUI state restored");
            SameCells(GenDraw.Edges,2,5,6); SameCells(GenDraw.Stripes,2,5,6);
            Check(warnings[0].Cells.Contains(CameraJumper.Target) && CameraJumper.Map==map,"click centers on an actual overlap cell");
            Check(locker.Cells.Count==4 && work.Enabled && second.Enabled,"warning is non-mutating");
            GenDraw.Edges=null; Mouse.Hover=false; Time.frameCount+=2; Current.Game.Overlay.GameComponentUpdate();
            Check(GenDraw.Edges==null,"overlay expires when tab no longer requests it");
            cache.ShowMapOverlaps(); Current.Game.Overlay.GameComponentUpdate();
            SameCells(GenDraw.Edges,2,5,6); GenDraw.Edges=null;
            Mouse.Hover=true; Find.CurrentMap=otherMap; cache.Draw(work,0,ref y,500); Current.Game.Overlay.GameComponentUpdate();
            Check(GenDraw.Edges==null,"no overlay on a different map"); Find.CurrentMap=map;
            disabled.Enabled=true;
            group.Settings.Accept=item=>true;
            Time.realtimeSinceStartup=.5f; cache.Refresh(map,component);
            Check(ReferenceEquals(warnings,cache.For(work)),"stable cached layout inside interval");
            Time.realtimeSinceStartup=1.1f; cache.Refresh(map,component);
            warnings=cache.For(work);
            Check(warnings[0].Color.Equals(Color.red) && warnings[0].Cells.Count==4,"union full coverage turns red after edit while paused");
            Check(warnings[1].Cells==null,"storage warning never highlights unrelated overlap");
            disabled.Enabled=false; work.Area.Cells.Clear(); second.Area.Cells.Clear();
            Time.realtimeSinceStartup=2.2f; cache.Refresh(map,component);
            Check(cache.For(work).Count==0,"repaint and accepting filter resolve both warnings without game ticks");
            work.Area=Area(map,2,5); Time.realtimeSinceStartup=3.3f; cache.Refresh(map,component);
            Check(cache.For(work).Count==1,"reassigned area recalculated");
            cache.Refresh(otherMap,component); Check(cache.For(work).Count==0,"map change invalidates immediately");
            cache.Refresh(map,component); Check(cache.For(work).Count==1,"return to map recomputes");
            cache.Clear(); cache.Refresh(map,component); Check(cache.For(work).Count==1,"close/reopen clears stale references");
            nonWork.Enabled=true; Time.realtimeSinceStartup=4.4f; cache.Refresh(map,component);
            var nonWorkEntry=cache.For(work).Single(entry=>entry.Cells?.Contains(new IntVec3(1))==true);
            Check(nonWorkEntry.Color.g>0 && nonWorkEntry.Color.b>0,"Non-Work overlap is advisory, not automatically invalid changing space");
            cache.ShowMapOverlaps(); Current.Game.Overlay.GameComponentUpdate(); SameCells(GenDraw.Edges,1,2,5);
            Mouse.Hover=true; cache.Highlight(work,new Rect()); Current.Game.Overlay.GameComponentUpdate(); SameCells(GenDraw.Edges,1,2,5);
            cache.Clear(); GenDraw.Edges=null; Current.Game.Overlay.GameComponentUpdate(); Check(GenDraw.Edges==null,"closing tab clears overlay immediately");
            Console.WriteLine($"PASS {checks} locker warning checks"); return 0;
        }
        catch(Exception ex) {Console.Error.WriteLine(ex);return 1;}
    }
}
