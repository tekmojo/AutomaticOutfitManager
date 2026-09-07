// Headless contract checks compile the production rule/evaluator/outfit files.
// The small API doubles below intentionally do not simulate jobs or pathfinding.
using System;
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Detection;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using RimWorld;
using Verse;

internal static class NonWorkOutfitContractTests
{
    private static int passed;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        passed++;
        Console.WriteLine("PASS " + name);
    }
    public static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--verify-failure-reporting"))
                throw new InvalidOperationException("Intentional failure-reporting check");
            RunChecks();
            return 0;
        }
        catch (Exception exception)
        {
            // Report a failed check to the calling script. An unhandled .NET
            // exception opens a Windows crash dialog and can stall cleanup.
            Console.Error.WriteLine("FAIL " + exception);
            return 1;
        }
    }

    private static void CheckNonWorkBuffers()
    {
        var buffer = new NonWorkOutfitBuffer();
        Check(buffer.Start(10, 3, true, true, true), "non-work area activity starts retention");
        Check(!buffer.Complete(10, true, 3) && buffer.Completed == 0, "source-area meal does not spend follow-up credit");
        Check(buffer.Start(11, 3, false, true, false) && buffer.PendingJobId == -1, "movement preserves outfit without becoming a buffered task");
        Check(buffer.Start(12, 3, false, true, true) && buffer.Completed == 0, "starting outside task does not count before success");
        Check(!buffer.Complete(13, true, 3) && buffer.PendingJobId == 12, "unrelated job completion cannot spend buffer");
        Check(!buffer.Complete(12, false, 3) && buffer.Completed == 0 && buffer.PendingJobId == -1, "failed task clears candidate without credit");
        Check(buffer.Start(14, 3, false, true, true) && buffer.Complete(14, true, 3) && buffer.Completed == 1, "successful compatible follow-up counts once");
        Check(!buffer.Complete(14, true, 3) && buffer.Completed == 1, "duplicate completion cannot count twice");
        Check(buffer.Start(15, 3, false, true, true), "second task admitted");
        // Mirrors the primitive fields persisted by ExposeData; no Job is deep-saved.
        var loaded = new NonWorkOutfitBuffer { Completed = buffer.Completed,
            PendingJobId = buffer.PendingJobId, LastCompletedJobId = buffer.LastCompletedJobId,
            LastStartedJobId = buffer.LastStartedJobId };
        Check(loaded.Start(15, 3, false, true, true) && loaded.Completed == 1 && loaded.PendingJobId == 15,
            "loaded running candidate preserves completed count and identity");
        Check(loaded.Complete(15, true, 3) && loaded.Completed == 2, "loaded candidate counts only when it succeeds");
        Check(!loaded.Start(16, 3, false, false, true) && loaded.Completed == 2, "conflicting new work ends retention without false completion");
        Check(loaded.Start(17, 3, false, true, true) && loaded.Complete(17, true, 3), "final compatible task completes allowance");
        Check(!loaded.Start(18, 3, false, true, true), "exhausted allowance releases before another outside task");
        Check(loaded.Start(19, 3, true, true, true) && loaded.Completed == 0, "real source-area activity starts a fresh allowance");
        Check(!loaded.Start(20, 0, false, true, true), "Immediate never creates follow-up credit");
    }

    private static void RunChecks()
    {
        CheckNonWorkBuffers();
        var component = new AutomaticOutfitManagerGameComponent();
        AutomaticOutfitManagerGameComponent.Current = component;
        var shirtDef = new ThingDef { apparel = new ApparelProperties() };
        var armorDef = new ThingDef { apparel = new ApparelProperties() };
        var gunDef = new ThingDef { IsWeapon = true, IsRangedWeapon = true };
        var shirt = new Apparel { def = shirtDef, HitPoints = 10 };
        var sameKindShirt = new Apparel { def = shirtDef };
        var armor = new Apparel { def = armorDef };
        var gun = new ThingWithComps { def = gunDef };
        var pawn = new Pawn();
        pawn.apparel.WornApparel.Add(shirt);
        var rule = new ApparelRule { Kind = AreaRuleKind.NonWork };
        Check(rule.DefaultToSavedPersonalOutfit, "new rules default to saved personal outfits");
        rule.RequiredApparel.Add(armorDef);
        rule.RequiredWeapons.Add(gunDef);
        rule.AllowedApparelHitPoints = new FloatRange(.5f, 1f);
        Check(RuleEvaluator.HasMissingRequiredGear(pawn, rule), "no saved outfit uses fallback requirements");
        pawn.apparel.WornApparel.Add(armor);
        pawn.equipment.Primary = gun;
        Check(!RuleEvaluator.HasMissingRequiredGear(pawn, rule), "fallback permits complete selected gear");
        pawn.apparel.WornApparel.Remove(armor);
        pawn.equipment.Primary = null;
        var saved = SavedNonWorkOutfit.Capture(pawn);
        component.Saved[pawn] = saved;
        Check(!RuleEvaluator.HasMissingRequiredGear(pawn, rule), "saved outfit overrides incompatible fallback and its condition range");
        Check(!RuleEvaluator.RequiredApparelFor(pawn, rule).Any(), "saved outfit removes fallback from shared apparel planner");
        RuleEvaluator.TryCombinedWeaponRequirement(new[] { rule }, out var combined, pawn);
        Check(!combined.HasRequirement, "saved unarmed outfit removes fallback weapon from combined planner");
        pawn.equipment.Primary = gun;
        Check(RuleEvaluator.HasMissingRequiredGear(pawn, rule), "saved unarmed slot rejects current primary");
        component.State = new PawnApparelState { WeaponRuleOverrideExplicit = true };
        Check(!RuleEvaluator.HasMissingRequiredGear(pawn, rule), "explicit player weapon override retains existing exemption");
        component.State = null;
        pawn.equipment.Primary = null;
        pawn.apparel.WornApparel.Add(armor);
        Check(RuleEvaluator.HasMissingRequiredGear(pawn, rule), "extra work armor must be removed even when saved shirt remains worn");
        pawn.apparel.WornApparel.Clear();
        pawn.apparel.WornApparel.Add(sameKindShirt);
        Check(RuleEvaluator.HasMissingRequiredGear(pawn, rule), "same definition cannot replace exact saved item silently");
        pawn.apparel.WornApparel.Clear();
        pawn.apparel.WornApparel.Add(armor);
        pawn.equipment.Primary = gun;
        Check(RuleEvaluator.UsesSavedNonWorkOutfit(pawn, rule) &&
              RuleEvaluator.HasMissingRequiredGear(pawn, rule), "unavailable saved items never activate otherwise satisfied fallback");
        rule.DefaultToSavedPersonalOutfit = false;
        Check(!RuleEvaluator.UsesSavedNonWorkOutfit(pawn, rule) &&
              !RuleEvaluator.HasMissingRequiredGear(pawn, rule),
            "unchecked preference uses selected gear despite an unavailable saved snapshot");
        RuleEvaluator.TryCombinedWeaponRequirement(new[] { rule }, out combined, pawn);
        Check(combined.HasRequirement && combined.Matches(gun) &&
              RuleEvaluator.RequiredApparelFor(pawn, rule).Contains(armorDef),
            "unchecked preference reaches both apparel and weapon planners");
        var personalArea = new ApparelRule { Kind = AreaRuleKind.NonWork };
        Check(RuleEvaluator.SavedNonWorkOutfitConflicts(pawn, new[] { personalArea, rule }),
            "saved-personal and selected-gear non-work rules detect conflicting overlap");
        rule.DefaultToSavedPersonalOutfit = true;
        Check(ReferenceEquals(component.Saved[pawn], saved) &&
              RuleEvaluator.HasMissingRequiredGear(pawn, rule),
            "re-enabling preference reuses the untouched snapshot");
        var work = new ApparelRule();
        work.RequiredApparel.Add(armorDef);
        work.RequiredWeapons.Add(gunDef);
        Check(!RuleEvaluator.HasMissingRequiredGear(pawn, work), "work requirements are unchanged for a pawn with a saved outfit");
        Check(RuleEvaluator.SavedNonWorkOutfitConflicts(pawn, new[] { work, rule }),
            "overlapping work armor conflicts with the saved civilian outfit");
        Check(!RuleEvaluator.SavedNonWorkOutfitConflicts(pawn, new[] { work }),
            "a work-only destination does not activate the non-work conflict guard");
        var compatibleWork = new ApparelRule();
        compatibleWork.RequiredApparel.Add(shirtDef);
        Check(!RuleEvaluator.SavedNonWorkOutfitConflicts(pawn, new[] { compatibleWork, rule }),
            "compatible overlapping work requirements remain usable");
        var workState = new PawnApparelState
        {
            Pawn = pawn,
            ApparelInterventionActive = true, OriginalApparel = new List<Apparel> { shirt },
            WeaponInterventionActive = true, OriginalWeapon = null
        };
        var captured = SavedNonWorkOutfit.FromExistingWorkSession(workState, work);
        Check(captured.Apparel.SequenceEqual(new[] { shirt }) && captured.Weapon == null,
            "legacy in-flight snapshot uses original outfit rather than issued gear");
        workState.NonWorkFallbackActive = true;
        Check(SavedNonWorkOutfit.FromExistingWorkSession(workState, work) == null,
            "fallback session cannot become an automatic pre-work snapshot");
        workState.NonWorkFallbackActive = false;
        workState.NonWorkRestorationRuleId = "returning";
        Check(SavedNonWorkOutfit.FromExistingWorkSession(workState, work) == null,
            "non-work restoration target cannot be recaptured as a pre-work snapshot");
        workState.NonWorkRestorationRuleId = null;
        Check(SavedNonWorkOutfit.FromExistingWorkSession(workState, rule) == null,
            "non-work rule cannot create a pre-work snapshot");
        pawn.apparel.WornApparel.Clear();
        pawn.equipment.Primary = null;
        component.Saved[pawn] = SavedNonWorkOutfit.Capture(pawn);
        Check(RuleEvaluator.UsesSavedNonWorkOutfit(pawn, rule) &&
              !RuleEvaluator.HasMissingRequiredGear(pawn, rule), "empty automatic snapshot is a saved outfit, not absent data");
        var emptyArea = new ApparelRule { Kind = AreaRuleKind.NonWork, DefaultToSavedPersonalOutfit = false };
        var returning = new PawnApparelState
        {
            Pawn = pawn, ActiveRuleId = work.Id,
            ManagedApparel = new List<Apparel> { armor },
            ManagedWeapons = new List<ThingWithComps> { gun }
        };
        component.Rules = new List<ApparelRule> { work, emptyArea };
        component.State = returning;
        component.Saved[pawn] = saved;
        pawn.apparel.WornApparel.Add(shirt);
        pawn.apparel.WornApparel.Add(armor);
        pawn.equipment.Primary = gun;
        Check(RuleEvaluator.NeedsNonWorkGearReturn(pawn, emptyArea) &&
              RuleEvaluator.HasMissingRequiredGear(pawn, emptyArea),
            "unchecked empty non-work rule blocks entry until issued work gear is returned");
        var afterReturn = NonWorkOutfitPolicy.Target(pawn, emptyArea);
        Check(afterReturn.Apparel.SequenceEqual(new[] { shirt }) && afterReturn.Weapon == null,
            "work-gear return retains worn personal clothes and allows unarmed slots");
        Check(ReferenceEquals(component.Saved[pawn], saved) && saved.Apparel.Contains(shirt),
            "temporary return target does not replace the automatic personal snapshot");
        Check(RuleEvaluator.SelectedNonWorkOutfitConflicts(pawn, new[] { work, emptyArea }),
            "overlap cannot demand removal and immediate re-equipping of the same work gear");
        emptyArea.RequiredApparel.Add(armorDef);
        emptyArea.RequiredWeapons.Add(gunDef);
        Check(RuleEvaluator.SelectedNonWorkOutfitConflicts(pawn, new[] { work, emptyArea }),
            "fallback cannot require gear from a work rule selected for removal");
        returning.ActiveRuleId = emptyArea.Id;
        Check(!RuleEvaluator.NeedsNonWorkGearReturn(pawn, emptyArea),
            "gear issued for a non-work rule is retained rather than returned repeatedly");
        returning.ActiveRuleId = work.Id;
        returning.WeaponRuleOverrideExplicit = true;
        Check(NonWorkOutfitPolicy.Target(pawn, emptyArea).Weapon == gun,
            "work-gear return respects explicit player weapon choice");
        returning.ManagedApparel.Clear();
        returning.ManagedWeapons.Clear();
        Check(!RuleEvaluator.NeedsNonWorkGearReturn(pawn, emptyArea),
            "personally worn armor is not automatically classified as issued work gear");
        component.State = null;
        var legacy = new ApparelRule { Kind = AreaRuleKind.NonWork, DefaultToSavedPersonalOutfit = false };
        Scribe.Loading = true;
        legacy.ExposeData();
        Scribe.Loading = false;
        Check(legacy.Kind == AreaRuleKind.Work, "old saves default to Work Area rules");
        Check(legacy.CustomDescription == null, "old saves leave rule descriptions automatic");
        Scribe.LoadedValues["customDescription"] = "Kitchen staff — clean clothes";
        var describedRule = new ApparelRule();
        Scribe.Loading = true;
        describedRule.ExposeData();
        Scribe.Loading = false;
        Check(describedRule.CustomDescription == "Kitchen staff — clean clothes",
            "saved custom rule description loads without replacing the rule name");
        Scribe.LoadedValues.Remove("customDescription");
        Check(legacy.DefaultToSavedPersonalOutfit, "saves without the preference key default to checked");
        Scribe.LoadedValues["defaultToSavedPersonalOutfit"] = false;
        Scribe.Loading = true;
        rule.ExposeData();
        Scribe.Loading = false;
        Check(!rule.DefaultToSavedPersonalOutfit, "an explicitly saved unchecked preference loads as unchecked");

        Check(legacy.RemoveAllWorkOutfits, "old saves default to returning all work outfits");
        var otherWork = new ApparelRule { Name = "Security" };
        otherWork.RequiredWeapons.Add(gunDef);
        var workshop = new ApparelRule { Name = "Workshop" };
        workshop.RequiredApparel.Add(armorDef);
        var partial = new ApparelRule { Kind = AreaRuleKind.NonWork,
            RemoveAllWorkOutfits = false, DefaultToSavedPersonalOutfit = false };
        Check(new ApparelRule().RemoveAllWorkOutfits, "new rules return all work outfits by default");
        returning.ManagedApparel.Add(armor);
        returning.ManagedWeapons.Add(gun);
        returning.WeaponRuleOverrideExplicit = false;
        component.State = returning;
        saved.WorkGear.Clear();
        saved.RecordIssuedGear(returning, new[] { armor }, gun, new[] { workshop, otherWork });
        Check(saved.WorkGear.Count == 2 &&
              saved.WorkGear.Single(entry => entry.Item == armor).RuleIds.SequenceEqual(new[] { workshop.Id }) &&
              saved.WorkGear.Single(entry => entry.Item == gun).RuleIds.SequenceEqual(new[] { otherWork.Id }),
            "production issuance records each item against its actual source requirements");
        Check(!RuleEvaluator.NeedsNonWorkGearReturn(pawn, partial),
            "explicit empty selection retains all issued gear");
        returning.ApparelInterventionActive = true;
        Check(NonWorkOutfitPolicy.NeedsStateHandoff(pawn, partial),
            "keep-all handoff rebases the outgoing restoration target so idle recall cannot strip retained gear");
        partial.WorkOutfitsToRemove.Add(workshop.Id);
        Check(NonWorkOutfitPolicy.ShouldReturn(pawn, partial, armor) &&
              !NonWorkOutfitPolicy.ShouldReturn(pawn, partial, gun),
            "selecting one work rule returns its armor and keeps another rule's weapon");
        var partialTarget = NonWorkOutfitPolicy.Target(pawn, partial);
        Check(partialTarget.Apparel.SequenceEqual(new[] { shirt }) && partialTarget.Weapon == gun,
            "unchecked partial return allows vacated slots while retaining unselected gear");
        partial.DefaultToSavedPersonalOutfit = true;
        partialTarget = NonWorkOutfitPolicy.Target(pawn, partial);
        Check(partialTarget.Apparel.SequenceEqual(new[] { shirt }) && partialTarget.Weapon == gun && saved.Weapon == null,
            "saved personal outfit fills around retained gear without rewriting its unarmed snapshot");
        pawn.apparel.WornApparel.Remove(armor);
        component.State = null;
        Check(!RuleEvaluator.HasMissingRequiredGear(pawn, partial),
            "retained provenance survives restoration-state removal without a repeat return");
        pawn.apparel.WornApparel.Add(armor);
        component.State = returning;
        otherWork.RequiredApparel.Add(armorDef);
        saved.RecordIssuedGear(returning, new[] { armor }, null, new[] { otherWork });
        Check(!NonWorkOutfitPolicy.ShouldReturn(pawn, partial, armor),
            "shared issued gear stays while one recorded source rule is unselected");
        partial.WorkOutfitsToRemove.Add(otherWork.Id);
        Check(NonWorkOutfitPolicy.ShouldReturn(pawn, partial, armor) &&
              NonWorkOutfitPolicy.ShouldReturn(pawn, partial, gun),
            "shared issued gear returns when every recorded source is selected");
        workshop.Name = "Renamed workshop";
        Check(NonWorkOutfitPolicy.ShouldReturn(pawn, partial, armor),
            "renaming a source rule preserves selection by stable identity");
        saved.WorkGear.Single(entry => entry.Item == armor).RuleIds.Clear();
        Check(!NonWorkOutfitPolicy.ShouldReturn(pawn, partial, armor),
            "specific selections conservatively retain legacy gear without source history");
        partial.RemoveAllWorkOutfits = true;
        Check(NonWorkOutfitPolicy.ShouldReturn(pawn, partial, armor),
            "All returns legacy unattributed work gear too");
        partial.RemoveAllWorkOutfits = false;
        partial.WorkOutfitsToRemove.Clear();
        partial.DefaultToSavedPersonalOutfit = false;
        var anotherGunDef = new ThingDef { IsWeapon = true, IsRangedWeapon = true };
        partial.RequiredWeapons.Add(anotherGunDef);
        Check(RuleEvaluator.SelectedNonWorkOutfitConflicts(pawn, new[] { partial }),
            "incompatible fallback weapon blocks entry rather than replacing retained work weapon");
        partial.RequiredWeapons.Clear();
        partial.RequiredApparel.Add(armorDef);
        partial.AllowedApparelHitPoints = new FloatRange(.99f, 1f);
        armor.HitPoints = 10;
        Check(RuleEvaluator.SelectedNonWorkOutfitConflicts(pawn, new[] { partial }),
            "fallback quality requirements cannot silently replace retained work apparel");
        partial.AllowedApparelHitPoints = FloatRange.ZeroToOne;
        Check(!RuleEvaluator.SelectedNonWorkOutfitConflicts(pawn, new[] { partial }),
            "compatible fallback apparel can coexist with retained work gear");
        partial.RequiredApparel.Clear();
        var personalArmor = new Apparel { def = armorDef };
        saved.Apparel.Add(personalArmor);
        partial.DefaultToSavedPersonalOutfit = true;
        partialTarget = NonWorkOutfitPolicy.Target(pawn, partial);
        Check(partialTarget.Apparel.Contains(armor) && !partialTarget.Apparel.Contains(personalArmor) &&
              saved.Apparel.Contains(personalArmor),
            "retained work apparel takes its slot without deleting the conflicting personal snapshot item");
        saved.Apparel.Remove(personalArmor);
        Check(RuleEvaluator.SavedNonWorkOutfitConflicts(pawn, new[] { partial,
              new ApparelRule { Kind = AreaRuleKind.NonWork } }),
            "overlapping saved-outfit rules with contradictory removal choices block rather than cycle");
        returning.WeaponRuleOverrideExplicit = true;
        partial.RemoveAllWorkOutfits = true;
        partial.DefaultToSavedPersonalOutfit = false;
        Check(NonWorkOutfitPolicy.Target(pawn, partial).Weapon == gun,
            "explicit player weapon override also survives filtered entry targets");
        saved.Weapon = gun;
        Check(!NonWorkOutfitPolicy.IsIssued(pawn, gun),
            "exact personal weapon reused by work is not reclassified as issued gear");
        saved.Weapon = null;
        Scribe.LoadedValues["removeAllWorkOutfits"] = false;
        Scribe.LoadedValues["workOutfitsToRemove"] = new List<string> { workshop.Id, otherWork.Id };
        Scribe.Loading = true;
        var loadedSelection = new ApparelRule();
        loadedSelection.ExposeData();
        Scribe.Loading = false;
        Check(!loadedSelection.RemoveAllWorkOutfits && loadedSelection.WorkOutfitsToRemove.Count == 2,
            "serialized selective mode and multiple stable source IDs load together");

        // Configuration guard is independent of per-pawn issuance history.
        component.Rules = new List<ApparelRule> { workshop, otherWork };
        var fallbackRule = new ApparelRule { Kind = AreaRuleKind.NonWork, RemoveAllWorkOutfits = false };
        fallbackRule.WorkOutfitsToRemove.Add(workshop.Id);
        Check(NonWorkFallbackPolicy.ConflictingSource(fallbackRule, armorDef, component.Rules) == workshop,
            "fallback armor identifies the selected removal source");
        Check(NonWorkFallbackPolicy.ConflictingSource(fallbackRule, gunDef, component.Rules) == null,
            "unselected work source does not lock its fallback weapon");
        fallbackRule.RemoveAllWorkOutfits = true;
        Check(NonWorkFallbackPolicy.ConflictingSource(fallbackRule, gunDef, component.Rules) == otherWork,
            "All locks gear from every enabled work source");
        otherWork.Enabled = false;
        Check(NonWorkFallbackPolicy.ConflictingSource(fallbackRule, gunDef, component.Rules) == null,
            "disabled work sources do not create configuration conflicts");
        otherWork.Enabled = true;
        fallbackRule.RequiredApparel.Add(armorDef);
        Check(NonWorkFallbackPolicy.FirstConflict(fallbackRule, component.Rules) == workshop &&
              fallbackRule.RequiredApparel.Contains(armorDef),
            "existing conflicting choices are reported without silently deleting them");
        fallbackRule.DefaultToSavedPersonalOutfit = false;
        component.State = null;
        Check(RuleEvaluator.HasMissingRequiredGear(pawn, fallbackRule) &&
              RuleEvaluator.SelectedNonWorkOutfitConflicts(pawn, new[] { fallbackRule }),
            "already-worn conflicting fallback is blocked instead of admitted or reissued");
        fallbackRule.DefaultToSavedPersonalOutfit = true;
        saved.Apparel = pawn.apparel.WornApparel.ToList();
        saved.Weapon = pawn.equipment.Primary;
        Check(!RuleEvaluator.HasMissingRequiredGear(pawn, fallbackRule) &&
              !RuleEvaluator.SelectedNonWorkOutfitConflicts(pawn, new[] { fallbackRule }),
            "valid saved personal snapshot bypasses dormant fallback conflicts");
        component.Saved.Remove(pawn);
        Check(RuleEvaluator.HasMissingRequiredGear(pawn, fallbackRule),
            "checked preference without a snapshot still enforces fallback conflicts");
        fallbackRule.RequiredApparel.Clear();
        Check(NonWorkFallbackPolicy.FirstConflict(fallbackRule, component.Rules) == null,
            "removing the conflicting fallback choice resolves the configuration");
        fallbackRule.RequiredWeapons.Add(gunDef);
        fallbackRule.RemoveAllWorkOutfits = false;
        fallbackRule.WorkOutfitsToRemove.Clear();
        Check(NonWorkFallbackPolicy.FirstConflict(fallbackRule, component.Rules) == null,
            "empty removal selection leaves other work gear available as fallback");
        fallbackRule.WorkOutfitsToRemove.Add(otherWork.Id);
        Check(NonWorkFallbackPolicy.FirstConflict(fallbackRule, component.Rules) == otherWork,
            "changing removal scope detects an existing weapon conflict");
        otherWork.Kind = AreaRuleKind.NonWork;
        Check(NonWorkFallbackPolicy.FirstConflict(fallbackRule, component.Rules) == null,
            "another non-work rule never counts as a removed work source");
        otherWork.Kind = AreaRuleKind.Work;
        otherWork.RequiredWeapons.Clear();
        otherWork.RequiredWeapon = WeaponRequirement.Ranged;
        Check(NonWorkFallbackPolicy.ConflictingSource(fallbackRule, gunDef, component.Rules) == otherWork,
            "legacy ranged source requirements also lock matching fallback weapons");
        Check(NonWorkFallbackPolicy.ConflictingSource(workshop, armorDef, component.Rules) == null,
            "ordinary work-rule pickers are unaffected by removal conflicts");
        CheckSelectionConflicts();
        CheckWorkSnapshotCleanup();
        CheckRepairAndRetrieval();
        Console.WriteLine($"{passed} contract checks passed. Gameplay transitions require RimWorld testing.");
    }

    private static void CheckSelectionConflicts()
    {
        var map = new Map();
        var shirt = new ThingDef { apparel = new ApparelProperties() };
        var gun = new ThingDef { IsWeapon = true, IsRangedWeapon = true };
        var sword = new ThingDef { IsWeapon = true, IsMeleeWeapon = true };
        var work = new ApparelRule { Area = new Area { Map = map } };
        var dining = new ApparelRule { Kind = AreaRuleKind.NonWork, Area = new Area { Map = map } };
        dining.RequiredApparel.Add(shirt);
        dining.RequiredWeapons.Add(gun);
        var rules = new List<ApparelRule> { work, dining };
        Check(NonWorkFallbackPolicy.ConflictingDestination(work, shirt, rules) == dining,
            "Work apparel candidate detects an existing Non-Work fallback removal conflict before mutation");
        Check(NonWorkFallbackPolicy.ConflictingDestination(work, gun, rules) == dining,
            "Work weapon candidate detects an existing Non-Work fallback removal conflict");
        Check(work.RequiredApparel.Count == 0 && work.RequiredWeapons.Count == 0,
            "reverse candidate checks never change Work selections");
        Check(!GearSelectionPolicy.IsRetained(shirt, true, rules),
            "Non-Work fallback selection is not merely retained stock");
        Check(GearSelectionPolicy.IsRetained(sword, true, rules) &&
              !GearSelectionPolicy.IsRetained(sword, false, rules),
            "only remembered stock with no rule selection is retained");
        dining.DefaultToSavedPersonalOutfit = false;
        Check(NonWorkFallbackPolicy.ConflictingDestination(work, shirt, rules) == dining,
            "reverse validation also protects required Non-Work outfits with saved preference off");
        dining.RemoveAllWorkOutfits = false;
        Check(NonWorkFallbackPolicy.ConflictingDestination(work, shirt, rules) == null,
            "intentional Work and Non-Work sharing remains allowed when that outfit is kept");
        dining.WorkOutfitsToRemove.Add(work.Id);
        Check(NonWorkFallbackPolicy.ConflictingDestination(work, shirt, rules) == dining,
            "selective removal is checked using the exact source rule ID");
        dining.Enabled = false;
        Check(NonWorkFallbackPolicy.ConflictingDestination(work, shirt, rules) == null,
            "disabled Non-Work rule does not block a Work candidate");
        Check(!GearSelectionPolicy.IsRetained(shirt, true, rules),
            "disabled rule selection remains selected elsewhere rather than retained");
        dining.Enabled = true;
        work.Enabled = false;
        Check(NonWorkFallbackPolicy.ConflictingDestination(work, shirt, rules) == null,
            "disabled Work rule can be configured before enable-time validation");
        work.Enabled = true;
        work.RequiredApparel.Add(shirt);
        work.RequiredWeapons.Add(gun);
        Check(NonWorkFallbackPolicy.ConflictingDestination(work, null, rules) == dining,
            "existing selections are validated when enabling or reassigning a Work rule");
        dining.Area = new Area { Map = new Map() };
        Check(NonWorkFallbackPolicy.ConflictingDestination(work, shirt, rules) == null &&
              NonWorkFallbackPolicy.FirstConflict(dining, rules) == null,
            "both conflict directions ignore configured rules on other maps");
        Check(GearSelectionPolicy.SelectingRules(shirt, rules).Count == 2,
            "other-map selections remain visible as selections");
        dining.Area = new Area { Map = map };
        dining.WorkOutfitsToRemove.Clear();
        Check(NonWorkFallbackPolicy.FirstConflict(dining, rules, true) == work &&
              NonWorkFallbackPolicy.FirstConflict(dining, rules, false, new[] { work.Id }) == work,
            "All and individual removal previews detect newly contradictory selections");
        Check(!dining.RemoveAllWorkOutfits && dining.WorkOutfitsToRemove.Count == 0 &&
              NonWorkFallbackPolicy.FirstConflict(dining, rules) == null,
            "removal previews leave the saved settings untouched");
        dining.RequiredApparel.Clear();
        dining.RequiredWeapons.Clear();
        dining.RequiredWeapon = WeaponRequirement.Ranged;
        dining.RemoveAllWorkOutfits = true;
        Check(NonWorkFallbackPolicy.ConflictingDestination(work, gun, rules) == dining &&
              NonWorkFallbackPolicy.ConflictingDestination(work, sword, rules) == null,
            "legacy Non-Work weapon categories match only compatible Work candidates");
        Check(GearSelectionPolicy.Selects(dining, gun) && !GearSelectionPolicy.Selects(dining, sword),
            "legacy weapon categories count as selections in stock classification");
        var database = DefDatabase<ThingDef>.AllDefsListForReading.ToList();
        try
        {
            DefDatabase<ThingDef>.AllDefsListForReading.Add(gun);
            Check(NonWorkFallbackPolicy.FirstConflict(dining, rules) == work &&
                  NonWorkFallbackPolicy.ConflictingDestination(work, null, rules) == dining,
                "legacy Non-Work categories are checked in full configuration validation");
        }
        finally
        {
            DefDatabase<ThingDef>.AllDefsListForReading.Clear();
            DefDatabase<ThingDef>.AllDefsListForReading.AddRange(database);
        }
        dining.UseExactWeapons();
        dining.RequiredWeapons.Add(gun);
        dining.RequiredWeapons.Add(sword);
        Check(NonWorkFallbackPolicy.ConflictingDestination(work, gun, rules) == dining,
            "a conflicting exact weapon alternative remains visible even with another alternative");
        work.RequiredWeapons.Clear();
        work.RequiredWeapon = WeaponRequirement.Ranged;
        Check(NonWorkFallbackPolicy.FirstConflict(dining, rules) == work,
            "legacy Work weapon category conflicts with selected Non-Work exact gear");
        work.Enabled = false;
        Check(NonWorkFallbackPolicy.FirstConflict(dining, rules) == null,
            "disabled Work source does not create a Non-Work removal conflict");
        work.Enabled = true;
        work.Kind = AreaRuleKind.NonWork;
        Check(NonWorkFallbackPolicy.FirstConflict(dining, rules) == null,
            "Non-Work to Non-Work sharing never creates a Work removal conflict");
        dining.RequiredWeapons.Clear();
        dining.RequiredWeapon = WeaponRequirement.None;
        Check(!GearSelectionPolicy.IsRetained(gun, true, rules),
            "another selecting rule still prevents retained classification after one selection is removed");
        work.RequiredWeapon = WeaponRequirement.None;
        Check(GearSelectionPolicy.IsRetained(gun, true, rules),
            "removing the last selection changes remembered gear to retained stock");
    }

    private static void CheckRepairAndRetrieval()
    {
        var map = new Map();
        var source = new ApparelRule { Area = new Area { Map = map, Cells = new HashSet<int> { 10 } } };
        var destination = new ApparelRule { Area = new Area { Map = map, Cells = new HashSet<int> { 20 } } };
        var pawn = new Pawn { Map = map, Position = new IntVec3(0) };
        var material = new Thing { Map = map, Spawned = true, PositionHeld = new IntVec3(10),
            def = new ThingDef { category = ThingCategory.Item } };
        var building = new Thing { Map = map, Spawned = true, PositionHeld = new IntVec3(20),
            def = new ThingDef { category = ThingCategory.Building } };
        var repair = new Verse.AI.Job { def = new Verse.AI.JobDef { defName = "FixBrokenDownBuilding" },
            targetA = building, targetB = material };
        var rules = new List<ApparelRule> { source, destination, source };
        Check(RuleEvaluator.JobPreparationTargetsArea(repair, destination.Area) &&
              !RuleEvaluator.JobPreparationTargetsArea(repair, source.Area),
            "repair without a work giver classifies the building as its worksite, not its component");
        Check(RepairMaterialStage.SourceRules(pawn, repair, rules).SequenceEqual(new[] { source }),
            "repair discovers and deduplicates its material source before a boundary collision");
        repair.loadID = 42;
        var staged = new PawnApparelState { Pawn = pawn, ActiveRuleId = source.Id,
            Transition = ApparelTransition.Preparing, PendingWorkJob = repair,
            PendingBoundaryRuleIds = new List<string> { source.Id }, PendingBoundaryWorkJobLoadId = 42 };
        Check(RepairMaterialStage.StatusSources(pawn, repair, staged, destination, rules).SequenceEqual(new[] { source }),
            "destination UI recognizes exact saved source preparation without claiming destination readiness");
        staged.PendingWorkJob = null; staged.Transition = ApparelTransition.Active;
        material.Spawned = false;
        Check(RepairMaterialStage.StatusSources(pawn, repair, staged, destination, rules).Count == 1,
            "activated repair stays tracked while its component is carried");
        staged.PendingBoundaryWorkJobLoadId = 41;
        Check(RepairMaterialStage.StatusSources(pawn, repair, staged, destination, rules).Count == 0,
            "unrelated current repair cannot borrow a stale source stage");
        staged.PendingBoundaryWorkJobLoadId = 42; pawn.Position = new IntVec3(20);
        Check(RepairMaterialStage.StatusSources(pawn, repair, staged, destination, rules).Count == 0,
            "source stage never hides a missing-outfit warning inside the destination");
        pawn.Position = new IntVec3(0); staged.RecallRequested = true;
        Check(RepairMaterialStage.StatusSources(pawn, repair, staged, destination, rules).Count == 0,
            "recalled repair does not advertise active component collection");
        staged.RecallRequested = false; staged.Transition = ApparelTransition.Restoring;
        Check(RepairMaterialStage.StatusSources(pawn, repair, staged, destination, rules).Count == 0,
            "restoring session does not advertise active component collection");
        staged.Transition = ApparelTransition.Active; staged.PendingBoundaryRuleIds.Clear();
        Check(RepairMaterialStage.StatusSources(pawn, repair, staged, destination, rules).Count == 0,
            "another tracked outfit alone cannot suppress Untracked");
        staged.PendingBoundaryRuleIds.Add(source.Id); destination.Enabled = false;
        Check(RepairMaterialStage.StatusSources(pawn, repair, staged, destination, rules).Count == 0,
            "disabled destination does not advertise staged preparation");
        destination.Enabled = true; material.Spawned = true;
        staged.CurrentRuleIds.Add(destination.Id);
        Check(RepairMaterialStage.StatusSources(pawn, repair, staged, destination, rules).Count == 0,
            "destination-owned session uses its normal worker row without a duplicate staged row");
        source.WorkAreaPaused = true;
        Check(RepairMaterialStage.SourceRules(pawn, repair, rules).Count == 0, "paused repair source is not staged");
        source.WorkAreaPaused = false;
        source.Enabled = false;
        Check(RepairMaterialStage.SourceRules(pawn, repair, rules).Count == 0, "disabled repair source is not staged");
        source.Enabled = true;
        source.Kind = AreaRuleKind.NonWork;
        Check(RepairMaterialStage.SourceRules(pawn, repair, rules).Count == 0, "non-work material location is not a work source stage");
        source.Kind = AreaRuleKind.Work;
        pawn.Drafted = true;
        Check(RepairMaterialStage.SourceRules(pawn, repair, rules).Count == 0, "drafted pawn is not given an automatic material stage");
        pawn.Drafted = false;
        repair.targetQueueB = new List<LocalTargetInfo> { material };
        Check(RepairMaterialStage.SourceRules(pawn, repair, rules).Count == 0, "multi-target repair is outside the single-component handoff");
        repair.targetQueueB = null;
        repair.targetC = material;
        Check(RepairMaterialStage.SourceRules(pawn, repair, rules).Count == 0, "third-target repair is not rewritten as a single-component handoff");
        repair.targetC = default(LocalTargetInfo);
        material.Spawned = false;
        Check(RepairMaterialStage.SourceRules(pawn, repair, rules).Count == 0, "already carried material does not start another source stage");
        material.Spawned = true;
        material.def.IsWeapon = true;
        Check(RepairMaterialStage.SourceRules(pawn, repair, rules).Count == 0, "weapon targets are not staged as repair material");
        material.def.IsWeapon = false;
        repair.def.defName = "DoBill";
        Check(RepairMaterialStage.SourceRules(pawn, repair, rules).Count == 0, "bill ingredients do not opt into repair staging");

        var component = new AutomaticOutfitManagerGameComponent { Rules = new List<ApparelRule> { source, destination } };
        AutomaticOutfitManagerGameComponent.Current = component;
        RuleEvaluator.ResetRuntimeCache();
        var saved = new Apparel { Map = map, Spawned = true, PositionHeld = new IntVec3(10),
            def = new ThingDef { apparel = new ApparelProperties() } };
        var other = new Apparel { Map = map, Spawned = true, PositionHeld = new IntVec3(10), def = saved.def };
        component.State = new PawnApparelState { Pawn = pawn, ActiveRuleId = destination.Id,
            OriginalApparel = new List<Apparel> { saved }, RestorationSourceRuleIds = new List<string> { source.Id } };
        Check(!GearRetrievalRoute.RestrictedRules(pawn, saved).Contains(source),
            "exact saved garment can be retrieved from its remembered source after a rule handoff");
        Check(GearRetrievalRoute.RestrictedRules(pawn, other).Contains(source),
            "a different garment of the same definition does not inherit personal retrieval access");
        saved.PositionHeld = new IntVec3(30);
        Check(GearRetrievalRoute.RestrictedRules(pawn, saved).Contains(source),
            "remembered source cannot be used as a shortcut to an exact item elsewhere");
        saved.PositionHeld = new IntVec3(10);
        component.State.OriginalApparel.Clear();
        Check(GearRetrievalRoute.RestrictedRules(pawn, saved).Contains(source),
            "snapshot cleanup revokes access to an item that is no longer a personal target");
        Check(!GearRetrievalRoute.RestrictedRules(pawn, other, new[] { source }).Contains(source),
            "planned work gear may be fetched from its own target-owning rule");
        other.PositionHeld = new IntVec3(30);
        Check(GearRetrievalRoute.RestrictedRules(pawn, other, new[] { source }).Contains(source),
            "planned rule access also requires the rule to own the actual target");
        pawn.Position = new IntVec3(10);
        Check(!GearRetrievalRoute.RestrictedRules(pawn, other).Contains(source),
            "a pawn already inside a protected area retains egress");
        pawn.Position = new IntVec3(0);
        AutomaticOutfitManager.Patches.ProtectedPathAvoidance.RouteAvailable = false;
        Check(!GearRetrievalRoute.CanReach(pawn, other, new[] { destination }),
            "gear candidate is rejected when no route avoids the restricted areas");
        AutomaticOutfitManager.Patches.ProtectedPathAvoidance.RouteAvailable = true;
        Check(GearRetrievalRoute.CanReach(pawn, other, new[] { destination }),
            "gear candidate remains available when a route around foreign areas exists");
        other.Map = new Map();
        Check(!GearRetrievalRoute.CanReach(pawn, other), "gear retrieval rejects items on another map");
    }

    private static void CheckWorkSnapshotCleanup()
    {
        var map = new Map();
        var pawn = new Pawn { Map = map };
        var vestDef = new ThingDef { apparel = new ApparelProperties() };
        var shirt = new Apparel { def = new ThingDef { apparel = new ApparelProperties() } };
        var vest = new Apparel { def = vestDef, HitPoints = 69, HasQuality = true,
            Quality = QualityCategory.Legendary };
        var gunDef = new ThingDef { IsWeapon = true, IsRangedWeapon = true };
        var gun = new ThingWithComps { def = gunDef };
        pawn.apparel.WornApparel.AddRange(new[] { shirt, vest });
        pawn.equipment.Primary = gun;
        var work = new ApparelRule { Area = new Area { Map = map } };
        work.RequiredApparel.Add(vestDef);
        work.RequiredWeapons.Add(gunDef);
        var rules = new List<ApparelRule> { work };
        var saved = SavedNonWorkOutfit.Capture(pawn);
        Check(WorkGearSnapshotPolicy.Clean(saved, rules).Count == 2 &&
              saved.Apparel.SequenceEqual(new[] { shirt }) && saved.Weapon == null,
            "snapshot cleanup releases the matching formal vest and exact primary but keeps unrelated clothes");
        Check(pawn.apparel.WornApparel.Contains(vest) && pawn.equipment.Primary == gun,
            "classification alone never removes worn apparel or an equipped weapon");
        Check(saved.WorkGear.Count == 2 && saved.WorkGear.All(entry => entry.RuleIds.SequenceEqual(new[] { work.Id })),
            "released snapshot items retain their exact identities and matching source rule");
        Check(WorkGearSnapshotPolicy.Clean(saved, rules).Count == 0 && saved.WorkGear.Count == 2,
            "repeated cleanup makes no changes and cannot grow work history");
        work.Enabled = false;
        Check(WorkGearSnapshotPolicy.MatchingRules(pawn, vest, rules).Count == 0,
            "disabled work rules leave personal snapshots eligible");
        work.Enabled = true;
        work.WorkAreaPaused = true;
        Check(WorkGearSnapshotPolicy.MatchingRules(pawn, vest, rules).Count == 1,
            "pausing work keeps its selected gear designated as work stock");
        work.Kind = AreaRuleKind.NonWork;
        Check(WorkGearSnapshotPolicy.MatchingRules(pawn, vest, rules).Count == 0,
            "non-work fallback requirements never classify personal gear as work stock");
        work.Kind = AreaRuleKind.Work;
        work.Area.Map = new Map();
        Check(WorkGearSnapshotPolicy.MatchingRules(pawn, vest, rules).Count == 0,
            "a rule on another map cannot claim the pawn's personal vest");
        work.Area = null;
        Check(WorkGearSnapshotPolicy.MatchingRules(pawn, vest, rules).Count == 0,
            "an unconfigured Work rule without an area cannot claim personal gear");
        work.Area = new Area { Map = map };
        work.AllowedApparelHitPoints = new FloatRange(.7f, 1f);
        Check(WorkGearSnapshotPolicy.MatchingRules(pawn, vest, rules).Count == 0,
            "a 69 percent vest remains personal when Work requires at least 70 percent");
        work.AllowedApparelHitPoints = new FloatRange(.69f, 1f);
        work.AllowedApparelQuality = new QualityRange { min = QualityCategory.Good, max = QualityCategory.Excellent };
        Check(WorkGearSnapshotPolicy.MatchingRules(pawn, vest, rules).Count == 0,
            "legendary personal apparel remains personal when outside the Work quality range");
        work.AllowedApparelQuality = QualityRange.All;
        Check(WorkGearSnapshotPolicy.MatchingRules(pawn, vest, rules).Count == 1,
            "the same exact vest becomes work stock when both standards match");
        gun.HitPoints = 49;
        work.AllowedWeaponHitPoints = new FloatRange(.5f, 1f);
        Check(WorkGearSnapshotPolicy.MatchingRules(pawn, gun, rules).Count == 0,
            "work weapon condition limits also apply to snapshot cleanup");
        gun.HitPoints = 100;
        gun.HasQuality = true;
        gun.Quality = QualityCategory.Poor;
        work.AllowedWeaponQuality = new QualityRange { min = QualityCategory.Normal, max = QualityCategory.Legendary };
        Check(WorkGearSnapshotPolicy.MatchingRules(pawn, gun, rules).Count == 0,
            "work weapon quality limits also apply to snapshot cleanup");
        gun.Quality = QualityCategory.Normal;
        work.RequiredWeapons.Clear();
        Check(WorkGearSnapshotPolicy.MatchingRules(pawn, gun, rules).Count == 0,
            "no selected weapon requirement never claims every personal weapon");
        work.RequiredWeapon = WeaponRequirement.Ranged;
        Check(WorkGearSnapshotPolicy.MatchingRules(pawn, gun, rules).Count == 1,
            "legacy ranged Work requirements clean a matching primary");
        work.RequiredWeapons.Add(new ThingDef { IsWeapon = true, IsRangedWeapon = true });
        Check(WorkGearSnapshotPolicy.MatchingRules(pawn, gun, rules).Count == 0,
            "exact alternatives take priority over a legacy ranged category during cleanup");
        work.RequiredWeapons.Clear();
        var shared = new ApparelRule { Area = work.Area };
        shared.RequiredApparel.Add(vestDef);
        rules.Add(shared);
        saved = SavedNonWorkOutfit.Capture(pawn);
        WorkGearSnapshotPolicy.Clean(saved, rules);
        var destination = new ApparelRule { Kind = AreaRuleKind.NonWork, Area = work.Area,
            RemoveAllWorkOutfits = false };
        destination.WorkOutfitsToRemove.Add(work.Id);
        var component = new AutomaticOutfitManagerGameComponent { Rules = rules };
        AutomaticOutfitManagerGameComponent.Current = component;
        component.Saved[pawn] = saved;
        Check(!NonWorkOutfitPolicy.ShouldReturn(pawn, destination, vest) &&
              NonWorkOutfitPolicy.Target(pawn, destination).Apparel.Contains(vest),
            "cleaned shared vest stays worn when one matching Work source is unselected");
        destination.WorkOutfitsToRemove.Add(shared.Id);
        Check(NonWorkOutfitPolicy.ShouldReturn(pawn, destination, vest) &&
              !NonWorkOutfitPolicy.Target(pawn, destination).Apparel.Contains(vest),
            "selecting every source returns the vest without restoring it as personal");
        var state = new PawnApparelState { Pawn = pawn, ActiveRuleId = work.Id,
            ApparelInterventionActive = true, OriginalApparel = new List<Apparel> { shirt, vest },
            ReusedOriginalApparel = new List<Apparel> { vest },
            WeaponInterventionActive = true, OriginalWeapon = gun };
        var restoreVest = new Verse.AI.Job { def = JobDefOf.Wear, playerForced = true,
            targetA = new LocalTargetInfo { Thing = vest } };
        var restoreShirt = new Verse.AI.Job { def = JobDefOf.Wear, playerForced = true,
            targetA = new LocalTargetInfo { Thing = shirt } };
        Check(WorkGearSnapshotPolicy.IsSnapshotRestoreJob(state, restoreVest),
            "AOM's forced exact-snapshot Wear is recognized before cleanup");
        var explicitEquip = new Verse.AI.Job { def = JobDefOf.Equip, playerForced = true,
            targetA = new LocalTargetInfo { Thing = gun } };
        state.WeaponRestorationRequested = true;
        Check(!WorkGearSnapshotPolicy.IsSnapshotRestoreJob(state, explicitEquip),
            "explicit Equip is not mistaken for AOM's non-forced primary restoration");
        var removed = WorkGearSnapshotPolicy.CleanPersonalState(state, rules, saved,
            item => item == vest || item == gun);
        Check(removed.Count == 2 && state.OriginalApparel.SequenceEqual(new[] { shirt }) &&
              !state.ReusedOriginalApparel.Contains(vest) && state.ManagedApparel.Contains(vest) &&
              state.OriginalWeapon == null && state.ManagedWeapons.Contains(gun),
            "legacy active snapshots migrate held work gear to safe-return ownership without a restore/remove overlap");
        Check(WorkGearSnapshotPolicy.ObsoleteSnapshotRestore(state, restoreVest, removed) &&
              !WorkGearSnapshotPolicy.ObsoleteSnapshotRestore(state, restoreShirt, removed),
            "cleanup retires the old forced vest restore and preserves the unrelated shirt restore");
        state.OriginalApparel.Add(vest);
        Check(!WorkGearSnapshotPolicy.ObsoleteSnapshotRestore(state, restoreVest, removed),
            "a rebased partial target that deliberately keeps the vest preserves its queued Wear");
        state.OriginalApparel.Remove(vest);
        var unrelated = new Verse.AI.Job { playerForced = true, targetA = new LocalTargetInfo { Thing = vest } };
        Check(!WorkGearSnapshotPolicy.IsSnapshotRestoreJob(state, unrelated) &&
              !WorkGearSnapshotPolicy.ObsoleteSnapshotRestore(state, unrelated, removed),
            "cleanup never treats a different native action on the same exact item as a restore");
        Check(WorkGearSnapshotPolicy.CleanPersonalState(state, rules, saved, item => true).Count == 0,
            "active state migration is idempotent as well as stored-snapshot cleanup");
        var compliant = new PawnApparelState { Pawn = pawn, ActiveRuleId = work.Id };
        WorkGearSnapshotPolicy.CleanPersonalState(compliant, rules, saved, item => true);
        Check(compliant.ApparelInterventionActive && compliant.WeaponInterventionActive &&
              compliant.OriginalApparel.SequenceEqual(new[] { shirt }) && compliant.OriginalWeapon == null &&
              compliant.ManagedApparel.Contains(vest) && compliant.ManagedWeapons.Contains(gun),
            "legacy already-compliant workers get a safe return ledger for worn Work gear without losing personal clothes");
        state.OriginalApparel.Add(vest);
        state.ReusedOriginalApparel.Add(vest);
        state.ManagedApparel.Clear();
        WorkGearSnapshotPolicy.CleanPersonalState(state, rules, saved, item => false);
        Check(!state.OriginalApparel.Contains(vest) && !state.ReusedOriginalApparel.Contains(vest) &&
              state.ManagedApparel.Contains(vest),
            "a previously personal vest already assigned to preparation stays authorized before its Wear job begins");
        state.OriginalApparel.Add(vest);
        state.OriginalWeapon = gun;
        state.ManagedApparel.Clear();
        state.ManagedWeapons.Clear();
        WorkGearSnapshotPolicy.CleanPersonalState(state, rules, saved, item => false);
        Check(!state.OriginalApparel.Contains(vest) && state.OriginalWeapon == null &&
              state.ManagedApparel.Count == 0 && state.ManagedWeapons.Count == 0,
            "loose saved stock is released without assigning a new retrieval or removal to the former owner");
        state.OriginalWeapon = gun;
        state.WeaponRuleOverrideExplicit = true;
        WorkGearSnapshotPolicy.CleanPersonalState(state, rules, saved, item => true);
        Check(state.OriginalWeapon == null && state.WeaponRuleOverrideExplicit &&
              !state.ManagedWeapons.Contains(gun),
            "cleanup removes a stale saved primary without commandeering an explicit player weapon choice");
        state.OriginalApparel.Add(vest);
        state.NonWorkRestorationRuleId = destination.Id;
        Check(WorkGearSnapshotPolicy.CleanPersonalState(state, rules, saved, item => true).Count == 0 &&
              state.OriginalApparel.Contains(vest),
            "partial Non-Work restoration targets retain their deliberately kept work garment");
        state.NonWorkRestorationRuleId = null;
        state.NonWorkFallbackActive = true;
        Check(WorkGearSnapshotPolicy.CleanPersonalState(state, rules, saved, item => true).Count == 0,
            "temporary non-work fallback baselines are not mistaken for personal snapshots");
        state.NonWorkFallbackActive = false;
        state.ActiveRuleId = destination.Id;
        rules.Add(destination);
        Check(WorkGearSnapshotPolicy.CleanPersonalState(state, rules, saved, item => true).Count == 0,
            "active non-work transitions also preserve temporary retained-gear targets");
        work.RequiredApparel.Clear();
        shared.Enabled = false;
        Check(WorkGearSnapshotPolicy.MatchingRules(pawn, vest, rules).Count == 0,
            "retained catalogue and historical source entries alone do not classify newly captured personal items");
    }
}

