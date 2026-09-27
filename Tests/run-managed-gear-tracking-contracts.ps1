param([string]$CoreSource = '')
$ErrorActionPreference = 'Stop'
$rcRoot = Split-Path $PSScriptRoot -Parent
if (!$CoreSource) { $CoreSource = Join-Path $rcRoot 'Source/Core/AutomaticOutfitManagerGameComponent.cs' }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vsDir = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vsDir 'MSBuild/Current/Bin/Roslyn/csc.exe'
$stem = Join-Path $env:TEMP ('aom-tracking-' + [Guid]::NewGuid().ToString('N'))
try {
    $core = Get-Content -LiteralPath $CoreSource -Raw
    $code = 'using System; using System.Linq; using System.Collections.Generic; using Verse; using RimWorld; using AutomaticOutfitManager.State; using AutomaticOutfitManager.Rules; using AutomaticOutfitManager.Detection; namespace AutomaticOutfitManager.Core { public partial class AutomaticOutfitManagerGameComponent {'
    # Execute the actual completion, Forget and load bodies, including ordering
    # relative to state removal and lazy index invalidation. Native world hooks
    # unrelated to these records are stubbed in the fixture.
    foreach ($signature in @('public void EndIntervention(', 'public bool ForgetManagedStockDefinition(', 'public bool CanForgetManagedStockDefinition(', 'private int PruneUnusedManagedGearIds(', 'private static bool HeldByPawn(', 'public override void LoadedGame(', 'public bool IsManagedApparel(', 'public bool IsManagedWeapon(', 'private void EnsureManagedApparelIndex(', 'private void EnsureManagedWeaponIndex(')) {
        $start = $core.IndexOf($signature)
        if ($start -lt 0 -and $signature -eq 'private int PruneUnusedManagedGearIds(') { continue }
        if ($start -lt 0) { throw "Missing method $signature" }
        $brace = $core.IndexOf('{', $start); $end = $brace + 1; $depth = 1
        while ($depth -gt 0) { if ($core[$end] -eq '{') { $depth++ }; if ($core[$end] -eq '}') { $depth-- }; $end++ }
        $code += $core.Substring($start, $end - $start)
    }
    Set-Content -LiteralPath ($stem + '.cs') -Value ($code + '}}')
    $sources = @(($stem + '.cs'), (Join-Path $PSScriptRoot 'ManagedGearTrackingTests.cs'), (Join-Path $rcRoot 'Source/Storage/ManagedGearTracking.cs'), (Join-Path $rcRoot 'Source/Storage/ManagedApparelClassifier.cs'), (Join-Path $rcRoot 'Source/Storage/ManagedWeaponClassifier.cs'), (Join-Path $rcRoot 'Source/Storage/AutomaticOutfitStorageScope.cs'))
    & $compiler /nologo /target:exe /langversion:latest /warn:0 "/out:$stem.exe" $sources
    if ($LASTEXITCODE -ne 0) { throw 'Tracking fixture compilation failed.' }
    & ($stem + '.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Tracking regression checks failed.' }
} finally {
    foreach ($suffix in @('.cs', '.exe')) { if (Test-Path -LiteralPath ($stem + $suffix)) { Remove-Item -LiteralPath ($stem + $suffix) } }
}
