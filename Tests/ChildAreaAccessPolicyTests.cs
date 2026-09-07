using System;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using Verse;

namespace Verse
{
    public enum DevelopmentalStage { Baby, Child, Adult }
    public class RaceProperties { public bool Humanlike; }
    public class Pawn { public RaceProperties RaceProps; public DevelopmentalStage DevelopmentalStage; }
}
namespace AutomaticOutfitManager.Rules
{
    public class ApparelRule { public bool Enabled = true, AllowChildren; }
}
class ChildAreaAccessPolicyTests
{
    static int checks;
    static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        checks++;
    }
    static int Main()
    {
        try
        {
            foreach (bool humanlike in new[] { false, true })
            foreach (DevelopmentalStage stage in Enum.GetValues(typeof(DevelopmentalStage)))
            foreach (bool enabled in new[] { false, true })
            foreach (bool allow in new[] { false, true })
            {
                var pawn = new Pawn { RaceProps = new RaceProperties { Humanlike = humanlike }, DevelopmentalStage = stage };
                var rule = new ApparelRule { Enabled = enabled, AllowChildren = allow };
                bool expected = humanlike && stage == DevelopmentalStage.Child && enabled && !allow;
                Check(ChildAreaAccessPolicy.Disallows(pawn, rule) == expected,
                    $"humanlike={humanlike}, stage={stage}, enabled={enabled}, allow={allow}");
            }
            var child = new Pawn { RaceProps = new RaceProperties { Humanlike = true }, DevelopmentalStage = DevelopmentalStage.Child };
            var off = new ApparelRule();
            Check(!ChildAreaAccessPolicy.Disallows(null, off), "missing pawn");
            Check(!ChildAreaAccessPolicy.Disallows(child, null), "missing rule");
            Check(!ChildAreaAccessPolicy.Disallows(new Pawn(), off), "missing race");
            Check(ChildAreaAccessPolicy.Disallows(child, off), "new option defaults off");
            off.AllowChildren = true;
            Check(!ChildAreaAccessPolicy.Disallows(child, off), "enabling takes effect without stale cache");
            off.AllowChildren = false;
            Check(ChildAreaAccessPolicy.Disallows(child, off), "disabling takes effect without stale cache");
            Console.WriteLine($"PASS {checks} child access checks");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex); return 1; }
    }
}
