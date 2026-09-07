param([string]$LockerSource = '', [string]$CoreSource = '')
$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$testDir = Join-Path $env:TEMP ('aom-saved-recovery-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir | Out-Null
try {
    $harmonyCopy = Join-Path $testDir '0Harmony.dll'
    Copy-Item -LiteralPath 'F:/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll' -Destination $harmonyCopy
    $testOutput = Join-Path $testDir 'tests.exe'
    $patchText = Get-Content -LiteralPath (Join-Path $rcRoot 'Source/Patches/WorkGiverPausedArea_Patches.cs') -Raw
    $extracted = "using System; using System.Linq; using System.Reflection; using System.Collections.Generic; using HarmonyLib; using RimWorld; using Verse; using Verse.AI; using AutomaticOutfitManager.Detection; namespace AutomaticOutfitManager.Patches {`n"
    foreach ($name in @('WorkGiverPausedArea_HasJobThing_Patch','WorkGiverPausedArea_JobOnThing_Patch')) {
        $start = $patchText.IndexOf('internal static class ' + $name)
        if ($start -lt 0) { throw "Missing production scanner patch $name" }
        $brace = $patchText.IndexOf('{', $start); $depth = 1; $end = $brace + 1
        while ($depth -gt 0) { if ($patchText[$end] -eq '{') { $depth++ }; if ($patchText[$end] -eq '}') { $depth-- }; $end++ }
        $extracted += '[HarmonyPatch] ' + $patchText.Substring($start, $end - $start) + "`n"
    }
    $extracted += '}'
    $scannerPatches = Join-Path $testDir 'ScannerPatches.cs'
    Set-Content -LiteralPath $scannerPatches -Value $extracted
    # Exercise the actual idle-restoration decision block. Native world/driver
    # effects are deterministic fixture hooks, not replacements for this logic.
    if (!$CoreSource) { $CoreSource = Join-Path $rcRoot 'Source/Core/AutomaticOutfitManagerGameComponent.cs' }
    $core = Get-Content -LiteralPath $CoreSource -Raw
    $marker = $core.IndexOf('// Completion and live queued progress were checked above.')
    $start = $core.IndexOf('                    state.ActiveIdleTicks += 30;', $marker)
    $end = $core.IndexOf('                // Older RC saves can contain', $start)
    if ($marker -lt 0 -or $start -lt 0 -or $end -le $start) { throw 'Idle restoration decision block not found' }
    $body = $core.Substring($start,$end-$start).TrimEnd()
    if (!$body.EndsWith('}')) { throw 'Unexpected idle restoration block ending' }
    $body = $body.Substring(0,$body.Length-1)
    $prefix = @'
using System.Collections.Generic;using AutomaticOutfitManager.Core;using AutomaticOutfitManager.Detection;using AutomaticOutfitManager.State;using AutomaticOutfitManager.Rules;using Verse;using Verse.AI;
internal partial class CoreRecoveryHarness {
internal void Pulse(Pawn pawn,PawnApparelState state,int currentTick) {
ActiveState=state; ApparelRule rule=null; Job restorationJob=pawn.CurJob;
foreach(var pulse in new[]{0}) {
'@
    $coreFile = Join-Path $testDir 'CoreRecovery.cs'
    Set-Content -LiteralPath $coreFile -Value ($prefix + "`n" + $body + '}}}')
    if (!$LockerSource) { $LockerSource = Join-Path $rcRoot 'Source/Storage/WorkGiver_LockerRestock.cs' }
    $sources = @('Tests/SavedGearRecoveryContractTests.cs', 'Tests/LockerRecoveryTests.cs', 'Source/Detection/SavedGearRecovery.cs', 'Source/Detection/GearRetrievalRoute.cs', 'Source/Detection/RestorationPlanProgress.cs', 'Source/Detection/ManagedWorkClaimRegistry.cs', 'Source/Detection/ManagedWorkCandidateFilter.cs', 'Source/Storage/LockerHaulDestination.cs', 'Source/Patches/SavedGearRecovery_Patches.cs', 'Source/Patches/ReservationUtility_SavedApparel_Patch.cs') | ForEach-Object { Join-Path $rcRoot $_ }
    $sources += @($scannerPatches, $LockerSource, $coreFile)
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$testOutput" "/reference:$harmonyCopy" $sources
    if ($LASTEXITCODE -ne 0) { throw 'Saved gear recovery test compilation failed.' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Saved gear recovery checks failed.' }
} finally {
    foreach ($name in @('tests.exe', '0Harmony.dll', 'ScannerPatches.cs', 'CoreRecovery.cs')) {
        $file = Join-Path $testDir $name
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
    }
    Remove-Item -LiteralPath $testDir
}
exit 0
