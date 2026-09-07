$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$stem = Join-Path $env:TEMP ('aom-restoration-tests-' + [Guid]::NewGuid().ToString('N'))
$testOutput = $stem + '.exe'
$planner = $stem + '.cs'
try {
    # Compile the production planner methods verbatim; only unrelated holder/
    # inventory plumbing above BuildJobs is replaced by deterministic stubs.
    $source = Get-Content -LiteralPath (Join-Path $rcRoot 'Source/Detection/RestorationPlanner.cs') -Raw
    $start = $source.IndexOf('        public static List<Job> BuildJobs(')
    if ($start -lt 0) { throw 'Production planner entry point not found.' }
    $prefix = @'
using System.Collections.Generic;
using System.Linq;
using AutomaticOutfitManager.Core;
using AutomaticOutfitManager.Patches;
using AutomaticOutfitManager.Rules;
using AutomaticOutfitManager.State;
using AutomaticOutfitManager.Storage;
using RimWorld;
using Verse;
using Verse.AI;
namespace AutomaticOutfitManager.Detection {
public static class RestorationPlanner {
private const float TatteredHitPointThreshold=0.5f;
private static bool IsHeldByPawn(ThingWithComps t,Pawn p)=>false;
public static bool CanAttemptSavedWeaponEquip(ThingWithComps t,Pawn p,out string reason){reason=null;return !t.Reserved;}
'@
    Set-Content -LiteralPath $planner -Value ($prefix + "`n" + $source.Substring($start))
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$testOutput" $planner (Join-Path $PSScriptRoot 'RestorationContractTests.cs') (Join-Path $rcRoot 'Source/Detection/RestorationJobOrder.cs') (Join-Path $rcRoot 'Source/UI/RestorationActivity.cs') (Join-Path $rcRoot 'Source/Detection/SavedGearRestorationDiagnostics.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Restoration test compilation failed.' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Restoration contracts failed.' }
} finally {
    foreach ($file in @($testOutput,$planner)) { if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
}
exit 0
