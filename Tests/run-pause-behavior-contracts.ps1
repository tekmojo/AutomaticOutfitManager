param([switch]$PreviousDecision)
$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$stem = Join-Path $env:TEMP ('aom-pause-' + [Guid]::NewGuid().ToString('N'))
$generated = $stem + '.cs'
$mealSource = $stem + '-meal.cs'
$testOutput = $stem + '.exe'
function Extract-Method([string]$source, [string]$signature) {
    $start = $source.IndexOf($signature)
    if ($start -lt 0) { throw "Missing method: $signature" }
    $body = $source.IndexOf('{', $start)
    $depth = 1; $end = $body + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    return $source.Substring($start, $end - $start)
}
try {
    $filter = Get-Content (Join-Path $rcRoot 'Source/Patches/WorkGiverPausedArea_Patches.cs') -Raw
    $core = Get-Content (Join-Path $rcRoot 'Source/Core/AutomaticOutfitManagerGameComponent.cs') -Raw
    $startSource = Get-Content (Join-Path $rcRoot 'Source/Patches/PawnJobTracker_StartJob_Patch.cs') -Raw
    $nativeHelpers = (Extract-Method $startSource 'internal static bool IsNativeMentalActivity(') + (Extract-Method $startSource 'internal static bool IsNativeEmergencySafetyJob(')
    $denied = Extract-Method $filter 'public static ApparelRule DeniedActivityRule('
    $recall = Extract-Method $core 'private void ProcessPendingRecallInterrupts('
    $observedPolicy = (Extract-Method $filter 'internal static bool CanRecallObservedActivity(') + (Extract-Method $filter 'private static bool IsObservedRecallCareJob(')
    $observedAction = (Extract-Method $core 'internal bool CanRecallObservedActivity(') + (Extract-Method $core 'internal bool RecallObservedActivity(')
    $bufferSource = Get-Content (Join-Path $rcRoot 'Source/Patches/NonWorkBufferTracker.cs') -Raw
    $bufferRefresh = Extract-Method $bufferSource 'internal static void Refresh('
    $policy = [regex]::Match($filter, 'internal static bool ActivityRestrictedFor\([\s\S]*?;').Value
    $egress = [regex]::Match($filter, 'internal static bool IsOwnedAccessEgress\([\s\S]*?;').Value
    $exitMethods = $egress + (Extract-Method $filter 'private static bool IsRestrictedRoamingEgress(') + (Extract-Method $filter 'public static bool TryMakeAccessExitJob(') + (Extract-Method $filter 'private static bool TryFindSafeWanderingCell(') + (Extract-Method $filter 'private static bool HasRestrictedAreaClearance(')
    if (!$policy) { throw 'Missing activity restriction decision' }
    $prefix = 'using System;using System.Linq;using System.Collections.Generic;using RimWorld;using Verse;using Verse.AI;using AutomaticOutfitManager.Core;using AutomaticOutfitManager.Rules;using AutomaticOutfitManager.State;using AutomaticOutfitManager.Detection;'
    # Execute exact production decisions and the full callback-owning method.
    # Negative controls independently replay both former decisions.
    foreach ($case in $(if ($PreviousDecision) { @('activity','recall','meal','observed','reenroll') } else { @('current') })) {
        $usePolicy=$policy; $useRecall=$recall
        $useObservedPolicy = $observedPolicy
        if ($case -eq 'observed') { $useObservedPolicy = 'internal static bool CanRecallObservedActivity(Pawn pawn,Job job,ApparelRule rule)=>false;' }
        $useRefresh=$bufferRefresh
        if ($case -eq 'reenroll') { $useRefresh=$useRefresh.Replace('if (AccessExitJobs.IsOwned(pawn, job)) return;', '') }
        if ($case -eq 'activity') { $usePolicy='internal static bool ActivityRestrictedFor(ApparelRule r,Pawn p,Job j)=>!WorkAllowedFor(r,p);' }
        if ($case -eq 'recall') { $useRecall=$recall -replace 'if \(clearTrackedOnlySession && StateFor\(pawn\) == state &&\s*state.Transition != ApparelTransition.ReturningToChangingArea &&\s*state.Transition != ApparelTransition.Restoring\)', 'if (clearTrackedOnlySession)' }
        $meal = Get-Content (Join-Path $rcRoot 'Source/Patches/PausedMealDestination.cs') -Raw
        if ($case -eq 'meal') { $meal = $meal.Replace('var rules = RuleEvaluator.EnabledRulesForMap(pawn.Map);', 'if (pawn != null) return false; var rules = RuleEvaluator.EnabledRulesForMap(pawn.Map);') }
        Set-Content -LiteralPath $mealSource -Value $meal
        Set-Content -LiteralPath $generated -Value ($prefix + 'namespace AutomaticOutfitManager.Patches {public static partial class PausedAreaWorkFilter {' + $denied + $usePolicy + $exitMethods + $useObservedPolicy + '} public static partial class NonWorkBufferTracker {' + $useRefresh + '}} namespace AutomaticOutfitManager.Core {public partial class AutomaticOutfitManagerGameComponent {' + $useRecall + $observedAction + '}}')
        Add-Content -LiteralPath $generated -Value ('namespace AutomaticOutfitManager.Patches {public static partial class PawnJobTracker_StartJob_Patch {' + $nativeHelpers + '}}')
        & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$testOutput" $generated (Join-Path $PSScriptRoot 'PauseBehaviorTests.cs') $mealSource (Join-Path $rcRoot 'Source/Patches/NativeRuleControl.cs')
        if ($LASTEXITCODE -ne 0) { throw 'Pause contract compilation failed' }
        # Expected negative controls write to stderr; Windows PowerShell must
        # reach the explicit exit/message assertion instead of stopping early.
        $nativeErrorPreference = $ErrorActionPreference
        try { $ErrorActionPreference = 'Continue'; $output = & $testOutput 2>&1; $result = $LASTEXITCODE }
        finally { $ErrorActionPreference = $nativeErrorPreference }
        if ($PreviousDecision) {
            $expected = if ($case -eq 'activity') { 'paused native occupant must be denied' } elseif ($case -eq 'meal') { 'native closed chair corrected before path' } elseif ($case -eq 'observed') { 'observed child learning offers recall' } elseif ($case -eq 'reenroll') { 'exit callback must not reenroll cleared buffer' } else { 'native callback return keeps owning state' }
            if ($result -eq 0 -or ($output -join "`n") -notmatch $expected) { throw "Negative control failed: $case : $output" }
            Write-Output "Previous $case decision fails its regression (negative control)."
        } else {
            $output
            if ($result -ne 0) { throw 'Pause behavior contracts failed' }
        }
    }
} finally {
    foreach ($file in @($generated,$mealSource,$testOutput)) { if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
}
exit 0
