$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$stem = Join-Path $env:TEMP ('aom-observed-transition-' + [Guid]::NewGuid().ToString('N'))
$testOutput = $stem + '.exe'
$assignments = $stem + '.cs'
try {
    # Compile the production target-ownership predicates, not copies of their
    # decisions. Portal routing is outside this display regression fixture.
    $source = Get-Content -LiteralPath (Join-Path $rcRoot 'Source/Patches/PawnJobTracker_StartJob_Patch.cs') -Raw
    $apparelStart = $source.IndexOf('        private static bool IsAllowedTransitionWear(')
    $apparelEnd = $source.IndexOf('        private static bool HaulDestinationRejectsGear(', $apparelStart)
    $weaponStart = $source.IndexOf('        internal static bool IsAssignedTransitionWeaponJob(')
    $weaponEnd = $source.IndexOf('        internal static bool IsAssignedCrossMapChangingAreaReturnJob(', $weaponStart)
    if ($apparelStart -lt 0 -or $apparelEnd -le $apparelStart -or $weaponStart -lt 0 -or $weaponEnd -le $weaponStart) { throw 'Assigned transition predicates not found.' }
    $prefix = @'
using AutomaticOutfitManager.Core;using AutomaticOutfitManager.State;using RimWorld;using Verse;using Verse.AI;
namespace AutomaticOutfitManager.Patches { internal static class PawnJobTracker_StartJob_Patch {
private static bool IsAssignedCrossMapChangingAreaReturnJob(PawnApparelState state,Job job)=>false;
'@
    Set-Content -LiteralPath $assignments -Value ($prefix + "`n" + $source.Substring($apparelStart,$apparelEnd-$apparelStart) + $source.Substring($weaponStart,$weaponEnd-$weaponStart) + '}}')
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$testOutput" $assignments (Join-Path $PSScriptRoot 'ObservedTransitionContractTests.cs') (Join-Path $rcRoot 'Source/UI/ObservedOutfitTransition.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Observed transition test compilation failed.' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Observed transition contracts failed.' }
} finally {
    foreach ($file in @($testOutput,$assignments)) { if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file } }
}
exit 0