namespace UnityEngine { public static class Time { public static int frameCount; } }
namespace Verse
{
    public interface IExposable { void ExposeData(); }
    public static class Scribe
    {
        public static bool Loading;
        public static Dictionary<string, object> LoadedValues = new Dictionary<string, object>();
    }
    public enum LookMode { Reference, Def, Value, Deep }
    public static class Scribe_Values
    {
        public static void Look<T>(ref T value, string key, T defaultValue = default(T))
        { if (Scribe.Loading) value = Scribe.LoadedValues.TryGetValue(key, out var saved) ? (T)saved : defaultValue; }
    }
    public static class Scribe_Deep { public static void Look<T>(ref T value, string key) {} }
    public static class Scribe_References { public static void Look<T>(ref T value, string key) {} }
    public static class Scribe_Collections { public static void Look<T>(ref List<T> value, string key, LookMode mode)
        { if (Scribe.Loading && Scribe.LoadedValues.TryGetValue(key, out var data)) value = new List<T>((List<T>)data); } }
    public struct FloatRange
    {
        public float min, max;
        public FloatRange(float min, float max) { this.min = min; this.max = max; }
        public static FloatRange ZeroToOne => new FloatRange(0, 1);
        public bool IncludesEpsilon(float n) => n >= min && n <= max;
    }
    public struct IntVec3 { public static IntVec3 Invalid => new IntVec3(-1); public int Value; public IntVec3(int value) { Value = value; } public bool IsValid => Value >= 0; public bool InBounds(Map map) => map != null; }
    public class Map {}
    public class Area { public Map Map; public HashSet<int> Cells = new HashSet<int>(); public bool this[IntVec3 cell] => Cells.Contains(cell.Value); }
    public enum ThingCategory { Item, Building, Pawn }
    public class ThingDef
    {
        public ThingCategory category; public bool IsWeapon, IsMeleeWeapon, IsRangedWeapon;
        public ApparelProperties apparel;
        public string LabelCap = "test", defName = "test";
        public bool HasQuality; public bool HasComp(Type type) => HasQuality;
    }
    public class ApparelProperties { public int developmentalStageFilter = 1; }
    public class Thing
    {
        public ThingDef def;
        public Map Map; private Map heldMap; public Map MapHeld { get => heldMap ?? Map; set => heldMap = value; }
        public int stackCount = 1;
        public IntVec3 PositionHeld;
        public bool Spawned, Destroyed;
    }
    public class ThingWithComps : Thing
    {
        public int HitPoints = 100, MaxHitPoints = 100;
        public bool HasQuality; public QualityCategory Quality;
        public bool TryGetQuality(out QualityCategory quality) { quality = Quality; return HasQuality; }
    }
    public class Pawn
    {
        public Map Map;
        public IntVec3 Position;
        public bool Drafted, Downed;
        public int DevelopmentalStage = 1;
        public string LabelShortCap = "pawn";
        public RaceProperties RaceProps = new RaceProperties();
        public ApparelTracker apparel = new ApparelTracker();
        public EquipmentTracker equipment = new EquipmentTracker();
    }
    public class RaceProperties { public object body; public bool Humanlike = true; }
    public class ApparelTracker { public List<Apparel> WornApparel = new List<Apparel>(); }
    public class EquipmentTracker { public ThingWithComps Primary; }
    public struct LocalTargetInfo
    {
        public Thing Thing;
        public IntVec3 Cell;
        public bool HasThing => Thing != null;
        public bool IsValid => HasThing || Cell.Value != 0;
        public static implicit operator LocalTargetInfo(Thing thing) => new LocalTargetInfo { Thing = thing };
        public static implicit operator LocalTargetInfo(IntVec3 cell) => new LocalTargetInfo { Cell = cell };
    }
    public static class DefDatabase<T> { public static List<T> AllDefsListForReading = new List<T>(); }
    public static class GenAdj { public static IEnumerable<IntVec3> CellsOccupiedBy(Thing thing) => new IntVec3[0]; }
}
namespace Verse.AI
{
    public enum PathEndMode { OnCell, Touch, ClosestTouch }
    public class JobDef { public string defName = "test"; }
    public class Job
    {
        public int loadID;
        public bool playerForced;
        public object workGiverDef;
        public JobDef def = new JobDef();
        public LocalTargetInfo targetA, targetB, targetC;
        public List<LocalTargetInfo> targetQueueA, targetQueueB;
    }
}
namespace RimWorld
{
    public static class JobDefOf
    {
        public static Verse.AI.JobDef Wear = new Verse.AI.JobDef();
        public static Verse.AI.JobDef Equip = new Verse.AI.JobDef();
    }
    public class Apparel : ThingWithComps {}
    public enum QualityCategory { Awful, Poor, Normal, Good, Excellent, Masterwork, Legendary }
    public struct QualityRange
    {
        public QualityCategory min, max;
        public static QualityRange All => new QualityRange { max = QualityCategory.Legendary };
        public bool Includes(QualityCategory q) => q >= min && q <= max;
    }
    public class CompQuality {}
    public static class ApparelUtility { public static bool HasPartsToWear(Pawn pawn, ThingDef def) => true;
        public static bool CanWearTogether(ThingDef a, ThingDef b, object body) => a != b; }
}
namespace AutomaticOutfitManager.State
{
    public enum ApparelTransition { Preparing, Active, ReturningToChangingArea, Restoring }
    public class PawnApparelState
    {
        public ApparelTransition Transition;
        public bool RecallRequested;
        public Verse.AI.Job PendingWorkJob;
        public List<string> PendingBoundaryRuleIds = new List<string>();
        public int PendingBoundaryWorkJobLoadId = -1;
        public Pawn Pawn;
        public string ActiveRuleId;
        public List<string> CurrentRuleIds = new List<string>();
        public List<string> RestorationSourceRuleIds = new List<string>();
        public bool NonWorkFallbackActive;
        public string NonWorkRestorationRuleId;
        public bool ApparelInterventionActive, WeaponInterventionActive, WeaponRuleOverrideExplicit;
        public bool WeaponRestorationRequested;
        public List<Apparel> OriginalApparel;
        public List<Apparel> ReusedOriginalApparel = new List<Apparel>();
        public ThingWithComps OriginalWeapon;
        public List<Apparel> ManagedApparel = new List<Apparel>();
        public List<ThingWithComps> ManagedWeapons = new List<ThingWithComps>();
    }
}
namespace AutomaticOutfitManager.Core
{
    public class AutomaticOutfitManagerGameComponent
    {
        public static AutomaticOutfitManagerGameComponent Current;
        public List<ApparelRule> Rules;
        public Dictionary<Pawn, SavedNonWorkOutfit> Saved = new Dictionary<Pawn, SavedNonWorkOutfit>();
        public PawnApparelState State;
        public SavedNonWorkOutfit NonWorkOutfitFor(Pawn pawn) => pawn != null && Saved.TryGetValue(pawn, out var s) ? s : null;
        public PawnApparelState StateFor(Pawn pawn) => State;
        public ApparelRule RuleById(string id) => Rules?.FirstOrDefault(rule => rule.Id == id);
    }
    public static class AomLog
    {
        public static bool ShouldLogDetailed(Pawn pawn, string key, int ticks = 0) => false;
        public static void Detailed(string message) {}
    }
}
namespace AutomaticOutfitManager.Detection
{
    public static class PawnAccessClassifier { public static bool IsApparelEligibleHuman(Pawn pawn) => true; }
}
namespace AutomaticOutfitManager.Patches
{
    public static class ProtectedPathAvoidance
    {
        public static bool RouteAvailable = true;
        public static List<ApparelRule> LastRestricted;
        public static Verse.AI.PathEndMode? LastEndMode;
        public static bool SegmentAvoidsRules(Pawn pawn, IntVec3 start, LocalTargetInfo target, List<ApparelRule> rules,
            Predicate<IntVec3> unsafeCell = null, Verse.AI.PathEndMode? exactEndMode = null)
        { LastRestricted = rules; LastEndMode = exactEndMode; return RouteAvailable; }
    }
    public static class PausedAreaWorkFilter { public static bool UsesManagedWorkPreparation(Verse.AI.Job job) => true; }
}
