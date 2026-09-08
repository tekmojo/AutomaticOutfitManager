param([switch]$PreviousDecision, [switch]$PreviousHaulRecoveryDecision, [switch]$PreviousPauseControlDecision)
$ErrorActionPreference='Stop'
$rcRoot=Split-Path $PSScriptRoot -Parent
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir=& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler=Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$testDir=Join-Path $env:TEMP ('aom-paused-haul-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
function Extract-Method([string]$source,[string]$signature){
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $body=$source.IndexOf('{',$start);$end=$body+1;$depth=1
 while($depth -gt 0){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 $source.Substring($start,$end-$start)
}
try{
 $harmony=Join-Path $testDir '0Harmony.dll'
 Copy-Item -LiteralPath 'F:/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll' -Destination $harmony
 $filter=Get-Content (Join-Path $rcRoot 'Source/Patches/WorkGiverPausedArea_Patches.cs') -Raw
 $methods=''
 foreach($method in @('public static ApparelRule DeniedActivityRule(','public static ApparelRule DeniedPausedAreaRule(','public static bool MatchesPermittedHaulingRule(','public static bool HasPermittedHaulingContext(','internal static bool IsPermittedHaulingContinuation(','internal static Job FinalizerForPreparedHaul(','internal static bool ShouldRecallForPausedRule(','public static bool JobMayEnterPausedRule(','public static bool MatchesProtectedTransitRule(')){
  $methods+=Extract-Method $filter $method
 }
 $methods+=[regex]::Match($filter,'internal static bool ActivityRestrictedFor\([\s\S]*?;').Value
 $methods+=Extract-Method $filter 'public static bool IsEssentialPersonalJob('
 $ui=Get-Content (Join-Path $rcRoot 'Source/UI/MainRulesWindow.cs') -Raw
 $toggle=Extract-Method $ui 'private static void ToggleWorkPause('
 $tracks=Extract-Method $ui 'private static bool TracksRule('
 $core=Get-Content (Join-Path $rcRoot 'Source/Core/AutomaticOutfitManagerGameComponent.cs') -Raw
 $coreMethods=''
 foreach($method in @('public void RequestRulePauseRecall(','private static void RequestRecallCore(','public bool TryCancelRulePauseRecall(','private void ProcessPendingRecallInterrupts(')){$coreMethods+=Extract-Method $core $method}
 foreach($method in @('internal static bool IsConstructionMaterialDelivery(','internal static bool IsHaulingOnlyJob(','internal static bool SupplyTargetOnMap(')){$methods+=Extract-Method $filter $method}
 $methods+=Extract-Method $filter 'public static bool ActivityAllowedAtRuleBoundary('
 $methods+=Extract-Method $filter 'internal static bool SupplyDestinationOutsideRule('
 $methods+=Extract-Method $filter 'internal static bool SupplyOriginalTargetAllowed('
 foreach($method in @('private static bool ShouldReject(Pawn pawn, Thing thing, IntVec3 cellArgument)','internal static bool ShouldRejectPausedScannerTarget(','private static bool ShouldRejectScannerTarget(','private static bool ScannerUsesHaulingAccess(')){$methods+=Extract-Method $filter $method}
 $methods+=[regex]::Match($filter,'private static bool ContainsIgnoreCase\([\s\S]*?;').Value
 $methods+='public static bool ShouldRejectPausedScannerTarget(WorkGiver_Scanner s,Pawn p,Thing t)=>ShouldRejectPausedScannerTarget(s,p,t,IntVec3.Invalid);public static bool ShouldRejectScannerTarget(WorkGiver_Scanner s,Pawn p,Thing t)=>ShouldRejectScannerTarget(s,p,t,IntVec3.Invalid);'
 $hasHook=Extract-Method ($filter.Substring($filter.IndexOf('internal static class WorkGiverPausedArea_HasJobThing_Patch'))) 'private static void Postfix('
 $jobHook=Extract-Method ($filter.Substring($filter.IndexOf('internal static class WorkGiverPausedArea_JobOnThing_Patch'))) 'private static void Postfix('
 $scannerHooks='[HarmonyLib.HarmonyPatch(typeof(WorkGiver_Scanner),"HasJobOnThing")]static class NativeSupplyHasHook{'+$hasHook+'}[HarmonyLib.HarmonyPatch(typeof(WorkGiver_Scanner),"JobOnThing")]static class NativeSupplyJobHook{'+$jobHook+'}'
 foreach($method in @('public static ApparelRule MatchingPermittedHaulingRule(','internal static bool HaulRequiresArea(','internal static bool IsMaterialDeliveryJob(','internal static bool IsPermittedMaterialCollection(','internal static bool IsPermittedHaulForRule(','internal static bool HasPermittedPendingHaul(','internal static void NotifyPermittedHaulEnded(')){
  $methods+=Extract-Method $filter $method
 }
 $restPolicy=Get-Content (Join-Path $rcRoot 'Source/Patches/RestActivityPolicy.cs') -Raw
 $needsPolicy=Get-Content (Join-Path $rcRoot 'Source/Patches/RestorationNeeds.cs') -Raw
 $startJob=Get-Content (Join-Path $rcRoot 'Source/Patches/PawnJobTracker_StartJob_Patch.cs') -Raw
 $exitMethod=Extract-Method $startJob 'internal static bool IsUnavailableGearEgressJob('
 $startJob=$startJob.Replace("`r`n","`n")
 $policyMethods=(Extract-Method $startJob 'internal static bool ShouldResumePreparedJob(')+(Extract-Method $startJob 'private static bool HasCompletedPreparation(')+(Extract-Method $startJob "internal static bool PendingWorkJobIsViable(`n            Pawn pawn, Job job, out string reason,")
 $classifier=Extract-Method (Get-Content (Join-Path $rcRoot 'Source/Detection/ActivityJobClassifier.cs') -Raw) 'public static bool IsHauling('
 $footprint=[regex]::Match((Get-Content (Join-Path $rcRoot 'Source/Detection/GearRetrievalRoute.cs') -Raw),'internal static bool OwnsTarget\([\s\S]*?;').Value
 $policyMethods+=Extract-Method $startJob 'internal static void CompletePreparedJobAdmission('
 $policyMethods+=Extract-Method $startJob 'internal static bool IsBufferableJob('
 $policyMethods+=Extract-Method $startJob 'internal static bool IsNativeMentalActivity('
 $policyMethods+=Extract-Method $startJob 'private static bool TryCancelAutomaticIdleReturnForProtectedJob('
 $pathSource=Get-Content (Join-Path $rcRoot 'Source/Patches/ProtectedPathAvoidance.cs') -Raw
 $pathMethods=(Extract-Method $pathSource 'public static bool RouteRequiresRestrictedArea(')+(Extract-Method $pathSource 'internal static bool RouteRequiresRestrictedArea(')+(Extract-Method $pathSource 'internal static bool PathAvoidsRules(')+(Extract-Method $pathSource 'internal static IReadOnlyList<ApparelRule> RestrictedTransitRules(')
 foreach($case in $(if($PreviousDecision){@('route','wait','egress','shortcut','active','viability','resume','grace','material','supply','supplyuse','scanner','scanneraccess','footprint','originaltarget','restentry','restwait','restresume','blockedneed')}elseif($PreviousHaulRecoveryDecision){@('idlehaul','initialexit','finalizer')}elseif($PreviousPauseControlDecision){@('pausebutton','explicitrecall','nestedrest','nursingactivity','nursingpause','nursingpath')}else{@('current')})){
  $body=$methods
  $policy=$policyMethods
  $paths=$pathMethods
  if($case -eq 'idlehaul'){
   $idle=Extract-Method $startJob 'private static bool TryCancelAutomaticIdleReturnForProtectedJob('
   $old=$idle.Replace('.Where(rule => rule?.Enabled == true && rule.Area?.Map == pawn.Map)', '.Where(rule => rule?.Enabled == true && (!rule.WorkAreaPaused || RestActivityPolicy.Allowed(pawn, newJob, rule)) && rule.Area?.Map == pawn.Map)')
   $policy=$policy.Replace($idle,$old)
  }
  if($case -eq 'initialexit'){
   $paths=$paths.Replace((Extract-Method $pathSource 'internal static bool PathAvoidsRules('),'internal static bool PathAvoidsRules(IReadOnlyList<IntVec3> nodes,IntVec3 start,Map map,List<ApparelRule> rules,Predicate<IntVec3> unsafeCell,bool egress)=>!nodes.Any(cell=>cell.IsValid&&cell.InBounds(map)&&(rules.Any(rule=>rule.Area[cell])||unsafeCell?.Invoke(cell)==true));')
  }
  if($case -eq 'finalizer'){
   $body=$body.Replace((Extract-Method $filter 'internal static Job FinalizerForPreparedHaul('),'internal static Job FinalizerForPreparedHaul(Pawn p,Job parent,Job child)=>child;')
  }
  $rest=$restPolicy
  $needs=$needsPolicy
  $useToggle=$toggle
  if($case -eq 'pausebutton'){$useToggle=$useToggle -replace '&&\s*!Patches.RestActivityPolicy.Preserves\(state, rule, state.Pawn.jobs\?\.curJob\)',''}
  if($case -eq 'explicitrecall'){$useToggle=$useToggle -replace 'if \(state.RecallRequested && \(state.PauseRecallRuleIds\?\.Count \?\? 0\) == 0\)\s*continue;',''}
  if($case -eq 'nestedrest'){$rest=$rest -replace '&&\s*state.NestedRuleBuffers\?\.Any\(progress => progress\?\.RuleId == rule.Id\) != true',''}
  if($case -eq 'nursingactivity'){
   $restriction=[regex]::Match($filter,'internal static bool ActivityRestrictedFor\([\s\S]*?;').Value
   $body=$body.Replace($restriction,($restriction -replace '&&\s*!AnimalNursingPolicy.Allowed\(pawn, job, rule\)',''))
  }
  if($case -eq 'nursingpause'){
   $old=Extract-Method $filter 'public static ApparelRule DeniedPausedAreaRule('
   $body=$body.Replace($old,($old -replace '!AnimalNursingPolicy.Allowed\(pawn, job, rule\) &&',''))
  }
  if($case -eq 'nursingpath'){
   $old=Extract-Method $filter 'public static bool ActivityAllowedAtRuleBoundary('
   $body=$body.Replace($old,($old -replace '\|\|\s*AnimalNursingPolicy.Allowed\(pawn, job, rule\)',''))
  }
  Set-Content (Join-Path $testDir 'PauseControl.cs') ('using System;using System.Linq;using System.Collections.Generic;using Verse;using Verse.AI;using RimWorld;using AutomaticOutfitManager.Core;using AutomaticOutfitManager.Rules;using AutomaticOutfitManager.State;using AutomaticOutfitManager.Detection;using AutomaticOutfitManager.Patches;namespace AutomaticOutfitManager.UI{public static partial class MainRulesWindow{'+$useToggle+$tracks+'}}namespace AutomaticOutfitManager.Core{public partial class AutomaticOutfitManagerGameComponent{'+$coreMethods+'}}')
  if($case -eq 'originaltarget'){$body=$body.Replace((Extract-Method $filter 'internal static bool SupplyOriginalTargetAllowed('),'internal static bool SupplyOriginalTargetAllowed(Job j,Map m,ApparelRule r)=>!j.targetC.IsValid;')}
  if($case -eq 'restentry'){$body=$body -replace '\|\|\s*RestActivityPolicy.Allowed\(pawn, job, rule\)',''}
  if($case -eq 'restwait'){$body=$body -replace '!RestActivityPolicy.Preserves\(state, rule, job\) &&','';$body=$body -replace '&&\s*!RestActivityPolicy.Preserves\(state, rule, state.Pawn\?\.jobs\?\.curJob\)',''}
  if($case -eq 'restresume'){$policy=$policy -replace '\|\|\s*RestActivityPolicy.HasPendingRest\(pawn, state, state.PendingWorkJob\)',''}
  if($case -eq 'blockedneed'){$needs=$needs.Replace((Extract-Method $needsPolicy 'internal static bool CanDefer('),'internal static bool CanDefer(Pawn p,PawnApparelState s,Job j,int count,bool unavailable)=>false;')}
  Set-Content (Join-Path $testDir 'Rest.cs') $rest
  Set-Content (Join-Path $testDir 'Needs.cs') $needs
  if($case -eq 'route'){
   $old='public static bool HasPermittedHaulingContext(PawnApparelState state,ApparelRule rule){var pawn=state?.Pawn;if(pawn==null||rule==null||state.RecallRequested)return false;if(state.Transition==ApparelTransition.Preparing)return MatchesPermittedHaulingRule(pawn,state.PendingWorkJob,rule);return state.Transition==ApparelTransition.Active&&MatchesPermittedHaulingRule(pawn,pawn.jobs?.curJob,rule);}'
   $body=$body.Replace((Extract-Method $filter 'public static bool HasPermittedHaulingContext('),$old)
  }
  if($case -eq 'wait'){
   $body=$body -replace '!IsPermittedHaulingContinuation\(state, rule, job\) &&',''
  }
  if($case -eq 'egress'){$body=$body -replace 'if \(PawnJobTracker_StartJob_Patch.IsUnavailableGearEgressJob\(pawn, job, rule\)\)\s*return false;',''}
  if($case -eq 'shortcut'){$body=$body.Replace((Extract-Method $filter 'internal static bool HaulRequiresArea('),'internal static bool HaulRequiresArea(Pawn p,Job j,ApparelRule r)=>RuleEvaluator.JobTargetsArea(j,r.Area)||HaulingPathCrossesArea(p,j,r.Area);')}
  if($case -eq 'active'){$body=$body.Replace('(state.Transition == ApparelTransition.Preparing || state.Transition == ApparelTransition.Active)','(state.Transition == ApparelTransition.Preparing)')}
  if($case -eq 'viability'){$policy=$policy.Replace('permittedPendingHaul ||','')}
  if($case -eq 'resume'){$policy=$policy.Replace((Extract-Method $startJob 'internal static bool ShouldResumePreparedJob('),'internal static bool ShouldResumePreparedJob(Pawn p,AutomaticOutfitManagerGameComponent c,PawnApparelState s,Job j)=>s?.Transition==ApparelTransition.Preparing&&s.PendingWorkJob!=null&&!SameJob(j,s.PendingWorkJob)&&HasCompletedPreparation(p,c,s);')}
  if($case -eq 'grace'){$body=$body.Replace((Extract-Method $filter 'internal static void NotifyPermittedHaulEnded('),'internal static void NotifyPermittedHaulEnded(Pawn p,PawnApparelState s,Job j,JobCondition c){}')}
  if($case -eq 'material'){$body=$body.Replace((Extract-Method $filter 'internal static bool IsMaterialDeliveryJob('),'internal static bool IsMaterialDeliveryJob(Job j)=>false;')}
  if($case -eq 'supply'){$body=$body.Replace((Extract-Method $filter 'internal static bool IsMaterialDeliveryJob('),'internal static bool IsMaterialDeliveryJob(Job j)=>j?.def==JobDefOf.HaulToContainer&&(j.targetB.Thing is Frame||j.targetB.Thing is Blueprint)&&j.targetA.Thing!=null&&!j.targetA.Thing.Destroyed&&j.targetA.Thing.def?.category==ThingCategory.Item&&!j.targetA.Thing.def.IsApparel&&!j.targetA.Thing.def.IsWeapon;')}
  if($case -eq 'supplyuse'){$body=$body.Replace((Extract-Method $filter 'internal static bool IsHaulingOnlyJob('),'internal static bool IsHaulingOnlyJob(Job j)=>IsHaulingJob(j);')}
  if($case -eq 'scanner'){$body=$body.Replace((Extract-Method $filter 'internal static bool ShouldRejectPausedScannerTarget('),'internal static bool ShouldRejectPausedScannerTarget(WorkGiver_Scanner s,Pawn p,Thing t,IntVec3 c)=>ShouldReject(p,t,c);')}
  if($case -eq 'scanneraccess'){$body=$body.Replace('if (scanner is WorkGiver_Refuel) return false;','')}
  if($case -eq 'footprint'){$body=$body.Replace((Extract-Method $filter 'internal static bool SupplyDestinationOutsideRule('),'internal static bool SupplyDestinationOutsideRule(LocalTargetInfo t,ApparelRule r)=>!r.Area[t.Cell];')}
  $generated=Join-Path $testDir 'Filter.cs'
  Set-Content -LiteralPath $generated -Value ('using System;using System.Linq;using System.Collections.Generic;using Verse;using Verse.AI;using RimWorld;using AutomaticOutfitManager.Core;using AutomaticOutfitManager.Detection;using AutomaticOutfitManager.Rules;using AutomaticOutfitManager.State;namespace AutomaticOutfitManager.Detection{internal static class GearRetrievalRoute{'+$footprint+'}internal static class ActivityJobClassifier{'+$classifier+'}}namespace AutomaticOutfitManager.Patches{'+$scannerHooks+'public static partial class ProtectedPathAvoidance{'+$paths+'}public static partial class PausedAreaWorkFilter{'+$body+'}internal static partial class PawnJobTracker_StartJob_Patch{'+$exitMethod+$policy+'}}')
  $exe=Join-Path $testDir 'PausedHaulTests.exe'
  & $compiler /nologo /target:exe /main:PausedHaulTests /define:AOM_PAUSED_HAUL_TESTS /langversion:latest /warn:0 "/out:$exe" "/reference:$harmony" $generated (Join-Path $PSScriptRoot 'PausedHaulTests.cs') (Join-Path $PSScriptRoot 'PreparationHandoffTests.cs') (Join-Path $rcRoot 'Source/Patches/PreparationJobHandoff.cs') (Join-Path $testDir 'Rest.cs') (Join-Path $testDir 'Needs.cs') (Join-Path $rcRoot 'Source/Detection/RestorationPlanProgress.cs') (Join-Path $PSScriptRoot 'RestAndSupplyTests.cs') (Join-Path $PSScriptRoot 'HaulRecoveryRegressionTests.cs') (Join-Path $testDir 'PauseControl.cs') (Join-Path $PSScriptRoot 'PauseControlTests.cs') (Join-Path $rcRoot 'Source/Patches/AnimalNursingPolicy.cs') (Join-Path $rcRoot 'Source/Patches/NativeRuleControl.cs')
  if($LASTEXITCODE -ne 0){throw 'Paused haul compilation failed'}
  # Windows PowerShell represents native stderr as ErrorRecords. Expected
  # negative-control failures must reach the explicit exit/message assertions.
  $nativeErrorPreference=$ErrorActionPreference
  try { $ErrorActionPreference='Continue'; $output=& $exe 2>&1; $result=$LASTEXITCODE }
  finally { $ErrorActionPreference=$nativeErrorPreference }
  if($PreviousDecision -or $PreviousHaulRecoveryDecision -or $PreviousPauseControlDecision){
   $expected=if($case -eq 'route'){'locker route change must not recall owned haul'}elseif($case -eq 'egress'){'safe shortage exit must not prepare another outfit'}else{'native connective wait must not recall owned haul'}
   $regressions=@{shortcut='avoidable locker shortcut does not require PPE';active='active pending haul survives completion wait';viability='full viability survives locker route change';resume='active pending haul resumes after complete PPE';grace='successful haul keeps outfit during bounded native wait';material='exterior construction delivery may prepare source PPE';supply='native exterior supply transfer admitted';supplyuse='supply use is not transport'}
   if($regressions.ContainsKey($case)){$expected=$regressions[$case]}
   if($case -eq 'scanner'){$expected='native scanner accepts paused supply pickup'}
   if($case -eq 'scanneraccess'){$expected='rearm scanner uses Activities permission before job creation'}
   if($case -eq 'footprint'){$expected='recipient footprint cannot overlap paused construction'}
   if($case -eq 'originaltarget'){$expected='native original construction target permits exterior delivery'}
   if($case -eq 'restentry'){$expected='paused sleep is allowed with area permission'}
   if($case -eq 'restwait'){$expected='sleep Wear must survive pause watchdog'}
   if($case -eq 'restresume'){$expected='active sleep resumes after completed PPE'}
   if($case -eq 'blockedneed'){$expected='blocked snapshot permits safe rest'}
   if($case -eq 'idlehaul'){$expected='paused haul missing PPE cannot cancel automatic return'}
   if($case -eq 'initialexit'){$expected='locker-bound egress does not request fresh area PPE'}
   if($case -eq 'finalizer'){$expected='denied paused finalizer cannot displace prepared haul'}
   if($case -eq 'pausebutton'){$expected='pause button preserves permitted sleeper'}
   if($case -eq 'explicitrecall'){$expected='pause resume cannot relabel or cancel explicit recall'}
   if($case -eq 'nestedrest'){$expected='nested or overlapping rest keeps its paused rule'}
   if($case -eq 'nursingactivity'){$expected='nursing admitted by paused activity selection'}
   if($case -eq 'nursingpause'){$expected='nursing admitted by secondary pause selection'}
   if($case -eq 'nursingpath'){$expected='nursing admitted at paused path boundary'}
   if($result -eq 0 -or ($output -join "`n") -notmatch $expected){throw "Negative control failed: $case : $output"}
   Write-Output "Previous $case decision fails regression (negative control)."
  }else{$output;if($result -ne 0){throw 'Paused haul contracts failed'}}
 }
}finally{
 foreach($name in @('0Harmony.dll','Filter.cs','PausedHaulTests.exe','Rest.cs','Needs.cs','PauseControl.cs')){ $file=Join-Path $testDir $name;if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file} }
 Remove-Item -LiteralPath $testDir
}
exit 0
